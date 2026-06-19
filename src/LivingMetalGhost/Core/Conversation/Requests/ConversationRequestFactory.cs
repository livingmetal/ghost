using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Core.Conversation;

public sealed class ConversationRequestFactory
{
    private readonly PromptAssembler _promptAssembler;
    private readonly ConversationHistoryStore _historyStore;
    private readonly HiddenTraitScheduler _hiddenTraitScheduler;

    public ConversationRequestFactory(
        PromptAssembler promptAssembler,
        ConversationHistoryStore historyStore,
        HiddenTraitScheduler hiddenTraitScheduler)
    {
        _promptAssembler = promptAssembler;
        _historyStore = historyStore;
        _hiddenTraitScheduler = hiddenTraitScheduler;
    }

    public LlmRequest Create(
        AppConfig config,
        CharacterProfile character,
        ConversationMode mode,
        StoryState storyState,
        string userText,
        LlmOptions options,
        string repositoryContext = "",
        LlmImageAttachment? image = null)
    {
        return new LlmRequest
        {
            UserText = userText,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            Image = image,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                _hiddenTraitScheduler.BuildDirective(character, mode),
                repositoryContext),
            History = _historyStore.GetSnapshot(mode)
        };
    }
}
