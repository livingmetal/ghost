using System.IO;

namespace LivingMetalGhost.Agents;

/// <summary>
/// 외부 에이전트가 workspace_root 밖의 파일을 건드리지 못하도록 막는 방어 유틸.
/// 경로 정규화 후 루트 하위인지 검증한다(상위 경로 탈출 ../ 차단 포함).
/// </summary>
public static class WorkspaceGuard
{
    /// <summary>workspace_root 가 실제 존재하는 절대 경로인지 검증하고 정규화한다.</summary>
    public static bool TryResolveRoot(string configuredRoot, out string resolvedRoot, out string error)
    {
        resolvedRoot = string.Empty;
        error = string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(configuredRoot ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expanded))
        {
            error = "작업 루트(workspace_root)가 설정되지 않았습니다.";
            return false;
        }

        if (!Directory.Exists(expanded))
        {
            error = $"작업 루트 경로가 존재하지 않습니다: {expanded}";
            return false;
        }

        resolvedRoot = Path.GetFullPath(expanded);
        return true;
    }

    /// <summary>대상 경로가 루트 안에 있는지 확인한다.</summary>
    public static bool IsInsideRoot(string root, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var normalizedCandidate = Path.GetFullPath(candidatePath);

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>변경 파일 목록 중 루트를 벗어난 항목만 반환한다(비어 있으면 안전).</summary>
    public static IReadOnlyList<string> FindEscapingPaths(string root, IEnumerable<string> candidatePaths)
    {
        var escaping = new List<string>();
        foreach (var path in candidatePaths)
        {
            if (!IsInsideRoot(root, path))
            {
                escaping.Add(path);
            }
        }

        return escaping;
    }
}
