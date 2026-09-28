namespace Clipalka.Core.Services;

public sealed record SelectableDevice(string Id, bool IsDefault);

public static class DeviceSelectionPolicy
{
    public static string? SelectAvailableId(
        IReadOnlyCollection<SelectableDevice> availableDevices,
        string? savedDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(savedDeviceId) &&
            availableDevices.Any(device => string.Equals(device.Id, savedDeviceId, StringComparison.Ordinal)))
        {
            return savedDeviceId;
        }

        return availableDevices.FirstOrDefault(device => device.IsDefault)?.Id
               ?? availableDevices.FirstOrDefault()?.Id;
    }
}
