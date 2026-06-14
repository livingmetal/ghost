using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Skills;

public sealed class ChatSkill : IGhostSkill
{
    private readonly ConversationService _conversationService;

    public ChatSkill(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public string Name => "Chat";
    public string Description => "Default chat skill.";
    public IReadOnlyList<string> Examples => ["안녕", "이 코드 설명해줘"];

    public bool CanHandle(UserRequest request) => true;

    public Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
        => _conversationService.ChatAsync(request.RawText, request.UseAdvancedModel, ct);
}

