using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using Clipalka.Core.Services;

namespace Clipalka.App.Services;

// A non-exclusive background HID source. WinMM can enumerate a Bluetooth pad
// while returning only its neutral snapshot; raw reports bypass that bridge.
internal sealed class RawControllerInputs : IDisposable
{
    private const uint DeviceInfo = 0x2000000B, DeviceName = 0x20000007, PreparsedData = 0x20000005;
    private const int Success = 0x110000;
    private readonly HwndSource _window;
    private readonly Dictionary<nint, Device> _devices = [];
    private readonly Dictionary<nint, Device> _byHandle = [];
    private bool _disposed;

    public RawControllerInputs()
    {
        _window = new HwndSource(new HwndSourceParameters("CLIPALKA controller input")
        { ParentWindow = new nint(-3), WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WindowProc);
        if (!RegisterRawInputDevices(Registrations(0x100, _window.Handle), 2, (uint)Marshal.SizeOf<Registration>()))
        {
            _window.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static Registration[] Registrations(uint flags, nint target) =>
        [new() { Page = 1, Usage = 4, Flags = flags, Target = target },
         new() { Page = 1, Usage = 5, Flags = flags, Target = target }];

    public IReadOnlyList<IControllerInput> Enumerate()
    {
        uint count = 0;
        var size = (uint)Marshal.SizeOf<DeviceList>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var list = new DeviceList[count];
        var found = GetRawInputDeviceList(list, ref count, size);
        if (found == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
        var seen = new HashSet<nint>();
        for (var i = 0; i < found; i++)
        {
            if (list[i].Type != 2) continue;
            var handle = list[i].Handle;
            var info = Marshal.AllocHGlobal(32);
            try
            {
                Marshal.WriteInt32(info, 32);
                uint length = 32;
                if (GetRawInputDeviceInfo(handle, DeviceInfo, info, ref length) == uint.MaxValue) continue;
                var page = (ushort)Marshal.ReadInt16(info, 20);
                var usage = (ushort)Marshal.ReadInt16(info, 22);
                if (page != 1 || usage is not (4 or 5)) continue;
                seen.Add(handle);
                if (_devices.ContainsKey(handle)) continue;
                var nameLength = 0u;
                if (GetRawInputDeviceInfo(handle, DeviceName, 0, ref nameLength) == uint.MaxValue) continue;
                var path = new StringBuilder((int)nameLength + 1);
                if (GetRawInputDeviceName(handle, DeviceName, path, ref nameLength) == uint.MaxValue) continue;
                var descriptorLength = 0u;
                if (GetRawInputDeviceInfo(handle, PreparsedData, 0, ref descriptorLength) == uint.MaxValue || descriptorLength == 0) continue;
                var descriptor = Marshal.AllocHGlobal((int)descriptorLength);
                if (GetRawInputDeviceInfo(handle, PreparsedData, descriptor, ref descriptorLength) == uint.MaxValue)
                { Marshal.FreeHGlobal(descriptor); continue; }
                var vendor = (uint)Marshal.ReadInt32(info, 8);
                var product = (uint)Marshal.ReadInt32(info, 12);
                var device = new Device(path.ToString(), vendor, product, descriptor);
                _devices.Add(handle, device);
                _byHandle[handle] = device;
            }
            finally { Marshal.FreeHGlobal(info); }
        }
        foreach (var removed in _devices.Keys.Where(handle => !seen.Contains(handle)).ToArray())
        { _devices[removed].Dispose(); _devices.Remove(removed); _byHandle.Remove(removed); }
        return _devices.Values.Cast<IControllerInput>().ToArray();
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0xFF || _disposed) return 0;
        var headerSize = (uint)(8 + 2 * IntPtr.Size);
        uint size = 0;
        if (GetRawInputData(lParam, 0x10000003, 0, ref size, headerSize) == uint.MaxValue || size < headerSize + 8 || size > 65536) return 0;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, 0x10000003, buffer, ref size, headerSize) == uint.MaxValue || Marshal.ReadInt32(buffer) != 2) return 0;
            var handle = Marshal.ReadIntPtr(buffer, 8);
            if (!_byHandle.TryGetValue(handle, out var device)) return 0;
            var reportSize = Marshal.ReadInt32(buffer, (int)headerSize);
            var count = Marshal.ReadInt32(buffer, (int)headerSize + 4);
            if (reportSize <= 0 || count <= 0 || (long)reportSize * count > size - headerSize - 8) return 0;
            var report = new byte[reportSize];
            for (var i = 0; i < count; i++)
            {
                Marshal.Copy(buffer + (int)headerSize + 8 + i * reportSize, report, 0, reportSize);
                device.Accept(report);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        // Let DefWindowProc perform WM_INPUT cleanup; never consume mouse/keyboard.
        return 0;
    }

    private sealed class Device(string path, uint vendor, uint product, nint descriptor) : IControllerInput, IDisposable
    {
        private int[] _pressed = [];
        private long _reports;
        private int _lastStatus;
        private byte _reportId;
        public bool HasReading { get; private set; }
        public string Diagnostic => $"reports={_reports}, report=0x{_reportId:X2}, HID status=0x{_lastStatus:X8}";
        public string Id => "rawhid:" + path;
        public string Name => (vendor == 0x054C && product == 0x0CE6 ? "DualSense" : $"Gamepad {vendor:X4}:{product:X4}") + " · Raw Input";
        public int[] ReadPressed() => _pressed;
        public string Label(int input) => input >= 0x10000
            ? "D-pad " + new[] { "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft" }[input - 0x10000]
            : vendor == 0x054C && product == 0x0CE6 && input is >= 0 and < 15
            ? new[] { "Square", "Cross", "Circle", "Triangle", "L1", "R1", "L2", "R2", "Create", "Options", "L3", "R3", "PS", "Touchpad", "Mute" }[input]
            : $"Button {input + 1}";

        public void Accept(byte[] report)
        {
            var usages = new ushort[128];
            uint count = (uint)usages.Length;
            _reports++;
            _reportId = report.Length == 0 ? (byte)0 : report[0];
            if (vendor == 0x054C && product == 0x0CE6 && _reportId == 0x31)
            {
                if (!DualSenseInputReport.TryRead(report, out var sonyButtons)) return;
                _pressed = sonyButtons;
                HasReading = true;
                _lastStatus = Success;
                return;
            }
            _lastStatus = HidP_GetUsages(0, 9, 0, usages, ref count, descriptor, report, (uint)report.Length);
            if (_lastStatus != Success) return;
            var pressed = usages.Take((int)count).Where(usage => usage > 0).Select(usage => (int)usage - 1).ToList();
            if (HidP_GetUsageValue(0, 1, 0, 0x39, out var hat, descriptor, report, (uint)report.Length) == Success && hat < 8)
                pressed.Add(0x10000 + (int)hat);
            _pressed = pressed.Distinct().Order().ToArray();
            HasReading = true;
        }
        public void Dispose() => Marshal.FreeHGlobal(descriptor);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RegisterRawInputDevices(Registrations(1, 0), 2, (uint)Marshal.SizeOf<Registration>());
        _window.RemoveHook(WindowProc);
        _window.Dispose();
        foreach (var device in _devices.Values) device.Dispose();
        _devices.Clear(); _byHandle.Clear();
    }

    [StructLayout(LayoutKind.Sequential)] private struct DeviceList { public nint Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] private struct Registration { public ushort Page, Usage; public uint Flags; public nint Target; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputDeviceList([Out] DeviceList[]? devices, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)] private static extern uint GetRawInputDeviceInfo(nint device, uint command, nint data, ref uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetRawInputDeviceName(nint device, uint command, StringBuilder data, ref uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("hid.dll")] private static extern int HidP_GetUsages(int reportType, ushort page, ushort collection, [Out] ushort[] usages, ref uint count, nint descriptor, byte[] report, uint length);
    [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int reportType, ushort page, ushort collection, ushort usage, out uint value, nint descriptor, byte[] report, uint length);
}
