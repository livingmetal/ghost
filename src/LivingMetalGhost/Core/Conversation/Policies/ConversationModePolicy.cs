using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Conversation;

public enum ConversationHistoryChannel
{
    Companion,
    Roleplay
}

public static class ConversationModePolicy
{
    public static ConversationHistoryChannel GetHistoryChannel(ConversationMode mode)
    {
        return mode == ConversationMode.Story
            ? ConversationHistoryChannel.Roleplay
            : ConversationHistoryChannel.Companion;
    }

    public static bool UsesLlmMood(ConversationMode mode)
    {
        return mode != ConversationMode.Advanced;
    }
}
