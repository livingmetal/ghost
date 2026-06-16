namespace LivingMetalGhost.Core.Models;

public sealed class StoryState
{
    public bool Enabled { get; set; }
    public string Title { get; set; } = "default";
    public string Scene { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PlayerRole { get; set; } = "주인공";
    public string Mood { get; set; } = "daily";
    public int Tension { get; set; }
    public int Affinity { get; set; } = 50;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
