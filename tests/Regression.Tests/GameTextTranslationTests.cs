using System.Globalization;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

[Collection("Language")]
public sealed class GameTextTranslationTests : IDisposable
{
    private readonly LanguageMode _mode = Loc.Mode;
    private readonly bool _translation = Loc.TranslateItemsAndActions;
    private readonly CultureInfo _culture = CultureInfo.CurrentUICulture;

    [Fact]
    public void DisablingAndRestoringSavesTheChoiceAndKeepsRussianPromptsAndPersonalKeys()
    {
        Loc.Mode = LanguageMode.Russian;
        Loc.TranslateItemsAndActions = true;
        var config = new Configuration { Language = LanguageMode.Russian, KeyReadUI = "Alt+F10" };
        var saves = new List<bool>();
        var reply = GameTextTranslation.HandleCommand("translate off", config,
            () => saves.Add(config.TranslateItemsAndActions));
        Assert.Contains("отключён", reply);
        Assert.False(config.TranslateItemsAndActions);
        Assert.False(Loc.IsRussianItemActionText);
        Assert.True(Loc.IsRussian);
        Assert.Equal(LanguageMode.Russian, config.Language);
        Assert.Equal("Alt+F10", config.KeyReadUI);
        Assert.Contains("Неизвестная команда", AccessibilityStrings.UnknownCommand);
        Assert.False(GameTextTranslation.ShouldTranslate("Item"));
        Assert.False(GameTextTranslation.ShouldTranslate("ActionTransient"));
        Assert.True(GameTextTranslation.ShouldTranslate("ClassJob"));

        reply = GameTextTranslation.HandleCommand("  ПЕРЕВОД\tВКЛ  ", config,
            () => saves.Add(config.TranslateItemsAndActions));
        Assert.Contains("включён", reply);
        Assert.True(Loc.IsRussianItemActionText);
        Assert.True(GameTextTranslation.ShouldTranslate("Item"));
        Assert.Equal(new[] { false, true }, saves);
    }

    [Theory]
    [InlineData("translate")]
    [InlineData("перевод")]
    [InlineData("translate of")]
    [InlineData("translate off extra")]
    [InlineData("перевод неизвестно")]
    public void StatusAndInvalidArgumentsDoNotChangeOrSaveSettings(string command)
    {
        Loc.Mode = LanguageMode.Russian;
        Loc.TranslateItemsAndActions = false;
        var config = new Configuration { TranslateItemsAndActions = false };
        Assert.NotNull(GameTextTranslation.HandleCommand(command, config,
            () => throw new Exception("Read-only commands must not save.")));
        Assert.False(config.TranslateItemsAndActions);
        Assert.False(Loc.TranslateItemsAndActions);
    }

    [Theory]
    [InlineData("translateoff")]
    [InlineData("переводчик выкл")]
    [InlineData("lang ru")]
    [InlineData("")]
    public void UnrelatedCommandsStillReachTheirOriginalHandler(string command)
    {
        var config = new Configuration();
        Assert.Null(GameTextTranslation.HandleCommand(command, config,
            () => throw new Exception("Unrelated commands must not save.")));
        Assert.True(config.TranslateItemsAndActions);
    }

    [Fact]
    public void ExistingSettingsDefaultToTranslationAndDisabledChoiceSurvivesReload()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var old = JsonSerializer.Deserialize<Configuration>("{\"Version\":17,\"Language\":3,\"KeyReadUI\":\"Alt+F10\"}", options)!;
        Assert.True(old.TranslateItemsAndActions);
        GameTextTranslation.HandleCommand("перевод выкл", old, () => { });
        var loaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(old, options), options)!;
        Loc.Mode = loaded.Language;
        Loc.TranslateItemsAndActions = loaded.TranslateItemsAndActions;
        Assert.False(Loc.IsRussianItemActionText);
        Assert.True(Loc.IsRussian);
        Assert.Equal("Alt+F10", loaded.KeyReadUI);
    }

    [Theory]
    [InlineData("ru-RU", LanguageMode.Auto, true)]
    [InlineData("en-US", LanguageMode.Auto, false)]
    [InlineData("ru-RU", LanguageMode.English, false)]
    [InlineData("ru-RU", LanguageMode.German, false)]
    public void EnablingTranslationRespectsTheAnnouncementLanguage(string culture, LanguageMode mode, bool russian)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        Loc.Mode = mode;
        var config = new Configuration { Language = mode, TranslateItemsAndActions = false };
        GameTextTranslation.HandleCommand("translate ON", config, () => { });
        Assert.Equal(russian, GameTextTranslation.ShouldTranslate("CraftAction"));
        Assert.Equal(mode, Loc.Mode);
        Assert.Equal(mode, config.Language);
    }

    public void Dispose()
    {
        Loc.Mode = _mode;
        Loc.TranslateItemsAndActions = _translation;
        CultureInfo.CurrentUICulture = _culture;
    }
}
