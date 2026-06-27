using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workbench;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Conversation;

public sealed class ConversationRequestFactoryTests : IDisposable
{
    private readonly string _root;
    private readonly ConversationHistoryStore _history;
    private readonly ConversationRequestFactory _factory;

    public ConversationRequestFactoryTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        var workspaceStore = new WorkspaceStore(paths);
        var sessionLog = new AdvancedSessionLogService(paths, workspaceStore);
        var storyCharacterStore = new StoryCharacterStore(paths);
        _history = new ConversationHistoryStore();
        _factory = new ConversationRequestFactory(
            new PromptAssembler(
                new AdvancedPromptPolicy(sessionLog),
                new CharacterMoodResolver(),
                storyCharacterStore,
                new StoryPlanStore(paths)),
            _history,
            new HiddenTraitScheduler(_history));
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
    public void Create_UsesRequestedModeHistoryAndOptions()
    {
        _history.Add(ConversationMode.Daily, "user", "companion history");
        _history.Add(ConversationMode.Story, "user", "roleplay history");
        var options = new LlmOptions
        {
            Provider = "test",
            Model = "test-model"
        };

        var request = _factory.Create(
            new AppConfig(),
            CreateCharacter(),
            ConversationMode.Story,
            new StoryState(),
            "formatted input",
            options);

        Assert.Equal("formatted input", request.UserText);
        Assert.Equal("test-model", request.Model);
        Assert.Same(options, request.Options);
        var history = Assert.Single(request.History);
        Assert.Equal("roleplay history", history.Content);
        Assert.DoesNotContain("companion history", request.SystemPrompt);
    }

    [Fact]
    public void Create_InjectsRepositoryContextOnlyThroughAdvancedPolicy()
    {
        var request = _factory.Create(
            new AppConfig(),
            CreateCharacter(),
            ConversationMode.Advanced,
            new StoryState(),
            "inspect",
            new LlmOptions { Provider = "test", Model = "model" },
            "repository snapshot");

        Assert.Contains("repository snapshot", request.SystemPrompt);
        Assert.Contains("Do not output a [mood: ...] tag.", request.SystemPrompt);
        Assert.DoesNotContain(
            "Line 1: exactly one mood tag",
            request.SystemPrompt);
    }

    [Fact]
    public void Create_ForwardsImageOnlyForCurrentRequest()
    {
        var image = new LlmImageAttachment(
            "sample.png",
            "image/png",
            "AA==",
            "sample.png");

        var request = _factory.Create(
            new AppConfig(),
            CreateCharacter(),
            ConversationMode.Daily,
            new StoryState(),
            "describe",
            new LlmOptions { Provider = "gemini", Model = "model" },
            image: image);

        Assert.Same(image, request.Image);
        Assert.Empty(request.History);
    }

    private static CharacterProfile CreateCharacter()
    {
        return new CharacterProfile(
            Id: "test",
            DisplayName: "Test",
            Description: string.Empty,
            DefaultAppearance: "appearance",
            DefaultBackground: "background",
            DefaultPersonality: "personality",
            HiddenTraits: [],
            Presentation: new CharacterPresentationProfile(
                [],
                "normal",
                [],
                "full-body"),
            Visual: new SpriteCharacterVisualProfile(
                RootDirectory: "root",
                IdleSpritePath: "idle.png",
                BlinkSpritePath: null,
                SpeakingSpritePaths: ["speaking.png"],
                MoodSpritePaths: new Dictionary<string, string>(),
                MoodBlinkSpritePaths: new Dictionary<string, string>(),
                MoodCycleSpritePaths:
                    new Dictionary<string, IReadOnlyList<string>>(),
                Width: 1,
                Height: 1,
                IdleMotion: null,
                SpeakingMotion: null));
    }
}
