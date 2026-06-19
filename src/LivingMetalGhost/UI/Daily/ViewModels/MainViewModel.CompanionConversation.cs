using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.Presentation;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
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

    partial void OnIsAdvancedModeChanged(bool value)
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

        var submittedInput = InputText;
        var submittedImage = SelectedImage;
        var promptText = ImageInputService.BuildPromptText(
            submittedInput,
            submittedImage);
        var displayText = ImageInputService.BuildDisplayText(
            submittedInput,
            submittedImage);
        var request = new UserRequest
        {
            RawText = promptText,
            UseAdvancedModel = IsAdvancedMode,
            Image = submittedImage
        };
        if (string.IsNullOrWhiteSpace(request.RawText) && request.Image is null)
        {
            return;
        }

        CancelMoodHold();
        SetCharacterMood(_spriteDirector.ResolveThinkingMood(CurrentMode));
        _isResponding = true;
        PendingUserMessageText = displayText;
        IsUserMessagePending = true;
        Messages.Add(new ChatMessage
        {
            Text = displayText,
            SpeakerName = "YOU",
            IsUser = true,
            IsRoleplay = false
        });

        try
        {
            var turn = await _companionConversationController.SendAsync(
                request.RawText,
                request.UseAdvancedModel,
                request.Image,
                CancellationToken.None);
            var result = turn.Result;
            BubbleText = result.BubbleText;
            var assistantMood = _spriteDirector.ResolveSpeakingMood(result.Mood, CurrentMode);
            SetCharacterMood(assistantMood);
            CaptureAgentJobs(turn.Request, result);
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: false, assistantMood);
            IsUserMessagePending = false;
            PendingUserMessageText = string.Empty;
            if (SubmittedInputPolicy.ShouldClear(InputText, submittedInput))
            {
                InputText = string.Empty;
            }
            if (ReferenceEquals(SelectedImage, submittedImage))
            {
                SelectedImage = null;
            }

            await WriteLogAsync(displayText, result.BubbleText, isProactive: false, assistantMood);
            if (IsAdvancedMode)
            {
                CapturePatchProposals(result.BubbleText);
            }

            DispatchAppCommand(result.Action);
        }
        catch (Exception ex)
        {
            IsUserMessagePending = false;
            PendingUserMessageText = string.Empty;
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
}
