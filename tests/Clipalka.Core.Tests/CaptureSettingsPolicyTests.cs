using Clipalka.Core.Models;
using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureSettingsPolicyTests
{
    [Fact]
    public void VolumeAndHotkeyChanges_DoNotRestartAudioCapture()
    {
        var before = new AppSettings();
        var after = new AppSettings { MicrophoneVolumePercent = 20, OutputVolumePercent = 50, ReplayHotkey = "Shift+Z" };
        Assert.False(CaptureSettingsPolicy.RequiresReplayRestart(before, after));
    }

    [Fact]
    public void EndpointOrFpsChange_RequiresNewSession()
    {
        Assert.True(CaptureSettingsPolicy.RequiresReplayRestart(new AppSettings(), new AppSettings { InputAudioDeviceId = "new-sonar" }));
        Assert.True(CaptureSettingsPolicy.RequiresReplayRestart(new AppSettings(), new AppSettings { FramesPerSecond = 120 }));
    }

    [Theory]
    [InlineData(100, 1f)]
    [InlineData(68, .68f)]
    [InlineData(0, 0f)]
    [InlineData(-1, 0f)]
    public void Gain_DoesNotAmplifyOrInvertSamples(int percent, float expected) =>
        Assert.Equal(expected, CaptureSettingsPolicy.Gain(percent));

    [Fact]
    public void Ramp_IsMonotonicAndReachesRequestedLevel()
    {
        var gains = Enumerable.Range(0, 9).Select(step => CaptureSettingsPolicy.InterpolateGain(1, .2f, step, 8)).ToArray();
        Assert.Equal(1, gains[0]);
        Assert.Equal(.2f, gains[^1], 5);
        Assert.All(gains, gain => Assert.InRange(gain, .19999f, 1));
        Assert.True(gains.Zip(gains.Skip(1)).All(pair => pair.First >= pair.Second));
    }
}
