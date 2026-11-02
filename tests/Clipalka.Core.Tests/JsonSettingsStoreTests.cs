using Clipalka.Core.Models;
using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "clipalka-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsSettings()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = new AppSettings
        {
            OutputDirectory = "D:\\Clips",
            FramesPerSecond = 120,
            OutputAudioDeviceId = "sonar-game",
            InputAudioDeviceId = "sonar-mic",
            OutputVolumePercent = 91,
            MicrophoneVolumePercent = 73,
            RecordHotkey = string.Empty,
            ReplayHotkey = "Shift+Z",
            MicrophoneHotkey = "Shift+X",
            GalleryHotkey = "Ctrl+G",
            ReplayControllerBinding = new ControllerBinding
            {
                DeviceId = "bluetooth-controller-id", DeviceName = "Wireless Controller",
                Inputs = [5, 8], Label = "R1 + Create"
            }
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected.OutputDirectory, actual.OutputDirectory);
        Assert.Equal(expected.FramesPerSecond, actual.FramesPerSecond);
        Assert.Equal(expected.OutputAudioDeviceId, actual.OutputAudioDeviceId);
        Assert.Equal(expected.InputAudioDeviceId, actual.InputAudioDeviceId);
        Assert.Equal(expected.OutputVolumePercent, actual.OutputVolumePercent);
        Assert.Equal(expected.MicrophoneVolumePercent, actual.MicrophoneVolumePercent);
        Assert.Equal(expected.RecordHotkey, actual.RecordHotkey);
        Assert.Equal(expected.ReplayHotkey, actual.ReplayHotkey);
        Assert.Equal(expected.MicrophoneHotkey, actual.MicrophoneHotkey);
        Assert.Equal(expected.GalleryHotkey, actual.GalleryHotkey);
        Assert.NotNull(actual.ReplayControllerBinding);
        Assert.Equal(expected.ReplayControllerBinding.DeviceId, actual.ReplayControllerBinding.DeviceId);
        Assert.Equal(expected.ReplayControllerBinding.Inputs, actual.ReplayControllerBinding.Inputs);
        Assert.Equal(expected.ReplayControllerBinding.Label, actual.ReplayControllerBinding.Label);
    }

    [Fact]
    public async Task LoadAsync_BrokenJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{ definitely broken");

        var actual = await new JsonSettingsStore(path).LoadAsync();

        Assert.Equal(60, actual.FramesPerSecond);
        Assert.Equal("Ctrl+Shift+Z", actual.ReplayHotkey);
        Assert.Null(actual.ReplayControllerBinding);
    }

    [Fact]
    public async Task LoadAsync_LegacySettings_DoNotEnableControllerHotkey()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "legacy.json");
        await File.WriteAllTextAsync(path, "{\"FramesPerSecond\":120,\"ReplayHotkey\":\"Shift+Z\"}");
        var settings = await new JsonSettingsStore(path).LoadAsync();
        Assert.Null(settings.ReplayControllerBinding);
        Assert.Equal("Shift+Z", settings.ReplayHotkey);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
