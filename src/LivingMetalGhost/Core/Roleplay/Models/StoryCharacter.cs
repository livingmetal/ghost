namespace LivingMetalGhost.Core.Models;

public sealed class StoryCharacterDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string BaseAppearance { get; set; } = string.Empty;
    public string BaseBackground { get; set; } = string.Empty;
    public string BasePersonality { get; set; } = string.Empty;
    public string SpeechStyle { get; set; } = string.Empty;
    public List<string> Boundaries { get; set; } = [];
    public List<string> Secrets { get; set; } = [];
}

public sealed class StoryCharacterState
{
    public string CharacterId { get; set; } = string.Empty;
    public string CurrentAppearance { get; set; } = string.Empty;
    public Dictionary<string, int> CurrentEmotion { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> RelationshipMetrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PersonalityDrift { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string CurrentGoal { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
