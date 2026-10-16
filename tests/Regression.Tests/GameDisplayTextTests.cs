using System.Text;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina.Data.Structs.Excel;
using Lumina.Text.ReadOnly;
using NativeSheet = FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelSheet;
using NativeLanguage = FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelLanguage;
using NativeVariant = FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelVariant;
using NativeColumnType = FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelColumnType;

namespace Regression.Tests;

[Collection("Language")]
public sealed unsafe class GameDisplayTextTests : IDisposable
{
    private readonly LanguageMode _mode = Loc.Mode;
    private readonly bool _translate = Loc.TranslateItemsAndActions;
    private static ReadOnlySeString Text(string text) => new(Encoding.UTF8.GetBytes(text));

    public GameDisplayTextTests() { Loc.Mode = LanguageMode.Russian; }

    [Fact]
    public void PrimaKeepsItsExactTextEvenWhenOnlyThePluginTranslationIsOff()
    {
        Loc.TranslateItemsAndActions = false;
        var calls = new List<(string Sheet, uint Row)>();
        GameDisplayText.Configure(null, (sheet, id, source) =>
        {
            Assert.True(source.Span.SequenceEqual("Fire"u8));
            calls.Add((sheet, id));
            return Text("Пламя Prima");
        });
        Assert.Equal("Пламя Prima", GameDisplayText.Find("Action", 141, "Fire"u8)?.ExtractText());
        Assert.Equal(new[] { ("Action", 141u) }, calls);
        Assert.False(Loc.IsRussianItemActionText);
    }

    [Theory]
    [InlineData(LanguageMode.English)] [InlineData(LanguageMode.German)]
    public void OtherPromptLanguagesKeepTheGameTranslationWithoutEnablingRussianCatalogs(LanguageMode mode)
    {
        Loc.Mode = mode;
        GameDisplayText.Configure(null, (_, _, _) => Text("Гил Prima"));
        Assert.Equal("Гил Prima", GameDisplayText.Find("Item", 1, "Gil"u8)?.ExtractText());
        Assert.False(GameTextTranslation.ShouldTranslate("Item"));
    }

    [Fact]
    public void MissingDisabledAndFailedReadersKeepTheFallbackAvailable()
    {
        GameDisplayText.Configure(null, null);
        Assert.Null(GameDisplayText.Find("Item", 1, "Gil"u8));
        GameDisplayText.Configure(null, (_, _, _) => throw new IOException("Missing game row"));
        Assert.Null(GameDisplayText.Find("Item", 1, "Gil"u8));
        GameDisplayText.Configure(null, (_, _, _) => Text("Гил"), () => false);
        Assert.Null(GameDisplayText.Find("Item", 1, "Gil"u8));
    }

    [Fact]
    public void LoadingUnloadingAndReplacingTheReaderInvalidateNameCaches()
    {
        var active = false;
        GameDisplayText.Configure(null, (_, _, _) => Text("Prima"), () => active);
        var before = GameDisplayText.Revision;
        active = true;
        Assert.True(GameDisplayText.Revision > before);
        var loaded = GameDisplayText.Revision;
        Assert.Equal(loaded, GameDisplayText.Revision);
        active = false;
        Assert.True(GameDisplayText.Revision > loaded);
        var unloaded = GameDisplayText.Revision;
        GameDisplayText.Configure(null, (_, _, _) => Text("Другой пакет"));
        Assert.True(GameDisplayText.Revision > unloaded);
    }

    [Theory]
    [InlineData("FateName", "Fate")] [InlineData("PetName", "Pet")]
    [InlineData("XBMPetDescription", "XBMPet")] [InlineData("ENpcResidentName", "ENpcResident")]
    [InlineData("AddonText", "Addon")]
    public void OlderCatalogAliasesReadTheActualGameSheet(string oldName, string actualSheet)
    {
        GameDisplayText.Configure(null, (sheet, id, source) =>
        {
            Assert.Equal(actualSheet, sheet); Assert.Equal(17u, id);
            Assert.True(source.Span.SequenceEqual("original"u8));
            return Text("Текст Prima");
        });
        Assert.Equal("Текст Prima", RussianAuthorText.Translate(oldName, 17, "original"u8, "Старый перевод"));
    }

    [Fact]
    public void IdenticalColumnsNeedAgreementAndUntranslatedTextKeepsTheCatalogFallback()
    {
        Assert.Null(NativeGameDisplayText.Consensus("Original"u8, []));
        Assert.Null(NativeGameDisplayText.Consensus("Original"u8, [Text("Original")]));
        Assert.Null(NativeGameDisplayText.Consensus("Original"u8, [Text("")]));
        Assert.Null(NativeGameDisplayText.Consensus("Original"u8, [Text("Prima"), Text("Original")]));
        Assert.Null(NativeGameDisplayText.Consensus("Original"u8, [Text("Prima"), Text("Другой текст")]));
        Assert.Equal("Prima", NativeGameDisplayText.Consensus("Original"u8, [Text("Prima"), Text("Prima")])?.ExtractText());
    }

    [Fact]
    public void PayloadBytesSurviveWithoutOldTerminologySubstitution()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("Potency Prima "), 2, 0x10, 1, 3];
        var text = new ReadOnlySeString(bytes);
        GameDisplayText.Configure(null, (_, _, _) => text);
        Assert.Equal(bytes, GameDisplayText.Find("ActionTransient", 141, "original"u8)!.Value.Data.ToArray());
    }

    [Fact]
    public void ExactAliasesPreserveIdsAndConflictingNamesDoNotPickAnotherItem()
    {
        var names = GameNameIndex.Build([(1908u, "Composite Bow", "Лук Prima"),
            (1909u, "Second Bow", "Лук Prima"), (1910u, "Third Bow", "Composite Bow"),
            (1911u, "Same name", "same NAME"), (1912u, "", "Однозначный предмет")]);
        Assert.False(names.ContainsKey("Лук Prima"));
        Assert.False(names.ContainsKey("Composite Bow"));
        Assert.Equal(1909u, names["Second Bow"]);
        Assert.Equal(1911u, names["same name"]);
        Assert.Equal(1912u, names["Однозначный предмет"]);
        Assert.False(names.ContainsKey("предмет"));
    }

    [Fact]
    public void NativeLayoutRequiresSameTypesOffsetsLanguageAndRowVariant()
    {
        var definitions = stackalloc NativeSheet.ColumnInfo[2];
        definitions[0] = new() { ColumnType = NativeColumnType.String, Offset = 0 };
        definitions[1] = new() { ColumnType = NativeColumnType.UInt8, Offset = 4 };
        var columns = new ExcelColumnDefinition[] {
            new() { Type = ExcelColumnDataType.String, Offset = 0 },
            new() { Type = (ExcelColumnDataType)3, Offset = 4 } };
        NativeSheet sheet = new() { Version = 3, ColumnDefinitions = definitions, ColumnCount = 2,
            Language = NativeLanguage.English, Variant = NativeVariant.SingleRow };
        Assert.True(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.ColumnDefinitions[0].Offset = 2;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.ColumnDefinitions[0].Offset = 0;
        sheet.ColumnDefinitions[1].ColumnType = NativeColumnType.Int16;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.ColumnDefinitions[1].ColumnType = NativeColumnType.UInt8;
        sheet.Language = NativeLanguage.German;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.Language = NativeLanguage.English;
        sheet.Variant = NativeVariant.MultiRow;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.Variant = NativeVariant.SingleRow;
        sheet.ColumnCount = 1;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        sheet.ColumnCount = 2; sheet.ColumnDefinitions = null;
        Assert.False(NativeGameDisplayText.LayoutMatches(&sheet, columns));
        Assert.False(NativeGameDisplayText.LayoutMatches(null, columns));
    }

    public void Dispose()
    {
        GameDisplayText.Configure(null, null);
        Loc.Mode = _mode;
        Loc.TranslateItemsAndActions = _translate;
    }
}
