using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using LuminaAddon = Lumina.Excel.Sheets.Addon;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Condition and spiritbond of ONE item instance, in the game's words and the
/// client's language: "Zustand 53 %, Bindung 12 %".
///
/// Read from the instance rather than from the item tooltip, because the
/// tooltip text trails the cursor: log 2026-10-03 13:54:15 announced the body
/// piece with the 53 % of the gloves focused just before it (the same piece had
/// said 62 % six seconds earlier), and where a parent group hides the lines (soul
/// crystal, consumables) they stay "visible" with the previous item's numbers
/// (probes 2026-10-04 17:19 and 17:38). Callers therefore hand over an instance
/// they know to be the right one - the worn slot (CharacterEquipSlot) or the one
/// the game itself resolved on hover (ItemSlotService.TryGetHoveredInstance).
///
/// WHICH PIECES: condition only where the Item sheet names a repair class
/// (ClassJobRepair), spiritbond only where it names a materia extraction type
/// (MaterializeType) - NOT the materia slot count: the neck, wrist and both
/// rings of the 2026-10-04 test have no slots and the tooltip still showed their
/// bond. Offline 2026-10-04 the only equipment with neither flag is the 31 soul
/// crystals, which show neither line. Unmeasured: 33 Novus/Zodiac relic weapons
/// carry a repair class but no extraction type, so they get a condition and no
/// bond - nobody here owns one to check the tooltip.
///
/// LABELS: Addon rows 498 / 499, an adjacent pair ("Zustand" / "Bindung",
/// "Condition" / "Spiritbond", 耐久度 / 錬精度; offline dump 2026-10-04).
/// </summary>
internal static unsafe class ItemWearText
{
    private const uint ConditionLabelRow  = 498;
    private const uint SpiritbondLabelRow = 499;

    /// <summary>"Zustand 53 %, Bindung 12 %" for the instance, "" when the item
    /// has neither (soul crystals, everything that is not equipment).</summary>
    public static string Describe(InventoryItem* item, IDataManager data, IPluginLog log)
    {
        if (item == null || item->ItemId == 0) return string.Empty;
        // GetItemId carries the HQ flag. The sheet row is the base id.
        var itemId = ItemSlotService.GetBaseItemId(item->GetItemId());
        if (!data.GetExcelSheet<LuminaItem>().TryGetRow(itemId, out var row)) return string.Empty;

        var parts = new List<string>();
        if (row.ClassJobRepair.RowId != 0)
        {
            // The game's own percentage (100 * Condition / 30000, ClientStructs).
            parts.Add($"{AddonText(data, ConditionLabelRow)} {item->GetConditionPercentage()} %");
        }
        if (row.MaterializeType != 0)
            parts.Add($"{AddonText(data, SpiritbondLabelRow)} {SpiritbondPercent(item->GetSpiritbondOrCollectability())} %");

        log.Info($"[Wear] item={itemId} roh: Zustand={item->Condition} Bindung={item->SpiritbondOrCollectability} -> '{string.Join(", ", parts)}'");
        return string.Join(", ", parts);
    }

    /// <summary>
    /// The spiritbond percentage as the item tooltip shows it. The raw value runs
    /// 0..10000 for 0..100 %, and the tooltip cuts the decimals: measured
    /// 2026-10-04 against the tooltip of the worn pieces (9234 -> 92, 3787 -> 37,
    /// 3619 -> 36, 848 -> 8, 828 -> 8, 10000 -> 100). The one exception is a bond
    /// that has started but not reached 1 %: raw 4 showed "1 %", not "0 %".
    /// </summary>
    private static int SpiritbondPercent(ushort raw)
    {
        // WORKAROUND: the clean path is the tooltip's own figure
        // (AddonItemDetail.SpiritbondValue), but that text trails the cursor and
        // keeps the previous item's numbers where its group is hidden (soul
        // crystal, probe 2026-10-04 17:19). So the tooltip's rounding is mirrored
        // instead. The "below 1 % shows 1 %" rule rests on a single measurement
        // (Yo-kai Watch, raw 4); approved by the user 2026-10-04 for parity with
        // what a sighted player reads.
        var percent = raw / 100;
        return raw > 0 && percent == 0 ? 1 : percent;
    }

    private static string AddonText(IDataManager data, uint rowId)
        => Loc.IsRussian ? rowId == ConditionLabelRow ? AccessibilityStrings.WearConditionLabel : AccessibilityStrings.SpiritbondLabel
        : data.GetExcelSheet<LuminaAddon>().TryGetRow(rowId, out var row)
            ? TolkService.Sanitize(row.Text.ExtractText()).Trim()
            : string.Empty;
}
