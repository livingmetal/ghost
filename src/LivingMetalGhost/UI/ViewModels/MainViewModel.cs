using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Providers.Llm;
using LivingMetalGhost.Skills;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppConfigLoader _configLoader;
    private readonly IntentRouter _intentRouter;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ConversationService _conversationService;
    private readonly ConversationLogService _conversationLogService;
    private readonly SpriteDirector _spriteDirector;
    private readonly StoryStateStore _storyStateStore;
    private bool _isResponding;
    private bool _isStoryResponding;
    private CancellationTokenSource? _moodHoldCts;

    [ObservableProperty]
    private string bubbleText = "기다리고 있어요.";

    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private string storyInputText = string.Empty;

    [ObservableProperty]
    private string characterMood = "idle";

    [ObservableProperty]
    private string characterStateLabel = "IDLE";

    [ObservableProperty]
    private bool isCharacterSpeaking;

    [ObservableProperty]
    private string selectedCharacterId = "ssuang";

    [ObservableProperty]
    private string characterDisplayName = "쑝";

    [ObservableProperty]
    private string selectedCharacterSizePresetId = "normal";

    [ObservableProperty]
    private double characterScale = 1.0;

    [ObservableProperty]
    private string selectedCharacterFramingPresetId = "full-body";

    [ObservableProperty]
    private bool isAdvancedMode;

    [ObservableProperty]
    private bool isStoryMode;

    [ObservableProperty]
    private bool isLocalLmAvailable;

    /// <summary>
    /// 현재 설정된 고급 대화 provider 를 기준으로 고급 모드 토글 가능 여부.
    /// lmbot: 로컬 CLI 감지 결과 / 그 외(API 기반): 항상 true.
    /// </summary>
    [ObservableProperty]
    private bool isAdvancedModeAvailable = true;

    [ObservableProperty]
    private int proactiveSettingsRevision;

    public MainViewModel(
        AppConfigLoader configLoader,
        IntentRouter intentRouter,
        SettingsViewModel settingsViewModel,
        ConversationService conversationService,
        ConversationLogService conversationLogService,
        SpriteDirector spriteDirector,
        StoryStateStore storyStateStore)
    {
        _configLoader = configLoader;
        _intentRouter = intentRouter;
        _settingsViewModel = settingsViewModel;
        _conversationService = conversationService;
        _conversationLogService = conversationLogService;
        _spriteDirector = spriteDirector;
        _storyStateStore = storyStateStore;
        IsStoryMode = _storyStateStore.Load().Enabled;
        RefreshSelectedCharacter();
        _ = RefreshLocalLmAvailabilityAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<ChatMessage> StoryMessages { get; } = [];

    /// <summary>고급 Workbench / Agent Dock 에 표시할 현재 작업들.</summary>
    public ObservableCollection<AgentJob> ActiveAgentJobs { get; } = [];

    public ConversationMode CurrentMode => IsAdvancedMode
        ? ConversationMode.Advanced
        : ConversationMode.Daily;

    public string ActiveProviderLabel => BuildProviderLabel(CurrentMode);

    public string StoryProviderLabel => BuildProviderLabel(ConversationMode.Story)
        .Replace("STORY:", "ROLEPLAY:", StringComparison.OrdinalIgnoreCase);

    public async Task RefreshLocalLmAvailabilityAsync()
    {
        var advancedProvider = _configLoader.Load().AdvancedLlm.Provider;
        var isInstalledApps =
            string.Equals(advancedProvider, "installed-apps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(advancedProvider, "installed_apps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(advancedProvider, "apps", StringComparison.OrdinalIgnoreCase);
        var needsLocalLm = string.Equals(advancedProvider, "lmbot", StringComparison.OrdinalIgnoreCase);

        if (isInstalledApps)
        {
            InstalledAppDetector.Invalidate();
            var info = await InstalledAppDetector.DetectAsync();
            IsLocalLmAvailable = info.IsAnyAvailable;
            IsAdvancedModeAvailable = info.IsAnyAvailable;
        }
        else if (needsLocalLm)
        {
            LocalLmDetector.Invalidate();
            var info = await LocalLmDetector.DetectAsync();
            IsLocalLmAvailable = info.IsAvailable;
            IsAdvancedModeAvailable = info.IsAvailable;
        }
        else
        {
            IsLocalLmAvailable = false;
            IsAdvancedModeAvailable = true;
        }

        if (!IsAdvancedModeAvailable && IsAdvancedMode)
        {
            IsAdvancedMode = false;
        }
    }

    private string BuildProviderLabel(ConversationMode mode)
    {
        var config = _configLoader.Load();
        var llm = mode == ConversationMode.Advanced ? config.AdvancedLlm : config.Llm;
        var modeLabel = mode switch
        {
            ConversationMode.Advanced => "ADVANCED",
            ConversationMode.Story => "STORY",
            _ => "DAILY"
        };
        var model = string.IsNullOrWhiteSpace(llm.Model) ? "(default)" : llm.Model;
        return $"{modeLabel}: {llm.Provider} / {model}";
    }

    public (bool Enabled, int MinMinutes, int MaxMinutes) GetProactiveChatSettings()
    {
        var config = _configLoader.Load();
        var legacyInterval = Math.Clamp(config.App.ProactiveChatIntervalMinutes, 5, 240);
        var minMinutes = config.App.ProactiveChatMinMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMinMinutes, 5, 240);
        var maxMinutes = config.App.ProactiveChatMaxMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMaxMinutes, 5, 240);

        return (
            config.App.EnableProactiveChat,
            Math.Min(minMinutes, maxMinutes),
            Math.Max(minMinutes, maxMinutes));
    }

    public void SaveCharacterScale()
    {
        var config = _configLoader.Load();
        config.App.CharacterProfiles ??= [];
        var character = CharacterCatalog.Get(SelectedCharacterId);
        if (!config.App.CharacterProfiles.TryGetValue(character.Id, out var profile))
        {
            profile = new CharacterPromptSettings();
            config.App.CharacterProfiles[character.Id] = profile;
        }

        profile.CharacterScale = Math.Clamp(CharacterScale, 0.55, 2.0);
        _configLoader.Save(config);
    }

    public void SetAdvancedMode(bool enabled)
    {
        if (enabled && !IsAdvancedModeAvailable)
        {
            IsAdvancedMode = false;
            return;
        }

        IsAdvancedMode = enabled;
    }

    public void ToggleAdvancedMode()
    {
        SetAdvancedMode(!IsAdvancedMode);
    }

    public void SetStoryMode(bool enabled)
    {
        var wasEnabled = IsStoryMode;
        var state = _storyStateStore.SetEnabled(enabled);
        IsStoryMode = enabled;

        if (enabled && !wasEnabled)
        {
            ShowRoleplayOpening(state);
        }
    }

    public void ToggleStoryMode()
    {
        SetStoryMode(!IsStoryMode);
    }

    public string GetRoleplayStateSummary()
    {
        var state = _storyStateStore.Load();
        var objectives = state.Objectives.Count == 0
            ? "- 목표 없음"
            : string.Join(
                Environment.NewLine,
                state.Objectives.Select(objective => $"- {(objective.Done ? "완료" : "진행 중")} · {objective.Text}"));
        var memoryCount = _storyStateStore.CountMemoryEntries();

        return $"""
            상태: {(IsStoryMode ? "켜짐" : "꺼짐")}
            제목: {state.Title}
            플레이어: {state.PlayerRole}
            분위기: {state.Mood}
            긴장도: {state.Tension}/5
            기억 항목: {memoryCount}
            마지막 갱신: {state.UpdatedAt:yyyy-MM-dd HH:mm:ss}

            요약:
            {state.Summary}

            목표:
            {objectives}
            """.Trim();
    }

    public void ResetRoleplayState()
    {
        var resetState = _storyStateStore.Reset(IsStoryMode);
        StoryMessages.Clear();
        if (IsStoryMode)
        {
            ShowRoleplayOpening(resetState);
        }
    }

    private void ShowRoleplayOpening(StoryState state)
    {
        var openingText = StoryStateStore.BuildOpeningText(state);
        StoryMessages.Add(new ChatMessage
        {
            Text = openingText,
            SpeakerName = "STORY",
            IsProactive = true,
            IsRoleplay = true
        });
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        RefreshModePresentation();
    }

    partial void OnIsStoryModeChanged(bool value)
    {
        RefreshModePresentation();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_isResponding)
        {
            return;
        }

        var request = new UserRequest { RawText = InputText.Trim(), UseAdvancedModel = IsAdvancedMode };
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return;
        }

        CancelMoodHold();
        SetCharacterMood(_spriteDirector.ResolveThinkingMood(CurrentMode));
        _isResponding = true;
        Messages.Add(new ChatMessage
        {
            Text = request.RawText,
            SpeakerName = "YOU",
            IsUser = true,
            IsRoleplay = false
        });
        InputText = string.Empty;

        try
        {
            var skill = _intentRouter.Route(request);
            var result = await skill.HandleAsync(request, CancellationToken.None);
            BubbleText = result.BubbleText;
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, CurrentMode);
            SetCharacterMood(assistantMood);
            CaptureAgentJobs(request, result);
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: false, assistantMood);
            await WriteLogAsync(request.RawText, result.BubbleText, isProactive: false, assistantMood);
            DispatchAppCommand(result.Action);
        }
        catch (Exception ex)
        {
            BubbleText = $"요청을 처리하지 못했어요: {ex.Message}";
            IsCharacterSpeaking = false;
            SetCharacterMood("error");
            Messages.Add(new ChatMessage
            {
                Text = BubbleText,
                SpeakerName = CharacterDisplayName.ToUpperInvariant()
            });
        }
        finally
        {
            _isResponding = false;
        }
    }

    [RelayCommand]
    private async Task StorySendAsync()
    {
        if (_isStoryResponding)
        {
            return;
        }

        var rawText = StoryInputText.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        _isStoryResponding = true;
        StoryMessages.Add(new ChatMessage
        {
            Text = rawText,
            SpeakerName = "YOU",
            IsUser = true,
            IsRoleplay = true
        });
        StoryInputText = string.Empty;

        try
        {
            var result = await _conversationService.RoleplayAsync(rawText, CancellationToken.None);
            await DisplayAssistantResponseAsync(
                result.BubbleText,
                false,
                result.Mood,
                StoryMessages,
                ConversationMode.Story,
                false);
            await WriteLogAsync(
                rawText,
                result.BubbleText,
                false,
                result.Mood,
                ConversationMode.Story);
        }
        catch (Exception ex)
        {
            StoryMessages.Add(new ChatMessage
            {
                Text = $"롤플레잉 응답을 처리하지 못했어요: {ex.Message}",
                SpeakerName = "SYSTEM",
                IsRoleplay = true
            });
        }
        finally
        {
            _isStoryResponding = false;
        }
    }

    // AppCommandSkill 이 돌려준 Action 값을 실제 동작으로 연결한다.
    // 비파괴적 명령만 즉시 처리하고, 종료처럼 위험한 명령은 의도적으로 보류한다.
    private void DispatchAppCommand(string? action)
    {
        switch (action)
        {
            case AppCommandActions.OpenSettings:
                OpenSettings();
                break;
            case AppCommandActions.OpenLog:
                OpenConversationLog();
                break;
            // TODO: AppCommandActions.ExitApp 는 확인 절차를 둔 뒤 Application.Current.Shutdown() 연결.
        }
    }

    public async Task StartConversationAsync()
    {
        if (_isResponding)
        {
            return;
        }

        CancelMoodHold();
        _isResponding = true;
        SetCharacterMood(_spriteDirector.ResolveThinkingMood(CurrentMode));

        try
        {
            var result = await _conversationService.StartConversationAsync(CancellationToken.None);
            BubbleText = result.BubbleText;
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, CurrentMode);
            SetCharacterMood(assistantMood);
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: true, assistantMood);
            await WriteLogAsync(string.Empty, result.BubbleText, isProactive: true, assistantMood);
        }
        catch (Exception ex)
        {
            BubbleText = $"먼저 말을 걸지 못했어요: {ex.Message}";
            IsCharacterSpeaking = false;
            SetCharacterMood("error");
        }
        finally
        {
            _isResponding = false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new UI.Views.SettingsWindow
        {
            DataContext = _settingsViewModel,
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        RefreshSelectedCharacter();
        OnPropertyChanged(nameof(ActiveProviderLabel));
        OnPropertyChanged(nameof(StoryProviderLabel));
        ProactiveSettingsRevision++;
    }

    public void OpenConversationLog()
    {
        var window = new UI.Views.ConversationLogWindow
        {
            DataContext = new ConversationLogViewModel(_conversationLogService),
            Owner = Application.Current.MainWindow
        };
        window.Show();
    }

    private Task DisplayAssistantResponseAsync(string response, bool isProactive, string assistantMood)
    {
        return DisplayAssistantResponseAsync(
            response,
            isProactive,
            assistantMood,
            Messages,
            CurrentMode,
            animateCharacter: true);
    }

    private async Task DisplayAssistantResponseAsync(
        string response,
        bool isProactive,
        string assistantMood,
        ObservableCollection<ChatMessage> targetMessages,
        ConversationMode mode,
        bool animateCharacter)
    {
        var compact = mode != ConversationMode.Advanced;
        var chunks = SplitForPacedDisplay(response, compact);
        if (animateCharacter)
        {
            IsCharacterSpeaking = true;
        }

        try
        {
            for (var index = 0; index < chunks.Count; index++)
            {
                var message = new ChatMessage
                {
                    SpeakerName = isProactive
                        ? $"{CharacterDisplayName.ToUpperInvariant()} • 먼저 말 걸기"
                        : CharacterDisplayName.ToUpperInvariant(),
                    IsProactive = isProactive && index == 0,
                    IsRoleplay = mode == ConversationMode.Story,
                    IsTyping = true
                };
                targetMessages.Add(message);
                await TypeMessageAsync(message, chunks[index]);

                if (index + 1 < chunks.Count)
                {
                    await Task.Delay(compact ? 220 : 320);
                }
            }
        }
        finally
        {
            if (animateCharacter)
            {
                IsCharacterSpeaking = false;
            }
        }

        if (animateCharacter)
        {
            StartPostSpeechMoodHold(assistantMood);
        }
    }

    private void RefreshModePresentation()
    {
        OnPropertyChanged(nameof(CurrentMode));
        OnPropertyChanged(nameof(ActiveProviderLabel));
        OnPropertyChanged(nameof(StoryProviderLabel));

        if (!_isResponding && !IsCharacterSpeaking)
        {
            var restingMood = _spriteDirector.ResolveRestingMood(CurrentMode);
            SetCharacterMood(restingMood);
        }
    }

    private void SetCharacterMood(string mood)
    {
        CharacterMood = mood;
        CharacterStateLabel = _spriteDirector.ToStateLabel(mood, CurrentMode);
    }

    private void StartPostSpeechMoodHold(string mood)
    {
        CancelMoodHold();
        var cts = new CancellationTokenSource();
        _moodHoldCts = cts;
        _ = HoldMoodThenRestAsync(mood, CurrentMode, cts.Token);
    }

    private async Task HoldMoodThenRestAsync(string mood, ConversationMode mode, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_spriteDirector.GetPostSpeechHoldMilliseconds(mood, mode), ct);
            if (!ct.IsCancellationRequested && !_isResponding && !IsCharacterSpeaking)
            {
                SetCharacterMood(_spriteDirector.ResolveRestingMood(CurrentMode));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelMoodHold()
    {
        if (_moodHoldCts is null)
        {
            return;
        }

        _moodHoldCts.Cancel();
        _moodHoldCts.Dispose();
        _moodHoldCts = null;
    }

    private void CaptureAgentJobs(UserRequest request, SkillResult result)
    {
        if (result.Action is "advanced-coding-duo-suggested")
        {
            ActiveAgentJobs.Add(new AgentJob
            {
                AgentType = "codex-cli",
                DisplayName = "Codex",
                Title = request.RawText,
                Summary = "코드 분석 및 리팩터링 제안 생성",
                Status = AgentJobStatus.WaitingApproval,
                Progress = 1.0,
                RequiresApproval = true
            });
            ActiveAgentJobs.Add(new AgentJob
            {
                AgentType = "claude-code",
                DisplayName = "Claude Code",
                Title = request.RawText,
                Summary = "보안/구조 검토 제안 생성",
                Status = AgentJobStatus.WaitingApproval,
                Progress = 1.0,
                RequiresApproval = true
            });
        }
        else if (result.Action is "advanced-task-suggested")
        {
            ActiveAgentJobs.Add(new AgentJob
            {
                AgentType = "agent",
                DisplayName = "Agent",
                Title = request.RawText,
                Summary = "고급 작업 승인을 기다리는 중",
                Status = AgentJobStatus.WaitingApproval,
                Progress = 0.25,
                RequiresApproval = true
            });
        }

        TrimAgentJobs();
    }

    private void TrimAgentJobs()
    {
        const int maximumVisibleJobs = 4;
        while (ActiveAgentJobs.Count > maximumVisibleJobs)
        {
            ActiveAgentJobs.RemoveAt(0);
        }
    }

    private static async Task TypeMessageAsync(ChatMessage message, string text)
    {
        var elements = GetTextElements(text);
        var batchSize = elements.Count switch
        {
            > 1200 => 5,
            > 700 => 3,
            > 350 => 2,
            _ => 1
        };
        var baseDelayMilliseconds = elements.Count switch
        {
            > 1200 => 8,
            > 700 => 11,
            > 350 => 16,
            _ => 24
        };
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < elements.Count;)
        {
            var currentBatchSize = Math.Min(batchSize, elements.Count - index);
            string lastElement = string.Empty;
            for (var batchIndex = 0; batchIndex < currentBatchSize; batchIndex++)
            {
                lastElement = elements[index++];
                builder.Append(lastElement);
            }

            message.Text = builder.ToString();
            var delay = baseDelayMilliseconds + GetNaturalPauseMilliseconds(lastElement);
            await Task.Delay(delay);
        }

        message.IsTyping = false;
    }

    private static IReadOnlyList<string> GetTextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static int GetNaturalPauseMilliseconds(string textElement)
    {
        return textElement switch
        {
            "." or "!" or "?" or "…" => 180,
            "," or ";" or ":" => 80,
            "\n" or "\r\n" => 220,
            _ => 0
        };
    }

    private static IReadOnlyList<string> SplitForPacedDisplay(string response, bool compact)
    {
        var targetLength = compact ? 80 : 130;
        var maximumLength = compact ? 140 : 220;
        var normalized = response.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maximumLength)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var remaining = normalized;
        while (remaining.Length > 0)
        {
            if (remaining.Length <= maximumLength)
            {
                chunks.Add(remaining.Trim());
                break;
            }

            var splitIndex = FindSplitIndex(remaining, targetLength, maximumLength);
            chunks.Add(remaining[..splitIndex].Trim());
            remaining = remaining[splitIndex..].TrimStart();
        }

        return chunks;
    }

    private static int FindSplitIndex(string text, int targetLength, int maximumLength)
    {
        var searchLength = Math.Min(maximumLength, text.Length);
        var preferredSeparators = new[] { "\n\n", "\n", ". ", "! ", "? ", "。", "！", "？", ", ", " " };
        var bestIndex = -1;
        var bestDistance = int.MaxValue;

        foreach (var separator in preferredSeparators)
        {
            var index = text.LastIndexOf(separator, searchLength - 1, StringComparison.Ordinal);
            if (index <= 20)
            {
                continue;
            }

            var candidate = index + separator.Length;
            var distance = Math.Abs(candidate - targetLength);
            if (distance < bestDistance)
            {
                bestIndex = candidate;
                bestDistance = distance;
            }
        }

        return bestIndex > 0 ? bestIndex : maximumLength;
    }

    private async Task WriteLogAsync(
        string userText,
        string assistantText,
        bool isProactive,
        string mood = "speaking",
        ConversationMode? modeOverride = null)
    {
        var config = _configLoader.Load();
        var mode = modeOverride ?? CurrentMode;
        var llm = mode == ConversationMode.Advanced ? config.AdvancedLlm : config.Llm;

        await _conversationLogService.AppendAsync(new ConversationLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            UserText = userText,
            AssistantText = assistantText,
            Provider = llm.Provider,
            Model = llm.Model,
            IsProactive = isProactive,
            CharacterId = SelectedCharacterId,
            CharacterName = CharacterDisplayName,
            ProviderLabel = BuildProviderLabel(mode),
            Mood = mood,
            Mode = mode.ToString()
        }, CancellationToken.None);
    }

    private void RefreshSelectedCharacter()
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        SelectedCharacterId = character.Id;
        CharacterDisplayName = character.DisplayName;

        var profile = config.App.CharacterProfiles is not null &&
                      config.App.CharacterProfiles.TryGetValue(character.Id, out var savedProfile)
            ? savedProfile
            : null;
        SelectedCharacterSizePresetId = string.IsNullOrWhiteSpace(profile?.CharacterSizePresetId)
            ? "normal"
            : profile.CharacterSizePresetId;
        SelectedCharacterFramingPresetId = string.IsNullOrWhiteSpace(profile?.CharacterFramingPresetId)
            ? "full-body"
            : profile.CharacterFramingPresetId;
        CharacterScale = profile?.CharacterScale is > 0 ? profile.CharacterScale : 1.0;
    }
}
