using System.IO;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 고급 Workbench 대화는 일반 대화 로그와 별도로 workspace/session 단위로 저장한다.
/// 원문 transcript는 보관하고, 재사용 컨텍스트는 pinned/project memory만 선별해 주입한다.
/// </summary>
public sealed class AdvancedSessionLogService
{
    private const string DefaultWorkspaceId = "default";
    private readonly string _workspaceRoot;
    private readonly string _sessionsRoot;
    private readonly string _summariesRoot;
    private readonly string _projectMemoryFile;
    private readonly string _pinnedContextFile;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private int _currentTurnCount;

    public AdvancedSessionLogService(AppPaths paths)
    {
        _workspaceRoot = Path.Combine(paths.Root, "Workspaces", DefaultWorkspaceId);
        _sessionsRoot = Path.Combine(_workspaceRoot, "sessions");
        _summariesRoot = Path.Combine(_workspaceRoot, "summaries");
        _projectMemoryFile = Path.Combine(_workspaceRoot, "project_memory.jsonl");
        _pinnedContextFile = Path.Combine(_workspaceRoot, "pinned_context.md");
        StartNewSession();
    }

    public string WorkspaceId => DefaultWorkspaceId;
    public string CurrentSessionId { get; private set; } = string.Empty;
    public string WorkspaceRoot => _workspaceRoot;
    public string CurrentSessionFile => Path.Combine(_sessionsRoot, $"{CurrentSessionId}.jsonl");
    public string ProjectMemoryFile => _projectMemoryFile;
    public string PinnedContextFile => _pinnedContextFile;
    public int CurrentTurnCount => _currentTurnCount;

    public void StartNewSession()
    {
        Directory.CreateDirectory(_sessionsRoot);
        Directory.CreateDirectory(_summariesRoot);
        CurrentSessionId = $"{DateTime.Now:yyyyMMdd-HHmmss}-advanced";
        _currentTurnCount = 0;
    }

    public async Task AppendTurnAsync(AdvancedSessionLogEntry entry, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_sessionsRoot);
        entry.WorkspaceId = WorkspaceId;
        entry.SessionId = CurrentSessionId;
        entry.Timestamp = DateTimeOffset.Now;

        var line = JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine;
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(CurrentSessionFile, line, Encoding.UTF8, cancellationToken);
            _currentTurnCount++;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public string BuildWorkbenchContextText()
    {
        Directory.CreateDirectory(_workspaceRoot);
        var memoryCount = CountJsonlLines(_projectMemoryFile);
        var pinnedLength = File.Exists(_pinnedContextFile)
            ? SafeReadAllText(_pinnedContextFile).Length
            : 0;

        return $"""
            Workspace: {WorkspaceId}
            Session: {CurrentSessionId}
            Turns: {_currentTurnCount}
            Project memory: {memoryCount} entries
            Pinned context: {pinnedLength} chars
            Session file:
            {CurrentSessionFile}
            """;
    }

    public string BuildReusablePromptContext(int maximumCharacters = 5000)
    {
        var blocks = new List<string>();
        var pinned = SafeReadAllText(_pinnedContextFile).Trim();
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            blocks.Add($"Pinned context:\n{pinned}");
        }

        var memory = ReadLastJsonLines(_projectMemoryFile, 12);
        if (!string.IsNullOrWhiteSpace(memory))
        {
            blocks.Add($"Project memory entries:\n{memory}");
        }

        var combined = string.Join("\n\n", blocks).Trim();
        if (combined.Length <= maximumCharacters)
        {
            return combined;
        }

        return combined[^maximumCharacters..];
    }

    public void EnsureWorkspaceFiles()
    {
        Directory.CreateDirectory(_workspaceRoot);
        Directory.CreateDirectory(_sessionsRoot);
        Directory.CreateDirectory(_summariesRoot);
        if (!File.Exists(_pinnedContextFile))
        {
            File.WriteAllText(_pinnedContextFile,
                "# Pinned Context\n\n고급모드에서 항상 참고할 짧은 프로젝트 맥락을 여기에 적습니다.\n",
                Encoding.UTF8);
        }

        if (!File.Exists(_projectMemoryFile))
        {
            File.WriteAllText(_projectMemoryFile, string.Empty, Encoding.UTF8);
        }
    }

    private static int CountJsonlLines(string filePath)
    {
        try
        {
            return File.Exists(filePath)
                ? File.ReadLines(filePath).Count(line => !string.IsNullOrWhiteSpace(line))
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string ReadLastJsonLines(string filePath, int count)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine,
                File.ReadLines(filePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .TakeLast(count));
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string SafeReadAllText(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
