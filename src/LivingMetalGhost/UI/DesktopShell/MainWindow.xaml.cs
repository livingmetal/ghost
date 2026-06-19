using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LivingMetalGhost.AppCore.Desktop;
using LivingMetalGhost.AppCore.ModeCoordination;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.UI.ViewModels;
using LivingMetalGhost.UI.Views;

namespace LivingMetalGhost;

public partial class MainWindow : Window
{
    private Point _mouseDownPosition;
    private bool _isDragging;
    private ChatWindow? _chatWindow;
    private AdvancedWorkbenchWindow? _advancedWorkbenchWindow;
    private StoryWindow? _storyWindow;
    private MainViewModel? _subscribedViewModel;
    private readonly DispatcherTimer _proactiveChatTimer;
    private readonly ProactivePresenceScheduler _presenceScheduler = new();
    private double _resizeStartScale;
    private double _resizeDragDistance;
    private double _resizeMaximumScale = 2.0;

    public TrayIconService? TrayIconService { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += MainWindow_OnDataContextChanged;
        LocationChanged += (_, _) => PositionCompanionWindows();
        CharacterSurface.PreviewMouseLeftButtonDown += CharacterSurface_OnMouseLeftButtonDown;
        CharacterSurface.PreviewMouseMove += CharacterSurface_OnMouseMove;
        CharacterSurface.PreviewMouseLeftButtonUp += CharacterSurface_OnMouseLeftButtonUp;

        _proactiveChatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _proactiveChatTimer.Tick += ProactiveChatTimer_OnTick;
        _proactiveChatTimer.Start();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeToViewModel(DataContext as MainViewModel);
        SyncModeMenuItems();
        SyncAdvancedWorkbenchVisibility();
        SyncStoryWindowVisibility();
        ResetPosition();
    }

    private void MainWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToViewModel(e.NewValue as MainViewModel);
        SyncModeMenuItems();
        SyncAdvancedWorkbenchVisibility();
        SyncStoryWindowVisibility();
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

        SyncModeMenuItems();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ProactiveSettingsRevision))
        {
            _presenceScheduler.ResetDailySchedule();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.IsAdvancedMode)
            or nameof(MainViewModel.IsStoryMode)
            or nameof(MainViewModel.IsAdvancedModeAvailable))
        {
            SyncModeMenuItems();
            if (e.PropertyName == nameof(MainViewModel.IsAdvancedMode))
            {
                SyncAdvancedWorkbenchVisibility();
            }

            if (e.PropertyName is nameof(MainViewModel.IsStoryMode) or nameof(MainViewModel.IsAdvancedMode))
            {
                SyncStoryWindowVisibility();
            }
        }
    }

    private void SyncModeMenuItems()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        var advanced = _subscribedViewModel.IsAdvancedMode;
        StoryModeMenuItem.IsChecked = ConversationModeCoordinator.IsRoleplayActive(
            _subscribedViewModel.IsStoryMode,
            advanced);
        StoryModeMenuItem.IsEnabled = !advanced;
        StoryModeMenuItem.ToolTip = advanced
            ? "고급 모드 중에는 롤플레잉 모드가 일시 중지됩니다. 고급 모드를 끄면 이전 상태로 돌아갑니다."
            : "AI소설/미연시/ORPG식 허구 장면 진행 모드";

        AdvancedModeMenuItem.IsChecked = advanced;
        AdvancedModeMenuItem.IsEnabled = _subscribedViewModel.IsAdvancedModeAvailable;
        AdvancedModeMenuItem.ToolTip = _subscribedViewModel.IsAdvancedModeAvailable
            ? "고급 작업/검토 모드"
            : "현재 고급 모드 provider를 사용할 수 없습니다.";
    }

    private void CharacterSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ResizeCharacterMenuItem.IsChecked)
        {
            return;
        }

        _mouseDownPosition = e.GetPosition(this);
        _isDragging = false;
        CharacterSurface.CaptureMouse();
    }

    private void CharacterSurface_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ResizeCharacterMenuItem.IsChecked)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || !CharacterSurface.IsMouseCaptured)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var movedFarEnough =
            Math.Abs(currentPosition.X - _mouseDownPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPosition.Y - _mouseDownPosition.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough)
        {
            return;
        }

        _isDragging = true;
        CharacterSurface.ReleaseMouseCapture();
        DragMove();
    }

    private void CharacterSurface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ResizeCharacterMenuItem.IsChecked)
        {
            return;
        }

        CharacterSurface.ReleaseMouseCapture();
        if (!_isDragging)
        {
            ShowDailyChatWindow();
        }
    }

    private void ShowDailyChatWindow()
    {
        if (ViewModel.IsAdvancedMode)
        {
            OpenAdvancedWorkbench();
            return;
        }

        EnsureChatWindow();
        PositionChatWindow();
        _chatWindow!.ShowConsole();
    }

    public void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = AlwaysOnTopMenuItem.IsChecked;
    }

    public void OpenChatFromTray()
    {
        RestoreFromTray();
        if (ViewModel.IsAdvancedMode)
        {
            OpenAdvancedWorkbench();
            return;
        }

        EnsureChatWindow();
        PositionChatWindow();
        _chatWindow!.ShowConsole();
    }

    public void OpenSettingsFromTray()
    {
        RestoreFromTray();
        OpenSettings();
    }

    private void EnsureChatWindow()
    {
        if (_chatWindow is not null)
        {
            return;
        }

        _chatWindow = new ChatWindow
        {
            DataContext = ViewModel,
            Owner = this,
            Topmost = Topmost
        };
        _chatWindow.Closing += ChatWindow_OnClosing;
    }

    private void EnsureAdvancedWorkbenchWindow()
    {
        if (_advancedWorkbenchWindow is not null)
        {
            return;
        }

        _advancedWorkbenchWindow = new AdvancedWorkbenchWindow
        {
            DataContext = ViewModel,
            Owner = this,
            Topmost = Topmost
        };
        _advancedWorkbenchWindow.Closing += AdvancedWorkbenchWindow_OnClosing;
    }

    private void ChatWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        _chatWindow?.Hide();
    }

    private void EnsureStoryWindow()
    {
        if (_storyWindow is not null)
        {
            return;
        }

        _storyWindow = new StoryWindow
        {
            DataContext = ViewModel,
            Owner = this,
            Topmost = Topmost
        };
        _storyWindow.Closing += StoryWindow_OnClosing;
    }

    private void StoryWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        var viewModel = _subscribedViewModel;
        if (viewModel is null)
        {
            _storyWindow?.Hide();
            return;
        }

        if (ConversationModeCoordinator.IsRoleplayActive(
                viewModel.IsStoryMode,
                viewModel.IsAdvancedMode))
        {
            viewModel.SetStoryMode(false);
        }
        else
        {
            _storyWindow?.Hide();
        }
    }

    private void PositionStoryWindow()
    {
        if (_storyWindow is null)
        {
            return;
        }

        ApplyPosition(
            _storyWindow,
            CompanionWindowPlacement.Center(
                GetWorkArea(),
                GetSize(_storyWindow)));
    }

    private void SyncStoryWindowVisibility()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        if (ConversationModeCoordinator.IsRoleplayActive(
                _subscribedViewModel.IsStoryMode,
                _subscribedViewModel.IsAdvancedMode))
        {
            _chatWindow?.Hide();
            EnsureStoryWindow();
            PositionStoryWindow();
            _storyWindow!.ShowConsole();
        }
        else
        {
            _storyWindow?.Hide();
        }
    }

    private void AdvancedWorkbenchWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (Application.Current.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        e.Cancel = true;
        _advancedWorkbenchWindow?.Hide();
    }

    private void PositionChatWindow()
    {
        if (_chatWindow is null)
        {
            return;
        }

        var companionSize = GetSize(this);
        ApplyPosition(
            _chatWindow,
            CompanionWindowPlacement.PositionChat(
                GetWorkArea(),
                new DesktopBounds(
                    Left,
                    Top,
                    companionSize.Width,
                    companionSize.Height),
                GetSize(_chatWindow)));
    }

    private void PositionAdvancedWorkbenchWindow()
    {
        if (_advancedWorkbenchWindow is null)
        {
            return;
        }

        ApplyPosition(
            _advancedWorkbenchWindow,
            CompanionWindowPlacement.Center(
                GetWorkArea(),
                GetSize(_advancedWorkbenchWindow)));
    }

    private void PositionCompanionWindows()
    {
        PositionChatWindow();
        PositionAdvancedWorkbenchWindow();
        PositionStoryWindow();
    }

    private void SyncAdvancedWorkbenchVisibility()
    {
        if (_subscribedViewModel is null)
        {
            return;
        }

        if (_subscribedViewModel.IsAdvancedMode)
        {
            _chatWindow?.Hide();
            _storyWindow?.Hide();
            OpenAdvancedWorkbench();
        }
        else
        {
            _advancedWorkbenchWindow?.Hide();
        }
    }

    private void OpenAdvancedWorkbench()
    {
        EnsureAdvancedWorkbenchWindow();
        PositionAdvancedWorkbenchWindow();
        _advancedWorkbenchWindow!.ShowWorkbench();
    }

    private void ResetPosition()
    {
        ApplyPosition(
            this,
            CompanionWindowPlacement.BottomRight(
                GetWorkArea(),
                GetSize(this),
                margin: 24));
        PositionCompanionWindows();
    }

    private async void ProactiveChatTimer_OnTick(object? sender, EventArgs e)
    {
        var settings = ViewModel.GetProactiveChatSettings();
        var action = _presenceScheduler.Tick(
            settings.Enabled,
            ViewModel.IsAdvancedMode,
            ViewModel.IsStoryMode,
            settings.MinMinutes,
            settings.MaxMinutes,
            DateTimeOffset.Now);

        switch (action)
        {
            case ProactivePresenceAction.StartDailyConversation:
                await ViewModel.StartConversationAsync();
                break;
            case ProactivePresenceAction.StartStoryIdle:
                await ViewModel.StartStoryIdleAsync();
                break;
        }
    }

    private void OpenChatMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsAdvancedMode)
        {
            OpenAdvancedWorkbench();
            return;
        }

        EnsureChatWindow();
        if (!_chatWindow!.IsVisible)
        {
            PositionChatWindow();
            _chatWindow.ShowConsole();
        }
        else
        {
            _chatWindow.Activate();
            _chatWindow.FocusPrompt();
        }
    }

    private void StoryModeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsAdvancedMode)
        {
            SyncModeMenuItems();
            return;
        }

        ViewModel.SetStoryMode(StoryModeMenuItem.IsChecked);
        SyncModeMenuItems();
        SyncStoryWindowVisibility();
    }

    private void AdvancedModeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetAdvancedMode(AdvancedModeMenuItem.IsChecked);
        SyncModeMenuItems();
        SyncAdvancedWorkbenchVisibility();
        SyncStoryWindowVisibility();
    }

    private void RoleplayStateMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            ViewModel.GetRoleplayStateSummary(),
            "롤플레잉 상태",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ResetRoleplayStateMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "롤플레잉 장면 상태와 턴 기억을 초기화할까요?",
            "롤플레잉 상태 초기화",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        ViewModel.ResetRoleplayState();
        SyncModeMenuItems();
        MessageBox.Show(
            "롤플레잉 상태를 초기화했어요.",
            "롤플레잉 상태 초기화",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void ResizeCharacterMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var visibility = ResizeCharacterMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        ResizeAdorner.Visibility = visibility;
        ResizeThumb.Visibility = visibility;
        CharacterSurface.Cursor = ResizeCharacterMenuItem.IsChecked ? Cursors.Arrow : Cursors.Hand;
    }

    private void ResizeThumb_OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _resizeStartScale = ViewModel.CharacterScale;
        _resizeDragDistance = 0;
        var workArea = SystemParameters.WorkArea;
        var widthLimit = ActualWidth > 0
            ? _resizeStartScale * ((workArea.Width - 24) / ActualWidth)
            : 2.0;
        var heightLimit = ActualHeight > 0
            ? _resizeStartScale * ((workArea.Height - 24) / ActualHeight)
            : 2.0;
        _resizeMaximumScale = Math.Max(0.55, Math.Min(2.0, Math.Min(widthLimit, heightLimit)));
    }

    private void ResizeThumb_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        _resizeDragDistance += (e.HorizontalChange + e.VerticalChange) / 2;
        ViewModel.CharacterScale = Math.Clamp(
            _resizeStartScale + (_resizeDragDistance / 300),
            0.55,
            _resizeMaximumScale);
        Dispatcher.BeginInvoke(KeepOnScreen, DispatcherPriority.Loaded);
    }

    private void ResizeThumb_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        KeepOnScreen();
        ViewModel.SaveCharacterScale();
        ResizeCharacterMenuItem.IsChecked = false;
        ResizeAdorner.Visibility = Visibility.Collapsed;
        ResizeThumb.Visibility = Visibility.Collapsed;
        CharacterSurface.Cursor = Cursors.Hand;
    }

    private void KeepOnScreen()
    {
        ApplyPosition(
            this,
            CompanionWindowPlacement.Clamp(
                GetWorkArea(),
                new DesktopPosition(Left, Top),
                new DesktopSize(ActualWidth, ActualHeight)));
        PositionCompanionWindows();
    }

    private static DesktopBounds GetWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        return new DesktopBounds(
            workArea.Left,
            workArea.Top,
            workArea.Width,
            workArea.Height);
    }

    private static DesktopSize GetSize(Window window)
    {
        var width = double.IsNaN(window.Width) || window.Width <= 0
            ? window.ActualWidth
            : window.Width;
        var height = double.IsNaN(window.Height) || window.Height <= 0
            ? window.ActualHeight
            : window.Height;
        return new DesktopSize(width, height);
    }

    private static void ApplyPosition(
        Window window,
        DesktopPosition position)
    {
        window.Left = position.Left;
        window.Top = position.Top;
    }

    private void OpenSettings()
    {
        if (ViewModel.OpenSettingsCommand.CanExecute(null))
        {
            ViewModel.OpenSettingsCommand.Execute(null);
        }
    }

    private void HideToTrayMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        _chatWindow?.Hide();
        _advancedWorkbenchWindow?.Hide();
        _storyWindow?.Hide();
        Hide();
        TrayIconService?.ShowHiddenNotification();
    }

    private void ConversationLogMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenConversationLog();
    }

    private void AlwaysOnTopMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Topmost = AlwaysOnTopMenuItem.IsChecked;
        if (_chatWindow is not null)
        {
            _chatWindow.Topmost = Topmost;
        }

        if (_advancedWorkbenchWindow is not null)
        {
            _advancedWorkbenchWindow.Topmost = Topmost;
        }

        if (_storyWindow is not null)
        {
            _storyWindow.Topmost = Topmost;
        }
    }

    private void ResetPositionMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        ResetPosition();
    }

    private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            $"LivingMetalGhost\n{ViewModel.CharacterDisplayName} desktop assistant",
            "정보",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
