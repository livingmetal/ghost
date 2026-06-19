using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

public static class LlmCapabilityPolicy
{
    private static readonly string[] KnownImageModelPrefixes =
    [
        "gemini-",
        "gpt-4o",
        "gpt-4.1",
        "gpt-5"
    ];

    public static bool SupportsOpenAiCompatibleImageInput(LlmOptions options)
    {
        var provider = options.Provider.Trim();
        if (provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("openai", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("chatgpt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) &&
            (baseUri.Host.EndsWith(
                 "generativelanguage.googleapis.com",
                 StringComparison.OrdinalIgnoreCase) ||
             baseUri.Host.EndsWith(
                 "api.openai.com",
                 StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var model = options.Model.Trim();
        return KnownImageModelPrefixes.Any(prefix =>
            model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
