using System;
using System.Collections.Generic;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Equipment access for blind players: reads the currently worn gear aloud
/// and equips the game's own "recommended gear" selection - the same
/// optimizer a sighted player reaches via the button in the character
/// window. Gear data comes from Dalamud's IGameInventory (EquippedItems
/// container, no UI scraping); the optimizer is the game's
/// RecommendEquipModule (ilspycmd-verified 2026-07-16:
/// UIModule.GetRecommendEquipModule, SetupForClassJob(classJobId) starts an
/// async update flagged by IsUpdating, EquipRecommendedGear() applies it).
/// </summary>
public sealed class EquipmentService
{
    private readonly IGameInventory _inventory;
    private readonly IDataManager _data;
    private readonly GearInfoService _gearInfo;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public EquipmentService(IGameInventory inventory, IDataManager data, GearInfoService gearInfo, TolkService tolk, IPluginLog log)
    {
        _inventory = inventory;
        _data = data;
        _gearInfo = gearInfo;
        _tolk = tolk;
        _log = log;
    }

    // ── Ausrüstung vorlesen ──

    /// <summary>
    /// Announces every worn item with its slot: "Waffe: Bronzegladius,
    /// Kopf: leer, ...". Slot labels derive from the item's own
    /// EquipSlotCategory sheet row (game data), not from slot-index guesses.
    /// </summary>
    public void ReadEquipment()
    {
        var parts = new List<string>();
        var empty = 0;
        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.EquippedItems))
        {
            if (item.IsEmpty || item.ItemId == 0)
            {
                empty++;
                continue;
            }

            var label = SlotLabel(item.BaseItemId);
            var name = ResolveItemName(item.BaseItemId);
            var hq = item.IsHq ? " Hoch-Qualität" : string.Empty;
            // Level per piece; "tragbar" is omitted for wearable gear (brief) -
            // worn pieces are normally wearable, only a mismatch (e.g. after a
            // class change) is worth words: "nicht tragbar, nur für ...".
            var gear = _gearInfo.DescribeGear(item.BaseItemId, briefWhenWearable: true);
            var gearNote = gear.Length > 0 ? $", {gear}" : string.Empty;
            _log.Info($"[Equip] slot={item.InventorySlot} id={item.ItemId} '{label}: {name}'{hq}{gearNote}");
            parts.Add($"{label}: {name}{hq}{gearNote}");
        }

        if (parts.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Ausrüstung angelegt.");
            return;
        }
        var emptyNote = empty > 0 ? $" {empty} Plätze frei." : string.Empty;
        _tolk.SpeakInterrupt($"Ausrüstung: {string.Join(". ", parts)}.{emptyNote}");
    }

    /// <summary>
    /// German slot label from the item's EquipSlotCategory row: the column
    /// that is set (&gt;0) names the slot (sheet layout ilspycmd-verified).
    /// The labels themselves are our UI wording, like other spoken labels.
    /// </summary>
    private string SlotLabel(uint baseItemId)
    {
        if (!_data.GetExcelSheet<LuminaItem>().TryGetRow(baseItemId, out var row))
            return "Ausrüstung";
        var slot = row.EquipSlotCategory.ValueNullable;
        if (slot == null) return "Ausrüstung";
        var s = slot.Value;
        if (s.MainHand    > 0) return "Waffe";
        if (s.OffHand     > 0) return "Nebenhand";
        if (s.Head        > 0) return "Kopf";
        if (s.Body        > 0) return "Rumpf";
        if (s.Gloves      > 0) return "Hände";
        if (s.Waist       > 0) return "Gürtel";
        if (s.Legs        > 0) return "Beine";
        if (s.Feet        > 0) return "Füße";
        if (s.Ears        > 0) return "Ohren";
        if (s.Neck        > 0) return "Hals";
        if (s.Wrists      > 0) return "Handgelenke";
        if (s.FingerL     > 0 || s.FingerR > 0) return "Ring";
        if (s.SoulCrystal > 0) return "Jobkristall";
        return "Ausrüstung";
    }

    private string ResolveItemName(uint baseItemId)
    {
        if (_data.GetExcelSheet<LuminaItem>().TryGetRow(baseItemId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return $"Gegenstand {baseItemId}";
    }

    // ── Empfohlene Ausrüstung anlegen (das Spiel wählt, wir lösen nur aus) ──

    private enum EquipPhase
    {
        Idle,
        WaitingForSetup,   // RecommendEquipModule computes its selection (IsUpdating)
        WaitingForResult,  // gear swap applied; wait, then compare and announce
    }

    private EquipPhase _phase = EquipPhase.Idle;
    private DateTime _phaseStartedAt;
    private Dictionary<int, uint> _equippedBefore = new();

    /// <summary>
    /// Triggers the game's own "recommended gear" flow for the current class:
    /// SetupForClassJob starts the async selection, Update() applies it once
    /// the module finishes and then announces how many pieces changed - the
    /// honest feedback a blind player needs (a combat lockout or an already
    /// optimal set both end in "unverändert").
    /// </summary>
    public unsafe void EquipRecommended()
    {
        if (_phase != EquipPhase.Idle)
        {
            _tolk.SpeakInterrupt("Ausrüstungswechsel läuft schon.");
            return;
        }

        // try-catch: native game module via FFXIVClientStructs (signatures can
        // break on game patches; a failed equip must never crash the plugin).
        try
        {
            var uiModule = UIModule.Instance();
            var playerState = PlayerState.Instance();
            if (uiModule == null || playerState == null)
            {
                _tolk.SpeakInterrupt("Ausrüstungsmodul nicht verfügbar.");
                return;
            }

            var job = playerState->CurrentClassJobId;
            var module = uiModule->GetRecommendEquipModule();
            if (module == null)
            {
                _tolk.SpeakInterrupt("Ausrüstungsmodul nicht verfügbar.");
                return;
            }

            _equippedBefore = SnapshotEquipped();
            module->SetupForClassJob(job);
            _phase = EquipPhase.WaitingForSetup;
            _phaseStartedAt = DateTime.UtcNow;
            _log.Info($"[Equip] Empfohlene Ausrüstung: Setup gestartet (Job {job}).");
            _tolk.SpeakInterrupt("Lege empfohlene Ausrüstung an.");
        }
        catch (Exception ex)
        {
            _phase = EquipPhase.Idle;
            _log.Error(ex, "[Equip] RecommendEquipModule fehlgeschlagen");
            _tolk.SpeakInterrupt("Ausrüstungswechsel fehlgeschlagen.");
        }
    }

    /// <summary>Drives the equip flow. Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public unsafe void Update()
    {
        if (_phase == EquipPhase.Idle) return;

        var elapsed = (DateTime.UtcNow - _phaseStartedAt).TotalSeconds;

        // try-catch: native game module, see EquipRecommended.
        try
        {
            if (_phase == EquipPhase.WaitingForSetup)
            {
                var module = UIModule.Instance()->GetRecommendEquipModule();
                if (module->IsUpdating && elapsed < 3)
                    return;
                if (module->IsUpdating)
                {
                    // 3 s and still computing: give up rather than equip stale data.
                    _phase = EquipPhase.Idle;
                    _log.Info("[Equip] Setup nach 3 s nicht fertig, abgebrochen.");
                    _tolk.SpeakInterrupt("Ausrüstungswechsel hat nicht geklappt.");
                    return;
                }
                module->EquipRecommendedGear();
                _phase = EquipPhase.WaitingForResult;
                _phaseStartedAt = DateTime.UtcNow;
                _log.Info("[Equip] EquipRecommendedGear ausgelöst.");
                return;
            }

            // WaitingForResult: give the swap a moment, then report the diff.
            if (elapsed < 0.8) return;
            _phase = EquipPhase.Idle;

            var after = SnapshotEquipped();
            var changed = CountChangedSlots(_equippedBefore, after);
            _log.Info($"[Equip] Ergebnis: {changed} Plätze geändert.");
            _tolk.SpeakInterrupt(changed > 0
                ? $"Empfohlene Ausrüstung angelegt, {changed} Teile gewechselt."
                : "Ausrüstung unverändert. Entweder schon optimal, oder Wechsel gerade nicht möglich.");
        }
        catch (Exception ex)
        {
            _phase = EquipPhase.Idle;
            _log.Error(ex, "[Equip] Update des Ausrüstungswechsels fehlgeschlagen");
        }
    }

    /// <summary>Worn item ids by slot, for the before/after comparison.</summary>
    private Dictionary<int, uint> SnapshotEquipped()
    {
        var map = new Dictionary<int, uint>();
        foreach (var item in _inventory.GetInventoryItems(GameInventoryType.EquippedItems))
        {
            if (item.IsEmpty || item.ItemId == 0) continue;
            map[(int)item.InventorySlot] = item.ItemId;
        }
        return map;
    }

    private static int CountChangedSlots(Dictionary<int, uint> before, Dictionary<int, uint> after)
    {
        var changed = 0;
        foreach (var (slot, id) in after)
            if (!before.TryGetValue(slot, out var old) || old != id)
                changed++;
        foreach (var slot in before.Keys)
            if (!after.ContainsKey(slot))
                changed++;
        return changed;
    }
}
