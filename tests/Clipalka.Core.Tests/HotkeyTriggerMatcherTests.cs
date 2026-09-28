using Clipalka.Core.Models;
using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class HotkeyTriggerMatcherTests
{
    [Fact]
    public void IsMatch_AcceptsShiftZWithoutRequiringExclusiveRegistration()
    {
        var binding = new HotkeyBinding(HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat, 0x5A);

        var matches = HotkeyTriggerMatcher.IsMatch(binding, 0x5A, HotkeyModifiers.Shift);

        Assert.True(matches);
    }
}
