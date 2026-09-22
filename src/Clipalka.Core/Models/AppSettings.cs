namespace Clipalka.Core.Models;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "CLIPALKA");

    public int FramesPerSecond { get; set; } = 60;
    public int ReplaySeconds { get; set; } = 30;
    public string? DisplayDeviceName { get; set; }
    public string? OutputAudioDeviceId { get; set; }
    public string? InputAudioDeviceId { get; set; }
    public string RecordHotkey { get; set; } = "Ctrl+Shift+R";
    public string ReplayHotkey { get; set; } = "Shift+Z";
    public bool StartReplayBufferWithApp { get; set; } = true;
}

