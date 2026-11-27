namespace Clipalka.Core.Services;

public enum CapturePurpose
{
    ManualRecording,
    ReplayBuffer
}

public static class CaptureSelectionPolicy
{
    public static bool IsKnownGameProcess(string processName) =>
        processName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("RobloxPlayer", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("Windows10Universal", StringComparison.OrdinalIgnoreCase);

    public static bool HasGameWindowSize(string processName, int width, int height, double monitorCoverage) =>
        IsKnownGameProcess(processName)
            ? width >= 320 && height >= 240
            : width >= 800 && height >= 500 && monitorCoverage >= 0.30;

    public static bool ShouldCaptureForegroundWindow(
        CapturePurpose purpose,
        bool autoCaptureGame,
        bool isForegroundGame) => autoCaptureGame && isForegroundGame;

    public static bool ShouldReplaceReplayTarget(
        bool currentIsApplication,
        bool currentIsAlive,
        bool candidateIsApplication,
        bool candidateIsKnownGame = false,
        bool isSameWindow = false)
    {
        if (currentIsApplication)
        {
            // A heuristic match (for example a launcher) must not pin the buffer
            // forever when a recognized game subsequently becomes foreground.
            return !currentIsAlive || (candidateIsApplication && candidateIsKnownGame && !isSameWindow);
        }

        return candidateIsApplication;
    }
}
