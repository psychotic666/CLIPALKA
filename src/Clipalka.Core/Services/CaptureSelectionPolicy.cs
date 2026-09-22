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
        bool isForegroundGame) => autoCaptureGame && isForegroundGame;

    public static bool ShouldReplaceReplayTarget(
        bool currentIsApplication,
        bool currentIsAlive,
        bool candidateIsApplication)
    {
        if (currentIsApplication)
        {
            return !currentIsAlive;
        }

        return candidateIsApplication;
    }
}
