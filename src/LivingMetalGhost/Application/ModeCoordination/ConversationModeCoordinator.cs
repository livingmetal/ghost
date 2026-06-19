using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.AppCore.ModeCoordination;

public static class ConversationModeCoordinator
{
    public static ConversationMode GetCompanionMode(bool advancedEnabled)
    {
        return advancedEnabled
            ? ConversationMode.Advanced
            : ConversationMode.Daily;
    }

    public static bool ResolveAdvancedEnabled(bool requested, bool available)
    {
        return requested && available;
    }

    public static bool IsRoleplayActive(bool storyEnabled, bool advancedEnabled)
    {
        return storyEnabled && !advancedEnabled;
    }

    public static bool IsCompanionOverlaySuppressed(bool storyEnabled, bool advancedEnabled)
    {
        return storyEnabled || advancedEnabled;
    }
}
