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
    public string Description => "UI helper actions are exposed from buttons and tray menus, not from chat text.";
    public IReadOnlyList<string> Examples => [];

    public bool CanHandle(UserRequest request) => false;

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        return Task.FromResult(new SkillResult
        {
            BubbleText = string.Empty,
            Mood = "neutral",
            Action = string.Empty,
            UsedLlm = false
        });
    }
}
