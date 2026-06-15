using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

/// <summary>ViewModel/Dispatcher 가 처리할 수 있는 앱 명령 액션 값.</summary>
public static class AppCommandActions
{
    public const string OpenSettings = "open-settings";
    public const string ExitApp = "exit-app";
    public const string OpenLog = "open-log";
}

public sealed class AppCommandSkill : IGhostSkill
{
    public string Name => "AppCommand";
    public string Description => "마스코트 앱의 기본 셸 명령(설정/종료/로그/고급 모드)을 해석한다.";
    public IReadOnlyList<string> Examples => ["설정 열어", "종료해", "로그 보여줘", "고급 모드 켜"];

    public bool CanHandle(UserRequest request) => Match(request.RawText) is not null;

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        // 명확한 Action 값을 반환한다. 실제 동작 연결은 ViewModel/AppCommandDispatcher 담당.
        var (action, bubble, mood) = Match(request.RawText) ?? (
            AppCommandActions.OpenSettings, "설정 창을 여는 명령으로 해석했어요.", "strict");

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

        if (Contains(raw, "로그"))
        {
            return (AppCommandActions.OpenLog, "대화 로그 창을 여는 명령으로 해석했어요.", "acknowledging");
        }

        if (Contains(raw, "종료") || Contains(raw, "끄기") || Contains(raw, "exit"))
        {
            return (AppCommandActions.ExitApp, "종료 명령으로 해석했어요.", "strict");
        }

        if (Contains(raw, "설정") || Contains(raw, "settings"))
        {
            return (AppCommandActions.OpenSettings, "설정 창을 여는 명령으로 해석했어요.", "strict");
        }

        return null;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
