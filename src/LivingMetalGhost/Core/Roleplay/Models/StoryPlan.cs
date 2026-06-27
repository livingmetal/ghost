namespace LivingMetalGhost.Core.Models;

public sealed class StoryPlan
{
    public int SchemaVersion { get; set; }
    public string CharacterId { get; set; } = string.Empty;
    public string WriterSettingsFingerprint { get; set; } = string.Empty;
    public string Title { get; set; } = "Untitled Story";
    public string Premise { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public List<StoryAct> Acts { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public List<StoryBeatSeed> BeatSeeds { get; set; } = [];
    public List<StoryEndingCandidate> EndingCandidates { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public bool HasContent() =>
        !string.IsNullOrWhiteSpace(Premise) ||
        (Acts?.Any(act => act is not null &&
            (!string.IsNullOrWhiteSpace(act.Goal) || (act.Beats?.Count ?? 0) > 0)) ?? false) ||
        (BeatSeeds?.Count ?? 0) > 0;
}

public sealed class StoryAct
{
    public int Act { get; set; }
    public string Goal { get; set; } = string.Empty;
    public List<string> Beats { get; set; } = [];
}

public sealed class StoryBeatSeed
{
    public string When { get; set; } = string.Empty;
    public string Beat { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

public sealed class StoryEndingCandidate
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
