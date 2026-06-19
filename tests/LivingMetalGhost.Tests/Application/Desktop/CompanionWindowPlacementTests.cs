using LivingMetalGhost.AppCore.Desktop;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Desktop;

public sealed class CompanionWindowPlacementTests
{
    private static readonly DesktopBounds WorkArea =
        new(0, 0, 1920, 1080);

    [Fact]
    public void PositionChat_PrefersLeftSideWithSpriteOverlap()
    {
        var position = CompanionWindowPlacement.PositionChat(
            WorkArea,
            new DesktopBounds(1400, 600, 400, 400),
            new DesktopSize(486, 120));

        Assert.Equal(1066, position.Left);
        Assert.Equal(632, position.Top);
    }

    [Fact]
    public void PositionChat_FlipsRightNearLeftEdge()
    {
        var position = CompanionWindowPlacement.PositionChat(
            WorkArea,
            new DesktopBounds(20, 500, 400, 400),
            new DesktopSize(486, 120));

        Assert.Equal(268, position.Left);
        Assert.Equal(532, position.Top);
    }

    [Fact]
    public void Center_UsesWorkAreaOffset()
    {
        var position = CompanionWindowPlacement.Center(
            new DesktopBounds(1920, 0, 1920, 1080),
            new DesktopSize(980, 680));

        Assert.Equal(2390, position.Left);
        Assert.Equal(200, position.Top);
    }

    [Fact]
    public void Clamp_KeepsOversizedWindowAtWorkAreaOrigin()
    {
        var position = CompanionWindowPlacement.Clamp(
            WorkArea,
            new DesktopPosition(500, 500),
            new DesktopSize(2200, 1200));

        Assert.Equal(0, position.Left);
        Assert.Equal(0, position.Top);
    }

    [Fact]
    public void BottomRight_AppliesMargin()
    {
        var position = CompanionWindowPlacement.BottomRight(
            WorkArea,
            new DesktopSize(400, 500),
            margin: 24);

        Assert.Equal(1496, position.Left);
        Assert.Equal(556, position.Top);
    }
}
