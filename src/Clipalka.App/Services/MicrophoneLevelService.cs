using NAudio.CoreAudioApi;

namespace Clipalka.App.Services;

internal readonly record struct MicrophoneLevel(float Peak, bool Available, string Description);

// A shared, input-only monitoring client, active only while the capture page is
// visible. Audio is discarded in memory; nothing is saved or played back.
internal sealed class MicrophoneLevelService : IDisposable
{
    private MMDevice? _device;
    private WasapiCapture? _capture;
    private string? _deviceId;
    private string? _failure;
    private long _retryAt;
    private float _envelope;

    public MicrophoneLevel Read(string? deviceId, int volumePercent, bool muted)
    {
        if (muted)
        {
            Suspend();
            return new(0, true, "Микрофон выключен в CLIPALKA");
        }
        if (_deviceId != deviceId)
        {
            Suspend();
            _deviceId = deviceId;
            _retryAt = 0;
        }
        try
        {
            if (Volatile.Read(ref _failure) is { } failure) throw new IOException(failure);
            if (_capture is null)
            {
                if (Environment.TickCount64 < _retryAt) return new(0, false, "Уровень микрофона недоступен");
                using var enumerator = new MMDeviceEnumerator();
                _device = string.IsNullOrWhiteSpace(deviceId)
                    ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                    : enumerator.GetDevice(deviceId);
                _capture = new WasapiCapture(_device) { ShareMode = AudioClientShareMode.Shared };
                _capture.RecordingStopped += RecordingStopped;
                _capture.StartRecording();
            }
            var device = _device!;
            var raw = device.AudioEndpointVolume.Mute ? 0 : device.AudioMeterInformation.MasterPeakValue;
            var peak = float.IsFinite(raw) ? Math.Clamp(raw * Math.Clamp(volumePercent, 0, 100) / 100f, 0, 1) : 0;
            // Immediate attack, short decay. Both derive from actual endpoint peaks.
            _envelope = Math.Max(peak, _envelope * .78f);
            return new(_envelope, true, "Уровень входа: " + device.FriendlyName);
        }
        catch (Exception error)
        {
            Suspend();
            _retryAt = Environment.TickCount64 + 2000;
            return new(0, false, "Уровень микрофона недоступен: " + error.Message);
        }
    }

    private void RecordingStopped(object? sender, NAudio.Wave.StoppedEventArgs e)
    {
        if (ReferenceEquals(sender, _capture) && e.Exception is not null)
            Volatile.Write(ref _failure, e.Exception.Message);
    }

    public void Suspend()
    {
        var capture = _capture;
        _capture = null;
        if (capture is not null)
        {
            capture.RecordingStopped -= RecordingStopped;
            capture.Dispose();
        }
        _device?.Dispose();
        _device = null;
        _envelope = 0;
        Volatile.Write(ref _failure, null);
    }

    public void Dispose() => Suspend();
}
