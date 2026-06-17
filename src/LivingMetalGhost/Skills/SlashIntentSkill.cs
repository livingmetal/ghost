using System.Globalization;
using LivingMetalGhost.Core.Facts.Meals.Kaist;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Reminders;

namespace LivingMetalGhost.Skills;

/// <summary>
/// Handles explicit capability-intent requests that start with a single slash.
/// A leading slash means "try to execute a basic-mode capability"; it is not a fixed command name.
/// Double slash remains normal text so code comments and paths do not get hijacked.
/// </summary>
public sealed class SlashIntentSkill : IGhostSkill
{
    private static readonly string[] HelpWords = ["help", "도움말", "도움", "기능", "modules", "모듈"];
    private static readonly string[] TimeWords = ["시간", "몇시", "몇 시", "지금", "현재", "time"];
    private static readonly string[] DateWords = ["날짜", "오늘", "요일", "date"];
    private static readonly string[] KaistMenuWords = ["카이스트", "kaist", "문지", "문지캠퍼스", "식단", "학식", "구내식당", "점심", "중식", "조식", "아침", "석식", "저녁"];
    private static readonly string[] TimerWords = ["타이머", "알림", "리마인더", "분 뒤", "시간 뒤", "초 뒤", "timer"];

    private readonly KaistMunjiMenuService _kaistMenuService;
    private readonly ReminderService _reminderService;

    public SlashIntentSkill(KaistMunjiMenuService kaistMenuService, ReminderService reminderService)
    {
        _kaistMenuService = kaistMenuService;
        _reminderService = reminderService;
    }

    public string Name => "SlashIntent";
    public string Description => "A single leading slash enters explicit basic-mode capability intent routing.";
    public IReadOnlyList<string> Examples => ["/지금 시간", "/오늘 날짜", "/문지 점심", "/10분 뒤 물 마시기"];

    public bool CanHandle(UserRequest request)
    {
        return !request.UseAdvancedModel && IsSlashIntent(request.RawText);
    }

    public async Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        var intentText = ExtractIntentText(request.RawText);
        var response = string.IsNullOrWhiteSpace(intentText)
            ? BuildHelpText()
            : await HandleIntentAsync(intentText, ct);

        return new SkillResult
        {
            BubbleText = response,
            Mood = "speaking",
            Action = "slash-intent",
            UsedLlm = false
        };
    }

    public static bool IsSlashIntent(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        var text = rawText.TrimStart();
        if (!text.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        // Keep // available for code comments, paths, and ordinary text.
        return !text.StartsWith("//", StringComparison.Ordinal);
    }

    private static string ExtractIntentText(string rawText)
    {
        return rawText.TrimStart()[1..].Trim();
    }

    private async Task<string> HandleIntentAsync(string text, CancellationToken ct)
    {
        if (ContainsAny(text, HelpWords))
        {
            return BuildHelpText();
        }

        var wantsTime = ContainsAny(text, TimeWords);
        var wantsDate = ContainsAny(text, DateWords);
        if (wantsTime || wantsDate)
        {
            return BuildTimeText(wantsTime, wantsDate);
        }

        if (ContainsAny(text, KaistMenuWords))
        {
            return await _kaistMenuService.GetTodayMenuTextAsync(text, ct);
        }

        if (ContainsAny(text, TimerWords))
        {
            return await _reminderService.CreateFromTextAsync(text, ct);
        }

        return "실행할 기능을 확정하지 못했어. 지금은 /지금 시간, /오늘 날짜, /문지 점심, /10분 뒤 알림 같은 형태를 기능 의도 모드로 구분할 수 있어.";
    }

    private static string BuildTimeText(bool includeTime, bool includeDate)
    {
        var now = GetKoreaNow();
        var culture = CultureInfo.GetCultureInfo("ko-KR");
        var parts = new List<string>();

        if (includeDate)
        {
            parts.Add($"오늘은 {now.ToString("yyyy년 M월 d일 dddd", culture)}야.");
        }

        if (includeTime)
        {
            parts.Add($"지금은 한국 시간 기준 {now:HH:mm}이야.");
        }

        if (parts.Count == 0)
        {
            parts.Add($"현재 한국 시간은 {now.ToString("yyyy-MM-dd HH:mm", culture)}이야.");
        }

        return string.Join(" ", parts);
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

    private static string BuildHelpText()
    {
        return "슬래시 기능 의도 모드야. 입력의 첫 유효 문자가 /이고 //가 아니면 일반 대화 대신 기능 실행 의도로 해석해. 지금은 /지금 시간, /오늘 날짜, /문지 점심, /10분 뒤 물 마시기를 처리할 수 있어.";
    }

    private static bool ContainsAny(string text, IEnumerable<string> words)
    {
        return words.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
