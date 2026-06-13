using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.UI.ViewModels;
using LivingMetalGhost.UI.Views;

namespace LivingMetalGhost;

public partial class MainWindow : Window
{
    private Point _mouseDownPosition;
    private bool _isDragging;
    private ChatWindow? _chatWindow;
    private MainViewModel? _subscribedViewModel;
    private readonly DispatcherTimer _proactiveChatTimer;
    private DateTimeOffset? _nextProactiveChatAt;
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
        ResetPosition();
    }

    private void MainWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SubscribeToViewModel(e.NewValue as MainViewModel);
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
        if (e.PropertyName == nameof(MainViewModel.ProactiveSettingsRevision))
        {
            _nextProactiveChatAt = null;
            return;
        }

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
            ToggleChatWindow();
        }
    }

    private void ToggleChatWindow()
    {
        EnsureChatWindow();

        if (_chatWindow!.IsVisible)
        {
            _chatWindow.Hide();
            return;
        }

        PositionChatWindow();
        _chatWindow.ShowConsole();
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
            Owner = this
        };
        _chatWindow.Closing += ChatWindow_OnClosing;
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

    private void PositionChatWindow()
    {
        if (_chatWindow is null)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var preferredLeft = Left - _chatWindow.Width + 32;
        if (preferredLeft < workArea.Left)
        {
            preferredLeft = Left + Width - 32;
        }

        _chatWindow.Left = Math.Clamp(
            preferredLeft,
            workArea.Left,
            workArea.Right - _chatWindow.Width);
        _chatWindow.Top = Math.Clamp(
            Top + 55,
            workArea.Top,
            workArea.Bottom - _chatWindow.Height);
    }

    private void PositionCompanionWindows()
    {
        PositionChatWindow();
    }

    private void ResetPosition()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Bottom - Height - 24;
        PositionCompanionWindows();
    }

    private async void ProactiveChatTimer_OnTick(object? sender, EventArgs e)
    {
        var settings = ViewModel.GetProactiveChatSettings();
        if (!settings.Enabled)
        {
            _nextProactiveChatAt = null;
            return;
        }

        if (_nextProactiveChatAt is null)
        {
            ScheduleNextProactiveChat(settings.MinMinutes, settings.MaxMinutes);
            return;
        }

        if (DateTimeOffset.Now < _nextProactiveChatAt.Value)
        {
            return;
        }

        await ViewModel.StartConversationAsync();
        ScheduleNextProactiveChat(settings.MinMinutes, settings.MaxMinutes);
    }

    private void ScheduleNextProactiveChat(int minMinutes, int maxMinutes)
    {
        var delayMinutes = Random.Shared.Next(minMinutes, maxMinutes + 1);
        _nextProactiveChatAt = DateTimeOffset.Now.AddMinutes(delayMinutes);
    }

    private void OpenChatMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
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
    }

    private void KeepOnScreen()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - ActualWidth));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - ActualHeight));
        PositionCompanionWindows();
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
