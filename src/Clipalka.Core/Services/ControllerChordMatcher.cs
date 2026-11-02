namespace Clipalka.Core.Services;

// Uses monotonic time supplied by the caller, not wall-clock time.
public sealed class ControllerChordMatcher
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(400);
    private bool _armed;
    private TimeSpan? _pressedAt;

    public void Reset() { _armed = false; _pressedAt = null; }

    public bool Update(IReadOnlyCollection<int> chord, IReadOnlyCollection<int> pressed, TimeSpan now, bool enabled)
    {
        if (!enabled || chord.Count == 0) { Reset(); return false; }
        if (pressed.Count == 0) { _armed = true; _pressedAt = null; return false; }
        if (!_armed) return false;
        if (pressed.Count != chord.Count || !chord.All(pressed.Contains))
        {
            _pressedAt = null;
            return false;
        }
        _pressedAt ??= now;
        if (now - _pressedAt.Value < HoldDuration) return false;
        Reset(); // A full release is required before another clip can be requested.
        return true;
    }
}
