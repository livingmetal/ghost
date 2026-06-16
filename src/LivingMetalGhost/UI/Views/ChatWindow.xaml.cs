using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class ChatWindow : Window
{
    private const int BubbleHoldMilliseconds = 5200;
    private static readonly Color NormalBorder = Color.FromRgb(0xEE, 0xE3, 0xD8);
    private static readonly Color RoleplayBorder = Color.FromRgb(0x9B, 0x6A, 0xD6);
    private static readonly Color AdvancedBorder = Color.FromRgb(0x7B, 0x4F, 0xC8);

    private readonly DispatcherTimer _bubbleDismissTimer;
    private MainViewModel? _subscribedViewModel;

    public ChatWindow()
    {
        InitializeComponent();
        DataContextChanged += ChatWindow_OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SubscribeToViewModel(DataContext as MainViewModel);
            ApplyModeVisuals();
            HideBubble(immediate: true);
        };

        _bubbleDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(BubbleHoldMilliseconds)
        };
        _bubbleDismissTimer.Tick += (_, _) => HideBubble();
    }

    public void FocusPrompt()
    {
        PromptTextBox.Focus();
        Keyboard.Focus(PromptTextBox);
    }

    public void ShowConsole()
    {
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        FocusPrompt();
    }

    private void ChatWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToViewModel(e.NewValue as MainViewModel);
        ApplyModeVisuals();
    }

    private void SubscribeToViewModel(MainViewModel? viewModel)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsAdvancedMode)
            or nameof(MainViewModel.IsStoryMode)
            or nameof(MainViewModel.CurrentMode)
            or nameof(MainViewModel.ActiveProviderLabel))
        {
            ApplyModeVisuals();
        }

        if (e.PropertyName is nameof(MainViewModel.BubbleText))
        {
            ShowBubble();
            if (_subscribedViewModel is not { IsCharacterSpeaking: true })
            {
                ScheduleBubbleDismiss();
            }
        }

        if (e.PropertyName is nameof(MainViewModel.IsCharacterSpeaking))
        {
            if (_subscribedViewModel is { IsCharacterSpeaking: true })
            {
                ShowBubble();
            }
            else
            {
                ScheduleBubbleDismiss();
            }
        }
    }

    private void ApplyModeVisuals()
    {
        var mode = _subscribedViewModel?.CurrentMode ?? ConversationMode.Daily;
        var targetColor = mode switch
        {
            ConversationMode.Advanced => AdvancedBorder,
            ConversationMode.Story => RoleplayBorder,
            _ => NormalBorder
        };

        TitleLabel.Text = mode switch
        {
            ConversationMode.Advanced => "GHOST WORKBENCH",
            ConversationMode.Story => "ROLEPLAY CONSOLE",
            _ => "PROMPT BUBBLE"
        };
        ProviderLabel.Text = (_subscribedViewModel?.ActiveProviderLabel ?? string.Empty)
            .Replace("STORY:", "ROLEPLAY:", StringComparison.OrdinalIgnoreCase);
        SendButton.Content = mode == ConversationMode.Advanced ? "RUN" : "➤";
        SendButton.Background = new SolidColorBrush(mode switch
        {
            ConversationMode.Advanced => Color.FromRgb(0x7B, 0x4F, 0xC8),
            ConversationMode.Story => Color.FromRgb(0x9B, 0x6A, 0xD6),
            _ => Color.FromRgb(0x9B, 0x6A, 0xD6)
        });

        var animation = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(200));
        BorderAccent.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private void ShowBubble()
    {
        if (_subscribedViewModel is null || string.IsNullOrWhiteSpace(_subscribedViewModel.BubbleText))
        {
            return;
        }

        _bubbleDismissTimer.Stop();
        BubbleHost.Visibility = Visibility.Visible;
        BubbleHost.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void ScheduleBubbleDismiss()
    {
        if (BubbleHost.Visibility != Visibility.Visible)
        {
            return;
        }

        _bubbleDismissTimer.Stop();
        _bubbleDismissTimer.Start();
    }

    private void HideBubble(bool immediate = false)
    {
        _bubbleDismissTimer.Stop();
        if (immediate)
        {
            BubbleHost.BeginAnimation(OpacityProperty, null);
            BubbleHost.Opacity = 0;
            BubbleHost.Visibility = Visibility.Collapsed;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        animation.Completed += (_, _) =>
        {
            if (BubbleHost.Opacity <= 0.01)
            {
                BubbleHost.Visibility = Visibility.Collapsed;
            }
        };
        BubbleHost.BeginAnimation(OpacityProperty, animation);
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleAdvancedMode();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ToggleAdvancedMode()
    {
        _subscribedViewModel?.ToggleAdvancedMode();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void PromptTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        if (DataContext is MainViewModel viewModel && viewModel.SendCommand.CanExecute(null))
        {
            viewModel.SendCommand.Execute(null);
        }
    }
}
