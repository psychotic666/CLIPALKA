using Clipalka.App.Models;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

public sealed class DeviceCatalogService
{
    public IReadOnlyList<DeviceOption> GetDisplays() => Recorder.GetDisplays()
        .Select(display => new DeviceOption(display.DeviceName, display.FriendlyName))
        .ToList();

    public IReadOnlyList<DeviceOption> GetOutputDevices() => Recorder.GetSystemAudioLoopbackDevices()
        .Select(device => new DeviceOption(device.DeviceName, device.FriendlyName, device.IsDefaultDevice))
        .OrderByDescending(device => device.IsDefault)
        .ThenBy(device => device.Name)
        .ToList();

    public IReadOnlyList<DeviceOption> GetInputDevices() => Recorder.GetSystemAudioCaptureDevices()
        .Select(device => new DeviceOption(device.DeviceName, device.FriendlyName, device.IsDefaultDevice))
        .OrderByDescending(device => device.IsDefault)
        .ThenBy(device => device.Name)
        .ToList();
}

