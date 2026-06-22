using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public static class StoryFactMerger
{
    public const int MaxFacts = 24;

    private static readonly string[] AllowedKinds =
    [
        "premise", "self", "player", "relationship", "promise", "open_loop",
        "preference", "location", "item", "boundary", "question"
    ];

    private static readonly string[] ProtectedKinds =
    [
        "premise", "self", "relationship", "promise", "open_loop", "boundary"
    ];

    public static IReadOnlyList<StoryMemoryFact> Merge(
        IEnumerable<StoryMemoryFact>? existingFacts,
        IEnumerable<StoryMemoryFact>? candidateFacts)
    {
        var now = DateTimeOffset.Now;
        var merged = new Dictionary<string, StoryMemoryFact>(StringComparer.OrdinalIgnoreCase);

        foreach (var fact in existingFacts ?? [])
        {
            AddOrMerge(merged, fact, now, refreshMention: false);
        }

        foreach (var fact in candidateFacts ?? [])
        {
            AddOrMerge(merged, fact, now, refreshMention: true);
        }

        return merged.Values
            .Where(fact => string.Equals(NormalizeStatus(fact.Status), "active", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(fact => IsProtectedKind(fact.Kind))
            .ThenByDescending(fact => fact.Weight)
            .ThenByDescending(fact => fact.MentionCount)
            .ThenByDescending(fact => fact.LastMentionedAt)
            .Take(MaxFacts)
            .Select(Clone)
            .ToList();
    }

    public static bool IsProtectedKind(string? kind)
    {
        return !string.IsNullOrWhiteSpace(kind) &&
               ProtectedKinds.Contains(kind.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "premise";
        }

        var normalized = kind.Trim().ToLowerInvariant();
        return AllowedKinds.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : "premise";
    }

    private static void AddOrMerge(
        Dictionary<string, StoryMemoryFact> merged,
        StoryMemoryFact source,
        DateTimeOffset now,
        bool refreshMention)
    {
        var incoming = Clone(source);
        incoming.Text = NormalizeText(incoming.Text);
        if (string.IsNullOrWhiteSpace(incoming.Text))
        {
            return;
        }

        incoming.LastMentionedAt = refreshMention ? now : incoming.LastMentionedAt;

        if (!merged.TryGetValue(incoming.Text, out var current))
        {
            merged[incoming.Text] = incoming;
            return;
        }

        current.Kind = ChooseKind(current, incoming);
        current.Weight = Math.Max(current.Weight, incoming.Weight);
        current.Status = ChooseStatus(current.Status, incoming.Status);
        current.FirstSeenAt = current.FirstSeenAt <= incoming.FirstSeenAt ? current.FirstSeenAt : incoming.FirstSeenAt;
        current.LastMentionedAt = current.LastMentionedAt >= incoming.LastMentionedAt ? current.LastMentionedAt : incoming.LastMentionedAt;
        current.MentionCount = Math.Max(1, current.MentionCount) + Math.Max(1, incoming.MentionCount);
    }

    private static string ChooseKind(StoryMemoryFact current, StoryMemoryFact incoming)
    {
        if (IsProtectedKind(current.Kind))
        {
            return current.Kind;
        }

        if (IsProtectedKind(incoming.Kind))
        {
            return incoming.Kind;
        }

        return incoming.Weight > current.Weight ? incoming.Kind : current.Kind;
    }

    private static string ChooseStatus(string current, string incoming)
    {
        return string.Equals(NormalizeStatus(current), "active", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(NormalizeStatus(incoming), "active", StringComparison.OrdinalIgnoreCase)
            ? "active"
            : NormalizeStatus(current);
    }

    private static StoryMemoryFact Clone(StoryMemoryFact source)
    {
        var now = DateTimeOffset.Now;
        return new StoryMemoryFact
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
            Kind = NormalizeKind(source.Kind),
            Text = NormalizeText(source.Text),
            Weight = Math.Clamp(source.Weight <= 0 ? 1 : source.Weight, 1, 5),
            Status = NormalizeStatus(source.Status),
            FirstSeenAt = source.FirstSeenAt == default ? now : source.FirstSeenAt,
            LastMentionedAt = source.LastMentionedAt == default ? now : source.LastMentionedAt,
            MentionCount = Math.Max(1, source.MentionCount)
        };
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "active";
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized is "active" or "resolved" or "superseded" ? normalized : "active";
    }

    private static string NormalizeText(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
