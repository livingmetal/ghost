using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ConversationResponseProcessorTests
{
    private readonly ConversationResponseProcessor _processor =
        new(new CharacterMoodResolver());

    [Fact]
    public void Process_DailyParsesSanitizesAndResolvesMood()
    {
        var result = _processor.Process(
            " [mood: happy]\nHello ",
            ConversationMode.Daily,
            CreateVisual("happy"));

        Assert.Equal("Hello", result.Text);
        Assert.Equal("happy", result.Mood);
    }

    [Fact]
    public void Process_RoleplayRemovesLegacyStoryTags()
    {
        var result = _processor.Process(
            "[mood: happy]\n[story: scene]\n\nVisible text",
            ConversationMode.Story,
            CreateVisual("happy"));

        Assert.Equal("Visible text", result.Text);
        Assert.Equal("happy", result.Mood);
    }

    [Fact]
    public void Process_AdvancedIgnoresRequestedMood()
    {
        var result = _processor.Process(
            "[mood: happy]\nTechnical response",
            ConversationMode.Advanced,
            CreateVisual("happy"));

        Assert.Equal("Technical response", result.Text);
        Assert.Equal("idle", result.Mood);
    }

    private static SpriteCharacterVisualProfile CreateVisual(string mood)
    {
        return new SpriteCharacterVisualProfile(
            RootDirectory: "root",
            IdleSpritePath: "idle.png",
            BlinkSpritePath: null,
            SpeakingSpritePaths: [],
            MoodSpritePaths: new Dictionary<string, string>
            {
                [mood] = $"{mood}.png"
            },
            MoodBlinkSpritePaths: new Dictionary<string, string>(),
            MoodCycleSpritePaths:
                new Dictionary<string, IReadOnlyList<string>>(),
            Width: 1,
            Height: 1,
            IdleMotion: null,
            SpeakingMotion: null);
    }
}
