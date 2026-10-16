using System.Globalization;
using FF14Accessibility;

namespace Localization.Tests;

public sealed class LanguageSelectionTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentUICulture;
    private readonly LanguageMode _mode = Loc.Mode;

    [Theory]
    [InlineData("ru-RU", LanguageMode.English, false, false)]
    [InlineData("ru-RU", LanguageMode.German, true, false)]
    [InlineData("de-DE", LanguageMode.Russian, false, true)]
    [InlineData("en-US", LanguageMode.Russian, false, true)]
    [InlineData("de-DE", LanguageMode.English, false, false)]
    public void ExplicitChoiceOverridesWindowsLanguage(string culture, LanguageMode mode, bool german, bool russian)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        Loc.Mode = mode;
        Assert.Equal(german, Loc.IsGerman);
        Assert.Equal(russian, Loc.IsRussian);
    }

    [Theory]
    [InlineData("ru-RU", false, true)]
    [InlineData("de-DE", true, false)]
    [InlineData("en-US", false, false)]
    [InlineData("fr-FR", false, false)]
    public void AutomaticChoiceFollowsWindowsLanguage(string culture, bool german, bool russian)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        Loc.Mode = LanguageMode.Auto;
        Assert.Equal(german, Loc.IsGerman);
        Assert.Equal(russian, Loc.IsRussian);
    }

    [Theory]
    [InlineData(" RU ", LanguageMode.Russian)]
    [InlineData("РУССКИЙ", LanguageMode.Russian)]
    [InlineData("russisch", LanguageMode.Russian)]
    [InlineData("Deutsch", LanguageMode.German)]
    [InlineData("EN", LanguageMode.English)]
    [InlineData("auto", LanguageMode.Auto)]
    public void LanguageCommandsKeepAllSupportedModes(string argument, LanguageMode expected)
        => Assert.Equal(expected, Loc.ParseArg(argument));

    [Fact]
    public void UnknownLanguageDoesNotSilentlySelectAnotherMode()
        => Assert.Null(Loc.ParseArg("unknown"));

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _culture;
        Loc.Mode = _mode;
    }
}
