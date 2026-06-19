using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.UI.Presentation;

public sealed class AssistantMessagePresenter
{
    public async Task PresentAsync(
        string response,
        bool isProactive,
        string characterDisplayName,
        ObservableCollection<ChatMessage> targetMessages,
        ConversationMode mode)
    {
        var compact = mode != ConversationMode.Advanced;
        var chunks = CreateChunks(response, compact);

        for (var index = 0; index < chunks.Count; index++)
        {
            var message = new ChatMessage
            {
                SpeakerName = isProactive
                    ? $"{characterDisplayName.ToUpperInvariant()} • 먼저 말 걸기"
                    : characterDisplayName.ToUpperInvariant(),
                IsProactive = isProactive && index == 0,
                IsRoleplay = mode == ConversationMode.Story,
                IsTyping = true
            };
            targetMessages.Add(message);
            await TypeMessageAsync(message, chunks[index]);

            if (index + 1 < chunks.Count)
            {
                await Task.Delay(compact ? 220 : 320);
            }
        }
    }

    public static IReadOnlyList<string> CreateChunks(string response, bool compact)
    {
        var targetLength = compact ? 80 : 130;
        var maximumLength = compact ? 140 : 220;
        var normalized = response.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maximumLength)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var remaining = normalized;
        while (remaining.Length > 0)
        {
            if (remaining.Length <= maximumLength)
            {
                chunks.Add(remaining.Trim());
                break;
            }

            var splitIndex = FindSplitIndex(remaining, targetLength, maximumLength);
            chunks.Add(remaining[..splitIndex].Trim());
            remaining = remaining[splitIndex..].TrimStart();
        }

        return chunks;
    }

    private static async Task TypeMessageAsync(ChatMessage message, string text)
    {
        var elements = GetTextElements(text);
        var batchSize = elements.Count switch
        {
            > 1200 => 5,
            > 700 => 3,
            > 350 => 2,
            _ => 1
        };
        var baseDelayMilliseconds = elements.Count switch
        {
            > 1200 => 8,
            > 700 => 11,
            > 350 => 16,
            _ => 24
        };
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < elements.Count;)
        {
            var currentBatchSize = Math.Min(batchSize, elements.Count - index);
            string lastElement = string.Empty;
            for (var batchIndex = 0; batchIndex < currentBatchSize; batchIndex++)
            {
                lastElement = elements[index++];
                builder.Append(lastElement);
            }

            message.Text = builder.ToString();
            var delay = baseDelayMilliseconds + GetNaturalPauseMilliseconds(lastElement);
            await Task.Delay(delay);
        }

        message.IsTyping = false;
    }

    private static IReadOnlyList<string> GetTextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static int GetNaturalPauseMilliseconds(string textElement)
    {
        return textElement switch
        {
            "." or "!" or "?" or "…" => 180,
            "," or ";" or ":" => 80,
            "\n" or "\r\n" => 220,
            _ => 0
        };
    }

    private static int FindSplitIndex(string text, int targetLength, int maximumLength)
    {
        var searchLength = Math.Min(maximumLength, text.Length);
        var preferredSeparators = new[] { "\n\n", "\n", ". ", "! ", "? ", "。", "！", "？", ", ", " " };
        var bestIndex = -1;
        var bestDistance = int.MaxValue;

        foreach (var separator in preferredSeparators)
        {
            var index = text.LastIndexOf(separator, searchLength - 1, StringComparison.Ordinal);
            if (index <= 20)
            {
                continue;
            }

            var candidate = index + separator.Length;
            var distance = Math.Abs(candidate - targetLength);
            if (distance < bestDistance)
            {
                bestIndex = candidate;
                bestDistance = distance;
            }
        }

        return bestIndex > 0 ? bestIndex : maximumLength;
    }
}
