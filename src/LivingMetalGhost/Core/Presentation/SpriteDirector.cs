using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Presentation;

/// <summary>
/// LLM이 반환한 mood를 그대로 UI에 꽂지 않고, 현재 모드/상황과 합쳐
/// 실제 캐릭터 표시 상태를 결정하는 작은 연출 감독이다.
/// </summary>
public sealed class SpriteDirector
{
    public string ResolveThinkingMood(ConversationMode mode) => mode switch
    {
        ConversationMode.Advanced => "serious",
        ConversationMode.Story => "thinking",
        _ => "thinking"
    };

    public string ResolveSpeakingMood(string? requestedMood, ConversationMode mode)
    {
        if (string.IsNullOrWhiteSpace(requestedMood))
        {
            return mode == ConversationMode.Advanced ? "serious" : "speaking";
        }

        var normalized = requestedMood.Trim().ToLowerInvariant();
        return normalized switch
        {
            "working" when mode == ConversationMode.Advanced => "serious",
            "speaking" when mode == ConversationMode.Advanced => "serious",
            _ => normalized
        };
    }

    public string ResolveRestingMood(ConversationMode mode) => mode switch
    {
        ConversationMode.Advanced => "serious",
        ConversationMode.Story => "listening",
        _ => "listening"
    };

    public int GetPostSpeechHoldMilliseconds(string mood, ConversationMode mode)
    {
        if (mode == ConversationMode.Advanced)
        {
            return 6000;
        }

        return mood switch
        {
            "strict" or "serious" or "skeptical" => 6000,
            "concerned" or "apologetic" => 5500,
            "happy" or "soft-smile" or "amused" => 4500,
            _ => 4000
        };
    }

    public string ToStateLabel(string mood, ConversationMode mode)
    {
        var prefix = mode switch
        {
            ConversationMode.Advanced => "ADV",
            ConversationMode.Story => "STORY",
            _ => "DAILY"
        };

        return $"{prefix}:{mood.ToUpperInvariant()}";
    }
}
