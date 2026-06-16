using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public static class AppCommandActions
{
    public const string OpenSettings = "open-settings";
    public const string OpenLog = "open-log";
}

public sealed class AppCommandSkill : IGhostSkill
{
    public string Name => "AppCommand";
    public string Description => "UI helper.";
    public IReadOnlyList<string> Examples => ["설정 열어", "로그 보여줘"];

    public bool CanHandle(UserRequest request) => Match(request.RawText) is not null;

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        var (action, bubble, mood) = Match(request.RawText) ??
            (AppCommandActions.OpenSettings, "설정을 열게요.", "strict");

        return Task.FromResult(new SkillResult
        {
            BubbleText = bubble,
            Mood = mood,
            Action = action,
            UsedLlm = false
        });
    }

    private static (string Action, string Bubble, string Mood)? Match(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Contains("로그", StringComparison.OrdinalIgnoreCase))
        {
            return (AppCommandActions.OpenLog, "대화 로그를 열게요.", "acknowledging");
        }

        if (raw.Contains("설정", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("settings", StringComparison.OrdinalIgnoreCase))
        {
            return (AppCommandActions.OpenSettings, "설정을 열게요.", "strict");
        }

        return null;
    }
}
