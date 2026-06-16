using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LivingMetalGhost.UI.Behaviors;

/// <summary>
/// 읽기 전용 RichTextBox에 인라인 문법을 적용한다.
/// 롤플레잉 모드에서는 *행동* / (속마음)을 이탤릭으로 렌더링하고,
/// 고급 모드에서는 가벼운 Markdown 렌더링을 적용한다.
/// </summary>
public static class InlineMarkup
{
    private static readonly Brush NarrationBrush = CreateFrozenBrush(Color.FromRgb(0x3B, 0x41, 0x49));
    private static readonly Brush BodyBrush = CreateFrozenBrush(Color.FromRgb(0x20, 0x2A, 0x35));
    private static readonly Brush MutedBrush = CreateFrozenBrush(Color.FromRgb(0x64, 0x70, 0x7E));
    private static readonly Brush CodeBackgroundBrush = CreateFrozenBrush(Color.FromRgb(0xF2, 0xF4, 0xF8));
    private static readonly Brush QuoteBrush = CreateFrozenBrush(Color.FromRgb(0x5D, 0x6B, 0x82));
    private static readonly FontFamily CodeFont = new("Consolas");

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(InlineMarkup),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    // 스토리(롤플레잉) 메시지에서만 *행동* / (속마음) 마크업을 적용한다(일상/고급 모드는 평문 유지).
    public static readonly DependencyProperty RoleplayProperty =
        DependencyProperty.RegisterAttached(
            "Roleplay",
            typeof(bool),
            typeof(InlineMarkup),
            new PropertyMetadata(false, OnTextChanged));

    public static bool GetRoleplay(DependencyObject element) => (bool)element.GetValue(RoleplayProperty);

    public static void SetRoleplay(DependencyObject element, bool value) => element.SetValue(RoleplayProperty, value);

    // 고급 모드용 경량 Markdown 렌더링. 외부 패키지 없이 heading/list/code/bold/italic/inline-code 정도만 처리한다.
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.RegisterAttached(
            "Markdown",
            typeof(bool),
            typeof(InlineMarkup),
            new PropertyMetadata(false, OnTextChanged));

    public static bool GetMarkdown(DependencyObject element) => (bool)element.GetValue(MarkdownProperty);

    public static void SetMarkdown(DependencyObject element, bool value) => element.SetValue(MarkdownProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        var text = GetText(richTextBox);
        var roleplay = GetRoleplay(richTextBox);
        var markdown = GetMarkdown(richTextBox);

        var document = richTextBox.Document;
        document.PagePadding = new Thickness(0);
        document.Blocks.Clear();

        if (markdown)
        {
            RenderMarkdown(document, text, richTextBox.FontSize);
            return;
        }

        // 행동/속마음(이탤릭) 글자는 일반 대화보다 1pt 작게 해서 평문과 더 잘 구분되게 한다.
        var baseFontSize = double.IsNaN(richTextBox.FontSize) || richTextBox.FontSize <= 0
            ? 14.0
            : richTextBox.FontSize;
        var narrationFontSize = Math.Max(1.0, baseFontSize - 1.0);

        var paragraph = new Paragraph { Margin = new Thickness(0) };
        foreach (var inline in BuildRoleplayInlines(text, roleplay, narrationFontSize))
        {
            paragraph.Inlines.Add(inline);
        }

        document.Blocks.Add(paragraph);
    }

    private static IEnumerable<Inline> BuildRoleplayInlines(string rawText, bool roleplay, double narrationFontSize)
    {
        var inlines = new List<Inline>();
        foreach (var segment in InlineMarkupParser.Parse(rawText, roleplay))
        {
            AddSegment(inlines, segment.Text, segment.Italic, narrationFontSize);
        }

        return inlines;
    }

    private static void AddSegment(List<Inline> inlines, string segment, bool italic, double narrationFontSize)
    {
        if (segment.Length == 0)
        {
            return;
        }

        var lines = segment.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length == 0)
            {
                continue;
            }

            var run = new Run(lines[i]);
            if (italic)
            {
                run.FontStyle = FontStyles.Italic;
                run.Foreground = NarrationBrush;
                run.FontSize = narrationFontSize;
            }

            inlines.Add(run);
        }
    }

    private static void RenderMarkdown(FlowDocument document, string rawText, double requestedFontSize)
    {
        var baseFontSize = double.IsNaN(requestedFontSize) || requestedFontSize <= 0
            ? 14.0
            : requestedFontSize;
        var text = (rawText ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (string.IsNullOrWhiteSpace(text))
        {
            document.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
            return;
        }

        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                index++;
                continue;
            }

            if (IsFence(trimmed))
            {
                var codeLines = new List<string>();
                index++;
                while (index < lines.Length && !IsFence(lines[index].Trim()))
                {
                    codeLines.Add(lines[index]);
                    index++;
                }

                if (index < lines.Length && IsFence(lines[index].Trim()))
                {
                    index++;
                }

                AddCodeBlock(document, string.Join("\n", codeLines), baseFontSize);
                continue;
            }

            if (TryGetHeading(trimmed, out var level, out var headingText))
            {
                AddHeading(document, headingText, level, baseFontSize);
                index++;
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                var rule = new Paragraph(new Run("────────────────────────"))
                {
                    Margin = new Thickness(0, 8, 0, 8),
                    Foreground = MutedBrush
                };
                document.Blocks.Add(rule);
                index++;
                continue;
            }

            if (TryGetListItem(trimmed, out var listText))
            {
                AddListParagraph(document, listText, baseFontSize);
                index++;
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                var quote = trimmed.TrimStart('>', ' ');
                AddQuote(document, quote, baseFontSize);
                index++;
                continue;
            }

            var paragraphLines = new List<string> { line.TrimEnd() };
            index++;
            while (index < lines.Length)
            {
                var next = lines[index];
                var nextTrimmed = next.Trim();
                if (nextTrimmed.Length == 0 ||
                    IsFence(nextTrimmed) ||
                    TryGetHeading(nextTrimmed, out _, out _) ||
                    TryGetListItem(nextTrimmed, out _) ||
                    nextTrimmed.StartsWith(">", StringComparison.Ordinal) ||
                    IsHorizontalRule(nextTrimmed))
                {
                    break;
                }

                paragraphLines.Add(next.TrimEnd());
                index++;
            }

            AddMarkdownParagraph(document, string.Join("\n", paragraphLines), baseFontSize);
        }
    }

    private static void AddHeading(FlowDocument document, string text, int level, double baseFontSize)
    {
        var fontSize = level switch
        {
            1 => baseFontSize + 7,
            2 => baseFontSize + 5,
            3 => baseFontSize + 3,
            _ => baseFontSize + 1
        };
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, level <= 2 ? 14 : 10, 0, 6),
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = BodyBrush
        };
        AddMarkdownInlines(paragraph.Inlines, text);
        document.Blocks.Add(paragraph);
    }

    private static void AddMarkdownParagraph(FlowDocument document, string text, double baseFontSize)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 4, 0, 9),
            FontSize = baseFontSize,
            LineHeight = baseFontSize + 8,
            Foreground = BodyBrush
        };
        AddMarkdownInlines(paragraph.Inlines, text);
        document.Blocks.Add(paragraph);
    }

    private static void AddListParagraph(FlowDocument document, string text, double baseFontSize)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(16, 2, 0, 4),
            FontSize = baseFontSize,
            LineHeight = baseFontSize + 8,
            Foreground = BodyBrush
        };
        paragraph.Inlines.Add(new Run("• ") { Foreground = MutedBrush, FontWeight = FontWeights.Bold });
        AddMarkdownInlines(paragraph.Inlines, text);
        document.Blocks.Add(paragraph);
    }

    private static void AddQuote(FlowDocument document, string text, double baseFontSize)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(14, 5, 0, 9),
            FontSize = baseFontSize,
            LineHeight = baseFontSize + 8,
            Foreground = QuoteBrush,
            BorderBrush = MutedBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0)
        };
        AddMarkdownInlines(paragraph.Inlines, text);
        document.Blocks.Add(paragraph);
    }

    private static void AddCodeBlock(FlowDocument document, string text, double baseFontSize)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 6, 0, 12),
            Padding = new Thickness(10),
            FontFamily = CodeFont,
            FontSize = Math.Max(11, baseFontSize - 1),
            LineHeight = baseFontSize + 7,
            Background = CodeBackgroundBrush,
            Foreground = BodyBrush
        };
        document.Blocks.Add(paragraph);
    }

    private static void AddMarkdownInlines(InlineCollection inlines, string text)
    {
        for (var index = 0; index < text.Length;)
        {
            var marker = FindNextInlineMarker(text, index);
            if (marker < 0)
            {
                AddPlainRun(inlines, text[index..]);
                break;
            }

            if (marker > index)
            {
                AddPlainRun(inlines, text[index..marker]);
            }

            if (text[marker] == '`')
            {
                var close = text.IndexOf('`', marker + 1);
                if (close > marker)
                {
                    var run = new Run(text[(marker + 1)..close])
                    {
                        FontFamily = CodeFont,
                        Background = CodeBackgroundBrush,
                        FontSize = 13
                    };
                    inlines.Add(run);
                    index = close + 1;
                    continue;
                }
            }

            if (marker + 1 < text.Length && text[marker] == '*' && text[marker + 1] == '*')
            {
                var close = text.IndexOf("**", marker + 2, StringComparison.Ordinal);
                if (close > marker)
                {
                    var span = new Bold();
                    AddPlainRun(span.Inlines, text[(marker + 2)..close]);
                    inlines.Add(span);
                    index = close + 2;
                    continue;
                }
            }

            if (text[marker] == '*')
            {
                var close = text.IndexOf('*', marker + 1);
                if (close > marker)
                {
                    var span = new Italic();
                    AddPlainRun(span.Inlines, text[(marker + 1)..close]);
                    inlines.Add(span);
                    index = close + 1;
                    continue;
                }
            }

            AddPlainRun(inlines, text[marker].ToString());
            index = marker + 1;
        }
    }

    private static void AddPlainRun(InlineCollection inlines, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            if (lines[i].Length > 0)
            {
                inlines.Add(new Run(lines[i]));
            }
        }
    }

    private static int FindNextInlineMarker(string text, int start)
    {
        var markers = new[]
        {
            text.IndexOf('`', start),
            text.IndexOf('*', start)
        }.Where(index => index >= 0);
        return markers.Any() ? markers.Min() : -1;
    }

    private static bool IsFence(string trimmed) => trimmed.StartsWith("```", StringComparison.Ordinal);

    private static bool TryGetHeading(string trimmed, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var hashes = 0;
        while (hashes < trimmed.Length && trimmed[hashes] == '#')
        {
            hashes++;
        }

        if (hashes is < 1 or > 6 || hashes >= trimmed.Length || trimmed[hashes] != ' ')
        {
            return false;
        }

        level = hashes;
        text = trimmed[(hashes + 1)..].Trim();
        return text.Length > 0;
    }

    private static bool TryGetListItem(string trimmed, out string text)
    {
        text = string.Empty;
        if (trimmed.Length >= 2 && (trimmed[0] is '-' or '*' or '+') && trimmed[1] == ' ')
        {
            text = trimmed[2..].Trim();
            return text.Length > 0;
        }

        var dot = trimmed.IndexOf('.');
        if (dot > 0 && dot < 4 && dot + 1 < trimmed.Length && trimmed[dot + 1] == ' ' && trimmed[..dot].All(char.IsDigit))
        {
            text = trimmed[(dot + 2)..].Trim();
            return text.Length > 0;
        }

        return false;
    }

    private static bool IsHorizontalRule(string trimmed) =>
        trimmed.Length >= 3 && trimmed.All(ch => ch == '-' || ch == '*' || ch == '_');

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
