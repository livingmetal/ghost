using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class StoryWindow : Window
{
    private MainViewModel? _subscribedViewModel;

    public StoryWindow()
    {
        InitializeComponent();
        DataContextChanged += StoryWindow_OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SubscribeToMessages(DataContext as MainViewModel);
            UpdateTitle();
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
        UpdateTitle();
        FocusPrompt();
    }

    private void StoryWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToMessages(e.NewValue as MainViewModel);
        UpdateTitle();
    }

    private void SubscribeToMessages(MainViewModel? viewModel)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Messages.CollectionChanged -= Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.Messages.CollectionChanged += Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.Messages)
            {
                message.PropertyChanged += Message_OnPropertyChanged;
            }
        }

        ScrollToLatestMessage();
    }

    private void UpdateTitle()
    {
        StoryTitleLabel.Text = (_subscribedViewModel?.ActiveProviderLabel ?? string.Empty)
            .Replace("STORY:", "ROLEPLAY:", StringComparison.OrdinalIgnoreCase);
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
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 스토리 창 닫기는 롤플레잉을 끝내고 데스크톱 동반자로 돌아가는 것으로 처리한다.
        _subscribedViewModel?.SetStoryMode(false);
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
