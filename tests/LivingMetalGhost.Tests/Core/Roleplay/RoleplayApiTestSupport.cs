using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Providers.Llm;

namespace LivingMetalGhost.Tests.Core.Roleplay;

internal static class RoleplayApiTestSupport
{
    public static CharacterProfile CreateCharacter() => new(
        Id: "test-character",
        DisplayName: "테스트 캐릭터",
        Description: string.Empty,
        DefaultAppearance: "검은 머리와 푸른 눈",
        DefaultBackground: "조용한 관측소의 안내자",
        DefaultPersonality: "차분하고 호기심이 많다.",
        HiddenTraits: [],
        Presentation: new CharacterPresentationProfile([], "normal", [], "full-body"),
        Visual: new SpriteCharacterVisualProfile(
            RootDirectory: "root",
            IdleSpritePath: "idle.png",
            BlinkSpritePath: null,
            SpeakingSpritePaths: ["speaking.png"],
            MoodSpritePaths: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["thinking"] = "thinking.png"
            },
            MoodBlinkSpritePaths: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            MoodCycleSpritePaths: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            Width: 1,
            Height: 1,
            IdleMotion: null,
            SpeakingMotion: null));
}

internal sealed class StubRoleplayProviderFactory : ILlmProviderFactory
{
    public StubRoleplayProviderFactory(StubRoleplayProvider provider)
    {
        Provider = provider;
    }

    public StubRoleplayProvider Provider { get; }
    public ILlmProvider Create(string providerName) => Provider;
}

internal sealed class StubRoleplayProvider : ILlmProvider
{
    private readonly Queue<string> _responses;

    public StubRoleplayProvider(params string[] responses)
    {
        _responses = new Queue<string>(responses);
    }

    public string Name => "stub";
    public int CallCount { get; private set; }
    public LlmRequest? LastRequest { get; private set; }

    public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        CallCount++;
        LastRequest = request;
        var text = _responses.Count > 0 ? _responses.Dequeue() : string.Empty;
        return Task.FromResult(new LlmResponse { Text = text });
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}
