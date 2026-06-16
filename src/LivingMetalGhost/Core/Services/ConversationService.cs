using System.Text.RegularExpressions;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationService
{
    private const int MaximumHistoryMessages = 20;
    private const int MaximumHistoryCharacters = 24000;
    private static readonly Regex MoodTagRegex = new(
        @"^\s*\[mood:\s*(?<mood>[a-z0-9_-]+)\s*\]\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly PromptAssembler _promptAssembler;
    private readonly StoryStateStore _storyStateStore;
    private readonly RoleplayStateUpdater _roleplayStateUpdater;
    private readonly List<LlmHistoryMessage> _history = [];
    private readonly Lock _historyLock = new();
    private readonly Dictionary<string, HiddenTraitRuntimeState> _hiddenTraitStates = new(StringComparer.OrdinalIgnoreCase);

    public ConversationService(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory,
        PromptAssembler promptAssembler,
        StoryStateStore storyStateStore,
        RoleplayStateUpdater roleplayStateUpdater)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
        _promptAssembler = promptAssembler;
        _storyStateStore = storyStateStore;
        _roleplayStateUpdater = roleplayStateUpdater;
    }

    public async Task<SkillResult> ChatAsync(string text, bool advanced, CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var mode = advanced
            ? ConversationMode.Advanced
            : storyState.Enabled ? ConversationMode.Story : ConversationMode.Daily;
        var llm = mode == ConversationMode.Advanced ? config.AdvancedLlm : config.Llm;
        var options = LlmOptions.FromSettings(llm);
        var provider = _providerFactory.Create(options.Provider);
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = text,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                BuildHiddenTraitDirective(character)),
            History = GetHistorySnapshot()
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(parsed.Text);
        var characterMood = NormalizeMood(parsed.Mood, character.Visual) ??
                            (response.FromFallback ? "thinking" : "speaking");

        AddToHistory("user", text);
        AddToHistory("assistant", characterText);
        if (mode == ConversationMode.Story)
        {
            _roleplayStateUpdater.UpdateAfterTurn(text, characterText, characterMood);
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
        var mode = storyState.Enabled ? ConversationMode.Story : ConversationMode.Daily;
        // 먼저 말 걸기는 가벼운 기본 대화이므로 항상 기본 llm 설정을 사용한다.
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText = mode == ConversationMode.Story
            ? "현재 roleplay_state에 어울리는 짧은 장면 반응이나 다음 한마디로 이야기를 이어가. 사용자의 행동은 대신 결정하지 마."
            : "지금 상황에 어울리는 짧은 말 한마디로 먼저 대화를 시작해. " +
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
                BuildHiddenTraitDirective(character)),
            History = GetHistorySnapshot()
        }, cancellationToken);
        var parsed = ParseMoodTaggedResponse(response.Text);
        var characterText = PolishCharacterSpeech(parsed.Text);
        var characterMood = NormalizeMood(parsed.Mood, character.Visual) ??
                            (response.FromFallback ? "happy" : "speaking");

        AddToHistory("assistant", characterText);
        if (mode == ConversationMode.Story)
        {
            _roleplayStateUpdater.UpdateAfterTurn(string.Empty, characterText, characterMood);
        }

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = mode == ConversationMode.Story ? "proactive-roleplay" : "proactive-chat",
            UsedLlm = true
        };
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
