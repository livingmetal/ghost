using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Security;

namespace LivingMetalGhost.Agents;

/// <summary>
/// Claude Code CLI 를 외부 작업 에이전트로 호출하는 실행기 (안전 우선 skeleton).
///
/// 안전 설계:
/// - Suggest/Ask 모드에서는 절대 프로세스를 실행하지 않고, 실행될 명령을 "제안"으로만 돌려준다.
/// - 실제 실행(Apply/Execute)은 config.agents.enable_execution == true 인 경우에만 허용한다(이중 안전장치).
/// - workspace_root 가 유효해야 하며, 작업 디렉터리를 그 밖으로 벗어나지 않게 고정한다.
/// - ProcessStartInfo.UseShellExecute = false, 인자는 ArgumentList 로 전달해 shell injection 을 줄인다.
/// - 타임아웃을 강제하고, 캡처한 stdout/stderr 는 SecretMasker 로 민감정보를 가린 뒤 보관한다.
///
/// TODO: 실제 패치 적용/검증 워크플로(변경 파일 파싱, diff 미리보기, 승인 후 적용)는 후속 작업.
/// </summary>
public sealed class ClaudeCodeExecutor : IAgentExecutor
{
    private readonly AppConfigLoader _configLoader;
    private readonly DpapiSecretStore _secretStore;

    public ClaudeCodeExecutor(AppConfigLoader configLoader, DpapiSecretStore secretStore)
    {
        _configLoader = configLoader;
        _secretStore = secretStore;
    }

    public string Name => "claude-code";

    public async Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var agents = config.Agents;
        var executable = string.IsNullOrWhiteSpace(agents.ClaudeCode.Executable)
            ? "claude"
            : Environment.ExpandEnvironmentVariables(agents.ClaudeCode.Executable.Trim());

        var rootConfigured = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
            ? agents.WorkspaceRoot
            : request.WorkspaceRoot;

        // 실행 가능 여부 판정 (이중 안전장치).
        var executionAllowed =
            agents.EnableExecution &&
            request.ApprovalMode is AgentApprovalMode.Apply or AgentApprovalMode.Execute;

        var arguments = BuildArguments(request.Instruction, agents.ClaudeCode.ExtraArgs);
        var plannedCommand = $"{executable} {string.Join(' ', arguments)}";

        if (!executionAllowed)
        {
            // 제안만: 실제로 아무것도 실행하지 않는다.
            var reason = !agents.EnableExecution
                ? "agents.enable_execution 이 false 입니다."
                : "승인 모드가 apply/execute 가 아닙니다.";

            return new AgentResult
            {
                Success = true,
                Summary =
                    $"[Claude Code · 제안 모드] 아래 명령을 실행할 준비가 되었지만 실행하지 않았습니다.\n" +
                    $"- 사유: {reason}\n" +
                    $"- 예정 명령: {plannedCommand}\n" +
                    $"- 작업 루트: {(string.IsNullOrWhiteSpace(rootConfigured) ? "(미설정)" : rootConfigured)}",
                RawOutput = string.Empty,
                ChangedFiles = Array.Empty<string>(),
                RequiresUserReview = true
            };
        }

        // 여기서부터는 실행 경로. workspace_root 검증 필수.
        if (!WorkspaceGuard.TryResolveRoot(rootConfigured, out var workspaceRoot, out var rootError))
        {
            return new AgentResult
            {
                Success = false,
                Summary = $"[Claude Code] 실행을 중단했습니다: {rootError}",
                RequiresUserReview = true
            };
        }

        var timeoutSeconds = Math.Clamp(agents.TimeoutSeconds, 30, 1800);
        using var process = new Process { StartInfo = CreateStartInfo(executable, arguments, workspaceRoot) };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new AgentResult
            {
                Success = false,
                Summary = "[Claude Code] CLI 실행 파일을 찾지 못했습니다. 설정에서 실행 경로를 확인해 주세요.",
                RawOutput = ex.Message,
                RequiresUserReview = true
            };
        }

        using var registration = ct.Register(() => TryKill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            return new AgentResult
            {
                Success = false,
                Summary = $"[Claude Code] 응답이 {timeoutSeconds}초 안에 끝나지 않아 중단했습니다.",
                RequiresUserReview = true
            };
        }

        var apiKey = SafeLoadApiKey();
        var stdout = SecretMasker.Mask(await stdoutTask, apiKey);
        var stderr = SecretMasker.Mask(await stderrTask, apiKey);
        var success = process.ExitCode == 0;

        return new AgentResult
        {
            Success = success,
            Summary = success
                ? "[Claude Code] 작업이 완료되었습니다. 변경 사항을 검토해 주세요."
                : $"[Claude Code] 작업이 실패했습니다 (종료 코드 {process.ExitCode}).",
            RawOutput = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}",
            // TODO: stdout 에서 변경 파일 목록 파싱 후 WorkspaceGuard.FindEscapingPaths 로 재검증.
            ChangedFiles = Array.Empty<string>(),
            RequiresUserReview = true
        };
    }

    private static IReadOnlyList<string> BuildArguments(string instruction, string extraArgs)
    {
        // print 모드(-p)로 비대화형 실행. 인자는 ArgumentList 로 분리 전달한다.
        var args = new List<string> { "-p", instruction };
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
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (isCommandScript)
        {
            // .cmd/.bat 는 cmd.exe 경유. 각 인자를 개별 따옴표 처리해 injection 위험을 줄인다.
            var line = string.Join(' ', new[] { executable }.Concat(arguments).Select(QuoteArgument));
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

    private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private string SafeLoadApiKey()
    {
        try
        {
            return _secretStore.LoadApiKey();
        }
        catch
        {
            return string.Empty;
        }
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
