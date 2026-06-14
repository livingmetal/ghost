using System.Diagnostics;
using System.IO;

namespace LivingMetalGhost.Providers.Llm;

/// <summary>
/// Windows에 설치된 AI 앱(ChatGPT, Claude) 감지기.
/// 데스크탑 앱 실행 파일과 CLI 도구 존재 여부를 모두 확인한다.
/// </summary>
public sealed record InstalledAppInfo(
    bool HasChatGpt,
    bool HasClaude,
    string? ChatGptExePath,
    string? ClaudeExePath,
    bool HasChatGptCli,
    bool HasClaudeCli)
{
    public bool IsAnyAvailable => HasChatGpt || HasClaude;
}

public static class InstalledAppDetector
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string AppData =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string[] ChatGptExePaths =
    [
        // Windows Store 앱 실행 별칭 (MSIX 설치 시 자동 생성)
        Path.Combine(LocalAppData, "Microsoft", "WindowsApps", "ChatGPT.exe"),
        Path.Combine(LocalAppData, "Programs", "ChatGPT", "ChatGPT.exe"),
        Path.Combine(LocalAppData, "Programs", "chatgpt", "ChatGPT.exe"),
        Path.Combine(LocalAppData, "OpenAI", "ChatGPT", "ChatGPT.exe"),
    ];

    private static readonly string[] ClaudeExePaths =
    [
        // Windows Store 앱 실행 별칭 (%LOCALAPPDATA%\Microsoft\WindowsApps\에 stub 생성)
        Path.Combine(LocalAppData, "Microsoft", "WindowsApps", "Claude.exe"),
        Path.Combine(LocalAppData, "Microsoft", "WindowsApps", "claude.exe"),
        // 일반 설치 경로
        Path.Combine(LocalAppData, "AnthropicPBC", "Claude", "Claude.exe"),
        Path.Combine(LocalAppData, "Programs", "Claude", "Claude.exe"),
        Path.Combine(LocalAppData, "Anthropic", "Claude", "Claude.exe"),
    ];

    // npm 전역 설치 시 claude CLI는 %APPDATA%\npm\ 에 설치된다.
    // GUI 앱은 셸과 PATH를 다르게 상속받을 수 있으므로 직접 경로를 먼저 확인한다.
    private static readonly string[] ClaudeCliDirectPaths =
    [
        Path.Combine(AppData, "npm", "claude.cmd"),
        Path.Combine(AppData, "npm", "claude"),
    ];

    private static volatile InstalledAppInfo? _cached;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public static async Task<InstalledAppInfo> DetectAsync()
    {
        if (_cached is not null) return _cached;

        await Lock.WaitAsync();
        try
        {
            if (_cached is not null) return _cached;

            var chatGptExe = FindExe(ChatGptExePaths);
            var claudeExe = FindExe(ClaudeExePaths)
                            ?? FindWindowsStorePackageDir("Claude");

            // npm 직접 경로 우선 확인 → 없으면 where.exe 로 PATH 탐색
            var hasClaudeCli = ClaudeCliDirectPaths.Any(File.Exists)
                               || await IsCliAvailableAsync("claude");

            // chatgpt CLI는 stdin 기반 대화가 불안정해 감지하지 않는다.
            // ChatGPT는 OpenAI API 경유로만 지원한다.
            _cached = new InstalledAppInfo(
                HasChatGpt: chatGptExe is not null,
                HasClaude: claudeExe is not null || hasClaudeCli,
                ChatGptExePath: chatGptExe,
                ClaudeExePath: claudeExe,
                HasChatGptCli: false,
                HasClaudeCli: hasClaudeCli);
            return _cached;
        }
        finally
        {
            Lock.Release();
        }
    }

    public static void Invalidate() => _cached = null;

    private static string? FindExe(string[] paths) =>
        paths.FirstOrDefault(File.Exists);

    // Windows Store(MSIX) 패키지는 %LOCALAPPDATA%\Packages\<AppName>_*\ 패턴으로 설치된다.
    // 실제 exe는 보호된 WindowsApps 폴더에 있으므로 패키지 디렉터리 존재 여부로 대신 확인한다.
    private static string? FindWindowsStorePackageDir(string appName)
    {
        var packagesDir = Path.Combine(LocalAppData, "Packages");
        if (!Directory.Exists(packagesDir)) return null;

        try
        {
            return Directory.EnumerateDirectories(
                    packagesDir,
                    $"{appName}_*",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> IsCliAvailableAsync(string tool)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    // where.exe를 직접 사용해 cmd.exe 래퍼 없이 PATH를 탐색한다.
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
