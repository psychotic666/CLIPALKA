using Clipalka.Core.Models;
using Clipalka.Core.Services;
using ScreenRecorderLib;

namespace Clipalka.App.Services;

public sealed class RecordingService : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
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
            var target = _captureTargets.Resolve(settings);
            var captureName = _captureTargets.GetActiveApplicationName() ?? target.Name;
            var path = RecordingPathService.CreateCapturePath(directory, captureName, false);
            _manualSession = CreateSession(settings, target, path, _microphoneMuted);
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

            var target = _captureTargets.Resolve(settings);
            _replaySession = CreateSession(settings, target, CreateReplayTemporaryPath(), _microphoneMuted);
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
            StatusChanged?.Invoke(this, "Replay-буфер выключен");
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

            var finishedSession = _replaySession;
            _replaySession = null;
            await finishedSession.StopAsync();
            finishedSession.Dispose();

            // Restart capture before rendering so the unavoidable gap stays as short as possible.
            var nextTarget = _captureTargets.Resolve(settings);
            _replaySession = CreateSession(settings, nextTarget, CreateReplayTemporaryPath(), _microphoneMuted);
            _replaySession.Start();
            ApplyMicrophoneState(_replaySession);

            var directory = RecordingPathService.EnsureOutputDirectory(settings.OutputDirectory);
            var replayName = _captureTargets.GetActiveApplicationName() ?? finishedSession.CaptureName;
            var outputPath = RecordingPathService.CreateCapturePath(
                directory, replayName, true);
            try
            {
                await _replayExporter.ExportLastAsync(
                    finishedSession.OutputPath,
                    outputPath,
                    TimeSpan.FromSeconds(settings.ReplaySeconds));
            }
            finally
            {
                DeleteIfExists(finishedSession.OutputPath);
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

        _gate.Dispose();
    }

    private static RecordingSession CreateSession(
        AppSettings settings,
        CaptureTarget target,
        string outputPath,
        bool microphoneMuted)
    {
        var outputAudio = string.IsNullOrWhiteSpace(settings.OutputAudioDeviceId)
            ? LoopbackAudioSource.Default
            : new LoopbackAudioSource(settings.OutputAudioDeviceId);
        var microphone = string.IsNullOrWhiteSpace(settings.InputAudioDeviceId)
            ? CaptureAudioSource.Default
            : new CaptureAudioSource(settings.InputAudioDeviceId);
        if (microphone is not null)
        {
            microphone.Volume = microphoneMuted ? 0f : 1f;
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
                IsHardwareEncodingEnabled = true,
                Quality = 92,
                Bitrate = settings.FramesPerSecond >= 120 ? 36_000_000 : 20_000_000,
                Encoder = new H264VideoEncoder
                {
                    EncoderProfile = H264Profile.High,
                    BitrateMode = H264BitrateControlMode.Quality
                },
                IsMp4FastStartEnabled = true
            },
            OutputOptions = new OutputOptions { RecorderMode = RecorderMode.Video },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = true },
            LogOptions = new LogOptions { IsLogEnabled = false }
        };
        options.SourceOptions.RecordingSources.Add(target.Source);

        return new RecordingSession(Recorder.CreateRecorder(options), microphone, outputPath, target.Name);
    }

    private void ApplyMicrophoneState(RecordingSession? session)
    {
        if (session?.Microphone is null)
        {
            return;
        }

        session.Microphone.Volume = _microphoneMuted ? 0f : 1f;
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
            string captureName)
        {
            Recorder = recorder;
            Microphone = microphone;
            OutputPath = outputPath;
            CaptureName = captureName;
            Recorder.OnRecordingComplete += OnRecordingComplete;
            Recorder.OnRecordingFailed += OnRecordingFailed;
        }

        public Recorder Recorder { get; }
        public CaptureAudioSource? Microphone { get; }
        public string OutputPath { get; }
        public string CaptureName { get; }

        public void Start()
        {
            Recorder.Record(OutputPath);
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
}
