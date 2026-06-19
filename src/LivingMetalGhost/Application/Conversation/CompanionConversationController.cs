using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Skills;

namespace LivingMetalGhost.AppCore.Conversation;

public sealed record CompanionConversationTurn(
    UserRequest Request,
    SkillResult Result);

public sealed class CompanionConversationController
{
    private readonly IntentRouter _intentRouter;
    private readonly ConversationService _conversationService;

    public CompanionConversationController(
        IntentRouter intentRouter,
        ConversationService conversationService)
    {
        _intentRouter = intentRouter;
        _conversationService = conversationService;
    }

    public async Task<CompanionConversationTurn> SendAsync(
        string text,
        bool useAdvancedModel,
        LlmImageAttachment? image,
        CancellationToken cancellationToken)
    {
        var request = new UserRequest
        {
            RawText = text,
            UseAdvancedModel = useAdvancedModel,
            Image = image
        };
        var skill = _intentRouter.Route(request);
        var result = await skill.HandleAsync(request, cancellationToken);
        return new CompanionConversationTurn(request, result);
    }

    public Task<SkillResult> StartAsync(CancellationToken cancellationToken) =>
        _conversationService.StartConversationAsync(cancellationToken);
}
