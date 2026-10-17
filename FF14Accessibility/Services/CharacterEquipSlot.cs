using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaAddon = Lumina.Excel.Sheets.Addon;

namespace FF14Accessibility.Services;

/// <summary>
/// Names the equipment slot under the cursor in the Character window ("Kopf",
/// "Finger (links)") and tells whether anything is worn there.
///
/// WHY THIS EXISTS: the slots are icon-only DragDrop components. A sighted player
/// reads the slot off its position on the paper doll and off the silhouette drawn
/// into an empty slot; neither reaches a screen reader. Filled slots used to speak
/// only the item, empty ones nothing at all ("[Focus] STUMM", log 2026-10-03) -
/// their icon id is uint.MaxValue, not the 0 the empty-slot branch looks for.
///
/// WHICH SLOT: AddonCharacter carries no slot array (ilspycmd 2026-10-04: only
/// Tabs, TabIndex, TabCount), but every slot registers its DragDrop events with a
/// parameter of its own. Measured with [CharSlotProbe] on 2026-10-04 over all 13
/// slots, matching the item detail agent's item against the EquippedItems
/// container: params 1..5 are container slots 0..4 (main hand .. hands), params
/// 6..13 are container slots 6..13 (legs .. soul crystal). Container slot 5 is
/// the waist slot the game retired; the window has no slot for it.
///
/// WHAT IT IS CALLED: the game's own words, in the client's language. Addon rows
/// 738..751 name the equipment slots in exactly the container order, waist
/// included ("Haupthand" .. "Taille" .. "Finger (rechts)", "Finger (links)",
/// "Jobkristall"); RaptureGearsetModule.GearsetItemIndex confirms slot 11 is the
/// right ring and 12 the left. "Empty" is Addon row 4947, the word the gear set
/// viewer writes into an empty equipment slot ("Leer" / "None" / 装備なし) -
/// the other "Leer" rows mean a vacant room or an unregistered set in Japanese.
/// </summary>
internal static unsafe class CharacterEquipSlot
{
    private const string AddonName = "Character";

    /// <summary>Addon row naming container slot 0 (main hand); slot n is this + n.</summary>
    private const uint SlotNameRowBase = 738;

    /// <summary>Addon row of the gear set viewer's word for an empty slot.</summary>
    private const uint EmptySlotRow = 4947;

    /// <summary>Highest slot param the window uses (soul crystal).</summary>
    private const uint LastSlotParam = 13;

    /// <summary>First param after the retired waist slot; from here on the param
    /// equals the container slot instead of being one above it.</summary>
    private const uint FirstParamAfterWaist = 6;

    /// <summary>
    /// Describes the Character window equipment slot that owns the focused
    /// component node, or returns false when the focus is anywhere else.
    /// </summary>
    /// <param name="slotNode">The focused slot's component node.</param>
    /// <param name="addonName">Name of the addon the focus belongs to.</param>
    /// <param name="data">Game data, for the slot and "empty" words.</param>
    /// <param name="log">Plugin log; a missing word is logged, never silent.</param>
    /// <param name="slotName">The game's name of the slot.</param>
    /// <param name="emptyWord">The game's word for an empty slot when nothing is
    /// worn there, otherwise "".</param>
    /// <param name="wear">Condition and spiritbond of the worn piece ("Zustand
    /// 53 %, Bindung 12 %"), "" when empty or when the piece has neither.</param>
    public static bool TryDescribe(AtkResNode* slotNode, string addonName, IDataManager data, IPluginLog log,
                                   out string slotName, out string emptyWord, out string wear)
    {
        slotName  = string.Empty;
        emptyWord = string.Empty;
        wear      = string.Empty;
        if (slotNode == null || addonName != AddonName) return false;

        var param = ReadSlotParam(slotNode);
        if (param < 1 || param > LastSlotParam) return false;
        var containerSlot = (int)(param < FirstParamAfterWaist ? param - 1 : param);

        slotName = Loc.IsRussian ? RussianSlot(containerSlot) : AddonText(data, SlotNameRowBase + (uint)containerSlot);
        if (slotName.Length == 0)
        {
            log.Warning($"[CharSlot] Addon-Zeile {SlotNameRowBase + (uint)containerSlot} fuer Slot {containerSlot} leer.");
            return false;
        }

        // Worn or not comes from the equipped container itself - the icon of an
        // empty slot is a silhouette with no item behind it.
        var inventory = InventoryManager.Instance();
        var equipped  = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null)
        {
            log.Warning("[CharSlot] EquippedItems-Container nicht verfuegbar.");
            return true;
        }

        var item = equipped->GetInventorySlot(containerSlot);
        if (item == null || item->ItemId == 0)
        {
            emptyWord = Loc.IsRussian ? AccessibilityStrings.EmptySlot : AddonText(data, EmptySlotRow);
            if (emptyWord.Length == 0)
            {
                log.Warning($"[CharSlot] Addon-Zeile {EmptySlotRow} leer, nehme Plugin-Wort.");
                emptyWord = AccessibilityStrings.EmptySlot;
            }
            return true;
        }

        wear = ItemWearText.Describe(item, data, log);
        return true;
    }

    /// <summary>The param of the slot's DragDropRollOver event, 0 when it has none.</summary>
    private static uint ReadSlotParam(AtkResNode* slotNode)
    {
        var evt   = slotNode->AtkEventManager.Event;
        var guard = 0;
        while (evt != null && guard++ < 32)
        {
            if (!AtkText.IsReadable(evt)) break;
            if (evt->State.EventType == AtkEventType.DragDropRollOver) return evt->Param;
            evt = evt->NextEvent;
        }
        return 0;
    }

    private static string AddonText(IDataManager data, uint rowId)
        => data.GetExcelSheet<LuminaAddon>().TryGetRow(rowId, out var row)
            ? TolkService.Sanitize(row.Text.ExtractText()).Trim()
            : string.Empty;

    internal static string RussianSlot(int slot) => slot switch
    {
        0 => AccessibilityStrings.EquipMainHand, 1 => AccessibilityStrings.SlotOffHand,
        2 => AccessibilityStrings.SlotHead, 3 => AccessibilityStrings.SlotBody,
        4 => AccessibilityStrings.SlotHands, 5 => AccessibilityStrings.SlotWaist,
        6 => AccessibilityStrings.SlotLegs, 7 => AccessibilityStrings.SlotFeet,
        8 => AccessibilityStrings.SlotEars, 9 => AccessibilityStrings.SlotNeck,
        10 => AccessibilityStrings.SlotWrists, 11 => AccessibilityStrings.EquipRightRing,
        12 => AccessibilityStrings.EquipLeftRing, 13 => AccessibilityStrings.SlotSoulCrystal,
        _ => string.Empty,
    };
}
