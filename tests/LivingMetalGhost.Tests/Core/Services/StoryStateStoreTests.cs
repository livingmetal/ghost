using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Services;

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
}
