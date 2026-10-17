using System;
using System.Collections.Generic;
using System.Linq;

namespace FF14Accessibility.Services;

/// <summary>
/// One carried stack for the spoken inventory menu. Grouping uses the game's
/// <c>Item.ItemUICategory</c> (or the key-item container). <see cref="Container"/>
/// and <see cref="Slot"/> are the physical slot, not the sorted display index.
/// The bag order in the game is left alone.
/// </summary>
internal sealed record InventoryBrowseItem(
    string GroupKey,
    uint SortOrder,
    string GroupName,
    string Name,
    int Quantity,
    bool IsHq,
    bool InGearset,
    uint BaseItemId,
    bool IsKeyItem,
    string Detail = "",
    uint Container = 0,
    int Slot = -1,
    uint ItemId = 0)
{
    internal string Label
    {
        get
        {
            var hq = IsHq ? AccessibilityStrings.HighQuality : string.Empty;
            var set = InGearset ? AccessibilityStrings.InGearsetShort : string.Empty;
            var core = Quantity > 1
                ? AccessibilityStrings.ItemStack(Name, Quantity, hq)
                : $"{Name}{hq}";
            return core + set;
        }
    }

    internal string SpokenDetail => string.IsNullOrWhiteSpace(Detail) ? Label : Detail;
}

internal sealed record InventoryBrowseGroup(
    string Key, uint SortOrder, string Name, IReadOnlyList<InventoryBrowseItem> Items);

/// <summary>
/// Builds the two-level spoken inventory: categories first, items inside a
/// category, Num4 back to the category list. Empty groups are omitted.
/// </summary>
internal static class InventoryBrowse
{
    internal const string KeyItemsGroup = "key";
    internal const uint KeyItemsSort = 1;
    internal const uint CategorySortBase = 1000;

    internal static string CategoryGroupKey(uint categoryId) => "c:" + categoryId;

    internal static IReadOnlyList<InventoryBrowseGroup> Group(IEnumerable<InventoryBrowseItem> items)
    {
        return items
            .Where(item => item.Quantity > 0 && item.Name.Length > 0 && item.GroupName.Length > 0)
            .GroupBy(item => item.GroupKey, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var ordered = group
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.IsHq)
                    .ThenBy(item => item.BaseItemId)
                    .ThenBy(item => item.Slot)
                    .ToList();
                return new InventoryBrowseGroup(first.GroupKey, first.SortOrder, first.GroupName, ordered);
            })
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static MenuLevel? BuildMenu(
        int gil,
        string gilName,
        IReadOnlyList<InventoryBrowseItem> items,
        Action<string> speak,
        Func<(int Gil, string GilName, IReadOnlyList<InventoryBrowseItem> Items)> snapshot,
        Func<InventoryBrowseItem, bool>? tryOpenItem = null)
    {
        if (gil < 0 && items.Count == 0) return null;
        return Categories(gil, gilName, items, speak, snapshot, tryOpenItem);
    }

    private static MenuLevel Categories(
        int gil,
        string gilName,
        IReadOnlyList<InventoryBrowseItem> items,
        Action<string> speak,
        Func<(int Gil, string GilName, IReadOnlyList<InventoryBrowseItem> Items)> snapshot,
        Func<InventoryBrowseItem, bool>? tryOpenItem)
    {
        var entries = new List<MenuEntry>();
        if (gil >= 0)
        {
            entries.Add(new MenuEntry
            {
                Label = $"{gilName}: {gil}",
                Key = "gil",
                StayOpen = true,
                Activate = () =>
                {
                    var fresh = snapshot();
                    speak($"{fresh.GilName}: {fresh.Gil}");
                }
            });
        }

        foreach (var group in Group(items))
        {
            var key = group.Key;
            var title = group.Name;
            entries.Add(new MenuEntry
            {
                Label = AccessibilityStrings.InventoryCategoryRow(group.Name, group.Items.Count),
                Key = key,
                NeighbourLabel = group.Name,
                Submenu = () => ItemsIn(key, title, speak, snapshot, tryOpenItem)
            });
        }

        return new MenuLevel
        {
            Title = AccessibilityStrings.InventoryMenuTitle,
            Intro = AccessibilityStrings.InventoryMenuHelp,
            Entries = entries,
            Rebuild = () =>
            {
                var fresh = snapshot();
                return Categories(fresh.Gil, fresh.GilName, fresh.Items, speak, snapshot, tryOpenItem)
                    ?? new MenuLevel
                    {
                        Title = AccessibilityStrings.InventoryMenuTitle,
                        Entries = []
                    };
            }
        };
    }

    private static MenuLevel ItemsIn(
        string key,
        string title,
        Action<string> speak,
        Func<(int Gil, string GilName, IReadOnlyList<InventoryBrowseItem> Items)> snapshot,
        Func<InventoryBrowseItem, bool>? tryOpenItem)
    {
        var fresh = snapshot();
        var group = Group(fresh.Items).FirstOrDefault(candidate => candidate.Key == key);
        var rows = group?.Items ?? [];
        var name = group?.Name ?? title;
        return new MenuLevel
        {
            Title = name,
            Intro = tryOpenItem == null ? string.Empty : AccessibilityStrings.InventoryItemMenuHelp,
            Entries = rows.Select(item => ItemRow(item, speak, tryOpenItem)).ToList(),
            Rebuild = () => ItemsIn(key, title, speak, snapshot, tryOpenItem)
        };
    }

    private static MenuEntry ItemRow(
        InventoryBrowseItem item, Action<string> speak, Func<InventoryBrowseItem, bool>? tryOpenItem)
    {
        var captured = item;
        // The row object must exist before the lambda runs. StayOpen flips only
        // after the game menu is requested, so a failed open leaves the list up.
        MenuEntry row = null!;
        row = new MenuEntry
        {
            Label = captured.Label,
            Key = $"{captured.GroupKey}:{captured.Container}:{captured.Slot}:{captured.ItemId}:{(captured.IsHq ? "hq" : "nq")}",
            StayOpen = true,
            Activate = () =>
            {
                if (tryOpenItem == null)
                {
                    speak(captured.SpokenDetail);
                    return;
                }
                if (tryOpenItem(captured)) row.StayOpen = false;
            }
        };
        return row;
    }
}
