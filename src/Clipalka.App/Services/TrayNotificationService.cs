using System.Drawing;
using System.Windows.Forms;

namespace Clipalka.App.Services;

public sealed class TrayNotificationService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly Font _menuFont = new("Segoe UI", 10);

    public TrayNotificationService()
    {
        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                ?? (Icon)SystemIcons.Application.Clone();

        var menu = new ContextMenuStrip
        {
            Font = _menuFont,
            ShowImageMargin = false,
            Padding = new Padding(6),
            BackColor = StudioMenuColors.Get("PanelBrush"),
            ForeColor = StudioMenuColors.Get("TextBrush"),
            Renderer = SystemInformation.HighContrast
                ? new ToolStripSystemRenderer()
                : new ToolStripProfessionalRenderer(new StudioMenuColors())
        };
        if (SystemInformation.HighContrast)
        {
            menu.BackColor = SystemColors.Menu;
            menu.ForeColor = SystemColors.MenuText;
        }
        menu.Items.Add("Открыть CLIPALKA", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Начать / остановить запись", null, (_, _) => ToggleRecordingRequested?.Invoke());
        menu.Items.Add("Сохранить последние 30 секунд", null, (_, _) => SaveReplayRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выйти", null, (_, _) => ExitRequested?.Invoke());
        foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
            item.Padding = new Padding(12, 8, 12, 8);

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
        _menuFont.Dispose();
    }

    private sealed class StudioMenuColors : ProfessionalColorTable
    {
        public StudioMenuColors() => UseSystemColors = false;
        public static Color Get(string key)
        {
            var color = ((System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color;
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public override Color ToolStripDropDownBackground => Get("PanelBrush");
        public override Color MenuBorder => Get("BorderBrush");
        public override Color MenuItemBorder => Get("BorderBrush");
        public override Color MenuItemSelected => Get("PanelHoverBrush");
        public override Color MenuItemSelectedGradientBegin => Get("PanelHoverBrush");
        public override Color MenuItemSelectedGradientEnd => Get("PanelHoverBrush");
        public override Color MenuItemPressedGradientBegin => Get("PanelHoverBrush");
        public override Color MenuItemPressedGradientEnd => Get("PanelHoverBrush");
        public override Color SeparatorDark => Get("BorderBrush");
        public override Color SeparatorLight => Get("PanelBrush");
    }
}
