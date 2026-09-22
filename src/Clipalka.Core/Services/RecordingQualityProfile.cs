namespace Clipalka.Core.Services;

public static class RecordingQualityProfile
{
    public const float OutputVolume = 0.82f;
    public const float MicrophoneVolume = 0.68f;

    public static int VideoBitrateFor(int framesPerSecond) => framesPerSecond switch
    {
        >= 120 => 100_000_000,
        >= 60 => 60_000_000,
        _ => 35_000_000
    };
}
