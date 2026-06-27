using System.Text;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    public void SetStoryMode(bool enabled)
    {
        var wasEnabled = IsStoryMode;
        var state = _roleplaySessionController.SetEnabled(enabled);
        IsStoryMode = enabled;
        RefreshStoryInfoPanel(state);

        if (enabled && !wasEnabled && state.ShowOpeningOnActivation)
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
        RefreshStoryInfoPanel(state);
        var openingText = _roleplaySessionController.BuildOpeningText(state);
        if (string.IsNullOrWhiteSpace(openingText))
        {
            return;
        }

        StoryMessages.Add(new ChatMessage
        {
            Text = openingText,
            SpeakerName = "STORY",
            IsProactive = true,
            IsRoleplay = true
        });
    }

    partial void OnIsStoryModeChanged(bool value)
    {
        RefreshModePresentation();
    }

    [RelayCommand]
    private async Task StorySendAsync()
    {
        if (_isStoryResponding)
        {
            return;
        }

        var submittedInput = StoryInputText;
        var submittedImage = StorySelectedImage;
        var rawText = ImageInputService.BuildPromptText(submittedInput, submittedImage);
        var displayText = ImageInputService.BuildDisplayText(submittedInput, submittedImage);
        if (string.IsNullOrWhiteSpace(rawText) && submittedImage is null)
        {
            return;
        }

        _isStoryResponding = true;
        StoryMessages.Add(new ChatMessage
        {
            Text = displayText,
            SpeakerName = "YOU",
            IsUser = true,
            IsRoleplay = true
        });
        StoryInputText = string.Empty;

        try
        {
            var result = await _roleplaySessionController.SendAsync(rawText, submittedImage, CancellationToken.None);
            RefreshStoryInfoPanel(_roleplaySessionController.GetSnapshot().State);
            if (ReferenceEquals(StorySelectedImage, submittedImage))
            {
                StorySelectedImage = null;
            }

            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, ConversationMode.Story);
            SetCharacterMood(assistantMood);
            StoryMessages.Add(new ChatMessage
            {
                Text = result.BubbleText,
                SpeakerName = "STORY",
                IsRoleplay = true
            });

            await WriteLogAsync(displayText, result.BubbleText, false, result.Mood, ConversationMode.Story);
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
            var result = await _roleplaySessionController.StartIdleAsync(CancellationToken.None);
            RefreshStoryInfoPanel(_roleplaySessionController.GetSnapshot().State);
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, ConversationMode.Story);
            SetCharacterMood(assistantMood);
            StoryMessages.Add(new ChatMessage
            {
                Text = result.BubbleText,
                SpeakerName = "STORY",
                IsProactive = true,
                IsRoleplay = true
            });
        }
        catch
        {
        }
        finally
        {
            _isStoryResponding = false;
        }
    }

    public string GetRoleplayStateSummary()
    {
        var snapshot = _roleplaySessionController.GetSnapshot();
        var state = snapshot.State;
        var memoryEntries = snapshot.MemoryEntries;
        var scene = string.IsNullOrWhiteSpace(state.Scene) ? "아직 고정된 장면 없음" : state.Scene.Trim();
        var summary = string.IsNullOrWhiteSpace(state.Summary) ? "아직 누적 요약 없음" : state.Summary.Trim();

        var builder = new StringBuilder();
        builder.AppendLine($"상태: {(state.Enabled ? "켜짐" : "꺼짐")}");
        builder.AppendLine($"롤플레잉 창: {(IsStoryMode ? "열림" : "닫힘")}");
        builder.AppendLine($"제목: {state.Title}");
        builder.AppendLine($"플레이어 역할: {state.PlayerRole}");
        builder.AppendLine($"턴: {state.TurnNumber}");
        builder.AppendLine($"시간: {state.StoryDate} {state.StoryTime}");
        builder.AppendLine($"장소: {state.Location}");
        builder.AppendLine($"호감도: {state.Affection}%");
        builder.AppendLine($"상태문: {state.StatusText}");
        builder.AppendLine($"분위기: {state.Mood}");
        builder.AppendLine($"긴장도: {state.Tension}/5");
        builder.AppendLine($"저장된 턴: {memoryEntries}");
        builder.AppendLine($"수정 시각: {state.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine("장면");
        builder.AppendLine(scene);
        builder.AppendLine();
        builder.AppendLine("요약");
        builder.AppendLine(summary);
        builder.AppendLine();
        builder.AppendLine("저장 위치");
        builder.AppendLine(snapshot.StoryRoot);
        return builder.ToString();
    }

    public void ResetRoleplayState()
    {
        var keepEnabled = ConversationModeCoordinator.IsRoleplayActive(IsStoryMode, IsAdvancedMode);
        var state = _roleplaySessionController.Reset(keepEnabled);
        IsStoryMode = state.Enabled;
        RefreshStoryInfoPanel(state);
        StoryMessages.Clear();
        if (IsStoryMode)
        {
            ShowRoleplayOpening(state);
        }

        BubbleText = "롤플레잉 장면 상태를 초기화했어요.";
        RefreshModePresentation();
    }
}
