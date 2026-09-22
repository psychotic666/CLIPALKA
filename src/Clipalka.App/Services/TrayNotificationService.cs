using System.Drawing;
using System.Windows.Forms;

namespace Clipalka.App.Services;

public sealed class TrayNotificationService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    public TrayNotificationService()
    {
        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? (Icon)SystemIcons.Application.Clone();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть CLIPALKA", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Начать / остановить запись", null, (_, _) => ToggleRecordingRequested?.Invoke());
        menu.Items.Add("Сохранить последние 30 секунд", null, (_, _) => SaveReplayRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "CLIPALKA — запись экрана",
            ContextMenuStrip = menu,
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
}
