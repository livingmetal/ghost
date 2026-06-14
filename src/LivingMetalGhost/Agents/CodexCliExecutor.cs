using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Security;

namespace LivingMetalGhost.Agents;

/// <summary>
/// Codex CLI 를 외부 작업 에이전트로 호출하는 실행기.
///
/// 안전 설계 (ClaudeCodeExecutor 와 동일):
/// - Suggest/Ask 모드에서는 실행하지 않고 예정 작업을 "제안"으로만 돌려준다.
/// - 실제 실행(Apply/Execute)은 config.agents.enable_execution == true 인 경우에만 허용한다.
/// - workspace_root 가 유효해야 하며, 작업 디렉터리를 그 밖으로 벗어나지 않게 고정한다.
/// - sandbox 수준: apply → workspace-write, execute → all.
/// </summary>
public sealed class CodexCliExecutor : IAgentExecutor
{
    private readonly AppConfigLoader _configLoader;
    private readonly DpapiSecretStore _secretStore;

    public CodexCliExecutor(AppConfigLoader configLoader, DpapiSecretStore secretStore)
    {
        _configLoader = configLoader;
        _secretStore = secretStore;
    }

    public string Name => "codex-cli";

    public async Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var agents = config.Agents;
        var executable = string.IsNullOrWhiteSpace(agents.CodexCli.Executable)
            ? "codex"
            : Environment.ExpandEnvironmentVariables(agents.CodexCli.Executable.Trim());

        var rootConfigured = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
            ? agents.WorkspaceRoot
            : request.WorkspaceRoot;

        var executionAllowed =
            agents.EnableExecution &&
            request.ApprovalMode is AgentApprovalMode.Apply or AgentApprovalMode.Execute;

        if (!executionAllowed)
        {
            var reason = !agents.EnableExecution
                ? "agents.enable_execution 이 false 입니다."
                : "승인 모드가 apply/execute 가 아닙니다.";

            return new AgentResult
            {
                Success = true,
                Summary =
                    $"[Codex CLI · 제안 모드] 아래 작업을 실행할 준비가 되었지만 실행하지 않았습니다.\n" +
                    $"- 사유: {reason}\n" +
                    $"- 작업 내용: {request.Instruction}\n" +
                    $"- 작업 루트: {(string.IsNullOrWhiteSpace(rootConfigured) ? "(미설정)" : rootConfigured)}",
                RawOutput = string.Empty,
                ChangedFiles = Array.Empty<string>(),
                RequiresUserReview = true
            };
        }

        if (!WorkspaceGuard.TryResolveRoot(rootConfigured, out var workspaceRoot, out var rootError))
        {
            return new AgentResult
            {
                Success = false,
                Summary = $"[Codex CLI] 실행을 중단했습니다: {rootError}",
                RequiresUserReview = true
            };
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), "LivingMetalGhost", "codex");
        Directory.CreateDirectory(outputDirectory);
        var outputFile = Path.Combine(outputDirectory, $"result-{Guid.NewGuid():N}.txt");
        var timeoutSeconds = Math.Clamp(agents.TimeoutSeconds, 30, 1800);

        // apply: workspace-write (파일 수정 가능, 외부 명령 금지)
        // execute: all (외부 명령까지 허용, 사용자가 명시적으로 선택한 경우)
        var sandbox = request.ApprovalMode == AgentApprovalMode.Execute ? "all" : "workspace-write";

        var arguments = BuildArguments(sandbox, outputFile, workspaceRoot, agents.CodexCli.ExtraArgs);
        using var process = new Process { StartInfo = CreateStartInfo(executable, arguments, workspaceRoot) };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            TryDelete(outputFile);
            return new AgentResult
            {
                Success = false,
                Summary = "[Codex CLI] 실행 파일을 찾지 못했습니다. 설정에서 실행 경로를 확인해 주세요.",
                RawOutput = ex.Message,
                RequiresUserReview = true
            };
        }

        using var cancelReg = ct.Register(() => TryKill(process));

        await process.StandardInput.WriteAsync(request.Instruction);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            TryDelete(outputFile);
            return new AgentResult
            {
                Success = false,
                Summary = $"[Codex CLI] 응답이 {timeoutSeconds}초 안에 끝나지 않아 중단했습니다.",
                RequiresUserReview = true
            };
        }

        var apiKey = SafeLoadApiKey();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var lastMessage = File.Exists(outputFile)
            ? (await File.ReadAllTextAsync(outputFile, ct)).Trim()
            : string.Empty;
        TryDelete(outputFile);

        var rawOutput = SecretMasker.Mask(
            string.IsNullOrWhiteSpace(lastMessage)
                ? (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr)
                : lastMessage,
            apiKey);
        var success = process.ExitCode == 0;

        return new AgentResult
        {
            Success = success,
            Summary = success
                ? "[Codex CLI] 작업이 완료되었습니다. 변경 사항을 검토해 주세요."
                : $"[Codex CLI] 작업이 실패했습니다 (종료 코드 {process.ExitCode}).",
            RawOutput = rawOutput,
            ChangedFiles = Array.Empty<string>(),
            RequiresUserReview = true
        };
    }

    private static IReadOnlyList<string> BuildArguments(
        string sandbox, string outputFile, string workspaceRoot, string extraArgs)
    {
        var args = new List<string>
        {
            "exec",
            "--sandbox", sandbox,
            "--skip-git-repo-check",
            "--json",
            "--output-last-message", outputFile,
            "-C", workspaceRoot
        };

        if (!string.IsNullOrWhiteSpace(extraArgs))
        {
            args.AddRange(extraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return args;
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var extension = Path.GetExtension(executable);
        var isCommandScript =
            string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isCommandScript
                ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
                : executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (isCommandScript)
        {
            var line = string.Join(' ',
                new[] { executable }.Concat(arguments).Select(QuoteArgument));
            startInfo.Arguments = $"/d /s /c \"{line}\"";
        }
        else
        {
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return startInfo;
    }

    private static string QuoteArgument(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private string SafeLoadApiKey()
    {
        try { return _secretStore.LoadApiKey(); }
        catch { return string.Empty; }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
