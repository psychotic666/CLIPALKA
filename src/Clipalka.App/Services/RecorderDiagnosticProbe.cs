using System.Diagnostics;
using Clipalka.Core.Services;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

/// <summary>Observes existing recorder callbacks without enabling video bitmap previews.</summary>
internal sealed class RecorderDiagnosticProbe : IDisposable
{
    private readonly Recorder _recorder;
    private readonly CaptureDiagnosticWriter _writer;
    private readonly string _session;
    private readonly string? _microphoneId;
    private readonly string? _outputId;
    private long _previousFrameTicks;
    private long _audioPacket;

    public RecorderDiagnosticProbe(Recorder recorder, CaptureDiagnosticWriter writer, string session,
        string? microphoneId, string? outputId)
    {
        _recorder = recorder; _writer = writer; _session = session;
        _microphoneId = microphoneId; _outputId = outputId;
        recorder.OnFrameRecorded += Frame;
        recorder.OnAudioPacketRecorded += Audio;
        recorder.OnStatusChanged += Status;
        recorder.OnRecordingFailed += Failed;
    }

    private void Frame(object? sender, FrameRecordedEventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Exchange(ref _previousFrameTicks, now);
        _writer.Log("frame", new { session = _session, frame = e.FrameNumber,
            // SRL v7.0.1 passes Unix milliseconds, NOT the encoded frame PTS.
            recorderUnixMs = e.Timestamp,
            callbackGapMs = previous == 0 ? 0 : Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds });
    }

    private void Audio(object? sender, AudioDataRecordedEventArgs e)
    {
        var data = e.AudioData;
        if (data is null) return;
        var packet = Interlocked.Increment(ref _audioPacket);
        var sources = data.Sources.Select(source => new
        {
            role = source.Id == _microphoneId ? "microphone-premix" : source.Id == _outputId ? "system-premix" : "unknown",
            id = source.Id, gain = source.Gain, bytes = source.Data?.Length ?? 0
        }).ToArray();
        _writer.Log("audio-packet", new { session = _session, packet, mixGain = data.Gain, mixBytes = data.Data?.Length ?? 0, sources });
        if (data.Data is { Length: > 0 }) _writer.Audio(_session + "-mix-pre-aac", data.Data);
        foreach (var source in data.Sources)
        {
            var role = source.Id == _microphoneId ? "microphone-premix" : source.Id == _outputId ? "system-premix" : null;
            if (role is not null && source.Data is { Length: > 0 }) _writer.Audio(_session + "-" + role, source.Data);
        }
    }

    private void Status(object? sender, RecordingStatusEventArgs e) =>
        _writer.Log("recorder-status", new { session = _session, status = e.Status.ToString() });
    private void Failed(object? sender, RecordingFailedEventArgs e) =>
        _writer.Log("recorder-error", new { session = _session, error = e.Error });

    public void Dispose()
    {
        _recorder.OnFrameRecorded -= Frame;
        _recorder.OnAudioPacketRecorded -= Audio;
        _recorder.OnStatusChanged -= Status;
        _recorder.OnRecordingFailed -= Failed;
        _writer.Log("probe-detached", new { session = _session });
    }
}
