using FFXIVClientStructs.FFXIV.Client.Game;

namespace FF14Accessibility.Services;

internal sealed record ArmouryListItem(InventoryType Container, int Slot, uint ItemId,
    uint Quantity, bool HighQuality, string Instance, string Label, string Detail = "")
{
    internal uint BaseItemId => ItemSlotService.GetBaseItemId(ItemId);
}

// A row identifies a physical item, never a displayed index or an icon.
internal sealed class ArmouryListModel
{
    internal const int Columns = 5;
    internal static readonly InventoryType[] Containers =
    [
        InventoryType.ArmoryMainHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
        InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
        InventoryType.ArmoryOffHand, InventoryType.ArmoryEar, InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.ArmorySoulCrystal,
    ];

    internal IReadOnlyList<ArmouryListItem> Items { get; private set; } = [];
    internal int Cursor { get; private set; }
    internal ArmouryListItem? Selected => Items.Count == 0 ? null : Items[Cursor];

    internal bool Replace(IEnumerable<ArmouryListItem> items)
    {
        var previous = Selected;
        // The collector supplies the game's order. Translation must not move
        // items, and category controls never become rows in this grid.
        var fresh = items.Where(i => Containers.Contains(i.Container) && i.ItemId != 0 && i.Quantity > 0)
            .ToArray();
        Items = fresh;
        var same = previous == null ? -1 : Array.FindIndex(fresh, i => SameAddress(i, previous));
        Cursor = same >= 0 ? same : Math.Clamp(Cursor, 0, Math.Max(0, fresh.Length - 1));
        // Spiritbond and other instance bytes may update while a long manual
        // description is being spoken. They still invalidate native actions,
        // but must not interrupt speech when the announced facts did not change.
        return previous == null ? Selected != null : Selected == null
            || !SameAddress(previous, Selected) || previous.Label != Selected.Label
            || previous.Detail != Selected.Detail;
    }

    internal void Move(int direction)
    {
        if (Items.Count > 0) Cursor = (Cursor + direction + Items.Count) % Items.Count;
    }

    internal void MoveRow(int direction)
    {
        if (Items.Count == 0) return;
        var rows = (Items.Count + Columns - 1) / Columns;
        var row = (Cursor / Columns + direction + rows) % rows;
        Cursor = Math.Min(row * Columns + Cursor % Columns, Items.Count - 1);
    }

    internal void End(bool last) => Cursor = last ? Math.Max(0, Items.Count - 1) : 0;
    internal void MoveCategory(int direction)
    {
        if (Selected is not { } selected || direction is not (-1 or 1)) return;
        var current = Array.IndexOf(Containers, selected.Container);
        for (var step = 1; step <= Containers.Length; step++)
        {
            var category = (current + direction * step + Containers.Length) % Containers.Length;
            for (var index = 0; index < Items.Count; index++)
            {
                if (Items[index].Container != Containers[category]) continue;
                Cursor = index;
                return;
            }
        }
    }

    internal static string CategoryName(InventoryType type) => type switch
    {
        InventoryType.ArmoryMainHand => AccessibilityStrings.ArmouryMainHand,
        InventoryType.ArmoryHead => AccessibilityStrings.SlotHead,
        InventoryType.ArmoryBody => AccessibilityStrings.SlotBody,
        InventoryType.ArmoryHands => AccessibilityStrings.SlotHands,
        InventoryType.ArmoryLegs => AccessibilityStrings.SlotLegs,
        InventoryType.ArmoryFeets => AccessibilityStrings.SlotFeet,
        InventoryType.ArmoryOffHand => AccessibilityStrings.SlotOffHand,
        InventoryType.ArmoryEar => AccessibilityStrings.SlotEars,
        InventoryType.ArmoryNeck => AccessibilityStrings.SlotNeck,
        InventoryType.ArmoryWrist => AccessibilityStrings.SlotWrists,
        InventoryType.ArmoryRings => AccessibilityStrings.SlotRing,
        InventoryType.ArmorySoulCrystal => AccessibilityStrings.SlotSoulCrystal,
        _ => string.Empty,
    };

    internal void Clear() { Items = []; Cursor = 0; }
    internal static bool SameAddress(ArmouryListItem a, ArmouryListItem b)
        => a.Container == b.Container && a.Slot == b.Slot && a.ItemId == b.ItemId
            && a.HighQuality == b.HighQuality;
    internal static bool CanAct(ArmouryListItem expected, ArmouryListItem? current)
        => current != null && SameAddress(expected, current) && expected.Instance == current.Instance;
}
