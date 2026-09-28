using System.Runtime.InteropServices;
using Clipalka.Core.Models;
using Clipalka.Core.Services;

namespace Clipalka.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private readonly Dictionary<int, HotkeyRegistration> _actions = new();
    private readonly HashSet<int> _pressedKeys = [];
    private readonly LowLevelKeyboardProcedure _keyboardProcedure;
    private nint _hookHandle;

    public GlobalHotkeyService()
    {
        _keyboardProcedure = KeyboardProcedure;
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _keyboardProcedure, GetModuleHandle(null), 0);
        if (_hookHandle == nint.Zero)
        {
            throw new InvalidOperationException(
                $"Не удалось подключить клавиатурные хоткеи (Windows error {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Register(int id, HotkeyBinding? binding, Action action, Func<bool>? canExecute = null)
    {
        Unregister(id);
        if (binding is not null)
        {
            _actions[id] = new HotkeyRegistration(binding, action, canExecute);
        }
    }

    public void Unregister(int id) => _actions.Remove(id);

    public void Dispose()
    {
        _actions.Clear();
        _pressedKeys.Clear();
        if (_hookHandle != nint.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = nint.Zero;
        }
    }

    private nint KeyboardProcedure(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var virtualKey = Marshal.ReadInt32(lParam);
            if (message is WmKeyUp or WmSysKeyUp)
            {
                _pressedKeys.Remove(virtualKey);
            }
            else if (message is WmKeyDown or WmSysKeyDown && _pressedKeys.Add(virtualKey))
            {
                var pressedModifiers = ReadPressedModifiers();
                foreach (var registration in _actions.Values.ToArray())
                {
                    if (HotkeyTriggerMatcher.IsMatch(registration.Binding, virtualKey, pressedModifiers) &&
                        registration.CanExecute?.Invoke() != false)
                    {
                        registration.Action();
                    }
                }
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private static HotkeyModifiers ReadPressedModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if (IsPressed(VkControl)) modifiers |= HotkeyModifiers.Control;
        if (IsPressed(VkMenu)) modifiers |= HotkeyModifiers.Alt;
        if (IsPressed(VkShift)) modifiers |= HotkeyModifiers.Shift;
        if (IsPressed(VkLWin) || IsPressed(VkRWin)) modifiers |= HotkeyModifiers.Windows;
        return modifiers;
    }

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private delegate nint LowLevelKeyboardProcedure(int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProcedure procedure,
        nint moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);

    private sealed record HotkeyRegistration(
        HotkeyBinding Binding,
        Action Action,
        Func<bool>? CanExecute);
}
