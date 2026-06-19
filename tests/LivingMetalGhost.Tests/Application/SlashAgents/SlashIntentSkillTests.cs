using LivingMetalGhost.Skills;
using Xunit;

namespace LivingMetalGhost.Tests.Application.SlashAgents;

public sealed class SlashIntentSkillTests
{
    [Theory]
    [InlineData("/날짜", true)]
    [InlineData("   /문지캠퍼스 점심", true)]
    [InlineData("// 주석", false)]
    [InlineData("https://example.com", false)]
    [InlineData("일반 대화", false)]
    public void IsSlashIntent_OnlyAcceptsSingleLeadingSlash(
        string text,
        bool expected)
    {
        Assert.Equal(expected, SlashIntentSkill.IsSlashIntent(text));
    }

    [Fact]
    public void ExtractIntentText_RemovesOnlyTheRoutingSlash()
    {
        Assert.Equal(
            "대전 날씨",
            SlashIntentSkill.ExtractIntentText("  /대전 날씨 "));
    }
}
