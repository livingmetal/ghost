namespace LivingMetalGhost.Agents;

/// <summary>
/// 외부 작업 에이전트(Claude Code / Codex CLI 등) 실행 계층.
/// 일반 텍스트 응답을 만드는 ILlmProvider 와 분리된, 저장소 분석·파일 수정 제안·
/// 빌드/테스트 실행 같은 무거운 작업을 다루는 인터페이스다.
/// </summary>
public interface IAgentExecutor
{
    /// <summary>에이전트 식별 이름(mock, claude-code, codex-cli 등).</summary>
    string Name { get; }

    Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct);
}

/// <summary>에이전트 승인/실행 모드.</summary>
public enum AgentApprovalMode
{
    /// <summary>실행 전에 사용자에게 무엇을 할지 묻기만 한다.</summary>
    Ask,

    /// <summary>제안만 한다. 파일을 실제로 수정하지 않는다(기본값, 가장 안전).</summary>
    Suggest,

    /// <summary>제안된 패치를 사용자 승인 후 적용한다.</summary>
    Apply,

    /// <summary>명령/빌드/테스트까지 사용자 승인 후 실행한다.</summary>
    Execute
}

public sealed class AgentRequest
{
    public string Instruction { get; init; } = "";
    public string WorkspaceRoot { get; init; } = "";
    public AgentApprovalMode ApprovalMode { get; init; } = AgentApprovalMode.Suggest;
}

public sealed class AgentResult
{
    public bool Success { get; init; }
    public string Summary { get; init; } = "";
    public string RawOutput { get; init; } = "";
    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    /// <summary>true 이면 사용자가 결과를 검토/승인해야 다음 단계로 진행할 수 있다.</summary>
    public bool RequiresUserReview { get; init; }
}

/// <summary>이름으로 에이전트 실행기를 생성한다.</summary>
public interface IAgentExecutorFactory
{
    IAgentExecutor Create(string executorName);
}
