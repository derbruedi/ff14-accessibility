using System.Globalization;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace SpeechLocalization.Tests;

public sealed class MainMenuTests : IDisposable
{
    private readonly LanguageMode _mode = Loc.Mode;
    private readonly CultureInfo _culture = CultureInfo.CurrentUICulture;
    public void Dispose() { Loc.Mode = _mode; CultureInfo.CurrentUICulture = _culture; }

    [Theory]
    [InlineData(1u, "Character", "Персонаж")]
    [InlineData(2u, "Duty", "Задания и миссии")]
    [InlineData(3u, "Logs", "Журналы")]
    [InlineData(4u, "Travel", "Путешествия")]
    [InlineData(5u, "Party", "Группа")]
    [InlineData(6u, "Social", "Общение")]
    [InlineData(7u, "System", "Система")]
    public void RussianNamesDescribeCategoriesRatherThanIndividualCommands(uint id, string original, string expected)
    {
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal(expected, AccessibilityStrings.MainMenuCategoryName(id, original));
    }

    [Theory]
    [InlineData(LanguageMode.German, "Karte & Transport")]
    [InlineData(LanguageMode.English, "Travel")]
    public void OtherModesKeepTheGameCategoryName(LanguageMode mode, string original)
    {
        Loc.Mode = mode;
        Assert.Equal(original, AccessibilityStrings.MainMenuCategoryName(4, original));
    }

    [Fact]
    public void UnknownAndEmptyCategoriesAreNotGuessed()
    {
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal("Future category", AccessibilityStrings.MainMenuCategoryName(99, "Future category"));
        Assert.Equal("", AccessibilityStrings.MainMenuCategoryName(7, ""));
        Assert.Equal(" ", AccessibilityStrings.MainMenuCategoryName(3, " "));
    }

    [Fact]
    public void SwitchingLanguageDoesNotKeepAnOldRussianLabel()
    {
        Loc.Mode = LanguageMode.Auto;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        Assert.Equal("Журналы", AccessibilityStrings.MainMenuCategoryName(3, "Logs"));
        Loc.Mode = LanguageMode.English;
        Assert.Equal("Logs", AccessibilityStrings.MainMenuCategoryName(3, "Logs"));
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal("Система", AccessibilityStrings.MainMenuCategoryName(7, "System"));
    }
}
