using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FormsScreen = System.Windows.Forms.Screen;

namespace Clipalka.App;

public partial class CaptureNotificationWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint WdaExcludeFromCapture = 0x00000011;
    private bool _isDismissing;

    public CaptureNotificationWindow(
        Services.CaptureNotificationKind kind,
        string title,
        string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ApplyKind(kind);
        SourceInitialized += Window_SourceInitialized;
        Loaded += Window_Loaded;
    }

    public void Dismiss()
    {
        if (_isDismissing)
        {
            return;
        }

        _isDismissing = true;
        Close();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new nint(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
        SetWindowDisplayAffinity(handle, WdaExcludeFromCapture);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionOnActiveScreen();
        var targetLeft = Left;
        Left += 28;
        Opacity = 0;
        BeginAnimation(LeftProperty, new DoubleAnimation(Left, targetLeft, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

        await Task.Delay(2800);
        if (_isDismissing)
        {
            return;
        }

        _isDismissing = true;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private void PositionOnActiveScreen()
    {
        var foreground = GetForegroundWindow();
        var screen = FormsScreen.FromHandle(foreground);
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = fromDevice.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = fromDevice.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        Left = bottomRight.X - ActualWidth - 22;
        Top = topLeft.Y + 22;
    }

    private void ApplyKind(Services.CaptureNotificationKind kind)
    {
        var (color, icon) = kind switch
        {
            Services.CaptureNotificationKind.Recording => ("#F04461", "●"),
            Services.CaptureNotificationKind.Replay => ("#7868FF", "↻"),
            Services.CaptureNotificationKind.Success => ("#45D483", "✓"),
            Services.CaptureNotificationKind.Microphone => ("#51B5FF", "●"),
            Services.CaptureNotificationKind.Error => ("#F04461", "!"),
            _ => ("#5865F2", "✓")
        };
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        AccentBar.Background = brush;
        StatusDot.Fill = brush;
        IconText.Foreground = brush;
        IconText.Text = icon;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint window, int index, nint newLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
}
