using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace LivingMetalGhost.Core.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Action _showCharacter;
    private readonly Action _openChat;
    private readonly Action _openSettings;
    private readonly Action _exit;

    public TrayIconService(
        Action showCharacter,
        Action openChat,
        Action openSettings,
        Action exit)
    {
        _showCharacter = showCharacter;
        _openChat = openChat;
        _openSettings = openSettings;
        _exit = exit;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("캐릭터 표시", null, (_, _) => Dispatch(_showCharacter));
        menu.Items.Add("대화창 열기", null, (_, _) => Dispatch(_openChat));
        menu.Items.Add("설정", null, (_, _) => Dispatch(_openSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Dispatch(_exit));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "LivingMetalGhost",
            Icon = LoadApplicationIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatch(_showCharacter);
    }

    public void ShowHiddenNotification()
    {
        _notifyIcon.BalloonTipTitle = "LivingMetalGhost";
        _notifyIcon.BalloonTipText = "트레이로 숨겼습니다. 아이콘을 더블클릭하면 다시 표시됩니다.";
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Application.Current.Dispatcher.Invoke(action);
    }

    private static Icon LoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return SystemIcons.Application;
        }

        return Icon.ExtractAssociatedIcon(executablePath) ?? SystemIcons.Application;
    }
}
