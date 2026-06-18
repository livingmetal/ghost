namespace LivingMetalGhost.Providers.Llm;

public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly MockLlmProvider _mock;
    private readonly OpenAiCompatibleProvider _openAiCompatible;
    private readonly CodexCliProvider _codex;
    private readonly LmBotProvider _lmBot;
    private readonly InstalledAppsProvider _installedApps;

    public LlmProviderFactory(
        MockLlmProvider mock,
        OpenAiCompatibleProvider openAiCompatible,
        CodexCliProvider codex,
        LmBotProvider lmBot,
        InstalledAppsProvider installedApps)
    {
        _mock = mock;
        _openAiCompatible = openAiCompatible;
        _codex = codex;
        _lmBot = lmBot;
        _installedApps = installedApps;
    }

    public ILlmProvider Create(string providerName) =>
        providerName.ToLowerInvariant() switch
        {
            "openai-compatible" or "openai" or "chatgpt" or "gemini" => _openAiCompatible,
            "codex" => _codex,
            "lmbot" => _lmBot,
            "installed-apps" or "installed_apps" or "apps" => _installedApps,
            "mock" => _mock,
            _ => _mock
        };
}
