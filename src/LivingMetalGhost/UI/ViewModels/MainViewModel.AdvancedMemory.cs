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
        var memoryStore = App.Services.GetRequiredService<ProjectMemoryStore>();
        return memoryStore.BuildDisplayText();
    }

    public void StartNewAdvancedSession()
    {
        var sessionLog = App.Services.GetRequiredService<AdvancedSessionLogService>();
        sessionLog.StartNewSession();
        AdvancedContextRevision++;
        BubbleText = "새 고급 작업 세션을 시작했어요.";
    }

    public async Task<ProjectMemoryEntry?> PromoteLastAdvancedAssistantMessageAsync(CancellationToken cancellationToken)
    {
        var lastAssistantMessage = Messages
            .Reverse()
            .FirstOrDefault(message => !message.IsUser && !message.IsTyping && !string.IsNullOrWhiteSpace(message.Text));

        if (lastAssistantMessage is null)
        {
            return null;
        }

        var sessionLog = App.Services.GetRequiredService<AdvancedSessionLogService>();
        var memoryStore = App.Services.GetRequiredService<ProjectMemoryStore>();
        var entry = await memoryStore.AddAsync(
            content: lastAssistantMessage.Text,
            type: "decision",
            sourceSessionId: sessionLog.CurrentSessionId,
            source: "last_assistant_response",
            tags: ["workbench", "promoted"],
            cancellationToken: cancellationToken);

        AdvancedContextRevision++;
        BubbleText = "마지막 고급 답변을 프로젝트 기억으로 저장했어요.";
        return entry;
    }
}
