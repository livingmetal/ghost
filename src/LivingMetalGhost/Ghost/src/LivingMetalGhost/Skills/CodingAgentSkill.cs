using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Skills;

/// <summary>
/// "코드 수정 / 파일 고쳐 / 빌드 / 테스트 / 저장소 분석 / PR" 처럼 무거운 작업 요청을 받아
/// 고급 작업 모드(Agent Executor)로 넘기는 진입점.
///
/// MVP 정책: 절대 바로 실행하지 않는다. 어떤 작업이 필요한지, 어떤 권한이 예상되는지,
/// 현재 승인 모드가 무엇인지 정리해 "승인 요청" 메시지를 돌려준다. 실제 실행은 사용자가
/// 고급 작업 실행을 승인(apply/execute + enable_execution)한 뒤에만 일어난다.
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

    public CodingAgentSkill(AppConfigLoader configLoader, IAgentExecutorFactory executorFactory)
    {
        _configLoader = configLoader;
        _executorFactory = executorFactory;
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
        var config = _configLoader.Load();
        var agents = config.Agents;
        var approvalMode = ParseApprovalMode(agents.ApprovalMode);
        var workspaceRoot = agents.WorkspaceRoot;

        // 고급 콘솔 모드: Codex(분석) + Claude Code(적용) 동시 실행
        if (request.UseAdvancedModel)
        {
            return await HandleAdvancedCodingAsync(request, agents, approvalMode, workspaceRoot, ct);
        }

        // 기본 모드: 설정된 단일 실행기 사용 (suggest 전용 — 파일 변경 없음)
        var executor = _executorFactory.Create(agents.DefaultExecutor);
        var agentResult = await executor.RunAsync(
            new AgentRequest
            {
                Instruction = request.RawText,
                WorkspaceRoot = workspaceRoot,
                ApprovalMode = AgentApprovalMode.Suggest
            },
            ct);

        var bubble =
            "이 요청은 고급 작업 모드가 필요합니다.\n" +
            $"대상 작업: {request.RawText}\n" +
            "예상 권한: 파일 읽기 / 패치 제안 / 명령 실행\n" +
            $"현재 승인 모드: {DescribeMode(approvalMode)}\n" +
            $"작업 루트: {(string.IsNullOrWhiteSpace(workspaceRoot) ? "(미설정 — 설정에서 지정하세요)" : workspaceRoot)}\n" +
            $"사용 에이전트: {executor.Name}\n" +
            "진행하려면 고급 작업 실행을 승인하세요.\n\n" +
            agentResult.Summary;

        return new SkillResult
        {
            BubbleText = bubble,
            Mood = "serious",
            Action = "advanced-task-suggested",
            UsedLlm = false,
            RawData = agentResult
        };
    }

    private async Task<SkillResult> HandleAdvancedCodingAsync(
        UserRequest request,
        AgentsSettings agents,
        AgentApprovalMode approvalMode,
        string workspaceRoot,
        CancellationToken ct)
    {
        // 고급 콘솔 모드에서는 Codex와 Claude Code를 병렬로 실행해 각각 제안을 수집한다.
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
            ? "(미설정 — 설정에서 지정하세요)"
            : workspaceRoot;

        var bubble =
            "고급 콘솔 모드 — Codex + Claude Code 분석 결과\n" +
            $"대상 작업: {request.RawText}\n" +
            $"작업 루트: {rootLabel}\n" +
            $"현재 승인 모드: {DescribeMode(approvalMode)}\n\n" +
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
