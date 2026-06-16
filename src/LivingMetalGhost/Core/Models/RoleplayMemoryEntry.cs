namespace LivingMetalGhost.Core.Models;

/// <summary>롤플레잉 모드의 턴 단위 기억. 실제 업무 기억과 분리된 memory.jsonl에 저장된다.</summary>
public sealed class RoleplayMemoryEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string UserText { get; set; } = string.Empty;
    public string AssistantText { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public int Tension { get; set; }
    public int Affinity { get; set; }
    public string Scene { get; set; } = string.Empty;
}
