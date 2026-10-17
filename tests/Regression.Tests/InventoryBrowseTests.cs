using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

[Collection("Language")]
public sealed class InventoryBrowseTests : IDisposable
{
    private readonly LanguageMode _previous = Loc.Mode;

    public InventoryBrowseTests() => Loc.Mode = LanguageMode.English;

    public void Dispose() => Loc.Mode = _previous;

    [Fact]
    public void EmptySnapshotCannotOpenAMenu()
    {
        Assert.Null(InventoryBrowse.BuildMenu(-1, "Gil", [], _ => { }, EmptySnapshot));
        Assert.Empty(InventoryBrowse.Group([]));
    }

    [Fact]
    public void GilAloneOpensAStayOpenRowWithoutCategories()
    {
        var spoken = new List<string>();
        var menu = InventoryBrowse.BuildMenu(250, "Gil", [], spoken.Add, () => (250, "Gil", []));
        Assert.NotNull(menu);
        var row = Assert.Single(menu.Entries);
        Assert.Equal("gil", row.Key);
        Assert.Equal("Gil: 250", row.Label);
        Assert.True(row.StayOpen);
        Assert.Null(row.Submenu);
        row.Activate!();
        Assert.Equal(["Gil: 250"], spoken);
    }

    [Fact]
    public void CategoriesComeFromTheGameGroupKeyAndEmptyGroupsStayOut()
    {
        var groups = InventoryBrowse.Group(
        [
            Bag("Potion", 44, "Medicine", 2),
            Bag("Ether", 44, "Medicine"),
            Bag("Copper Ore", 48, "Stone"),
            Bag("Gone", 44, "Medicine", 0),
            Bag("", 48, "Stone"),
            Key("Delivery Note"),
        ]);
        Assert.Equal(["key", "c:44", "c:48"], groups.Select(g => g.Key));
        Assert.Equal(["Key items", "Medicine", "Stone"], groups.Select(g => g.Name));
        Assert.Equal([1, 2, 1], groups.Select(g => g.Items.Count));
        Assert.Equal(["Ether", "Potion"], groups[1].Items.Select(i => i.Name));
        Assert.DoesNotContain(groups, g => g.Items.Any(i => i.Quantity == 0 || i.Name.Length == 0));
    }

    [Fact]
    public void HighQualityAndGearSetStaySeparateFromTheNormalStack()
    {
        var groups = InventoryBrowse.Group(
        [
            Bag("Honey", 45, "Ingredient", hq: false),
            Bag("Honey", 45, "Ingredient", hq: true, set: true),
        ]);
        var items = Assert.Single(groups).Items;
        Assert.Equal(2, items.Count);
        Assert.Equal("Honey", items[0].Label);
        Assert.Contains("high quality", items[1].Label, StringComparison.Ordinal);
        Assert.Contains("gear set", items[1].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyItemsNeverJoinABagCategoryEvenWithTheSameName()
    {
        var groups = InventoryBrowse.Group(
        [
            Bag("Letter", 61, "Miscellany"),
            Key("Letter"),
        ]);
        Assert.Equal(2, groups.Count);
        Assert.Equal(InventoryBrowse.KeyItemsGroup, groups[0].Key);
        Assert.Equal("c:61", groups[1].Key);
        Assert.True(Assert.Single(groups[0].Items).IsKeyItem);
        Assert.False(Assert.Single(groups[1].Items).IsKeyItem);
    }

    [Fact]
    public void CrystalsShareTheirItemUICategoryAndSortAfterKeyItems()
    {
        var groups = InventoryBrowse.Group(
        [
            Bag("Fire Crystal", 59, "Crystal", 99),
            Bag("Wind Shard", 59, "Crystal", 200),
            Key("Duty Key"),
            Bag("Potion", 44, "Medicine"),
        ]);
        Assert.Equal(["key", "c:44", "c:59"], groups.Select(g => g.Key));
        Assert.Equal(["Fire Crystal", "Wind Shard"], groups[2].Items.Select(i => i.Name));
    }

    [Fact]
    public void ConfirmOpensOnlyThatCategoryAndBackRebuildsFromAFreshSnapshot()
    {
        var items = new List<InventoryBrowseItem>
        {
            Bag("Potion", 44, "Medicine"),
            Bag("Ether", 44, "Medicine"),
            Bag("Copper Ore", 48, "Stone")
        };
        var spoken = new List<string>();
        var opened = new List<string>();
        var menu = InventoryBrowse.BuildMenu(10, "Gil", items, spoken.Add, () => (10, "Gil", items),
            item => { opened.Add(item.Name); return true; });
        Assert.NotNull(menu);
        Assert.Equal(3, menu.Entries.Count);
        Assert.Equal("gil", menu.Entries[0].Key);
        Assert.Contains("Medicine, 2 items", menu.Entries[1].Label, StringComparison.Ordinal);
        Assert.Contains("Stone, 1 items", menu.Entries[2].Label, StringComparison.Ordinal);

        var medicine = menu.Entries[1].Submenu!();
        Assert.Equal("Medicine", medicine.Title);
        Assert.Equal(["Ether", "Potion"], medicine.Entries.Select(e => e.Label));
        Assert.Contains("game menu", medicine.Intro, StringComparison.Ordinal);
        Assert.True(medicine.Entries[0].StayOpen);
        medicine.Entries[0].Activate!();
        Assert.Equal(["Ether"], opened);
        Assert.Empty(spoken);
        Assert.False(medicine.Entries[0].StayOpen);

        items.RemoveAll(i => i.Name == "Ether");
        var rebuilt = menu.Rebuild!();
        Assert.Contains("Medicine, 1 items", rebuilt.Entries[1].Label, StringComparison.Ordinal);
        var afterBack = rebuilt.Entries[1].Submenu!();
        Assert.Equal(["Potion"], afterBack.Entries.Select(e => e.Label));
    }

    [Fact]
    public void ConfirmOpensTheChosenStackAndAFailureStaysOnTheItem()
    {
        var opened = new List<int>();
        var items = new List<InventoryBrowseItem>
        {
            Bag("Ether", 44, "Medicine", slot: 7, itemId: 10),
            Bag("Ether", 44, "Medicine", slot: 3, itemId: 10),
        };
        var menu = InventoryBrowse.BuildMenu(-1, "Gil", items, _ => { }, () => (-1, "Gil", items),
            item => { opened.Add(item.Slot); return item.Slot == 3; });
        var rows = menu!.Entries[0].Submenu!();
        rows.Entries[0].Activate!();
        rows.Entries[1].Activate!();
        Assert.Equal([3, 7], opened);
        Assert.False(rows.Entries[0].StayOpen);
        Assert.True(rows.Entries[1].StayOpen);
    }

    [Fact]
    public void ShopWindowOwnsTheItemMenuWhileItIsOpen()
    {
        Assert.Equal(9u, InventoryContextOwner.Choose(9, 4));
        Assert.Equal(4u, InventoryContextOwner.Choose(0, 4));
        Assert.Equal(0u, InventoryContextOwner.Choose(0, 0));
        Assert.False(InventoryContextOwner.NeedsGameWindow(9, 0));
        Assert.False(InventoryContextOwner.NeedsGameWindow(0, 4));
        Assert.True(InventoryContextOwner.NeedsGameWindow(0, 0));
    }

    [Fact]
    public void RussianLabelsKeepTheSameGroupKeys()
    {
        Loc.Mode = LanguageMode.Russian;
        var groups = InventoryBrowse.Group([Bag("Зелье", 44, "Снадобье"), Key("Письмо")]);
        var menu = InventoryBrowse.BuildMenu(-1, "Гил", groups.SelectMany(g => g.Items).ToList(),
            _ => { }, () => (-1, "Гил", groups.SelectMany(g => g.Items).ToList()));
        Assert.NotNull(menu);
        Assert.Equal(InventoryBrowse.KeyItemsGroup, menu.Entries[0].Key);
        Assert.Equal("c:44", menu.Entries[1].Key);
        Assert.Contains("Ключевые предметы", menu.Entries[0].Label, StringComparison.Ordinal);
        Assert.Contains("Снадобье", menu.Entries[1].Label, StringComparison.Ordinal);
        Assert.Equal("Инвентарь", menu.Title);
    }

    [Fact]
    public void TranslateOffLanguageStillNamesTheMenuInGerman()
    {
        Loc.Mode = LanguageMode.German;
        var menu = InventoryBrowse.BuildMenu(-1, "Gil", [Bag("Trank", 44, "Arznei")],
            _ => { }, () => (-1, "Gil", [Bag("Trank", 44, "Arznei")]));
        Assert.NotNull(menu);
        Assert.Equal("Inventar", menu.Title);
        Assert.Contains("Arznei, 1 Gegenstände", menu.Entries[0].Label, StringComparison.Ordinal);
    }

    private static (int Gil, string GilName, IReadOnlyList<InventoryBrowseItem> Items) EmptySnapshot()
        => (-1, "Gil", []);

    private static InventoryBrowseItem Bag(string name, uint category, string categoryName,
        int quantity = 1, bool hq = false, bool set = false, int slot = -1, uint itemId = 0)
        => new(InventoryBrowse.CategoryGroupKey(category), InventoryBrowse.CategorySortBase + category,
            categoryName, name, quantity, hq, set, category, false, name, 0, slot, itemId);

    private static InventoryBrowseItem Key(string name)
        => new(InventoryBrowse.KeyItemsGroup, InventoryBrowse.KeyItemsSort,
            AccessibilityStrings.InventoryKeyItemsCategory, name, 1, false, false, 1, true, name);
}
