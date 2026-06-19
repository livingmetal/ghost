using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.Core.Models;
using Xunit;

namespace LivingMetalGhost.Tests.Application.ModeCoordination;

public sealed class ConversationModeCoordinatorTests
{
    [Theory]
    [InlineData(false, ConversationMode.Daily)]
    [InlineData(true, ConversationMode.Advanced)]
    public void GetCompanionMode_DependsOnlyOnAdvancedState(
        bool advancedEnabled,
        ConversationMode expected)
    {
        Assert.Equal(
            expected,
            ConversationModeCoordinator.GetCompanionMode(advancedEnabled));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ResolveAdvancedEnabled_RequiresRequestAndAvailability(
        bool requested,
        bool available,
        bool expected)
    {
        Assert.Equal(
            expected,
            ConversationModeCoordinator.ResolveAdvancedEnabled(requested, available));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void IsRoleplayActive_PreservesStoryStateButSuspendsItDuringAdvanced(
        bool storyEnabled,
        bool advancedEnabled,
        bool expected)
    {
        Assert.Equal(
            expected,
            ConversationModeCoordinator.IsRoleplayActive(storyEnabled, advancedEnabled));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void IsCompanionOverlaySuppressed_ForEitherDedicatedMode(
        bool storyEnabled,
        bool advancedEnabled,
        bool expected)
    {
        Assert.Equal(
            expected,
            ConversationModeCoordinator.IsCompanionOverlaySuppressed(
                storyEnabled,
                advancedEnabled));
    }
}
