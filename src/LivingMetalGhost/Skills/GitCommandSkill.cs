using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Skills;

/// <summary>
/// Git 관련 요청 중 읽기 전용 상태 확인은 Workbench가 직접 수행한다.
/// pull/fetch/reset 같은 변경 가능 명령은 바로 실행하지 않고 승인 필요 작업으로 분류한다.
/// </summary>
public sealed class GitCommandSkill : IGhostSkill
{
    private readonly WorkspaceStore _workspaceStore;
    private readonly CommandPolicyService _commandPolicyService;

    public GitCommandSkill(
        WorkspaceStore workspaceStore,
        CommandPolicyService commandPolicyService)
    {
        _workspaceStore = workspaceStore;
        _commandPolicyService = commandPolicyService;
    }

    public string Name => "GitCommand";
    public string Description => "git status/diff 같은 읽기 전용 명령을 자동 실행하고, fetch/pull/reset은 승인 대상으로 분류한다.";
    public IReadOnlyList<string> Examples => ["git status", "git pull 해줘", "원격 브랜치랑 차이 확인해줘"];

    public bool CanHandle(UserRequest request)
    {
        var text = request.RawText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("git", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("깃", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("pull", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("fetch", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("원격 브랜치", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        var workspace = _workspaceStore.Load();
        if (!WorkspaceGuard.TryResolveRoot(workspace.RootPath, out var workspaceRoot, out var rootError))
        {
            return new SkillResult
            {
                BubbleText =
                    "Git 상태를 확인하려면 먼저 Workbench의 워크스페이스 Root Path를 지정해야 해.\n" +
                    $"현재 문제: {rootError}",
                Mood = "concerned",
                Action = "git-workspace-missing",
                UsedLlm = false
            };
        }

        if (IsFetchRequest(request.RawText))
        {
            return await BuildNetworkApprovalResponseAsync(request.RawText, workspaceRoot, ct);
        }

        if (IsPullOrWriteRequest(request.RawText))
        {
            return await BuildPullPreflightResponseAsync(workspaceRoot, ct);
        }

        return await BuildStatusResponseAsync(workspaceRoot, ct);
    }

    private async Task<SkillResult> BuildStatusResponseAsync(string workspaceRoot, CancellationToken ct)
    {
        var branch = await RunSafeGitCommandAsync("git branch --show-current", "branch", ["branch", "--show-current"], workspaceRoot, ct);
        var status = await RunSafeGitCommandAsync("git status --short", "status", ["status", "--short"], workspaceRoot, ct);
        var diff = await RunSafeGitCommandAsync("git diff --stat", "diff stat", ["diff", "--stat"], workspaceRoot, ct);

        var bubble = new StringBuilder();
        bubble.AppendLine("읽기 전용 Git 상태 확인을 실행했어.");
        bubble.AppendLine();
        bubble.AppendLine($"작업 루트: {workspaceRoot}");
        bubble.AppendLine($"현재 브랜치: {DisplaySingleLine(branch.Output, "(알 수 없음)")}");
        bubble.AppendLine();
        bubble.AppendLine("로컬 변경:");
        bubble.AppendLine(DisplayBlock(status.Output, "변경 없음"));
        bubble.AppendLine();
        bubble.AppendLine("Diff stat:");
        bubble.AppendLine(DisplayBlock(diff.Output, "diff 없음"));

        if (!branch.Success || !status.Success || !diff.Success)
        {
            bubble.AppendLine();
            bubble.AppendLine("일부 git 읽기 명령이 실패했어. 저장소 경로나 git 설치 상태를 확인해야 해.");
        }

        return new SkillResult
        {
            BubbleText = bubble.ToString().TrimEnd(),
            Mood = "serious",
            Action = "git-status-readonly",
            UsedLlm = false,
            RawData = new { Branch = branch, Status = status, Diff = diff }
        };
    }

    private async Task<SkillResult> BuildPullPreflightResponseAsync(string workspaceRoot, CancellationToken ct)
    {
        var statusResult = await BuildStatusResponseAsync(workspaceRoot, ct);
        var fetchDecision = _commandPolicyService.Evaluate("git fetch origin");
        var pullDecision = _commandPolicyService.Evaluate("git pull");

        var bubble = new StringBuilder();
        bubble.AppendLine("바로 pull은 위험해서 먼저 읽기 전용 상태만 확인했어.");
        bubble.AppendLine();
        bubble.AppendLine(statusResult.BubbleText);
        bubble.AppendLine();
        bubble.AppendLine("다음 단계 후보:");
        bubble.AppendLine($"- git fetch origin: {DescribeDecision(fetchDecision)}");
        bubble.AppendLine($"- git pull: {DescribeDecision(pullDecision)}");
        bubble.AppendLine();
        bubble.AppendLine("권장 흐름은 fetch 승인 → 원격 차이 확인 → 로컬 변경 처리 방침 결정 → pull 승인 순서야.");

        return new SkillResult
        {
            BubbleText = bubble.ToString().TrimEnd(),
            Mood = "serious",
            Action = "git-pull-preflight",
            UsedLlm = false,
            RawData = new { Fetch = fetchDecision, Pull = pullDecision, Status = statusResult.RawData }
        };
    }

    private async Task<SkillResult> BuildNetworkApprovalResponseAsync(string rawText, string workspaceRoot, CancellationToken ct)
    {
        var statusResult = await BuildStatusResponseAsync(workspaceRoot, ct);
        var command = rawText.Contains("ls-remote", StringComparison.OrdinalIgnoreCase)
            ? "git ls-remote"
            : "git fetch origin";
        var decision = _commandPolicyService.Evaluate(command);

        return new SkillResult
        {
            BubbleText =
                statusResult.BubbleText + "\n\n" +
                $"요청한 원격 확인 명령은 `{command}`로 분류했어.\n" +
                $"위험도: {decision.RiskLevel}\n" +
                $"판정: {decision.Reason}\n" +
                "네트워크 접근이 필요하니 지금 단계에서는 자동 실행하지 않고 승인 카드로 넘기는 게 맞아.",
            Mood = "serious",
            Action = "git-network-approval-needed",
            UsedLlm = false,
            RawData = decision
        };
    }

    private async Task<GitCommandResult> RunSafeGitCommandAsync(
        string commandLine,
        string label,
        IReadOnlyList<string> arguments,
        string workspaceRoot,
        CancellationToken ct)
    {
        var decision = _commandPolicyService.Evaluate(commandLine);
        if (!decision.CanAutoRun)
        {
            return new GitCommandResult(label, false, string.Empty, decision.Reason, decision.RiskLevel);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspaceRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new GitCommandResult(label, false, string.Empty, ex.Message, decision.RiskLevel);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            return new GitCommandResult(label, false, string.Empty, "git 명령이 20초 안에 끝나지 않아 중단했습니다.", decision.RiskLevel);
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();
        return new GitCommandResult(
            label,
            process.ExitCode == 0,
            stdout,
            stderr,
            decision.RiskLevel);
    }

    private static bool IsPullOrWriteRequest(string text)
    {
        return text.Contains("pull", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("merge", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("checkout", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("clean", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFetchRequest(string text)
    {
        return text.Contains("fetch", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ls-remote", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("원격", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeDecision(CommandPolicyDecision decision)
    {
        return decision.CanAutoRun
            ? $"자동 실행 가능 ({decision.RiskLevel})"
            : $"승인 필요 ({decision.RiskLevel}) — {decision.Reason}";
    }

    private static string DisplaySingleLine(string text, string fallback)
    {
        var value = text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string DisplayBlock(string text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private sealed record GitCommandResult(
        string Label,
        bool Success,
        string Output,
        string Error,
        CommandRiskLevel RiskLevel);
}
