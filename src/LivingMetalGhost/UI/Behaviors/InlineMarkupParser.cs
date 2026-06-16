using System.Collections.Generic;

namespace LivingMetalGhost.UI.Behaviors;

/// <summary>
/// 롤플레잉 인라인 문법(*행동*)을 세그먼트로 분해하는 순수 로직. WPF에 의존하지 않아 단독 테스트가 가능하다.
/// 스트리밍(글자 단위 타이핑) 중에도 여는 별표가 보이면 즉시 이탤릭으로 처리한다.
/// </summary>
public static class InlineMarkupParser
{
    public readonly record struct Segment(string Text, bool Italic);

    public static IReadOnlyList<Segment> Parse(string rawText)
    {
        var segments = new List<Segment>();
        if (string.IsNullOrEmpty(rawText))
        {
            return segments;
        }

        var text = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        var index = 0;
        while (index < text.Length)
        {
            var open = text.IndexOf('*', index);
            if (open < 0)
            {
                Add(segments, text.Substring(index), italic: false);
                break;
            }

            if (open > index)
            {
                Add(segments, text.Substring(index, open - index), italic: false);
            }

            // 연속된 별표(*, ** 등)는 하나의 구분자로 본다(마크다운식 **굵게**도 행동으로 처리).
            var contentStart = SkipStars(text, open);
            var close = text.IndexOf('*', contentStart);
            if (close >= 0)
            {
                // 닫힌 쌍은 공백/줄바꿈이 별표에 붙어 있어도 항상 이탤릭으로 본다.
                Add(segments, text.Substring(contentStart, close - contentStart), italic: true);
                index = SkipStars(text, close);
                continue;
            }

            // 닫는 별표가 없다(스트리밍 중이거나 외톨이 별표).
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

            break;
        }

        return segments;
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
