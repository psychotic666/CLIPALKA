using System.Runtime.InteropServices;
using Clipalka.Core.Models;
using Clipalka.Core.Services;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

public sealed class RecordingService : IAsyncDisposable
{
    private static readonly TimeSpan ReplaySegmentOverlap = TimeSpan.FromMilliseconds(180);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<CompletedReplaySegment> _completedReplaySegments = new();
    private readonly ReplayClipExporter _replayExporter;
    private readonly CaptureTargetService _captureTargets;
    private RecordingSession? _manualSession;
    private RecordingSession? _replaySession;
    private bool _microphoneMuted;

    public RecordingService(ReplayClipExporter replayExporter, CaptureTargetService captureTargets)
    {
        _replayExporter = replayExporter;
        _captureTargets = captureTargets;
    }

    public bool IsRecording => _manualSession is not null;
    public bool IsReplayBuffering => _replaySession is not null;
    public bool IsMicrophoneMuted => _microphoneMuted;
    public string? ReplayCaptureName => _replaySession?.Target.Name;
    public bool IsReplayCapturingApplication => _replaySession?.Target.IsApplication == true;

    public event EventHandler<string>? StatusChanged;

    public async Task<string?> ToggleRecordingAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_manualSession is not null)
            {
                var session = _manualSession;
                _manualSession = null;
                await session.StopAsync();
                session.Dispose();
                StatusChanged?.Invoke(this, $"Запись сохранена: {session.OutputPath}");
                return session.OutputPath;
            }

            var directory = RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            var target = _captureTargets.Resolve(settings, CapturePurpose.ManualRecording);
            var captureName = _captureTargets.GetActiveApplicationName() ?? target.Name;
            var path = RecordingPathService.CreateCapturePath(directory, captureName, false);
            _manualSession = CreateSession(settings, target, path, _microphoneMuted, false);
            _manualSession.Start();
            ApplyMicrophoneState(_manualSession);
            StatusChanged?.Invoke(this, $"Идёт запись: {target.Name}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartReplayBufferAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_replaySession is not null)
            {
                return;
            }

            var target = _captureTargets.Resolve(settings, CapturePurpose.ReplayBuffer);
            DeleteReplayTemporaryFiles();
            _replaySession = CreateSession(settings, target, CreateReplayTemporaryPath(), _microphoneMuted, true);
            _replaySession.Start();
            ApplyMicrophoneState(_replaySession);
            StatusChanged?.Invoke(this, $"Replay-буфер активен: последние {settings.ReplaySeconds} секунд");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopReplayBufferAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_replaySession is null)
            {
                return;
            }

            var session = _replaySession;
            _replaySession = null;
            await session.StopAsync();
            session.Dispose();
            DeleteIfExists(session.OutputPath);
            ClearCompletedReplaySegments();
            StatusChanged?.Invoke(this, "Replay-буфер выключен");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RefreshReplayTargetAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_replaySession is null)
            {
                return;
            }

            var current = _replaySession;
            var currentIsAlive = _captureTargets.IsTargetAlive(current.Target);
            var candidate = _captureTargets.Resolve(settings, CapturePurpose.ReplayBuffer);
            var shouldReplaceTarget = CaptureSelectionPolicy.ShouldReplaceReplayTarget(
                    current.Target.IsApplication,
                    currentIsAlive,
                    candidate.IsApplication);
            var segmentLength = TimeSpan.FromSeconds(Math.Max(40, settings.ReplaySeconds + 10));
            if (!shouldReplaceTarget && DateTimeOffset.UtcNow - current.StartedAtUtc < segmentLength)
            {
                CleanupExpiredReplaySegments(settings.ReplaySeconds);
                return;
            }

            var nextTarget = shouldReplaceTarget ? candidate : current.Target;
            await RotateReplaySessionAsync(settings, nextTarget, keepFinishedSegment: !shouldReplaceTarget);
            if (shouldReplaceTarget)
            {
                ClearCompletedReplaySegments();
                StatusChanged?.Invoke(this, candidate.IsApplication
                    ? $"Replay привязан к игре: {candidate.Name}"
                    : $"Replay переключён на монитор: {candidate.Name}");
            }
            CleanupExpiredReplaySegments(settings.ReplaySeconds);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> SaveReplayAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_replaySession is null)
            {
                throw new InvalidOperationException("Сначала включите replay-буфер.");
            }

            var directory = RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            var replayName = _replaySession.Target.Name;
            var outputPath = RecordingPathService.CreateCapturePath(
                directory, replayName, true);
            var snapshotPath = CreateReplaySnapshotPath();
            var replaySources = _completedReplaySegments.Select(segment => segment.Path).ToList();
            try
            {
                await CopyGrowingFileSnapshotAsync(_replaySession.OutputPath, snapshotPath);
                replaySources.Add(snapshotPath);
                await _replayExporter.ExportLastAsync(
                    replaySources,
                    outputPath,
                    TimeSpan.FromSeconds(settings.ReplaySeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException)
            {
                // Some Windows encoders deny shared reads. Fall back to a fast rollover,
                // preserving the old session as a finalized replay source.
                await RotateReplaySessionAsync(settings, _replaySession.Target, keepFinishedSegment: true);
                replaySources = _completedReplaySegments.Select(segment => segment.Path).ToList();
                await _replayExporter.ExportLastAsync(
                    replaySources,
                    outputPath,
                    TimeSpan.FromSeconds(settings.ReplaySeconds));
            }
            finally
            {
                DeleteIfExists(snapshotPath);
            }

            StatusChanged?.Invoke(this, $"Replay сохранён: {outputPath}");
            return outputPath;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SetMicrophoneMuted(bool muted)
    {
        _microphoneMuted = muted;
        ApplyMicrophoneState(_manualSession);
        ApplyMicrophoneState(_replaySession);
        StatusChanged?.Invoke(this, muted ? "Микрофон выключен" : "Микрофон включён");
    }

    public async ValueTask DisposeAsync()
    {
        if (_manualSession is not null)
        {
            await _manualSession.StopAsync();
            _manualSession.Dispose();
            _manualSession = null;
        }

        if (_replaySession is not null)
        {
            var temporaryPath = _replaySession.OutputPath;
            await _replaySession.StopAsync();
            _replaySession.Dispose();
            _replaySession = null;
            DeleteIfExists(temporaryPath);
        }

        ClearCompletedReplaySegments();

        _gate.Dispose();
    }

    private static RecordingSession CreateSession(
        AppSettings settings,
        CaptureTarget target,
        string outputPath,
        bool microphoneMuted,
        bool isReplayBuffer)
    {
        var outputAudio = string.IsNullOrWhiteSpace(settings.OutputAudioDeviceId)
            ? LoopbackAudioSource.Default
            : new LoopbackAudioSource(settings.OutputAudioDeviceId);
        var microphone = string.IsNullOrWhiteSpace(settings.InputAudioDeviceId)
            ? CaptureAudioSource.Default
            : new CaptureAudioSource(settings.InputAudioDeviceId);
        if (microphone is not null)
        {
            microphone.ForceMono = true;
            microphone.Volume = microphoneMuted ? 0f : RecordingQualityProfile.MicrophoneVolume;
        }

        if (outputAudio is not null)
        {
            outputAudio.Volume = RecordingQualityProfile.OutputVolume;
        }

        var audioOptions = new AudioOptions
        {
            IsAudioEnabled = true,
            Bitrate = AudioBitrate.bitrate_192kbps,
            Channels = AudioChannels.Stereo
        };

        if (outputAudio is not null)
        {
            audioOptions.AudioSources.Add(outputAudio);
        }

        if (microphone is not null)
        {
            audioOptions.AudioSources.Add(microphone);
        }

        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions(),
            AudioOptions = audioOptions,
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Framerate = settings.FramesPerSecond,
                IsFixedFramerate = true,
                IsLowLatencyEnabled = false,
                IsThrottlingDisabled = false,
                IsHardwareEncodingEnabled = true,
                Quality = 100,
                Bitrate = RecordingQualityProfile.VideoBitrateFor(settings.FramesPerSecond),
                Encoder = new H264VideoEncoder
                {
                    EncoderProfile = H264Profile.High,
                    BitrateMode = H264BitrateControlMode.UnconstrainedVBR
                },
                IsMp4FastStartEnabled = !isReplayBuffer,
                IsFragmentedMp4Enabled = isReplayBuffer
            },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = true },
            LogOptions = new LogOptions { IsLogEnabled = false }
        };
        options.SourceOptions.RecordingSources.Add(target.Source);

        return new RecordingSession(Recorder.CreateRecorder(options), microphone, outputPath, target);
    }

    private void ApplyMicrophoneState(RecordingSession? session)
    {
        if (session?.Microphone is null)
        {
            return;
        }

        session.Microphone.Volume = _microphoneMuted ? 0f : RecordingQualityProfile.MicrophoneVolume;
        session.Recorder.GetDynamicOptionsBuilder()
            .SetUpdatedAudioSource(session.Microphone)
            .Apply();
    }

    private static string CreateReplayTemporaryPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CLIPALKA", "Replay");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"buffer_{Guid.NewGuid():N}.mp4");
    }

    private static string CreateReplaySnapshotPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CLIPALKA", "Replay");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"snapshot_{Guid.NewGuid():N}.mp4");
    }

    private async Task RotateReplaySessionAsync(
        AppSettings settings,
        CaptureTarget target,
        bool keepFinishedSegment)
    {
        var current = _replaySession;
        var next = CreateSession(settings, target, CreateReplayTemporaryPath(), _microphoneMuted, true);
        next.Start();
        ApplyMicrophoneState(next);
        _replaySession = next;

        if (current is null)
        {
            return;
        }

        await Task.Delay(ReplaySegmentOverlap);
        await current.StopAsync();
        current.Dispose();
        if (keepFinishedSegment)
        {
            _completedReplaySegments.Enqueue(new CompletedReplaySegment(current.OutputPath, DateTimeOffset.UtcNow));
        }
        else
        {
            DeleteIfExists(current.OutputPath);
        }
    }

    private void CleanupExpiredReplaySegments(int replaySeconds)
    {
        var oldestUsefulTime = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(replaySeconds + 5);
        while (_completedReplaySegments.TryPeek(out var segment) && segment.FinishedAtUtc < oldestUsefulTime)
        {
            _completedReplaySegments.Dequeue();
            DeleteIfExists(segment.Path);
        }
    }

    private void ClearCompletedReplaySegments()
    {
        while (_completedReplaySegments.TryDequeue(out var segment))
        {
            DeleteIfExists(segment.Path);
        }
    }

    private static async Task CopyGrowingFileSnapshotAsync(string sourcePath, string destinationPath)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var remaining = source.Length;
        var buffer = new byte[1024 * 1024];
        while (remaining > 0)
        {
            var bytesRead = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
            if (bytesRead == 0)
            {
                break;
            }
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
            remaining -= bytesRead;
        }
    }

    private static void DeleteReplayTemporaryFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CLIPALKA", "Replay");
        if (!Directory.Exists(directory))
        {
            return;
        }

        var staleBefore = DateTime.UtcNow - TimeSpan.FromDays(1);
        foreach (var path in Directory.EnumerateFiles(directory, "*.mp4"))
        {
            if (File.GetLastWriteTimeUtc(path) < staleBefore)
            {
                DeleteIfExists(path);
            }
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A locked temporary file can be removed on the next application start.
        }
    }

    private sealed class RecordingSession : IDisposable
    {
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _started;

        public RecordingSession(
            Recorder recorder,
            CaptureAudioSource? microphone,
            string outputPath,
            CaptureTarget target)
        {
            Recorder = recorder;
            Microphone = microphone;
            OutputPath = outputPath;
            Target = target;
            Recorder.OnRecordingComplete += OnRecordingComplete;
            Recorder.OnRecordingFailed += OnRecordingFailed;
        }

        public Recorder Recorder { get; }
        public CaptureAudioSource? Microphone { get; }
        public string OutputPath { get; }
        public CaptureTarget Target { get; }
        public DateTimeOffset StartedAtUtc { get; private set; }

        public void Start()
        {
            Recorder.Record(OutputPath);
            StartedAtUtc = DateTimeOffset.UtcNow;
            _started = true;
        }

        public async Task StopAsync()
        {
            if (!_started)
            {
                return;
            }

            Recorder.Stop();
            await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
            _started = false;
        }

        public void Dispose()
        {
            Recorder.OnRecordingComplete -= OnRecordingComplete;
            Recorder.OnRecordingFailed -= OnRecordingFailed;
            Recorder.Dispose();
        }

        private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs eventArgs) =>
            _stopped.TrySetResult();

        private void OnRecordingFailed(object? sender, RecordingFailedEventArgs eventArgs) =>
            _stopped.TrySetException(new InvalidOperationException(eventArgs.Error));
    }

    private sealed record CompletedReplaySegment(string Path, DateTimeOffset FinishedAtUtc);
}
