using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

public interface ILlmProvider
{
    string Name { get; }
    bool SupportsImageInput => false;
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct);
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken ct);
}

public interface ILlmProviderFactory
{
    ILlmProvider Create(string providerName);
}

