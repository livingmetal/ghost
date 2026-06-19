using LivingMetalGhost.Core.Conversation;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ConversationResponseParserTests
{
    [Fact]
    public void ParseMoodTag_ExtractsLeadingMoodAndText()
    {
        var parsed = ConversationResponseParser.ParseMoodTag(
            "  [mood: Soft-Smile]\r\nHello");

        Assert.Equal("soft-smile", parsed.Mood);
        Assert.Equal("Hello", parsed.Text);
    }

    [Fact]
    public void ParseMoodTag_LeavesTextWithoutLeadingMoodUnchanged()
    {
        var parsed = ConversationResponseParser.ParseMoodTag(
            "Hello [mood: thinking]");

        Assert.Null(parsed.Mood);
        Assert.Equal("Hello [mood: thinking]", parsed.Text);
    }

    [Fact]
    public void ParseMoodTag_HandlesEmptyResponse()
    {
        var parsed = ConversationResponseParser.ParseMoodTag("  ");

        Assert.Null(parsed.Mood);
        Assert.Equal(string.Empty, parsed.Text);
    }

    [Fact]
    public void StripLegacyRoleplayTags_RemovesTagsAndCollapsesBlankLines()
    {
        var cleaned = ConversationResponseParser.StripLegacyRoleplayTags(
            "[story: scene]\n\n\n\nVisible text");

        Assert.Equal("Visible text", cleaned);
    }
}
