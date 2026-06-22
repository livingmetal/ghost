namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 일상 대화와 분리된 롤플레잉 장면 상태.
/// 실제 업무/프로젝트 기억과 섞이지 않도록 story 전용 저장소에만 보관한다.
/// </summary>
public sealed class StoryState
{
    public bool Enabled { get; set; }
    public string Title { get; set; } = "default";
    public string Scene { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string OpeningLine { get; set; } = string.Empty;
    public string PlayerRole { get; set; } = "주인공";
    public string Mood { get; set; } = "daily";
    public int Tension { get; set; }

    /// <summary>이야기의 장기 연속성 기억. 전제·자기인식·관계·약속·미해결 떡밥을 보호한다.</summary>
    public List<StoryMemoryFact> Facts { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

/// <summary>롤플레잉 기억 한 조각. Kind 예: premise, self, player, relationship, promise, open_loop.</summary>
public sealed class StoryMemoryFact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "premise";
    public string Text { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public string Status { get; set; } = "active";
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastMentionedAt { get; set; } = DateTimeOffset.Now;
    public int MentionCount { get; set; } = 1;
}
