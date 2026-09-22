using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class RecordingQualityProfileTests
{
    [Theory]
    [InlineData(30, 35_000_000)]
    [InlineData(60, 60_000_000)]
    [InlineData(120, 100_000_000)]
    public void VideoBitrateFor_ProvidesFastMotionHeadroom(int fps, int expected)
    {
        Assert.Equal(expected, RecordingQualityProfile.VideoBitrateFor(fps));
    }
}
