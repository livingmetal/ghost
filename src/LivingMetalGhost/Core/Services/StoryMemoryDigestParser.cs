using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 스토리 기억 요약(LLM) 응답에서 fact 목록을 견고하게 추출하는 순수 로직.
/// 모델이 JSON 앞뒤에 잡설을 붙여도 첫 배열만 떼어 파싱한다. 실패하면 빈 목록을 돌려준다.
/// </summary>
public static class StoryMemoryDigestParser
{
    private const int MaxFacts = 8;
    private static readonly string[] AllowedKinds = ["premise", "self", "relationship", "question"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static IReadOnlyList<StoryMemoryFact> Parse(string? llmText)
    {
        var json = ExtractJsonArray(llmText);
        if (json is null)
        {
            return [];
        }

        List<FactDto>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<FactDto>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return [];
        }

        if (raw is null)
        {
            return [];
        }

        var facts = raw
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new StoryMemoryFact
            {
                Kind = NormalizeKind(item.Kind),
                Text = item.Text.Trim(),
                Weight = item.Weight <= 0 ? 1 : Math.Min(item.Weight, 5)
            })
            .GroupBy(fact => fact.Text, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(fact => fact.Weight)
            .Take(MaxFacts)
            .ToList();

        return facts;
    }

    private static string? ExtractJsonArray(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return text.Substring(start, end - start + 1);
    }

    private static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "premise";
        }

        var normalized = kind.Trim().ToLowerInvariant();
        return AllowedKinds.Contains(normalized) ? normalized : "premise";
    }

    private sealed class FactDto
    {
        public string? Kind { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
    }
}
