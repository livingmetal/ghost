using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public sealed class IntentRouter
{
    private readonly SkillRegistry _skillRegistry;

    public IntentRouter(SkillRegistry skillRegistry)
    {
        _skillRegistry = skillRegistry;
    }

    public IGhostSkill Route(UserRequest request) => _skillRegistry.Resolve(request);
}

