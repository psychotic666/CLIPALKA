using System.Diagnostics;
using System.Runtime.InteropServices;
using Clipalka.Core.Models;
using Clipalka.Core.Services;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

public sealed record CaptureTarget(RecordingSourceBase Source, string Name, bool IsApplication);

public sealed class CaptureTargetService
{
    private static readonly HashSet<string> NonCaptureProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "clipalka", "explorer", "searchhost", "shellexperiencehost", "startmenuexperiencehost",
        "textinputhost", "applicationframehost", "systemsettings", "dwm", "lockapp",
        "chrome", "msedge", "firefox", "opera", "brave", "arc", "discord", "telegram", "spotify",
        "code", "devenv", "windowsterminal", "cmd", "powershell", "pwsh", "notepad",
        "steam", "steamwebhelper", "epicgameslauncher", "battle.net", "upc", "eadesktop"
    };

    public CaptureTarget Resolve(AppSettings settings, CapturePurpose purpose)
    {
        var window = nint.Zero;
        var name = string.Empty;
        var hasForegroundGame = purpose == CapturePurpose.ManualRecording &&
                                TryGetForegroundGameWindow(out window, out name);
        if (CaptureSelectionPolicy.ShouldCaptureForegroundWindow(
                purpose, settings.AutoCaptureGame, hasForegroundGame))
        {
            var source = new WindowRecordingSource(window)
            {
                IsBorderRequired = false,
                IsCursorCaptureEnabled = true
            };
            return new CaptureTarget(source, name, true);
        }

        var display = string.IsNullOrWhiteSpace(settings.DisplayDeviceName)
            ? DisplayRecordingSource.MainMonitor
            : new DisplayRecordingSource(settings.DisplayDeviceName);
        if (display is null)
        {
            throw new InvalidOperationException("Не найден экран для записи.");
        }

        display.RecorderApi = RecorderApi.WindowsGraphicsCapture;
        display.IsBorderRequired = false;
        display.IsCursorCaptureEnabled = true;
        var displayName = Recorder.GetDisplays()
            .FirstOrDefault(candidate => candidate.DeviceName == display.DeviceName)?.FriendlyName;
        return new CaptureTarget(display, string.IsNullOrWhiteSpace(displayName) ? "Экран" : displayName, false);
    }

    public bool IsLikelyGameActive()
    {
        var foreground = GetForegroundWindow();
        return foreground != nint.Zero && IsGameCandidate(foreground, out _, out _);
    }

    public string? GetActiveApplicationName()
    {
        var name = GetForegroundApplicationName();
        return string.Equals(name, "CLIPALKA", StringComparison.OrdinalIgnoreCase) ? null : name;
    }

    private static bool TryGetForegroundGameWindow(out nint handle, out string name)
    {
        handle = GetForegroundWindow();
        name = string.Empty;
        if (handle == nint.Zero || !IsGameCandidate(handle, out name, out _))
        {
            handle = nint.Zero;
            return false;
        }
        return true;
    }

    private static bool IsGameCandidate(nint handle, out string name, out double monitorCoverage)
    {
        name = string.Empty;
        monitorCoverage = 0;
        if (!IsWindowVisible(handle) || !GetWindowRect(handle, out var windowRect))
        {
            return false;
        }

        var width = Math.Max(0, windowRect.Right - windowRect.Left);
        var height = Math.Max(0, windowRect.Bottom - windowRect.Top);
        if (width < 800 || height < 500)
        {
            return false;
        }

        GetWindowThreadProcessId(handle, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (NonCaptureProcesses.Contains(process.ProcessName))
            {
                return false;
            }

            name = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                ? process.ProcessName
                : process.MainWindowTitle.Trim();
        }
        catch (ArgumentException)
        {
            return false;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorWidth = Math.Max(1, monitorInfo.Monitor.Right - monitorInfo.Monitor.Left);
        var monitorHeight = Math.Max(1, monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top);
        monitorCoverage = Math.Min(1d, (double)(width * height) / (monitorWidth * monitorHeight));
        return monitorCoverage >= 0.30;
    }

    private static string? GetForegroundApplicationName()
    {
        var handle = GetForegroundWindow();
        if (handle == nint.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(handle, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.MainWindowTitle)
                ? process.ProcessName
                : process.MainWindowTitle.Trim();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
