using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public sealed class SkillRegistry
{
    private readonly IReadOnlyList<IGhostSkill> _skills;

    public SkillRegistry(AppCommandSkill appCommandSkill, ChatSkill chatSkill, TranslateSkill translateSkill)
    {
        _skills = new IGhostSkill[] { appCommandSkill, translateSkill, chatSkill };
    }

    public IGhostSkill Resolve(UserRequest request)
    {
        return _skills.FirstOrDefault(skill => skill.CanHandle(request)) ?? _skills.Last();
    }
}

