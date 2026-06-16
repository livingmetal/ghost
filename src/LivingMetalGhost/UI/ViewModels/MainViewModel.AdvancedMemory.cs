using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    private int _advancedContextRevision;

    public int AdvancedContextRevision
    {
        get => _advancedContextRevision;
        private set => SetProperty(ref _advancedContextRevision, value);
    }

    public string GetProjectMemorySummary()
    {
        var memoryStore = global::LivingMetalGhost.App.Services.GetRequiredService<ProjectMemoryStore>();
        return memoryStore.BuildDisplayText();
    }

    public void StartNewAdvancedSession()
    {
        var sessionLog = global::LivingMetalGhost.App.Services.GetRequiredService<AdvancedSessionLogService>();
        sessionLog.StartNewSession();
        AdvancedContextRevision++;
        BubbleText = "새 고급 작업 세션을 시작했어요.";
    }

    public string? GetLastCompletedAssistantMessageText()
    {
        return Messages
            .Reverse()
            .FirstOrDefault(message => !message.IsUser && !message.IsTyping && !string.IsNullOrWhiteSpace(message.Text))
            ?.Text;
    }

    public string GetCurrentAdvancedSessionId()
    {
        var sessionLog = global::LivingMetalGhost.App.Services.GetRequiredService<AdvancedSessionLogService>();
        return sessionLog.CurrentSessionId;
    }

    public async Task<ProjectMemoryEntry> SaveProjectMemoryAsync(
        string content,
        string type,
        CancellationToken cancellationToken)
    {
        var sessionLog = global::LivingMetalGhost.App.Services.GetRequiredService<AdvancedSessionLogService>();
        var memoryStore = global::LivingMetalGhost.App.Services.GetRequiredService<ProjectMemoryStore>();
        var entry = await memoryStore.AddAsync(
            content: content,
            type: type,
            sourceSessionId: sessionLog.CurrentSessionId,
            source: "memory_editor",
            tags: ["workbench", "promoted"],
            cancellationToken: cancellationToken);

        AdvancedContextRevision++;
        BubbleText = "프로젝트 기억으로 저장했어요.";
        return entry;
    }

    public async Task<ProjectMemoryEntry?> PromoteLastAdvancedAssistantMessageAsync(CancellationToken cancellationToken)
    {
        var lastAssistantMessage = GetLastCompletedAssistantMessageText();
        if (string.IsNullOrWhiteSpace(lastAssistantMessage))
        {
            return null;
        }

        return await SaveProjectMemoryAsync(lastAssistantMessage, "decision", cancellationToken);
    }
}
