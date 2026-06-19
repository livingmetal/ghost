using LivingMetalGhost.UI.Presentation;
using Xunit;

namespace LivingMetalGhost.Tests.UI.Presentation;

public sealed class AssistantMessagePresenterTests
{
    [Fact]
    public void CreateChunks_PreservesShortResponseAsSingleMessage()
    {
        var chunks = AssistantMessagePresenter.CreateChunks("  short response  ", compact: true);

        Assert.Equal(["short response"], chunks);
    }

    [Fact]
    public void CreateChunks_UsesCompactMaximumLength()
    {
        var response = string.Join(' ', Enumerable.Repeat("123456789", 30));

        var chunks = AssistantMessagePresenter.CreateChunks(response, compact: true);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 140));
        Assert.Equal(response, string.Join(' ', chunks));
    }

    [Fact]
    public void CreateChunks_AllowsLongerAdvancedMessages()
    {
        var response = new string('a', 180);

        var compactChunks = AssistantMessagePresenter.CreateChunks(response, compact: true);
        var advancedChunks = AssistantMessagePresenter.CreateChunks(response, compact: false);

        Assert.Equal(2, compactChunks.Count);
        Assert.Single(advancedChunks);
    }
}
