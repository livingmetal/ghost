using LivingMetalGhost.Core.Conversation;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class CharacterSpeechSanitizerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_EmptyInput_ReturnsEmpty(string text)
    {
        Assert.Equal(string.Empty, CharacterSpeechSanitizer.Sanitize(text));
    }

    [Fact]
    public void Sanitize_RemovesStockPrefixAndClosingPhrase()
    {
        var sanitized = CharacterSpeechSanitizer.Sanitize(
            "좋은 질문입니다. 직접 확인해야 합니다. 도움이 되었으면 좋겠습니다.");

        Assert.Equal("직접 확인해야 합니다.", sanitized);
    }

    [Fact]
    public void Sanitize_CollapsesThreeOrMoreLineFeeds()
    {
        var sanitized = CharacterSpeechSanitizer.Sanitize(
            "first\n\n\n\nsecond");

        Assert.Equal(
            $"first{Environment.NewLine}{Environment.NewLine}second",
            sanitized);
    }

    [Fact]
    public void Sanitize_PreservesOrdinaryDialogue()
    {
        const string dialogue = "그 가정은 한 번 더 확인해야 해.";

        Assert.Equal(dialogue, CharacterSpeechSanitizer.Sanitize(dialogue));
    }
}
