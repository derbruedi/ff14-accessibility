using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One destination for a timed/seasonal event (e.g. Yo-kai zone).</summary>
/// <param name="EventName">Spoken event name ("Yo-kai").</param>
/// <param name="ZoneName">Localised territory place name.</param>
/// <param name="TerritoryId">Destination TerritoryType RowId.</param>
/// <param name="MapId">Map used for hop routing.</param>
/// <param name="Position">World point to walk to (usually an aetheryte).</param>
/// <param name="Hint">Short spoken hint after the line (may be empty).</param>
public sealed record TimedEventDestination(
    string EventName,
    string ZoneName,
    uint TerritoryId,
    uint MapId,
    Vector3 Position,
    string Hint);

/// <summary>
/// Browser catalogue for <b>timed</b> collaboration events (Yo-kai Watch and
/// similar): zones where the event's ordinary FATEs count, so the player can
/// travel there with Numpad3.
///
/// Yo-kai does NOT set Fate.AdventEvent / MoonFaireEvent / SpecialFate (offline
/// sheet check 2026-09-06). Medals drop from normal FATEs in fixed zone lists
/// published with the event. There is no client sheet that lists those zones.
///
/// WORKAROUND: territory ids below are the official Yo-kai medal zones from the
/// 2026 event guide (same list sighted players use). Shown only while the
/// player owns the Yo-kai Watch (Item 15222 or EventItem 2001948). User asked
/// explicitly for this category (2026-09-06) after sheet-flag FATE spawn areas
/// proved the wrong mental model for Yo-kai.
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

    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly PlacesService _places;
    private readonly InventoryService _inventory;
    private readonly IPluginLog _log;

    // Aetheryte positions per territory: sheet data, built once.
    private Dictionary<uint, (Vector3 Pos, uint MapId)>? _aetheryteByTerritory;

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
    /// no matching event is unlocked for this character (e.g. no Yo-kai Watch).
    /// </summary>
    public List<(TimedEventDestination Dest, bool InCurrentZone)> GetAreasSorted()
    {
        var result = new List<(TimedEventDestination Dest, bool InCurrentZone)>();
        if (!HasYokaiWatch()) return result;

        var terrSheet = _data.GetExcelSheet<TerritoryType>();
        if (terrSheet == null) return result;

        var aetherytes = EnsureAetheryteIndex();
        var currentTerritory = (uint)_clientState.TerritoryType;
        var hopsByMap = _places.GetHopDistances();
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
                // No walkable aetheryte coords — still list the zone name so the
                // player knows where to go; hop routing uses the primary map.
                mapId = terr.Map.RowId;
                pos = Vector3.Zero;
                _log.Info($"[Events] Yo-kai-Zone {tid} '{zone}' ohne Ätheryt-Position.");
            }

            result.Add((
                new TimedEventDestination(eventName, zone, tid, mapId, pos, hint),
                tid == currentTerritory));
        }

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
    /// Debug dump for Strg+F5: Yo-kai access + live Fate sheet flags.
    /// </summary>
    public unsafe void DumpFateEventProbe()
    {
        _log.Info(
            $"[FateEventProbe] === Yo-kaiWatch bag={_inventory.CountOf(YokaiWatchItemId)} " +
            $"key={_inventory.HasKeyItem(YokaiWatchEventItemId)} destinations={Count} " +
            $"terr={_clientState.TerritoryType} ===");

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
