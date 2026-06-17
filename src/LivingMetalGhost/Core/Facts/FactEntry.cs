namespace LivingMetalGhost.Core.Facts;

public sealed record FactEntry(
    string Key,
    string Category,
    string Source,
    string SourceUrl,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    string PayloadJson,
    string ParserId,
    int SchemaVersion);
