using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplayInputFormatterTests
{
    [Fact]
    public void FormatForPrompt_TreatsPlainTextAsSpokenDialogue()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("  안녕하세요.  ");

        Assert.Contains("Player input interpreted by roleplay syntax:", formatted);
        Assert.Contains("Spoken dialogue:", formatted);
        Assert.Contains("- \"안녕하세요.\"", formatted);
    }

    [Fact]
    public void FormatForPrompt_SeparatesDialogueActionAndThought()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("안녕. **손을 흔든다** (조금 긴장된다)");

        Assert.Contains("Spoken dialogue:", formatted);
        Assert.Contains("- \"안녕.\"", formatted);
        Assert.Contains("Visible action / narration:", formatted);
        Assert.Contains("- 손을 흔든다", formatted);
        Assert.Contains("Inner thought", formatted);
        Assert.Contains("- 조금 긴장된다", formatted);
    }

    [Fact]
    public void FormatForPrompt_InstructsCharacterNotToKnowInnerThoughts()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("(이건 숨겨야 해)");

        Assert.DoesNotContain("Spoken dialogue:", formatted);
        Assert.DoesNotContain("Visible action / narration:", formatted);
        Assert.Contains("The player only thought privately", formatted);
        Assert.Contains("Do not answer, mention, or hint that you know the thought", formatted);
    }

    [Fact]
    public void FormatForPrompt_NormalizesWhitespaceAcrossSegments()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("안녕\r\n   반가워.   **  고개를   끄덕인다  **");

        Assert.Contains("- \"안녕 반가워.\"", formatted);
        Assert.Contains("- 고개를 끄덕인다", formatted);
    }

    [Fact]
    public void FormatForPrompt_ParsesDoubleAsteriskActionSyntax()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("**문을 연다**");

        Assert.DoesNotContain("Spoken dialogue:", formatted);
        Assert.Contains("Visible action / narration:", formatted);
        Assert.Contains("- 문을 연다", formatted);
    }

    [Fact]
    public void FormatForPrompt_KeepsSingleAsteriskTextAsDialogue()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("*A*B*");

        Assert.Contains("Spoken dialogue:", formatted);
        Assert.Contains("- \"*A*B*\"", formatted);
        Assert.DoesNotContain("Visible action / narration:", formatted);
    }

    [Fact]
    public void FormatForPrompt_AllowsSingleAsteriskInsideDoubleAsteriskAction()
    {
        var formatted = RoleplayInputFormatter.FormatForPrompt("**A*B**");

        Assert.DoesNotContain("Spoken dialogue:", formatted);
        Assert.Contains("Visible action / narration:", formatted);
        Assert.Contains("- A*B", formatted);
    }
}
