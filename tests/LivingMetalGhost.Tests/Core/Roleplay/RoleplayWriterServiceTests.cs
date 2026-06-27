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
        Assert.Equal(StoryPlanIdentity.CurrentSchemaVersion, first.SchemaVersion);
        Assert.Equal("test-character", first.CharacterId);
        Assert.NotEmpty(first.WriterSettingsFingerprint);
        Assert.Equal(first.Title, second?.Title);
        Assert.Equal(1, provider.CallCount);
        Assert.Contains("Preserve player agency", provider.LastRequest?.SystemPrompt);
    }

    [Fact]
    public async Task EnsurePlanAsync_RegeneratesWhenWriterSettingsChange()
    {
        const string firstResponse =
            """{"title":"첫 계획","premise":"첫 설정을 따른다.","acts":[]}""";
        const string secondResponse =
            """{"title":"새 계획","premise":"바뀐 설정을 따른다.","acts":[]}""";
        var provider = new StubRoleplayProvider(firstResponse, secondResponse);
        var paths = new AppPaths(_root);
        var store = new StoryPlanStore(paths);
        var service = new RoleplayWriterService(
            store,
            new StoryCharacterStore(paths),
            new StubRoleplayProviderFactory(provider));
        var config = new AppConfig();
        config.RoleplayLlm.Writer.Provider = "stub";
        var character = RoleplayApiTestSupport.CreateCharacter();

        var first = await service.EnsurePlanAsync(
            config,
            character,
            new StoryState(),
            CancellationToken.None);
        config.RoleplayLlm.WriterSettings.Genre = "우주 오페라";
        var second = await service.EnsurePlanAsync(
            config,
            character,
            new StoryState(),
            CancellationToken.None);

        Assert.Equal("첫 계획", first?.Title);
        Assert.Equal("새 계획", second?.Title);
        Assert.Equal(2, provider.CallCount);
        Assert.Equal("새 계획", store.Load().Title);
    }

    [Fact]
    public async Task EnsurePlanAsync_WhenDisabled_DoesNotCallProvider()
    {
        var provider = new StubRoleplayProvider(
            """{"title":"생성되면 안 됨","premise":"disabled","acts":[]}""");
        var paths = new AppPaths(_root);
        var service = new RoleplayWriterService(
            new StoryPlanStore(paths),
            new StoryCharacterStore(paths),
            new StubRoleplayProviderFactory(provider));
        var config = new AppConfig();
        config.RoleplayLlm.EnableWriter = false;

        var plan = await service.EnsurePlanAsync(
            config,
            RoleplayApiTestSupport.CreateCharacter(),
            new StoryState(),
            CancellationToken.None);

        Assert.Null(plan);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public void StoryPlanParser_RejectsEmptyOrMalformedOutput()
    {
        Assert.Null(StoryPlanParser.Parse("not json"));
        Assert.Null(StoryPlanParser.Parse("{\"title\":\"empty\"}"));
    }
}
