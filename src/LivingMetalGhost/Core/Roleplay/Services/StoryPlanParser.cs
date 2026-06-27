using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public static class StoryPlanParser
{
    public static StoryPlan? Parse(string? text)
    {
        var json = RoleplayJson.ExtractObject(text);
        if (json is null)
        {
            return null;
        }

        StoryPlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<StoryPlan>(json, RoleplayJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (plan is null)
        {
            return null;
        }

        Normalize(plan);
        return plan.HasContent() ? plan : null;
    }

    private static void Normalize(StoryPlan plan)
    {
        plan.Title = Clean(plan.Title, 120, "Untitled Story");
        plan.Premise = Clean(plan.Premise, 1200);
        plan.Genre = Clean(plan.Genre, 160);
        plan.Notes = NormalizeStrings(plan.Notes, 12, 300);
        plan.Acts = (plan.Acts ?? [])
            .Where(act => act is not null)
            .Take(5)
            .Select((act, index) => new StoryAct
            {
                Act = act.Act > 0 ? act.Act : index + 1,
                Goal = Clean(act.Goal, 400),
                Beats = NormalizeStrings(act.Beats, 10, 300)
            })
            .Where(act => !string.IsNullOrWhiteSpace(act.Goal) || act.Beats.Count > 0)
            .ToList();
        plan.BeatSeeds = (plan.BeatSeeds ?? [])
            .Where(seed => seed is not null && !string.IsNullOrWhiteSpace(seed.Beat))
            .Take(16)
            .Select(seed => new StoryBeatSeed
            {
                When = Clean(seed.When, 160),
                Beat = Clean(seed.Beat, 400),
                Purpose = Clean(seed.Purpose, 300)
            })
            .ToList();
        plan.EndingCandidates = (plan.EndingCandidates ?? [])
            .Where(ending => ending is not null && !string.IsNullOrWhiteSpace(ending.Name))
            .Take(6)
            .Select(ending => new StoryEndingCandidate
            {
                Name = Clean(ending.Name, 120),
                Condition = Clean(ending.Condition, 300),
                Summary = Clean(ending.Summary, 500)
            })
            .ToList();
    }

    private static List<string> NormalizeStrings(IEnumerable<string>? values, int maximumCount, int maximumLength) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Clean(value, maximumLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumCount)
            .ToList();

    private static string Clean(string? value, int maximumLength, string fallback = "")
    {
        var clean = value?.Trim() ?? string.Empty;
        if (clean.Length > maximumLength)
        {
            clean = clean[..maximumLength].TrimEnd() + "…";
        }

        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }
}
