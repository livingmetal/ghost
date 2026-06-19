using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Characters;

public sealed class CharacterMoodResolverTests
{
    private readonly CharacterMoodResolver _resolver = new();

    [Fact]
    public void Resolve_DailyUsesSupportedExplicitMood()
    {
        var visual = CreateSpriteVisual(
            moodSpritePaths: new Dictionary<string, string>
            {
                ["happy"] = "happy.png"
            });

        var mood = _resolver.Resolve(
            ConversationMode.Daily,
            " HAPPY ",
            visual);

        Assert.Equal("happy", mood);
    }

    [Fact]
    public void Resolve_UnsupportedMoodFallsBackToIdle()
    {
        var mood = _resolver.Resolve(
            ConversationMode.Story,
            "angry",
            CreateSpriteVisual());

        Assert.Equal("idle", mood);
    }

    [Fact]
    public void Resolve_AdvancedIgnoresSupportedLlmMood()
    {
        var visual = CreateSpriteVisual(
            moodSpritePaths: new Dictionary<string, string>
            {
                ["happy"] = "happy.png"
            });

        var mood = _resolver.Resolve(
            ConversationMode.Advanced,
            "happy",
            visual);

        Assert.Equal("idle", mood);
    }

    [Fact]
    public void GetAvailableMoods_ExcludesModularBlinkState()
    {
        var visual = new ModularCharacterVisualProfile(
            RootDirectory: "root",
            LayerOrder: [],
            DefaultLayerPaths: new Dictionary<string, string?>(),
            States: new Dictionary<string, ModularCharacterState>
            {
                ["idle"] = new(new Dictionary<string, string?>()),
                ["blink"] = new(new Dictionary<string, string?>()),
                ["happy"] = new(new Dictionary<string, string?>())
            },
            SpeakingStates: [],
            SpeakingStatesByState:
                new Dictionary<string, IReadOnlyList<ModularCharacterState>>(),
            IdleStateName: "idle",
            BlinkStateName: "blink",
            Width: 1,
            Height: 1,
            IdleMotion: null,
            SpeakingMotion: null);

        var moods = _resolver.GetAvailableMoods(visual);

        Assert.Contains("idle", moods);
        Assert.Contains("happy", moods);
        Assert.DoesNotContain("blink", moods);
    }

    private static SpriteCharacterVisualProfile CreateSpriteVisual(
        IReadOnlyDictionary<string, string>? moodSpritePaths = null)
    {
        return new SpriteCharacterVisualProfile(
            RootDirectory: "root",
            IdleSpritePath: "idle.png",
            BlinkSpritePath: null,
            SpeakingSpritePaths: [],
            MoodSpritePaths: moodSpritePaths ?? new Dictionary<string, string>(),
            MoodBlinkSpritePaths: new Dictionary<string, string>(),
            MoodCycleSpritePaths:
                new Dictionary<string, IReadOnlyList<string>>(),
            Width: 1,
            Height: 1,
            IdleMotion: null,
            SpeakingMotion: null);
    }
}
