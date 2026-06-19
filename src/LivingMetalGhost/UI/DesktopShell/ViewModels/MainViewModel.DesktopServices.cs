using System.Windows;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Skills;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    public string ActiveProviderLabel => BuildProviderLabel(CurrentMode);

    public string StoryProviderLabel => BuildProviderLabel(ConversationMode.Story)
        .Replace("STORY:", "ROLEPLAY:", StringComparison.OrdinalIgnoreCase);

    public async Task RefreshLocalLmAvailabilityAsync()
    {
        var availability =
            await _desktopRuntimeSettingsService.DetectAdvancedModeAvailabilityAsync();
        IsLocalLmAvailable = availability.IsLocalLmAvailable;
        IsAdvancedModeAvailable = availability.IsAdvancedModeAvailable;

        if (!IsAdvancedModeAvailable && IsAdvancedMode)
        {
            IsAdvancedMode = false;
        }
    }

    public (bool Enabled, int MinMinutes, int MaxMinutes) GetProactiveChatSettings()
    {
        var settings = _desktopRuntimeSettingsService.GetProactiveChatSettings();
        return (settings.Enabled, settings.MinMinutes, settings.MaxMinutes);
    }

    private string BuildProviderLabel(ConversationMode mode)
    {
        return _conversationTurnLogWriter.GetProviderLabel(mode);
    }

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

    private Task WriteLogAsync(
        string userText,
        string assistantText,
        bool isProactive,
        string mood = "speaking",
        ConversationMode? modeOverride = null)
    {
        var mode = modeOverride ?? CurrentMode;
        return _conversationTurnLogWriter.WriteAsync(
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
