using System.Collections.ObjectModel;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    private CancellationTokenSource? _moodHoldCts;

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

    private Task DisplayAssistantResponseAsync(
        string response,
        bool isProactive,
        string assistantMood)
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
        if (animateCharacter)
        {
            IsCharacterSpeaking = true;
        }

        try
        {
            await _assistantMessagePresenter.PresentAsync(
                response,
                isProactive,
                CharacterDisplayName,
                targetMessages,
                mode);
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

    private async Task HoldMoodThenRestAsync(
        string mood,
        ConversationMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                _spriteDirector.GetPostSpeechHoldMilliseconds(mood, mode),
                cancellationToken);
            if (!cancellationToken.IsCancellationRequested &&
                !_isResponding &&
                !IsCharacterSpeaking)
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
