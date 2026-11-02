using System.Runtime.InteropServices;

namespace Clipalka.App.Services;

internal interface IControllerInput
{
    string Id { get; }
    string Name { get; }
    int[] ReadPressed();
    string Label(int input);
}

// Desktop fallbacks for drivers not enumerated by Windows.Gaming.Input.
internal static class NativeControllerInputs
{
    public static string VerifyDesktopInterop()
    {
        if (Marshal.SizeOf<XState>() != 16 || Marshal.SizeOf<JoyInfo>() != 52 || Marshal.SizeOf<JoyCaps>() != 728)
            throw new InvalidOperationException("Unexpected Windows controller structure layout.");
        var sources = Enumerate().ToList();
        foreach (var source in sources) source.ReadPressed();
        return $"PASS: XInput/WinMM entrypoints callable; native layouts 16/52/728 verified; readable desktop controller sources: {sources.Count}. Physical Bluetooth detection requires a connected controller.";
    }

    public static IEnumerable<IControllerInput> Enumerate()
    {
        for (uint i = 0; i < 4; i++)
            if (XInputGetState(i, out _) == 0) yield return new XInputDevice(i);
        for (uint i = 0; i < Math.Min(joyGetNumDevs(), 16); i++)
        {
            var state = JoyInfo.Create();
            if (joyGetPosEx(i, ref state) == 0 && joyGetDevCapsW((nuint)i, out var caps, (uint)Marshal.SizeOf<JoyCaps>()) == 0 && caps.ButtonCount > 0)
                yield return new JoystickDevice(i, caps);
        }
    }

    private sealed class XInputDevice(uint slot) : IControllerInput
    {
        public string Id => $"xinput:{slot}";
        public string Name => $"XInput Controller {slot + 1} · Flydigi / Steam Input";
        public int[] ReadPressed()
        {
            if (XInputGetState(slot, out var state) != 0) throw new IOException("XInput controller disconnected.");
            return Enumerable.Range(0, 16).Where(bit => (state.Buttons & (1 << bit)) != 0).ToArray();
        }
        public string Label(int input) => input switch
        {
            0 => "D-pad Up", 1 => "D-pad Down", 2 => "D-pad Left", 3 => "D-pad Right",
            4 => "Start", 5 => "Back", 6 => "L3", 7 => "R3", 8 => "LB", 9 => "RB",
            12 => "A", 13 => "B", 14 => "X", 15 => "Y", _ => $"Button {input + 1}"
        };
    }

    private sealed class JoystickDevice(uint slot, JoyCaps caps) : IControllerInput
    {
        public string Id => $"winmm:{caps.Manufacturer:X4}:{caps.Product:X4}:{caps.RegistryKey}:{slot}";
        public string Name => (string.IsNullOrWhiteSpace(caps.Name) ? $"Gamepad {slot + 1}" : caps.Name) + " · Desktop HID";
        public int[] ReadPressed()
        {
            var state = JoyInfo.Create();
            if (joyGetPosEx(slot, ref state) != 0) throw new IOException("Desktop HID controller disconnected.");
            var inputs = Enumerable.Range(0, (int)Math.Min(caps.ButtonCount, 32)).Where(bit => (state.Buttons & (1u << bit)) != 0).ToList();
            if ((caps.Capabilities & 16) != 0 && state.Pov != 0xFFFF && state.Pov < 36000)
                inputs.Add(32 + (int)((state.Pov + 2250) / 4500 % 8));
            return inputs.ToArray();
        }
        public string Label(int input) => input >= 32
            ? "D-pad " + new[] { "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft" }[input - 32]
            : $"Button {input + 1}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XState
    {
        public uint Packet;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LeftX, LeftY, RightX, RightY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfo
    {
        public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2;
        public static JoyInfo Create() => new() { Size = (uint)Marshal.SizeOf<JoyInfo>(), Flags = 0xC0 };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort Manufacturer, Product;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public uint XMin, XMax, YMin, YMax, ZMin, ZMax, ButtonCount, PeriodMin, PeriodMax;
        public uint RMin, RMax, UMin, UMax, VMin, VMax, Capabilities, MaxAxes, AxisCount, MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegistryKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemDriver;
    }

    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint index, out XState state);
    [DllImport("winmm.dll")] private static extern uint joyGetNumDevs();
    [DllImport("winmm.dll")] private static extern uint joyGetPosEx(uint id, ref JoyInfo info);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint joyGetDevCapsW(nuint id, out JoyCaps caps, uint size);
}
