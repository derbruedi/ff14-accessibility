using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

[Collection("Language")]
public sealed class ArmouryItemSpeechTests : IDisposable
{
    private readonly LanguageMode _previous = Loc.Mode;
    public ArmouryItemSpeechTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() => Loc.Mode = _previous;

    [Theory]
    [InlineData(1891u, false, false)]
    [InlineData(1001895u, true, false)]
    [InlineData(3539u, false, true)]
    [InlineData(501895u, false, false)]
    public void LoggedGearGetsFullParametersInsteadOfOnlyTheEquipLevel(uint rawId, bool hq, bool materia)
    {
        var baseId = ItemSlotService.GetBaseItemId(rawId);
        var nameId = 0u;
        var gearId = 0u;
        var label = ArmouryListAccess.DescribeLabel(rawId, 1, hq, materia,
            id => { nameId = id; return "Короткий лук из ясеня"; },
            (id, brief) =>
            {
                gearId = id;
                return brief ? "Нужен уровень 15" :
                    "Нужен уровень 15, можно надеть, Уровень предмета 15, Физический урон 17, Ловкость плюс 2";
            });
        Assert.Equal(baseId, nameId);
        Assert.Equal(baseId, gearId);
        Assert.Contains("Физический урон 17", label);
        Assert.Contains("Ловкость плюс 2", label);
        Assert.Equal(hq, label.Contains(AccessibilityStrings.HighQuality));
        Assert.Equal(hq || materia, label.Contains(AccessibilityStrings.ArmouryListBaseStats));
    }

    [Theory]
    [InlineData(LanguageMode.Russian)]
    [InlineData(LanguageMode.English)]
    [InlineData(LanguageMode.German)]
    public void NonGearAndEmptyDescriptionStillHaveANameAndQuantity(LanguageMode language)
    {
        Loc.Mode = language;
        var label = ArmouryListAccess.DescribeLabel(100, 2, false, false,
            _ => "Crystal", (_, _) => string.Empty);
        Assert.Contains("Crystal", label);
        Assert.Contains("2", label);
        Assert.DoesNotContain(AccessibilityStrings.ArmouryListBaseStats, label);
        Assert.False(label.EndsWith(", "));
    }
}
