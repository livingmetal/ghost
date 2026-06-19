using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Providers.Llm;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayMemoryDigestServiceTests : IDisposable
{
    private readonly string _root;
    private readonly StoryStateStore _store;

    public RoleplayMemoryDigestServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        _store = new StoryStateStore(paths, new AppConfigLoader(paths));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(12, true)]
    public void IsDigestDue_UsesSixTurnIntervals(int turnCount, bool expected)
    {
        Assert.Equal(expected, RoleplayMemoryDigestService.IsDigestDue(turnCount));
    }

    [Fact]
    public async Task DigestIfDueAsync_DoesNotCallProviderBeforeInterval()
    {
        AppendMemory(5);
        var provider = new StubProvider("[]");
        var service = new RoleplayMemoryDigestService(
            _store,
            new StubProviderFactory(provider));

        await service.DigestIfDueAsync(CreateOptions(), CancellationToken.None);

        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task DigestIfDueAsync_ReplacesFactsWithParsedDigest()
    {
        var state = _store.Load();
        state.Facts =
        [
            new StoryMemoryFact { Kind = "premise", Text = "old fact", Weight = 1 }
        ];
        _store.Save(state);
        AppendMemory(6);

        var provider = new StubProvider(
            """[{"kind":"relationship","text":"신뢰가 깊어졌다.","weight":4}]""");
        var service = new RoleplayMemoryDigestService(
            _store,
            new StubProviderFactory(provider));

        await service.DigestIfDueAsync(CreateOptions(), CancellationToken.None);

        var fact = Assert.Single(_store.Load().Facts);
        Assert.Equal("relationship", fact.Kind);
        Assert.Equal("신뢰가 깊어졌다.", fact.Text);
        Assert.Equal(4, fact.Weight);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task DigestIfDueAsync_InvalidResponsePreservesExistingFacts()
    {
        var state = _store.Load();
        state.Facts =
        [
            new StoryMemoryFact { Kind = "premise", Text = "keep this", Weight = 3 }
        ];
        _store.Save(state);
        AppendMemory(6);

        var service = new RoleplayMemoryDigestService(
            _store,
            new StubProviderFactory(new StubProvider("not json")));

        await service.DigestIfDueAsync(CreateOptions(), CancellationToken.None);

        var fact = Assert.Single(_store.Load().Facts);
        Assert.Equal("keep this", fact.Text);
    }

    private void AppendMemory(int count)
    {
        for (var index = 0; index < count; index++)
        {
            _store.AppendMemory(new RoleplayMemoryEntry
            {
                UserText = $"user-{index}",
                AssistantText = $"assistant-{index}"
            });
        }
    }

    private static LlmOptions CreateOptions()
    {
        return new LlmOptions
        {
            Provider = "stub",
            Model = "stub-model"
        };
    }

    private sealed class StubProviderFactory : ILlmProviderFactory
    {
        private readonly ILlmProvider _provider;

        public StubProviderFactory(ILlmProvider provider)
        {
            _provider = provider;
        }

        public ILlmProvider Create(string providerName) => _provider;
    }

    private sealed class StubProvider : ILlmProvider
    {
        private readonly string _response;

        public StubProvider(string response)
        {
            _response = response;
        }

        public string Name => "stub";
        public int CallCount { get; private set; }

        public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(new LlmResponse { Text = _response });
        }

        public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
