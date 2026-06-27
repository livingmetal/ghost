using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

/// <summary>Character API 호출과 캐릭터 응답 정규화를 한 경계로 묶는다.</summary>
public sealed class RoleplayCharacterService
{
    private readonly ILlmProviderFactory _providerFactory;
    private readonly ConversationRequestFactory _requestFactory;
    private readonly ConversationResponseProcessor _responseProcessor;

    public RoleplayCharacterService(
        ILlmProviderFactory providerFactory,
        ConversationRequestFactory requestFactory,
        ConversationResponseProcessor responseProcessor)
    {
        _providerFactory = providerFactory;
        _requestFactory = requestFactory;
        _responseProcessor = responseProcessor;
    }

    public async Task<ProcessedConversationResponse> GenerateAsync(
        AppConfig config,
        CharacterProfile character,
        StoryState state,
        string userText,
        LlmImageAttachment? image,
        CancellationToken cancellationToken)
    {
        var options = LlmOptions.FromSettings(config.RoleplayLlm.Character);
        var provider = _providerFactory.Create(options.Provider);
        if (image is not null && !provider.SupportsImageInput(options))
        {
            throw new InvalidOperationException($"{provider.Name} does not support image input.");
        }

        var response = await provider.GenerateAsync(
            _requestFactory.Create(
                config,
                character,
                ConversationMode.Story,
                state,
                userText,
                options,
                image: image),
            cancellationToken);

        return _responseProcessor.Process(response.Text, ConversationMode.Story, character.Visual);
    }
}
