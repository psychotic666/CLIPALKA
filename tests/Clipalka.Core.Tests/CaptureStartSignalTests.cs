using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class CaptureStartSignalTests
{
    [Fact]
    public async Task WaitsUntilActualFrameAndIgnoresLaterStop()
    {
        var signal = new CaptureStartSignal();
        var pending = signal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(pending.IsCompleted);
        signal.FrameArrived();
        signal.Stopped();
        await pending;
    }

    [Fact]
    public async Task FailedStartDoesNotBecomeReadyOnLateFrame()
    {
        var signal = new CaptureStartSignal();
        signal.Stopped("Audio initialization failed");
        signal.FrameArrived();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal("Audio initialization failed", error.Message);
    }

    [Fact]
    public async Task TimesOutInsteadOfWaitingForever()
    {
        var signal = new CaptureStartSignal();
        await Assert.ThrowsAsync<TimeoutException>(() => signal.WaitAsync(TimeSpan.FromMilliseconds(10)));
    }
}
