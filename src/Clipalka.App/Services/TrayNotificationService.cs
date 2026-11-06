using System.Drawing;
using System.Windows.Forms;

namespace Clipalka.App.Services;

public sealed class TrayNotificationService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly TrayMenu _menu;

    public TrayNotificationService()
    {
        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? (Icon)SystemIcons.Application.Clone();

        _menu = new TrayMenu(() => OpenRequested?.Invoke(), () => ToggleRecordingRequested?.Invoke(),
            () => SaveReplayRequested?.Invoke(), () => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "CLIPALKA — запись экрана",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public event Action? OpenRequested;
    public event Action? ToggleRecordingRequested;
    public event Action? SaveReplayRequested;
    public event Action? ExitRequested;

    public void ShowInfo(string title, string message)
    {
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    public void UpdateState(bool recording, bool replayReady, bool busy, string recordHotkey, string replayHotkey)
        => _menu.UpdateState(recording, replayReady, busy, recordHotkey, replayHotkey);
}
