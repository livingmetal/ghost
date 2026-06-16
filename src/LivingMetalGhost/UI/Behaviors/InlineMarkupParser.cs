using System.Collections.Generic;

namespace LivingMetalGhost.UI.Behaviors;

/// <summary>
/// 롤플레잉 인라인 문법을 세그먼트로 분해하는 순수 로직. WPF에 의존하지 않아 단독 테스트가 가능하다.
/// *행동* / **행동** 은 별표를 감춘 이탤릭으로, (속마음) 은 괄호를 남긴 이탤릭으로 처리한다.
/// 스트리밍(글자 단위 타이핑) 중에도 여는 기호가 보이면 즉시 이탤릭으로 처리한다.
/// </summary>
public static class InlineMarkupParser
{
    public readonly record struct Segment(string Text, bool Italic);

    /// <param name="roleplay">스토리(롤플레잉) 모드 여부. true 일 때만 *행동* / (속마음) 마크업을 적용한다.
    /// false(일상/고급 모드)면 별표·괄호를 그대로 둔 평문으로 처리한다.</param>
    public static IReadOnlyList<Segment> Parse(string rawText, bool roleplay = false)
    {
        var segments = new List<Segment>();
        if (string.IsNullOrEmpty(rawText))
        {
            return segments;
        }

        var text = rawText.Replace("\r\n", "\n").Replace('\r', '\n');

        if (!roleplay)
        {
            // 일상/고급 모드에서는 마크업을 적용하지 않고 글자 그대로 둔다.
            Add(segments, text, italic: false);
            return segments;
        }

        var index = 0;
        while (index < text.Length)
        {
            var marker = FindNextMarker(text, index, parseThoughts: true);
            if (marker < 0)
            {
                Add(segments, text.Substring(index), italic: false);
                break;
            }

            if (marker > index)
            {
                Add(segments, text.Substring(index, marker - index), italic: false);
            }

            var consumed = text[marker] == '*'
                ? ConsumeAction(text, marker, segments)
                : ConsumeThought(text, marker, segments);

            if (consumed < 0)
            {
                break; // 닫히지 않은 기호를 글자로 처리하고 종료했다.
            }

            index = consumed;
        }

        return segments;
    }

    // 행동: *...* / **...**. 별표는 감춘다. 반환값은 다음 인덱스, 종료 시 -1.
    private static int ConsumeAction(string text, int open, List<Segment> segments)
    {
        var contentStart = SkipStars(text, open);
        var close = text.IndexOf('*', contentStart);
        if (close >= 0)
        {
            // 닫힌 쌍은 공백/줄바꿈이 별표에 붙어 있어도 항상 이탤릭으로 본다.
            Add(segments, text.Substring(contentStart, close - contentStart), italic: true);
            return SkipStars(text, close);
        }

        if (contentStart < text.Length && !char.IsWhiteSpace(text[contentStart]))
        {
            // 여는 별표가 글자에 붙어 있으면 타이핑 중 행동으로 보고 끝까지 즉시 이탤릭.
            Add(segments, text.Substring(contentStart), italic: true);
        }
        else
        {
            // "2 * 3" 처럼 공백에 둘러싸인 외톨이 별표는 글자 그대로 둔다.
            Add(segments, text.Substring(open), italic: false);
        }

        return -1;
    }

    // 속마음: (...). 괄호는 남기고 앞에 💭 를 붙인다(행동과 구분). 반환값은 다음 인덱스, 종료 시 -1.
    private const string ThoughtPrefix = "💭";

    private static int ConsumeThought(string text, int open, List<Segment> segments)
    {
        var close = text.IndexOf(')', open + 1);
        if (close >= 0)
        {
            Add(segments, ThoughtPrefix + text.Substring(open, close - open + 1), italic: true);
            return close + 1;
        }

        // 닫는 괄호가 아직 없다(스트리밍 중). 여는 괄호부터 끝까지 이탤릭.
        Add(segments, ThoughtPrefix + text.Substring(open), italic: true);
        return -1;
    }

    private static int FindNextMarker(string text, int start, bool parseThoughts)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '*')
            {
                return i;
            }

            if (parseThoughts && text[i] == '(')
            {
                return i;
            }
        }

        return -1;
    }

    private static int SkipStars(string text, int index)
    {
        var i = index;
        while (i < text.Length && text[i] == '*')
        {
            i++;
        }

        return i;
    }

    private static void Add(List<Segment> segments, string text, bool italic)
    {
        if (text.Length > 0)
        {
            segments.Add(new Segment(text, italic));
        }
    }
}
