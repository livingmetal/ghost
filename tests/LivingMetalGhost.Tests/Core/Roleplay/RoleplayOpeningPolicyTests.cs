using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayOpeningPolicyTests : IDisposable
{
    private readonly string _root;
    private readonly StoryStateStore _store;

    public RoleplayOpeningPolicyTests()
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

    [Fact]
    public void SetEnabled_ShowsOpeningOnlyOnFirstActivation()
    {
        var first = _store.SetEnabled(true);

        Assert.True(first.ShowOpeningOnActivation);
        Assert.True(first.OpeningShown);

        var disabled = _store.SetEnabled(false);
        Assert.False(disabled.ShowOpeningOnActivation);

        var second = _store.SetEnabled(true);
        Assert.False(second.ShowOpeningOnActivation);
        Assert.True(second.OpeningShown);
    }

    [Fact]
    public void Reset_AllowsOpeningAgainWhenKeptEnabled()
    {
        _store.SetEnabled(true);

        var reset = _store.Reset(keepEnabled: true);

        Assert.True(reset.ShowOpeningOnActivation);
        Assert.True(reset.OpeningShown);
    }
}
