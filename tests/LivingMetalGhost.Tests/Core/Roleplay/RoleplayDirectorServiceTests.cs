using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayDirectorServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "LivingMetalGhost.Tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task CreateUpdateAsync_UsesDirectorEndpointAndParsesPatch()
    {
        var provider = new StubRoleplayProvider(
            """result: {"location":"옥상","tension":3,"affection":7,"relationship_metrics":{"trust":4}}""");
        var paths = new AppPaths(_root);
        var service = new RoleplayDirectorService(
            new StoryPlanStore(paths),
            new StoryCharacterStore(paths),
            new StubRoleplayProviderFactory(provider));
        var config = new AppConfig();
        config.RoleplayLlm.Director.Provider = "stub";
        config.RoleplayLlm.Director.Model = "director-model";

        var update = await service.CreateUpdateAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState { Enabled = true },
            "문을 열었다.",
            "바람이 불어온다.",
            "thinking",
            CancellationToken.None);

        Assert.NotNull(update);
        Assert.Equal("옥상", update.Location);
        Assert.Equal(3, update.Tension);
        Assert.Equal(4, update.RelationshipMetrics["trust"]);
        Assert.Equal("director-model", provider.LastRequest?.ResolveModel());
        Assert.Empty(provider.LastRequest?.History ?? []);
    }

    [Fact]
    public async Task CreateUpdateAsync_WhenDisabled_DoesNotCallProvider()
    {
        var provider = new StubRoleplayProvider("{}");
        var paths = new AppPaths(_root);
        var service = new RoleplayDirectorService(
            new StoryPlanStore(paths),
            new StoryCharacterStore(paths),
            new StubRoleplayProviderFactory(provider));
        var config = new AppConfig();
        config.RoleplayLlm.EnableDirectorStateUpdate = false;

        var update = await service.CreateUpdateAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState(),
            "user",
            "character",
            "idle",
            CancellationToken.None);

        Assert.Null(update);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void StateUpdater_AppliesDirectorPatchWithConservativeClamps()
    {
        var paths = new AppPaths(_root);
        var configLoader = new AppConfigLoader(paths);
        var stateStore = new StoryStateStore(paths, configLoader);
        var state = stateStore.Load();
        state.Enabled = true;
        state.Tension = 1;
        state.Affection = 0;
        stateStore.Save(state);
        var updater = new RoleplayStateUpdater(
            stateStore,
            new StoryCharacterStore(paths),
            configLoader);

        updater.UpdateAfterTurn(
            "문을 열었다.",
            "바람이 불어온다.",
            "thinking",
            new RoleplayDirectorUpdate
            {
                Location = "옥상",
                Summary = "두 사람은 옥상 문을 열었다.",
                Tension = 5,
                Affection = 100,
                CurrentEmotion = new Dictionary<string, int> { ["fear"] = 100 },
                PersonalityDrift = new Dictionary<string, int> { ["openness"] = 10 }
            });

        var updated = stateStore.Load();
        Assert.Equal(1, updated.TurnNumber);
        Assert.Equal("옥상", updated.Location);
        Assert.Equal("두 사람은 옥상 문을 열었다.", updated.Summary);
        Assert.Equal(2, updated.Tension);
        Assert.Equal(10, updated.Affection);
        Assert.Single(stateStore.ReadRecentMemory(10));
    }
}
