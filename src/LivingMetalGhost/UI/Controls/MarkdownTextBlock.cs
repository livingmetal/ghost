using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace LivingMetalGhost.UI.Controls;

public sealed class MarkdownTextBlock : TextBlock
{
    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText),
        typeof(string),
        typeof(MarkdownTextBlock),
        new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static readonly DependencyProperty IsTypingProperty = DependencyProperty.Register(
        nameof(IsTyping),
        typeof(bool),
        typeof(MarkdownTextBlock),
        new PropertyMetadata(false, OnMarkdownChanged));

    private static readonly Regex MarkdownTokenRegex = new(
        @"(?<strong>\*\*(?<strongText>[^*]+)\*\*)|(?<italic>\*(?<italicText>[^*]+)\*)",
        RegexOptions.Compiled);

    public string MarkdownText
    {
        get => (string)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public bool IsTyping
    {
        get => (bool)GetValue(IsTypingProperty);
        set => SetValue(IsTypingProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownTextBlock)dependencyObject).RenderMarkdown();
    }

    private void RenderMarkdown()
    {
        Inlines.Clear();
        var text = MarkdownText ?? string.Empty;
        var lastIndex = 0;

        foreach (Match match in MarkdownTokenRegex.Matches(text))
        {
            AddPlainText(text[lastIndex..match.Index]);
            var value = match.Groups["strong"].Success
                ? match.Groups["strongText"].Value
                : match.Groups["italicText"].Value;
            Inlines.Add(new Run(value) { FontStyle = FontStyles.Italic });
            lastIndex = match.Index + match.Length;
        }

        AddPlainText(text[lastIndex..]);
        if (IsTyping)
        {
            Inlines.Add(new Run("▌"));
        }
    }

    private void AddPlainText(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Inlines.Add(new Run(value));
        }
    }
}
