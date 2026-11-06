using Clipalka.Core.Models;

namespace Clipalka.App.Services;

internal static class MicrophoneLevelVerification
{
    public static async Task<string> RunAsync(AppSettings settings)
    {
        using var monitor = new MicrophoneLevelService();
        float maximum = 0;
        string name = "";
        for (var i = 0; i < 35; i++)
        {
            var sample = monitor.Read(settings.InputAudioDeviceId, settings.MicrophoneVolumePercent, false);
            if (!sample.Available) throw new InvalidOperationException(sample.Description);
            if (!float.IsFinite(sample.Peak) || sample.Peak is < 0 or > 1)
                throw new InvalidOperationException("Invalid microphone level.");
            maximum = Math.Max(maximum, sample.Peak);
            name = sample.Description;
            await Task.Delay(50);
        }
        var muted = monitor.Read(settings.InputAudioDeviceId, settings.MicrophoneVolumePercent, true);
        if (muted.Peak != 0) throw new InvalidOperationException("Muted microphone must have no level.");
        monitor.Suspend();
        var resumed = monitor.Read(settings.InputAudioDeviceId, settings.MicrophoneVolumePercent, false);
        if (!resumed.Available) throw new InvalidOperationException("Monitor failed after resume: " + resumed.Description);
        monitor.Suspend();
        var missing = monitor.Read("CLIPALKA-invalid-endpoint", 100, false);
        if (missing.Available || missing.Peak != 0)
            throw new InvalidOperationException("Unavailable endpoint must not show a simulated level.");
        return $"PASS: selected-endpoint shared WASAPI monitoring, bounded peaks, mute, suspend/resume and unavailable device; maximum observed peak={maximum:F6}; {name}. No audio files were written.";
    }
}
