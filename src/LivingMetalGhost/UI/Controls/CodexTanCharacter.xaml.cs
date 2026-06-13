using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace LivingMetalGhost.UI.Controls;

public partial class CodexTanCharacter : UserControl
{
    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood),
        typeof(string),
        typeof(CodexTanCharacter),
        new PropertyMetadata("idle", OnMoodChanged));

    private Storyboard? _idleStoryboard;
    private Storyboard? _thinkingStoryboard;
    private Storyboard? _speakingStoryboard;

    public CodexTanCharacter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public string Mood
    {
        get => (string)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    private static void OnMoodChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CodexTanCharacter character && character.IsLoaded)
        {
            character.ApplyMood(e.NewValue as string ?? "idle");
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _idleStoryboard = (Storyboard)FindResource("IdleMotion");
        _thinkingStoryboard = (Storyboard)FindResource("ThinkingMotion");
        _speakingStoryboard = (Storyboard)FindResource("SpeakingMotion");

        _idleStoryboard.Begin(this, true);
        ApplyMood(Mood);
    }

    private void ApplyMood(string mood)
    {
        _thinkingStoryboard?.Remove(this);
        _speakingStoryboard?.Remove(this);

        ThinkingDots.Visibility = Visibility.Collapsed;
        SpeakingMouth.Visibility = Visibility.Collapsed;
        IdleMouth.Visibility = Visibility.Visible;
        LeftEye.Height = 12;
        RightEye.Height = 12;

        switch (mood.ToLowerInvariant())
        {
            case "thinking":
                ThinkingDots.Visibility = Visibility.Visible;
                IdleMouth.Visibility = Visibility.Collapsed;
                _thinkingStoryboard?.Begin(this, true);
                break;
            case "speaking":
                SpeakingMouth.Visibility = Visibility.Visible;
                IdleMouth.Visibility = Visibility.Collapsed;
                _speakingStoryboard?.Begin(this, true);
                break;
            case "happy":
                LeftEye.Height = 5;
                RightEye.Height = 5;
                break;
            case "strict":
                LeftEye.Height = 6;
                RightEye.Height = 6;
                break;
        }
    }
}
