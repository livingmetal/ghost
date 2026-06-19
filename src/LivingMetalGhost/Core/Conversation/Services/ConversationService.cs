using System.IO;
using System.Text.RegularExpressions;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationService
{
    private static readonly Regex MoodTagRegex = new(
        @"^\s*\[mood:\s*(?<mood>[a-z0-9_-]+)\s*\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly PromptAssembler _promptAssembler;
    private readonly StoryStateStore _storyStateStore;
    private readonly RoleplayStateUpdater _roleplayStateUpdater;
    private readonly ConversationHistoryStore _historyStore;
    private readonly AdvancedSessionLogService _advancedSessionLogService;
    private readonly WorkspaceStore _workspaceStore;
    private readonly Core.Workspace.WorkspaceContextBuilder _workspaceContextBuilder;
    private readonly Lock _hiddenTraitLock = new();
    private readonly Dictionary<string, HiddenTraitRuntimeState> _hiddenTraitStates = new(StringComparer.OrdinalIgnoreCase);

    public ConversationService(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory,
        PromptAssembler promptAssembler,
        StoryStateStore storyStateStore,
        RoleplayStateUpdater roleplayStateUpdater,
        ConversationHistoryStore historyStore,
        AdvancedSessionLogService advancedSessionLogService,
        WorkspaceStore workspaceStore,
        Core.Workspace.WorkspaceContextBuilder workspaceContextBuilder)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
        _promptAssembler = promptAssembler;
        _storyStateStore = storyStateStore;
        _roleplayStateUpdater = roleplayStateUpdater;
        _historyStore = historyStore;
        _advancedSessionLogService = advancedSessionLogService;
        _workspaceStore = workspaceStore;
        _workspaceContextBuilder = workspaceContextBuilder;
    }

    private string BuildRepositoryContext(ConversationMode mode, string userText)
    {
        if (mode != ConversationMode.Advanced)
        {
            return string.Empty;
        }

        try
        {
            var root = _workspaceStore.Load().RootPath;
            return string.IsNullOrWhiteSpace(root)
                ? string.Empty
                : _workspaceContextBuilder.Build(root, userText);
        }
        catch
        {
            return string.Empty;
        }
    }

    public Task<SkillResult> ChatAsync(string text, bool advanced, CancellationToken cancellationToken)
    {
        var mode = advanced ? ConversationMode.Advanced : ConversationMode.Daily;
        return ChatAsync(text, mode, cancellationToken);
    }

    public Task<SkillResult> RoleplayAsync(string text, CancellationToken cancellationToken)
    {
        return ChatAsync(text, ConversationMode.Story, cancellationToken);
    }

    public void RememberExternalTurn(
        ConversationMode mode,
        string userText,
        string assistantText,
        string source)
    {
        if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(assistantText))
        {
            return;
        }

        var normalizedSource = string.IsNullOrWhiteSpace(source)
            ? "external"
            : source.Trim();
        var rememberedAssistantText = string.IsNullOrWhiteSpace(assistantText)
            ? $"[{normalizedSource} result]{Environment.NewLine}(no text returned)"
            : $"[{normalizedSource} result]{Environment.NewLine}{assistantText.Trim()}";

        if (!string.IsNullOrWhiteSpace(userText))
        {
            _historyStore.Add(mode, "user", userText.Trim());
        }

        _historyStore.Add(mode, "assistant", rememberedAssistantText);
    }

    private async Task<SkillResult> ChatAsync(string text, ConversationMode mode, CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var userTextForProvider = mode == ConversationMode.Story
            ? RoleplayInputFormatter.FormatForPrompt(text)
            : text;
        var llm = mode == ConversationMode.Advanced ? config.AdvancedLlm : config.Llm;
        var options = LlmOptions.FromSettings(llm);
        var repositoryContext = BuildRepositoryContext(mode, text);
        var provider = _providerFactory.Create(options.Provider);
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userTextForProvider,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                BuildHiddenTraitDirective(character, mode),
                repositoryContext),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var storyCleanText = mode == ConversationMode.Story
            ? StripLegacyStoryTags(parsed.Text)
            : parsed.Text;
        var characterText = PolishCharacterSpeech(storyCleanText);
        var characterMood = ResolveResponseMood(
            mode,
            parsed.Mood,
            character.Visual);

        _historyStore.Add(mode, "user", userTextForProvider);
        _historyStore.Add(mode, "assistant", characterText);
        if (mode == ConversationMode.Story)
        {
            _roleplayStateUpdater.UpdateAfterTurn(text, characterText, characterMood);
            await MaybeDigestStoryMemoryAsync(options, cancellationToken);
        }
        else if (mode == ConversationMode.Advanced)
        {
            await _advancedSessionLogService.AppendTurnAsync(new AdvancedSessionLogEntry
            {
                Provider = options.Provider,
                Model = options.Model,
                CharacterId = character.Id,
                CharacterName = character.DisplayName,
                UserText = text,
                AssistantText = characterText,
                Mood = characterMood,
                Action = "advanced-chat",
                UsedContext = GetAdvancedUsedContextLabels()
            }, cancellationToken);
        }

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = mode == ConversationMode.Story ? "roleplay-chat" : "chat",
            UsedLlm = true
        };
    }

    public async Task<SkillResult> StartConversationAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var mode = ConversationMode.Daily;
        // 먼저 말 걸기는 가벼운 기본 대화이므로 항상 기본 llm 설정을 사용한다.
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText = "지금 상황에 어울리는 짧은 말 한마디로 먼저 대화를 시작해. " +
                       "질문, 가벼운 안부, 작업 집중 확인, 휴식 제안 중 하나를 자연스럽게 선택해. " +
                       "설명이나 따옴표 없이 실제로 사용자에게 말할 문장만 출력해.";
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userText,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                BuildHiddenTraitDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(parsed.Text);
        var characterMood = ResolveResponseMood(
            mode,
            parsed.Mood,
            character.Visual);

        _historyStore.Add(mode, "assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = "proactive-chat",
            UsedLlm = true
        };
    }

    private const int StoryDigestEveryTurns = 6;

    // N턴마다 최근 기억을 LLM으로 요약해 StoryState.Facts(관계 텍스처/미해결 질문 등)를 갱신한다.
    // 실패는 조용히 무시하고 기존 facts를 유지한다(추가 LLM 호출 1회 비용).
    private async Task MaybeDigestStoryMemoryAsync(LlmOptions options, CancellationToken cancellationToken)
    {
        var turnCount = _storyStateStore.CountMemoryEntries();
        if (turnCount == 0 || turnCount % StoryDigestEveryTurns != 0)
        {
            return;
        }

        try
        {
            var state = _storyStateStore.Load();
            var recent = _storyStateStore.ReadRecentMemory(StoryDigestEveryTurns);
            if (recent.Count == 0)
            {
                return;
            }

            var existingFacts = string.Join(
                Environment.NewLine,
                state.Facts.Select(fact => $"- ({fact.Kind}, w{fact.Weight}) {fact.Text}"));
            var recentTurns = string.Join(
                Environment.NewLine,
                recent.Select(entry => $"User: {entry.UserText}\nCharacter: {entry.AssistantText}"));

            var systemPrompt = """
                You maintain a compact fictional-roleplay memory. You are not a character; you only summarize.
                Given existing memory facts and the most recent turns, return an UPDATED fact list.
                Output rules:
                - Output a JSON array only. No prose, no code fences.
                - Each item: {"kind": "...", "text": "...", "weight": 1-5}.
                - kind is one of: premise, self, relationship, question.
                - Keep premise and self facts stable. Update relationship texture and open questions from recent events.
                - Merge duplicates. Keep at most 8 facts. Write text in Korean, one short sentence each.
                """;
            var userText = $"""
                Existing facts:
                {existingFacts}

                Recent turns:
                {recentTurns}

                Return the updated JSON fact array.
                """;

            var provider = _providerFactory.Create(options.Provider);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                UserText = userText,
                UserTitle = string.Empty,
                Model = options.Model,
                Options = options,
                SystemPrompt = systemPrompt,
                History = []
            }, cancellationToken);

            var digested = StoryMemoryDigestParser.Parse(response.Text);
            if (digested.Count == 0)
            {
                return;
            }

            // 최신 상태를 다시 읽어 덮어쓰기 경합을 줄이고 facts만 교체한다.
            var latest = _storyStateStore.Load();
            latest.Facts = digested
                .Select(fact => new StoryMemoryFact { Kind = fact.Kind, Text = fact.Text, Weight = fact.Weight })
                .ToList();
            _storyStateStore.Save(latest);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // 기억 요약은 보조 기능이므로 실패해도 대화 흐름을 막지 않는다.
        }
    }

    /// <summary>스토리 모드 idle 비트: 사용자 입력 없이 짧은 존재감만 보여준다. 플롯은 진행하지 않는다.</summary>
    public async Task<SkillResult> StartStoryIdleAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        const ConversationMode mode = ConversationMode.Story;
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText =
            "지금은 사용자의 입력이 없는 조용한 순간이야. 짧은 '존재감' 비트를 보여줘. " +
            "**행동/지문**을 한두 개와 짧은 혼잣말 한마디 정도로만 표현해. " +
            "큰 사건이나 플롯을 진행하지 말고, 사용자의 행동·말·감정을 대신 정하지 마. " +
            "메뉴, 선택지, 시스템 언급은 금지. 장면을 크게 바꾸지 말고 분위기만 가볍게 살려.";
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userText,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                BuildHiddenTraitDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(StripLegacyStoryTags(parsed.Text));
        var characterMood = ResolveResponseMood(
            mode,
            parsed.Mood,
            character.Visual);

        // idle 비트는 연속성을 위해 히스토리에만 남기고 StoryState(장면/요약)는 바꾸지 않는다.
        _historyStore.Add(mode, "assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = "story-idle",
            UsedLlm = true
        };
    }

    private IReadOnlyList<string> GetAdvancedUsedContextLabels()
    {
        var labels = new List<string>();
        if (File.Exists(_advancedSessionLogService.PinnedContextFile))
        {
            labels.Add("pinned_context");
        }

        if (File.Exists(_advancedSessionLogService.ProjectMemoryFile))
        {
            labels.Add("project_memory");
        }

        return labels;
    }

    private string BuildHiddenTraitDirective(CharacterProfile character, ConversationMode mode)
    {
        if (character.HiddenTraits.Count == 0)
        {
            return string.Empty;
        }

        var upcomingReplyIndex = GetAssistantReplyCount(mode) + 1;
        var activeTraits = GetActiveHiddenTraits(character, mode, upcomingReplyIndex);
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

    private int GetAssistantReplyCount(ConversationMode mode)
    {
        return _historyStore.CountByRole(mode, "assistant");
    }

    private IReadOnlyList<HiddenCharacterTrait> GetActiveHiddenTraits(
        CharacterProfile character,
        ConversationMode mode,
        int upcomingReplyIndex)
    {
        var activeTraits = new List<HiddenCharacterTrait>();

        lock (_hiddenTraitLock)
        {
            foreach (var trait in character.HiddenTraits)
            {
                var historyChannel = ConversationModePolicy.GetHistoryChannel(mode);
                var key = $"{historyChannel}:{character.Id}:{trait.Id}";
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

    private static string StripLegacyStoryTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(text, @"\[story:\s*[^\]]*\]", string.Empty, RegexOptions.IgnoreCase);
        return Regex.Replace(cleaned, @"\n{3,}", Environment.NewLine + Environment.NewLine).Trim();
    }

    private static string? NormalizeMood(string? mood, CharacterVisualProfile visual)
    {
        if (string.IsNullOrWhiteSpace(mood))
        {
            return null;
        }

        var normalized = mood.Trim().ToLowerInvariant();
        return GetAvailableSpriteMoods(visual).Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : null;
    }

    private static string ResolveResponseMood(
        ConversationMode mode,
        string? requestedMood,
        CharacterVisualProfile visual)
    {
        var normalizedMood = NormalizeMood(requestedMood, visual);
        return CharacterExpressionPolicy.ResolveResponseState(normalizedMood, mode);
    }

    private static IReadOnlyList<string> GetAvailableSpriteMoods(CharacterVisualProfile visual)
    {
        var moods = new List<string>();

        if (visual is ModularCharacterVisualProfile modular)
        {
            moods.AddRange(modular.States.Keys
                .Where(mood => !string.IsNullOrWhiteSpace(mood))
                .Where(mood => !string.Equals(mood, modular.BlinkStateName, StringComparison.OrdinalIgnoreCase)));

            if (modular.SpeakingStates.Count > 0 || modular.SpeakingStatesByState.Count > 0)
            {
                moods.Add("speaking");
            }
        }
        else if (visual is SpriteCharacterVisualProfile sprite)
        {
            if (!string.IsNullOrWhiteSpace(sprite.IdleSpritePath))
            {
                moods.Add("idle");
            }

            moods.AddRange(sprite.MoodSpritePaths.Keys);
            moods.AddRange(sprite.MoodBlinkSpritePaths.Keys);
            moods.AddRange(sprite.MoodCycleSpritePaths.Keys);

            if (sprite.SpeakingSpritePaths.Count > 0)
            {
                moods.Add("speaking");
            }
        }

        if (moods.Count == 0)
        {
            moods.Add("speaking");
        }

        return moods
            .Where(mood => !string.IsNullOrWhiteSpace(mood))
            .Select(mood => mood.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(mood => mood, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
