using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Presentation;

/// <summary>
/// LLM이 반환한 mood를 그대로 UI에 꽂지 않고, 현재 모드/상황과 합쳐
/// 실제 캐릭터 표시 상태를 결정하는 작은 연출 감독이다.
/// </summary>
public sealed class SpriteDirector
{
    public string ResolveThinkingMood(ConversationMode mode) =>
        CharacterExpressionPolicy.ResolvePendingState(mode);

    public string ResolveSpeakingMood(string? requestedMood, ConversationMode mode) =>
        CharacterExpressionPolicy.ResolveResponseState(requestedMood, mode);

    public string ResolveRestingMood(ConversationMode mode) =>
        CharacterExpressionPolicy.ResolveRestingState(mode);

    public int GetPostSpeechHoldMilliseconds(string mood, ConversationMode mode) =>
        CharacterExpressionPolicy.GetPostSpeechHoldMilliseconds(mood, mode);

    public string ToStateLabel(string mood, ConversationMode mode)
    {
        var prefix = mode switch
        {
            ConversationMode.Advanced => "ADV",
            ConversationMode.Story => "RP",
            _ => "DAILY"
        };

        return $"{prefix}:{mood.ToUpperInvariant()}";
    }
}
