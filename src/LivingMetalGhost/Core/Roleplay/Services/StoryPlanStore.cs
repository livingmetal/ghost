using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public sealed class StoryPlanStore
{
    private readonly AppPaths _paths;

    public StoryPlanStore(AppPaths paths)
    {
        _paths = paths;
    }

    public string StoryRoot => Path.Combine(_paths.Root, "story");
    public string StoryPlanFile => Path.Combine(StoryRoot, "story_plan.json");

    public StoryPlan Load()
    {
        Directory.CreateDirectory(StoryRoot);
        if (!File.Exists(StoryPlanFile))
        {
            var initial = new StoryPlan();
            Save(initial);
            return initial;
        }

        try
        {
            var json = File.ReadAllText(StoryPlanFile);
            return StoryPlanParser.Parse(json) ?? new StoryPlan();
        }
        catch
        {
            return new StoryPlan();
        }
    }

    public void Save(StoryPlan plan)
    {
        Directory.CreateDirectory(StoryRoot);
        plan.UpdatedAt = DateTimeOffset.Now;
        File.WriteAllText(StoryPlanFile, JsonSerializer.Serialize(plan, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };
}
