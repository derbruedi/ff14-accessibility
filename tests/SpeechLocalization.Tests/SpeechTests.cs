using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SpeechLocalization.Tests;

public sealed class SpeechTests : IDisposable
{
    private readonly LanguageMode _mode = Loc.Mode;
    private readonly CultureInfo _culture = CultureInfo.CurrentUICulture;
    public void Dispose() { Loc.Mode = _mode; CultureInfo.CurrentUICulture = _culture; }

    private static Dictionary<uint, (string De, string En, string BriefDe, string BriefEn)> Table(Type type)
        => (Dictionary<uint, (string, string, string, string)>)type.GetField("Text", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    [Fact]
    public void EveryMeasuredShapeHasRussianShortAndFullSpeech()
    {
        Loc.Mode = LanguageMode.Russian;
        var table = Table(typeof(CharaMakeShapeText));
        Assert.True(table.Count > 1000);
        foreach (var (key, text) in table)
        {
            var face = key / 1000;
            var field = key % 1000 / 10;
            var entry = (int)(key % 10);
            var full = CharaMakeShapeText.Describe(face, field, entry);
            var brief = CharaMakeShapeText.Summarize(face, field, entry);
            Assert.Matches("[А-Яа-яЁё]", full!);
            Assert.Matches("[А-Яа-яЁё]", brief!);
            Assert.DoesNotMatch("[A-Za-z]", full!);
            Assert.DoesNotMatch("[A-Za-z]", brief!);
            Assert.Equal(text.En.Split(", ").Length, full!.Split(", ").Length);
        }
    }

    [Fact]
    public void EveryAuthoredFaceIsTranslatedWithItsOriginalDetails()
    {
        Loc.Mode = LanguageMode.Russian;
        var faces = Table(typeof(CharaMakeIconText)).Where(x => x.Value.BriefEn.Length == 0).ToList();
        Assert.Equal(132, faces.Count);
        foreach (var (id, original) in faces)
        {
            var text = CharaMakeIconText.Describe(id)!;
            Assert.Matches("[А-Яа-яЁё]", text);
            Assert.DoesNotMatch("[A-Za-z]", text);
            Assert.Equal(text, CharaMakeIconText.Summarize(id));
            Assert.Equal(original.En.Split(", ").Length, text.Split(", ").Length);
        }
    }

    [Fact]
    public void EveryAppearanceIconHasBothRussianReadouts()
    {
        Loc.Mode = LanguageMode.Russian;
        foreach (var (id, _) in Table(typeof(CharaMakeIconText)))
        {
            foreach (var text in new[] { CharaMakeIconText.Describe(id), CharaMakeIconText.Summarize(id) })
            {
                Assert.Matches("[А-Яа-яЁё]", text!);
                Assert.DoesNotMatch("[A-Za-z]", text!);
            }
        }
    }

    [Theory]
    [InlineData(LanguageMode.English)]
    [InlineData(LanguageMode.German)]
    public void OtherLanguagesKeepEveryOriginalDescription(LanguageMode mode)
    {
        Loc.Mode = mode;
        foreach (var (id, text) in Table(typeof(CharaMakeIconText)))
        {
            var full = mode == LanguageMode.German ? text.De : text.En;
            var brief = mode == LanguageMode.German ? text.BriefDe : text.BriefEn;
            Assert.Equal(full, CharaMakeIconText.Describe(id));
            Assert.Equal(brief.Length > 0 ? brief : full, CharaMakeIconText.Summarize(id));
        }
        foreach (var (key, text) in Table(typeof(CharaMakeShapeText)))
        {
            Assert.Equal(mode == LanguageMode.German ? text.De : text.En,
                CharaMakeShapeText.Describe(key / 1000, key % 1000 / 10, (int)(key % 10)));
            Assert.Equal(mode == LanguageMode.German ? text.BriefDe : text.BriefEn,
                CharaMakeShapeText.Summarize(key / 1000, key % 1000 / 10, (int)(key % 10)));
        }
    }

    [Fact]
    public void SwitchingLanguageUsesCurrentSelectionAndUnknownEntriesStayUnknown()
    {
        Loc.Mode = LanguageMode.English;
        var english = CharaMakeShapeText.Describe(131101, 18, 2);
        Loc.Mode = LanguageMode.Russian;
        var russian = CharaMakeShapeText.Describe(131101, 18, 2);
        Assert.NotEqual(english, russian);
        Loc.Mode = LanguageMode.Auto;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        Assert.Equal(russian, CharaMakeShapeText.Describe(131101, 18, 2));
        Loc.Mode = LanguageMode.English;
        Assert.Equal(english, CharaMakeShapeText.Describe(131101, 18, 2));
        Assert.Null(CharaMakeShapeText.Describe(0, 18, 2));
        Assert.Null(CharaMakeShapeText.Describe(131101, 18, 1));
        Assert.Null(CharaMakeIconText.Describe(0));
        Assert.Null(CharaMakeIconText.Summarize(uint.MaxValue));
    }

    [Fact]
    public void MissingTranslationKeepsOriginalRatherThanBorrowingSimilarDescription()
    {
        var translate = typeof(AccessibilityStrings).GetMethod("LocText", BindingFlags.NonPublic | BindingFlags.Static)!;
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal("new unknown, wider", translate.Invoke(null, new object[] { "de", "new unknown, wider" }));
        Assert.Equal("", translate.Invoke(null, new object[] { "", "" }));
    }

    [Fact]
    public void GarudaSettingAndDiagnosticsUseRussianWithoutLosingNumbers()
    {
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal("Переход на событие Гаруды", AccessibilityStrings.OptNocturneWarpMode);
        Assert.Equal("В журнал записаны сведения об умениях: 37.", AccessibilityStrings.ActionProbeSaved(37));
        Assert.Equal("Внимание: по данным игры изучено 5, а окно показывает 7.", AccessibilityStrings.AozProbeCounterMismatch(5, 7));
        var flight = AccessibilityStrings.FlightProbeSaved("Место", 47, 9, false, FlightBlock.AetherCurrents);
        Assert.Contains("47", flight);
        Assert.Contains("9", flight);
        Assert.Contains("нет", flight);
        Assert.DoesNotMatch("[A-Za-z]", flight);
        Loc.Mode = LanguageMode.English;
        Assert.Equal("Garuda event Warp", AccessibilityStrings.OptNocturneWarpMode);
        Loc.Mode = LanguageMode.German;
        Assert.Equal("Teleport beim Garuda-Ereignis", AccessibilityStrings.OptNocturneWarpMode);
    }

    [Fact]
    public void EmbeddedCatalogHasOnlyLiveSourceKeysAndNonEmptyRussianValues()
    {
        using var resource = typeof(AccessibilityStrings).Assembly.GetManifestResourceStream("FF14Accessibility.Resources.RussianCharacterText.json.gz");
        Assert.NotNull(resource);
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(gzip)!;
        var sources = Table(typeof(CharaMakeShapeText)).Values.Concat(Table(typeof(CharaMakeIconText)).Values)
            .SelectMany(x => new[] { x.En, x.BriefEn }).ToHashSet();
        Assert.True(catalog.Count >= 1247);
        foreach (var (source, russian) in catalog)
        {
            Assert.Contains(source, sources);
            Assert.Matches("[А-Яа-яЁё]", russian);
        }
    }
}
