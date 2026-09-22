using Clipalka.Core.Models;

namespace Clipalka.Core.Tests;

public sealed class HotkeyBindingTests
{
    [Theory]
    [InlineData("Shift+Z", HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat, 0x5A)]
    [InlineData("Ctrl+Shift+R", HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat, 0x52)]
    [InlineData("Alt+F12", HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat, 0x7B)]
    [InlineData("Win+Space", HotkeyModifiers.Windows | HotkeyModifiers.NoRepeat, 0x20)]
    public void TryParse_ValidCombination_ReturnsBinding(
        string value,
        HotkeyModifiers expectedModifiers,
        int expectedVirtualKey)
    {
        var result = HotkeyBinding.TryParse(value, out var binding);

        Assert.True(result);
        Assert.NotNull(binding);
        Assert.Equal(expectedModifiers, binding.Modifiers);
        Assert.Equal(expectedVirtualKey, binding.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_EmptyValue_DisablesHotkey(string value)
    {
        Assert.True(HotkeyBinding.TryParse(value, out var binding));
        Assert.Null(binding);
    }

    [Theory]
    [InlineData("Shift+")]
    [InlineData("Banana+Z")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Ж")]
    public void TryParse_InvalidCombination_ReturnsFalse(string value)
    {
        Assert.False(HotkeyBinding.TryParse(value, out _));
    }
}

