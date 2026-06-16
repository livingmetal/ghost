using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
    private const int WindowIdleHideMilliseconds = 45000;
    private static readonly Color NormalBorder = Color.FromRgb(0xEE, 0xE3, 0xD8);
    private static readonly Color RoleplayBorder = Color.FromRgb(0x9B, 0x6A, 0xD6);
    private static readonly Color AdvancedBorder = Color.FromRgb(0x7B, 0x4F, 0xC8);

    private readonly DispatcherTimer _bubbleDismissTimer;
    private readonly DispatcherTimer _windowIdleTimer;
    private MainViewModel? _subscribedViewModel;
    private ChatMessage? _trackedAssistantMessage;

    public bool HasManualPosition { get; private set; }

    public ChatWindow()
    {
        InitializeComponent();
        DataContextChanged += ChatWindow_OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SubscribeToViewModel(DataContext as MainViewModel);
            ApplyModeVisuals();
            ApplySavedPlacement();
            HideBubble(immediate: true);
        };
        PreviewMouseMove += (_, _) => RestartWindowIdleTimer();
        PreviewKeyDown += (_, _) => RestartWindowIdleTimer();
        Deactivated += (_, _) => RestartWindowIdleTimer();

        _bubbleDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(BubbleHoldMilliseconds)
        };
        _bubbleDismissTimer.Tick += (_, _) => HideBubble();

        _windowIdleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(WindowIdleHideMilliseconds)
        };
        _windowIdleTimer.Tick += (_, _) => TryHideAfterIdle();
    }

    public void FocusPrompt()
    {
        PromptTextBox.Focus();
        Keyboard.Focus(PromptTextBox);
    }

    public void ShowConsole()
    {
        if (IsVisible)
        {
            HideConsole();
            return;
        }

        Show();
        ApplySavedPlacement();
        Activate();
        SetDailyAwakeState();
        FocusPrompt();
        RestartWindowIdleTimer();
    }

    public void HideConsole()
    {
        _windowIdleTimer.Stop();
        HideBubble(immediate: true);
        Hide();
        SetDailySleepState();
    }

    private void ChatWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToViewModel(e.NewValue as MainViewModel);
        ApplyModeVisuals();
        ApplySavedPlacement();
    }

    private void SubscribeToViewModel(MainViewModel? viewModel)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _subscribedViewModel.Messages.CollectionChanged -= Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        _trackedAssistantMessage = null;
        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            _subscribedViewModel.Messages.CollectionChanged += Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged += Message_OnPropertyChanged;
            }
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
            if (_trackedAssistantMessage is null && _subscribedViewModel is not null)
            {
                SetBubbleText(_subscribedViewModel.BubbleText);
            }

            ShowBubble(allowEmpty: false);
            RestartWindowIdleTimer();
            if (_subscribedViewModel is not { IsCharacterSpeaking: true })
            {
                ScheduleBubbleDismiss();
            }
        }

        if (e.PropertyName is nameof(MainViewModel.IsCharacterSpeaking))
        {
            if (_subscribedViewModel is { IsCharacterSpeaking: true })
            {
                ShowBubble(allowEmpty: true);
                RestartWindowIdleTimer();
            }
            else
            {
                ScheduleBubbleDismiss();
                RestartWindowIdleTimer();
            }
        }
    }

    private void Messages_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ChatMessage>())
            {
                item.PropertyChanged += Message_OnPropertyChanged;
                if (!item.IsUser)
                {
                    TrackAssistantMessage(item);
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ChatMessage>())
            {
                item.PropertyChanged -= Message_OnPropertyChanged;
                if (ReferenceEquals(_trackedAssistantMessage, item))
                {
                    _trackedAssistantMessage = null;
                }
            }
        }
    }

    private void Message_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatMessage message || !ReferenceEquals(message, _trackedAssistantMessage))
        {
            return;
        }

        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.DisplayText) or nameof(ChatMessage.IsTyping))
        {
            SetBubbleText(message.DisplayText);
            ShowBubble(allowEmpty: message.IsTyping);
            RestartWindowIdleTimer();
            if (!message.IsTyping && _subscribedViewModel is not { IsCharacterSpeaking: true })
            {
                ScheduleBubbleDismiss();
            }
        }
    }

    private void TrackAssistantMessage(ChatMessage message)
    {
        _trackedAssistantMessage = message;
        SetBubbleText(message.DisplayText);
        ShowBubble(allowEmpty: true);
        RestartWindowIdleTimer();
    }

    private void SetBubbleText(string? text)
    {
        BubbleTextBlock.Text = text ?? string.Empty;
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

    private void ShowBubble(bool allowEmpty = false)
    {
        if (!allowEmpty && string.IsNullOrWhiteSpace(BubbleTextBlock.Text))
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

    private void RestartWindowIdleTimer()
    {
        if (!IsVisible)
        {
            return;
        }

        _windowIdleTimer.Stop();
        _windowIdleTimer.Start();
    }

    private void TryHideAfterIdle()
    {
        _windowIdleTimer.Stop();
        if (!IsVisible)
        {
            return;
        }

        if (_subscribedViewModel is { IsCharacterSpeaking: true } ||
            !string.IsNullOrWhiteSpace(PromptTextBox.Text) ||
            IsKeyboardFocusWithin)
        {
            RestartWindowIdleTimer();
            return;
        }

        HideConsole();
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
            RememberManualPlacement();
            RestartWindowIdleTimer();
        }
    }

    private void InputPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        RestartWindowIdleTimer();
        if (e.ClickCount >= 2)
        {
            e.Handled = true;
            ToggleAdvancedMode();
            return;
        }

        if (sender is TextBox || e.OriginalSource is TextBox)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
                RememberManualPlacement();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void ApplySavedPlacement()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        var placement = _subscribedViewModel.GetDailyChatWindowPlacement();
        if (!placement.HasPosition)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(placement.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(placement.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
        HasManualPosition = true;
    }

    private void RememberManualPlacement()
    {
        HasManualPosition = true;
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
        _subscribedViewModel?.SaveDailyChatWindowPlacement(Left, Top);
    }

    private void ToggleAdvancedMode()
    {
        _subscribedViewModel?.ToggleAdvancedMode();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        HideConsole();
    }

    private void PromptTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        RestartWindowIdleTimer();
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

    private void SetDailyAwakeState()
    {
        if (_subscribedViewModel is null || _subscribedViewModel.IsAdvancedMode || _subscribedViewModel.IsStoryMode)
        {
            return;
        }

        _subscribedViewModel.CharacterMood = "listening";
        _subscribedViewModel.CharacterStateLabel = "DAILY:LISTENING";
    }

    private void SetDailySleepState()
    {
        if (_subscribedViewModel is null || _subscribedViewModel.IsAdvancedMode || _subscribedViewModel.IsStoryMode || _subscribedViewModel.IsCharacterSpeaking)
        {
            return;
        }

        _subscribedViewModel.CharacterMood = "idle";
        _subscribedViewModel.CharacterStateLabel = "DAILY:IDLE";
    }
}
