using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class RecordingPathServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "clipalka-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureOutputDirectory_CreatesDirectory()
    {
        var result = RecordingPathService.EnsureOutputDirectory(_directory);

        Assert.True(Directory.Exists(result));
        Assert.Equal(Path.GetFullPath(_directory), result);
    }

    [Fact]
    public void CreateVideoPath_UsesSafeTimestampedName()
    {
        var timestamp = new DateTimeOffset(2026, 9, 22, 15, 4, 5, TimeSpan.Zero);

        var result = RecordingPathService.CreateVideoPath(_directory, "replay:30s", timestamp);

        Assert.EndsWith($"replay30s_2026-09-22_15-04-05.mp4", result);
    }

    [Fact]
    public void CreateCapturePath_UsesApplicationNameAndDate()
    {
        var timestamp = new DateTimeOffset(2026, 9, 22, 18, 42, 10, TimeSpan.Zero);

        var result = RecordingPathService.CreateCapturePath(
            _directory, "Counter-Strike 2: Premier", true, timestamp);

        Assert.EndsWith("Counter-Strike 2 Premier Replay - 2026-09-22 18-42-10.mp4", result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
