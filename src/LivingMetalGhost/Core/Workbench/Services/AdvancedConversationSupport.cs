using System.IO;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public sealed class AdvancedConversationSupport
{
    private readonly AdvancedSessionLogService _sessionLogService;
    private readonly WorkspaceStore _workspaceStore;
    private readonly Core.Workspace.WorkspaceContextBuilder _workspaceContextBuilder;

    public AdvancedConversationSupport(
        AdvancedSessionLogService sessionLogService,
        WorkspaceStore workspaceStore,
        Core.Workspace.WorkspaceContextBuilder workspaceContextBuilder)
    {
        _sessionLogService = sessionLogService;
        _workspaceStore = workspaceStore;
        _workspaceContextBuilder = workspaceContextBuilder;
    }

    public string BuildRepositoryContext(string userText)
    {
        try
        {
            var root = _workspaceStore.Load().RootPath;
            return string.IsNullOrWhiteSpace(root)
                ? string.Empty
                : _workspaceContextBuilder.Build(root, userText);
        }
        catch
        {
            return string.Empty;
        }
    }

    public Task RecordTurnAsync(
        LlmOptions options,
        CharacterProfile character,
        string userText,
        string assistantText,
        string mood,
        CancellationToken cancellationToken)
    {
        return _sessionLogService.AppendTurnAsync(new AdvancedSessionLogEntry
        {
            Provider = options.Provider,
            Model = options.Model,
            CharacterId = character.Id,
            CharacterName = character.DisplayName,
            UserText = userText,
            AssistantText = assistantText,
            Mood = mood,
            Action = "advanced-chat",
            UsedContext = GetUsedContextLabels()
        }, cancellationToken);
    }

    private IReadOnlyList<string> GetUsedContextLabels()
    {
        var labels = new List<string>();
        if (File.Exists(_sessionLogService.PinnedContextFile))
        {
            labels.Add("pinned_context");
        }

        if (File.Exists(_sessionLogService.ProjectMemoryFile))
        {
            labels.Add("project_memory");
        }

        return labels;
    }
}
