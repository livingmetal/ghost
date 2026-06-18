namespace LivingMetalGhost.Core.Models;

/// <summary>고급 Workbench 대화/작업 세션 원문 로그.</summary>
public sealed class AdvancedSessionLogEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string WorkspaceId { get; set; } = "default";
    public string SessionId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string UserText { get; set; } = string.Empty;
    public string AssistantText { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public string Action { get; set; } = "advanced-chat";
    public IReadOnlyList<string> UsedContext { get; set; } = Array.Empty<string>();
}
