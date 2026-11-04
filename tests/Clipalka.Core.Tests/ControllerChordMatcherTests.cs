using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class ControllerChordMatcherTests
{
    private readonly ControllerChordMatcher _matcher = new();
    private static readonly int[] Chord = [4, 8];
    private bool Read(int ms, int[] pressed, bool enabled = true) =>
        _matcher.Update(Chord, pressed, TimeSpan.FromMilliseconds(ms), enabled);

    [Fact]
    public void GameplayButtonHeld_StillSavesTheAssignedChord()
    {
        Read(0, []);
        Assert.True(Read(10, [2, 4, 8]));
        Assert.False(Read(500, [2, 4, 8]));
    }

    [Fact]
    public void UnrelatedButtonCanStayHeld_BeforeAndBetweenClips()
    {
        Read(0, [2]);
        Assert.False(Read(10, [2, 4]));
        Assert.True(Read(30, [2, 4, 8]));
        Assert.False(Read(50, [2]));
        Assert.True(Read(70, [2, 4, 8]));
    }

    [Fact]
    public void AShortChordPress_SavesOnceWithoutARequiredHold()
    {
        Read(0, []);
        Assert.True(Read(10, Chord));
        Assert.False(Read(50, []));
    }

    [Fact]
    public void HeldAtStartup_DoesNotTriggerUntilReleased()
    {
        Assert.False(Read(0, Chord));
        Assert.False(Read(500, Chord));
        Assert.False(Read(600, []));
        Assert.True(Read(700, Chord));
        Assert.False(Read(1100, Chord));
    }

    [Fact]
    public void LongHold_FiresOnce_ThenRequiresAssignedButtonsReleased()
    {
        Read(0, []);
        Assert.True(Read(10, Chord));
        Assert.False(Read(1000, Chord));
        Assert.False(Read(1100, [4]));
        Assert.False(Read(1200, Chord));
        Read(1500, []);
        Assert.True(Read(1600, Chord));
        Assert.False(Read(2000, Chord));
    }

    [Fact]
    public void RegainingFocusOrReconnect_DoesNotFireHeldChord()
    {
        Read(0, []);
        Assert.True(Read(10, Chord));
        Assert.False(Read(500, Chord, false));
        Assert.False(Read(1000, Chord));
        Read(1100, []);
        Assert.True(Read(1200, Chord));
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
