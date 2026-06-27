using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class StoryStateStoreTests
{
    [Fact]
    public void BuildOpeningText_UsesDoubleAsteriskActionSyntaxInGuide()
    {
        var openingText = StoryStateStore.BuildOpeningText(new StoryState
        {
            Scene = "테스트 장면",
            Summary = "테스트 목표"
        });

        Assert.Contains("**이렇게 쓰면 행동이나 상황 설명입니다.**", openingText);
        Assert.Contains("*이렇게 쓰면 일반 이탤릭 강조입니다.*", openingText);
        Assert.Contains("(이렇게 쓰면 속마음입니다.)", openingText);
        Assert.DoesNotContain("\n*이렇게 쓰면 행동이나 상황 설명입니다.*", openingText);
    }

    [Fact]
    public void Constructor_MigratesLegacyStoryStateIntoCanonicalStoryDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var legacyRoot = Path.Combine(root, "Stories", "default");
            Directory.CreateDirectory(legacyRoot);
            File.WriteAllText(
                Path.Combine(legacyRoot, "story_state.json"),
                """{"enabled":true,"title":"legacy","location":"old"}""");
            var paths = new LivingMetalGhost.Core.Config.AppPaths(root);

            var store = new StoryStateStore(paths, new LivingMetalGhost.Core.Config.AppConfigLoader(paths));

            Assert.Equal(Path.Combine(root, "story"), store.StoryRoot);
            Assert.Equal("legacy", store.Load().Title);
            Assert.True(File.Exists(Path.Combine(root, "story", "story_state.json")));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
