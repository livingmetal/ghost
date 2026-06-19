using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Characters;

public sealed class CharacterExpressionPolicyTests
{
    [Theory]
    [InlineData(ConversationMode.Daily)]
    [InlineData(ConversationMode.Story)]
    [InlineData(ConversationMode.Advanced)]
    public void PendingAndRestingStates_AreNeutral(ConversationMode mode)
    {
        Assert.Equal("idle", CharacterExpressionPolicy.ResolvePendingState(mode));
        Assert.Equal("idle", CharacterExpressionPolicy.ResolveRestingState(mode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("idle")]
    [InlineData("speaking")]
    [InlineData("working")]
    public void NeutralOrMissingMood_UsesIdleState(string? mood)
    {
        Assert.Equal(
            "idle",
            CharacterExpressionPolicy.ResolveResponseState(mood, ConversationMode.Daily));
    }

    [Theory]
    [InlineData("thinking", "thinking")]
    [InlineData("soft-smile", "soft-smile")]
    [InlineData("serious", "strict")]
    public void ExpressiveModes_UseExplicitMoodOnly(string mood, string expected)
    {
        Assert.Equal(
            expected,
            CharacterExpressionPolicy.ResolveResponseState(mood, ConversationMode.Daily));
        Assert.Equal(
            expected,
            CharacterExpressionPolicy.ResolveResponseState(mood, ConversationMode.Story));
    }

    [Theory]
    [InlineData("thinking")]
    [InlineData("strict")]
    [InlineData("soft-smile")]
    public void AdvancedMode_IgnoresRequestedMood(string mood)
    {
        Assert.Equal(
            "idle",
            CharacterExpressionPolicy.ResolveResponseState(mood, ConversationMode.Advanced));
    }

    [Fact]
    public void NeutralState_HasNoPostSpeechHold()
    {
        Assert.Equal(
            0,
            CharacterExpressionPolicy.GetPostSpeechHoldMilliseconds(
                "idle",
                ConversationMode.Daily));
        Assert.Equal(
            0,
            CharacterExpressionPolicy.GetPostSpeechHoldMilliseconds(
                "strict",
                ConversationMode.Advanced));
    }

    [Fact]
    public void ExplicitMood_IsHeldBeforeReturningToIdle()
    {
        Assert.Equal(
            4500,
            CharacterExpressionPolicy.GetPostSpeechHoldMilliseconds(
                "soft-smile",
                ConversationMode.Story));
    }
}
