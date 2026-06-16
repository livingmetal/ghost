using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LivingMetalGhost.UI.Behaviors;

/// <summary>
/// 읽기 전용 RichTextBox에 롤플레잉 인라인 문법을 적용한다.
/// *...* 는 이탤릭(지문/행동)으로 렌더링하고, 별표 자체는 화면에서 감춘다.
/// 줄바꿈(\n)은 LineBreak 로 보존한다.
/// </summary>
public static class InlineMarkup
{
    private static readonly Brush NarrationBrush = CreateFrozenBrush(Color.FromRgb(0x3B, 0x41, 0x49));

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

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        var text = GetText(richTextBox);
        var roleplay = GetRoleplay(richTextBox);

        // 행동/속마음(이탤릭) 글자는 일반 대화보다 1pt 작게 해서 평문과 더 잘 구분되게 한다.
        var baseFontSize = double.IsNaN(richTextBox.FontSize) || richTextBox.FontSize <= 0
            ? 14.0
            : richTextBox.FontSize;
        var narrationFontSize = Math.Max(1.0, baseFontSize - 1.0);

        var document = richTextBox.Document;
        document.PagePadding = new Thickness(0);
        document.Blocks.Clear();

        var paragraph = new Paragraph { Margin = new Thickness(0) };
        foreach (var inline in BuildInlines(text, roleplay, narrationFontSize))
        {
            paragraph.Inlines.Add(inline);
        }

        document.Blocks.Add(paragraph);
    }

    private static IEnumerable<Inline> BuildInlines(string rawText, bool roleplay, double narrationFontSize)
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

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
