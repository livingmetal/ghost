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
    private ChatMessage? _selectedMemoryCandidate;
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
            RefreshSelectedMemoryCandidateText();
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
        RefreshSelectedMemoryCandidateText();
        Activate();
        FocusPrompt();
    }

    private void AdvancedWorkbenchWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToMessages(e.NewValue as MainViewModel);
        RefreshContextText();
        RefreshSelectedMemoryCandidateText();
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

        _selectedMemoryCandidate = null;
        RefreshSelectedMemoryCandidateText();
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
                if (ReferenceEquals(_selectedMemoryCandidate, item))
                {
                    _selectedMemoryCandidate = null;
                }
            }
        }

        RefreshContextText();
        RefreshSelectedMemoryCandidateText();
        ScrollToLatestMessage();
    }

    private void Message_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.DisplayText))
        {
            RefreshSelectedMemoryCandidateText();
            ScrollToLatestMessage();
        }
    }

    private void RefreshContextText()
    {
        WorkbenchContextText.Text = _advancedSessionLogService.BuildWorkbenchContextText();
    }

    private void RefreshSelectedMemoryCandidateText()
    {
        if (_selectedMemoryCandidate is null || string.IsNullOrWhiteSpace(_selectedMemoryCandidate.Text))
        {
            SelectedMemoryCandidateText.Text = "선택된 기억 후보: 없음";
            return;
        }

        var text = _selectedMemoryCandidate.Text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (text.Length > 70)
        {
            text = text[..70] + "…";
        }

        SelectedMemoryCandidateText.Text = $"선택된 기억 후보: {text}";
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

    private async void MessageBubble_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ChatMessage message)
        {
            return;
        }

        if (message.IsUser || message.IsTyping || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        _selectedMemoryCandidate = message;
        RefreshSelectedMemoryCandidateText();

        if (e.ClickCount < 2)
        {
            return;
        }

        e.Handled = true;
        await SaveMemoryCandidateAsync(message.Text);
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
        _selectedMemoryCandidate = null;
        RefreshContextText();
        RefreshSelectedMemoryCandidateText();
    }

    private void WorkspaceSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new WorkspaceSettingsWindow
        {
            Owner = this
        };
        window.ShowDialog();
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
        var window = new ProjectMemoryManagerWindow
        {
            Owner = this
        };
        window.ShowDialog();
        RefreshContextText();
    }

    private async void PromoteMemoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        var candidateText = _selectedMemoryCandidate is { IsUser: false, IsTyping: false } &&
                            !string.IsNullOrWhiteSpace(_selectedMemoryCandidate.Text)
            ? _selectedMemoryCandidate.Text
            : _subscribedViewModel.GetLastCompletedAssistantMessageText();
        await SaveMemoryCandidateAsync(candidateText);
    }

    private async void ApproveAgentJobButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null || sender is not FrameworkElement element ||
            element.DataContext is not AgentJob job)
        {
            return;
        }

        await _subscribedViewModel.ApproveAgentJobAsync(job, CancellationToken.None);
        RefreshContextText();
    }

    private void RejectAgentJobButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_subscribedViewModel is null || sender is not FrameworkElement element ||
            element.DataContext is not AgentJob job)
        {
            return;
        }

        _subscribedViewModel.RejectAgentJob(job);
        RefreshContextText();
    }

    private async Task SaveMemoryCandidateAsync(string? candidateText)
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(candidateText))
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
            candidateText)
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
        RefreshSelectedMemoryCandidateText();
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
