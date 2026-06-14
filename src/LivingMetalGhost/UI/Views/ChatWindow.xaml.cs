using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class ChatWindow : Window
{
    private static readonly Color NormalBorder  = Color.FromRgb(0x1B, 0x24, 0x30);
    private static readonly Color AdvancedBorder = Color.FromRgb(0x7B, 0x4F, 0xC8);

    private MainViewModel? _subscribedViewModel;

    public ChatWindow()
    {
        InitializeComponent();
        DataContextChanged += ChatWindow_OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SubscribeToMessages(DataContext as MainViewModel);
        };
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
        SubscribeToMessages(e.NewValue as MainViewModel);
    }

    private void SubscribeToMessages(MainViewModel? viewModel)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Messages.CollectionChanged -= Messages_OnCollectionChanged;
            _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Messages.CollectionChanged += Messages_OnCollectionChanged;
            _subscribedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged += Message_OnPropertyChanged;
            }
        }

        ScrollToLatestMessage();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAdvancedMode))
        {
            ApplyModeVisuals();
        }
    }

    private void ApplyModeVisuals()
    {
        var advanced = _subscribedViewModel?.IsAdvancedMode ?? false;
        var targetColor = advanced ? AdvancedBorder : NormalBorder;

        TitleLabel.Text = advanced ? "ADVANCED CONSOLE" : "PROMPT CONSOLE";
        SendButton.Background = new SolidColorBrush(advanced
            ? Color.FromRgb(0x7B, 0x4F, 0xC8)
            : Color.FromRgb(0xE9, 0x6A, 0x42));

        var animation = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(200));
        BorderAccent.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    private void Messages_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ChatMessage>())
            {
                item.PropertyChanged += Message_OnPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ChatMessage>())
            {
                item.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        ScrollToLatestMessage();
    }

    private void Message_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.DisplayText))
        {
            ScrollToLatestMessage();
        }
    }

    private void ScrollToLatestMessage()
    {
        Dispatcher.BeginInvoke(ConversationScrollViewer.ScrollToEnd);
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
        if (_subscribedViewModel is null)
        {
            return;
        }

        _subscribedViewModel.IsAdvancedMode = !_subscribedViewModel.IsAdvancedMode;
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
