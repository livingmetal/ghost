using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Workspace;

/// <summary>
/// 모델 응답에서 파일 편집 제안을 견고하게 추출하는 순수 로직.
/// 약속된 형식만 인식한다:
/// <code>
/// ```ghost-edit path=relative/path.cs
/// (전체 새 파일 내용)
/// ```
/// </code>
/// 형식이 없으면 빈 목록을 돌려준다(모델이 그냥 설명만 했을 때 안전).
/// </summary>
public static class PatchProposalParser
{
    private static readonly Regex EditBlockRegex = new(
        @"```ghost-edit\s+path=(?<path>[^\r\n]+)\r?\n(?<body>.*?)\r?\n?```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<PatchProposal> Parse(string? modelText, string rationale = "")
    {
        if (string.IsNullOrEmpty(modelText))
        {
            return [];
        }

        var proposals = new List<PatchProposal>();
        foreach (Match match in EditBlockRegex.Matches(modelText))
        {
            var path = match.Groups["path"].Value.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            proposals.Add(new PatchProposal(
                NormalizePath(path),
                match.Groups["body"].Value,
                rationale));
        }

        return proposals;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}
