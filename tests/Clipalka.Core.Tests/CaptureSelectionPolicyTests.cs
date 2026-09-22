using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureSelectionPolicyTests
{
    [Theory]
    [InlineData(CapturePurpose.ManualRecording, true, true, true)]
    [InlineData(CapturePurpose.ManualRecording, true, false, false)]
    [InlineData(CapturePurpose.ManualRecording, false, true, false)]
    [InlineData(CapturePurpose.ReplayBuffer, true, true, false)]
    public void ShouldCaptureForegroundWindow_UsesWindowOnlyForActiveManualGame(
        CapturePurpose purpose,
        bool autoCaptureGame,
        bool isForegroundGame,
        bool expected)
    {
        var actual = CaptureSelectionPolicy.ShouldCaptureForegroundWindow(
            purpose, autoCaptureGame, isForegroundGame);

        Assert.Equal(expected, actual);
    }
}
