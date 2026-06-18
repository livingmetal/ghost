using System.Diagnostics;
using System.IO;

namespace LivingMetalGhost.Providers.Llm;

public sealed record LocalLmInfo(bool HasClaude, bool HasCodex)
{
    public bool IsAvailable => HasClaude || HasCodex;
}

public static class LocalLmDetector
{
    // npm 전역 설치 시 claude CLI가 위치하는 경로.
    // GUI 앱은 셸과 PATH를 다르게 상속받을 수 있어 직접 경로를 먼저 확인한다.
    internal static readonly string[] ClaudeCliDirectPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude.cmd"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "claude"),
    ];

    private static volatile LocalLmInfo? _cached;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public static async Task<LocalLmInfo> DetectAsync()
    {
        if (_cached is not null) return _cached;

        await Lock.WaitAsync();
        try
        {
            if (_cached is not null) return _cached;

            _cached = new LocalLmInfo(
                HasClaude: ClaudeCliDirectPaths.Any(File.Exists) || await IsAvailableAsync("claude"),
                HasCodex: await IsAvailableAsync("codex"));
            return _cached;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static void Invalidate() => _cached = null;

    // npm 전역 설치 CLI는 .cmd 래퍼이므로 Process.Start("claude") 가 실패한다.
    // where.exe 명령으로 PATH 내 존재 여부만 확인한다.
    private static async Task<bool> IsAvailableAsync(string tool)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = tool,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
