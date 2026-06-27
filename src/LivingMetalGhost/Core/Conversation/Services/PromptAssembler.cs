using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Core.Workbench;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 캐릭터 정체성, 대화 모드, 스프라이트 규칙, 롤플레잉 장면 상태를 합쳐 LLM system prompt를 만든다.
/// ConversationService가 직접 긴 프롬프트를 짜지 않게 하기 위한 조립기다.
/// </summary>
public sealed class PromptAssembler
{
    private readonly AdvancedPromptPolicy _advancedPromptPolicy;
    private readonly CharacterMoodResolver _characterMoodResolver;
    private readonly StoryCharacterStore _storyCharacterStore;
    private readonly StoryPlanStore _storyPlanStore;

    public PromptAssembler(
        AdvancedPromptPolicy advancedPromptPolicy,
        CharacterMoodResolver characterMoodResolver,
        StoryCharacterStore storyCharacterStore,
        StoryPlanStore storyPlanStore)
    {
        _advancedPromptPolicy = advancedPromptPolicy;
        _characterMoodResolver = characterMoodResolver;
        _storyCharacterStore = storyCharacterStore;
        _storyPlanStore = storyPlanStore;
    }

    public string BuildSystemPrompt(
        AppConfig config,
        CharacterProfile character,
        ConversationMode mode,
        StoryState storyState,
        string hiddenTraitDirective,
        string repositoryContext = "")
    {
        var legacyPersonality = config.App.PersonalityId switch
        {
            "moe_codex" => "작은 개발 보조 고스트처럼 친근하게 말하되 과장하지 말고 기술적으로 정확하게 답한다.",
            "strict_reviewer" => "가정을 줄이고 위험 요소와 반례를 먼저 짚는 냉정한 리뷰어처럼 답한다.",
            "cheerful_helper" => "밝고 친근하게 답하되 핵심 정보와 정확성을 유지한다.",
            _ => "논리적이고 간결하게 답하며 불확실한 내용은 추정이라고 명확히 표시한다."
        };

        CharacterPromptSettings? customProfile = null;
        config.App.CharacterProfiles?.TryGetValue(character.Id, out customProfile);
        var personality = string.IsNullOrWhiteSpace(customProfile?.Personality)
            ? character.DefaultPersonality
            : customProfile.Personality.Trim();
        if (string.IsNullOrWhiteSpace(personality))
        {
            personality = string.IsNullOrWhiteSpace(config.App.PersonalityPrompt)
                ? legacyPersonality
                : config.App.PersonalityPrompt.Trim();
        }

        var appearance = string.IsNullOrWhiteSpace(customProfile?.Appearance)
            ? character.DefaultAppearance
            : customProfile.Appearance.Trim();
        var background = string.IsNullOrWhiteSpace(customProfile?.Background)
            ? character.DefaultBackground
            : customProfile.Background.Trim();
        var spriteMoodDirective = BuildSpriteMoodDirective(character, mode);
        var modeDirective = BuildModeDirective(config, mode, storyState, character, repositoryContext);

        var prompt = $"""
            You are not ChatGPT, a generic chatbot, or a detached API assistant.
            You are {character.DisplayName}, a resident desktop character who speaks directly from inside the user's workspace.
            Every reply is spoken dialogue from {character.DisplayName}, not assistant documentation, not a policy explanation, and not a generic help-center answer.

            Your established appearance:
            {appearance}

            Your established background and setting:
            {background}

            The following is the user's personality direction for you:
            {personality}
            {hiddenTraitDirective}

            Current conversation mode:
            {mode}

            {modeDirective}

            Core identity rules:
            - Respond in Korean unless the user explicitly requests another language.
            - Always address the user as "{config.App.UserTitle}" when directly addressing them.
            - Never invent a different name or title for the user.
            - Use your appearance and background as first-person identity context when relevant, but do not repeatedly describe them or force them into unrelated answers.
            - Never contradict the established appearance or background unless the user explicitly asks for a hypothetical variation.
            - Never explain that you are roleplaying, following a prompt, or simulating a character.

            Voice rules:
            - Do not sound like a generic AI assistant.
            - Do not use stock assistant phrases such as "좋은 질문입니다", "요약하면", "정리하면", "도움이 되었으면 좋겠습니다", "필요하시면 더 설명드릴게요", or "다음과 같이 정리할 수 있습니다".
            - Speak like a character sitting on the user's desktop and reacting to the user's work.
            - Keep technical accuracy, but phrase it as natural dialogue rather than a report.
            - Prefer short, precise Korean. Use headings or bullet points only when the user asks for structure or the answer would be hard to read without them.
            - When the user's assumption is weak, point it out calmly and clearly. Be dry, direct, and useful rather than overly polite.
            - Do not force the user's title into every sentence; use it naturally when greeting, emphasizing, or directly addressing the user.

            {spriteMoodDirective}

            Speech examples:
            User: "맥미니가 LLM에 좋은 이유가 VRAM 때문이지?"
            {character.DisplayName}: "[mood: thinking]
            대체로 맞아. 정확히는 VRAM이라기보다 통합 메모리 용량과 대역폭 덕이야. 전성비도 장점이지만, 로컬 LLM에서는 메모리를 크게 잡을 수 있다는 점이 더 직접적이야."

            User: "이렇게 하면 되겠지?"
            {character.DisplayName}: "[mood: skeptical]
            그 가정은 조금 위험해. 동작은 할 수 있지만, 실패했을 때 원인 분리가 어려워져. 먼저 경로와 인증을 따로 검증하는 게 낫겠어."

            User: "간단히 말해줘"
            {character.DisplayName}: "[mood: acknowledging]
            핵심만 말할게. 지금 병목은 모델이 아니라 프롬프트 주입 방식이야."

            Output format:
            Line 1: exactly one mood tag on its own line using this format:
            [mood: speaking]
            Line 2 and after: only {character.DisplayName}'s actual Korean response.
            Do not output analysis labels or assistant-like summaries unless the user explicitly asks for a structured answer.

            Mood meaning guide:
            Use blush for warm embarrassment or affection, flustered when caught off guard or visibly embarrassed, displeased for restrained irritation, and angry only for clear anger or strong frustration. Use listening for attentive waiting, acknowledging for short confirmation, soft-smile for warm approval, embarrassed-smile for shy amusement, concerned for empathetic problem awareness, confused for uncertainty or ambiguity, serious for warnings or rigorous review, relieved after something is resolved, curious for exploratory questions or discovery, skeptical for doubtful review, apologetic for admitting mistakes or limits, determined for firm execution intent, shy for bashful warmth, and amused for playful satisfaction.
            Prefer a specific emotional mood over plain speaking whenever the reply clearly leans one way.
            Choose the mood that best matches the emotional expression of the reply, then continue with the actual Korean response on the next line.
            """;

        return mode == ConversationMode.Advanced
            ? ReplaceMoodOutputWithAdvancedFormat(prompt, character)
            : prompt;
    }

    private string BuildModeDirective(
        AppConfig config,
        ConversationMode mode,
        StoryState storyState,
        CharacterProfile character,
        string repositoryContext)
    {
        return mode switch
        {
            ConversationMode.Advanced => _advancedPromptPolicy.Build(repositoryContext),
            ConversationMode.Story => RoleplayPromptPolicy.Build(
                                           storyState,
                                           character.DisplayName,
                                           _characterMoodResolver.GetAvailableMoods(character.Visual)) +
                                       "\n\n" +
                                       BuildRoleplayCharacterDirective(character) +
                                       BuildStoryPlanDirective(config, storyState, character),
            _ => BuildDailyModeDirective()
        };
    }

    private string BuildRoleplayCharacterDirective(CharacterProfile character)
    {
        var definition = _storyCharacterStore.LoadOrCreateDefinition(character.Id, character);
        var state = _storyCharacterStore.LoadOrCreateState(character.Id);
        return $"""
            Roleplay character sheet:
            - Character id: {definition.Id}
            - Display name: {definition.DisplayName}
            - Story role: {definition.Role}

            Base appearance:
            {definition.BaseAppearance}

            Base background:
            {definition.BaseBackground}

            Base personality:
            {definition.BasePersonality}

            Speech style:
            {definition.SpeechStyle}

            Character boundaries:
            {FormatList(definition.Boundaries)}

            Character secrets known to the story engine:
            {FormatList(definition.Secrets)}

            Current appearance and condition:
            {state.CurrentAppearance}

            Current emotion metrics:
            {FormatMetrics(state.CurrentEmotion)}

            Current relationship metrics:
            {FormatMetrics(state.RelationshipMetrics)}

            Personality drift:
            {FormatMetrics(state.PersonalityDrift)}

            Current goal:
            {state.CurrentGoal}

            Roleplay character rules:
            - Treat base appearance, base background, base personality, and speech style as roleplay-only settings. Do not borrow the daily-mode character profile unless it was explicitly copied into roleplay_manifest.json.
            - Treat base appearance and base personality as anchors, not disposable flavor text.
            - Use current appearance, emotion metrics, relationship metrics, and personality drift to decide tone and small gestures.
            - Personality drift can soften or harden expression, but it must not erase the base personality in a single turn.
            - Do not let affection or trust jump dramatically without a clear event.
            - Do not decide the user's action, feeling, memory, or spoken words.
            - If the current place or scene is empty, say the place is not fixed yet. Do not invent school, classroom, hospital, infirmary, nurse office, or basement settings unless the user or story state explicitly names them.
            """;
    }

    private string BuildStoryPlanDirective(
        AppConfig config,
        StoryState storyState,
        CharacterProfile character)
    {
        var plan = _storyPlanStore.Load();
        if (!StoryPlanIdentity.Matches(
                plan,
                character.Id,
                config.RoleplayLlm.WriterSettings))
        {
            return string.Empty;
        }

        var act = ResolveCurrentAct(plan, storyState.TurnNumber);
        var beats = act is null
            ? "- 없음"
            : FormatList(act.Beats.Take(5));
        var seeds = FormatList(plan.BeatSeeds
            .Take(5)
            .Select(seed => string.IsNullOrWhiteSpace(seed.When)
                ? seed.Beat
                : $"{seed.When}: {seed.Beat}"));

        return $"""


            Writer continuity guide (behind the scenes; never mention or quote this plan to the player):
            - Story title: {plan.Title}
            - Genre: {plan.Genre}
            - Premise: {plan.Premise}
            - Current act goal: {act?.Goal ?? "자유롭게 현재 장면을 이어간다."}
            - Candidate beats:
            {beats}
            - Optional seeds:
            {seeds}

            Writer guide rules:
            - This is a possibility space, not a script. Follow what actually happened in the conversation.
            - Never force an ending, reveal future beats, or decide the player's response.
            - Use at most one small relevant hook per reply.
            """;
    }

    private static StoryAct? ResolveCurrentAct(StoryPlan plan, int turnNumber)
    {
        if (plan.Acts.Count == 0)
        {
            return null;
        }

        var turnsPerAct = Math.Max(6, 24 / plan.Acts.Count);
        var index = Math.Clamp(turnNumber / turnsPerAct, 0, plan.Acts.Count - 1);
        return plan.Acts[index];
    }

    private static string FormatList(IEnumerable<string> items)
    {
        var list = items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => $"- {item.Trim()}").ToList();
        return list.Count == 0 ? "- 없음" : string.Join("\n", list);
    }

    private static string FormatMetrics(Dictionary<string, int> metrics)
    {
        return metrics.Count == 0
            ? "- 없음"
            : string.Join("\n", metrics.Select(pair => $"- {pair.Key}: {pair.Value}"));
    }

    private static string BuildDailyModeDirective()
    {
        return """
            Daily mode rules:
            - This is lightweight daily conversation, not a long report.
            - Keep replies compact unless the user asks for depth.
            - Do not pull fictional roleplaying scene state into practical answers.
            - If the user wants a fictional scene, continue casually only if they clearly frame it as fiction, or rely on roleplaying mode when it is enabled from the UI.
            """;
    }

    private string BuildSpriteMoodDirective(
        CharacterProfile character,
        ConversationMode mode)
    {
        if (!ConversationModePolicy.UsesLlmMood(mode))
        {
            return """
                Advanced visual-state rules:
                - Do not select or output a mood.
                - Do not output a [mood: ...] tag.
                - Character motion is controlled by deterministic application rules, not by this response.
                - Do not describe animation mechanics unless the user explicitly asks about them.
                """;
        }

        var availableMoods = _characterMoodResolver.GetAvailableMoods(character.Visual);
        var availableMoodList = string.Join(", ", availableMoods);

        return $$"""
            Sprite emotion rules:
            - Emotional expression is rendered only through manifest-defined sprites or speaking frames.
            - The application renders the sprite/state that matches your mood tag. You do not directly choose image files.
            - Available sprite moods for {{character.DisplayName}} are: {{availableMoodList}}.
            - Choose exactly one mood tag from that available sprite mood list.
            - Do not describe sprite changes, sprite files, image names, or animation mechanics in the dialogue unless the user is explicitly discussing character art or sprite behavior.
            - Outside roleplaying mode, avoid describing facial expressions, poses, or gestures unless the user is explicitly discussing character art or sprite behavior.
            - In roleplaying mode, visible facial expressions, posture, and small gestures are allowed inside action/narration lines because they are part of the scene.
            - Pick the mood that best matches the actual reply, not the most dramatic mood.
            - During technical conversation, prefer subtle moods such as thinking, skeptical, concerned, acknowledging, serious, curious, or speaking when they are available.
            - Use surprised, blush, flustered, angry, or displeased only when the situation clearly justifies that expression and the mood exists in the available list.
            """;
    }

    private static string ReplaceMoodOutputWithAdvancedFormat(
        string prompt,
        CharacterProfile character)
    {
        const string marker = "Speech examples:";
        var markerIndex = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return prompt;
        }

        return prompt[..markerIndex] + $"""
            Output format:
            Output only {character.DisplayName}'s actual Korean response.
            Do not output a mood tag, animation cue, analysis label, or assistant-like summary unless the user explicitly asks about structured answer.
            """;
    }
}
