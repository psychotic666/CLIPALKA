using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class ControllerChordMatcherTests
{
    private readonly ControllerChordMatcher _matcher = new();
    private static readonly int[] Chord = [4, 8];
    private bool Read(int ms, int[] pressed, bool enabled = true) =>
        _matcher.Update(Chord, pressed, TimeSpan.FromMilliseconds(ms), enabled);

    [Fact]
    public void HeldAtStartup_DoesNotTriggerUntilReleased()
    {
        Assert.False(Read(0, Chord));
        Assert.False(Read(500, Chord));
        Assert.False(Read(600, []));
        Assert.False(Read(700, Chord));
        Assert.True(Read(1100, Chord));
    }

    [Fact]
    public void LongHold_FiresOnce_ThenRequiresFullRelease()
    {
        Read(0, []);
        Assert.False(Read(10, Chord));
        Assert.False(Read(409, Chord));
        Assert.True(Read(410, Chord));
        Assert.False(Read(1000, Chord));
        Assert.False(Read(1100, [4]));
        Assert.False(Read(1200, Chord));
        Read(1500, []);
        Read(1600, Chord);
        Assert.True(Read(2000, Chord));
    }

    [Fact]
    public void ExtraGameplayButton_InterruptsHold()
    {
        Read(0, []);
        Read(10, Chord);
        Assert.False(Read(350, [4, 8, 2]));
        Assert.False(Read(400, Chord));
        Assert.False(Read(799, Chord));
        Assert.True(Read(800, Chord));
    }

    [Fact]
    public void ShortPress_DoesNotSaveClip()
    {
        Read(0, []);
        Read(10, Chord);
        Assert.False(Read(200, []));
        Assert.False(Read(600, []));
    }

    [Fact]
    public void RegainingFocusOrReconnect_DoesNotFireHeldChord()
    {
        Read(0, []);
        Read(10, Chord);
        Assert.False(Read(500, Chord, false));
        Assert.False(Read(1000, Chord));
        Read(1100, []);
        Read(1200, Chord);
        Assert.True(Read(1600, Chord));
        _matcher.Reset();
        Assert.False(Read(2500, Chord));
    }

    [Fact]
    public void DisabledOrPartialChord_NeverFires()
    {
        Read(0, []);
        Assert.False(Read(10, [4]));
        Assert.False(Read(1000, [4]));
        Assert.False(Read(2000, Chord, false));
        Assert.False(_matcher.Update([], Chord, TimeSpan.FromSeconds(4), true));
    }
}
