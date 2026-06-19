using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class HiddenTraitSchedulerTests
{
    [Fact]
    public void CharacterWithoutHiddenTraits_ReturnsEmptyDirective()
    {
        var scheduler = new HiddenTraitScheduler(new ConversationHistoryStore());

        Assert.Equal(
            string.Empty,
            scheduler.BuildDirective(CreateCharacter([]), ConversationMode.Daily));
    }

    [Fact]
    public void DailyAndAdvanced_ShareHiddenTraitSchedule()
    {
        var history = new ConversationHistoryStore();
        var scheduler = new HiddenTraitScheduler(history);
        var character = CreateCharacter([CreateTrait()]);

        var first = scheduler.BuildDirective(character, ConversationMode.Daily);
        history.Add(ConversationMode.Daily, "assistant", "reply");
        var second = scheduler.BuildDirective(character, ConversationMode.Advanced);

        Assert.Contains("stay dormant", first);
        Assert.Contains("Hidden side guidance:", second);
        Assert.Contains("test hidden trait", second);
    }

    [Fact]
    public void Roleplay_HasIndependentHiddenTraitSchedule()
    {
        var history = new ConversationHistoryStore();
        var scheduler = new HiddenTraitScheduler(history);
        var character = CreateCharacter([CreateTrait()]);

        scheduler.BuildDirective(character, ConversationMode.Daily);
        history.Add(ConversationMode.Daily, "assistant", "reply");
        var companion = scheduler.BuildDirective(character, ConversationMode.Advanced);
        var roleplay = scheduler.BuildDirective(character, ConversationMode.Story);

        Assert.Contains("Hidden side guidance:", companion);
        Assert.Contains("stay dormant", roleplay);
    }

    private static HiddenCharacterTrait CreateTrait()
    {
        return new HiddenCharacterTrait(
            "test-trait",
            "test hidden trait",
            1,
            1,
            1,
            1);
    }

    private static CharacterProfile CreateCharacter(
        IReadOnlyList<HiddenCharacterTrait> hiddenTraits)
    {
        var presentation = new CharacterPresentationProfile(
            [],
            "normal",
            [],
            "full-body");
        var visual = new SpriteCharacterVisualProfile(
            string.Empty,
            "idle.png",
            null,
            ["speaking.png"],
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyList<string>>(),
            100,
            100,
            null,
            null);

        return new CharacterProfile(
            "test",
            "Test Character",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            hiddenTraits,
            presentation,
            visual);
    }
}
