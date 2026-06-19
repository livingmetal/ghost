using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workbench;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ConversationModePolicyTests : IDisposable
{
    private readonly string _root;

    public ConversationModePolicyTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void DailyAndAdvanced_ShareCompanionHistory()
    {
        Assert.Equal(
            ConversationHistoryChannel.Companion,
            ConversationModePolicy.GetHistoryChannel(ConversationMode.Daily));
        Assert.Equal(
            ConversationHistoryChannel.Companion,
            ConversationModePolicy.GetHistoryChannel(ConversationMode.Advanced));
    }

    [Fact]
    public void Roleplay_UsesSeparateHistory()
    {
        Assert.Equal(
            ConversationHistoryChannel.Roleplay,
            ConversationModePolicy.GetHistoryChannel(ConversationMode.Story));
        Assert.NotEqual(
            ConversationModePolicy.GetHistoryChannel(ConversationMode.Daily),
            ConversationModePolicy.GetHistoryChannel(ConversationMode.Story));
    }

    [Theory]
    [InlineData(ConversationMode.Daily, true)]
    [InlineData(ConversationMode.Story, true)]
    [InlineData(ConversationMode.Advanced, false)]
    public void LlmMood_IsEnabledOnlyForExpressiveModes(
        ConversationMode mode,
        bool expected)
    {
        Assert.Equal(expected, ConversationModePolicy.UsesLlmMood(mode));
    }

    [Fact]
    public void AdvancedPrompt_DoesNotRequestMoodTags()
    {
        var paths = new AppPaths(_root);
        var workspaceStore = new WorkspaceStore(paths);
        var sessionLog = new AdvancedSessionLogService(paths, workspaceStore);
        var assembler = new PromptAssembler(
            new AdvancedPromptPolicy(sessionLog),
            new CharacterMoodResolver());

        var prompt = assembler.BuildSystemPrompt(
            new AppConfig(),
            CreateCharacter(),
            ConversationMode.Advanced,
            new StoryState(),
            string.Empty);

        Assert.Contains("Do not select or output a mood.", prompt);
        Assert.Contains("Do not output a mood tag", prompt);
        Assert.DoesNotContain("Line 1: exactly one mood tag", prompt);
        Assert.DoesNotContain("Speech examples:", prompt);
    }

    [Fact]
    public void DailyPrompt_StillRequestsMoodTags()
    {
        var paths = new AppPaths(_root);
        var workspaceStore = new WorkspaceStore(paths);
        var sessionLog = new AdvancedSessionLogService(paths, workspaceStore);
        var assembler = new PromptAssembler(
            new AdvancedPromptPolicy(sessionLog),
            new CharacterMoodResolver());

        var prompt = assembler.BuildSystemPrompt(
            new AppConfig(),
            CreateCharacter(),
            ConversationMode.Daily,
            new StoryState(),
            string.Empty);

        Assert.Contains("Line 1: exactly one mood tag", prompt);
        Assert.Contains("[mood: speaking]", prompt);
    }

    private static CharacterProfile CreateCharacter()
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
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["thinking"] = "thinking.png"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
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
            [],
            presentation,
            visual);
    }
}
