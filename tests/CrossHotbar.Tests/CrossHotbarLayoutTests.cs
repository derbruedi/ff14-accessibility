using FF14Accessibility.Services;

namespace FF14Accessibility.Tests;

public sealed class CrossHotbarLayoutTests
{
    [Fact]
    public void EveryCrossSetIncludesTheLastFourButtonsMissingFromKeyboardBars()
    {
        for (var bar = 10; bar <= 17; bar++)
            for (var slot = 0; slot < 16; slot++)
                Assert.True(CrossHotbarLayout.IsValidTarget(bar, slot));
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(18, 0)]
    [InlineData(10, -1)]
    [InlineData(17, 16)]
    public void RejectsDestinationsOutsideNormalCrossSets(int bar, int slot)
        => Assert.False(CrossHotbarLayout.IsValidTarget(bar, slot));

    [Fact]
    public void PickerWrapKeepsKeyboardTargetsAccessibleInBothDirections()
    {
        Assert.Equal(8, CrossHotbarLayout.MoveChoice(0, -1));
        Assert.Equal(0, CrossHotbarLayout.MoveChoice(8, 1));
        Assert.Equal(8, CrossHotbarLayout.MoveChoice(7, 1));
        Assert.Equal(7, CrossHotbarLayout.MoveChoice(8, -1));
    }

    [Theory]
    [InlineData(0, "LDL")]
    [InlineData(7, "LAD")]
    [InlineData(8, "RDL")]
    [InlineData(15, "RAD")]
    public void SlotLabelsDoNotSwapTriggerHalves(int slot, string code)
        => Assert.Equal(code, CrossHotbarLayout.SlotCode(slot));

    [Fact]
    public void OutOfRangeSlotCannotBeGivenAValidButtonLabel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CrossHotbarLayout.SlotCode(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CrossHotbarLayout.SlotCode(16));
        Assert.Equal(16, Enumerable.Range(0, 16).Select(CrossHotbarLayout.SlotCode).Distinct().Count());
    }
}
