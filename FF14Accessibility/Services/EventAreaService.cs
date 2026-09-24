using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One destination for a timed/seasonal event (e.g. Yo-kai zone or collab FATE).</summary>
/// <param name="EventName">Spoken event or FATE name ("Yo-kai", "Die mechanischen Krieger").</param>
/// <param name="ZoneName">Localised territory place name.</param>
/// <param name="TerritoryId">Destination TerritoryType RowId.</param>
/// <param name="MapId">Map used for hop routing.</param>
/// <param name="Position">World point to walk to (aetheryte or FATE EventRange).</param>
/// <param name="Hint">Short spoken hint after the line (may be empty).</param>
public sealed record TimedEventDestination(
    string EventName,
    string ZoneName,
    uint TerritoryId,
    uint MapId,
    Vector3 Position,
    string Hint);

/// <summary>
/// Browser catalogue for <b>timed</b> collaboration events: Yo-kai medal zones
/// and collab Event-FATE spawn points, so the player can travel with Numpad3.
///
/// Yo-kai does NOT set Fate.AdventEvent / MoonFaireEvent / SpecialFate (offline
/// sheet check 2026-09-06). Medals drop from normal FATEs in fixed zone lists.
/// WORKAROUND: territory ids below from the official Yo-kai guide; shown only
/// with the Yo-kai Watch (Item 15222 or EventItem 2001948).
///
/// Collab Event-FATEs (e.g. FFXV "Like Clockwork") also lack those flags
/// (offline 2026-09-24: Fate 1409 A=M=S=false). AdventEvent/MoonFaire/SpecialFate
/// are a different set (Moon Faire rice-pounding etc.), not Nocturne.
/// WORKAROUND: Fate RowIds in <see cref="CollabEventFateIds"/>; name and spawn
/// position come from the Fate sheet + planevent.lgb EventRange (Location field).
/// </summary>
public sealed class EventAreaService
{
    // Item sheet: equippable Yo-kai Watch. EventItem sheet: key-item copy.
    private const uint YokaiWatchItemId = 15222;
    private const uint YokaiWatchEventItemId = 2001948;

    // WORKAROUND: no TerritoryType join exists for Yo-kai eligible zones.
    // Source: official 2026 Yo-kai Watch event (standard + legendary medal zones).
    private static readonly uint[] YokaiTerritoryIds =
    {
        // La Noscea
        134, 135, 138, 139, 180,
        // Black Shroud
        148, 152, 153, 154,
        // Thanalan
        140, 141, 145, 146,
        // Heavensward (legendary medals)
        397, 398, 399, 400, 401, 402,
        // Stormblood (legendary medals)
        612, 613, 614, 620, 621, 622,
    };

    // WORKAROUND: collab Event-FATEs are not marked AdventEvent/MoonFaire/SpecialFate.
    // Fate 1409 = EN "Like Clockwork" / DE "Die mechanischen Krieger" (A Nocturne for
    // Heroes / FFXV). Location→planevent EventRange = terr 141 (verified 2026-09-24).
    private static readonly uint[] CollabEventFateIds =
    {
        1409,
    };

    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly PlacesService _places;
    private readonly InventoryService _inventory;
    private readonly IPluginLog _log;

    // Aetheryte positions per territory: sheet data, built once.
    private Dictionary<uint, (Vector3 Pos, uint MapId)>? _aetheryteByTerritory;

    // EventRange InstanceId → territory + world position for collab Fate.Location ids.
    private Dictionary<uint, (uint TerritoryId, Vector3 Pos)>? _eventRangeByInstanceId;

    // Resolved collab FATE destinations, built once from sheet + EventRange lookup.
    private List<TimedEventDestination>? _collabFateDestinations;

    public EventAreaService(
        IClientState clientState,
        IDataManager data,
        PlacesService places,
        InventoryService inventory,
        IPluginLog log)
    {
        _clientState = clientState;
        _data = data;
        _places = places;
        _inventory = inventory;
        _log = log;
    }

    /// <summary>
    /// Timed-event destinations, current zone first then fewest hops. Empty when
    /// neither Yo-kai (no Watch) nor any collab Event-FATE entry is available.
    /// </summary>
    public List<(TimedEventDestination Dest, bool InCurrentZone)> GetAreasSorted()
    {
        var result = new List<(TimedEventDestination Dest, bool InCurrentZone)>();
        var currentTerritory = (uint)_clientState.TerritoryType;
        var hopsByMap = _places.GetHopDistances();

        if (HasYokaiWatch())
            AddYokaiDestinations(result, currentTerritory);

        foreach (var dest in EnsureCollabFateDestinations())
            result.Add((dest, dest.TerritoryId == currentTerritory));

        int HopsTo(uint mapId, bool here)
        {
            if (here) return 0;
            if (mapId != 0 && hopsByMap.TryGetValue(mapId, out var hops)) return hops;
            return int.MaxValue / 4;
        }

        return result
            .OrderByDescending(x => x.InCurrentZone)
            .ThenBy(x => HopsTo(x.Dest.MapId, x.InCurrentZone))
            .ThenBy(x => x.Dest.ZoneName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>How many destinations the timed-event list currently holds.</summary>
    public int Count => GetAreasSorted().Count;

    /// <summary>True when the Yo-kai Watch is in bags/equipped or as key item.</summary>
    public bool HasYokaiWatch()
    {
        var bag = _inventory.CountOf(YokaiWatchItemId);
        if (bag > 0) return true;
        return _inventory.HasKeyItem(YokaiWatchEventItemId);
    }

    /// <summary>
    /// Debug dump for Strg+F5: Yo-kai access, collab FATE rows, live Fate flags.
    /// </summary>
    public unsafe void DumpFateEventProbe()
    {
        var collab = EnsureCollabFateDestinations();
        _log.Info(
            $"[FateEventProbe] === Yo-kaiWatch bag={_inventory.CountOf(YokaiWatchItemId)} " +
            $"key={_inventory.HasKeyItem(YokaiWatchEventItemId)} destinations={Count} " +
            $"collabFates={collab.Count} terr={_clientState.TerritoryType} ===");

        foreach (var d in collab)
        {
            _log.Info(
                $"[FateEventProbe] Collab '{d.EventName}' terr={d.TerritoryId} '{d.ZoneName}' " +
                $"pos=({d.Position.X:F1}|{d.Position.Y:F1}|{d.Position.Z:F1})");
        }

        var fateSheet = _data.GetExcelSheet<Fate>();
        if (fateSheet == null) return;

        var mgr = FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager.Instance();
        if (mgr == null)
        {
            _log.Info("[FateEventProbe] FateManager.Instance() ist null.");
            return;
        }

        for (var i = 0; i < mgr->Fates.Count; i++)
        {
            var fate = mgr->Fates[i].Value;
            if (fate == null) continue;
            var id = fate->FateId;
            var flags = "(kein Sheet)";
            if (fateSheet.TryGetRow(id, out var row))
                flags = $"A={row.AdventEvent} M={row.MoonFaireEvent} S={row.SpecialFate}";
            _log.Info(
                $"[FateEventProbe] Live id={id} '{fate->Name}' {flags} " +
                $"welt=({fate->Location.X:F1}|{fate->Location.Y:F1}|{fate->Location.Z:F1})");
        }
    }

    private void AddYokaiDestinations(
        List<(TimedEventDestination Dest, bool InCurrentZone)> result,
        uint currentTerritory)
    {
        var terrSheet = _data.GetExcelSheet<TerritoryType>();
        if (terrSheet == null) return;

        var aetherytes = EnsureAetheryteIndex();
        var eventName = AccessibilityStrings.TimedEventYokaiName;
        var hint = AccessibilityStrings.TimedEventYokaiHint;

        foreach (var tid in YokaiTerritoryIds)
        {
            if (!terrSheet.TryGetRow(tid, out var terr)) continue;
            var zone = terr.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(zone)) continue;

            Vector3 pos;
            uint mapId;
            if (aetherytes.TryGetValue(tid, out var aeth))
            {
                pos = aeth.Pos;
                mapId = aeth.MapId != 0 ? aeth.MapId : terr.Map.RowId;
            }
            else
            {
                mapId = terr.Map.RowId;
                pos = Vector3.Zero;
                _log.Info($"[Events] Yo-kai-Zone {tid} '{zone}' ohne Ätheryt-Position.");
            }

            result.Add((
                new TimedEventDestination(eventName, zone, tid, mapId, pos, hint),
                tid == currentTerritory));
        }
    }

    private List<TimedEventDestination> EnsureCollabFateDestinations()
    {
        if (_collabFateDestinations != null) return _collabFateDestinations;
        _collabFateDestinations = BuildCollabFateDestinations();
        return _collabFateDestinations;
    }

    private List<TimedEventDestination> BuildCollabFateDestinations()
    {
        var result = new List<TimedEventDestination>();
        var fateSheet = _data.GetExcelSheet<Fate>();
        var terrSheet = _data.GetExcelSheet<TerritoryType>();
        if (fateSheet == null || terrSheet == null) return result;

        var ranges = EnsureEventRangesForCollabFates(fateSheet);
        var hint = AccessibilityStrings.TimedEventFateHint;

        foreach (var fateId in CollabEventFateIds)
        {
            if (!fateSheet.TryGetRow(fateId, out var fate))
            {
                _log.Info($"[Events] Collab-FATE id={fateId} fehlt im Sheet.");
                continue;
            }

            var name = fate.Name.ExtractText().Trim();
            if (string.IsNullOrEmpty(name))
            {
                _log.Info($"[Events] Collab-FATE id={fateId} hat keinen Namen.");
                continue;
            }

            if (!ranges.TryGetValue(fate.Location, out var range))
            {
                _log.Info(
                    $"[Events] Collab-FATE id={fateId} '{name}' Location={fate.Location} " +
                    "ohne EventRange in planevent.lgb.");
                continue;
            }

            if (!terrSheet.TryGetRow(range.TerritoryId, out var terr))
            {
                _log.Info(
                    $"[Events] Collab-FATE id={fateId} Territory {range.TerritoryId} fehlt.");
                continue;
            }

            var zone = terr.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(zone)) continue;

            var mapId = terr.Map.RowId;
            result.Add(new TimedEventDestination(
                name, zone, range.TerritoryId, mapId, range.Pos, hint));
            _log.Info(
                $"[Events] Collab-FATE id={fateId} '{name}' → terr={range.TerritoryId} '{zone}' " +
                $"pos=({range.Pos.X:F1}|{range.Pos.Y:F1}|{range.Pos.Z:F1}).");
        }

        return result;
    }

    /// <summary>
    /// Resolves Fate.Location EventRange ids for the collab allowlist only —
    /// scans planevent.lgb until every needed InstanceId is found.
    /// </summary>
    private Dictionary<uint, (uint TerritoryId, Vector3 Pos)> EnsureEventRangesForCollabFates(
        Lumina.Excel.ExcelSheet<Fate> fateSheet)
    {
        if (_eventRangeByInstanceId != null) return _eventRangeByInstanceId;

        var needed = new HashSet<uint>();
        foreach (var fateId in CollabEventFateIds)
        {
            if (!fateSheet.TryGetRow(fateId, out var fate)) continue;
            if (fate.Location != 0) needed.Add(fate.Location);
        }

        _eventRangeByInstanceId = needed.Count == 0
            ? new Dictionary<uint, (uint, Vector3)>()
            : BuildEventRangeIndex(needed);
        return _eventRangeByInstanceId;
    }

    /// <summary>
    /// Finds the given EventRange InstanceIds in planevent.lgb. Stops when all
    /// needed ids are resolved. Fate.Location is that InstanceId (game-api.md).
    /// </summary>
    private Dictionary<uint, (uint TerritoryId, Vector3 Pos)> BuildEventRangeIndex(
        HashSet<uint> needed)
    {
        var result = new Dictionary<uint, (uint TerritoryId, Vector3 Pos)>();
        var terrSheet = _data.GetExcelSheet<TerritoryType>();
        if (terrSheet == null || needed.Count == 0) return result;

        var territories = 0;
        foreach (var terr in terrSheet)
        {
            if (result.Count >= needed.Count) break;

            var bg = terr.Bg.ExtractText();
            if (string.IsNullOrEmpty(bg) || !bg.Contains("/level/", StringComparison.Ordinal))
                continue;

            var cut = bg.LastIndexOf("/level/", StringComparison.Ordinal);
            var path = "bg/" + bg[..(cut + 7)] + "planevent.lgb";

            LgbFile? lgb;
            try
            {
                lgb = _data.GetFile<LgbFile>(path);
            }
            catch (Exception ex)
            {
                // External layout file: missing/renamed on patch must not break Events.
                _log.Warning($"[Events] {path} nicht lesbar: {ex.Message}");
                continue;
            }
            if (lgb == null) continue;

            territories++;
            foreach (var layer in lgb.Layers)
            {
                foreach (var instance in layer.InstanceObjects)
                {
                    if (instance.AssetType != LayerEntryType.EventRange) continue;
                    var id = instance.InstanceId;
                    if (id == 0 || !needed.Contains(id) || result.ContainsKey(id)) continue;
                    var t = instance.Transform;
                    result[id] = (
                        terr.RowId,
                        new Vector3(t.Translation.X, t.Translation.Y, t.Translation.Z));
                }
            }
        }

        _log.Info(
            $"[Events] EventRange-Lookup: {result.Count}/{needed.Count} Treffer " +
            $"nach {territories} Zonen mit planevent.lgb.");
        return result;
    }

    private Dictionary<uint, (Vector3 Pos, uint MapId)> EnsureAetheryteIndex()
    {
        if (_aetheryteByTerritory != null) return _aetheryteByTerritory;
        _aetheryteByTerritory = BuildAetheryteIndex();
        return _aetheryteByTerritory;
    }

    private Dictionary<uint, (Vector3 Pos, uint MapId)> BuildAetheryteIndex()
    {
        var result = new Dictionary<uint, (Vector3 Pos, uint MapId)>();
        var sheet = _data.GetExcelSheet<Aetheryte>();
        if (sheet == null) return result;

        foreach (var a in sheet)
        {
            if (!a.IsAetheryte || a.Invisible) continue;
            var tid = a.Territory.RowId;
            if (tid == 0 || result.ContainsKey(tid)) continue;

            var levelRef = a.Level[0];
            if (levelRef.RowId == 0 || levelRef.ValueNullable is not { } level) continue;

            result[tid] = (new Vector3(level.X, level.Y, level.Z), a.Map.RowId);
        }

        _log.Info($"[Events] Ätheryt-Index: {result.Count} Zonen mit Position.");
        return result;
    }
}
