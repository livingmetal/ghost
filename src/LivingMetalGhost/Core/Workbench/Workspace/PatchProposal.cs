namespace LivingMetalGhost.Core.Workspace;

/// <summary>
/// 고급 모드에서 모델이 제안한 한 파일 단위 편집(전체 새 내용). 적용 전 단계의 데이터일 뿐이며,
/// 실제 쓰기는 사용자 승인 후 PatchApplyService 에서만 일어난다.
/// </summary>
public sealed record PatchProposal(
    string RelativePath,
    string NewContent,
    string Rationale)
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed record PatchApplyResult(bool Success, string RelativePath, string Message);
