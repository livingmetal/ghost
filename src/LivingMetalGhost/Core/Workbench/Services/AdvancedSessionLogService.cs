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
    private readonly WorkspaceStore _workspaceStore;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private int _currentTurnCount;

    public AdvancedSessionLogService(AppPaths paths, WorkspaceStore workspaceStore)
    {
        _workspaceRoot = Path.Combine(paths.Root, "Workspaces", DefaultWorkspaceId);
        _sessionsRoot = Path.Combine(_workspaceRoot, "sessions");
        _summariesRoot = Path.Combine(_workspaceRoot, "summaries");
        _projectMemoryFile = Path.Combine(_workspaceRoot, "project_memory.jsonl");
        _pinnedContextFile = Path.Combine(_workspaceRoot, "pinned_context.md");
        _workspaceStore = workspaceStore;
        StartNewSession();
    }

    public string WorkspaceId => DefaultWorkspaceId;
    public string CurrentSessionId { get; private set; } = string.Empty;
    public string WorkspaceRoot => _workspaceRoot;
    public string CurrentSessionFile => Path.Combine(_sessionsRoot, $"{CurrentSessionId}.jsonl");
    public string CurrentSummaryFile => Path.Combine(_summariesRoot, $"{CurrentSessionId}.md");
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

    public async Task<string> GenerateCurrentSessionSummaryAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_summariesRoot);
        var entries = ReadSessionEntries(CurrentSessionFile);
        var summary = BuildSessionSummaryMarkdown(entries);
        await File.WriteAllTextAsync(CurrentSummaryFile, summary, Encoding.UTF8, cancellationToken);
        return CurrentSummaryFile;
    }

    public string BuildWorkbenchContextText()
    {
        Directory.CreateDirectory(_workspaceRoot);
        var workspace = _workspaceStore.Load();
        var memoryCount = CountJsonlLines(_projectMemoryFile);
        var enabledMemoryCount = ReadEnabledProjectMemoryEntries().Count;
        var pinnedLength = File.Exists(_pinnedContextFile)
            ? SafeReadAllText(_pinnedContextFile).Length
            : 0;
        var summaryState = File.Exists(CurrentSummaryFile) ? "exists" : "none";

        return $"""
            Workspace: {workspace.DisplayName} ({workspace.WorkspaceId})
            Root: {(string.IsNullOrWhiteSpace(workspace.RootPath) ? "not set" : workspace.RootPath)}
            Session: {CurrentSessionId}
            Turns: {_currentTurnCount}
            Project memory: {enabledMemoryCount}/{memoryCount} enabled
            Pinned context: {pinnedLength} chars
            Summary: {summaryState}
            Session file:
            {CurrentSessionFile}
            Summary file:
            {CurrentSummaryFile}
            """;
    }

    public string BuildReusablePromptContext(int maximumCharacters = 5000)
    {
        var blocks = new List<string>();
        var workspacePolicy = _workspaceStore.BuildPromptContext().Trim();
        if (!string.IsNullOrWhiteSpace(workspacePolicy))
        {
            blocks.Add(workspacePolicy);
        }

        var pinned = SafeReadAllText(_pinnedContextFile).Trim();
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            blocks.Add($"Pinned context:\n{pinned}");
        }

        var memory = BuildEnabledProjectMemoryPromptBlock(12);
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

    private string BuildSessionSummaryMarkdown(IReadOnlyList<AdvancedSessionLogEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Advanced Session Summary");
        builder.AppendLine();
        builder.AppendLine($"- Workspace: {WorkspaceId}");
        builder.AppendLine($"- Session: {CurrentSessionId}");
        builder.AppendLine($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"- Turns: {entries.Count}");
        builder.AppendLine($"- Transcript: `{CurrentSessionFile}`");
        builder.AppendLine();

        if (entries.Count == 0)
        {
            builder.AppendLine("아직 저장된 고급모드 턴이 없습니다.");
            return builder.ToString();
        }

        var providerModels = entries
            .Select(entry => $"{entry.Provider}/{entry.Model}")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (providerModels.Length > 0)
        {
            builder.AppendLine("## Provider / Model");
            foreach (var providerModel in providerModels)
            {
                builder.AppendLine($"- {providerModel}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Timeline");
        foreach (var entry in entries)
        {
            builder.AppendLine($"### {entry.Timestamp:HH:mm:ss} · {entry.Action}");
            builder.AppendLine($"- User: {TrimForSummary(entry.UserText, 240)}");
            builder.AppendLine($"- Assistant: {TrimForSummary(entry.AssistantText, 360)}");
            if (entry.UsedContext.Count > 0)
            {
                builder.AppendLine($"- Used context: {string.Join(", ", entry.UsedContext)}");
            }

            builder.AppendLine();
        }

        var candidateMemories = ExtractCandidateMemoryLines(entries);
        builder.AppendLine("## Candidate Memory Lines");
        if (candidateMemories.Count == 0)
        {
            builder.AppendLine("- 자동 후보 없음. Workbench에서 필요한 답변을 수동으로 프로젝트 기억에 승격하세요.");
        }
        else
        {
            foreach (var candidate in candidateMemories)
            {
                builder.AppendLine($"- {candidate}");
            }
        }

        return builder.ToString();
    }

    private IReadOnlyList<AdvancedSessionLogEntry> ReadSessionEntries(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var entries = new List<AdvancedSessionLogEntry>();
        foreach (var line in SafeReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<AdvancedSessionLogEntry>(line, _jsonOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // 손상된 줄 하나 때문에 요약 생성 전체를 막지 않는다.
            }
        }

        return entries.OrderBy(entry => entry.Timestamp).ToArray();
    }

    private IReadOnlyList<ProjectMemoryEntry> ReadEnabledProjectMemoryEntries()
    {
        if (!File.Exists(_projectMemoryFile))
        {
            return [];
        }

        var entries = new List<ProjectMemoryEntry>();
        foreach (var line in SafeReadLines(_projectMemoryFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ProjectMemoryEntry>(line, _jsonOptions);
                if (entry is not null && entry.IsEnabled && !string.IsNullOrWhiteSpace(entry.Content))
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ToArray();
    }

    private string BuildEnabledProjectMemoryPromptBlock(int maximumEntries)
    {
        var entries = ReadEnabledProjectMemoryEntries().Take(maximumEntries).ToArray();
        if (entries.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.AppendLine($"- [{entry.Type}] {entry.Content}");
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> ExtractCandidateMemoryLines(IEnumerable<AdvancedSessionLogEntry> entries)
    {
        var keywords = new[]
        {
            "결론", "추천", "주의", "위험", "다음 단계", "반영", "구조", "저장", "분리", "승인", "워크벤치", "기억"
        };
        var candidates = new List<string>();

        foreach (var entry in entries)
        {
            var lines = entry.AssistantText
                .Replace("\r\n", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                var normalized = line.Trim().TrimStart('-', '*', ' ', '\t');
                if (normalized.Length < 10)
                {
                    continue;
                }

                if (keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(TrimForSummary(normalized, 220));
                }

                if (candidates.Count >= 12)
                {
                    return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                }
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string TrimForSummary(string text, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (normalized.Length <= maximumCharacters)
        {
            return normalized;
        }

        return normalized[..maximumCharacters].TrimEnd() + "…";
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

    private static IEnumerable<string> SafeReadLines(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? File.ReadLines(filePath).ToArray() : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }
}
