using System.IO;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 고급 Workbench에서 승인된 프로젝트 기억을 jsonl로 저장하고 조회한다.
/// Transcript 전체를 기억으로 쓰지 않고, 선별된 항목만 재사용하기 위한 저장소다.
/// </summary>
public sealed class ProjectMemoryStore
{
    private readonly AdvancedSessionLogService _advancedSessionLogService;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ProjectMemoryStore(AdvancedSessionLogService advancedSessionLogService)
    {
        _advancedSessionLogService = advancedSessionLogService;
    }

    public string MemoryFile => _advancedSessionLogService.ProjectMemoryFile;

    public async Task<ProjectMemoryEntry> AddAsync(
        string content,
        string type,
        string sourceSessionId,
        string source,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken)
    {
        var entry = new ProjectMemoryEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.Now,
            WorkspaceId = _advancedSessionLogService.WorkspaceId,
            IsEnabled = true,
            Type = NormalizeType(type),
            Content = content.Trim(),
            SourceSessionId = sourceSessionId,
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim(),
            Tags = tags ?? Array.Empty<string>()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(MemoryFile)!);
        var line = JsonSerializer.Serialize(entry, _jsonOptions) + Environment.NewLine;
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(MemoryFile, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        return entry;
    }

    public async Task<bool> UpdateAsync(ProjectMemoryEntry updatedEntry, CancellationToken cancellationToken)
    {
        var entries = ReadAll().ToList();
        var index = entries.FindIndex(entry => string.Equals(entry.Id, updatedEntry.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        updatedEntry.Type = NormalizeType(updatedEntry.Type);
        updatedEntry.Content = updatedEntry.Content.Trim();
        updatedEntry.WorkspaceId = string.IsNullOrWhiteSpace(updatedEntry.WorkspaceId)
            ? _advancedSessionLogService.WorkspaceId
            : updatedEntry.WorkspaceId.Trim();
        if (string.IsNullOrWhiteSpace(updatedEntry.Content))
        {
            return false;
        }

        entries[index] = updatedEntry;
        await RewriteAllAsync(entries, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entries = ReadAll().ToList();
        var removed = entries.RemoveAll(entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        await RewriteAllAsync(entries, cancellationToken);
        return true;
    }

    public IReadOnlyList<ProjectMemoryEntry> ReadAll()
    {
        if (!File.Exists(MemoryFile))
        {
            return [];
        }

        var entries = new List<ProjectMemoryEntry>();
        foreach (var line in SafeReadLines(MemoryFile))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ProjectMemoryEntry>(line, _jsonOptions);
                if (entry is not null && !string.IsNullOrWhiteSpace(entry.Content))
                {
                    entry.Type = NormalizeType(entry.Type);
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // 손상된 한 줄 때문에 전체 기억 조회가 막히면 안 된다.
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ToArray();
    }

    public IReadOnlyList<ProjectMemoryEntry> ReadEnabled()
    {
        return ReadAll()
            .Where(entry => entry.IsEnabled)
            .ToArray();
    }

    public string BuildReusablePromptText(int maximumEntries = 12)
    {
        var entries = ReadEnabled()
            .Take(maximumEntries)
            .ToArray();
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

    public string BuildDisplayText(int maximumEntries = 30)
    {
        var allEntries = ReadAll();
        var entries = allEntries.Take(maximumEntries).ToArray();
        if (entries.Length == 0)
        {
            return "아직 프로젝트 기억이 없습니다." + Environment.NewLine + Environment.NewLine + MemoryFile;
        }

        var enabledCount = allEntries.Count(entry => entry.IsEnabled);
        var builder = new StringBuilder();
        builder.AppendLine($"프로젝트 기억: {allEntries.Count}개 / 활성 {enabledCount}개");
        builder.AppendLine($"저장 위치: {MemoryFile}");
        builder.AppendLine();

        foreach (var entry in entries)
        {
            builder.AppendLine($"[{entry.Type}] {(entry.IsEnabled ? "enabled" : "disabled")} · {entry.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            if (!string.IsNullOrWhiteSpace(entry.SourceSessionId))
            {
                builder.AppendLine($"session: {entry.SourceSessionId}");
            }

            builder.AppendLine(entry.Content);
            if (entry.Tags.Count > 0)
            {
                builder.AppendLine("tags: " + string.Join(", ", entry.Tags));
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private async Task RewriteAllAsync(IReadOnlyList<ProjectMemoryEntry> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MemoryFile)!);
        var builder = new StringBuilder();
        foreach (var entry in entries.OrderBy(entry => entry.CreatedAt))
        {
            builder.AppendLine(JsonSerializer.Serialize(entry, _jsonOptions));
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(MemoryFile, builder.ToString(), Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static string NormalizeType(string type)
    {
        var normalized = string.IsNullOrWhiteSpace(type) ? "decision" : type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "decision" or "fact" or "warning" or "todo" or "preference" => normalized,
            _ => "decision"
        };
    }

    private static IEnumerable<string> SafeReadLines(string filePath)
    {
        try
        {
            return File.ReadLines(filePath).ToArray();
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
