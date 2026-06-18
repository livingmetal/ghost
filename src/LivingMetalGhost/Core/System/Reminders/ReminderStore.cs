using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.Core.Reminders;

public sealed class ReminderStore
{
    private const string PendingStatus = "scheduled";
    private const string CompletedStatus = "completed";
    private readonly string _reminderRoot;
    private readonly string _remindersFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public ReminderStore(AppPaths paths)
    {
        _reminderRoot = Path.Combine(paths.Root, "Reminders");
        _remindersFile = Path.Combine(_reminderRoot, "reminders.json");
    }

    public async Task AddAsync(ReminderEntry entry, CancellationToken ct)
    {
        var entries = await ReadAllAsync(ct);
        entries.RemoveAll(existing => string.Equals(existing.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        entries.Add(entry);
        await SaveAllAsync(entries, ct);
    }

    public async Task<IReadOnlyList<ReminderEntry>> GetDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var entries = await ReadAllAsync(ct);
        return entries
            .Where(entry => string.Equals(entry.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.DueAt <= now)
            .OrderBy(entry => entry.DueAt)
            .ToArray();
    }

    public async Task CompleteAsync(string id, CancellationToken ct)
    {
        var entries = await ReadAllAsync(ct);
        var index = entries.FindIndex(entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var entry = entries[index];
        entries[index] = entry with { Status = CompletedStatus };
        await SaveAllAsync(entries, ct);
    }

    private async Task<List<ReminderEntry>> ReadAllAsync(CancellationToken ct)
    {
        if (!File.Exists(_remindersFile))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(_remindersFile, ct);
            return JsonSerializer.Deserialize<List<ReminderEntry>>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
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

    private async Task SaveAllAsync(IReadOnlyList<ReminderEntry> entries, CancellationToken ct)
    {
        Directory.CreateDirectory(_reminderRoot);
        var json = JsonSerializer.Serialize(entries.OrderBy(entry => entry.DueAt).ToArray(), _jsonOptions);
        var tempFile = _remindersFile + ".tmp";
        await File.WriteAllTextAsync(tempFile, json, ct);
        if (File.Exists(_remindersFile))
        {
            File.Delete(_remindersFile);
        }

        File.Move(tempFile, _remindersFile);
    }
}
