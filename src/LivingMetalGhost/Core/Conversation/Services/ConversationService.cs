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
    private readonly PromptAssembler _promptAssembler;
    private readonly StoryStateStore _storyStateStore;
    private readonly RoleplayStateUpdater _roleplayStateUpdater;
    private readonly RoleplayMemoryDigestService _roleplayMemoryDigestService;
    private readonly ConversationHistoryStore _historyStore;
    private readonly HiddenTraitScheduler _hiddenTraitScheduler;
    private readonly AdvancedConversationSupport _advancedConversationSupport;
    private readonly CharacterMoodResolver _characterMoodResolver;
    private readonly ExternalConversationTurnRecorder _externalTurnRecorder;
    public ConversationService(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory,
        PromptAssembler promptAssembler,
        StoryStateStore storyStateStore,
        RoleplayStateUpdater roleplayStateUpdater,
        RoleplayMemoryDigestService roleplayMemoryDigestService,
        ConversationHistoryStore historyStore,
        HiddenTraitScheduler hiddenTraitScheduler,
        AdvancedConversationSupport advancedConversationSupport,
        CharacterMoodResolver characterMoodResolver,
        ExternalConversationTurnRecorder externalTurnRecorder)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
        _promptAssembler = promptAssembler;
        _storyStateStore = storyStateStore;
        _roleplayStateUpdater = roleplayStateUpdater;
        _roleplayMemoryDigestService = roleplayMemoryDigestService;
        _historyStore = historyStore;
        _hiddenTraitScheduler = hiddenTraitScheduler;
        _advancedConversationSupport = advancedConversationSupport;
        _characterMoodResolver = characterMoodResolver;
        _externalTurnRecorder = externalTurnRecorder;
    }

    public Task<SkillResult> ChatAsync(string text, bool advanced, CancellationToken cancellationToken)
    {
        var mode = advanced ? ConversationMode.Advanced : ConversationMode.Daily;
        return ChatAsync(text, mode, cancellationToken);
    }

    public Task<SkillResult> RoleplayAsync(string text, CancellationToken cancellationToken)
    {
        return ChatAsync(text, ConversationMode.Story, cancellationToken);
    }

    Task<SkillResult> IRoleplayConversation.SendAsync(
        string text,
        CancellationToken cancellationToken) =>
        RoleplayAsync(text, cancellationToken);

    Task<SkillResult> IRoleplayConversation.StartIdleAsync(
        CancellationToken cancellationToken) =>
        StartStoryIdleAsync(cancellationToken);

    public void RememberExternalTurn(
        ConversationMode mode,
        string userText,
        string assistantText,
        string source)
    {
        _externalTurnRecorder.Record(
            mode,
            userText,
            assistantText,
            source);
    }

    private async Task<SkillResult> ChatAsync(string text, ConversationMode mode, CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var userTextForProvider = mode == ConversationMode.Story
            ? RoleplayInputFormatter.FormatForPrompt(text)
            : text;
        var llm = mode == ConversationMode.Advanced ? config.AdvancedLlm : config.Llm;
        var options = LlmOptions.FromSettings(llm);
        var repositoryContext = mode == ConversationMode.Advanced
            ? _advancedConversationSupport.BuildRepositoryContext(text)
            : string.Empty;
        var provider = _providerFactory.Create(options.Provider);
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userTextForProvider,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                _hiddenTraitScheduler.BuildDirective(character, mode),
                repositoryContext),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var storyCleanText = mode == ConversationMode.Story
            ? ConversationResponseParser.StripLegacyRoleplayTags(parsed.Text)
            : parsed.Text;
        var characterText = CharacterSpeechSanitizer.Sanitize(storyCleanText);
        var characterMood = _characterMoodResolver.Resolve(
            mode,
            parsed.Mood,
            character.Visual);

        _historyStore.Add(mode, "user", userTextForProvider);
        _historyStore.Add(mode, "assistant", characterText);
        if (mode == ConversationMode.Story)
        {
            _roleplayStateUpdater.UpdateAfterTurn(text, characterText, characterMood);
            await _roleplayMemoryDigestService.DigestIfDueAsync(options, cancellationToken);
        }
        else if (mode == ConversationMode.Advanced)
        {
            await _advancedConversationSupport.RecordTurnAsync(
                options,
                character,
                text,
                characterText,
                characterMood,
                cancellationToken);
        }

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = mode == ConversationMode.Story ? "roleplay-chat" : "chat",
            UsedLlm = true
        };
    }

    public async Task<SkillResult> StartConversationAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        var mode = ConversationMode.Daily;
        // 먼저 말 걸기는 가벼운 기본 대화이므로 항상 기본 llm 설정을 사용한다.
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText = "지금 상황에 어울리는 짧은 말 한마디로 먼저 대화를 시작해. " +
                       "질문, 가벼운 안부, 작업 집중 확인, 휴식 제안 중 하나를 자연스럽게 선택해. " +
                       "설명이나 따옴표 없이 실제로 사용자에게 말할 문장만 출력해.";
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userText,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                _hiddenTraitScheduler.BuildDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var characterText = CharacterSpeechSanitizer.Sanitize(parsed.Text);
        var characterMood = _characterMoodResolver.Resolve(
            mode,
            parsed.Mood,
            character.Visual);

        _historyStore.Add(mode, "assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = "proactive-chat",
            UsedLlm = true
        };
    }

    /// <summary>스토리 모드 idle 비트: 사용자 입력 없이 짧은 존재감만 보여준다. 플롯은 진행하지 않는다.</summary>
    public async Task<SkillResult> StartStoryIdleAsync(CancellationToken cancellationToken)
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var storyState = _storyStateStore.Load();
        const ConversationMode mode = ConversationMode.Story;
        var options = LlmOptions.FromSettings(config.Llm);
        var provider = _providerFactory.Create(options.Provider);
        var userText =
            "지금은 사용자의 입력이 없는 조용한 순간이야. 짧은 '존재감' 비트를 보여줘. " +
            "**행동/지문**을 한두 개와 짧은 혼잣말 한마디 정도로만 표현해. " +
            "큰 사건이나 플롯을 진행하지 말고, 사용자의 행동·말·감정을 대신 정하지 마. " +
            "메뉴, 선택지, 시스템 언급은 금지. 장면을 크게 바꾸지 말고 분위기만 가볍게 살려.";
        var response = await provider.GenerateAsync(new LlmRequest
        {
            UserText = userText,
            UserTitle = config.App.UserTitle,
            Model = options.Model,
            Options = options,
            SystemPrompt = _promptAssembler.BuildSystemPrompt(
                config,
                character,
                mode,
                storyState,
                _hiddenTraitScheduler.BuildDirective(character, mode)),
            History = _historyStore.GetSnapshot(mode)
        }, cancellationToken);
        var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
        var characterText = CharacterSpeechSanitizer.Sanitize(
            ConversationResponseParser.StripLegacyRoleplayTags(parsed.Text));
        var characterMood = _characterMoodResolver.Resolve(
            mode,
            parsed.Mood,
            character.Visual);

        // idle 비트는 연속성을 위해 히스토리에만 남기고 StoryState(장면/요약)는 바꾸지 않는다.
        _historyStore.Add(mode, "assistant", characterText);

        return new SkillResult
        {
            BubbleText = characterText,
            Mood = characterMood,
            Action = "story-idle",
            UsedLlm = true
        };
    }

}
