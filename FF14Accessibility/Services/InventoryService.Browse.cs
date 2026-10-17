using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaItemUICategory = Lumina.Excel.Sheets.ItemUICategory;

namespace FF14Accessibility.Services;

public sealed partial class InventoryService
{
    private InventoryContextMenu? _contextMenu;
    private readonly List<int> _heldConfirmKeys = new();

    /// <summary>True while the key that opened an item menu is still held.
    /// The plugin clears that key until release so it cannot confirm the first row.</summary>
    internal Func<int, bool>? ConfirmKeyIsDown { get; set; }

    /// <summary>Supplies the game UI used to open an item's context menu.</summary>
    internal void UseGameGui(IGameGui gui) => _contextMenu = new InventoryContextMenu(gui, _log);

    /// <summary>
    /// Clears the confirm key that opened the game menu, until the player lets go.
    /// Call once per frame before other key handling.
    /// </summary>
    public void SuppressOpenContextHold(IKeyState keys)
    {
        for (var i = _heldConfirmKeys.Count - 1; i >= 0; i--)
        {
            var vk = _heldConfirmKeys[i];
            if (ConfirmKeyIsDown == null || !ConfirmKeyIsDown(vk))
                _heldConfirmKeys.RemoveAt(i);
            else
                keys[(VirtualKey)vk] = false;
        }
    }

    /// <summary>
    /// Finishes an item menu that is waiting for the game to show the inventory.
    /// Call once per frame after <see cref="SuppressOpenContextHold"/>.
    /// </summary>
    public void PollPendingItemMenu()
    {
        if (_contextMenu == null || !_contextMenu.TryFinishPending(out var result)) return;
        if (result is InventoryContextResult.Opened or InventoryContextResult.Pending) return;
        SpeakContextResult(result);
    }

    /// <summary>
    /// Spoken two-level inventory: categories from <c>Item.ItemUICategory</c>,
    /// key items and crystals as their own groups, gil as a top-level row.
    /// Confirming an item asks the game for that slot's context menu.
    /// Null when nothing is readable yet.
    /// </summary>
    public MenuLevel? BuildBrowseMenu()
    {
        var snapshot = SnapshotBrowse();
        var menu = InventoryBrowse.BuildMenu(
            snapshot.Gil, snapshot.GilName, snapshot.Items, text => _tolk.SpeakInterrupt(text),
            SnapshotBrowse, TryOpenBrowseItem);
        if (menu != null)
            _log.Info($"[Inventory] Kategorie-Menü: gil={(snapshot.Gil >= 0)} " +
                      $"gruppen={InventoryBrowse.Group(snapshot.Items).Count} " +
                      $"stücke={snapshot.Items.Count}.");
        return menu;
    }

    private (int Gil, string GilName, IReadOnlyList<InventoryBrowseItem> Items) SnapshotBrowse()
        => (GetGil(), ResolveItemName(1), CollectBrowseItems());

    private List<InventoryBrowseItem> CollectBrowseItems()
    {
        var result = new List<InventoryBrowseItem>();
        foreach (var page in BagPages)
            AddSheetBrowseItems(result, page);
        AddKeyBrowseItems(result);
        AddSheetBrowseItems(result, GameInventoryType.Crystals);
        return result;
    }

    private void AddSheetBrowseItems(List<InventoryBrowseItem> result, GameInventoryType container)
    {
        var slot = 0;
        foreach (var item in _inventory.GetInventoryItems(container))
        {
            // The span is the container itself. Its index is the slot
            // OpenForItemSlot addresses, not the sorted position in the window.
            var physical = slot++;
            if (item.IsEmpty || item.ItemId == 0) continue;
            var name = ResolveItemLabel(item.BaseItemId);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var categoryId = 0u;
            if (_data.GetExcelSheet<LuminaItem>().TryGetRow(item.BaseItemId, out var row))
                categoryId = row.ItemUICategory.RowId;
            var categoryName = ResolveCategoryName(categoryId);
            var set = IsRegisteredToGearset(item);
            if ((int)item.InventorySlot != physical)
                _log.Info($"[Inventory] Slotfeld {item.InventorySlot} weicht vom Fach {physical} in {container} ab.");
            result.Add(new InventoryBrowseItem(
                InventoryBrowse.CategoryGroupKey(categoryId),
                InventoryBrowse.CategorySortBase + categoryId,
                categoryName,
                name,
                item.Quantity,
                item.IsHq,
                set,
                item.BaseItemId,
                false,
                DescribeBrowseItem(name, item.Quantity, item.IsHq, set, item.BaseItemId, false),
                (uint)container,
                physical,
                item.ItemId));
        }
    }

    private void AddKeyBrowseItems(List<InventoryBrowseItem> result)
    {
        var slot = 0;
        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.KeyItems))
        {
            var physical = slot++;
            if (item.IsEmpty || item.ItemId == 0) continue;
            var name = ResolveKeyItemName(item.ItemId);
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.Add(new InventoryBrowseItem(
                InventoryBrowse.KeyItemsGroup,
                InventoryBrowse.KeyItemsSort,
                AccessibilityStrings.InventoryKeyItemsCategory,
                name,
                item.Quantity,
                false,
                false,
                item.ItemId,
                true,
                DescribeBrowseItem(name, item.Quantity, false, false, 0, true),
                (uint)GameInventoryType.KeyItems,
                physical,
                item.ItemId));
        }
    }

    private bool TryOpenBrowseItem(InventoryBrowseItem item)
    {
        if (_contextMenu == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.InventoryContextFailed);
            return false;
        }

        InventoryContextResult result;
        try
        {
            result = _contextMenu.TryOpen(item.Container, item.Slot, item.ItemId);
        }
        catch (Exception ex)
        {
            _log.Error($"[Inventory] Context menu failed: {ex.Message}");
            result = InventoryContextResult.Failed;
        }

        if (result is InventoryContextResult.Opened or InventoryContextResult.Pending)
        {
            _heldConfirmKeys.Clear();
            foreach (var vk in SpokenMenu.KeysConfirm)
                if (ConfirmKeyIsDown?.Invoke(vk) == true) _heldConfirmKeys.Add(vk);
            if (_heldConfirmKeys.Count == 0) _heldConfirmKeys.Add(0x60);
            return true;
        }

        SpeakContextResult(result);
        return false;
    }

    private void SpeakContextResult(InventoryContextResult result)
    {
        _tolk.SpeakInterrupt(result switch
        {
            InventoryContextResult.Changed => AccessibilityStrings.InventoryContextChanged,
            InventoryContextResult.NoOwner => AccessibilityStrings.InventoryContextNoWindow,
            _ => AccessibilityStrings.InventoryContextFailed,
        });
    }

    private string ResolveCategoryName(uint categoryId)
    {
        if (categoryId != 0
            && _data.GetExcelSheet<LuminaItemUICategory>().TryGetRow(categoryId, out var row))
        {
            var name = RussianGameText.Name(_data, row, x => x.Name).Trim();
            if (name.Length > 0) return name;
        }
        return AccessibilityStrings.InventoryUnknownCategory;
    }

    private string DescribeBrowseItem(
        string name, int quantity, bool hq, bool set, uint baseItemId, bool keyItem)
    {
        var hqSuffix = hq ? AccessibilityStrings.HighQuality : string.Empty;
        var setSuffix = set ? AccessibilityStrings.InGearsetShort : string.Empty;
        var head = (quantity > 1
            ? AccessibilityStrings.ItemStack(name, quantity, hqSuffix)
            : $"{name}{hqSuffix}") + setSuffix;
        if (keyItem || baseItemId == 0) return head;

        var text = head;
        var basics = _gear.DescribeItemBasics(baseItemId);
        if (basics.Length > 0) text += ". " + basics;
        var gear = _gear.DescribeGear(baseItemId);
        if (gear.Length > 0) text += ". " + gear;
        var classes = _gear.DescribeOwnClasses(baseItemId);
        if (classes.Length > 0) text += classes;
        var description = FlattenDescription(_descriptions.Item(baseItemId));
        if (description.Length > 0) text += ". " + description;
        return text;
    }

    private static string FlattenDescription(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var flat = text.Replace('\r', ' ').Replace('\n', ' ');
        while (flat.Contains("  ", StringComparison.Ordinal))
            flat = flat.Replace("  ", " ", StringComparison.Ordinal);
        return flat.Trim();
    }
}
