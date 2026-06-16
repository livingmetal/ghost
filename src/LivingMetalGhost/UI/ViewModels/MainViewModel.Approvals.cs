using System.Collections.ObjectModel;
using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<PendingApprovalRequest> PendingApprovals { get; } = [];

    partial void OnBubbleTextChanged(string value)
    {
        TryCreateGitFetchApprovalFromBubble(value);
    }

    public async Task ApproveAgentJobAsync(AgentJob job, CancellationToken cancellationToken)
    {
        if (!IsApprovalJob(job))
        {
            return;
        }

        var approval = FindPendingApproval(job.Title);
        if (approval is null)
        {
            job.Status = AgentJobStatus.Failed;
            job.Summary = "대응되는 승인 요청을 찾지 못했습니다.";
            return;
        }

        job.Status = AgentJobStatus.Running;
        job.Progress = 0.35;

        await ApprovePendingApprovalAsync(approval, cancellationToken);

        job.Status = approval.Status == PendingApprovalStatus.Completed
            ? AgentJobStatus.Completed
            : AgentJobStatus.Failed;
        job.Progress = 1.0;
        job.Summary = approval.ResultText;
    }

    public async Task AlwaysApproveAgentJobAsync(AgentJob job, CancellationToken cancellationToken)
    {
        if (!IsApprovalJob(job))
        {
            return;
        }

        var approval = FindPendingApproval(job.Title);
        if (approval is null)
        {
            job.Status = AgentJobStatus.Failed;
            job.Summary = "대응되는 승인 요청을 찾지 못했습니다.";
            return;
        }

        if (!CanRememberApproval(approval))
        {
            job.Summary = "이 작업은 매번 확인이 필요해서 항상 승인 대상으로 저장하지 않습니다. 이번만 승인합니다.";
            await ApproveAgentJobAsync(job, cancellationToken);
            return;
        }

        var workspaceStore = global::LivingMetalGhost.App.Services.GetRequiredService<WorkspaceStore>();
        workspaceStore.AddAlwaysApprovedCommand(approval.Command);

        job.Summary = "다음부터 같은 원격 확인은 자동 승인합니다.";
        await ApproveAgentJobAsync(job, cancellationToken);
    }

    public void RejectAgentJob(AgentJob job)
    {
        if (!IsApprovalJob(job))
        {
            return;
        }

        var approval = FindPendingApproval(job.Title);
        if (approval is not null)
        {
            RejectPendingApproval(approval);
        }

        job.Status = AgentJobStatus.Cancelled;
        job.Progress = 1.0;
        job.Summary = "사용자가 거절했습니다.";
    }

    public async Task ApprovePendingApprovalAsync(PendingApprovalRequest approval, CancellationToken cancellationToken)
    {
        if (approval.Status != PendingApprovalStatus.Pending)
        {
            return;
        }

        approval.Status = PendingApprovalStatus.Running;

        var commandPolicy = global::LivingMetalGhost.App.Services.GetRequiredService<CommandPolicyService>();
        var workspaceStore = global::LivingMetalGhost.App.Services.GetRequiredService<WorkspaceStore>();
        var executor = new ApprovedCommandExecutor(commandPolicy, workspaceStore);

        var result = await executor.ExecuteAsync(approval, cancellationToken);

        approval.ResultText = result.DisplayText;
        approval.CompletedAt = DateTimeOffset.Now;
        approval.Status = result.Success ? PendingApprovalStatus.Completed : PendingApprovalStatus.Failed;

        var response = result.Success
            ? $"승인된 명령을 실행했어.\n\nCommand: {approval.Command}\n\n{result.DisplayText}"
            : $"승인된 명령 실행이 실패했어.\n\nCommand: {approval.Command}\n\n{result.DisplayText}";

        Messages.Add(new ChatMessage
        {
            SpeakerName = CharacterDisplayName.ToUpperInvariant(),
            Text = response
        });
        BubbleText = response;

        if (result.Success && string.Equals(approval.Command, "git fetch origin", StringComparison.OrdinalIgnoreCase))
        {
            AddGitPullApprovalIfMissing(approval.WorkspaceRoot);
        }

        TrimPendingApprovals();
    }

    public void RejectPendingApproval(PendingApprovalRequest approval)
    {
        if (approval.Status != PendingApprovalStatus.Pending)
        {
            return;
        }

        approval.Status = PendingApprovalStatus.Rejected;
        approval.CompletedAt = DateTimeOffset.Now;
        approval.ResultText = "사용자가 거절했습니다.";
        TrimPendingApprovals();
    }

    private PendingApprovalRequest? FindPendingApproval(string command)
    {
        return PendingApprovals.FirstOrDefault(approval =>
            approval.Status == PendingApprovalStatus.Pending &&
            string.Equals(approval.Command, command, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsApprovalJob(AgentJob job)
    {
        return job.RequiresApproval &&
               string.Equals(job.AgentType, "approval", StringComparison.OrdinalIgnoreCase) &&
               job.Status == AgentJobStatus.WaitingApproval;
    }

    private static bool CanRememberApproval(PendingApprovalRequest approval)
    {
        // 항상 승인은 네트워크 읽기 정도까지만 허용한다.
        // git pull, merge, checkout 같은 WorkspaceWrite는 계속 매번 승인해야 한다.
        return approval.RiskLevel == CommandRiskLevel.NetworkRead;
    }

    private void TryCreateGitFetchApprovalFromBubble(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.Contains("승인 카드", StringComparison.OrdinalIgnoreCase) ||
            !value.Contains("git fetch origin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var workspaceStore = global::LivingMetalGhost.App.Services.GetRequiredService<WorkspaceStore>();
        var workspace = workspaceStore.Load();

        var commandPolicy = global::LivingMetalGhost.App.Services.GetRequiredService<CommandPolicyService>();
        var decision = commandPolicy.Evaluate("git fetch origin");

        var approval = new PendingApprovalRequest
        {
            Title = "원격 상태 확인",
            Command = "git fetch origin",
            WorkspaceRoot = workspace.RootPath,
            RiskLevel = decision.RiskLevel,
            Reason = decision.Reason
        };

        var registeredApproval = AddApprovalIfMissing(approval);

        if (CanRememberApproval(registeredApproval) &&
            workspaceStore.IsAlwaysApproved(registeredApproval.Command))
        {
            _ = ApprovePendingApprovalAsync(registeredApproval, CancellationToken.None);
            return;
        }

        AddApprovalAgentJobIfMissing(registeredApproval);
    }

    private void AddGitPullApprovalIfMissing(string workspaceRoot)
    {
        var commandPolicy = global::LivingMetalGhost.App.Services.GetRequiredService<CommandPolicyService>();
        var decision = commandPolicy.Evaluate("git pull");

        var approval = new PendingApprovalRequest
        {
            Title = "Pull 적용",
            Command = "git pull",
            WorkspaceRoot = workspaceRoot,
            RiskLevel = decision.RiskLevel,
            Reason = decision.Reason
        };

        var registeredApproval = AddApprovalIfMissing(approval);
        AddApprovalAgentJobIfMissing(registeredApproval);
    }

    private PendingApprovalRequest AddApprovalIfMissing(PendingApprovalRequest approval)
    {
        var existing = PendingApprovals.FirstOrDefault(item =>
            item.Status == PendingApprovalStatus.Pending &&
            string.Equals(item.Command, approval.Command, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.WorkspaceRoot, approval.WorkspaceRoot, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing;
        }

        PendingApprovals.Add(approval);
        TrimPendingApprovals();
        return approval;
    }

    private void AddApprovalAgentJobIfMissing(PendingApprovalRequest approval)
    {
        if (ActiveAgentJobs.Any(job =>
                job.RequiresApproval &&
                string.Equals(job.AgentType, "approval", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(job.Title, approval.Command, StringComparison.OrdinalIgnoreCase) &&
                job.Status is AgentJobStatus.WaitingApproval or AgentJobStatus.Running))
        {
            return;
        }

        ActiveAgentJobs.Add(new AgentJob
        {
            AgentType = "approval",
            DisplayName = "승인 필요",
            Title = approval.Command,
            Summary = $"{approval.Title} · {approval.RiskLabel} · {approval.Reason}",
            Status = AgentJobStatus.WaitingApproval,
            Progress = 0.0,
            RequiresApproval = true
        });

        TrimAgentJobs();
    }

    private void TrimPendingApprovals()
    {
        const int maximumVisibleApprovals = 6;
        while (PendingApprovals.Count > maximumVisibleApprovals)
        {
            var removable = PendingApprovals.FirstOrDefault(approval => approval.Status != PendingApprovalStatus.Pending) ??
                            PendingApprovals[0];
            PendingApprovals.Remove(removable);
        }
    }
}
