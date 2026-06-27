using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// Memory API 후보를 적용할 때 세계관 전제와 장기 약속 같은 핵심 사실이
/// 한 번의 불완전한 요약 응답 때문에 사라지지 않도록 보호한다.
/// </summary>
public static class StoryFactMerger
{
    public const int MaxFacts = 16;

    private static readonly HashSet<string> ProtectedKinds = new(
        ["premise", "self", "promise", "open_loop", "boundary"],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<StoryMemoryFact> Merge(
        IEnumerable<StoryMemoryFact>? existingFacts,
        IEnumerable<StoryMemoryFact>? candidateFacts)
    {
        var merged = new Dictionary<string, StoryMemoryFact>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidateFacts ?? [])
        {
            AddOrPrefer(merged, candidate);
        }

        foreach (var existing in existingFacts ?? [])
        {
            if (IsProtectedKind(existing.Kind))
            {
                AddOrPrefer(merged, existing);
            }
        }

        return merged.Values
            .OrderByDescending(fact => IsProtectedKind(fact.Kind))
            .ThenByDescending(fact => fact.Weight)
            .Take(MaxFacts)
            .Select(Clone)
            .ToList();
    }

    public static bool IsProtectedKind(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && ProtectedKinds.Contains(kind.Trim());

    private static void AddOrPrefer(
        Dictionary<string, StoryMemoryFact> merged,
        StoryMemoryFact source)
    {
        var clone = Clone(source);
        if (string.IsNullOrWhiteSpace(clone.Text))
        {
            return;
        }

        if (!merged.TryGetValue(clone.Text, out var current) ||
            IsProtectedKind(clone.Kind) && !IsProtectedKind(current.Kind) ||
            clone.Weight > current.Weight)
        {
            merged[clone.Text] = clone;
        }
    }

    private static StoryMemoryFact Clone(StoryMemoryFact source) => new()
    {
        Kind = StoryMemoryDigestParser.NormalizeKind(source.Kind),
        Text = NormalizeText(source.Text),
        Weight = Math.Clamp(source.Weight <= 0 ? 1 : source.Weight, 1, 5)
    };

    private static string NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
