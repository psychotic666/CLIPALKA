namespace Clipalka.Core.Services;

// A chord is an edge, not an exact snapshot of every gameplay button.
public sealed class ControllerChordMatcher
{
    private bool _armed;

    public void Reset() => _armed = false;

    public bool Update(IReadOnlyCollection<int> chord, IReadOnlyCollection<int> pressed, TimeSpan now, bool enabled)
    {
        if (!enabled || chord.Count == 0) { Reset(); return false; }
        if (!chord.Any(pressed.Contains)) { _armed = true; return false; }
        if (!_armed) return false;
        if (!chord.All(pressed.Contains))
        {
            return false;
        }
        Reset(); // Release the assigned buttons before another clip; gameplay inputs may stay held.
        return true;
    }
}
