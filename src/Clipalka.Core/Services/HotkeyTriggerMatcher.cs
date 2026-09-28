using Clipalka.Core.Models;

namespace Clipalka.Core.Services;

public static class HotkeyTriggerMatcher
{
    private const HotkeyModifiers MatchableModifiers =
        HotkeyModifiers.Control |
        HotkeyModifiers.Alt |
        HotkeyModifiers.Shift |
        HotkeyModifiers.Windows;

    public static bool IsMatch(
        HotkeyBinding binding,
        int virtualKey,
        HotkeyModifiers pressedModifiers) =>
        binding.VirtualKey == virtualKey &&
        (binding.Modifiers & MatchableModifiers) == (pressedModifiers & MatchableModifiers);
}
