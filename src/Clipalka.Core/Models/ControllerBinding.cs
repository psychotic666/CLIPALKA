namespace Clipalka.Core.Models;

public sealed class ControllerBinding
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public int[] Inputs { get; set; } = [];
    public string Label { get; set; } = string.Empty;
}
