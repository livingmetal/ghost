namespace LivingMetalGhost.Agents;

/// <summary>
/// 기본 에이전트 실행기. 실제 외부 CLI 를 호출하지 않고, 무엇을 할지 요약만 돌려준다.
/// 어떤 승인 모드에서도 파일을 수정하거나 명령을 실행하지 않는다(항상 안전).
/// </summary>
public sealed class MockAgentExecutor : IAgentExecutor
{
    public string Name => "mock";

    public Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct)
    {
        var summary =
            $"[Mock 에이전트] 요청을 접수했지만 실제 실행은 하지 않았습니다.\n" +
            $"- 작업 내용: {request.Instruction}\n" +
            $"- 작업 루트: {(string.IsNullOrWhiteSpace(request.WorkspaceRoot) ? "(미설정)" : request.WorkspaceRoot)}\n" +
            $"- 승인 모드: {request.ApprovalMode}";

        return Task.FromResult(new AgentResult
        {
            Success = true,
            Summary = summary,
            RawOutput = string.Empty,
            ChangedFiles = Array.Empty<string>(),
            RequiresUserReview = true
        });
    }
}
