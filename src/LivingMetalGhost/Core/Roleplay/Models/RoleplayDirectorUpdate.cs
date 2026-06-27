namespace LivingMetalGhost.Core.Models;

/// <summary>
/// Director API가 제안하는 한 턴의 구조화된 상태 변경이다. 값은 적용 시점에
/// 다시 검증하고 제한하므로 모델 출력이 곧바로 영속 상태를 덮어쓰지 않는다.
/// </summary>
public sealed class RoleplayDirectorUpdate
{
    public string Scene { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string StoryDate { get; set; } = string.Empty;
    public string StoryTime { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public int? Tension { get; set; }
    public int? Affection { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string CurrentAppearance { get; set; } = string.Empty;
    public string CurrentGoal { get; set; } = string.Empty;
    public Dictionary<string, int> CurrentEmotion { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> RelationshipMetrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PersonalityDrift { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
