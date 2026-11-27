using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureSelectionPolicyTests
{
    [Theory]
    [InlineData("RobloxPlayerBeta", 640, 480, 0.08, true)]
    [InlineData("robloxplayerbeta", 1280, 720, 0.25, true)]
    [InlineData("Windows10Universal", 640, 480, 0.08, true)]
    [InlineData("RobloxPlayerBeta", 0, 0, 0, false)]
    [InlineData("RobloxPlayerBeta", 200, 100, 0.01, false)]
    [InlineData("OtherGame", 640, 480, 0.08, false)]
    [InlineData("OtherGame", 1280, 720, 0.40, true)]
    [InlineData("RobloxPlayerBetaLauncher", 640, 480, 0.08, false)]
    public void WindowedRoblox_DoesNotRequireThirtyPercentOfMonitor(
        string process, int width, int height, double coverage, bool expected) =>
        Assert.Equal(expected, CaptureSelectionPolicy.HasGameWindowSize(process, width, height, coverage));

    [Theory]
    [InlineData(true, true, false, true)] // Recognized Roblox replaces a pinned launcher.
    [InlineData(true, true, true, false)] // Same game must not restart every timer tick.
    [InlineData(false, false, false, false)] // Alt+Tab to desktop keeps game capture.
    [InlineData(true, false, false, false)] // A heuristic match cannot displace a live game.
    public void ReplayTarget_RecognizedGameCanReplacePinnedWindow(
        bool candidateIsApplication, bool knownGame, bool sameWindow, bool expected) =>
        Assert.Equal(expected, CaptureSelectionPolicy.ShouldReplaceReplayTarget(
            true, true, candidateIsApplication, knownGame, sameWindow));

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
