using System.Reflection;
using FF14Accessibility.Services;

namespace Regression.Tests;

public class VitalPercentTests
{
    [Theory]
    [InlineData(0U, 0U, 0)]
    [InlineData(1U, 0U, 0)]
    [InlineData(0U, 100U, 0)]
    [InlineData(1U, 3U, 33)]
    [InlineData(100U, 100U, 100)]
    [InlineData(110U, 100U, 100)]
    [InlineData(100000000U, 200000000U, 50)]
    [InlineData(4294967295U, 4294967295U, 100)]
    public void HealthPercentHandlesBossHpWithoutOverflow(uint current, uint max, int expected)
    {
        Assert.Equal(expected, VitalPercent.Floor(current, max));
        // Exercise the real combat threshold path as well as the shared helper.
        Assert.Equal(expected, typeof(CombatService).GetMethod("HpPercent", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [current, max]));
    }
}
