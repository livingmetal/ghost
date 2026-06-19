using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Conversation;

public static class CharacterSpeechSanitizer
{
    private static readonly string[] BannedPrefixes =
    [
        @"^좋은 질문입니다[.!。]?\s*",
        @"^좋은 질문이에요[.!。]?\s*",
        @"^요약하면[:,]?\s*",
        @"^정리하면[:,]?\s*",
        @"^결론부터 말하면[:,]?\s*",
        @"^핵심부터 말하면[:,]?\s*",
        @"^다음과 같이 정리할 수 있습니다[.!。]?\s*"
    ];

    private static readonly string[] BannedPhrases =
    [
        "도움이 되었으면 좋겠습니다.",
        "도움이 되었으면 좋겠어요.",
        "필요하시면 더 설명드릴게요.",
        "필요하면 더 설명드릴게요.",
        "궁금한 점이 있으면 말씀해 주세요.",
        "추가로 궁금한 점이 있으면 알려주세요."
    ];

    public static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sanitized = text.Trim();
        foreach (var prefix in BannedPrefixes)
        {
            sanitized = Regex.Replace(
                sanitized,
                prefix,
                string.Empty,
                RegexOptions.IgnoreCase);
        }

        foreach (var phrase in BannedPhrases)
        {
            sanitized = sanitized.Replace(
                phrase,
                string.Empty,
                StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Regex.Replace(
            sanitized,
            @"\n{3,}",
            Environment.NewLine + Environment.NewLine);
        return sanitized.Trim();
    }
}
