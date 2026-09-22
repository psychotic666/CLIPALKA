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

        return key switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ENTER" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            _ => null
        };
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

        return virtualKey switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Escape",
            _ => $"Key{virtualKey}"
        };
    }
}
