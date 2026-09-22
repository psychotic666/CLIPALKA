using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Clipalka.Core.Models;

namespace Clipalka.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly nint _windowHandle;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();

    public GlobalHotkeyService(nint windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle)
                  ?? throw new InvalidOperationException("Не удалось подключить обработчик окна.");
        _source.AddHook(WindowProcedure);
    }

    public void Register(int id, HotkeyBinding? binding, Action action)
    {
        Unregister(id);
        if (binding is null)
        {
            return;
        }

        if (!RegisterHotKey(_windowHandle, id, (uint)binding.Modifiers, (uint)binding.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Горячая клавиша уже используется другим приложением.");
        }

        _actions[id] = action;
    }

    public void Unregister(int id)
    {
        if (_actions.Remove(id))
        {
            UnregisterHotKey(_windowHandle, id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys.ToArray())
        {
            Unregister(id);
        }

        _source.RemoveHook(WindowProcedure);
    }

    private nint WindowProcedure(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }

        return nint.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}

