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
