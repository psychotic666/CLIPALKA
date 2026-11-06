using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clipalka.App.Controls;

public sealed class AudioLevelMeter : Control
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsMutedProperty = DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsAvailableProperty = DependencyProperty.Register(nameof(IsAvailable), typeof(bool), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public bool IsMuted { get => (bool)GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }
    public bool IsAvailable { get => (bool)GetValue(IsAvailableProperty); set => SetValue(IsAvailableProperty, value); }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        const int bars = 16;
        var peak = double.IsFinite(Level) ? Math.Clamp(Level, 0, 1) : 0;
        var normalized = peak <= .001 ? 0 : Math.Clamp((20 * Math.Log10(peak) + 60) / 60, 0, 1);
        var active = IsAvailable && !IsMuted ? (int)Math.Ceiling(normalized * bars) : 0;
        var gap = ActualWidth < 75 ? 1.5 : 2;
        var width = Math.Max(1, (ActualWidth - gap * (bars - 1)) / bars);
        var height = Math.Min(18, ActualHeight);
        var inactive = (Brush)FindResource("PanelHoverBrush");
        var violet = (Brush)FindResource("AccentHoverBrush");
        var clipping = (Brush)FindResource("DangerBrush");
        drawing.PushOpacity(IsAvailable && !IsMuted ? 1 : .55);
        for (var i = 0; i < bars; i++)
            drawing.DrawRoundedRectangle(i < active ? peak >= .98 && i >= 14 ? clipping : violet : inactive, null,
                new Rect(i * (width + gap), (ActualHeight - height) / 2, width, height), 1, 1);
        drawing.Pop();
    }
}
