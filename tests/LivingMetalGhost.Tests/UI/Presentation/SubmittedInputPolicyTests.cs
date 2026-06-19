using LivingMetalGhost.UI.Presentation;
using Xunit;

namespace LivingMetalGhost.Tests.UI.Presentation;

public sealed class SubmittedInputPolicyTests
{
    [Fact]
    public void ShouldClear_WhenInputWasNotEdited()
    {
        Assert.True(
            SubmittedInputPolicy.ShouldClear(
                "submitted message",
                "submitted message"));
    }

    [Fact]
    public void ShouldClear_PreservesNewTextTypedDuringResponse()
    {
        Assert.False(
            SubmittedInputPolicy.ShouldClear(
                "next message",
                "submitted message"));
    }

    [Fact]
    public void ShouldClear_UsesExactInputIncludingWhitespace()
    {
        Assert.False(
            SubmittedInputPolicy.ShouldClear(
                "submitted message",
                " submitted message "));
    }
}
