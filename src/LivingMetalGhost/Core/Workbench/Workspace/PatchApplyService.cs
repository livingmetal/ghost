using System.IO;
using LivingMetalGhost.Agents;

namespace LivingMetalGhost.Core.Workspace;

/// <summary>
/// 승인된 패치 제안을 워크스페이스에 적용한다(coding-agent 로드맵 M5의 apply 단계).
/// 안전 게이트: 사용자 승인 없이는 절대 쓰지 않고, 워크스페이스 루트 밖 경로는 거부한다.
/// 삭제는 하지 않는다(전체 내용 쓰기만). 미리보기에 표시된 내용 그대로 기록한다.
/// </summary>
public sealed class PatchApplyService
{
    public PatchApplyResult Apply(string root, PatchProposal proposal, bool approved)
    {
        if (!approved)
        {
            return new PatchApplyResult(false, proposal.RelativePath, "승인되지 않은 패치는 적용하지 않습니다.");
        }

        if (!WorkspaceGuard.TryResolveRoot(root, out var resolvedRoot, out var rootError))
        {
            return new PatchApplyResult(false, proposal.RelativePath, rootError);
        }

        if (string.IsNullOrWhiteSpace(proposal.RelativePath))
        {
            return new PatchApplyResult(false, proposal.RelativePath, "대상 경로가 비어 있습니다.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(resolvedRoot, proposal.RelativePath));
        if (!WorkspaceGuard.IsInsideRoot(resolvedRoot, fullPath))
        {
            return new PatchApplyResult(false, proposal.RelativePath, "워크스페이스 루트 밖 경로에는 적용할 수 없습니다.");
        }

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, proposal.NewContent, System.Text.Encoding.UTF8);
            return new PatchApplyResult(true, proposal.RelativePath, $"적용 완료 ({proposal.NewContent.Length}자).");
        }
        catch (IOException ex)
        {
            return new PatchApplyResult(false, proposal.RelativePath, $"적용 실패: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new PatchApplyResult(false, proposal.RelativePath, $"적용 실패: {ex.Message}");
        }
    }
}
