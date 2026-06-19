using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.AppCore.Roleplay;

public sealed record RoleplaySessionSnapshot(
    StoryState State,
    int MemoryEntries,
    string StoryRoot);

public sealed class RoleplaySessionController
{
    private readonly StoryStateStore _storyStateStore;
    private readonly IRoleplayConversation _conversation;

    public RoleplaySessionController(
        StoryStateStore storyStateStore,
        IRoleplayConversation conversation)
    {
        _storyStateStore = storyStateStore;
        _conversation = conversation;
    }

    public bool IsEnabled => _storyStateStore.Load().Enabled;

    public StoryState SetEnabled(bool enabled) =>
        _storyStateStore.SetEnabled(enabled);

    public StoryState Reset(bool keepEnabled) =>
        _storyStateStore.Reset(keepEnabled);

    public RoleplaySessionSnapshot GetSnapshot() =>
        new(
            _storyStateStore.Load(),
            _storyStateStore.CountMemoryEntries(),
            _storyStateStore.StoryRoot);

    public string BuildOpeningText(StoryState state) =>
        StoryStateStore.BuildOpeningText(state);

    public Task<SkillResult> SendAsync(
        string text,
        CancellationToken cancellationToken) =>
        _conversation.SendAsync(text, cancellationToken);

    public Task<SkillResult> StartIdleAsync(CancellationToken cancellationToken) =>
        _conversation.StartIdleAsync(cancellationToken);
}
