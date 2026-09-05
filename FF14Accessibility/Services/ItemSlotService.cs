using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Answers "which item is in the slot under the cursor" from the GAME'S OWN
/// item-detail agent, not from the icon drawn on the slot.
///
/// WHY THIS EXISTS: item slots carry no text node, so the name used to come from
/// a reverse lookup of the icon id (InventoryService.ResolveIconItem). An icon
/// does not identify an item. Measured against the shipped Item sheet
/// (game data of 2026-09-04, 52801 rows): 26223 distinct icons carry 50773 named
/// items, and 9198 of those icons are shared by more than one item - 33748 items
/// in total. Wood is the worst corner of the sheet and the one the user hit:
///   icon 22416 = 27 items - Maple Branch, Ash Branch, Willow, Cedar, Elm, Yew, ...
///   icon 22403 = 10 items - Chestnut Log, Teak Log, Larch Log, Red Pine Log, ...
/// Three neighbouring bag slots holding Maple, Ash and Yew Branch therefore all
/// announced "Ash Branch" (log 2026-09-04 16:24, [ItemSlotProbe]), because both
/// icon maps in InventoryService are last-writer-wins per icon. For a blind
/// player that is not cosmetic: it is handing over the wrong item at a turn-in.
///
/// WHAT WAS TRIED FIRST AND IS DISPROVEN - keep it that way. The obvious fix was
/// to take the slot's index in its addon's slot array (AddonInventoryGrid holds
/// 35 AtkComponentDragDrop*) and read that slot out of the inventory container.
/// The probe measured it and it does NOT work: the grid index is a SORTED DISPLAY
/// position, not a container slot, and the view spans every bag at once. Log
/// 2026-09-04 23:25, one focus change with all four bags dumped at that index:
///   grid index 2 showed Sticky Rice   - which really sits in Inventory3 slot 8
///   grid index 3 showed Lalafellin Lentil -                  Inventory2 slot 5
///   grid index 1 showed Buffalo Beans -                      Inventory2 slot 3
/// 41 focus changes, 41 misses, not one container hit. Mapping a display index
/// back to a slot would mean re-implementing the client's sort order, which would
/// go wrong silently the moment the player re-sorts.
///
/// WHAT DOES WORK: AgentItemDetail. It carries the item of the slot the cursor is
/// on (ItemId @312), and - contrary to what the tooltip WINDOW behaviour in
/// TooltipService suggested - it does follow KEYBOARD focus: every probe line
/// read kind=InventoryItem, shown=True. The one catch is timing. At the moment the
/// focus reader runs, the agent still describes the slot the cursor just LEFT; it
/// catches up within a single frame. Measured over 41 focus changes: the read one
/// frame later named the right item every single time, with Index equal to the
/// grid index.
///
/// So the agent is asked, and believed only once it is PROVEN to be about this
/// slot - see <see cref="TryResolve"/>. Until then the caller waits a frame
/// rather than announcing the previous item.
/// </summary>
/// <summary>Why <see cref="ItemSlotService.TryResolve"/> could not answer yet, and
/// therefore how long it is worth waiting.</summary>
public enum SlotWait
{
    /// <summary>Nothing to wait for - the agent does not cover this slot at all
    /// (quest reward panels, some shop rows). Fall back at once.</summary>
    None,

    /// <summary>The agent is open but still describes the slot the cursor just
    /// left. Measured: it is right on the very next frame, every time.</summary>
    AgentBehind,

    /// <summary>The detail window has not opened yet, which happens when the bag
    /// or armoury itself was only just opened. Worth real patience: it IS coming,
    /// nothing else can name the slot correctly meanwhile, and the window's own
    /// "Inventory"/"Bag 3" announcements cover the delay.</summary>
    AgentOpening,
}

public sealed unsafe class ItemSlotService
{
    private readonly IDataManager _data;

    /// <summary>What the game adds to an icon id to draw its HQ variant. The Item
    /// sheet only ever holds the plain id, so a slot icon has to come back down by
    /// this before it can be compared against a row.</summary>
    private const uint HqIconOffset = 1_000_000;

    /// <summary>
    /// Whether a slot is drawing the HQ face of its item - the symbol a sighted
    /// player reads quality off.
    ///
    /// The offset is the game's own, on the ICON as well as on the item id, and it
    /// is measured, not assumed: the two Honey stacks in the bag drew 1025102 and
    /// 25102 for a sheet row whose icon is 25102, while the detail agent named the
    /// first one 1004850 against a row id of 4850 (log 2026-09-05 00:11).
    ///
    /// Taken from the icon rather than from the agent on purpose - the icon is
    /// there on every path, including the fallback where the agent never answered,
    /// so quality cannot go missing just because the name came the other way.
    /// </summary>
    public static bool IsHighQuality(uint iconId) => iconId >= HqIconOffset;

    public ItemSlotService(IDataManager data) => _data = data;

    /// <summary>
    /// The Item sheet row of the slot the given component belongs to, or 0 when
    /// the agent cannot yet be shown to describe THIS slot.
    ///
    /// <paramref name="wait"/> tells the caller WHY a 0 came back, because the two
    /// reasons deserve very different patience - see <see cref="SlotWait"/>.
    /// </summary>
    /// <param name="comp">The focused slot component.</param>
    /// <param name="iconId">The icon the game is drawing on that slot.</param>
    /// <param name="lastSpokenItemId">Item announced for the PREVIOUS focus - what
    /// a stale agent looks like when the slot index cannot be checked.</param>
    /// <param name="acceptOnIconAlone">Set once the caller has waited long enough:
    /// drops the "differs from the last one" test, which two neighbouring slots
    /// holding the SAME item would otherwise never pass.</param>
    public uint TryResolve(AtkComponentBase* comp, uint iconId, uint lastSpokenItemId,
                           bool acceptOnIconAlone, out SlotWait wait)
    {
        wait = SlotWait.None;
        if (iconId == 0) return 0;

        // PROOF 1's key, and also the marker for "this is a window where the agent
        // is known to answer" - so it is needed before the agent is even read.
        var slotIndex = SlotIndex(comp);

        var agent = AgentItemDetail.Instance();
        if (agent == null || !agent->IsAddonShown())
        {
            // Not shown yet. In the slot-array windows that is temporary - the game
            // opens the detail a moment after the cursor lands, and 7 of the 62
            // focus changes in the probe run 2026-09-05 00:00 caught exactly that
            // gap, all of them right after a tab switch. Waiting is right there.
            // Everywhere else (quest reward panels, some shop rows) it never opens
            // at all, and waiting would only delay the icon fallback.
            wait = slotIndex >= 0 ? SlotWait.AgentOpening : SlotWait.None;
            return 0;
        }

        // The agent keeps the HQ/collectible offset applied; sheet lookups need the
        // base id. Dalamud owns that mapping - the same call DescribeGearsetMark uses.
        var itemId = Dalamud.Utility.ItemUtil.GetBaseId(agent->ItemId).ItemId;
        if (itemId == 0) return 0;

        // PROOF 1, the strong one: the agent's Index is the slot's position in its
        // addon's slot array, and that array we can read. Measured equal on every
        // caught-up probe line - 27 in the inventory grid and 35 in the armoury
        // board, so the two windows number their slots the same way.
        if (slotIndex >= 0 && agent->Index == (uint)slotIndex) return itemId;

        // PROOF 2, for slots whose addon holds no readable slot array: the item the
        // agent names must at least be drawn with the icon this slot is drawing.
        // An HQ slot draws its icon a million higher (measured: HQ Honey on icon
        // 1025102, the sheet row carries 25102), and the sheet has no such rows -
        // so without stripping that offset every HQ item would count as a mismatch.
        var slotIcon = IsHighQuality(iconId) ? iconId - HqIconOffset : iconId;
        if (!_data.GetExcelSheet<LuminaItem>().TryGetRow(itemId, out var row) || row.Icon != slotIcon)
        {
            wait = SlotWait.AgentBehind;   // live agent, wrong slot - it catches up next frame
            return 0;
        }

        // The icon alone cannot separate a caught-up agent from a stale one when
        // both slots share an icon - which is exactly the branches. So while there
        // is still time to wait, an item equal to the one just announced counts as
        // stale; after the wait it is accepted, because by then it is far more
        // likely to be two stacks of the same thing.
        if (!acceptOnIconAlone && itemId == lastSpokenItemId && lastSpokenItemId != 0)
        {
            wait = SlotWait.AgentBehind;
            return 0;
        }

        return itemId;
    }

    /// <summary>
    /// Position of a slot in its addon's slot array, or -1 when the addon holds
    /// none this code may read.
    ///
    /// The three addons below are the only ones whose struct is KNOWN to match the
    /// name, and the names are not guessed - they are the AddonAttribute
    /// identifiers on the structs themselves (FFXIVClientStructs 7.55.1). Reading
    /// a slot array out of an addon whose struct does not match would be a wild
    /// pointer read, and a crash is worse than a slow name.
    /// </summary>
    private int SlotIndex(AtkComponentBase* comp)
    {
        var dragDrop = FindDragDrop(comp);
        if (dragDrop == null) return -1;

        var addon = FindAddon((AtkResNode*)dragDrop->OwnerNode);
        if (addon == null) return -1;

        return addon->NameString switch
        {
            // AddonInventoryGrid: InventoryGrid, InventoryGrid0/1, InventoryGrid0E..3E
            "InventoryGrid"   or "InventoryGrid0"  or "InventoryGrid1"  or
            "InventoryGrid0E" or "InventoryGrid1E" or
            "InventoryGrid2E" or "InventoryGrid3E" => IndexOf(((AddonInventoryGrid*)addon)->Slots, dragDrop),
            "ArmouryBoard"                         => IndexOf(((AddonArmouryBoard*)addon)->Slots, dragDrop),
            // UNMEASURED, and knowingly so - the player has no chocobo yet
            // (2026-09-05), so no run has ever opened this window. Reading the array
            // is safe (the struct matches the name), but whether the agent's Index
            // lines up with it is a guess, and there is a concrete reason to doubt
            // it: this array is 70 long because the window shows TWO pages of 35 at
            // once, so the agent may well count per page and match only the first.
            // Costs nothing if it is wrong - proof 1 simply fails, proof 2 or the
            // icon fallback answers instead, exactly as before this service existed.
            // Whether it does line up is still unmeasured; the first saddlebag
            // session will show it.
            "InventoryBuddy"                       => IndexOf(((AddonInventoryBuddy*)addon)->Slots, dragDrop),
            _                                      => -1,
        };
    }

    /// <summary>Position of a slot component in an addon's slot array, or -1.</summary>
    private static int IndexOf(Span<Pointer<AtkComponentDragDrop>> slots, AtkComponentDragDrop* dragDrop)
    {
        for (var i = 0; i < slots.Length; i++)
            if (slots[i].Value == dragDrop) return i;
        return -1;
    }

    /// <summary>The DragDrop component a slot belongs to: the component itself, or
    /// the one a few levels above it (the focus sits on a collision child, and an
    /// Icon component is wrapped by its DragDrop). null when there is none.</summary>
    private static AtkComponentDragDrop* FindDragDrop(AtkComponentBase* comp)
    {
        if (comp == null) return null;
        if (comp->GetComponentType() == ComponentType.DragDrop) return (AtkComponentDragDrop*)comp;

        var node = (AtkResNode*)comp->OwnerNode;
        for (var up = 0; up < 3 && node != null; up++, node = node->ParentNode)
        {
            if ((int)node->Type < 1000) continue;
            var parent = ((AtkComponentNode*)node)->Component;
            if (parent != null && parent->GetComponentType() == ComponentType.DragDrop)
                return (AtkComponentDragDrop*)parent;
        }
        return null;
    }

    /// <summary>The addon a node belongs to, found by its root node - the same
    /// climb UIReaderService.FindAddonNameForNode uses.</summary>
    private static AtkUnitBase* FindAddon(AtkResNode* node)
    {
        if (node == null) return null;

        var root  = node;
        var guard = 0;
        while (root->ParentNode != null && guard++ < 64) root = root->ParentNode;

        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return null;

        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var a = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (a != null && a->RootNode == root) return a;
        }
        return null;
    }
}
