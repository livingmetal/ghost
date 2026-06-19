using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.AppCore.Desktop;
using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.AppCore.Roleplay;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.UI.Presentation;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppConfigLoader _configLoader;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ConversationLogService _conversationLogService;
    private readonly CompanionConversationController _companionConversationController;
    private readonly ConversationTurnLogWriter _conversationTurnLogWriter;
    private readonly DesktopRuntimeSettingsService _desktopRuntimeSettingsService;
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
    private string pendingUserMessageText = string.Empty;

    [ObservableProperty]
    private bool isUserMessagePending;

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
    /// 현재 설정된 고급 대화 provider를 기준으로 고급 모드 토글 가능 여부.
    /// lmbot은 로컬 CLI 감지 결과를 사용하고, 그 외 API provider는 항상 활성화한다.
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
        DesktopRuntimeSettingsService desktopRuntimeSettingsService,
        SpriteDirector spriteDirector,
        RoleplaySessionController roleplaySessionController,
        AssistantMessagePresenter assistantMessagePresenter)
    {
        _configLoader = configLoader;
        _settingsViewModel = settingsViewModel;
        _conversationLogService = conversationLogService;
        _companionConversationController = companionConversationController;
        _conversationTurnLogWriter = conversationTurnLogWriter;
        _desktopRuntimeSettingsService = desktopRuntimeSettingsService;
        _spriteDirector = spriteDirector;
        _roleplaySessionController = roleplaySessionController;
        _assistantMessagePresenter = assistantMessagePresenter;
        IsStoryMode = _roleplaySessionController.IsEnabled;
        RefreshSelectedCharacter();
        _ = RefreshLocalLmAvailabilityAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<ChatMessage> StoryMessages { get; } = [];

    /// <summary>고급 Workbench와 Agent Dock에 표시할 현재 작업.</summary>
    public ObservableCollection<AgentJob> ActiveAgentJobs { get; } = [];

    public ConversationMode CurrentMode =>
        ConversationModeCoordinator.GetCompanionMode(IsAdvancedMode);
}
