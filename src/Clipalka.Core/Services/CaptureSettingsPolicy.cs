using Clipalka.Core.Models;

namespace Clipalka.Core.Services;

public static class CaptureSettingsPolicy
{
    public static bool RequiresReplayRestart(AppSettings before, AppSettings after) =>
        before.FramesPerSecond != after.FramesPerSecond || before.ReplaySeconds != after.ReplaySeconds ||
        before.DisplayDeviceName != after.DisplayDeviceName || before.InputAudioDeviceId != after.InputAudioDeviceId ||
        before.OutputAudioDeviceId != after.OutputAudioDeviceId || before.AutoCaptureGame != after.AutoCaptureGame;

    public static float Gain(int percent) => Math.Clamp(percent, 0, 100) / 100f;
    public static float InterpolateGain(float from, float to, int step, int steps) =>
        from + (to - from) * Math.Clamp((float)step / Math.Max(1, steps), 0, 1);
}
