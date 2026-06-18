using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Skills;

/// <summary>
/// "코드 수정 / 파일 고쳐 / 빌드 / 테스트 / 저장소 분석 / PR" 처럼 무거운 작업 요청을 받아
/// 고급 작업 모드(Agent Executor)로 넘기는 진입점.
///
/// MVP 정책: 실제 작업성 명령은 고급 모드에서만 처리한다. 일반/롤플레잉 모드에서는
/// 캐릭터 대화와 명령 실행 경계를 분리하기 위해 작업 실행 후보도 만들지 않는다.
/// </summary>
public sealed class CodingAgentSkill : IGhostSkill
{
    private static readonly string[] TriggerPhrases =
    [
        "코드 수정", "코드 고쳐", "코드 고쳐줘", "파일 고쳐", "파일 수정", "파일 만들어", "파일 생성",
        "빌드해", "빌드 해", "빌드하고", "테스트해", "테스트 해", "테스트 돌려",
        "저장소 분석", "리포 분석", "레포 분석", "리팩터", "리팩토링",
        "커밋해", "커밋 해", "pr 만들", "pr 생성", "pull request", "풀 리퀘", "풀리퀘", "패치 만들", "패치해"
    ];

    private readonly AppConfigLoader _configLoader;
    private readonly IAgentExecutorFactory _executorFactory;
    private readonly WorkspaceStore _workspaceStore;
    private readonly AgentWorkspacePolicy _workspacePolicy;

    public CodingAgentSkill(
        AppConfigLoader configLoader,
        IAgentExecutorFactory executorFactory,
        WorkspaceStore workspaceStore,
        AgentWorkspacePolicy workspacePolicy)
    {
        _configLoader = configLoader;
        _executorFactory = executorFactory;
        _workspaceStore = workspaceStore;
        _workspacePolicy = workspacePolicy;
    }

    public string Name => "CodingAgent";
    public string Description => "코드/파일/빌드/테스트/저장소 같은 무거운 작업을 고급 에이전트로 넘기는 스킬.";
    public IReadOnlyList<string> Examples => ["이 코드 고쳐줘", "저장소 분석해줘", "빌드하고 테스트해"];

    public bool CanHandle(UserRequest request)
    {
        var text = request.RawText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TriggerPhrases.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SkillResult> HandleAsync(UserRequest request, CancellationToken ct)
    {
        if (!request.UseAdvancedModel)
        {
            return new SkillResult
            {
                BubbleText = "코드/파일/빌드 같은 작업 명령은 고급 모드에서만 처리할 수 있어. 일반/롤플레잉 모드에서는 명령 후보를 만들지 않아.",
                Mood = "serious",
                Action = "command-blocked-outside-advanced",
                UsedLlm = false
            };
        }

        var config = _configLoader.Load();
        var agents = config.Agents;
        var approvalMode = ParseApprovalMode(agents.ApprovalMode);
        var workspaceRoot = ResolveWorkspaceRoot(agents.WorkspaceRoot);

        return await HandleAdvancedCodingAsync(request, agents, approvalMode, workspaceRoot, ct);
    }

    private async Task<SkillResult> HandleAdvancedCodingAsync(
        UserRequest request,
        AgentsSettings agents,
        AgentApprovalMode approvalMode,
        string workspaceRoot,
        CancellationToken ct)
    {
        var codexExecutor = _executorFactory.Create("codex-cli");
        var claudeExecutor = _executorFactory.Create("claude-code");

        var agentRequest = new AgentRequest
        {
            Instruction = request.RawText,
            WorkspaceRoot = workspaceRoot,
            ApprovalMode = AgentApprovalMode.Suggest
        };

        var codexTask = codexExecutor.RunAsync(agentRequest, ct);
        var claudeTask = claudeExecutor.RunAsync(agentRequest, ct);

        await Task.WhenAll(codexTask, claudeTask);

        var codexResult = await codexTask;
        var claudeResult = await claudeTask;

        var rootLabel = string.IsNullOrWhiteSpace(workspaceRoot)
            ? "(미설정 — 워크스페이스에서 지정하세요)"
            : workspaceRoot;

        var bubble =
            "고급 콘솔 모드 — Codex + Claude Code 분석 결과\n" +
            $"대상 작업: {request.RawText}\n" +
            $"작업 루트: {rootLabel}\n" +
            $"현재 승인 모드: {DescribeMode(approvalMode)}\n" +
            "Workspace policy:\n" +
            _workspacePolicy.BuildExecutionPolicyLabel(agentRequest, agents.WorkspaceRoot) + "\n\n" +
            $"[Codex]\n{codexResult.Summary}\n\n" +
            $"[Claude Code]\n{claudeResult.Summary}";

        return new SkillResult
        {
            BubbleText = bubble,
            Mood = "serious",
            Action = "advanced-coding-duo-suggested",
            UsedLlm = false,
            RawData = new { Codex = codexResult, ClaudeCode = claudeResult }
        };
    }

    private string ResolveWorkspaceRoot(string agentsWorkspaceRoot)
    {
        var workspace = _workspaceStore.Load();
        return string.IsNullOrWhiteSpace(workspace.RootPath)
            ? agentsWorkspaceRoot
            : workspace.RootPath;
    }

    private static AgentApprovalMode ParseApprovalMode(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "ask" => AgentApprovalMode.Ask,
            "apply" => AgentApprovalMode.Apply,
            "execute" => AgentApprovalMode.Execute,
            _ => AgentApprovalMode.Suggest
        };

    private static string DescribeMode(AgentApprovalMode mode) => mode switch
    {
        AgentApprovalMode.Ask => "ask (실행 전 확인)",
        AgentApprovalMode.Apply => "apply (승인 후 패치 적용)",
        AgentApprovalMode.Execute => "execute (승인 후 명령 실행)",
        _ => "suggest (제안만, 파일 변경 없음)"
    };
}
