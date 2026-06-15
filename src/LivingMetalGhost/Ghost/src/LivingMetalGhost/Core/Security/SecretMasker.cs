using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Security;

/// <summary>
/// 로그(stdout/stderr 등)에 API Key 같은 민감 문자열이 그대로 남지 않도록 마스킹한다.
/// 외부 에이전트 출력은 무엇이 섞일지 모르므로, 알려진 비밀값 + 일반적인 토큰 패턴을 모두 가린다.
/// </summary>
public static partial class SecretMasker
{
    private const string Redacted = "***REDACTED***";

    // sk-..., ghp_..., Bearer xxxxx, AIza... 등 흔한 키/토큰 패턴.
    [GeneratedRegex(@"(?i)\b(sk-[A-Za-z0-9_\-]{8,}|ghp_[A-Za-z0-9]{20,}|AIza[A-Za-z0-9_\-]{20,})\b")]
    private static partial Regex TokenPatternRegex();

    [GeneratedRegex(@"(?i)(bearer\s+)[A-Za-z0-9._\-]{8,}")]
    private static partial Regex BearerRegex();

    /// <summary>알려진 비밀값들을 우선 가린 뒤 일반 토큰 패턴도 마스킹한다.</summary>
    public static string Mask(string? text, params string?[] knownSecrets)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;
        foreach (var secret in knownSecrets)
        {
            if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= 4)
            {
                result = result.Replace(secret, Redacted, StringComparison.Ordinal);
            }
        }

        result = BearerRegex().Replace(result, $"$1{Redacted}");
        result = TokenPatternRegex().Replace(result, Redacted);
        return result;
    }
}
