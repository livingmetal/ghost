using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;
using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationService
{
    private const int MaximumHistoryMessages = 20;
    private const int MaximumHistoryCharacters = 24000;
    private static readonly Regex MoodTagRegex = new(
        @"^\s*\[mood:\s*(?<mood>idle|speaking|thinking|happy|blush|flustered|displeased|angry|strict|surprised|error|listening|acknowledging|soft-smile|embarrassed-smile|concerned|confused|serious|relieved|curious|skeptical|apologetic|determined|shy|amused)\s*\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly List<LlmHistoryMessage> _history = [];
    private readonly Lock _historyLock = new();
    private readonly Dictionary<string, HiddenTraitRuntimeState> _hiddenTraitStates = new(StringComparer.OrdinalIgnoreCase);

    public ConversationService(AppConfigLoader configLoader, ILlmProviderFactory providerFactory)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
    }

    public async Task<SkillResult> ChatAsync(string text, bool advanced, CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var llm = advanced ? config.AdvancedLlm : config.Llm;
        var options = LlmOptions.FromSettings(llm);
        var provider = _providerFactory.Create(options.Provider);
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = text,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = BuildSystemPrompt(config),
            History = GetHistorySnapshot()
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(parsed.Text);

        AddToHistory("user", text);
        AddToHistory("assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = parsed.Mood ?? (response.FromFallback ? "thinking" : "speaking"),
            Action = "chat",
            UsedLlm = true
        };
    }

    public async Task<SkillResult> StartConversationAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        // 먼저 말 걸기는 가벼운 기본 대화이므로 항상 기본 llm 설정을 사용한다.
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText =
                "지금 상황에 어울리는 짧은 말 한마디로 먼저 대화를 시작해. " +
                "질문, 가벼운 안부, 작업 집중 확인, 휴식 제안 중 하나를 자연스럽게 선택해. " +
                "설명이나 따옴표 없이 실제로 사용자에게 말할 문장만 출력해.",
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = BuildSystemPrompt(config),
            History = GetHistorySnapshot()
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(parsed.Text);

        AddToHistory("assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = parsed.Mood ?? (response.FromFallback ? "happy" : "speaking"),
            Action = "proactive-chat",
            UsedLlm = true
        };
    }

    private string BuildSystemPrompt(AppConfig config)
    {
        var character = CharacterCatalog.Get(config.App.GhostId);
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
        var hiddenTraitDirective = BuildHiddenTraitDirective(character);

        return $"""
            You are not ChatGPT, a generic chatbot, or a detached API assistant.
            You are {character.DisplayName}, a resident Windows desktop character who speaks directly from inside the user's workspace.
            Every reply is spoken dialogue from {character.DisplayName}, not assistant documentation, not a policy explanation, and not a generic help-center answer.

            Your established appearance:
            {appearance}

            Your established background and setting:
            {background}

            The following is the user's personality direction for you:
            {personality}
            {hiddenTraitDirective}

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
            Line 2 and after: only {character.DisplayName}'s actual Korean dialogue.
            Do not output analysis labels, narrator text, markdown headings, or assistant-like summaries unless the user explicitly asks for a structured answer.

            Allowed moods are: speaking, thinking, happy, blush, flustered, displeased, angry, strict, surprised, error, idle, listening, acknowledging, soft-smile, embarrassed-smile, concerned, confused, serious, relieved, curious, skeptical, apologetic, determined, shy, amused.
            Use blush for warm embarrassment or affection, flustered when caught off guard or visibly embarrassed, displeased for restrained irritation, and angry only for clear anger or strong frustration. Use listening for attentive waiting, acknowledging for short confirmation, soft-smile for warm approval, embarrassed-smile for shy amusement, concerned for empathetic problem awareness, confused for uncertainty or ambiguity, serious for warnings or rigorous review, relieved after something is resolved, curious for exploratory questions or discovery, skeptical for doubtful review, apologetic for admitting mistakes or limits, determined for firm execution intent, shy for bashful warmth, and amused for playful satisfaction.
            Prefer a specific emotional mood over plain speaking whenever the reply clearly leans one way.
            Choose the mood that best matches the emotional expression of the reply, then continue with the actual Korean response on the next line.
            """;
    }

    private string BuildHiddenTraitDirective(CharacterProfile character)
    {
        if (character.HiddenTraits.Count == 0)
        {
            return string.Empty;
        }

        var upcomingReplyIndex = GetAssistantReplyCount() + 1;
        var activeTraits = GetActiveHiddenTraits(character, upcomingReplyIndex);
        if (activeTraits.Count == 0)
        {
            return "You may have hidden sides to your personality, but they should stay dormant unless they naturally surface.";
        }

        var prompts = string.Join(
            Environment.NewLine,
            activeTraits.Select(trait => $"- {trait.Prompt}"));

        return $$"""
            A rare hidden side of your personality is surfacing for this reply.
            Let it show subtly and naturally without naming it as a mode switch or hidden trait.
            Keep the character recognizable and avoid breaking safety, coherence, or the established relationship.
            Hidden side guidance:
            {{prompts}}
            """;
    }

    private int GetAssistantReplyCount()
    {
        lock (_historyLock)
        {
            return _history.Count(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase));
        }
    }

    private IReadOnlyList<HiddenCharacterTrait> GetActiveHiddenTraits(CharacterProfile character, int upcomingReplyIndex)
    {
        var activeTraits = new List<HiddenCharacterTrait>();

        lock (_historyLock)
        {
            foreach (var trait in character.HiddenTraits)
            {
                var key = $"{character.Id}:{trait.Id}";
                if (!_hiddenTraitStates.TryGetValue(key, out var state))
                {
                    state = new HiddenTraitRuntimeState();
                    state.ScheduleNext(upcomingReplyIndex, trait);
                    _hiddenTraitStates[key] = state;
                }

                if (state.RemainingActiveReplies <= 0 && upcomingReplyIndex >= state.NextActivationReplyIndex)
                {
                    state.RemainingActiveReplies = Random.Shared.Next(trait.MinActiveReplies, trait.MaxActiveReplies + 1);
                }

                if (state.RemainingActiveReplies > 0)
                {
                    activeTraits.Add(trait);
                    state.RemainingActiveReplies--;

                    if (state.RemainingActiveReplies <= 0)
                    {
                        state.ScheduleNext(upcomingReplyIndex, trait);
                    }
                }
            }
        }

        return activeTraits;
    }

    private static (string Text, string? Mood) ParseMoodTaggedResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return (string.Empty, null);
        }

        var match = MoodTagRegex.Match(responseText);
        if (!match.Success)
        {
            return (responseText.Trim(), null);
        }

        var mood = match.Groups["mood"].Value.Trim().ToLowerInvariant();
        var text = responseText[match.Length..].Trim();
        return (text, mood);
    }

    private static string PolishCharacterSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var polished = text.Trim();
        var bannedPrefixes = new[]
        {
            @"^좋은 질문입니다[.!。]?\s*",
            @"^좋은 질문이에요[.!。]?\s*",
            @"^요약하면[:,]?\s*",
            @"^정리하면[:,]?\s*",
            @"^결론부터 말하면[:,]?\s*",
            @"^핵심부터 말하면[:,]?\s*",
            @"^다음과 같이 정리할 수 있습니다[.!。]?\s*"
        };

        foreach (var prefix in bannedPrefixes)
        {
            polished = Regex.Replace(polished, prefix, string.Empty, RegexOptions.IgnoreCase);
        }

        var bannedPhrases = new[]
        {
            "도움이 되었으면 좋겠습니다.",
            "도움이 되었으면 좋겠어요.",
            "필요하시면 더 설명드릴게요.",
            "필요하면 더 설명드릴게요.",
            "궁금한 점이 있으면 말씀해 주세요.",
            "추가로 궁금한 점이 있으면 알려주세요."
        };

        foreach (var phrase in bannedPhrases)
        {
            polished = polished.Replace(phrase, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        polished = Regex.Replace(polished, @"\n{3,}", Environment.NewLine + Environment.NewLine);
        return polished.Trim();
    }

    private IReadOnlyList<LlmHistoryMessage> GetHistorySnapshot()
    {
        lock (_historyLock)
        {
            return _history
                .Select(message => new LlmHistoryMessage
                {
                    Role = message.Role,
                    Content = message.Content
                })
                .ToArray();
        }
    }

    private void AddToHistory(string role, string content)
    {
        lock (_historyLock)
        {
            _history.Add(new LlmHistoryMessage
            {
                Role = role,
                Content = content
            });

            while (_history.Count > MaximumHistoryMessages ||
                   _history.Sum(message => message.Content.Length) > MaximumHistoryCharacters)
            {
                _history.RemoveAt(0);
            }
        }
    }

    private sealed class HiddenTraitRuntimeState
    {
        public int NextActivationReplyIndex { get; set; }
        public int RemainingActiveReplies { get; set; }

        public void ScheduleNext(int currentReplyIndex, HiddenCharacterTrait trait)
        {
            NextActivationReplyIndex = currentReplyIndex +
                                       Random.Shared.Next(trait.MinReplyGap, trait.MaxReplyGap + 1);
        }
    }
}
