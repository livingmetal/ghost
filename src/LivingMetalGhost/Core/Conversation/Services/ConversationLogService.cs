using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationLogService
{
    private const string FilePrefix = "conversation-";
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ConversationLogService(AppPaths paths)
    {
        _paths = paths;
    }

    public Task AppendAsync(ConversationLogEntry entry)
    {
        return AppendAsync(entry, CancellationToken.None);
    }

    public async Task AppendAsync(ConversationLogEntry entry, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.Logs);
        var filePath = GetFilePath(entry.Timestamp.LocalDateTime.Date);
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(filePath, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public IReadOnlyList<DateTime> GetAvailableDates()
    {
        if (!Directory.Exists(_paths.Logs))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(_paths.Logs, $"{FilePrefix}*.jsonl")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name?[FilePrefix.Length..])
            .Select(value => DateTime.TryParseExact(
                value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
                ? date
                : (DateTime?)null)
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .OrderByDescending(date => date)
            .ToArray();
    }

    public async Task<IReadOnlyList<ConversationLogEntry>> ReadAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(date);
        if (!File.Exists(filePath))
        {
            return [];
        }

        var entries = new List<ConversationLogEntry>();
        await foreach (var line in File.ReadLinesAsync(filePath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<ConversationLogEntry>(line, JsonOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Keep valid entries visible even if one line is damaged.
            }
        }

        return entries.OrderBy(entry => entry.Timestamp).ToArray();
    }

    private string GetFilePath(DateTime date)
    {
        return Path.Combine(_paths.Logs, $"{FilePrefix}{date:yyyyMMdd}.jsonl");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
