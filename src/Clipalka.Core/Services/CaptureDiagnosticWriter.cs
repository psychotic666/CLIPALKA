using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Clipalka.Core.Services;

/// <summary>Bounded, non-blocking producer queue. File I/O never runs in capture callbacks.</summary>
public sealed class CaptureDiagnosticWriter : IAsyncDisposable
{
    private readonly Channel<Entry> _queue;
    private readonly Task _worker;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _dropped;
    private string? _error;
    public string DirectoryPath { get; }
    public long DroppedEntries => Interlocked.Read(ref _dropped);
    public string? Error => Volatile.Read(ref _error);

    public CaptureDiagnosticWriter(string directory, int capacity = 256)
    {
        DirectoryPath = Path.GetFullPath(directory);
        Directory.CreateDirectory(DirectoryPath);
        _queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
        _worker = Task.Run(WriteAsync);
    }

    public bool Log(string kind, object details) => Enqueue(new Entry(
        JsonSerializer.Serialize(new { kind, utc = DateTimeOffset.UtcNow, elapsedMs = _clock.Elapsed.TotalMilliseconds, details }), null, null));

    // Input is the SRL audio preview format: stereo PCM16, 48 kHz, after gain/mono conversion.
    public bool Audio(string name, byte[] pcm)
    {
        if (name.Length is < 1 or > 100 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new ArgumentException("Audio name must be a simple file stem.", nameof(name));
        if (pcm.Length == 0) return true;
        if (pcm.Length > 256 * 1024 || pcm.Length % 4 != 0)
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }
        // Managed SRL arrays are owned per callback. Copy to make the writer's ownership explicit.
        return Enqueue(new Entry(null, name, (byte[])pcm.Clone()));
    }

    private bool Enqueue(Entry entry)
    {
        if (Error is null && _queue.Writer.TryWrite(entry)) return true;
        Interlocked.Increment(ref _dropped);
        return false;
    }

    private async Task WriteAsync()
    {
        var waves = new Dictionary<string, PcmWave>();
        try
        {
            using var log = new StreamWriter(Path.Combine(DirectoryPath, "events.jsonl"), false, Encoding.UTF8);
            await foreach (var entry in _queue.Reader.ReadAllAsync())
            {
                if (entry.Json is not null) await log.WriteLineAsync(entry.Json);
                else if (entry.AudioName is not null && entry.Pcm is not null)
                {
                    if (!waves.TryGetValue(entry.AudioName, out var wave))
                    {
                        wave = new PcmWave(Path.Combine(DirectoryPath, entry.AudioName + ".wav"));
                        waves.Add(entry.AudioName, wave);
                    }
                    wave.Write(entry.Pcm);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Volatile.Write(ref _error, e.Message);
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out _)) Interlocked.Increment(ref _dropped);
        }
        finally
        {
            foreach (var wave in waves.Values)
            {
                try { wave.Dispose(); }
                catch (IOException e) { Volatile.Write(ref _error, e.Message); }
            }
            try
            {
                await File.WriteAllTextAsync(Path.Combine(DirectoryPath, "writer-summary.json"),
                    JsonSerializer.Serialize(new { droppedEntries = DroppedEntries, error = Error,
                        note = "Dropped entries mean incomplete diagnostics; WAV files concatenate received PCM packets. See callback timestamps in events.jsonl." }));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Volatile.Write(ref _error, e.Message); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _worker;
    }

    private sealed record Entry(string? Json, string? AudioName, byte[]? Pcm);

    private sealed class PcmWave : IDisposable
    {
        private readonly BinaryWriter _writer;
        public PcmWave(string path)
        {
            _writer = new BinaryWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            _writer.Write(Encoding.ASCII.GetBytes("RIFF")); _writer.Write(0);
            _writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16);
            _writer.Write((short)1); _writer.Write((short)2); _writer.Write(48000);
            _writer.Write(192000); _writer.Write((short)4); _writer.Write((short)16);
            _writer.Write(Encoding.ASCII.GetBytes("data")); _writer.Write(0);
        }
        public void Write(byte[] pcm) => _writer.Write(pcm);
        public void Dispose()
        {
            try
            {
                var length = checked((int)_writer.BaseStream.Length);
                _writer.Seek(4, SeekOrigin.Begin); _writer.Write(length - 8);
                _writer.Seek(40, SeekOrigin.Begin); _writer.Write(length - 44);
            }
            finally { _writer.Dispose(); }
        }
    }
}
