using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureSelectionPolicyTests
{
    [Theory]
    [InlineData(CapturePurpose.ManualRecording, true, true, true)]
    [InlineData(CapturePurpose.ManualRecording, true, false, false)]
    [InlineData(CapturePurpose.ManualRecording, false, true, false)]
    [InlineData(CapturePurpose.ReplayBuffer, true, true, true)]
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

    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    public void ShouldReplaceReplayTarget_KeepsLiveGameAndPromotesDisplayToGame(
        bool currentIsApplication,
        bool currentIsAlive,
        bool candidateIsApplication,
        bool expected)
    {
        var actual = CaptureSelectionPolicy.ShouldReplaceReplayTarget(
            currentIsApplication, currentIsAlive, candidateIsApplication);

        Assert.Equal(expected, actual);
    }
}
