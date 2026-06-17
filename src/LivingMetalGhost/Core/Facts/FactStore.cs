using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Facts;

public sealed class FactStore
{
    private readonly string _factRoot;
    private readonly string _factsFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public FactStore(AppPaths paths)
    {
        _factRoot = Path.Combine(paths.Root, "Facts");
        _factsFile = Path.Combine(_factRoot, "facts.jsonl");
    }

    public string FactRoot => _factRoot;
    public string FactsFile => _factsFile;

    public async Task<FactEntry?> TryGetLatestAsync(string key, CancellationToken ct)
    {
        var entries = await ReadAllAsync(ct);
        return entries
            .Where(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.ObservedAt)
            .FirstOrDefault();
    }

    public bool IsFresh(FactEntry entry, DateTimeOffset now)
    {
        return entry.ValidUntil is null || entry.ValidUntil.Value >= now;
    }

    public async Task UpsertAsync(FactEntry entry, CancellationToken ct)
    {
        Directory.CreateDirectory(_factRoot);
        var entries = await ReadAllAsync(ct);
        var nextEntries = entries
            .Where(existing => !string.Equals(existing.Key, entry.Key, StringComparison.OrdinalIgnoreCase))
            .Append(entry)
            .OrderBy(existing => existing.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(existing => existing.ObservedAt)
            .ToArray();

        var lines = nextEntries.Select(existing => JsonSerializer.Serialize(existing, _jsonOptions)).ToArray();
        var tempFile = _factsFile + ".tmp";
        await File.WriteAllLinesAsync(tempFile, lines, ct);
        if (File.Exists(_factsFile))
        {
            File.Delete(_factsFile);
        }

        File.Move(tempFile, _factsFile);
    }

    private async Task<IReadOnlyList<FactEntry>> ReadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_factsFile))
        {
            return [];
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(_factsFile, ct);
            var entries = new List<FactEntry>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<FactEntry>(line, _jsonOptions);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                    // Ignore corrupt JSONL rows so one bad fact does not break the whole assistant.
                }
            }

            return entries;
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
