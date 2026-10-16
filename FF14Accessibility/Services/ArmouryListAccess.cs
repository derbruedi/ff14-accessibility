using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

internal readonly record struct ArmouryListWindow(nint Address, ushort Id);

internal interface IArmouryListAccess
{
    ArmouryListWindow Window { get; }
    bool ContextVisible { get; }
    bool TryCollect(out List<ArmouryListItem> items);
    bool OpenContext(ArmouryListWindow window, ArmouryListItem expected);
    bool ContextMatches(ArmouryListWindow window, ArmouryListItem expected);
    void CloseContext(ArmouryListWindow window, ArmouryListItem expected);
    void Close(ArmouryListWindow window);
}

internal sealed unsafe class ArmouryListAccess(IGameGui gui, InventoryService inventory, GearInfoService gear) : IArmouryListAccess
{
    public ArmouryListWindow Window
    {
        get { var board = Visible("ArmouryBoard"); return board == null ? default : new((nint)board, board->Id); }
    }
    public bool ContextVisible => Visible("ContextMenu") != null;

    public bool TryCollect(out List<ArmouryListItem> items)
    {
        items = [];
        var manager = InventoryManager.Instance();
        if (manager == null) return false;
        var order = ItemOrderModule.Instance();
        for (var tab = 0; tab < ArmouryListModel.Containers.Length; tab++)
        {
            var type = ArmouryListModel.Containers[tab];
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded || container->Items == null) { items.Clear(); return false; }
            if (!TryGetSectionSlots(ItemSlotService.GetArmourySorter(order, tab), type, container->Size, out var slots))
            { items.Clear(); return false; }
            foreach (var slot in slots)
            {
                var item = Read(container->Items + slot, type, slot);
                if (item != null) items.Add(item);
            }
        }
        return true;
    }

    internal static bool TryGetSectionSlots(ItemOrderModuleSorter* sorter, InventoryType type,
        int size, out int[] slots)
    {
        slots = [];
        if (size < 0 || size > 140) return false;
        if (sorter == null) { slots = Enumerable.Range(0, size).ToArray(); return true; }
        var ordered = new int[size];
        var seen = new HashSet<int>();
        for (var display = 0; display < size; display++)
        {
            if (!ItemSlotService.TryGetSortedAddress(sorter, 0, display, out var container, out var slot)
                || container != type || slot >= size || !seen.Add(slot)) return false;
            ordered[display] = slot;
        }
        slots = ordered;
        return true;
    }

    private ArmouryListItem? Read(InventoryItem* item, InventoryType type, int slot)
    {
        var id = item->GetItemId();
        if (id == 0 || item->Quantity <= 0) return null;
        var hq = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
        var baseId = ItemSlotService.GetBaseItemId(id);
        var hasMateria = false;
        foreach (var materia in item->Materia) hasMateria |= materia != 0;
        var label = DescribeLabel(id, (uint)item->Quantity, hq, hasMateria,
            inventory.ResolveItemLabel, gear.DescribeGear);
        var sets = RaptureGearsetModule.Instance();
        var registered = sets != null && sets->NumGearsets > 0 && sets->IsItemRegisteredToGearset(item);
        if (registered) label += AccessibilityStrings.InGearsetShort;
        var detail = registered ? AccessibilityStrings.InGearsetWarning : string.Empty;
        if (type != InventoryType.ArmorySoulCrystal)
        {
            var condition = item->GetConditionPercentage();
            if (condition < 100) label += ", " + AccessibilityStrings.ItemCondition(condition);
            detail += " " + AccessibilityStrings.ItemCondition(condition);
        }
        // Includes materia, condition, glamour and binding. Replacing the same
        // ID in the same slot cannot pass the native action's instance check.
        var instance = Convert.ToHexString(new ReadOnlySpan<byte>(item, sizeof(InventoryItem)));
        return new(type, slot, id, (uint)item->Quantity, hq, instance, label, detail.Trim());
    }

    // The short mode belongs to the overview of worn gear, not to browsing
    // individual items. Keep the same full values for every Armoury category.
    internal static string DescribeLabel(uint rawId, uint quantity, bool highQuality, bool hasMateria,
        Func<uint, string> itemLabel, Func<uint, bool, string> describeGear)
    {
        var baseId = ItemSlotService.GetBaseItemId(rawId);
        var label = itemLabel(baseId) + (highQuality ? AccessibilityStrings.HighQuality : string.Empty);
        if (quantity > 1) label = AccessibilityStrings.ItemQuantity(quantity.ToString(), label);
        var gear = describeGear(baseId, false);
        if (!string.IsNullOrWhiteSpace(gear))
        {
            if (highQuality || hasMateria) label += ", " + AccessibilityStrings.ArmouryListBaseStats;
            label += ", " + gear;
        }
        return label;
    }

    private bool ItemMatches(ArmouryListItem expected)
    {
        var manager = InventoryManager.Instance();
        var container = manager == null ? null : manager->GetInventoryContainer(expected.Container);
        return container != null && container->IsLoaded && container->Items != null
            && expected.Slot >= 0 && expected.Slot < container->Size
            && ArmouryListModel.CanAct(expected, Read(container->Items + expected.Slot, expected.Container, expected.Slot));
    }

    public bool OpenContext(ArmouryListWindow window, ArmouryListItem expected)
    {
        if (Window != window || window.Address == 0 || ContextVisible || !ItemMatches(expected)) return false;
        var agent = AgentInventoryContext.Instance();
        if (agent == null) return false;
        agent->OpenForItemSlot(expected.Container, expected.Slot, 0, window.Id);
        return TargetMatches(window, expected) && ItemMatches(expected);
    }

    public bool ContextMatches(ArmouryListWindow window, ArmouryListItem expected)
        => Window == window && TargetMatches(window, expected) && ItemMatches(expected);

    private static bool TargetMatches(ArmouryListWindow window, ArmouryListItem expected)
    {
        var agent = AgentInventoryContext.Instance();
        return agent != null && agent->TargetInventoryId == expected.Container
            && agent->TargetInventorySlotId == expected.Slot && agent->OwnerAddonId == window.Id
            && agent->TargetInventorySlot != null && agent->TargetInventorySlot->GetItemId() == expected.ItemId;
    }

    public void CloseContext(ArmouryListWindow window, ArmouryListItem expected)
    {
        // Never close an unrelated item menu that took over while we waited.
        var agent = AgentInventoryContext.Instance();
        var context = Visible("ContextMenu");
        if (context != null && agent != null && agent->OwnerAddonId == window.Id
            && agent->TargetInventoryId == expected.Container && agent->TargetInventorySlotId == expected.Slot)
            context->Close(true);
    }

    public void Close(ArmouryListWindow window)
    {
        var board = Visible("ArmouryBoard");
        if (board != null && Window == window && window.Address != 0) board->Close(true);
    }

    private AtkUnitBase* Visible(string name)
    {
        var ptr = gui.GetAddonByName(name);
        var addon = ptr.IsNull ? null : (AtkUnitBase*)(nint)ptr;
        return addon != null && addon->IsVisible ? addon : null;
    }
}
