using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Conversation;

public sealed record ParsedConversationResponse(string Text, string? Mood);

public static partial class ConversationResponseParser
{
    public static ParsedConversationResponse ParseMoodTag(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new ParsedConversationResponse(string.Empty, null);
        }

        var match = MoodTagRegex().Match(responseText);
        if (!match.Success)
        {
            return new ParsedConversationResponse(responseText.Trim(), null);
        }

        var mood = match.Groups["mood"].Value.Trim().ToLowerInvariant();
        var text = responseText[match.Length..].Trim();
        return new ParsedConversationResponse(text, mood);
    }

    public static string StripLegacyRoleplayTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = LegacyStoryTagRegex().Replace(text, string.Empty);
        return ExcessiveNewlineRegex()
            .Replace(cleaned, Environment.NewLine + Environment.NewLine)
            .Trim();
    }

    [GeneratedRegex(
        @"^\s*\[mood:\s*(?<mood>[a-z0-9_-]+)\s*\]\s*",
        RegexOptions.IgnoreCase)]
    private static partial Regex MoodTagRegex();

    [GeneratedRegex(@"\[story:\s*[^\]]*\]", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyStoryTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessiveNewlineRegex();
}
