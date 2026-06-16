namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 일상모드 안에서 AI소설/미연시처럼 대화를 이어갈 때 사용하는 최소 장면 상태.
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
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
