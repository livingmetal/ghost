using LivingMetalGhost.AppCore.Roleplay;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Application.Roleplay;

public sealed class RoleplaySessionControllerTests : IDisposable
{
    private readonly string _root;
    private readonly StoryStateStore _store;
    private readonly StubRoleplayConversation _conversation;
    private readonly RoleplaySessionController _controller;

    public RoleplaySessionControllerTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "LivingMetalGhost.Tests",
            Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(_root);
        _store = new StoryStateStore(paths, new AppConfigLoader(paths));
        _conversation = new StubRoleplayConversation();
        _controller = new RoleplaySessionController(_store, _conversation);
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
    public void SetEnabled_UpdatesPersistedSessionState()
    {
        var state = _controller.SetEnabled(true);

        Assert.True(state.Enabled);
        Assert.True(_controller.IsEnabled);
        Assert.True(_store.Load().Enabled);
    }

    [Fact]
    public void GetSnapshot_ContainsStateMemoryCountAndRoot()
    {
        _controller.SetEnabled(true);
        _store.AppendMemory(new RoleplayMemoryEntry
        {
            UserText = "hello",
            AssistantText = "welcome"
        });

        var snapshot = _controller.GetSnapshot();

        Assert.True(snapshot.State.Enabled);
        Assert.Equal(1, snapshot.MemoryEntries);
        Assert.Equal(_store.StoryRoot, snapshot.StoryRoot);
    }

    [Fact]
    public void Reset_PreservesRequestedEnabledStateAndClearsMemory()
    {
        _controller.SetEnabled(true);
        _store.AppendMemory(new RoleplayMemoryEntry
        {
            UserText = "hello",
            AssistantText = "welcome"
        });

        var state = _controller.Reset(keepEnabled: true);

        Assert.True(state.Enabled);
        Assert.Equal(0, _controller.GetSnapshot().MemoryEntries);
    }

    [Fact]
    public async Task SendAndIdle_AreDelegatedToRoleplayConversation()
    {
        var sendResult = await _controller.SendAsync("hello", CancellationToken.None);
        var idleResult = await _controller.StartIdleAsync(CancellationToken.None);

        Assert.Equal("send:hello", sendResult.BubbleText);
        Assert.Equal("idle", idleResult.BubbleText);
        Assert.Equal("hello", _conversation.LastText);
        Assert.Equal(1, _conversation.SendCount);
        Assert.Equal(1, _conversation.IdleCount);
    }

    private sealed class StubRoleplayConversation : IRoleplayConversation
    {
        public string LastText { get; private set; } = string.Empty;
        public int SendCount { get; private set; }
        public int IdleCount { get; private set; }

        public Task<SkillResult> SendAsync(
            string text,
            CancellationToken cancellationToken)
        {
            LastText = text;
            SendCount++;
            return Task.FromResult(new SkillResult { BubbleText = $"send:{text}" });
        }

        public Task<SkillResult> StartIdleAsync(CancellationToken cancellationToken)
        {
            IdleCount++;
            return Task.FromResult(new SkillResult { BubbleText = "idle" });
        }
    }
}
