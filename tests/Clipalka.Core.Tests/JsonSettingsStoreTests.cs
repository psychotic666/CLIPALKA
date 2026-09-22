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
            RecordHotkey = string.Empty,
            ReplayHotkey = "Shift+Z"
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected.OutputDirectory, actual.OutputDirectory);
        Assert.Equal(expected.FramesPerSecond, actual.FramesPerSecond);
        Assert.Equal(expected.OutputAudioDeviceId, actual.OutputAudioDeviceId);
        Assert.Equal(expected.InputAudioDeviceId, actual.InputAudioDeviceId);
        Assert.Equal(expected.RecordHotkey, actual.RecordHotkey);
        Assert.Equal(expected.ReplayHotkey, actual.ReplayHotkey);
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
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
