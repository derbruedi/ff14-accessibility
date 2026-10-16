using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FF14Accessibility.Services;

namespace RussianDescriptions.Tests;

public sealed class CatalogTests
{
    private static readonly RussianDescriptionCatalog Catalog = RussianDescriptionCatalog.Load();

    [Theory]
    [InlineData("GeneralAction", 30u, "Change the order of hotbar-assigned actions.", "Изменить порядок действий на панели питомца.")]
    [InlineData("GeneralAction", 9u, "Summon a mount at random.", "Призвать случайный транспорт.")]
    [InlineData("MainCommand", 1u, "Sheathe/unsheathe your main arm.", "Обнажить или убрать основное оружие.")]
    public void NewMenuDescriptionsAreBoundToTheirOwnSource(string sheet, uint id, string source, string translation)
    {
        Assert.Equal(translation, Encoding.UTF8.GetString(Catalog.Find(sheet, id, Encoding.UTF8.GetBytes(source), true)!));
        Assert.Null(Catalog.Find(sheet, id, Encoding.UTF8.GetBytes(source + " Changed"), true));
        Assert.Null(Catalog.Find(sheet, id, Encoding.UTF8.GetBytes(source), false));
    }

    [Fact]
    public void EmbeddedCatalogContainsRussianItemDescription()
    {
        var bytes = Catalog.Find("Item", 1, Encoding.UTF8.GetBytes("Standard Eorzean currency."), true);
        Assert.NotNull(bytes);
        Assert.Equal("Стандартная валюта Эорзии.", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void OtherLanguagesNeverReceiveRussianDescription()
        => Assert.Null(Catalog.Find("Item", 1, Encoding.UTF8.GetBytes("Standard Eorzean currency."), false));

    [Fact]
    public void ChangedPotencyRejectsOldTranslation()
    {
        using var fixture = Fixture();
        var catalog = new RussianDescriptionCatalog(fixture);
        Assert.NotNull(catalog.Find("Action", 123, Encoding.UTF8.GetBytes("Potency: 100"), true));
        Assert.Null(catalog.Find("Action", 123, Encoding.UTF8.GetBytes("Potency: 120"), true));
    }

    [Fact]
    public void SameIdInDifferentSheetsDoesNotCrossDescriptions()
    {
        using var fixture = Fixture();
        var catalog = new RussianDescriptionCatalog(fixture);
        var source = Encoding.UTF8.GetBytes("Potency: 100");
        Assert.Equal("Боевое", Encoding.UTF8.GetString(catalog.Find("Action", 123, source, true)!));
        Assert.Equal("Ремесленное", Encoding.UTF8.GetString(catalog.Find("CraftAction", 123, source, true)!));
    }

    [Theory]
    [InlineData("Item", 0u)]
    [InlineData("Item", uint.MaxValue)]
    [InlineData("CraftAction", 1u)]
    [InlineData("NoSuchSheet", 1u)]
    public void MissingEntriesAllowGameFallback(string sheet, uint id)
        => Assert.Null(Catalog.Find(sheet, id, Encoding.UTF8.GetBytes("Standard Eorzean currency."), true));

    [Fact]
    public void LocalTranslationIsBoundToExactInstalledSource()
    {
        var source = Encoding.UTF8.GetBytes("Phantom weapon.");
        Assert.Equal("Призрачное оружие.", Encoding.UTF8.GetString(Catalog.Find("Item", 50978, source, true)!));
        Assert.Null(Catalog.Find("Item", 50978, Encoding.UTF8.GetBytes("A different weapon."), true));
    }

    private static MemoryStream Fixture()
    {
        var result = new MemoryStream();
        var source = Encoding.UTF8.GetBytes("Potency: 100");
        var rows = new Dictionary<string, Dictionary<uint, byte[][]>>
        {
            ["Action"] = new() { [123] = [source, Encoding.UTF8.GetBytes("Боевое")] },
            ["CraftAction"] = new() { [123] = [source, Encoding.UTF8.GetBytes("Ремесленное")] },
        };
        using (var gzip = new GZipStream(result, CompressionMode.Compress, leaveOpen: true))
            JsonSerializer.Serialize(gzip, rows);
        result.Position = 0;
        return result;
    }
}
