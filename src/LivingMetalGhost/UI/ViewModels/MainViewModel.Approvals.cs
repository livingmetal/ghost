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

    private void TryCreateGitFetchApprovalFromBubble(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.Contains("승인 카드", StringComparison.OrdinalIgnoreCase) ||
            !value.Contains("git fetch origin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var workspace = global::LivingMetalGhost.App.Services.GetRequiredService<WorkspaceStore>().Load();
        var commandPolicy = global::LivingMetalGhost.App.Services.GetRequiredService<CommandPolicyService>();
        var decision = commandPolicy.Evaluate("git fetch origin");
        AddApprovalIfMissing(new PendingApprovalRequest
        {
            Title = "원격 상태 확인",
            Command = "git fetch origin",
            WorkspaceRoot = workspace.RootPath,
            RiskLevel = decision.RiskLevel,
            Reason = decision.Reason
        });
    }

    private void AddGitPullApprovalIfMissing(string workspaceRoot)
    {
        var commandPolicy = global::LivingMetalGhost.App.Services.GetRequiredService<CommandPolicyService>();
        var decision = commandPolicy.Evaluate("git pull");
        AddApprovalIfMissing(new PendingApprovalRequest
        {
            Title = "Pull 적용",
            Command = "git pull",
            WorkspaceRoot = workspaceRoot,
            RiskLevel = decision.RiskLevel,
            Reason = decision.Reason
        });
    }

    private void AddApprovalIfMissing(PendingApprovalRequest approval)
    {
        if (PendingApprovals.Any(existing =>
                existing.Status == PendingApprovalStatus.Pending &&
                string.Equals(existing.Command, approval.Command, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.WorkspaceRoot, approval.WorkspaceRoot, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        PendingApprovals.Add(approval);
        TrimPendingApprovals();
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
