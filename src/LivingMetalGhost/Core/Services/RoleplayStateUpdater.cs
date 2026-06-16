using System.Text.RegularExpressions;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 롤플레잉 모드에서 대화가 오갈 때 story_state.json과 memory.jsonl을 가볍게 갱신한다.
/// 추가 LLM 호출 없이 휴리스틱으로만 동작하므로 토큰 비용을 늘리지 않는다.
/// </summary>
public sealed class RoleplayStateUpdater
{
    private const int MaximumSummaryCharacters = 1600;
    private readonly StoryStateStore _storyStateStore;

    public RoleplayStateUpdater(StoryStateStore storyStateStore)
    {
        _storyStateStore = storyStateStore;
    }

    public void UpdateAfterTurn(
        string userText,
        string assistantText,
        string mood)
    {
        var state = _storyStateStore.Load();
        if (!state.Enabled)
        {
            return;
        }

        var cleanUserText = CleanText(userText);
        var cleanAssistantText = CleanText(assistantText);
        if (string.IsNullOrWhiteSpace(cleanUserText) &&
            string.IsNullOrWhiteSpace(cleanAssistantText))
        {
            return;
        }

        state.Scene = UpdateScene(state.Scene, cleanUserText, cleanAssistantText);
        state.Summary = AppendBeat(state.Summary, cleanUserText, cleanAssistantText);
        state.Mood = string.IsNullOrWhiteSpace(mood) ? state.Mood : mood.Trim().ToLowerInvariant();
        state.Tension = EstimateTension(state.Tension, cleanUserText, cleanAssistantText);

        _storyStateStore.Save(state);
        _storyStateStore.AppendMemory(new RoleplayMemoryEntry
        {
            Timestamp = DateTimeOffset.Now,
            UserText = cleanUserText,
            AssistantText = cleanAssistantText,
            Mood = state.Mood,
            Tension = state.Tension,
            Scene = state.Scene
        });
    }

    private static string UpdateScene(string currentScene, string userText, string assistantText)
    {
        if (!string.IsNullOrWhiteSpace(currentScene))
        {
            return currentScene;
        }

        var firstCandidate = FirstSentence(userText);
        if (string.IsNullOrWhiteSpace(firstCandidate) || firstCandidate.Length < 12)
        {
            firstCandidate = FirstSentence(assistantText);
        }

        if (string.IsNullOrWhiteSpace(firstCandidate))
        {
            return currentScene;
        }

        return TrimToLength(firstCandidate, 180);
    }

    private static string AppendBeat(string currentSummary, string userText, string assistantText)
    {
        var userBeat = TrimToLength(userText, 140);
        var assistantBeat = TrimToLength(assistantText, 220);
        var beat = $"- 사용자: {userBeat}\n  캐릭터: {assistantBeat}";

        var nextSummary = string.IsNullOrWhiteSpace(currentSummary)
            ? beat
            : currentSummary.Trim() + Environment.NewLine + beat;

        return TrimSummary(nextSummary, MaximumSummaryCharacters);
    }

    private static int EstimateTension(int currentTension, string userText, string assistantText)
    {
        var text = (userText + " " + assistantText).ToLowerInvariant();
        var dangerWords = new[]
        {
            "위험", "경고", "비상", "오류", "침입", "전투", "추격", "공격", "폭발", "붕괴", "잠금", "이상", "검은", "정지", "실패"
        };
        var calmWords = new[]
        {
            "휴식", "안심", "괜찮", "해결", "조용", "평온", "웃", "차분", "완료", "성공"
        };

        var tension = currentTension;
        if (dangerWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            tension++;
        }

        if (calmWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            tension--;
        }

        return Math.Clamp(tension, 0, 5);
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(text, @"^\s*\[mood:\s*[a-z0-9_-]+\s*\]\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = Regex.Match(text.Trim(), @"^(.{1,220}?[.!?。！？…]|.{1,220})(\s|$)");
        return match.Success ? match.Groups[1].Value.Trim() : TrimToLength(text.Trim(), 180);
    }

    private static string TrimSummary(string summary, int maximumCharacters)
    {
        if (summary.Length <= maximumCharacters)
        {
            return summary;
        }

        var lines = summary
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        while (string.Join(Environment.NewLine, lines).Length > maximumCharacters && lines.Count > 2)
        {
            lines.RemoveAt(0);
        }

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string TrimToLength(string text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maximumLength)
        {
            return text.Trim();
        }

        return text[..maximumLength].TrimEnd() + "…";
    }
}
