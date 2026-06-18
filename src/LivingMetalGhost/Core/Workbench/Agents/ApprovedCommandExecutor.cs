using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Agents;

/// <summary>
/// 사용자가 Workbench 승인 카드에서 승인한 명령만 실행한다.
/// shell을 통하지 않고 git 명령만 1차 지원한다.
/// </summary>
public sealed class ApprovedCommandExecutor
{
    private readonly CommandPolicyService _commandPolicyService;
    private readonly WorkspaceStore _workspaceStore;

    public ApprovedCommandExecutor(CommandPolicyService commandPolicyService, WorkspaceStore workspaceStore)
    {
        _commandPolicyService = commandPolicyService;
        _workspaceStore = workspaceStore;
    }

    public async Task<ApprovedCommandResult> ExecuteAsync(PendingApprovalRequest request, CancellationToken cancellationToken)
    {
        var decision = _commandPolicyService.Evaluate(request.Command);
        if (decision.RiskLevel is CommandRiskLevel.Dangerous or CommandRiskLevel.Blocked)
        {
            return new ApprovedCommandResult(false, string.Empty, decision.Reason);
        }

        if (!decision.IsAllowedByWorkspace && decision.RiskLevel != CommandRiskLevel.SafeRead)
        {
            return new ApprovedCommandResult(false, string.Empty, "workspace.json의 AllowedCommands에 없는 명령입니다.");
        }

        var workspaceRoot = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
            ? _workspaceStore.Load().RootPath
            : request.WorkspaceRoot;
        if (!WorkspaceGuard.TryResolveRoot(workspaceRoot, out var resolvedRoot, out var rootError))
        {
            return new ApprovedCommandResult(false, string.Empty, rootError);
        }

        var parts = SplitCommand(request.Command);
        if (parts.Count == 0 || !string.Equals(parts[0], "git", StringComparison.OrdinalIgnoreCase))
        {
            return new ApprovedCommandResult(false, string.Empty, "현재 승인 실행기는 git 명령만 지원합니다.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = resolvedRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in parts.Skip(1))
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
            return new ApprovedCommandResult(false, string.Empty, ex.Message);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return new ApprovedCommandResult(false, string.Empty, "명령이 90초 안에 끝나지 않아 중단했습니다.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();
        return new ApprovedCommandResult(
            process.ExitCode == 0,
            stdout,
            stderr);
    }

    private static IReadOnlyList<string> SplitCommand(string command)
    {
        // 1차 구현: git fetch origin / git pull 같은 단순 명령만 다룬다.
        // 따옴표가 필요한 복합 명령은 승인 실행 대상에서 제외하는 편이 안전하다.
        return command
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
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
}

public sealed record ApprovedCommandResult(bool Success, string Output, string Error)
{
    public string DisplayText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Output) && !string.IsNullOrWhiteSpace(Error))
            {
                return Output + Environment.NewLine + Error;
            }

            if (!string.IsNullOrWhiteSpace(Output))
            {
                return Output;
            }

            return string.IsNullOrWhiteSpace(Error) ? "출력 없음" : Error;
        }
    }
}
