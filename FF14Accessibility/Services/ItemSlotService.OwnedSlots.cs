using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed unsafe partial class ItemSlotService
{
    // Native GetItemId includes HQ/collectible flags. Item-sheet lookups use
    // the base row; native actions and instance checks keep the original ID.
    internal static uint GetBaseItemId(uint rawItemId)
        => Dalamud.Utility.ItemUtil.GetBaseId(rawItemId).ItemId;

    internal enum OwnedSlotStatus { OtherWindow, Updating, Ready }

    internal readonly record struct OwnedSlot(
        OwnedSlotStatus Status, nint Owner, int Page, int DisplaySlot,
        InventoryType Container = default, int PhysicalSlot = -1,
        uint ItemId = 0, uint Quantity = 0, bool HighQuality = false);

    // The grid is a DISPLAY index. The client's sort map supplies the physical
    // page and slot, including items displayed on a different bag page.
    internal OwnedSlot ReadOwnedSlot(AtkComponentBase* component, uint iconId, string quantity)
    {
        var dragDrop = FindDragDrop(component);
        var addon = dragDrop == null ? null : FindAddon((AtkResNode*)dragDrop->OwnerNode);
        if (addon == null) return default;
        var name = addon->NameString;
        if (name is not ("InventoryGrid" or "InventoryGrid0" or "InventoryGrid1"
            or "InventoryGrid0E" or "InventoryGrid1E" or "InventoryGrid2E" or "InventoryGrid3E"
            or "ArmouryBoard")) return default;

        var displaySlot = name == "ArmouryBoard"
            ? IndexOf(((AddonArmouryBoard*)addon)->Slots, dragDrop)
            : IndexOf(((AddonInventoryGrid*)addon)->Slots, dragDrop);
        var page = -1;
        ItemOrderModuleSorter* sorter = null;
        var order = ItemOrderModule.Instance();
        if (order != null && name == "ArmouryBoard")
        {
            // Native PreviousTab/NextTab wrap 0..11. ButtonClick event params
            // are 1..12, so they must not be used as the stored tab index.
            sorter = GetArmourySorter(order, ((AddonArmouryBoard*)addon)->TabIndex);
            page = 0;
        }
        else if (order != null)
        {
            sorter = order->InventorySorter;
            page = DisplayPage(name, VisibleTab("Inventory"), VisibleTab("InventoryLarge"));
        }

        var pending = new OwnedSlot(OwnedSlotStatus.Updating, (nint)addon, page, displaySlot);
        if (!addon->IsVisible || displaySlot < 0 || !TryGetSortedAddress(sorter, page, displaySlot,
                out var containerType, out var physicalSlot)) return pending;
        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(containerType);
        if (container == null || !container->IsLoaded || container->Items == null
            || physicalSlot < 0 || physicalSlot >= container->Size) return pending;
        var item = container->Items + physicalSlot;
        var id = GetBaseItemId(item->GetItemId());
        var hq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
        if (id == 0)
            return iconId == 0 ? new(OwnedSlotStatus.Ready, (nint)addon, page, displaySlot,
                containerType, physicalSlot) : pending;
        if (!_data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id, out var row)
            || !MatchesDrawing(iconId, row.Icon, hq, quantity, (uint)item->Quantity)) return pending;
        return new(OwnedSlotStatus.Ready, (nint)addon, page, displaySlot,
            containerType, physicalSlot, id, (uint)item->Quantity, hq);
    }

    internal static int DisplayPage(string grid, int standardTab, int largeTab) => grid switch
    {
        "InventoryGrid" when standardTab is >= 0 and < 4 => standardTab,
        "InventoryGrid0" when largeTab is >= 0 and < 2 => largeTab * 2,
        "InventoryGrid1" when largeTab is >= 0 and < 2 => largeTab * 2 + 1,
        "InventoryGrid0E" => 0, "InventoryGrid1E" => 1,
        "InventoryGrid2E" => 2, "InventoryGrid3E" => 3,
        _ => -1,
    };

    internal static ItemOrderModuleSorter* GetArmourySorter(ItemOrderModule* order, int tab)
        => order == null || tab < 0 || tab >= order->ArmourySorter.Length
            ? null : order->ArmourySorter[tab].Value;

    private static int VisibleTab(string name)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return -1;
        for (var i = 0; i < manager->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var a = manager->AllLoadedUnitsList.Entries[i].Value;
            if (a == null || !a->IsVisible || a->NameString != name) continue;
            return name == "Inventory" ? ((AddonInventory*)a)->TabIndex : ((AddonInventoryLarge*)a)->TabIndex;
        }
        return -1;
    }

    internal static bool TryGetSortedAddress(ItemOrderModuleSorter* sorter, int page, int slot,
        out InventoryType container, out int physicalSlot)
    {
        container = default;
        physicalSlot = -1;
        if (sorter == null || page < 0 || slot < 0 || sorter->ItemsPerPage <= 0
            || sorter->ItemsPerPage > 140 || slot >= sorter->ItemsPerPage
            || sorter->SortFunctionIndex != -1) return false;
        var index = (long)page * sorter->ItemsPerPage + slot;
        if (index < 0 || index >= sorter->Items.Count || index >= 140) return false;
        var entry = sorter->Items[(int)index].Value;
        if (entry == null || entry->Slot >= sorter->ItemsPerPage) return false;
        var pages = sorter->InventoryType == InventoryType.Inventory1 ? 4 : 1;
        if (entry->Page >= pages) return false;
        container = (InventoryType)((int)sorter->InventoryType + entry->Page);
        physicalSlot = entry->Slot;
        return true;
    }

    internal static bool MatchesDrawing(uint icon, uint sheetIcon, bool hq, string visibleQuantity, uint count)
        => icon == sheetIcon + (hq ? HqIconOffset : 0)
            && (visibleQuantity.Length == 0 || uint.TryParse(visibleQuantity, out var drawn) && drawn == count);
}
