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

    [ObservableProperty] private string bubbleText = "기다리고 있어요.";
    [ObservableProperty] private string inputText = string.Empty;
    [ObservableProperty] private LlmImageAttachment? selectedImage;
    [ObservableProperty] private string pendingUserMessageText = string.Empty;
    [ObservableProperty] private bool isUserMessagePending;
    [ObservableProperty] private string storyInputText = string.Empty;
    [ObservableProperty] private LlmImageAttachment? storySelectedImage;
    [ObservableProperty] private string storyInfoText = string.Empty;
    [ObservableProperty] private bool isStoryInfoPanelVisible = true;
    [ObservableProperty] private string characterMood = "idle";
    [ObservableProperty] private string characterStateLabel = "IDLE";
    [ObservableProperty] private bool isCharacterSpeaking;
    [ObservableProperty] private string selectedCharacterId = "ssuang";
    [ObservableProperty] private string characterDisplayName = "쑝";
    [ObservableProperty] private string selectedCharacterSizePresetId = "normal";
    [ObservableProperty] private double characterScale = 1.0;
    [ObservableProperty] private string selectedCharacterFramingPresetId = "full-body";
    [ObservableProperty] private bool isAdvancedMode;
    [ObservableProperty] private bool isStoryMode;
    [ObservableProperty] private bool isLocalLmAvailable;
    [ObservableProperty] private bool isAdvancedModeAvailable = true;
    [ObservableProperty] private int proactiveSettingsRevision;

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
        RefreshStoryInfoPanel(_roleplaySessionController.GetSnapshot().State);
        RefreshSelectedCharacter();
        _ = RefreshLocalLmAvailabilityAsync();
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<ChatMessage> StoryMessages { get; } = [];
    public ObservableCollection<AgentJob> ActiveAgentJobs { get; } = [];

    public ConversationMode CurrentMode => ConversationModeCoordinator.GetCompanionMode(IsAdvancedMode);

    public void RefreshStoryInfoPanel(StoryState state)
    {
        var config = _configLoader.Load();
        IsStoryInfoPanelVisible = config.RoleplayLlm.EnableStatePanel;
        var scene = string.IsNullOrWhiteSpace(state.Location) ? state.Scene : state.Location;
        if (string.IsNullOrWhiteSpace(scene)) scene = "장소 미정";
        var status = string.IsNullOrWhiteSpace(state.StatusText) ? state.Mood : state.StatusText;
        StoryInfoText = $"[No.] #{state.TurnNumber}\n[Date] {state.StoryDate} | {state.StoryTime}\n[Place] {scene}\n\n[Love] {state.Affection}%\n[Info] {status}";
    }

    public void SelectImage(string filePath, bool storyMode)
    {
        SetSelectedImage(ImageInputService.Load(filePath), storyMode);
    }

    public void SetSelectedImage(LlmImageAttachment image, bool storyMode)
    {
        if (storyMode) StorySelectedImage = image;
        else SelectedImage = image;
    }

    public void ClearSelectedImage(bool storyMode)
    {
        if (storyMode) StorySelectedImage = null;
        else SelectedImage = null;
    }
}
