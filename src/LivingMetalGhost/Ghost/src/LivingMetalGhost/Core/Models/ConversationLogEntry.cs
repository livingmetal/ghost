namespace LivingMetalGhost.Core.Models;

public sealed class ConversationLogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string UserText { get; set; } = string.Empty;
    public string AssistantText { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsProactive { get; set; }
}
