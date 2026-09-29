namespace Clipalka.Core.Services;

public static class RecordingQualityProfile
{
    public const float OutputVolume = 0.82f;
    public const float MicrophoneVolume = 0.68f;

    // Forced CFR can compact the video timeline when the requested rate is
    // higher than the capture and encoder path can sustain. Audio still runs
    // in real time, which leaves a frozen tail and visible stutter.
    public const bool UseFixedFramerate = false;

    // Game clips do not need a desktop pointer. Some Windows cursor themes make
    // the native cursor path fail every frame and add avoidable capture work.
    public const bool CaptureMousePointer = false;

    public static int VideoBitrateFor(int framesPerSecond) => framesPerSecond switch
    {
        >= 120 => 100_000_000,
        >= 60 => 60_000_000,
        _ => 35_000_000
    };
}
