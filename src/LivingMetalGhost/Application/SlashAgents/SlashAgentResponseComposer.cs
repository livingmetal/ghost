using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.AppCore.SlashAgents;

public sealed class SlashAgentResponseComposer
{
    private readonly AppConfigLoader _configLoader;
    private readonly ILlmProviderFactory _providerFactory;

    public SlashAgentResponseComposer(
        AppConfigLoader configLoader,
        ILlmProviderFactory providerFactory)
    {
        _configLoader = configLoader;
        _providerFactory = providerFactory;
    }

    public async Task<(string Text, bool UsedLlm)> ComposeAsync(
        string userIntent,
        SlashCapabilityResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Success)
        {
            return (result.Facts, false);
        }

        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        var options = LlmOptions.FromSettings(config.Llm);
        try
        {
            var provider = _providerFactory.Create(options.Provider);
            var response = await provider.GenerateAsync(new LlmRequest
            {
                UserText = $"""
                    User slash request:
                    {userIntent}

                    Verified current facts:
                    {result.Facts}
                    """,
                UserTitle = config.App.UserTitle,
                Model = options.Model,
                Options = options,
                SystemPrompt = $"""
                    You are {character.DisplayName}, a desktop character.
                    Explain the verified facts naturally in concise Korean.
                    Preserve all dates, times, locations, measurements, menu items, and source names exactly.
                    Do not claim that your training data supplied these facts.
                    The facts came from an approved live or local capability.
                    Do not add unsupported details and do not output a mood tag.
                    """
            }, cancellationToken);

            if (!response.FromFallback)
            {
                var parsed = ConversationResponseParser.ParseMoodTag(response.Text);
                var text = CharacterSpeechSanitizer.Sanitize(parsed.Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return (text, true);
                }
            }
        }
        catch
        {
            // Verified facts are a safe fallback when narration fails.
        }

        return (result.Facts, false);
    }
}
