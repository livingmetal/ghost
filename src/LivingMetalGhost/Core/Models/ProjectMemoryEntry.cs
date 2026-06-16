namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 고급 Workbench에서 사용자가 승인해 재사용하기로 한 프로젝트 기억.
/// 원문 transcript와 달리 다음 고급모드 프롬프트에 들어갈 수 있다.
/// </summary>
public sealed class ProjectMemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string WorkspaceId { get; set; } = "default";
    public string Type { get; set; } = "decision";
    public string Content { get; set; } = string.Empty;
    public string SourceSessionId { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}
