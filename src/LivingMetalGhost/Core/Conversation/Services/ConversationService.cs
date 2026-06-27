using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Core.Services;

public sealed class ConversationService : IRoleplayConversation
{
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly ConversationRequestFactory _requestFactory;
    private readonly StoryStateStore _storyStateStore;
    private readonly RoleplayWriterService _roleplayWriterService;
    private readonly RoleplayCharacterService _roleplayCharacterService;
    private readonly RoleplayDirectorService _roleplayDirectorService;
    private readonly RoleplayStateUpdater _roleplayStateUpdater;
    private readonly RoleplayMemoryDigestService _roleplayMemoryDigestService;
    private readonly ConversationHistoryStore _historyStore;
    private readonly AdvancedConversationSupport _advancedConversationSupport;
    private readonly ConversationResponseProcessor _responseProcessor;
    private readonly ExternalConversationTurnRecorder _externalTurnRecorder;

    public ConversationService(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory,
        ConversationRequestFactory requestFactory,
        StoryStateStore storyStateStore,
        RoleplayWriterService roleplayWriterService,
        RoleplayCharacterService roleplayCharacterService,
        RoleplayDirectorService roleplayDirectorService,
        RoleplayStateUpdater roleplayStateUpdater,
        RoleplayMemoryDigestService roleplayMemoryDigestService,
        ConversationHistoryStore historyStore,
        AdvancedConversationSupport advancedConversationSupport,
        ConversationResponseProcessor responseProcessor,
        ExternalConversationTurnRecorder externalTurnRecorder)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
        _requestFactory = requestFactory;
        _storyStateStore = storyStateStore;
        _roleplayWriterService = roleplayWriterService;
        _roleplayCharacterService = roleplayCharacterService;
        _roleplayDirectorService = roleplayDirectorService;
        _roleplayStateUpdater = roleplayStateUpdater;
        _roleplayMemoryDigestService = roleplayMemoryDigestService;
        _historyStore = historyStore;
        _advancedConversationSupport = advancedConversationSupport;
        _responseProcessor = responseProcessor;
        _externalTurnRecorder = externalTurnRecorder;
    }

    public Task<SkillResult> ChatAsync(string text, bool advanced, LlmImageAttachment? image, CancellationToken cancellationToken)
    {
        var mode = advanced ? ConversationMode.Advanced : ConversationMode.Daily;
        return ChatAsync(text, mode, image, cancellationToken);
    }

    public Task<SkillResult> RoleplayAsync(string text, LlmImageAttachment? image, CancellationToken cancellationToken)
    {
        return ChatAsync(text, ConversationMode.Story, image, cancellationToken);
    }

    Task<SkillResult> IRoleplayConversation.SendAsync(string text, LlmImageAttachment? image, CancellationToken cancellationToken) =>
        RoleplayAsync(text, image, cancellationToken);

    Task<SkillResult> IRoleplayConversation.StartIdleAsync(CancellationToken cancellationToken) =>
        StartStoryIdleAsync(cancellationToken);

    public void RememberExternalTurn(ConversationMode mode, string userText, string assistantText, string source)
    {
        _externalTurnRecorder.Record(mode, userText, assistantText, source);
    }

    private async Task<SkillResult> ChatAsync(string text, ConversationMode mode, LlmImageAttachment? image, CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var normalizedText = ImageInputService.BuildPromptText(text, image);
        var userDisplayText = ImageInputService.BuildDisplayText(text, image);
        var userTextForProvider = mode == ConversationMode.Story
            ? RoleplayInputFormatter.FormatForPrompt(normalizedText)
            : normalizedText;
        var llm = ResolveLlmSettings(config, mode);
        var options = LlmOptions.FromSettings(llm);
        var repositoryContext = mode == ConversationMode.Advanced
            ? _advancedConversationSupport.BuildRepositoryContext(normalizedText)
            : string.Empty;
        ProcessedConversationResponse processed;
        if (mode == ConversationMode.Story)
        {
            await _roleplayWriterService.EnsurePlanAsync(config, character, storyState, cancellationToken);
            processed = await _roleplayCharacterService.GenerateAsync(
                config,
                character,
                storyState,
                userTextForProvider,
                image,
                cancellationToken);
        }
        else
        {
            var provider = _providerFactory.Create(options.Provider);
            if (image is not null && !provider.SupportsImageInput(options))
            {
                throw new InvalidOperationException($"{provider.Name} does not support image input.");
            }

            var response = await provider.GenerateAsync(
                _requestFactory.Create(config, character, mode, storyState, userTextForProvider, options, repositoryContext, image),
                cancellationToken);
            processed = _responseProcessor.Process(response.Text, mode, character.Visual);
        }

        _historyStore.Add(mode, "user", ImageInputService.BuildDisplayText(userTextForProvider, image));
        _historyStore.Add(mode, "assistant", processed.Text);
        RoleplayTurnCompletion? roleplayCompletion = null;
        if (mode == ConversationMode.Story)
        {
            roleplayCompletion = new RoleplayTurnCompletion(FinalizeRoleplayTurnAsync(
                config,
                character,
                storyState,
                userDisplayText,
                processed,
                cancellationToken));
        }
        else if (mode == ConversationMode.Advanced)
        {
            await _advancedConversationSupport.RecordTurnAsync(
                options,
                character,
                userDisplayText,
                processed.Text,
                processed.Mood,
                cancellationToken);
        }

        return new SkillResult
        {
            BubbleText = processed.Text,
            Mood = processed.Mood,
            Action = mode == ConversationMode.Story ? "roleplay-chat" : "chat",
            UsedLlm = true,
            RawData = roleplayCompletion
        };
    }

    private async Task FinalizeRoleplayTurnAsync(
        AppConfig config,
        CharacterProfile character,
        StoryState storyState,
        string userDisplayText,
        ProcessedConversationResponse processed,
        CancellationToken cancellationToken)
    {
        var directorUpdate = await _roleplayDirectorService.CreateUpdateAsync(
            config,
            character,
            storyState,
            userDisplayText,
            processed.Text,
            processed.Mood,
            cancellationToken);
        _roleplayStateUpdater.UpdateAfterTurn(
            userDisplayText,
            processed.Text,
            processed.Mood,
            directorUpdate);
        var memoryOptions = LlmOptions.FromSettings(config.RoleplayLlm.Memory);
        await _roleplayMemoryDigestService.DigestIfDueAsync(memoryOptions, cancellationToken);
    }

    public async Task<SkillResult> StartConversationAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        const ConversationMode mode = ConversationMode.Daily;
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText = "Start a short proactive conversation in the current character voice. Output only the line to say.";
        var response = await provider.GenerateAsync(
            _requestFactory.Create(config, character, mode, storyState, userText, options),
            cancellationToken);
        var processed = _responseProcessor.Process(response.Text, mode, character.Visual);

        _historyStore.Add(mode, "assistant", processed.Text);

        return new SkillResult
        {
            BubbleText = processed.Text,
            Mood = processed.Mood,
            Action = "proactive-chat",
            UsedLlm = true
        };
    }

    public async Task<SkillResult> StartStoryIdleAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        await _roleplayWriterService.EnsurePlanAsync(config, character, storyState, cancellationToken);
        var userText = "Show a very short idle story beat. Do not advance the plot or decide the user's action.";
        var processed = await _roleplayCharacterService.GenerateAsync(
            config,
            character,
            storyState,
            userText,
            image: null,
            cancellationToken: cancellationToken);

        _historyStore.Add(ConversationMode.Story, "assistant", processed.Text);

        return new SkillResult
        {
            BubbleText = processed.Text,
            Mood = processed.Mood,
            Action = "story-idle",
            UsedLlm = true
        };
    }

    private static LlmSettings ResolveLlmSettings(AppConfig config, ConversationMode mode)
    {
        return mode switch
        {
            ConversationMode.Advanced => config.AdvancedLlm,
            ConversationMode.Story => config.RoleplayLlm.Character,
            _ => config.Llm
        };
    }
}
