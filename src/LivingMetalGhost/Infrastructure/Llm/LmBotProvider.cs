using System.Diagnostics;
using System.IO;
using System.Text;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

/// <summary>
/// 로컬에 설치된 claude 또는 codex CLI를 사용하는 고급 대화 프로바이더.
/// claude가 우선이며, 없으면 codex로 폴백한다.
/// 둘 다 없으면 예외를 던진다.
/// </summary>
public sealed class LmBotProvider : ILlmProvider
{
    private readonly AppConfigLoader _configLoader;
    private readonly CodexCliProvider _codexProvider;

    public LmBotProvider(AppConfigLoader configLoader, CodexCliProvider codexProvider)
    {
        _configLoader = configLoader;
        _codexProvider = codexProvider;
    }

    public string Name => "LmBot";

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        var info = await LocalLmDetector.DetectAsync();

        if (info.HasClaude)
        {
            return await RunClaudeAsync(request, ct);
        }

        if (info.HasCodex)
        {
            return await _codexProvider.GenerateAsync(request, ct);
        }

        throw new InvalidOperationException(
            "로컬에 claude 또는 codex CLI가 설치되어 있지 않습니다. PATH에 claude 또는 codex를 설치해 주세요.");
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var response = await GenerateAsync(request, ct);
        yield return new LlmStreamChunk { Text = response.Text, IsCompleted = true };
    }

    private async Task<LlmResponse> RunClaudeAsync(LlmRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var timeoutSeconds = Math.Clamp(config.AdvancedLlm.TimeoutSeconds, 30, 900);
        var prompt = BuildPrompt(request);

        // npm 전역 설치 경로를 직접 확인한다.
        // GUI 앱에서 PATH로 claude를 찾지 못하는 경우를 대비해 전체 경로를 우선 사용한다.
        var claudeExe = ResolveClaudeExecutable();

        // .cmd/.bat 파일은 직접 Process.Start 할 수 없으므로 cmd.exe를 통해 실행한다.
        // stdin은 temp 파일 리디렉션 대신 StandardInput 으로 주입해 인수 이스케이프 문제를 피한다.
        var isCmdScript = claudeExe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                          || claudeExe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        var psi = new ProcessStartInfo
        {
            FileName = isCmdScript ? "cmd.exe" : claudeExe,
            Arguments = isCmdScript ? $"/d /c \"{claudeExe}\" -p" : "-p",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // stdin 쓰기는 별도 Task로 실행해 stdout/stderr 버퍼가 꽉 차더라도 교착이 생기지 않게 한다.
        var stdinTask = Task.Run(async () =>
        {
            try
            {
                await process.StandardInput.WriteAsync(prompt);
                process.StandardInput.Close();
            }
            catch
            {
                // 프로세스가 이미 종료된 경우 무시
            }
        }, ct);

        using var cancelReg = ct.Register(() => TryKill(process));
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
            throw new TimeoutException($"claude 응답이 {timeoutSeconds}초 안에 완료되지 않았습니다.");
        }

        await stdinTask;
        var stdout = (await stdoutTask).Trim();
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (detail.Length > 800) detail = detail[..800];
            throw new InvalidOperationException(
                $"claude 실행이 실패했습니다. 종료 코드 {process.ExitCode}: {detail.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException("claude가 응답을 반환하지 않았습니다.");
        }

        return new LlmResponse { Text = stdout, FromFallback = false };
    }

    // npm 전역 설치 경로를 먼저 확인한다. 없으면 PATH에서 찾도록 bare name을 반환한다.
    private static string ResolveClaudeExecutable()
    {
        return LocalLmDetector.ClaudeCliDirectPaths.FirstOrDefault(File.Exists) ?? "claude";
    }

    private static string BuildPrompt(LlmRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine(request.SystemPrompt);

        if (request.History.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Conversation history:");
            foreach (var msg in request.History)
            {
                sb.Append(msg.Role);
                sb.Append(": ");
                sb.AppendLine(msg.Content);
            }
        }

        sb.AppendLine();
        sb.Append("user: ");
        sb.AppendLine(request.UserText);
        return sb.ToString();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}
