namespace LivingMetalGhost.Providers.Llm;

public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly MockLlmProvider _mock;
    private readonly OpenAiCompatibleProvider _openAiCompatible;
    private readonly CodexCliProvider _codex;

    public LlmProviderFactory(
        MockLlmProvider mock,
        OpenAiCompatibleProvider openAiCompatible,
        CodexCliProvider codex)
    {
        _mock = mock;
        _openAiCompatible = openAiCompatible;
        _codex = codex;
    }

    public ILlmProvider Create(string providerName) =>
        providerName.ToLowerInvariant() switch
        {
            "openai-compatible" => _openAiCompatible,
            "gemini" => _openAiCompatible,
            "codex" => _codex,
            "mock" => _mock,
            _ => _mock
        };
}
