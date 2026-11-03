using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clipalka.App.Services;

internal static class WindowAppearance
{
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var dark = 1;
        var rounded = 2;
        // Unsupported attributes simply return an error on older Windows versions.
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
