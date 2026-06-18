using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace LivingMetalGhost.Core.Reminders;

public sealed class ReminderService
{
    private const string PendingStatus = "scheduled";
    private static readonly Regex DelayRegex = new(
        @"(?<amount>\d+)\s*(?<unit>초|분|시간|s|sec|second|seconds|m|min|minute|minutes|h|hr|hour|hours)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ReminderStore _store;
    private readonly DispatcherTimer _timer;
    private bool _isProcessing;

    public ReminderService(ReminderStore store)
    {
        _store = store;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _timer.Tick += async (_, _) => await ProcessDueSafelyAsync(CancellationToken.None);
    }

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }

        // If the app was closed when a reminder became due, show it as soon as Ghost starts again.
        _ = ProcessDueSafelyAsync(CancellationToken.None);
    }

    public async Task<string> CreateFromTextAsync(string text, CancellationToken ct)
    {
        var now = GetKoreaNow();
        var parsed = TryParseDelay(text, now);
        if (parsed is null)
        {
            return "타이머 시간을 읽지 못했어. 예: /10분 뒤 물 마시기, /30초 뒤 확인, /2시간 뒤 회의 준비";
        }

        var message = parsed.Value.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "알림";
        }

        var entry = new ReminderEntry(
            Id: $"reminder-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..34],
            Message: message,
            CreatedAt: now,
            DueAt: parsed.Value.DueAt,
            Status: PendingStatus);
        await _store.AddAsync(entry, ct);
        ShowCreatedConfirmation(entry);

        return $"설정했어. {FormatDueTime(entry.DueAt)}에 \"{entry.Message}\" 알림을 띄울게.";
    }

    private async Task ProcessDueSafelyAsync(CancellationToken ct)
    {
        try
        {
            await ProcessDueAsync(ct);
        }
        catch
        {
            // Reminder checks should never crash the app or create dispatcher exception loops.
        }
    }

    private async Task ProcessDueAsync(CancellationToken ct)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            var now = GetKoreaNow();
            var dueEntries = await _store.GetDueAsync(now, ct);
            foreach (var entry in dueEntries)
            {
                await _store.CompleteAsync(entry.Id, ct);
                ShowDueReminder(entry);
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private static void ShowCreatedConfirmation(ReminderEntry entry)
    {
        ShowMessageBox(
            $"알림을 설정했어.\n\n시간: {FormatDueTime(entry.DueAt)}\n내용: {entry.Message}",
            "LivingMetalGhost 알림 설정 완료");
    }

    private static void ShowDueReminder(ReminderEntry entry)
    {
        ShowMessageBox(
            $"{entry.Message}\n\n예정 시간: {FormatDueTime(entry.DueAt)}",
            "LivingMetalGhost 알림");
    }

    private static void ShowMessageBox(string text, string title)
    {
        void Show()
        {
            MessageBox.Show(
                text,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Show();
            return;
        }

        dispatcher.Invoke(Show);
    }

    private static ParsedReminder? TryParseDelay(string text, DateTimeOffset now)
    {
        var match = DelayRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var amount = int.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var delay = unit switch
        {
            "초" or "s" or "sec" or "second" or "seconds" => TimeSpan.FromSeconds(amount),
            "분" or "m" or "min" or "minute" or "minutes" => TimeSpan.FromMinutes(amount),
            "시간" or "h" or "hr" or "hour" or "hours" => TimeSpan.FromHours(amount),
            _ => TimeSpan.Zero
        };

        if (delay <= TimeSpan.Zero)
        {
            return null;
        }

        var message = text[(match.Index + match.Length)..].Trim();
        message = CleanupMessage(message);
        return new ParsedReminder(now.Add(delay), message);
    }

    private static string CleanupMessage(string message)
    {
        var cleaned = message.Trim();
        var prefixes = new[] { "뒤에", "후에", "뒤", "후", "있다가", "알림", "알려줘", "알려 줘", "리마인드", "말해줘", "말해 줘" };
        foreach (var prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "알림" : cleaned;
    }

    private static string FormatDueTime(DateTimeOffset dueAt)
    {
        var culture = CultureInfo.GetCultureInfo("ko-KR");
        return dueAt.ToString("M월 d일 HH:mm", culture);
    }

    private static DateTimeOffset GetKoreaNow()
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
        }
        catch (InvalidTimeZoneException)
        {
            return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
        }
    }

    private readonly record struct ParsedReminder(DateTimeOffset DueAt, string Message);
}
