using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

public sealed class MockLlmProvider : ILlmProvider
{
    public string Name => "Mock";

    public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        return Task.FromResult(new LlmResponse
        {
            Text = $"{request.UserTitle}, Mock 모드로 받은 메시지예요: {request.UserText}",
            FromFallback = true
        });
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return new LlmStreamChunk { Text = "[Mock] ", IsCompleted = false };
        await Task.Delay(40, ct);
        yield return new LlmStreamChunk { Text = $"{request.UserTitle}, {request.UserText}", IsCompleted = true };
    }
}
