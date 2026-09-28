using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class DeviceSelectionPolicyTests
{
    [Fact]
    public void SelectAvailableId_ReplacesMissingSavedDeviceWithCurrentDefault()
    {
        var available = new[]
        {
            new SelectableDevice("sonar-new", true),
            new SelectableDevice("speakers", false)
        };

        var selected = DeviceSelectionPolicy.SelectAvailableId(available, "sonar-old");

        Assert.Equal("sonar-new", selected);
    }

    [Fact]
    public void SelectAvailableId_KeepsSavedDeviceWhenItStillExists()
    {
        var available = new[]
        {
            new SelectableDevice("sonar-new", true),
            new SelectableDevice("headset", false)
        };

        var selected = DeviceSelectionPolicy.SelectAvailableId(available, "headset");

        Assert.Equal("headset", selected);
    }
}
