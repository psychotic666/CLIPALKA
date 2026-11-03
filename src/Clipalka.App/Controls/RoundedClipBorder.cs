using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clipalka.App.Controls;

// Border.CornerRadius paints a rounded background but does not clip its image child.
public sealed class RoundedClipBorder : Border
{
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Clip = new RectangleGeometry(new Rect(sizeInfo.NewSize), CornerRadius.TopLeft, CornerRadius.TopLeft);
    }
}
