using LivingMetalGhost.AppCore.SlashAgents;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

/// <summary>
/// Routes a single leading slash to the safe, LLM-planned capability agent.
/// Double slash remains normal text for comments, paths, and ordinary chat.
/// </summary>
public sealed class SlashIntentSkill : IGhostSkill
{
    private readonly SlashAgentService _slashAgentService;

    public SlashIntentSkill(SlashAgentService slashAgentService)
    {
        _slashAgentService = slashAgentService;
    }

    public string Name => "SlashIntent";
    public string Description =>
        "A single leading slash asks the basic LLM to select and run an approved capability.";

    public IReadOnlyList<string> Examples =>
    [
        "/날짜",
        "/시간",
        "/문지캠퍼스 점심 식사",
        "/대전 날씨"
    ];

    public bool CanHandle(UserRequest request)
    {
        return !request.UseAdvancedModel && IsSlashIntent(request.RawText);
    }

    public Task<SkillResult> HandleAsync(
        UserRequest request,
        CancellationToken cancellationToken)
    {
        return _slashAgentService.ExecuteAsync(
            ExtractIntentText(request.RawText),
            cancellationToken);
    }

    public static bool IsSlashIntent(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        var text = rawText.TrimStart();
        return text.StartsWith("/", StringComparison.Ordinal) &&
               !text.StartsWith("//", StringComparison.Ordinal);
    }

    public static string ExtractIntentText(string rawText)
    {
        return IsSlashIntent(rawText)
            ? rawText.TrimStart()[1..].Trim()
            : rawText.Trim();
    }
}
