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
        Loaded += (_, _) => SubscribeToMessages(DataContext as MainViewModel);
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

    private void StoryWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToMessages(e.NewValue as MainViewModel);
    }

    private void SubscribeToMessages(MainViewModel? viewModel)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.StoryMessages.CollectionChanged -= Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.StoryMessages)
            {
                message.PropertyChanged -= Message_OnPropertyChanged;
            }
        }

        _subscribedViewModel = viewModel;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.StoryMessages.CollectionChanged += Messages_OnCollectionChanged;
            foreach (var message in _subscribedViewModel.StoryMessages)
            {
                message.PropertyChanged += Message_OnPropertyChanged;
            }
        }

        ScrollToLatestMessage();
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
        _subscribedViewModel?.SetStoryMode(false);
    }

    private void PromptTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        e.Handled = true;
        if (DataContext is MainViewModel viewModel)
        {
            var command = viewModel.StorySendCommand;
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }
}
