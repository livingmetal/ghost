using CommunityToolkit.Mvvm.ComponentModel;
using LivingMetalGhost.Agents;

namespace LivingMetalGhost.Core.Models;

/// <summary>
/// Workbench에서 사용자가 승인/거절해야 하는 명령 실행 요청.
/// </summary>
public sealed partial class PendingApprovalRequest : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string WorkspaceRoot { get; init; } = string.Empty;
    public CommandRiskLevel RiskLevel { get; init; } = CommandRiskLevel.Blocked;
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    [ObservableProperty]
    private PendingApprovalStatus status = PendingApprovalStatus.Pending;

    [ObservableProperty]
    private string resultText = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? completedAt;

    public bool CanApprove => Status == PendingApprovalStatus.Pending;

    public string StatusLabel => Status switch
    {
        PendingApprovalStatus.Pending => "승인 대기",
        PendingApprovalStatus.Approved => "승인됨",
        PendingApprovalStatus.Running => "실행 중",
        PendingApprovalStatus.Completed => "완료",
        PendingApprovalStatus.Rejected => "거절됨",
        PendingApprovalStatus.Failed => "실패",
        _ => "알 수 없음"
    };

    public string RiskLabel => RiskLevel switch
    {
        CommandRiskLevel.SafeRead => "SafeRead",
        CommandRiskLevel.NetworkRead => "NetworkRead",
        CommandRiskLevel.WorkspaceWrite => "WorkspaceWrite",
        CommandRiskLevel.Dangerous => "Dangerous",
        _ => "Blocked"
    };

    public string TooltipText =>
        $"Command: {Command}\n" +
        $"Risk: {RiskLabel}\n" +
        $"Reason: {Reason}\n" +
        $"Workspace: {WorkspaceRoot}";

    partial void OnStatusChanged(PendingApprovalStatus value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(CanApprove));
    }
}

public enum PendingApprovalStatus
{
    Pending,
    Approved,
    Running,
    Completed,
    Rejected,
    Failed
}

public sealed class PendingApprovalBatch
{
    public IReadOnlyList<PendingApprovalRequest> Requests { get; init; } = Array.Empty<PendingApprovalRequest>();
}
