using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 롤플레잉 응답에 숨겨진 구조화 서사 신호([story: done=G1,G2])를 파싱하는 순수 로직.
/// 의존성이 없어 단독 테스트가 가능하다. 표시 텍스트에서 신호를 제거하고 완료 목표 id를 돌려준다.
/// </summary>
public static class StoryTagParser
{
    private static readonly Regex StoryTagRegex = new(
        @"\[story:\s*(?<body>[^\]]*)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DoneIdsRegex = new(
        @"done\s*=\s*(?<ids>[A-Za-z0-9_,\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static (string Text, IReadOnlyList<string> CompletedObjectiveIds) Parse(string responseText)
    {
        if (string.IsNullOrEmpty(responseText))
        {
            return (responseText ?? string.Empty, []);
        }

        var ids = new List<string>();
        var cleaned = StoryTagRegex.Replace(responseText, match =>
        {
            foreach (Match done in DoneIdsRegex.Matches(match.Groups["body"].Value))
            {
                foreach (var id in done.Groups["ids"].Value.Split(
                             ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    ids.Add(id);
                }
            }

            return string.Empty;
        });

        cleaned = Regex.Replace(cleaned, @"\n{3,}", Environment.NewLine + Environment.NewLine).Trim();
        return (cleaned, ids);
    }
}
