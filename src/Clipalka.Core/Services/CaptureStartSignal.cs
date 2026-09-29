namespace Clipalka.Core.Services;

/// <summary>A successful native start call is not proof that capture is producing frames.</summary>
public sealed class CaptureStartSignal
{
    private readonly TaskCompletionSource<string?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void FrameArrived() => _ready.TrySetResult(null);
    public void Stopped(string? error = null) => _ready.TrySetResult(error ?? "Захват завершился до первого кадра.");
    public async Task WaitAsync(TimeSpan timeout)
    {
        var error = await _ready.Task.WaitAsync(timeout);
        if (error is not null) throw new InvalidOperationException(error);
    }
}
