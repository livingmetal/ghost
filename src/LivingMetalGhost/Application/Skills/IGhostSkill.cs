using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

public interface IGhostSkill
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<string> Examples { get; }
    bool CanHandle(UserRequest request);
    Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct);
}

