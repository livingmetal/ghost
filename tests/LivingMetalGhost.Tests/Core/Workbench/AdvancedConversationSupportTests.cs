using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workspace;
using Xunit;

namespace LivingMetalGhost.Tests.Core.Workbench;

public sealed class AdvancedConversationSupportTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceStore _workspaceStore;
    private readonly AdvancedSessionLogService _sessionLog;
    private readonly AdvancedConversationSupport _support;

    public AdvancedConversationSupportTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        _workspaceStore = new WorkspaceStore(paths);
        _sessionLog = new AdvancedSessionLogService(paths, _workspaceStore);
        _support = new AdvancedConversationSupport(
            _sessionLog,
            _workspaceStore,
            new WorkspaceContextBuilder(new WorkspaceReadService()));
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
    public void BuildRepositoryContext_WithoutExplicitRoot_UsesDetectedWorkspace()
    {
        var context = _support.BuildRepositoryContext("question");

        Assert.Contains("Project instructions", context);
        Assert.Contains("README.md", context);
    }

    [Fact]
    public void BuildRepositoryContext_WithConfiguredRoot_ReturnsSnapshot()
    {
        var repositoryRoot = Path.Combine(_root, "repo");
        Directory.CreateDirectory(repositoryRoot);
        File.WriteAllText(
            Path.Combine(repositoryRoot, "README.md"),
            "# Test Repository\n\nArchitecture notes.");
        _workspaceStore.Save(new WorkspaceSettings { RootPath = repositoryRoot });

        var context = _support.BuildRepositoryContext("architecture");

        Assert.Contains("README.md", context);
        Assert.Contains("Test Repository", context);
    }

    [Fact]
    public async Task RecordTurnAsync_WritesAdvancedSessionMetadata()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_sessionLog.PinnedContextFile)!);
        File.WriteAllText(_sessionLog.PinnedContextFile, "pinned");
        File.WriteAllText(_sessionLog.ProjectMemoryFile, "{}");

        await _support.RecordTurnAsync(
            new LlmOptions { Provider = "stub", Model = "model" },
            CreateCharacter(),
            "question",
            "answer",
            "idle",
            CancellationToken.None);

        var line = Assert.Single(File.ReadAllLines(_sessionLog.CurrentSessionFile));
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        Assert.Equal("stub", root.GetProperty("provider").GetString());
        Assert.Equal("question", root.GetProperty("user_text").GetString());
        Assert.Contains(
            root.GetProperty("used_context").EnumerateArray().Select(item => item.GetString()),
            value => value == "pinned_context");
        Assert.Contains(
            root.GetProperty("used_context").EnumerateArray().Select(item => item.GetString()),
            value => value == "project_memory");
    }

    private static CharacterProfile CreateCharacter()
    {
        return new CharacterProfile(
            "test",
            "Test Character",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            new CharacterPresentationProfile([], "normal", [], "full-body"),
            new SpriteCharacterVisualProfile(
                string.Empty,
                "idle.png",
                null,
                [],
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlyList<string>>(),
                100,
                100,
                null,
                null));
    }
}
