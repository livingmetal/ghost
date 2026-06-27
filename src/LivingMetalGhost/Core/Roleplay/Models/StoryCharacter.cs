namespace LivingMetalGhost.Core.Models;

public sealed class RoleplayManifest
{
    public int ManifestVersion { get; set; } = 1;
    public string ActiveCharacterId { get; set; } = string.Empty;
    public Dictionary<string, StoryCharacterDefinition> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> GlobalBoundaries { get; set; } =
    [
        "롤플레잉 모드에서는 일반 모드의 외형, 배경, 성격, 말투를 직접 참조하지 않는다.",
        "캐릭터 정의는 roleplay_manifest.json의 값을 기준으로 한다.",
        "사용자가 지정하지 않은 학교, 병원, 교실, 양호실, 보건실 배경을 새로 만들지 않는다."
    ];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

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
