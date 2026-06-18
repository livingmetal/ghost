using System.Text;
using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Workspace;

/// <summary>
/// 고급 모드에서 사용자의 질문과 관련된 읽기 전용 저장소 컨텍스트를 만든다(coding-agent 로드맵 M2, Workflow 1).
/// 프로젝트 지시문(README/AGENT/AGENTS) 일부와, 질문 키워드로 찾은 관련 파일을 경로와 함께 묶어 준다.
/// 모델은 이 스냅샷에 적힌 경로만 인용하도록 안내된다.
/// </summary>
public sealed class WorkspaceContextBuilder
{
    private const int InstructionHeadLines = 40;
    private const int MaxKeywords = 4;
    private const int MaxHitsPerKeyword = 4;
    private const int MaxTotalHits = 14;
    private const int MaxFileMapEntries = 160;

    private static readonly string[] InstructionFiles = ["README.md", "AGENT.md", "AGENTS.md"];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "어디", "있어", "있나", "있는지", "무엇", "어떻게", "구현", "코드", "파일", "알려줘", "보여줘",
        "the", "is", "are", "was", "where", "what", "how", "why", "and", "for", "with", "this", "that"
    };

    private readonly WorkspaceReadService _readService;

    public WorkspaceContextBuilder(WorkspaceReadService readService)
    {
        _readService = readService;
    }

    public string Build(string root, string question)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        var instructions = BuildInstructionSection(root);
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            builder.Append(instructions);
        }

        var fileMap = BuildFileMapSection(root);
        if (!string.IsNullOrWhiteSpace(fileMap))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(fileMap);
        }

        var relevant = BuildRelevantFilesSection(root, question);
        if (!string.IsNullOrWhiteSpace(relevant))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(relevant);
        }

        return builder.ToString().Trim();
    }

    private string BuildInstructionSection(string root)
    {
        var sections = new List<string>();
        foreach (var name in InstructionFiles)
        {
            var slice = _readService.ReadFileSlice(root, name, 1, InstructionHeadLines);
            if (slice is null || slice.Lines.Count == 0)
            {
                continue;
            }

            sections.Add($"### {name} (first {slice.Lines.Count} lines)" + Environment.NewLine +
                         string.Join(Environment.NewLine, slice.Lines));
        }

        if (sections.Count == 0)
        {
            return string.Empty;
        }

        return "## Project instructions" + Environment.NewLine +
               string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine;
    }

    private string BuildFileMapSection(string root)
    {
        var files = _readService.BuildFileMap(root, MaxFileMapEntries + 1);
        if (files.Count == 0)
        {
            return string.Empty;
        }

        var shown = files.Take(MaxFileMapEntries).Select(file => $"- {file.RelativePath}");
        var note = files.Count > MaxFileMapEntries ? $" (showing first {MaxFileMapEntries})" : string.Empty;

        return $"## Workspace file map{note}" + Environment.NewLine +
               string.Join(Environment.NewLine, shown) + Environment.NewLine;
    }

    private string BuildRelevantFilesSection(string root, string question)
    {
        var keywords = ExtractKeywords(question);
        if (keywords.Count == 0)
        {
            return string.Empty;
        }

        var hits = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyword in keywords)
        {
            foreach (var match in _readService.SearchInFiles(root, keyword, MaxHitsPerKeyword))
            {
                var key = $"{match.RelativePath}:{match.LineNumber}";
                if (seen.Add(key))
                {
                    var line = match.Line.Length > 160 ? match.Line[..160] + "…" : match.Line;
                    hits.Add($"- {key}  {line}");
                    if (hits.Count >= MaxTotalHits)
                    {
                        break;
                    }
                }
            }

            if (hits.Count >= MaxTotalHits)
            {
                break;
            }
        }

        if (hits.Count == 0)
        {
            return string.Empty;
        }

        return "## Relevant files for this question (cite these paths; do not invent others)" + Environment.NewLine +
               string.Join(Environment.NewLine, hits) + Environment.NewLine;
    }

    private static IReadOnlyList<string> ExtractKeywords(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        return Regex.Split(question, @"[^\p{L}\p{Nd}_]+")
            .Where(token => token.Length >= 2)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxKeywords)
            .ToArray();
    }
}
