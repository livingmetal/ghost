using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.AppCore.Roleplay;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Providers.Llm;
using LivingMetalGhost.Skills;
using LivingMetalGhost.UI.Presentation;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppConfigLoader _configLoader;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ConversationLogService _conversationLogService;
    private readonly CompanionConversationController _companionConversationController;
    private readonly ConversationTurnLogWriter _conversationTurnLogWriter;
    private readonly SpriteDirector _spriteDirector;
    private readonly RoleplaySessionController _roleplaySessionController;
    private readonly AssistantMessagePresenter _assistantMessagePresenter;
    private bool _isResponding;
    private bool _isStoryResponding;

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
        SettingsViewModel settingsViewModel,
        ConversationLogService conversationLogService,
        CompanionConversationController companionConversationController,
        ConversationTurnLogWriter conversationTurnLogWriter,
        SpriteDirector spriteDirector,
        RoleplaySessionController roleplaySessionController,
        AssistantMessagePresenter assistantMessagePresenter)
    {
        _configLoader = configLoader;
        _settingsViewModel = settingsViewModel;
        _conversationLogService = conversationLogService;
        _companionConversationController = companionConversationController;
        _conversationTurnLogWriter = conversationTurnLogWriter;
        _spriteDirector = spriteDirector;
        _roleplaySessionController = roleplaySessionController;
        _assistantMessagePresenter = assistantMessagePresenter;
        IsStoryMode = _roleplaySessionController.IsEnabled;
        RefreshSelectedCharacter();
        _ = RefreshLocalLmAvailabilityAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<ChatMessage> StoryMessages { get; } = [];

    /// <summary>고급 Workbench / Agent Dock 에 표시할 현재 작업들.</summary>
    public ObservableCollection<AgentJob> ActiveAgentJobs { get; } = [];

    public ConversationMode CurrentMode =>
        ConversationModeCoordinator.GetCompanionMode(IsAdvancedMode);

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
        return _conversationTurnLogWriter.GetProviderLabel(mode);
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

    public void SetAdvancedMode(bool enabled)
    {
        IsAdvancedMode = ConversationModeCoordinator.ResolveAdvancedEnabled(
            enabled,
            IsAdvancedModeAvailable);
    }

    public void ToggleAdvancedMode()
    {
        SetAdvancedMode(!IsAdvancedMode);
    }

    public void SetStoryMode(bool enabled)
    {
        var wasEnabled = IsStoryMode;
        var state = _roleplaySessionController.SetEnabled(enabled);
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

    private void ShowRoleplayOpening(StoryState state)
    {
        var openingText = _roleplaySessionController.BuildOpeningText(state);
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
            var turn = await _companionConversationController.SendAsync(
                request.RawText,
                request.UseAdvancedModel,
                CancellationToken.None);
            var result = turn.Result;
            BubbleText = result.BubbleText;
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, CurrentMode);
            SetCharacterMood(assistantMood);
            CaptureAgentJobs(turn.Request, result);
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: false, assistantMood);
            await WriteLogAsync(request.RawText, result.BubbleText, isProactive: false, assistantMood);
            if (IsAdvancedMode)
            {
                CapturePatchProposals(result.BubbleText);
            }

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
            var result = await _roleplaySessionController.SendAsync(
                rawText,
                CancellationToken.None);
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
            var result = await _companionConversationController.StartAsync(
                CancellationToken.None);
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

    /// <summary>스토리 모드에서 사용자가 조용할 때 짧은 존재감 비트를 띄운다(Phase 4 idle presence).</summary>
    public async Task StartStoryIdleAsync()
    {
        if (!ConversationModeCoordinator.IsRoleplayActive(IsStoryMode, IsAdvancedMode) ||
            _isResponding ||
            _isStoryResponding ||
            IsCharacterSpeaking)
        {
            return;
        }

        _isStoryResponding = true;
        CancelMoodHold();
        SetCharacterMood(_spriteDirector.ResolveThinkingMood(ConversationMode.Story));

        try
        {
            var result = await _roleplaySessionController.StartIdleAsync(
                CancellationToken.None);
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, ConversationMode.Story);
            SetCharacterMood(assistantMood);
            await DisplayAssistantResponseAsync(
                result.BubbleText,
                isProactive: true,
                assistantMood,
                StoryMessages,
                ConversationMode.Story,
                animateCharacter: true);
        }
        catch
        {
            // idle 비트는 보조 연출이므로 실패해도 조용히 넘어간다.
        }
        finally
        {
            _isStoryResponding = false;
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

    private async Task WriteLogAsync(
        string userText,
        string assistantText,
        bool isProactive,
        string mood = "speaking",
        ConversationMode? modeOverride = null)
    {
        var mode = modeOverride ?? CurrentMode;
        await _conversationTurnLogWriter.WriteAsync(
            new ConversationTurnLogContext(
                userText,
                assistantText,
                isProactive,
                mood,
                mode,
                SelectedCharacterId,
                CharacterDisplayName),
            CancellationToken.None);
    }

}
