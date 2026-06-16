using System.Text;
using System.Text.RegularExpressions;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 롤플레잉 모드의 가벼운 입력 문법을 LLM이 안정적으로 이해할 수 있는 구조화 텍스트로 바꾼다.
/// 일반 문장 = 대사, *...* = 행동/지문, (...) = 속마음.
/// </summary>
public static class RoleplayInputFormatter
{
    private static readonly Regex SegmentRegex = new(
        @"(?<action>\*[^*]+\*)|(?<thought>\([^\r\n()]+\))",
        RegexOptions.Compiled);

    public static string FormatForPrompt(string rawText)
    {
        var parsed = Parse(rawText);
        if (parsed.Speeches.Count == 0 && parsed.Actions.Count == 0 && parsed.Thoughts.Count == 0)
        {
            return rawText.Trim();
        }

        var builder = new StringBuilder();
        builder.AppendLine("Player input interpreted by roleplay syntax:");
        builder.AppendLine("- Plain text is spoken dialogue heard by characters.");
        builder.AppendLine("- *...* is visible action or scene narration.");
        builder.AppendLine("- (...) is inner thought. Other characters cannot directly know it.");
        builder.AppendLine();

        if (parsed.Speeches.Count > 0)
        {
            builder.AppendLine("Spoken dialogue:");
            foreach (var speech in parsed.Speeches)
            {
                builder.AppendLine($"- \"{speech}\"");
            }
        }

        if (parsed.Actions.Count > 0)
        {
            if (parsed.Speeches.Count > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Visible action / narration:");
            foreach (var action in parsed.Actions)
            {
                builder.AppendLine($"- {action}");
            }
        }

        if (parsed.Thoughts.Count > 0)
        {
            if (parsed.Speeches.Count > 0 || parsed.Actions.Count > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Inner thought, not directly knowable by characters:");
            foreach (var thought in parsed.Thoughts)
            {
                builder.AppendLine($"- {thought}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Continue the scene from this input. Do not decide the player's next action.");
        return builder.ToString().Trim();
    }

    private static ParsedRoleplayInput Parse(string rawText)
    {
        var speeches = new List<string>();
        var actions = new List<string>();
        var thoughts = new List<string>();
        var text = rawText.Replace("\r\n", "\n");
        var lastIndex = 0;

        foreach (Match match in SegmentRegex.Matches(text))
        {
            AddSpeech(text[lastIndex..match.Index], speeches);

            var value = match.Value.Trim();
            if (match.Groups["action"].Success)
            {
                AddClean(value.Trim('*'), actions);
            }
            else if (match.Groups["thought"].Success)
            {
                AddClean(value.Trim('(', ')'), thoughts);
            }

            lastIndex = match.Index + match.Length;
        }

        AddSpeech(text[lastIndex..], speeches);
        return new ParsedRoleplayInput(speeches, actions, thoughts);
    }

    private static void AddSpeech(string value, ICollection<string> speeches)
    {
        AddClean(value, speeches);
    }

    private static void AddClean(string value, ICollection<string> collection)
    {
        var cleaned = Regex.Replace(value, @"\s+", " ").Trim();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            collection.Add(cleaned);
        }
    }

    private sealed record ParsedRoleplayInput(
        IReadOnlyList<string> Speeches,
        IReadOnlyList<string> Actions,
        IReadOnlyList<string> Thoughts);
}
