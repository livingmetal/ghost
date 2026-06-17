namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 캐릭터별 기본 시작 스토리 템플릿. Assets/Characters/&lt;name&gt;/story-default.json 에 보관하고
/// 롤플레잉 모드를 처음 켤 때 StoryState의 기본값으로 시드한다.
/// </summary>
public sealed record StoryTemplate(
    string CharacterId,
    string Title,
    string PlayerRole,
    string Scene,
    string Summary,
    string OpeningLine,
    string Mood,
    int Tension,
    IReadOnlyList<StoryMemoryFact> Facts);
