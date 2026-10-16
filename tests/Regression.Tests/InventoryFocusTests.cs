using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;

namespace Regression.Tests;

[Collection("Language")]
public unsafe sealed class InventoryFocusTests : IDisposable
{
    private readonly LanguageMode previous = Loc.Mode;
    public void Dispose() => Loc.Mode = previous;

    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1908u, 1908u)]
    [InlineData(1001908u, 1908u)]
    [InlineData(501908u, 1908u)]
    public void NativeItemQualityDoesNotChangeTheSheetRow(uint rawId, uint expected)
        => Assert.Equal(expected, ItemSlotService.GetBaseItemId(rawId));

    [Theory]
    [InlineData("InventoryGrid", 0, -1, 0)]
    [InlineData("InventoryGrid", 3, -1, 3)]
    [InlineData("InventoryGrid", 4, -1, -1)]
    [InlineData("InventoryGrid0", -1, 0, 0)]
    [InlineData("InventoryGrid1", -1, 0, 1)]
    [InlineData("InventoryGrid0", -1, 1, 2)]
    [InlineData("InventoryGrid1", -1, 1, 3)]
    [InlineData("InventoryGrid0E", -1, -1, 0)]
    [InlineData("InventoryGrid3E", -1, -1, 3)]
    [InlineData("InventoryGrid1", -1, 2, -1)]
    [InlineData("InventoryEventGrid", 0, 0, -1)]
    public void EachInventoryViewUsesItsOwnDisplayedPage(string grid, int small, int large, int expected)
        => Assert.Equal(expected, ItemSlotService.DisplayPage(grid, small, large));

    [Fact]
    public void NativeZeroBasedArmouryTabsUseTheMatchingCategorySorter()
    {
        var main = new ItemOrderModuleSorter { InventoryType = InventoryType.ArmoryMainHand };
        var head = new ItemOrderModuleSorter { InventoryType = InventoryType.ArmoryHead };
        var soul = new ItemOrderModuleSorter { InventoryType = InventoryType.ArmorySoulCrystal };
        var order = new ItemOrderModule();
        order.ArmouryMainHandSorter = &main;
        order.ArmouryHeadSorter = &head;
        order.ArmourySoulCrystalSorter = &soul;
        Assert.Equal((nint)(&main), (nint)ItemSlotService.GetArmourySorter(&order, 0));
        Assert.Equal((nint)(&head), (nint)ItemSlotService.GetArmourySorter(&order, 1));
        Assert.Equal((nint)(&soul), (nint)ItemSlotService.GetArmourySorter(&order, 11));
        Assert.Equal((nint)0, (nint)ItemSlotService.GetArmourySorter(&order, -1));
        Assert.Equal((nint)0, (nint)ItemSlotService.GetArmourySorter(&order, 12));
        Assert.Equal((nint)0, (nint)ItemSlotService.GetArmourySorter(null, 0));
    }

    [Fact]
    public void SortedBagUsesPhysicalPageAndSlotInsteadOfDisplayIndex()
    {
        var entries = stackalloc ItemOrderModuleSorterItemEntry[140];
        var pointers = stackalloc Pointer<ItemOrderModuleSorterItemEntry>[140];
        for (var i = 0; i < 140; i++) pointers[i] = &entries[i];
        // Measured old failure: display 2 was Sticky Rice at Inventory3/8.
        entries[2] = new() { Page = 2, Slot = 8 };
        entries[36] = new() { Page = 1, Slot = 5 };
        var sorter = new ItemOrderModuleSorter { InventoryType = InventoryType.Inventory1,
            ItemsPerPage = 35, SortFunctionIndex = -1 };
        sorter.Items.First = pointers;
        sorter.Items.Last = pointers + 140;
        sorter.Items.End = pointers + 140;
        Assert.True(ItemSlotService.TryGetSortedAddress(&sorter, 0, 2, out var page, out var slot));
        Assert.Equal(InventoryType.Inventory3, page);
        Assert.Equal(8, slot);
        Assert.True(ItemSlotService.TryGetSortedAddress(&sorter, 1, 1, out page, out slot));
        Assert.Equal(InventoryType.Inventory2, page);
        Assert.Equal(5, slot);
        sorter.SortFunctionIndex = 0;
        Assert.False(ItemSlotService.TryGetSortedAddress(&sorter, 0, 2, out _, out _));
        sorter.SortFunctionIndex = -1;
        entries[2].Page = 4;
        Assert.False(ItemSlotService.TryGetSortedAddress(&sorter, 0, 2, out _, out _));
        entries[2] = new() { Page = 0, Slot = 35 };
        Assert.False(ItemSlotService.TryGetSortedAddress(&sorter, 0, 2, out _, out _));
        Assert.False(ItemSlotService.TryGetSortedAddress(&sorter, 4, 0, out _, out _));
        Assert.False(ItemSlotService.TryGetSortedAddress(null, 0, 0, out _, out _));
    }

    [Theory]
    [InlineData(22416, 22416, false, "4", 4, true)]
    [InlineData(1025102, 25102, true, "7", 7, true)]
    [InlineData(25102, 25102, true, "7", 7, false)]
    [InlineData(22403, 22416, false, "4", 4, false)]
    [InlineData(22416, 22416, false, "4", 10, false)]
    public void StaleDrawingCannotNameOrDescribeNewContents(uint icon, uint rowIcon, bool hq,
        string drawn, uint actual, bool expected)
        => Assert.Equal(expected, ItemSlotService.MatchesDrawing(icon, rowIcon, hq, drawn, actual));

    [Theory]
    [InlineData("item")] [InlineData("page")] [InlineData("quantity")]
    [InlineData("focus")] [InlineData("translation")] [InlineData("closed")]
    public void AnyChangeCancelsPreviousItemDescriptionEvenOnTheSameNode(string change)
    {
        var reader = (UIReaderService)RuntimeHelpers.GetUninitializedObject(typeof(UIReaderService));
        var slot = new ItemSlotService.OwnedSlot(ItemSlotService.OwnedSlotStatus.Ready,
            100, 0, 2, InventoryType.Inventory3, 8, 5333, 4);
        var original = new UIReaderService.ItemFocusIdentity(42, 22416, "4", slot, true);
        reader.ObserveItemFocus(original);
        Field(reader, "_itemDwellId", 5333u);
        Field(reader, "_itemDwellArmed", true);
        Field(reader, "_lastFocusedNodePtr", (nint)42);
        reader.ObserveItemFocus(original);
        Assert.Equal(5333u, Field<uint>(reader, "_itemDwellId"));
        var next = change switch
        {
            "item" => original with { OwnedSlot = slot with { ItemId = 5334 } },
            "page" => original with { OwnedSlot = slot with { Page = 1 } },
            "quantity" => original with { Quantity = "3", OwnedSlot = slot with { Quantity = 3 } },
            "focus" => original with { Node = 43 },
            "translation" => original with { Translate = false },
            _ => default,
        };
        reader.ObserveItemFocus(next);
        Assert.Equal(0u, Field<uint>(reader, "_itemDwellId"));
        Assert.False(Field<bool>(reader, "_itemDwellArmed"));
        Assert.Equal((nint)0, Field<nint>(reader, "_lastFocusedNodePtr"));
    }

    [Theory]
    [InlineData(7, 1, "Главная рука")]
    [InlineData(8, 2, "Голова")]
    [InlineData(13, 7, "Левая рука")]
    [InlineData(18, 12, "Камень души")]
    public void SilentCategoryControlsFromLogReadBeforeTheyAreClicked(uint id, uint param, string label)
    {
        Loc.Mode = LanguageMode.Russian;
        var component = new AtkComponentBase();
        var owner = new AtkComponentNode { Component = &component };
        owner.AtkResNode.NodeId = id;
        owner.AtkResNode.Type = (NodeType)(id < 13 ? 1011 : 1012);
        var ev = new AtkEvent { Param = param };
        ev.State.EventType = AtkEventType.ButtonClick;
        owner.AtkResNode.AtkEventManager.Event = &ev;
        var collision = new AtkResNode { Type = NodeType.Collision, NodeId = 7,
            ParentNode = (AtkResNode*)&owner };
        Assert.True(UIReaderService.ReadArmouryFilterFocus(&collision, "ArmouryBoard", out var text));
        Assert.Equal(label + ", фильтр слота", text);
        Assert.False(UIReaderService.ReadArmouryFilterFocus(&collision, "Character", out _));
        ev.Param = 256; // Generic MouseOver params never supply category names.
        Assert.False(UIReaderService.ReadArmouryFilterFocus(&collision, "ArmouryBoard", out _));
    }

    [Fact]
    public void AllTwelveSlotFiltersHaveNamesAndItemSlotsHaveNone()
    {
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal(12, Enumerable.Range(7, 12).Select(i => UIReaderService.ArmouryFilterLabel((uint)i))
            .Where(s => s.Length > 0).Distinct().Count());
        Assert.Empty(UIReaderService.ArmouryFilterLabel(20));
    }

    private static void Field(UIReaderService reader, string name, object value)
        => typeof(UIReaderService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(reader, value);
    private static T Field<T>(UIReaderService reader, string name)
        => (T)typeof(UIReaderService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(reader)!;
}
