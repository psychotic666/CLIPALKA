using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Clipalka.App.Services;

// Keep native tray dismissal, keyboard navigation and DPI behavior, with the
// same flat surfaces and vector vocabulary as the WPF application.
internal sealed class TrayMenu : ContextMenuStrip
{
    private readonly Font _bodyFont = new("Segoe UI", 10.5f);
    private readonly Font _brandFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _captionFont = new("Segoe UI", 9f);
    private readonly List<Image> _artwork = [];
    private readonly ToolStripLabel _status;
    private readonly ToolStripMenuItem _record, _replay;
    private readonly Image _startImage, _stopImage;

    public TrayMenu(Action open, Action record, Action replay, Action exit)
    {
        Font = _bodyFont;
        Padding = new Padding(6, 8, 6, 8);
        AutoSize = false;
        LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
        ShowImageMargin = false;
        ImageScalingSize = new Size(20, 20);
        BackColor = Colors.Get("StudioCanvasBrush");
        ForeColor = Colors.Get("TextBrush");
        DropShadowEnabled = true;
        Renderer = SystemInformation.HighContrast ? new ToolStripSystemRenderer() : new SignalRenderer();
        if (SystemInformation.HighContrast)
        { BackColor = SystemColors.Menu; ForeColor = SystemColors.MenuText; }

        Items.Add(new ToolStripLabel("CLIPALKA")
        {
            Font = _brandFont, ForeColor = SystemInformation.HighContrast ? SystemColors.MenuText : Colors.Get("AccentHoverBrush"),
            Padding = new Padding(0, 5, 12, 0), Tag = "brand", TextAlign = ContentAlignment.MiddleLeft
        });
        _status = new ToolStripLabel("Буфер выключен")
        {
            Font = _captionFont, ForeColor = SystemInformation.HighContrast ? SystemColors.MenuText : Colors.Get("MutedBrush"),
            Padding = new Padding(0, 2, 12, 7), Tag = "caption", TextAlign = ContentAlignment.MiddleLeft
        };
        Items.Add(_status);
        Items.Add(new ToolStripSeparator());
        AddCommand("Открыть CLIPALKA", MakeIcon("open"), open);
        _startImage = MakeIcon("record");
        _stopImage = MakeIcon("stop");
        _record = AddCommand("Начать запись", _startImage, record);
        _replay = AddCommand("Сохранить момент", MakeIcon("save"), replay);
        Items.Add(new ToolStripSeparator());
        AddCommand("Выйти", MakeIcon("exit"), exit);
        UpdateState(false, false, false, "", "");
    }

    private ToolStripMenuItem AddCommand(string text, Image image, Action action)
    {
        var item = new ToolStripMenuItem(text, image, (_, _) => action())
        { Padding = new Padding(10, 10, 10, 10), ImageScaling = ToolStripItemImageScaling.SizeToFit };
        Items.Add(item);
        return item;
    }

    public void UpdateState(bool recording, bool replayReady, bool busy, string recordHotkey, string replayHotkey)
    {
        _record.Text = recording ? "Остановить запись" : "Начать запись";
        _record.Image = recording ? _stopImage : _startImage;
        _record.Enabled = !busy;
        _replay.Enabled = replayReady && !busy;
        _record.ShortcutKeyDisplayString = FormatHotkey(recordHotkey);
        _replay.ShortcutKeyDisplayString = FormatHotkey(replayHotkey);
        _status.Text = recording ? "● Идёт запись" : replayReady ? "● Буфер активен" : "Буфер выключен";
        if (!SystemInformation.HighContrast)
            _status.ForeColor = Colors.Get(recording ? "DangerBrush" : replayReady ? "SuccessBrush" : "MutedBrush");
        FitToContent();
    }

    private void FitToContent()
    {
        var scale = DeviceDpi / 96f;
        var width = (int)(250 * scale);
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        foreach (var item in Items.OfType<ToolStripMenuItem>())
        {
            // Reserve the longer recording label so the popup does not jump on Stop.
            var caption = item == _record ? "Остановить запись" : item.Text;
            var textWidth = TextRenderer.MeasureText(caption, Font, Size.Empty, flags).Width;
            var hint = item.ShortcutKeyDisplayString ?? "";
            var hintWidth = hint.Length == 0 ? 0 : TextRenderer.MeasureText(hint, Font, Size.Empty, flags).Width + (int)(20 * scale);
            width = Math.Max(width, textWidth + hintWidth + (int)(58 * scale) + Padding.Horizontal);
        }
        var height = Padding.Vertical;
        foreach (ToolStripItem item in Items)
        {
            item.AutoSize = false;
            item.Margin = Padding.Empty;
            item.Size = new Size(width - Padding.Horizontal, item is ToolStripSeparator
                ? (int)(9 * scale)
                : TextRenderer.MeasureText("Ag", item.Font).Height + item.Padding.Vertical);
            if (item.Available) height += item.Height;
        }
        Size = new Size(width, height);
        PerformLayout();
    }

    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        FitToContent();
        base.OnOpening(e);
    }

    private static string FormatHotkey(string value) => string.IsNullOrWhiteSpace(value) ? "" : value.Replace("+", " + ");

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (SystemInformation.HighContrast || Width < 2 || Height < 2) return;
        using var path = Rounded(new RectangleF(0, 0, Width, Height), 7 * DeviceDpi / 96f);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    private Image MakeIcon(string kind)
    {
        var bitmap = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(40f / 24, 40f / 24);
        var color = SystemInformation.HighContrast ? SystemColors.MenuText : Colors.Get(kind is "save" ? "AccentHoverBrush" : kind is "stop" ? "DangerBrush" : "TextBrush");
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (kind)
        {
            case "open":
                graphics.DrawLines(pen, [new PointF(3, 8), new(3, 3), new(8, 3)]);
                graphics.DrawLines(pen, [new PointF(16, 3), new(21, 3), new(21, 8)]);
                graphics.DrawLines(pen, [new PointF(21, 16), new(21, 21), new(16, 21)]);
                graphics.DrawLines(pen, [new PointF(8, 21), new(3, 21), new(3, 16)]);
                break;
            case "record": graphics.DrawEllipse(pen, 4, 4, 16, 16); break;
            case "stop":
                using (var brush = new SolidBrush(color))
                using (var shape = Rounded(new RectangleF(5, 5, 14, 14), 2)) graphics.FillPath(brush, shape);
                break;
            case "save":
                graphics.DrawLine(pen, 12, 3, 12, 15);
                graphics.DrawLines(pen, [new PointF(7, 10), new(12, 15), new(17, 10)]);
                graphics.DrawLines(pen, [new PointF(4, 16), new(4, 21), new(20, 21), new(20, 16)]);
                break;
            case "exit":
                graphics.DrawLines(pen, [new PointF(10, 4), new(4, 4), new(4, 20), new(10, 20)]);
                graphics.DrawLine(pen, 10, 12, 21, 12);
                graphics.DrawLines(pen, [new PointF(16, 7), new(21, 12), new(16, 17)]);
                break;
        }
        _artwork.Add(bitmap);
        return bitmap;
    }

    private static GraphicsPath Rounded(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class SignalRenderer : ToolStripProfessionalRenderer
    {
        public SignalRenderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Colors.Get("StudioCanvasBrush"));
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var width = ItemWidth(e.Item);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Rounded(new RectangleF(2, 1, width - 4, e.Item.Height - 2), 4);
            using var brush = new SolidBrush(Colors.Get("SelectedSurfaceBrush"));
            e.Graphics.FillPath(brush, path);
            using var accent = new Pen(Colors.Get("AccentHoverBrush"), 2);
            e.Graphics.DrawLine(accent, 3, 10, 3, e.Item.Height - 10);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is not ToolStripMenuItem item)
            {
                var labelScale = (e.Item.Owner?.DeviceDpi ?? 96) / 96f;
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont,
                    new Rectangle((int)(42 * labelScale), 0, Math.Max(1, ItemWidth(e.Item) - (int)(54 * labelScale)), e.Item.Height),
                    e.Item.ForeColor, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }
            // Draw both columns together; the native menu otherwise reserves an
            // empty submenu-arrow column and scatters the shortcut positions.
            if (e.Text != item.Text) return;
            var scale = (item.Owner?.DeviceDpi ?? 96) / 96f;
            if (item.Image is { } image)
            {
                var iconBounds = new Rectangle((int)(10 * scale), (item.Height - (int)(20 * scale)) / 2, (int)(20 * scale), (int)(20 * scale));
                if (item.Enabled) e.Graphics.DrawImage(image, iconBounds);
                else
                {
                    using var scaled = new Bitmap(image, iconBounds.Size);
                    ControlPaint.DrawImageDisabled(e.Graphics, scaled, iconBounds.X, iconBounds.Y, Colors.Get("StudioCanvasBrush"));
                }
            }
            var left = (int)(42 * scale);
            var right = ItemWidth(e.Item) - (int)(12 * scale);
            var hint = item.ShortcutKeyDisplayString ?? "";
            var hintWidth = hint.Length == 0 ? 0 : TextRenderer.MeasureText(hint, e.TextFont,
                Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            var color = Colors.Get(!item.Enabled ? "SubtleBrush" : item.Selected ? "FocusBrush" : "TextBrush");
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, item.Text, e.TextFont,
                new Rectangle(left, 0, Math.Max(1, right - left - hintWidth - (hintWidth > 0 ? (int)(20 * scale) : 0)), item.Height),
                color, flags | TextFormatFlags.EndEllipsis);
            if (hintWidth > 0)
                TextRenderer.DrawText(e.Graphics, hint, e.TextFont,
                    new Rectangle(right - hintWidth, 0, hintWidth, item.Height),
                    Colors.Get(!item.Enabled ? "SubtleBrush" : item.Selected ? "FocusBrush" : "MutedBrush"), flags | TextFormatFlags.Right);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            // Icons are rendered with the text, without a native image/arrow gutter.
        }
        private static int ItemWidth(ToolStripItem item) => item.Owner is { } owner
            ? owner.ClientSize.Width - item.Bounds.X - owner.Padding.Right
            : item.Width;
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Colors.Get("BorderBrush"));
            e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Colors.Get("BorderBrush"));
            using var path = Rounded(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 7);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class Colors : ProfessionalColorTable
    {
        public Colors() => UseSystemColors = false;
        public static Color Get(string key)
        {
            var color = ((System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.FindResource(key)).Color;
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        public override Color ToolStripDropDownBackground => Get("StudioCanvasBrush");
        public override Color ImageMarginGradientBegin => Get("StudioCanvasBrush");
        public override Color ImageMarginGradientMiddle => Get("StudioCanvasBrush");
        public override Color ImageMarginGradientEnd => Get("StudioCanvasBrush");
    }

    internal static void RenderSamples(string directory)
    {
        var calls = new int[4];
        using var menu = new TrayMenu(() => calls[0]++, () => calls[1]++, () => calls[2]++, () => calls[3]++);
        foreach (var (name, recording, ready, busy, selection) in new[]
        {
            ("tray-ready", false, true, false, 5),
            ("tray-recording", true, true, false, 4),
            ("tray-buffer-off", false, false, false, 3),
            ("tray-busy", false, true, true, 3)
        })
        {
            menu.UpdateState(recording, ready, busy, "Ctrl+Shift+R", "Shift+Z");
            menu.Show(new Point(12, 12));
            menu.Items[selection].Select();
            System.Windows.Forms.Application.DoEvents();
            using var bitmap = new Bitmap(menu.Width, menu.Height);
            menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
            bitmap.Save(Path.Combine(directory, name + ".png"), ImageFormat.Png);
            menu.Close();
        }
        menu.UpdateState(false, true, false, "", "");
        foreach (var item in menu.Items.OfType<ToolStripMenuItem>()) item.PerformClick();
        if (calls.Any(count => count != 1)) throw new InvalidOperationException("Tray commands must dispatch exactly once.");
        menu.UpdateState(false, false, true, "", "");
        if (menu._record.Enabled || menu._replay.Enabled || !string.IsNullOrEmpty(menu._record.ShortcutKeyDisplayString))
            throw new InvalidOperationException("Tray availability or disabled shortcuts do not match application state.");
        File.WriteAllText(Path.Combine(directory, "tray-verification.txt"), "PASS: four tray commands dispatch once; recording, replay off, busy and disabled-hotkey states rendered with the production menu.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        foreach (var image in _artwork) image.Dispose();
        _bodyFont.Dispose(); _brandFont.Dispose(); _captionFont.Dispose();
    }
}
