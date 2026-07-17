using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Describes equipment for announcements: the required level and whether the
/// player can wear the piece right now. Every fact is read from the game's
/// own data (ilspycmd-verified 2026-07-16): Item.LevelEquip/ClassJobCategory/
/// EquipRestriction from the Excel sheets, the player side from PlayerState
/// (CurrentLevel, CurrentClassJobId, Race, Sex). Nothing is recomputed.
/// The native InventoryManager.CanEquip exists but takes a raw item-row
/// pointer with unknown semantics - guessing pointers risks a crash, so the
/// crash-free sheet checks are used instead.
/// </summary>
public sealed class GearInfoService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    public GearInfoService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log = log;
    }

    /// <summary>
    /// Spoken gear info for an item: "Stufe 15, tragbar" / "Stufe 26, nicht
    /// tragbar, ab Stufe 26" / "Stufe 15, nicht tragbar, nur für Gladiator".
    /// "" when the item is not equipment (no EquipSlotCategory) or unknown.
    /// With briefWhenWearable only "Stufe 15" is returned for wearable gear,
    /// so reading all worn pieces (Strg+F6) does not repeat "tragbar" 12 times.
    /// </summary>
    public string DescribeGear(uint baseItemId, bool briefWhenWearable = false)
    {
        if (baseItemId == 0) return string.Empty;
        if (!_data.GetExcelSheet<LuminaItem>().TryGetRow(baseItemId, out var row)) return string.Empty;
        if (row.EquipSlotCategory.RowId == 0) return string.Empty; // not equipment

        var level = $"Stufe {row.LevelEquip}";
        var (ok, reason) = Wearability(row);
        return ok switch
        {
            true  => briefWhenWearable ? level : $"{level}, tragbar",
            false => $"{level}, nicht tragbar, {reason}",
            _     => level, // player state or sheet column unknown: no claim
        };
    }

    /// <summary>
    /// Gear info for a UI text that IS an equipment name (a shop row lists
    /// only name and price). "" when the text is no known equipment name.
    /// </summary>
    public string DescribeByName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        _gearNames ??= BuildGearNameCache();
        return _gearNames.TryGetValue(text.Trim().ToLowerInvariant(), out var id)
            ? DescribeGear(id)
            : string.Empty;
    }

    // ── Tragbarkeits-Prüfung ──

    /// <summary>
    /// (true, "") wearable; (false, reason) not wearable; (null, "") unknown -
    /// then only the level is announced, never a guessed verdict.
    /// </summary>
    private unsafe (bool? Ok, string Reason) Wearability(LuminaItem row)
    {
        var ps = PlayerState.Instance();
        if (ps == null) return (null, string.Empty);

        if (row.LevelEquip > ps->CurrentLevel)
            return (false, $"ab Stufe {row.LevelEquip}");

        if (row.ClassJobCategory.ValueNullable is { } cat)
        {
            var jobOk = AllowsJob(cat, ps->CurrentClassJobId);
            if (jobOk == null) return (null, string.Empty);
            if (jobOk == false)
            {
                var forWho = cat.Name.ExtractText().Trim();
                return (false, forWho.Length > 0 ? $"nur für {forWho}" : "andere Klasse nötig");
            }
        }

        if (row.EquipRestriction.ValueNullable is { } races && row.EquipRestriction.RowId != 0)
        {
            var raceOk = RaceAllowed(races, ps->Race, ps->Sex);
            if (raceOk == null) return (null, string.Empty);
            if (raceOk == false) return (false, "nicht für dein Volk");
        }

        return (true, string.Empty);
    }

    // One resolved column per job id; null means "column not found - stay silent".
    private readonly Dictionary<byte, PropertyInfo?> _jobColumns = new();

    /// <summary>
    /// Whether a ClassJobCategory row includes the given job; null when the
    /// job column cannot be resolved (then no claim is made). Public because
    /// the skill browser (HotbarService) filters Action rows the same way.
    /// </summary>
    public bool? AllowsJob(ClassJobCategory cat, byte jobId)
    {
        if (!_jobColumns.TryGetValue(jobId, out var prop))
        {
            prop = ResolveJobColumn(jobId);
            _jobColumns[jobId] = prop;
        }
        if (prop == null) return null;

        // try-catch: Reflection into the generated sheet struct.
        try
        {
            return (bool)prop.GetValue(cat)!;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[Gear] ClassJobCategory-Spalte {prop.Name} nicht lesbar");
            return null;
        }
    }

    /// <summary>
    /// The ClassJobCategory sheet has one bool column per job, NAMED after the
    /// English job abbreviation (ADV, GLA, ... PCT - ilspycmd-verified). The
    /// current job's column is found via the English ClassJob sheet instead of
    /// assuming column order; a miss is logged and reported as unknown.
    /// </summary>
    private PropertyInfo? ResolveJobColumn(byte jobId)
    {
        if (!_data.GetExcelSheet<ClassJob>(ClientLanguage.English).TryGetRow(jobId, out var job))
        {
            _log.Warning($"[Gear] Job {jobId} nicht im ClassJob-Sheet.");
            return null;
        }
        var abbr = job.Abbreviation.ExtractText().Trim();
        var prop = typeof(ClassJobCategory).GetProperty(abbr, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || prop.PropertyType != typeof(bool))
        {
            _log.Warning($"[Gear] Keine Job-Spalte '{abbr}' (Job {jobId}) im ClassJobCategory-Sheet.");
            return null;
        }
        _log.Info($"[Gear] Job {jobId} -> Spalte {abbr}.");
        return prop;
    }

    /// <summary>
    /// PlayerState.Race follows the Race sheet rows (1=Hyur .. 8=Viera), the
    /// EquipRaceCategory columns carry the same names in the same order; Sex
    /// 0=male/1=female (CustomizeData convention, in-game verified V4.16).
    /// Unknown values are logged and reported as unknown, never guessed.
    /// </summary>
    private bool? RaceAllowed(EquipRaceCategory r, byte race, byte sex)
    {
        bool? raceOk = race switch
        {
            1 => r.Hyur,
            2 => r.Elezen,
            3 => r.Lalafell,
            4 => r.Miqote,
            5 => r.Roegadyn,
            6 => r.AuRa,
            7 => r.Hrothgar,
            8 => r.Viera,
            _ => null,
        };
        if (raceOk == null)
        {
            _log.Warning($"[Gear] Unbekannte Volks-Id {race}.");
            return null;
        }
        if (!raceOk.Value) return false;

        return sex switch
        {
            0 => r.Male,
            1 => r.Female,
            _ => null,
        };
    }

    // ── Name → Item (für Laden-Zeilen, die nur Name + Preis zeigen) ──

    private Dictionary<string, uint>? _gearNames;

    /// <summary>Lowercased equipment names only - consumables in a list can then
    /// never be mis-matched, and the map stays small. Built once, lazily.</summary>
    private Dictionary<string, uint> BuildGearNameCache()
    {
        var map = new Dictionary<string, uint>();
        foreach (var row in _data.GetExcelSheet<LuminaItem>())
        {
            if (row.EquipSlotCategory.RowId == 0) continue;
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
                map.TryAdd(name.Trim().ToLowerInvariant(), row.RowId);
        }
        _log.Info($"[Gear] Namens-Cache gebaut: {map.Count} Ausrüstungs-Namen.");
        return map;
    }
}
