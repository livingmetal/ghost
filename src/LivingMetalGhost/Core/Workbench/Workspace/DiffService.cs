namespace LivingMetalGhost.Core.Workspace;

public enum DiffLineKind
{
    Context,
    Added,
    Removed
}

public sealed record DiffLine(DiffLineKind Kind, string Text);

public sealed record FileDiff(
    string RelativePath,
    IReadOnlyList<DiffLine> Lines,
    int AddedCount,
    int RemovedCount);

/// <summary>
/// 두 텍스트의 줄 단위 차이를 계산하는 읽기 전용 diff 엔진(coding-agent 로드맵 M5의 patch preview 토대).
/// LCS(최장 공통 부분 수열) 기반으로 추가/삭제/유지 줄을 만든다. 파일을 쓰거나 적용하지 않는다.
/// </summary>
public sealed class DiffService
{
    // 너무 큰 파일에서 O(n*m) DP가 폭주하지 않도록 상한을 둔다.
    private const int MaxLinesPerSide = 4000;

    public FileDiff BuildFileDiff(string relativePath, string? oldText, string? newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        if (oldLines.Length > MaxLinesPerSide || newLines.Length > MaxLinesPerSide)
        {
            // 상한 초과 시에는 전체 교체로 표시한다(정확한 LCS 대신 안전한 폴백).
            var fallback = new List<DiffLine>();
            fallback.AddRange(oldLines.Select(line => new DiffLine(DiffLineKind.Removed, line)));
            fallback.AddRange(newLines.Select(line => new DiffLine(DiffLineKind.Added, line)));
            return new FileDiff(relativePath, fallback, newLines.Length, oldLines.Length);
        }

        var lcs = BuildLcsTable(oldLines, newLines);
        var lines = new List<DiffLine>();
        var added = 0;
        var removed = 0;

        int i = 0, j = 0;
        while (i < oldLines.Length && j < newLines.Length)
        {
            if (oldLines[i] == newLines[j])
            {
                lines.Add(new DiffLine(DiffLineKind.Context, oldLines[i]));
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[i]));
                removed++;
                i++;
            }
            else
            {
                lines.Add(new DiffLine(DiffLineKind.Added, newLines[j]));
                added++;
                j++;
            }
        }

        while (i < oldLines.Length)
        {
            lines.Add(new DiffLine(DiffLineKind.Removed, oldLines[i]));
            removed++;
            i++;
        }

        while (j < newLines.Length)
        {
            lines.Add(new DiffLine(DiffLineKind.Added, newLines[j]));
            added++;
            j++;
        }

        return new FileDiff(relativePath, lines, added, removed);
    }

    /// <summary>diff를 사람이 읽을 수 있는 통합 형식 문자열로 만든다(미리보기 표시용).</summary>
    public string RenderUnified(FileDiff diff)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"--- {diff.RelativePath}");
        builder.AppendLine($"+{diff.AddedCount} -{diff.RemovedCount}");
        foreach (var line in diff.Lines)
        {
            var prefix = line.Kind switch
            {
                DiffLineKind.Added => "+",
                DiffLineKind.Removed => "-",
                _ => " "
            };
            builder.AppendLine(prefix + line.Text);
        }

        return builder.ToString().TrimEnd();
    }

    private static int[,] BuildLcsTable(string[] oldLines, string[] newLines)
    {
        var table = new int[oldLines.Length + 1, newLines.Length + 1];
        for (var i = oldLines.Length - 1; i >= 0; i--)
        {
            for (var j = newLines.Length - 1; j >= 0; j--)
            {
                table[i, j] = oldLines[i] == newLines[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}
