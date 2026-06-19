using System.Text;
using LivingMetalGhost.AppCore.ModeCoordination;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
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
