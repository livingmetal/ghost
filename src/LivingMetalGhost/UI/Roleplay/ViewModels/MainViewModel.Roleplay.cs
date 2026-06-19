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
        var rawText = ImageInputService.BuildPromptText(
            submittedInput,
            submittedImage);
        var displayText = ImageInputService.BuildDisplayText(
            submittedInput,
            submittedImage);
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
            var result = await _roleplaySessionController.SendAsync(
                rawText,
                submittedImage,
                CancellationToken.None);
            if (ReferenceEquals(StorySelectedImage, submittedImage))
            {
                StorySelectedImage = null;
            }
            await DisplayAssistantResponseAsync(
                result.BubbleText,
                false,
                result.Mood,
                StoryMessages,
                ConversationMode.Story,
                false);
            await WriteLogAsync(
                displayText,
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
            // Idle presence is optional and should fail silently.
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
        var scene = string.IsNullOrWhiteSpace(state.Scene)
            ? "아직 고정된 장면 없음"
            : state.Scene.Trim();
        var summary = string.IsNullOrWhiteSpace(state.Summary)
            ? "아직 누적 요약 없음"
            : state.Summary.Trim();

        var builder = new StringBuilder();
        builder.AppendLine($"상태: {(state.Enabled ? "켜짐" : "꺼짐")}");
        builder.AppendLine($"롤플레잉 창: {(IsStoryMode ? "열림" : "닫힘")}");
        builder.AppendLine($"제목: {state.Title}");
        builder.AppendLine($"플레이어 역할: {state.PlayerRole}");
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
        var keepEnabled = ConversationModeCoordinator.IsRoleplayActive(
            IsStoryMode,
            IsAdvancedMode);
        var state = _roleplaySessionController.Reset(keepEnabled);
        IsStoryMode = state.Enabled;
        StoryMessages.Clear();
        if (IsStoryMode)
        {
            ShowRoleplayOpening(state);
        }

        BubbleText = "롤플레잉 장면 상태를 초기화했어요.";
        RefreshModePresentation();
    }
}
