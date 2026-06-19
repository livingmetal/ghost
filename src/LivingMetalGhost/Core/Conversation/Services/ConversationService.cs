using System.IO;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationService
{
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly PromptAssembler _promptAssembler;
    private readonly StoryStateStore _storyStateStore;
    private readonly RoleplayStateUpdater _roleplayStateUpdater;
    private readonly ConversationHistoryStore _historyStore;
    private readonly HiddenTraitScheduler _hiddenTraitScheduler;
    private readonly AdvancedSessionLogService _advancedSessionLogService;
    private readonly WorkspaceStore _workspaceStore;
    private readonly Core.Workspace.WorkspaceContextBuilder _workspaceContextBuilder;
    public ConversationService(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory,
        PromptAssembler promptAssembler,
        StoryStateStore storyStateStore,
        RoleplayStateUpdater roleplayStateUpdater,
        ConversationHistoryStore historyStore,
        HiddenTraitScheduler hiddenTraitScheduler,
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
        _hiddenTraitScheduler = hiddenTraitScheduler;
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
                _hiddenTraitScheduler.BuildDirective(character, mode),
                repositoryContext),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var storyCleanText = mode == ConversationMode.Story
            ? ConversationResponseParser.StripLegacyRoleplayTags(parsed.Text)
            : parsed.Text;
        var characterText = CharacterSpeechSanitizer.Sanitize(storyCleanText);
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
                _hiddenTraitScheduler.BuildDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var characterText = CharacterSpeechSanitizer.Sanitize(parsed.Text);
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
                _hiddenTraitScheduler.BuildDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var characterText = CharacterSpeechSanitizer.Sanitize(
            ConversationResponseParser.StripLegacyRoleplayTags(parsed.Text));
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

}
