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
    private CaptureDiagnosticWriter? _diagnostics;
    private readonly HashSet<string> _diagnosticFiles = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _diagnosticStop;
    private Task _diagnosticLimitTask = Task.CompletedTask;
    public bool IsDiagnosing => _diagnostics is not null;
    public string? LastDiagnosticDirectory { get; private set; }

    public async Task StartDiagnosticsAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_diagnostics is not null) return;
            if (_manualSession is not null) throw new InvalidOperationException("Сначала остановите обычную запись.");
            var root = RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))!).AvailableFreeSpace < 3L * 1024 * 1024 * 1024)
                throw new IOException("Для диагностики нужно не менее 3 ГБ свободного места.");
            await StopReplayCoreAsync();
            var path = Path.Combine(root, "Diagnostics", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N")[..6]);
            _diagnostics = new CaptureDiagnosticWriter(path);
            LastDiagnosticDirectory = path;
            _diagnosticFiles.Clear();
            try
            {
                await File.WriteAllTextAsync(Path.Combine(path, "README.txt"),
                    "CLIPALKA diagnostic capture — local files only. Contains private voice and screen recordings. Review before sharing.\n" +
                    "*-microphone-premix.wav: microphone AFTER SRL resampling/ForceMono/volume, BEFORE mixing and AAC. Not raw Sonar input.\n" +
                    "*-system-premix.wav: system audio before mixing. *-mix-pre-aac.wav: combined PCM before AAC. PCM16 stereo 48000 Hz.\n" +
                    "WAV files concatenate callback packets; compare events.jsonl for timing and dropped packets in writer-summary.json.\n" +
                    "buffer_*.mp4: original recorder segments. snapshot_*.mp4: growing-file snapshot used by export (may be incomplete).\n" +
                    "Replay*.mp4 / game-named MP4: exported replay. frame timestamps are callback Unix milliseconds, not MP4 PTS.\n" +
                    "Diagnosis stops replay after 90 seconds or approximately 2 GiB; files are not uploaded or automatically deleted.\n");
                _diagnostics.Log("diagnostic-start", new { version = typeof(RecordingService).Assembly.GetName().Version?.ToString(),
                    os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(),
                    settings.FramesPerSecond, settings.ReplaySeconds, settings.InputAudioDeviceId, settings.OutputAudioDeviceId,
                    settings.MicrophoneVolumePercent, settings.OutputVolumePercent, settings.DisplayDeviceName, microphoneMuted = _microphoneMuted });
                var target = _captureTargets.Resolve(settings, CapturePurpose.ReplayBuffer);
                _replaySession = await StartSessionAsync(settings, target, CreateReplayTemporaryPath(), true);
                _diagnosticStop = new CancellationTokenSource();
                _diagnosticLimitTask = LimitDiagnosticsAsync(_diagnostics, _diagnosticStop.Token);
                StatusChanged?.Invoke(this, "Диагностика включена на 90 секунд. Записываются экран и отдельный голос.");
            }
            catch
            {
                await StopDiagnosticsCoreAsync();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task LimitDiagnosticsAsync(CaptureDiagnosticWriter writer, CancellationToken token)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
                var bytes = Directory.EnumerateFiles(writer.DirectoryPath).Sum(file => new FileInfo(file).Length);
                var free = new DriveInfo(Path.GetPathRoot(writer.DirectoryPath)!).AvailableFreeSpace;
                if (started.Elapsed >= TimeSpan.FromSeconds(90) || bytes >= 2L * 1024 * 1024 * 1024 || free < 512L * 1024 * 1024 || writer.Error is not null)
                {
                    // Stop capture even if a replay export currently holds the service gate.
                    _replaySession?.RequestStop();
                    await _gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        if (ReferenceEquals(_diagnostics, writer)) await StopDiagnosticsCoreAsync();
                    }
                    finally { _gate.Release(); }
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            writer.Log("diagnostic-limit-error", new { error = e.ToString() });
            // A monitoring failure must stop capture, not silently remove its time limit.
            await _gate.WaitAsync();
            try { if (ReferenceEquals(_diagnostics, writer)) await StopDiagnosticsCoreAsync(); }
            catch (Exception stopError) { StatusChanged?.Invoke(this, "Ошибка завершения диагностики: " + stopError.Message); }
            finally { _gate.Release(); }
        }
    }

    public async Task StopDiagnosticsAsync()
    {
        await _gate.WaitAsync();
        try { await StopDiagnosticsCoreAsync(); }
        finally { _gate.Release(); }
    }

    private async Task StopDiagnosticsCoreAsync()
    {
        var writer = _diagnostics;
        if (writer is null) return;
        _diagnosticStop?.Cancel();
        _diagnosticStop?.Dispose();
        _diagnosticStop = null;
        try { await StopReplayCoreAsync(); }
        finally
        {
            writer.Log("diagnostic-stop", new { });
            await writer.DisposeAsync();
            _diagnostics = null;
            _diagnosticFiles.Clear();
            StatusChanged?.Invoke(this, writer.Error is null
                ? "Диагностика завершена; replay выключен. Данные сохранены в папке Diagnostics."
                : "Диагностика завершена с ошибкой записи данных: " + writer.Error);
        }
    }

    public RecordingService(ReplayClipExporter replayExporter, CaptureTargetService captureTargets)
    {
        _replayExporter = replayExporter;
        _captureTargets = captureTargets;
    }

    public bool IsRecording => _manualSession is { HasFailed: false };
    public bool IsReplayBuffering => _replaySession is { HasFailed: false };
    public bool IsMicrophoneMuted => _microphoneMuted;
    public string? ReplayCaptureName => _replaySession?.Target.Name;
    public bool IsReplayCapturingApplication => _replaySession?.Target.IsApplication == true;

    public event EventHandler<string>? StatusChanged;

    public async Task<string?> ToggleRecordingAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            if (_diagnostics is not null) throw new InvalidOperationException("Во время диагностики используйте replay, а не вторую запись.");
            if (_manualSession is not null)
            {
                var session = _manualSession;
                _manualSession = null;
                try { await session.StopAsync(); }
                finally { session.Dispose(); }
                StatusChanged?.Invoke(this, $"Запись сохранена: {session.OutputPath}");
                return session.OutputPath;
            }

            var directory = RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            var target = _captureTargets.Resolve(settings, CapturePurpose.ManualRecording);
            var captureName = _captureTargets.GetActiveApplicationName() ?? target.Name;
            var path = RecordingPathService.CreateCapturePath(directory, captureName, false);
            _manualSession = await StartSessionAsync(settings, target, path, false);
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
            _replaySession = await StartSessionAsync(settings, target, CreateReplayTemporaryPath(), true);
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
            if (_diagnostics is not null) { await StopDiagnosticsCoreAsync(); return; }
            await StopReplayCoreAsync();
            StatusChanged?.Invoke(this, "Replay-буфер выключен");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopReplayCoreAsync()
    {
        var session = _replaySession;
        _replaySession = null;
        if (session is null) return;
        try { await session.StopAsync(); }
        finally { session.Dispose(); DeleteIfExists(session.OutputPath); ClearCompletedReplaySegments(); }
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
            if (current.HasFailed)
            {
                await StopReplayCoreAsync();
                return;
            }
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

            var directory = _diagnostics?.DirectoryPath ?? RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            var replayName = _replaySession.Target.Name;
            var outputPath = RecordingPathService.CreateCapturePath(
                directory, replayName, true);
            var snapshotPath = CreateReplaySnapshotPath();
            var replaySources = _completedReplaySegments.Select(segment => segment.Path).ToList();
            try
            {
                await CopyGrowingFileSnapshotAsync(_replaySession.OutputPath, snapshotPath);
                replaySources.Add(snapshotPath);
                _diagnostics?.Log("export-start", new { sources = replaySources, outputPath });
                await _replayExporter.ExportLastAsync(
                    replaySources,
                    outputPath,
                    TimeSpan.FromSeconds(settings.ReplaySeconds));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or COMException)
            {
                _diagnostics?.Log("export-fallback", new { error = exception.ToString() });
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
                _diagnostics?.Log("export-finished", new { outputPath, exists = File.Exists(outputPath) });
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
        _diagnostics?.Log("microphone-mute", new { muted });
        ApplyMicrophoneState(_manualSession);
        ApplyMicrophoneState(_replaySession);
        StatusChanged?.Invoke(this, muted ? "Микрофон выключен" : "Микрофон включён");
    }

    public async ValueTask DisposeAsync()
    {
        var failures = new List<Exception>();
        try { await StopDiagnosticsAsync(); }
        catch (Exception error) { failures.Add(error); }
        await _diagnosticLimitTask;
        await _gate.WaitAsync();
        try
        {
            if (_manualSession is not null)
            {
                try { await _manualSession.StopAsync(); }
                catch (Exception error) { failures.Add(error); }
                finally { _manualSession.Dispose(); _manualSession = null; }
            }
            try { await StopReplayCoreAsync(); }
            catch (Exception error) { failures.Add(error); }
            ClearCompletedReplaySegments();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
        if (failures.Count > 0) throw new AggregateException("Не все записи завершены корректно.", failures);
    }

    private RecordingSession CreateSession(
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
            // Preserve the device's channels. SRL already converts its input to the stereo output format.
            // Forcing a second mono conversion is unnecessary for Sonar and can mis-handle mono inputs.
            microphone.ForceMono = false;
            microphone.Volume = microphoneMuted ? 0f : VolumeFromPercent(settings.MicrophoneVolumePercent);
        }

        if (outputAudio is not null)
        {
            outputAudio.Volume = VolumeFromPercent(settings.OutputVolumePercent);
        }

        var audioOptions = new AudioOptions
        {
            IsAudioEnabled = true,
            Bitrate = AudioBitrate.bitrate_192kbps,
            Channels = AudioChannels.Stereo,
            IsAudioPacketPreviewEnabled = _diagnostics is not null
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
            LogOptions = _diagnostics is null ? new LogOptions { IsLogEnabled = false } : new LogOptions
            {
                IsLogEnabled = true, LogSeverityLevel = LogLevel.Debug,
                LogFilePath = Path.Combine(_diagnostics.DirectoryPath, "native-recorder.log")
            }
        };
        options.SourceOptions.RecordingSources.Add(target.Source);

        var recorder = Recorder.CreateRecorder(options);
        var probe = _diagnostics is null ? null : new RecorderDiagnosticProbe(recorder, _diagnostics,
            Path.GetFileNameWithoutExtension(outputPath), microphone?.ID, outputAudio?.ID);
        _diagnostics?.Log("session-created", new { outputPath, target = target.Name, target.IsApplication,
            microphoneId = microphone?.ID, outputId = outputAudio?.ID, microphoneDevice = microphone?.DeviceName,
            outputDevice = outputAudio?.DeviceName, forceMono = microphone?.ForceMono, settings.FramesPerSecond,
            requestedBitrate = RecordingQualityProfile.VideoBitrateFor(settings.FramesPerSecond) });
        return new RecordingSession(
            recorder,
            microphone,
            outputPath,
            target,
            VolumeFromPercent(settings.MicrophoneVolumePercent), probe, _diagnostics,
            error => StatusChanged?.Invoke(this, "Ошибка захвата: " + error));
    }

    private async Task<RecordingSession> StartSessionAsync(AppSettings settings, CaptureTarget target, string path, bool replay)
    {
        var session = CreateSession(settings, target, path, _microphoneMuted, replay);
        try
        {
            session.Start();
            ApplyMicrophoneState(session);
            await session.WaitForFirstFrameAsync();
            _diagnostics?.Log("first-frame-ready", new { path });
            return session;
        }
        catch
        {
            try { await session.StopAsync(); }
            catch (Exception e) { _diagnostics?.Log("failed-start-stop", new { path, error = e.Message }); }
            finally { session.Dispose(); }
            throw;
        }
    }

    private void ApplyMicrophoneState(RecordingSession? session)
    {
        if (session?.Microphone is null)
        {
            return;
        }

        session.Microphone.Volume = _microphoneMuted ? 0f : session.MicrophoneVolume;
        session.Recorder.GetDynamicOptionsBuilder()
            .SetUpdatedAudioSource(session.Microphone)
            .Apply();
    }

    private string CreateReplayTemporaryPath()
    {
        var directory = _diagnostics?.DirectoryPath ?? Path.Combine(Path.GetTempPath(), "CLIPALKA", "Replay");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"buffer_{Guid.NewGuid():N}.mp4");
        if (_diagnostics is not null) _diagnosticFiles.Add(path);
        return path;
    }

    private static float VolumeFromPercent(int value) => Math.Clamp(value, 0, 100) / 100f;

    private string CreateReplaySnapshotPath()
    {
        var directory = _diagnostics?.DirectoryPath ?? Path.Combine(Path.GetTempPath(), "CLIPALKA", "Replay");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"snapshot_{Guid.NewGuid():N}.mp4");
        if (_diagnostics is not null) _diagnosticFiles.Add(path);
        return path;
    }

    private async Task RotateReplaySessionAsync(
        AppSettings settings,
        CaptureTarget target,
        bool keepFinishedSegment)
    {
        var current = _replaySession;
        _diagnostics?.Log("rotation-start", new { previous = current?.OutputPath, target = target.Name });
        // Keep the previous recording alive until the replacement has actually encoded its first frame.
        var next = await StartSessionAsync(settings, target, CreateReplayTemporaryPath(), true);
        _replaySession = next;

        if (current is null)
        {
            return;
        }

        await Task.Delay(ReplaySegmentOverlap);
        try { await current.StopAsync(); }
        finally { current.Dispose(); }
        _diagnostics?.Log("rotation-finished", new { previous = current.OutputPath, next = next.OutputPath });
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

    private void DeleteReplayTemporaryFiles()
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

    private void DeleteIfExists(string path)
    {
        if (_diagnosticFiles.Contains(path)) return;
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
        private readonly CaptureStartSignal _firstFrame = new();
        private readonly object _lifetime = new();
        private bool _stopRequested;
        private bool _disposed;
        private string? _failure;
        private readonly Action<string> _failureCallback;
        public bool HasFailed => Volatile.Read(ref _failure) is not null;
        private bool _started;
        private readonly RecorderDiagnosticProbe? _probe;
        private readonly CaptureDiagnosticWriter? _diagnostics;

        public RecordingSession(
            Recorder recorder,
            CaptureAudioSource? microphone,
            string outputPath,
            CaptureTarget target,
            float microphoneVolume, RecorderDiagnosticProbe? probe, CaptureDiagnosticWriter? diagnostics, Action<string> failureCallback)
        {
            Recorder = recorder;
            Microphone = microphone;
            OutputPath = outputPath;
            Target = target;
            MicrophoneVolume = microphoneVolume;
            _probe = probe; _diagnostics = diagnostics;
            _failureCallback = failureCallback;
            Recorder.OnRecordingComplete += OnRecordingComplete;
            Recorder.OnRecordingFailed += OnRecordingFailed;
            Recorder.OnFrameRecorded += OnFirstFrame;
        }

        public Recorder Recorder { get; }
        public CaptureAudioSource? Microphone { get; }
        public string OutputPath { get; }
        public CaptureTarget Target { get; }
        public float MicrophoneVolume { get; }
        public DateTimeOffset StartedAtUtc { get; private set; }

        public void Start()
        {
            _diagnostics?.Log("record-call", new { OutputPath });
            Recorder.Record(OutputPath);
            _diagnostics?.Log("record-return", new { OutputPath });
            StartedAtUtc = DateTimeOffset.UtcNow;
            _started = true;
        }

        public async Task StopAsync()
        {
            if (!_started)
            {
                return;
            }

            RequestStop();
            _diagnostics?.Log("stop-call", new { OutputPath });
            await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(15));
            _started = false;
            if (_failure is not null) throw new InvalidOperationException(_failure);
        }

        public void RequestStop()
        {
            lock (_lifetime)
            {
                if (_disposed || _stopRequested || !_started) return;
                _stopRequested = true;
                Recorder.Stop();
            }
        }

        public Task WaitForFirstFrameAsync() => _firstFrame.WaitAsync(TimeSpan.FromSeconds(8));

        private void OnFirstFrame(object? sender, FrameRecordedEventArgs e) => _firstFrame.FrameArrived();

        public void Dispose()
        {
            lock (_lifetime)
            {
                if (_disposed) return;
                _disposed = true;
                Recorder.OnRecordingComplete -= OnRecordingComplete;
                Recorder.OnRecordingFailed -= OnRecordingFailed;
                Recorder.OnFrameRecorded -= OnFirstFrame;
                _probe?.Dispose();
                Recorder.Dispose();
            }
        }

        private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs eventArgs)
        {
            _firstFrame.Stopped();
            _stopped.TrySetResult();
        }

        private void OnRecordingFailed(object? sender, RecordingFailedEventArgs eventArgs)
        {
            Volatile.Write(ref _failure, eventArgs.Error);
            _firstFrame.Stopped(eventArgs.Error);
            _stopped.TrySetResult();
            // Do not block the native recording callback while the UI may be disposing the recorder.
            _ = Task.Run(() => _failureCallback(eventArgs.Error));
        }
    }

    private sealed record CompletedReplaySegment(string Path, DateTimeOffset FinishedAtUtc);
}
