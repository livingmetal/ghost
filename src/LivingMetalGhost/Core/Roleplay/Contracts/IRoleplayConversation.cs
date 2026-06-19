using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Roleplay;

public interface IRoleplayConversation
{
    Task<SkillResult> SendAsync(string text, CancellationToken cancellationToken);
    Task<SkillResult> StartIdleAsync(CancellationToken cancellationToken);
}
