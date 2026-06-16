using CommunityToolkit.Mvvm.ComponentModel;

namespace LivingMetalGhost.Core.Models;

/// <summary>Workbench/Agent Dock에 표시할 백그라운드 작업 상태.</summary>
public sealed partial class AgentJob : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentType { get; init; } = "agent";
    public string DisplayName { get; init; } = "Agent";
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private AgentJobStatus status = AgentJobStatus.Queued;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private bool requiresApproval;

    public string StatusLabel => Status switch
    {
        AgentJobStatus.Queued => "대기",
        AgentJobStatus.Running => "진행 중",
        AgentJobStatus.WaitingApproval => "승인 대기",
        AgentJobStatus.Applying => "적용 중",
        AgentJobStatus.Completed => "완료",
        AgentJobStatus.Failed => "실패",
        AgentJobStatus.Cancelled => "취소됨",
        _ => "알 수 없음"
    };

    public string TooltipText
    {
        get
        {
            var changedFiles = ChangedFiles.Count == 0
                ? "변경 파일 없음"
                : $"변경 파일 {ChangedFiles.Count}개";

            return $"{DisplayName} 작업\n" +
                   $"상태: {StatusLabel}\n" +
                   $"작업: {Title}\n" +
                   $"요약: {Summary}\n" +
                   $"{changedFiles}\n" +
                   $"시작: {StartedAt:HH:mm}";
        }
    }

    partial void OnStatusChanged(AgentJobStatus value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(TooltipText));
    }

    partial void OnSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(TooltipText));
    }
}

public enum AgentJobStatus
{
    Queued,
    Running,
    WaitingApproval,
    Applying,
    Completed,
    Failed,
    Cancelled
}
