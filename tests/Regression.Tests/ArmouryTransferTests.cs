using FF14Accessibility.Services;

namespace Regression.Tests;

public class ArmouryTransferTests
{
    [Fact]
    public unsafe void SwitchingInventoryWindowsCannotReuseTheOriginalOwner()
    {
        FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase original = default, replacement = default;
        original.Id = 10; original.IsVisible = true;
        replacement.Id = 20; replacement.IsVisible = true;
        var windows = new Dictionary<string, nint> {
            ["Inventory"] = (nint)(&original), ["InventoryLarge"] = (nint)(&replacement) };
        var gui = ModSwitchTests.Gate.Of<Dalamud.Plugin.Services.IGameGui>((method, args) =>
            Activator.CreateInstance(method.ReturnType, [windows.GetValueOrDefault((string)args![0]!)]));
        var service = new ArmouryTransferService(null!, null!, gui, null!, null!, null!);
        var find = typeof(ArmouryTransferService).GetMethod("FindVisibleInventoryAddonId",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(10u, find.Invoke(service, [10u]));
        original.IsVisible = false;
        Assert.Equal(0u, find.Invoke(service, [10u]));
        Assert.Equal(20u, find.Invoke(service, [0u])); // Other window really is open.
        original.IsVisible = true;
        original.Id = 30; // Recreated addon at the same address.
        Assert.Equal(0u, find.Invoke(service, [10u]));
        Assert.Equal(30u, find.Invoke(service, [30u]));
    }

    // Rows captured from 6.08.74 in dalamud.old.log, 2026-09-29 23:17.
    // Every callback LabelId was zero; the Russian patch supplied the text.
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 7)]
    public void RecordedRussianMenusSelectTheTransferInsteadOfSkippingEverything(bool equip, int expected)
    {
        var names = new List<string>();
        if (equip) names.Add("Экипировать");
        names.AddRange(["Сравнить предмет", "Примерить", "Отремонтировать", "Поиск предмета",
            "Сделать ссылку", "Найти рецепты с этим материалом", "Положить в арсенал", "Выкинуть", "Отсортировать"]);
        var rows = names.Select(n => new ArmouryMenuRow(0, n == "Отремонтировать", n, "")).ToArray();
        Assert.Equal(ArmouryMenuActionResult.Requested,
            ArmouryTransferRules.SelectCommand(rows, "Place in Armoury Chest", true, true, out var index));
        Assert.Equal(expected, index);
    }

    [Theory]
    [InlineData("Place in Armoury Chest", "", false)]
    [InlineData("Place in Armory Chest", "", false)]
    [InlineData("Поместить в оружейный сундук", "", true)]
    [InlineData("Положить в арсенал", "", true)]
    [InlineData("  Положить в арсенал  ", "", true)]
    [InlineData("In den Arsenal legen", "In den Arsenal legen", false)]
    [InlineData("Nouveau texte", "Nouveau texte", false)]
    public void SupportsKnownNamesAndTheCurrentGameLabel(string displayed, string gameLabel, bool russian)
    {
        Assert.Equal(ArmouryMenuActionResult.Requested,
            ArmouryTransferRules.SelectCommand([new(0, false, displayed, "")], gameLabel, russian, false, out var index));
        Assert.Equal(0, index);
    }

    [Theory]
    [InlineData("Выкинуть")]
    [InlineData("Забрать из арсенала")]
    [InlineData("Не положить в арсенал")]
    [InlineData("Поместить в сундук компании")]
    [InlineData("")]
    public void NeverUsesSubstringOrGuessedDestination(string displayed)
    {
        Assert.Equal(ArmouryMenuActionResult.Unavailable,
            ArmouryTransferRules.SelectCommand([new(0, false, displayed, "")], "", true, true, out var index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void RussianFallbackRespectsPluginLanguage()
    {
        Assert.Equal(ArmouryMenuActionResult.Unavailable,
            ArmouryTransferRules.SelectCommand([new(0, false, "Положить в арсенал", "")], "", false, true, out _));
    }

    [Fact]
    public void StableLabelAndEnglishLookupSurviveAnUnknownTranslation()
    {
        Assert.Equal(ArmouryMenuActionResult.Requested,
            ArmouryTransferRules.SelectCommand([new(1387, false, "another translation", "")], "", false, true, out _));
        Assert.Equal(ArmouryMenuActionResult.Requested,
            ArmouryTransferRules.SelectCommand([new(9999, false, "another translation", "Place in Armoury Chest")], "", false, true, out _));
    }

    [Fact]
    public void DisabledOrAmbiguousCommandsNeverIssueAMove()
    {
        Assert.Equal(ArmouryMenuActionResult.Unavailable,
            ArmouryTransferRules.SelectCommand([new(1387, true, "Положить в арсенал", "")], "", true, true, out _));
        Assert.Equal(ArmouryMenuActionResult.Unavailable,
            ArmouryTransferRules.SelectCommand([new(1387, false, "", ""), new(0, false, "Положить в арсенал", "")],
                "", true, true, out var index));
        Assert.Equal(-1, index);
    }

    [Fact]
    public void MenuCanPopulateAcrossFramesWithoutLosingTheItem()
    {
        Assert.Equal(ArmouryMenuActionResult.NotReady,
            ArmouryTransferRules.SelectCommand([], "", true, true, out _));
        Assert.Equal(ArmouryMenuActionResult.NotReady,
            ArmouryTransferRules.SelectCommand([new(0, false, "Сравнить предмет", "")], "", true, false, out _));
        Assert.Equal(ArmouryMenuActionResult.Requested,
            ArmouryTransferRules.SelectCommand([new(0, false, "Сравнить предмет", ""), new(0, false, "Положить в арсенал", "")],
                "", true, false, out var index));
        Assert.Equal(1, index);
    }

    [Theory]
    [InlineData(2997U, 1, 2, 800, false)] // source still contains the item
    [InlineData(0U, 1, 1, 800, false)] // disappearance alone is not a transfer
    [InlineData(0U, 1, -1, 800, false)] // destination unloaded
    [InlineData(0U, -1, 2, 800, false)] // no valid initial destination snapshot
    [InlineData(0U, 1, 2, 749, false)] // optimistic game update
    [InlineData(0U, 1, 2, 750, true)]
    [InlineData(3515U, 1, 2, 800, true)]
    public void RequiresBothInventoryChangesAndSettlingTime(uint source, int before, int after, int ms, bool confirmed)
        => Assert.Equal(confirmed, ArmouryTransferRules.IsConfirmed(2997, source, before, after, TimeSpan.FromMilliseconds(ms)));

    [Fact]
    public void UnreadableSourceCannotConfirmAMove()
        => Assert.False(ArmouryTransferRules.IsConfirmed(2997, null, 0, 1, TimeSpan.FromSeconds(1)));

    [Fact]
    public void HighQualityIdentityIsKeptDistinct()
        => Assert.False(ArmouryTransferRules.IsConfirmed(1002997, 1002997, 0, 1, TimeSpan.FromSeconds(1)));
}
