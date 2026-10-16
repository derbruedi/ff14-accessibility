using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class ActionMenuTests
{
    [Theory]
    [InlineData("Aspect Mastery III\nУр. 35", "Aspect Mastery III", 35)]
    [InlineData("Maim and Mend II Ур. 40", "Maim and Mend II", 40)]
    [InlineData("Firestarter\nУр. 42", "Firestarter", 42)]
    [InlineData("Enhanced Swiftcast Lv. 94", "Enhanced Swiftcast", 94)]
    [InlineData("Enhanced Addle, Level 98", "Enhanced Addle", 98)]
    [InlineData("Feuer St. 2", "Feuer", 2)]
    [InlineData("Базовая обработка", "Базовая обработка", 0)]
    [InlineData("Fire II", "Fire II", 0)]
    public void RecordedTraitLabelsRetainRomanNumeralsAndExtractOnlyExplicitLevels(string label, string name, int level)
        => Assert.Equal((name, level), ActionMenuText.ParseLabel(label));

    [Theory]
    [InlineData("Fire", "Fire II", false)]
    [InlineData("Enhanced Addle", "Addle", false)]
    [InlineData("Firestarter", "Firestarter Ур. 42", true)]
    [InlineData("Firestarter Ур. 42", "Firestarter Ур. 43", false)]
    [InlineData("", "", false)]
    public void DelayedPanelMustMatchExactly(string panel, string slot, bool expected)
        => Assert.Equal(expected, ActionMenuText.Matches(panel, slot));

    [Fact]
    public void AmbiguousTraitsCannotBorrowAnotherJobsDescription()
    {
        var fallback = new ActionMenuText.Entry("Raw", "Panel");
        Assert.Equal(fallback, ActionMenuText.Consensus([new("Усиление", "10%"), new("Усиление", "20%")], fallback));
        Assert.Equal(new("Усиление", "10%"), ActionMenuText.Consensus([new("Усиление", "10%"), new("Усиление", "10%")], fallback));
        Assert.Equal(fallback, ActionMenuText.Consensus([], fallback));
    }
}
