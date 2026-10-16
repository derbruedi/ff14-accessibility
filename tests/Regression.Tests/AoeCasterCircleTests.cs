using FF14Accessibility.Services;

namespace Regression.Tests;

public class AoeCasterCircleTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(0xE0000000UL)]
    [InlineData(123UL)]
    public void PlaincrackerAndAqueousDischargeSelfActionDataAcceptsTheirCaster(ulong target)
        => Assert.True(CombatService.CircleOnCaster(123, target, false, true, false, 0));

    [Theory]
    [InlineData(0UL)]
    [InlineData(0xE0000000UL)]
    [InlineData(123UL)]
    public void AcornArmageddonGroundTargetDataNeverPretendsToBeCasterCentered(ulong target)
        => Assert.False(CombatService.CircleOnCaster(123, target, true, false, false, 25));

    [Theory]
    [InlineData(456UL, false, true, false, 0)]
    [InlineData(0UL, false, false, false, 0)]
    [InlineData(0UL, false, true, true, 0)]
    [InlineData(0UL, false, true, false, 25)]
    public void AnAbsentOrDifferentTargetCannotAuthorizeAGuessedCenter(ulong target, bool area,
        bool self, bool hostile, int range)
        => Assert.False(CombatService.CircleOnCaster(123, target, area, self, hostile, range));
}
