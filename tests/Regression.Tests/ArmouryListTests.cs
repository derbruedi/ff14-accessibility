using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Regression.Tests;

public sealed class ArmouryListTests
{
    [Fact]
    public void BothQualitiesReadOneSheetRowButCannotReplaceEachOtherForAnAction()
    {
        var nq = Item(id: 1908);
        var hq = Item(id: 1001908, hq: true);
        Assert.Equal(1908u, nq.BaseItemId);
        Assert.Equal(nq.BaseItemId, hq.BaseItemId);
        Assert.Equal(1001908u, hq.ItemId);
        Assert.False(ArmouryListModel.CanAct(nq, hq));
        Assert.False(ArmouryListModel.CanAct(hq, nq));
        Assert.True(ArmouryListModel.CanAct(hq, hq));
    }

    private static ArmouryListItem Item(InventoryType type = InventoryType.ArmoryHead,
        int slot = 0, uint id = 100, bool hq = false, string instance = "AA", string label = "Hat")
        => new(type, slot, id, 1, hq, instance, label);

    [Fact]
    public void CategoryNavigationSkipsEmptySectionsAndWrapsInTheNativeOrder()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(InventoryType.ArmoryMainHand, 8), Item(InventoryType.ArmoryMainHand, 20),
            Item(InventoryType.ArmoryBody, 4), Item(InventoryType.ArmoryBody, 15),
            Item(InventoryType.ArmoryRings, 6)]);
        model.Move(1); model.MoveCategory(1);
        Assert.Equal(InventoryType.ArmoryBody, model.Selected!.Container); Assert.Equal(4, model.Selected.Slot);
        model.MoveCategory(1); Assert.Equal(InventoryType.ArmoryRings, model.Selected!.Container);
        model.MoveCategory(1); Assert.Equal(InventoryType.ArmoryMainHand, model.Selected!.Container);
        Assert.Equal(8, model.Selected.Slot);
        model.MoveCategory(-1); Assert.Equal(InventoryType.ArmoryRings, model.Selected!.Container);
        model.MoveCategory(-1); Assert.Equal(InventoryType.ArmoryBody, model.Selected!.Container);
        foreach (var type in ArmouryListModel.Containers) Assert.NotEmpty(ArmouryListModel.CategoryName(type));
    }

    [Fact]
    public void OneCategoryOrAnEmptyListCannotLeaveSelectionOutsideTheAvailableItems()
    {
        var model = new ArmouryListModel();
        model.MoveCategory(-1); model.MoveCategory(1); Assert.Null(model.Selected);
        model.Replace([Item(slot: 8), Item(slot: 20)]); model.Move(1);
        model.MoveCategory(1); Assert.Equal(8, model.Selected!.Slot);
        model.MoveCategory(-1); Assert.Equal(8, model.Selected!.Slot);
        model.MoveCategory(0); Assert.Equal(8, model.Selected!.Slot);
    }

    [Fact]
    public void AllTwelveNativeSectionsBecomeOneListWithoutGroupingOrIdDeduplication()
    {
        var model = new ArmouryListModel();
        model.Replace(ArmouryListModel.Containers.Select(type => Item(type)));
        Assert.Equal(12, model.Items.Count);
        Assert.Equal(12, model.Items.Select(i => i.Container).Distinct().Count());
        Assert.DoesNotContain(InventoryType.EquippedItems, ArmouryListModel.Containers);
        Assert.DoesNotContain(InventoryType.Inventory1, ArmouryListModel.Containers);
    }

    [Fact]
    public void EmptySlotsAndOtherInventoriesCannotEnterTheList()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(), Item(slot: 1, id: 0), Item(slot: 2) with { Quantity = 0 },
            Item(InventoryType.EquippedItems), Item(InventoryType.Inventory1)]);
        Assert.Single(model.Items);
    }

    [Fact]
    public void IdenticalItemsAndBothQualitiesKeepTheirOwnPhysicalAddresses()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(slot: 4), Item(slot: 8), Item(slot: 12, hq: true)]);
        Assert.Equal([4, 8, 12], model.Items.Select(i => i.Slot));
        model.Move(1); Assert.Equal(8, model.Selected!.Slot);
        model.Move(1); Assert.True(model.Selected!.HighQuality);
    }

    [Fact]
    public void RefreshPreservesTheSelectedInstanceWhenSortingOrTranslationChanges()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(slot: 4, label: "A"), Item(slot: 8, label: "B")]);
        model.Move(1);
        model.Replace([Item(slot: 8, label: "А"), Item(slot: 4, label: "Б"), Item(slot: 0, label: "0")]);
        Assert.Equal(8, model.Selected!.Slot);
        Assert.Equal("А", model.Selected.Label);
    }

    [Fact]
    public void RemovedLastItemCannotLeaveTheCursorOutsideTheList()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(slot: 1), Item(slot: 2), Item(slot: 3)]); model.End(true);
        Assert.True(model.Replace([Item(slot: 1)]));
        Assert.Equal(0, model.Cursor); Assert.Equal(1, model.Selected!.Slot);
        model.Replace([]); model.Move(-1); model.End(true);
        Assert.Null(model.Selected); Assert.Equal(0, model.Cursor);
    }

    [Theory]
    [InlineData("slot")] [InlineData("container")] [InlineData("id")]
    [InlineData("quality")] [InlineData("instance")] [InlineData("gone")]
    public void AnActionRejectsEveryStaleOrDifferentPhysicalItem(string change)
    {
        var original = Item();
        var current = change switch
        {
            "slot" => original with { Slot = 1 },
            "container" => original with { Container = InventoryType.ArmoryBody },
            "id" => original with { ItemId = 101 },
            "quality" => original with { HighQuality = true },
            "instance" => original with { Instance = "BB" },
            _ => null,
        };
        Assert.False(ArmouryListModel.CanAct(original, current));
        Assert.True(ArmouryListModel.CanAct(original, original));
    }

    [Fact]
    public void BrowsingWrapsAndFirstLastAndReopeningArePredictable()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(slot: 1), Item(slot: 2)]);
        model.Move(-1); Assert.Equal(1, model.Cursor);
        model.Move(1); Assert.Equal(0, model.Cursor);
        model.End(true); Assert.Equal(1, model.Cursor);
        model.Clear(); model.Replace([Item(slot: 9)]);
        Assert.Equal(0, model.Cursor); Assert.Equal(9, model.Selected!.Slot);
    }

    [Fact]
    public void GridUsesFiveColumnsAndKeepsEveryCategoryReachableWithoutChoosingASlot()
    {
        var model = new ArmouryListModel();
        model.Replace(ArmouryListModel.Containers.Select((type, i) => Item(type, i, label: "Item " + i)));
        model.MoveRow(1); Assert.Equal(5, model.Cursor);
        model.MoveRow(1); Assert.Equal(10, model.Cursor);
        model.MoveRow(1); Assert.Equal(0, model.Cursor);
        model.Move(4); model.MoveRow(1); Assert.Equal(9, model.Cursor);
        model.MoveRow(1); Assert.Equal(11, model.Cursor); // Last row has two items.
        model.End(false);
        var visited = new HashSet<InventoryType>();
        for (var i = 0; i < model.Items.Count; i++) { visited.Add(model.Selected!.Container); model.Move(1); }
        Assert.Equal(12, visited.Count);
        model.Clear(); model.MoveRow(-1); Assert.Equal(0, model.Cursor);
        model.Replace([Item()]); model.MoveRow(-1); Assert.Equal(0, model.Cursor);
    }

    [Fact]
    public void GameOrderDoesNotChangeWhenNamesAreTranslated()
    {
        var model = new ArmouryListModel();
        model.Replace([Item(slot: 8, label: "Z"), Item(slot: 4, label: "A")]);
        Assert.Equal([8, 4], model.Items.Select(i => i.Slot));
        model.Replace([Item(slot: 8, label: "А"), Item(slot: 4, label: "Я")]);
        Assert.Equal([8, 4], model.Items.Select(i => i.Slot));
    }
}
