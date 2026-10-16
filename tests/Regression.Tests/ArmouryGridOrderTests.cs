using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;

namespace Regression.Tests;

public unsafe sealed class ArmouryGridOrderTests
{
    [Fact]
    public void NativeSortMapReordersItemsWithoutChangingTheirPhysicalSlot()
    {
        var entries = stackalloc ItemOrderModuleSorterItemEntry[3];
        var pointers = stackalloc Pointer<ItemOrderModuleSorterItemEntry>[3];
        for (var i = 0; i < 3; i++) pointers[i] = &entries[i];
        entries[0] = new() { Page = 0, Slot = 2 };
        entries[1] = new() { Page = 0, Slot = 0 };
        entries[2] = new() { Page = 0, Slot = 1 };
        var sorter = new ItemOrderModuleSorter { InventoryType = InventoryType.ArmoryBody,
            ItemsPerPage = 3, SortFunctionIndex = -1 };
        sorter.Items.First = pointers; sorter.Items.Last = sorter.Items.End = pointers + 3;
        Assert.True(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryBody, 3, out var slots));
        Assert.Equal([2, 0, 1], slots);
        sorter.SortFunctionIndex = 0; // Do not act on a map while the game is updating it.
        Assert.False(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryBody, 3, out _));
        sorter.SortFunctionIndex = -1;
        Assert.False(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryHead, 3, out _));
        entries[2].Slot = 2; // A corrupt map must not repeat one physical item and lose another.
        Assert.False(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryBody, 3, out _));
        entries[2].Slot = 4;
        Assert.False(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryBody, 3, out _));
    }

    [Fact]
    public void MissingSortMapKeepsPhysicalOrderButAnIncompleteMapCannotDropItems()
    {
        Assert.True(ArmouryListAccess.TryGetSectionSlots(null, InventoryType.ArmoryHead, 3, out var slots));
        Assert.Equal([0, 1, 2], slots);
        var sorter = new ItemOrderModuleSorter { InventoryType = InventoryType.ArmoryHead,
            ItemsPerPage = 3, SortFunctionIndex = -1 };
        Assert.False(ArmouryListAccess.TryGetSectionSlots(&sorter, InventoryType.ArmoryHead, 3, out _));
        Assert.False(ArmouryListAccess.TryGetSectionSlots(null, InventoryType.ArmoryHead, -1, out _));
    }
}
