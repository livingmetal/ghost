using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Presentation;

public static class CharacterExpressionPolicy
{
    public const string NeutralState = "idle";

    public static string ResolvePendingState(ConversationMode mode)
    {
        return NeutralState;
    }

    public static string ResolveResponseState(string? requestedMood, ConversationMode mode)
    {
        if (!ConversationModePolicy.UsesLlmMood(mode) ||
            string.IsNullOrWhiteSpace(requestedMood))
        {
            return NeutralState;
        }

        return requestedMood.Trim().ToLowerInvariant() switch
        {
            "idle" or "speaking" or "working" => NeutralState,
            "serious" => "strict",
            var mood => mood
        };
    }

    public static string ResolveRestingState(ConversationMode mode)
    {
        return NeutralState;
    }

    public static int GetPostSpeechHoldMilliseconds(string mood, ConversationMode mode)
    {
        if (!ConversationModePolicy.UsesLlmMood(mode) ||
            string.Equals(mood, NeutralState, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return mood switch
        {
            "strict" or "skeptical" => 6000,
            "concerned" or "apologetic" => 5500,
            "happy" or "soft-smile" or "amused" => 4500,
            _ => 4000
        };
    }
}
