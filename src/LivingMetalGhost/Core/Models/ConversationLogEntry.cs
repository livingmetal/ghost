namespace LivingMetalGhost.Core.Models;

public sealed class ConversationLogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string UserText { get; set; } = string.Empty;
    public string AssistantText { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsProactive { get; set; }

    /// <summary>대화 당시 선택된 캐릭터 ID. 구버전 로그와 호환되도록 선택 메타데이터로 둔다.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>대화 당시 표시 캐릭터 이름.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>UI에 표시된 provider/model 라벨. 예: DAILY: Gemini / gemini-3.1-flash-lite.</summary>
    public string ProviderLabel { get; set; } = string.Empty;

    /// <summary>응답 후 캐릭터가 유지한 mood/state.</summary>
    public string Mood { get; set; } = string.Empty;
}
