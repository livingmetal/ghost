using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.UI.Dialogs;
using LivingMetalGhost.UI.ViewModels;

namespace LivingMetalGhost.UI.Views;

public partial class StoryWindow : Window
{
    private const double MaximumResizeWidth = 1100;
    private const double MaximumResizeHeight = 950;
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

    private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var maximumWidth = Math.Min(MaximumResizeWidth, Math.Max(MinWidth, workArea.Right - Left - 12));
        var maximumHeight = Math.Min(MaximumResizeHeight, Math.Max(MinHeight, workArea.Bottom - Top - 12));

        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, maximumWidth);
        Height = Math.Clamp(Height + e.VerticalChange, MinHeight, maximumHeight);
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

    private void AttachImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var image = ImageAttachmentDialog.Select(
            this,
            "롤플레잉에 첨부할 이미지 선택");
        if (image is null)
        {
            return;
        }

        viewModel.SetSelectedImage(image, storyMode: true);
    }

    private void RemoveImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.ClearSelectedImage(storyMode: true);
    }
}
