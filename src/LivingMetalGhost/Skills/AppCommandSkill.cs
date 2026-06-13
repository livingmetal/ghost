using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public sealed class AppCommandSkill : IGhostSkill
{
    public string Name => "AppCommand";
    public string Description => "Handles basic shell commands for the mascot application.";
    public IReadOnlyList<string> Examples => ["설정 열어", "종료해"];

    public bool CanHandle(UserRequest request)
        => request.RawText.Contains("설정", StringComparison.OrdinalIgnoreCase)
        || request.RawText.Contains("종료", StringComparison.OrdinalIgnoreCase);

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        var text = request.RawText.Contains("설정", StringComparison.OrdinalIgnoreCase)
            ? "설정 창을 여는 명령으로 해석했어요."
            : "종료 명령으로 해석했어요.";

        return Task.FromResult(new SkillResult
        {
            BubbleText = text,
            Mood = "strict",
            Action = "command",
            UsedLlm = false
        });
    }
}

