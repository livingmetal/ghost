using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class SpeechBubbleWindow : Window
{
    private const int BubbleHoldMilliseconds = 5200;
    private readonly DispatcherTimer _bubbleDismissTimer;
    private MainViewModel? _viewModel;
    private ChatMessage? _trackedMessage;

    public SpeechBubbleWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => Subscribe(e.NewValue as MainViewModel);
        Loaded += (_, _) =>
        {
            Subscribe(DataContext as MainViewModel);
            HideBubble(immediate: true);
        };

        _bubbleDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(BubbleHoldMilliseconds)
        };
        _bubbleDismissTimer.Tick += (_, _) => HideBubble();
    }

    public void PositionNear(Window characterWindow)
    {
        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        var bubbleWidth = ActualWidth > 0 ? ActualWidth : Width;
        var bubbleHeight = ActualHeight > 0 ? ActualHeight : MinHeight;
        var horizontalOverlap = Math.Clamp(characterWindow.ActualWidth * 0.38, 128, 220);
        var verticalOffset = Math.Clamp(characterWindow.ActualHeight * 0.06, 8, 42);

        var preferredLeft = characterWindow.Left - bubbleWidth + horizontalOverlap;
        if (preferredLeft < workArea.Left)
        {
            preferredLeft = characterWindow.Left + characterWindow.ActualWidth - horizontalOverlap;
        }

        Left = Math.Clamp(preferredLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - bubbleWidth));
        Top = Math.Clamp(characterWindow.Top + verticalOffset, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - bubbleHeight));
    }

    private void Subscribe(MainViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _viewModel.Messages.CollectionChanged -= Messages_OnCollectionChanged;
            foreach (var message in _viewModel.Messages)
            {
                message.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        _trackedMessage = null;
        _viewModel = viewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.Messages.CollectionChanged += Messages_OnCollectionChanged;
        foreach (var message in _viewModel.Messages)
        {
            message.PropertyChanged += Message_OnPropertyChanged;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsAdvancedMode) or nameof(MainViewModel.IsStoryMode))
        {
            if (_viewModel is { IsAdvancedMode: true } or { IsStoryMode: true })
            {
                HideBubble(immediate: true);
            }
        }

        if (e.PropertyName is nameof(MainViewModel.BubbleText))
        {
            if (_trackedMessage is null && _viewModel is not null)
            {
                BubbleTextBlock.Text = _viewModel.BubbleText;
            }

            ShowBubble(allowEmpty: false);
            if (_viewModel is not { IsCharacterSpeaking: true })
            {
                ScheduleBubbleDismiss();
            }
        }

        if (e.PropertyName is nameof(MainViewModel.IsCharacterSpeaking))
        {
            if (_viewModel is { IsCharacterSpeaking: true })
            {
                ShowBubble(allowEmpty: true);
            }
            else
            {
                ScheduleBubbleDismiss();
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
                if (!item.IsUser && !item.IsRoleplay)
                {
                    TrackMessage(item);
                }
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ChatMessage>())
            {
                item.PropertyChanged -= Message_OnPropertyChanged;
                if (ReferenceEquals(_trackedMessage, item))
                {
                    _trackedMessage = null;
                }
            }
        }
    }

    private void Message_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatMessage message || !ReferenceEquals(message, _trackedMessage))
        {
            return;
        }

        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.DisplayText) or nameof(ChatMessage.IsTyping))
        {
            BubbleTextBlock.Text = message.DisplayText;
            ShowBubble(allowEmpty: message.IsTyping);
            if (!message.IsTyping && _viewModel is not { IsCharacterSpeaking: true })
            {
                ScheduleBubbleDismiss();
            }
        }
    }

    private void TrackMessage(ChatMessage message)
    {
        _trackedMessage = message;
        BubbleTextBlock.Text = message.DisplayText;
        ShowBubble(allowEmpty: true);
    }

    private void ShowBubble(bool allowEmpty)
    {
        if (_viewModel is { IsAdvancedMode: true } or { IsStoryMode: true })
        {
            return;
        }

        if (!allowEmpty && string.IsNullOrWhiteSpace(BubbleTextBlock.Text))
        {
            return;
        }

        _bubbleDismissTimer.Stop();
        if (Owner is Window owner)
        {
            PositionNear(owner);
        }

        if (!IsVisible)
        {
            Show();
        }

        BubbleRoot.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void ScheduleBubbleDismiss()
    {
        if (!IsVisible)
        {
            return;
        }

        _bubbleDismissTimer.Stop();
        _bubbleDismissTimer.Start();
    }

    public void HideBubble(bool immediate = false)
    {
        _bubbleDismissTimer.Stop();
        if (immediate)
        {
            BubbleRoot.BeginAnimation(OpacityProperty, null);
            BubbleRoot.Opacity = 0;
            Hide();
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
            if (BubbleRoot.Opacity <= 0.01)
            {
                Hide();
            }
        };
        BubbleRoot.BeginAnimation(OpacityProperty, animation);
    }
}
