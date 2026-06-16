using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LivingMetalGhost.UI.Views;

public partial class AdvancedWorkbenchWindow : Window
{
    private MainViewModel? _subscribedViewModel;
    private readonly AdvancedSessionLogService _advancedSessionLogService;

    public AdvancedWorkbenchWindow()
    {
        InitializeComponent();
        _advancedSessionLogService = global::LivingMetalGhost.App.Services.GetRequiredService<AdvancedSessionLogService>();
        DataContextChanged += AdvancedWorkbenchWindow_OnDataContextChanged;
        Loaded += (_, _) =>
        {
            SubscribeToMessages(DataContext as MainViewModel);
            RefreshContextText();
        };
    }

    public void FocusPrompt()
    {
        PromptTextBox.Focus();
        Keyboard.Focus(PromptTextBox);
    }

    public void ShowWorkbench()
    {
        if (!IsVisible)
        {
            Show();
        }

        RefreshContextText();
        Activate();
        FocusPrompt();
    }

    private void AdvancedWorkbenchWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToMessages(e.NewValue as MainViewModel);
        RefreshContextText();
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
        if (e.PropertyName is nameof(MainViewModel.AdvancedContextRevision)
            or nameof(MainViewModel.ActiveProviderLabel))
        {
            RefreshContextText();
        }
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

        RefreshContextText();
        ScrollToLatestMessage();
    }

    private void Message_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.DisplayText))
        {
            ScrollToLatestMessage();
        }
    }

    private void RefreshContextText()
    {
        WorkbenchContextText.Text = _advancedSessionLogService.BuildWorkbenchContextText();
    }

    private void ScrollToLatestMessage()
    {
        Dispatcher.BeginInvoke(ConversationScrollViewer.ScrollToEnd);
    }

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ExitAdvancedMode();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ExitAdvancedButton_OnClick(object sender, RoutedEventArgs e)
    {
        ExitAdvancedMode();
    }

    private void ExitAdvancedMode()
    {
        _subscribedViewModel?.SetAdvancedMode(false);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void NewAdvancedSessionButton_OnClick(object sender, RoutedEventArgs e)
    {
        _subscribedViewModel?.StartNewAdvancedSession();
        RefreshContextText();
    }

    private async void GenerateSummaryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        var summaryPath = await _subscribedViewModel.GenerateCurrentAdvancedSessionSummaryAsync(CancellationToken.None);
        MessageBox.Show(
            $"현재 고급 세션 요약을 저장했어요.\n\n{summaryPath}",
            "세션 요약",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        RefreshContextText();
    }

    private void ProjectMemoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        MessageBox.Show(
            _subscribedViewModel.GetProjectMemorySummary(),
            "프로젝트 기억",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        RefreshContextText();
    }

    private async void PromoteMemoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        var lastReply = _subscribedViewModel.GetLastCompletedAssistantMessageText();
        if (string.IsNullOrWhiteSpace(lastReply))
        {
            MessageBox.Show(
                "저장할 완료된 답변을 찾지 못했어요.",
                "프로젝트 기억 저장",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var editor = new ProjectMemoryEditorWindow(
            _subscribedViewModel.GetCurrentAdvancedSessionId(),
            lastReply)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        var entry = await _subscribedViewModel.SaveProjectMemoryAsync(
            editor.MemoryContent,
            editor.MemoryType,
            CancellationToken.None);

        MessageBox.Show(
            $"프로젝트 기억으로 저장했어요.\n\n[{entry.Type}] {entry.Content}",
            "프로젝트 기억 저장",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        RefreshContextText();
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
