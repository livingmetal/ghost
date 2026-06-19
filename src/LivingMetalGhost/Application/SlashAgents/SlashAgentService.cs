using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.AppCore.SlashAgents;

public sealed class SlashAgentService
{
    private readonly SlashIntentPlanner _planner;
    private readonly SlashAgentResponseComposer _responseComposer;
    private readonly IReadOnlyDictionary<string, ISlashCapabilityHandler> _handlers;
    private readonly ConversationService _conversationService;

    public SlashAgentService(
        SlashIntentPlanner planner,
        SlashAgentResponseComposer responseComposer,
        IEnumerable<ISlashCapabilityHandler> handlers,
        ConversationService conversationService)
    {
        _planner = planner;
        _responseComposer = responseComposer;
        _handlers = handlers.ToDictionary(
            handler => handler.Capability,
            StringComparer.OrdinalIgnoreCase);
        _conversationService = conversationService;
    }

    public async Task<SkillResult> ExecuteAsync(
        string intentText,
        CancellationToken cancellationToken)
    {
        var plan = await _planner.PlanAsync(intentText, cancellationToken);
        var capabilityResult = await ExecuteCapabilityAsync(
            plan,
            cancellationToken);
        var composed = await _responseComposer.ComposeAsync(
            intentText,
            capabilityResult,
            cancellationToken);

        var rememberedUserText = string.IsNullOrWhiteSpace(intentText)
            ? "/"
            : $"/{intentText}";
        _conversationService.RememberExternalTurn(
            ConversationMode.Daily,
            rememberedUserText,
            composed.Text,
            $"slash-agent:{plan.Capability}");

        return new SkillResult
        {
            BubbleText = composed.Text,
            Mood = "idle",
            Action = $"slash-agent:{plan.Capability}",
            UsedLlm = plan.UsedLlm || composed.UsedLlm,
            RawData = plan
        };
    }

    private Task<SlashCapabilityResult> ExecuteCapabilityAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        if (_handlers.TryGetValue(plan.Capability, out var handler))
        {
            return handler.ExecuteAsync(plan, cancellationToken);
        }

        var facts = plan.Capability == SlashCapabilities.Help
            ? BuildHelpText()
            : "실행할 수 있는 기능을 확정하지 못했어. /날짜, /시간, /문지캠퍼스 점심 식사, /대전 날씨처럼 입력해 줘.";
        return Task.FromResult(new SlashCapabilityResult(
            plan.Capability,
            facts,
            Success: false));
    }

    private static string BuildHelpText()
    {
        return """
            슬래시 에이전트에서 사용할 수 있는 기능:
            - /날짜
            - /시간
            - /문지캠퍼스 점심 식사
            - /대전 날씨
            - /10분 뒤 물 마시기 알림
            //로 시작하는 입력은 일반 대화로 처리해.
            """.Trim();
    }
}
