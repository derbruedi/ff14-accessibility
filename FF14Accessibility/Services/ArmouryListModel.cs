using FFXIVClientStructs.FFXIV.Client.Game;

namespace FF14Accessibility.Services;

internal sealed record ArmouryListItem(InventoryType Container, int Slot, uint ItemId,
    uint Quantity, bool HighQuality, string Instance, string Label, string Detail = "",
    uint ClassId = 0, string ClassName = "")
{
    internal uint BaseItemId => ItemSlotService.GetBaseItemId(ItemId);
}

/// <summary>One ClassJobCategory that currently holds at least one item.
/// The name is already the spoken label. An empty name is the unknown group.</summary>
internal readonly record struct ArmouryClassGroup(uint Id, string Name, int Count);

// A row identifies a physical item, never a displayed index or an icon.
// Items stay in the collector's order inside a class. A piece that several
// classes may wear stays in its one ClassJobCategory, so it is not repeated.
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
    internal IReadOnlyList<ArmouryClassGroup> Classes { get; private set; } = [];
    internal int Cursor { get; private set; }
    internal int ClassCursor { get; private set; }
    internal bool InsideClass { get; private set; }
    internal ArmouryListItem? Selected => Items.Count == 0 ? null : Items[Cursor];

    internal int VisibleCount
    {
        get
        {
            if (!InsideClass || Classes.Count == 0) return Items.Count;
            var id = Classes[ClassCursor].Id;
            var count = 0;
            foreach (var item in Items)
                if (item.ClassId == id) count++;
            return count;
        }
    }

    internal int VisibleIndex
    {
        get
        {
            if (!InsideClass || Classes.Count == 0) return Cursor;
            var id = Classes[ClassCursor].Id;
            var seen = 0;
            for (var index = 0; index < Items.Count; index++)
            {
                if (Items[index].ClassId != id) continue;
                if (index == Cursor) return seen;
                seen++;
            }
            return 0;
        }
    }

    internal bool Replace(IEnumerable<ArmouryListItem> items)
    {
        var previous = Selected;
        var wasInside = InsideClass;
        var hadClass = Classes.Count > 0;
        var previousClassId = hadClass ? Classes[ClassCursor].Id : 0u;
        var beforeKey = ClassKey();
        var beforeVisible = wasInside ? VisibleCount : 0;
        // The collector supplies the game's order. Translation must not move
        // items, and category controls never become rows in this grid.
        var fresh = items.Where(i => Containers.Contains(i.Container) && i.ItemId != 0 && i.Quantity > 0)
            .ToArray();
        Items = fresh;
        var same = previous == null ? -1 : Array.FindIndex(fresh, i => SameAddress(i, previous));
        Cursor = same >= 0 ? same : Math.Clamp(Cursor, 0, Math.Max(0, fresh.Length - 1));
        RebuildClasses(fresh);
        var kept = -1;
        if (hadClass)
        {
            for (var index = 0; index < Classes.Count; index++)
                if (Classes[index].Id == previousClassId) { kept = index; break; }
        }
        if (Classes.Count == 0) ClassCursor = 0;
        else if (kept >= 0) ClassCursor = kept;
        else ClassCursor = Math.Clamp(ClassCursor, 0, Classes.Count - 1);
        if (wasInside && kept >= 0)
        {
            InsideClass = true;
            if ((uint)Cursor >= (uint)fresh.Length || fresh[Cursor].ClassId != Classes[ClassCursor].Id)
                SnapToClass();
        }
        else if (wasInside) InsideClass = false;
        var spokenChanged = previous == null ? Selected != null : Selected == null
            || !SameAddress(previous, Selected) || previous.Label != Selected.Label || previous.Detail != Selected.Detail;
        if (wasInside) return !InsideClass || spokenChanged || VisibleCount != beforeVisible;
        return spokenChanged || beforeKey != ClassKey();
    }

    internal void EnterClass()
    {
        if (Classes.Count == 0) return;
        InsideClass = true;
        var id = Classes[ClassCursor].Id;
        if ((uint)Cursor >= (uint)Items.Count || Items[Cursor].ClassId != id) SnapToClass();
    }

    internal void LeaveClass() => InsideClass = false;

    internal void MoveClass(int direction)
    {
        if (Classes.Count == 0 || direction == 0) return;
        ClassCursor = (ClassCursor + direction + Classes.Count) % Classes.Count;
    }

    internal void EndClass(bool last)
    {
        if (Classes.Count == 0) return;
        ClassCursor = last ? Classes.Count - 1 : 0;
    }

    internal void Move(int direction)
    {
        if (InsideClass) { MoveVisible(direction); return; }
        if (Items.Count > 0) Cursor = (Cursor + direction + Items.Count) % Items.Count;
    }

    internal void MoveRow(int direction)
    {
        if (InsideClass) { MoveVisibleRow(direction); return; }
        if (Items.Count == 0) return;
        var rows = (Items.Count + Columns - 1) / Columns;
        var row = (Cursor / Columns + direction + rows) % rows;
        Cursor = Math.Min(row * Columns + Cursor % Columns, Items.Count - 1);
    }

    internal void End(bool last)
    {
        if (InsideClass)
        {
            var visible = VisibleIndexes();
            if (visible.Length > 0) Cursor = last ? visible[^1] : visible[0];
            return;
        }
        Cursor = last ? Math.Max(0, Items.Count - 1) : 0;
    }

    internal void MoveCategory(int direction)
    {
        if (Selected is not { } selected || direction is not (-1 or 1)) return;
        var current = Array.IndexOf(Containers, selected.Container);
        var visible = InsideClass ? VisibleIndexes() : null;
        for (var step = 1; step <= Containers.Length; step++)
        {
            var category = (current + direction * step + Containers.Length) % Containers.Length;
            if (visible != null)
            {
                foreach (var index in visible)
                {
                    if (Items[index].Container != Containers[category]) continue;
                    Cursor = index;
                    return;
                }
                continue;
            }
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

    internal void Clear()
    {
        Items = []; Classes = []; Cursor = 0; ClassCursor = 0; InsideClass = false;
    }

    internal static bool SameAddress(ArmouryListItem a, ArmouryListItem b)
        => a.Container == b.Container && a.Slot == b.Slot && a.ItemId == b.ItemId
            && a.HighQuality == b.HighQuality;
    internal static bool CanAct(ArmouryListItem expected, ArmouryListItem? current)
        => current != null && SameAddress(expected, current) && expected.Instance == current.Instance;

    private void SnapToClass()
    {
        if (Classes.Count == 0) return;
        var id = Classes[ClassCursor].Id;
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].ClassId != id) continue;
            Cursor = index;
            return;
        }
    }

    private int[] VisibleIndexes()
    {
        if (!InsideClass || Classes.Count == 0) return [];
        var id = Classes[ClassCursor].Id;
        var indexes = new List<int>();
        for (var index = 0; index < Items.Count; index++)
            if (Items[index].ClassId == id) indexes.Add(index);
        return indexes.ToArray();
    }

    private void MoveVisible(int direction)
    {
        var visible = VisibleIndexes();
        if (visible.Length == 0) return;
        var position = Array.IndexOf(visible, Cursor);
        if (position < 0) position = 0;
        Cursor = visible[(position + direction + visible.Length) % visible.Length];
    }

    private void MoveVisibleRow(int direction)
    {
        var visible = VisibleIndexes();
        if (visible.Length == 0) return;
        var position = Array.IndexOf(visible, Cursor);
        if (position < 0) position = 0;
        var rows = (visible.Length + Columns - 1) / Columns;
        var row = (position / Columns + direction + rows) % rows;
        var target = Math.Min(row * Columns + position % Columns, visible.Length - 1);
        Cursor = visible[target];
    }

    private void RebuildClasses(ArmouryListItem[] fresh)
    {
        var order = new List<uint>();
        var names = new Dictionary<uint, string>();
        var counts = new Dictionary<uint, int>();
        foreach (var item in fresh)
        {
            if (!counts.TryGetValue(item.ClassId, out var count))
            {
                order.Add(item.ClassId);
                names[item.ClassId] = item.ClassName;
                counts[item.ClassId] = 1;
            }
            else
            {
                counts[item.ClassId] = count + 1;
                if (names[item.ClassId].Length == 0 && item.ClassName.Length > 0)
                    names[item.ClassId] = item.ClassName;
            }
        }
        var groups = new List<ArmouryClassGroup>(order.Count);
        foreach (var id in order) groups.Add(new ArmouryClassGroup(id, names[id], counts[id]));
        groups.Sort(static (left, right) =>
        {
            var empty = (left.Name.Length == 0 ? 1 : 0).CompareTo(right.Name.Length == 0 ? 1 : 0);
            if (empty != 0) return empty;
            var name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : left.Id.CompareTo(right.Id);
        });
        Classes = groups;
    }

    private string ClassKey()
    {
        if (Classes.Count == 0) return "0";
        var group = Classes[ClassCursor];
        return $"{Classes.Count}|{ClassCursor}|{group.Id}|{group.Name}|{group.Count}";
    }
}
