using System.Text.Json;
using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureDiagnosticWriterTests
{
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "clipalka-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FinalizesStereoPcmHeaderAndPreservesSamples()
    {
        var path = NewPath();
        await using (var writer = new CaptureDiagnosticWriter(path))
        {
            Assert.True(writer.Log("start", new { fps = 120 }));
            Assert.True(writer.Audio("session-microphone", [1, 2, 3, 4, 5, 6, 7, 8]));
        }
        var wav = await File.ReadAllBytesAsync(Path.Combine(path, "session-microphone.wav"));
        Assert.Equal(52, wav.Length);
        Assert.Equal(44, BitConverter.ToInt32(wav, 4));
        Assert.Equal(48000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(2, BitConverter.ToInt16(wav, 22));
        Assert.Equal(8, BitConverter.ToInt32(wav, 40));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, wav[44..]);
        using var line = JsonDocument.Parse((await File.ReadAllLinesAsync(Path.Combine(path, "events.jsonl")))[0]);
        Assert.Equal("start", line.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task RejectsTraversalMalformedAndOversizedPacketsAndLateWrites()
    {
        var writer = new CaptureDiagnosticWriter(NewPath());
        Assert.Throws<ArgumentException>(() => writer.Audio("../outside", [0, 0, 0, 0]));
        Assert.False(writer.Audio("mic", [1, 2, 3]));
        Assert.False(writer.Audio("mic", new byte[256 * 1024 + 4]));
        await writer.DisposeAsync();
        Assert.False(writer.Log("late", new { }));
        await writer.DisposeAsync();
        Assert.Equal(3, writer.DroppedEntries);
    }

    [Fact]
    public async Task StorageFailureDoesNotThrowIntoCaptureProducer()
    {
        var path = NewPath();
        Directory.CreateDirectory(Path.Combine(path, "events.jsonl"));
        var writer = new CaptureDiagnosticWriter(path);
        writer.Log("test", new { });
        await writer.DisposeAsync();
        Assert.NotNull(writer.Error);
    }

    [Fact]
    public async Task ConcurrentProducersAccountForEveryAcceptedOrDroppedEvent()
    {
        var path = NewPath();
        var writer = new CaptureDiagnosticWriter(path, capacity: 8);
        var accepted = 0;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(producer => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                if (writer.Log("frame", new { producer, frame = i })) Interlocked.Increment(ref accepted);
        })));
        await writer.DisposeAsync();
        Assert.Equal(800, accepted + writer.DroppedEntries);
        Assert.Equal(accepted, (await File.ReadAllLinesAsync(Path.Combine(path, "events.jsonl"))).Length);
        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(path, "writer-summary.json")));
        Assert.Equal(writer.DroppedEntries, summary.RootElement.GetProperty("droppedEntries").GetInt64());
    }

    [Fact]
    public async Task OwnsQueuedAudioAndKeepsSourcesSeparate()
    {
        var path = NewPath();
        var writer = new CaptureDiagnosticWriter(path);
        byte[] microphone = [1, 0, 1, 0];
        Assert.True(writer.Audio("mic", microphone));
        microphone[0] = 99;
        Assert.True(writer.Audio("mix", [2, 0, 2, 0]));
        await writer.DisposeAsync();
        Assert.Equal(1, (await File.ReadAllBytesAsync(Path.Combine(path, "mic.wav")))[44]);
        Assert.Equal(2, (await File.ReadAllBytesAsync(Path.Combine(path, "mix.wav")))[44]);
    }
}
