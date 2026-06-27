using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayWriterServiceTests : IDisposable
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
    public async Task EnsurePlanAsync_GeneratesPersistsAndReusesPlan()
    {
        var response = """
            ```json
            {
              "title": "별빛 관측소",
              "premise": "닫힌 관측소에서 오래된 신호의 정체를 함께 추적한다.",
              "genre": "미스터리",
              "acts": [{"act": 1, "goal": "신호를 확인한다.", "beats": ["전원이 켜진다."]}],
              "beat_seeds": [{"when": "침묵이 길어질 때", "beat": "수신기가 다시 울린다.", "purpose": "긴장 유지"}]
            }
            ```
            """;
        var provider = new StubRoleplayProvider(response);
        var paths = new AppPaths(_root);
        var store = new StoryPlanStore(paths);
        var service = new RoleplayWriterService(
            store,
            new StoryCharacterStore(paths),
            new StubRoleplayProviderFactory(provider));
        var config = new AppConfig();
        config.RoleplayLlm.Writer.Provider = "stub";

        var first = await service.EnsurePlanAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState { Scene = "관측소" },
            CancellationToken.None);
        var second = await service.EnsurePlanAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState(),
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("별빛 관측소", first.Title);
        Assert.True(store.Load().HasContent());
        Assert.Equal(first.Title, second?.Title);
        Assert.Equal(1, provider.CallCount);
        Assert.Contains("Preserve player agency", provider.LastRequest?.SystemPrompt);
    }

    [Fact]
    public void StoryPlanParser_RejectsEmptyOrMalformedOutput()
    {
        Assert.Null(StoryPlanParser.Parse("not json"));
        Assert.Null(StoryPlanParser.Parse("{\"title\":\"empty\"}"));
    }
}
