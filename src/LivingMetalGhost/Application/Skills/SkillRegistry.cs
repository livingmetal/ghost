using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public sealed class SkillRegistry
{
    private readonly IReadOnlyList<IGhostSkill> _skills;

    public SkillRegistry(
        AppCommandSkill appCommandSkill,
        SlashIntentSkill slashIntentSkill,
        ChatSkill chatSkill,
        TranslateSkill translateSkill,
        GitCommandSkill gitCommandSkill,
        CodingAgentSkill codingAgentSkill)
    {
        // 우선순위: 앱 명령 → 명시적 슬래시 기능 의도 → 번역 → git 안전 명령 → 코딩 에이전트 → 일반 대화(fallback).
        _skills = new IGhostSkill[] { appCommandSkill, slashIntentSkill, translateSkill, gitCommandSkill, codingAgentSkill, chatSkill };
    }

    public IGhostSkill Resolve(UserRequest request)
    {
        return _skills.FirstOrDefault(skill => skill.CanHandle(request)) ?? _skills.Last();
    }
}
