namespace Clipalka.Core.Services;

public enum CapturePurpose
{
    ManualRecording,
    ReplayBuffer
}

public static class CaptureSelectionPolicy
{
    public static bool ShouldCaptureForegroundWindow(
        CapturePurpose purpose,
        bool autoCaptureGame,
        bool isForegroundGame) =>
        purpose == CapturePurpose.ManualRecording && autoCaptureGame && isForegroundGame;
}
