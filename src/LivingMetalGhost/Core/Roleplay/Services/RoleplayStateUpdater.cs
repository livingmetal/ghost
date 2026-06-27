using System.Text.RegularExpressions;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

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

        state.TurnNumber++;
        state.StoryTime = AdvanceClock(state.StoryTime, 2);
        state.Scene = UpdateScene(state.Scene, cleanUserText, cleanAssistantText);
        state.Summary = AppendBeat(state.Summary, cleanUserText, cleanAssistantText);
        state.Mood = string.IsNullOrWhiteSpace(mood) ? state.Mood : mood.Trim().ToLowerInvariant();
        state.Tension = EstimateTension(state.Tension, cleanUserText, cleanAssistantText);
        state.Affection = EstimateAffection(state.Affection, cleanUserText, cleanAssistantText);
        state.StatusText = BuildStatusText(state.Mood, state.Tension, state.Affection, cleanUserText, cleanAssistantText);
        state.UpdatedAt = DateTimeOffset.Now;

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
        if (!string.IsNullOrWhiteSpace(currentScene)) return currentScene;
        var firstCandidate = FirstSentence(userText);
        if (string.IsNullOrWhiteSpace(firstCandidate) || firstCandidate.Length < 12)
        {
            firstCandidate = FirstSentence(assistantText);
        }
        return string.IsNullOrWhiteSpace(firstCandidate) ? currentScene : TrimToLength(firstCandidate, 180);
    }

    private static string AppendBeat(string currentSummary, string userText, string assistantText)
    {
        var beat = $"- 사용자: {TrimToLength(userText, 140)}\n  캐릭터: {TrimToLength(assistantText, 220)}";
        var nextSummary = string.IsNullOrWhiteSpace(currentSummary)
            ? beat
            : currentSummary.Trim() + Environment.NewLine + beat;
        return TrimSummary(nextSummary, MaximumSummaryCharacters);
    }

    private static int EstimateTension(int currentTension, string userText, string assistantText)
    {
        var text = (userText + " " + assistantText).ToLowerInvariant();
        var raised = new[] { "잘못", "상처", "무서", "분노", "떨", "불안", "짜증", "울" };
        var lowered = new[] { "휴식", "안심", "괜찮", "해결", "조용", "평온", "웃", "차분", "사과", "고마" };
        var tension = currentTension;
        if (raised.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) tension++;
        if (lowered.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) tension--;
        return Math.Clamp(tension, 0, 5);
    }

    private static int EstimateAffection(int currentAffection, string userText, string assistantText)
    {
        var text = (userText + " " + assistantText).ToLowerInvariant();
        var positive = new[] { "미안", "사과", "고마", "도와", "괜찮", "이해", "믿", "친절" };
        var negative = new[] { "싫", "화", "잘못", "무시", "거짓", "짜증", "상처", "울", "겁" };
        var affection = currentAffection;
        if (positive.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) affection += 2;
        if (negative.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) affection -= 2;
        return Math.Clamp(affection, -999, 999);
    }

    private static string BuildStatusText(string mood, int tension, int affection, string userText, string assistantText)
    {
        if (assistantText.Contains("사과", StringComparison.OrdinalIgnoreCase) || userText.Contains("미안", StringComparison.OrdinalIgnoreCase))
        {
            return "사과를 들은 뒤 분노보다 혼란이 커졌다. 아직 경계하지만 대화를 끊지는 못한다.";
        }
        if (tension >= 4)
        {
            return "감정의 압력이 높다. 말끝은 날카롭고, 작은 행동에도 즉각 반응할 수 있다.";
        }
        if (affection < -150)
        {
            return "마음의 거리는 멀다. 다만 완전히 돌아서기보다는 상대의 다음 말을 살피고 있다.";
        }
        return string.IsNullOrWhiteSpace(mood)
            ? "장면의 공기가 잠시 정리되었고, 다음 행동을 기다리고 있다."
            : $"{mood} 분위기 속에서 다음 행동을 기다리고 있다.";
    }

    private static string AdvanceClock(string currentTime, int minutes)
    {
        if (string.IsNullOrWhiteSpace(currentTime)) return "AM 10:13";
        var match = Regex.Match(currentTime.Trim(), @"^(AM|PM)\s*(\d{1,2}):(\d{2})$", RegexOptions.IgnoreCase);
        if (!match.Success) return currentTime;
        var hour = int.Parse(match.Groups[2].Value);
        var minute = int.Parse(match.Groups[3].Value) + minutes;
        while (minute >= 60)
        {
            minute -= 60;
            hour++;
        }
        if (hour > 12) hour -= 12;
        return $"{match.Groups[1].Value.ToUpperInvariant()} {hour:00}:{minute:00}";
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var cleaned = Regex.Replace(text, @"^\s*\[mood:\s*[a-z0-9_-]+\s*\]\s*", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    private static string FirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var match = Regex.Match(text.Trim(), @"^(.{1,220}?[.!?。！？…]|.{1,220})(\s|$)");
        return match.Success ? match.Groups[1].Value.Trim() : TrimToLength(text.Trim(), 180);
    }

    private static string TrimSummary(string summary, int maximumCharacters)
    {
        if (summary.Length <= maximumCharacters) return summary;
        var lines = summary.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
        while (string.Join(Environment.NewLine, lines).Length > maximumCharacters && lines.Count > 2)
        {
            lines.RemoveAt(0);
        }
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string TrimToLength(string text, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maximumLength) return text.Trim();
        return text[..maximumLength].TrimEnd() + "…";
    }
}
