namespace LivingMetalGhost.Core.Workspace;

public sealed record PatchReviewItem(PatchProposal Proposal, FileDiff Diff, bool IsNewFile);

/// <summary>
/// 모델 응답을 검토 가능한 패치 미리보기로 바꾸고, 승인된 것만 적용하는 연결 서비스(M5 wiring).
/// 파싱 → 현재 파일과 diff → (사용자 승인) → 적용 순서를 한 곳에 모은다.
/// 검토는 읽기 전용이며, 실제 쓰기는 Apply에서 approved=true 일 때만 일어난다.
/// </summary>
public sealed class PatchReviewService
{
    private readonly WorkspaceReadService _readService;
    private readonly DiffService _diffService;
    private readonly PatchApplyService _applyService;

    public PatchReviewService(
        WorkspaceReadService readService,
        DiffService diffService,
        PatchApplyService applyService)
    {
        _readService = readService;
        _diffService = diffService;
        _applyService = applyService;
    }

    public IReadOnlyList<PatchReviewItem> Review(string root, string? modelText)
    {
        var items = new List<PatchReviewItem>();
        foreach (var proposal in PatchProposalParser.Parse(modelText))
        {
            var current = _readService.ReadAllText(root, proposal.RelativePath);
            var diff = _diffService.BuildFileDiff(proposal.RelativePath, current, proposal.NewContent);
            items.Add(new PatchReviewItem(proposal, diff, current is null));
        }

        return items;
    }

    public PatchApplyResult Apply(string root, PatchProposal proposal, bool approved)
    {
        return _applyService.Apply(root, proposal, approved);
    }
}
