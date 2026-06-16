using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

/// <summary>ViewModel/Dispatcher 가 처리할 수 있는 앱 명령 액션 값.</summary>
public static class AppCommandActions
{
    public const string OpenSettings = "open-settings";
    public const string ExitApp = "exit-app";
    public const string OpenLog = "open-log";
    public const string EnableStoryMode = "enable-story-mode";
    public const string DisableStoryMode = "disable-story-mode";
}

public sealed class AppCommandSkill : IGhostSkill
{
    public string Name => "AppCommand";
    public string Description => "마스코트 앱의 기본 셸 명령(설정/종료/로그/스토리 모드)을 해석한다.";
    public IReadOnlyList<string> Examples => ["설정 열어", "종료해", "로그 보여줘", "소설 모드로 가자", "현실 작업으로 돌아가자"];

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

        if (IsStoryModeDisable(raw))
        {
            return (AppCommandActions.DisableStoryMode, "스토리 모드는 접어둘게. 이제 일상 대화 기준으로 받을게.", "acknowledging");
        }

        if (IsStoryModeEnable(raw))
        {
            return (AppCommandActions.EnableStoryMode, "스토리 모드로 전환할게. 허구 장면은 실제 작업 기억과 섞지 않겠어.", "curious");
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

    private static bool IsStoryModeEnable(string raw)
    {
        return (Contains(raw, "스토리 모드") ||
                Contains(raw, "소설 모드") ||
                Contains(raw, "ai소설") ||
                Contains(raw, "AI소설") ||
                Contains(raw, "미연시 모드")) &&
               !IsStoryModeDisable(raw);
    }

    private static bool IsStoryModeDisable(string raw)
    {
        var mentionsStory = Contains(raw, "스토리") ||
                            Contains(raw, "소설") ||
                            Contains(raw, "미연시") ||
                            Contains(raw, "이야기");
        var wantsExit = Contains(raw, "끄") ||
                        Contains(raw, "종료") ||
                        Contains(raw, "나가") ||
                        Contains(raw, "중지") ||
                        Contains(raw, "일상 모드") ||
                        Contains(raw, "일상모드") ||
                        Contains(raw, "현실 작업") ||
                        Contains(raw, "현실로") ||
                        Contains(raw, "돌아가");

        return (mentionsStory && wantsExit) || Contains(raw, "현실 작업으로 돌아");
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
