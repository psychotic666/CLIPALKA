namespace Clipalka.Core.Models;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}

public sealed record HotkeyBinding(HotkeyModifiers Modifiers, int VirtualKey)
{
    private static readonly IReadOnlyDictionary<string, int> NamedKeys =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Backspace"] = 0x08, ["Tab"] = 0x09, ["Clear"] = 0x0C, ["Enter"] = 0x0D,
            ["Pause"] = 0x13, ["CapsLock"] = 0x14, ["Escape"] = 0x1B, ["Space"] = 0x20,
            ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["End"] = 0x23, ["Home"] = 0x24,
            ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
            ["PrintScreen"] = 0x2C, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
            ["Num0"] = 0x60, ["Num1"] = 0x61, ["Num2"] = 0x62, ["Num3"] = 0x63,
            ["Num4"] = 0x64, ["Num5"] = 0x65, ["Num6"] = 0x66, ["Num7"] = 0x67,
            ["Num8"] = 0x68, ["Num9"] = 0x69, ["NumMultiply"] = 0x6A, ["NumAdd"] = 0x6B,
            ["NumSubtract"] = 0x6D, ["NumDecimal"] = 0x6E, ["NumDivide"] = 0x6F,
            ["NumLock"] = 0x90, ["ScrollLock"] = 0x91,
            ["BrowserBack"] = 0xA6, ["BrowserForward"] = 0xA7, ["BrowserRefresh"] = 0xA8,
            ["BrowserStop"] = 0xA9, ["BrowserSearch"] = 0xAA, ["BrowserFavorites"] = 0xAB,
            ["BrowserHome"] = 0xAC, ["VolumeMute"] = 0xAD, ["VolumeDown"] = 0xAE,
            ["VolumeUp"] = 0xAF, ["MediaNext"] = 0xB0, ["MediaPrevious"] = 0xB1,
            ["MediaStop"] = 0xB2, ["MediaPlayPause"] = 0xB3,
            ["Semicolon"] = 0xBA, ["Equals"] = 0xBB, ["Comma"] = 0xBC, ["Minus"] = 0xBD,
            ["Period"] = 0xBE, ["Slash"] = 0xBF, ["Backtick"] = 0xC0,
            ["LeftBracket"] = 0xDB, ["Backslash"] = 0xDC, ["RightBracket"] = 0xDD, ["Quote"] = 0xDE
        };

    public bool IsTypingSafe
    {
        get
        {
            var meaningfulModifiers = Modifiers & ~HotkeyModifiers.NoRepeat;
            var isTypingKey = VirtualKey is >= 0x30 and <= 0x5A or 0x09 or 0x0D or 0x20;
            var hasProtectiveModifier = (meaningfulModifiers &
                (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) != 0;
            return !isTypingKey || hasProtectiveModifier;
        }
    }

    public HotkeyBinding WithTypingProtection() => IsTypingSafe
        ? this
        : this with { Modifiers = Modifiers | HotkeyModifiers.Control };

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(FormatVirtualKey(VirtualKey));
        return string.Join('+', parts);
    }

    public static bool TryParse(string? value, out HotkeyBinding? binding)
    {
        binding = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.NoRepeat;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "SHIFT" => HotkeyModifiers.Shift,
                "ALT" => HotkeyModifiers.Alt,
                "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
                _ => (HotkeyModifiers)uint.MaxValue
            };

            if ((uint)modifiers == uint.MaxValue)
            {
                return false;
            }
        }

        var key = parts[^1].ToUpperInvariant();
        var virtualKey = ParseVirtualKey(key);
        if (virtualKey is null)
        {
            return false;
        }

        binding = new HotkeyBinding(modifiers, virtualKey.Value);
        return true;
    }

    private static int? ParseVirtualKey(string key)
    {
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return key[0];
        }

        if (key.Length is 2 or 3 && key[0] == 'F' &&
            int.TryParse(key[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            return 0x70 + functionNumber - 1;
        }

        if (NamedKeys.TryGetValue(key, out var namedVirtualKey))
        {
            return namedVirtualKey;
        }

        if (key.Equals("ESC", StringComparison.OrdinalIgnoreCase))
        {
            return 0x1B;
        }

        if (key.StartsWith("VK_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(key.AsSpan(3), System.Globalization.NumberStyles.HexNumber, null, out var rawVirtualKey) &&
            rawVirtualKey is > 0 and <= 0xFF)
        {
            return rawVirtualKey;
        }

        return null;
    }

    private static string FormatVirtualKey(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x70 + 1}";
        }

        var namedKey = NamedKeys.FirstOrDefault(pair => pair.Value == virtualKey).Key;
        if (!string.IsNullOrWhiteSpace(namedKey))
        {
            return namedKey;
        }

        return $"VK_{virtualKey:X2}";
    }
}
