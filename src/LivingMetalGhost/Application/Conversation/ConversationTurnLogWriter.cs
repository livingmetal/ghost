using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.AppCore.Conversation;

public sealed record ConversationTurnLogContext(
    string UserText,
    string AssistantText,
    bool IsProactive,
    string Mood,
    ConversationMode Mode,
    string CharacterId,
    string CharacterName);

public sealed class ConversationTurnLogWriter
{
    private readonly AppConfigLoader _configLoader;
    private readonly ConversationLogService _conversationLogService;

    public ConversationTurnLogWriter(
        AppConfigLoader configLoader,
        ConversationLogService conversationLogService)
    {
        _configLoader = configLoader;
        _conversationLogService = conversationLogService;
    }

    public string GetProviderLabel(ConversationMode mode)
    {
        var llm = GetLlmSettings(mode);
        var modeLabel = mode switch
        {
            ConversationMode.Advanced => "ADVANCED",
            ConversationMode.Story => "STORY",
            _ => "DAILY"
        };
        var model = string.IsNullOrWhiteSpace(llm.Model) ? "(default)" : llm.Model;
        return $"{modeLabel}: {llm.Provider} / {model}";
    }

    public Task WriteAsync(
        ConversationTurnLogContext context,
        CancellationToken cancellationToken)
    {
        var llm = GetLlmSettings(context.Mode);
        return _conversationLogService.AppendAsync(new ConversationLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            UserText = context.UserText,
            AssistantText = context.AssistantText,
            Provider = llm.Provider,
            Model = llm.Model,
            IsProactive = context.IsProactive,
            CharacterId = context.CharacterId,
            CharacterName = context.CharacterName,
            ProviderLabel = GetProviderLabel(context.Mode),
            Mood = context.Mood,
            Mode = context.Mode.ToString()
        }, cancellationToken);
    }

    private LlmSettings GetLlmSettings(ConversationMode mode)
    {
        var config = _configLoader.Load();
        return mode == ConversationMode.Advanced
            ? config.AdvancedLlm
            : config.Llm;
    }
}
