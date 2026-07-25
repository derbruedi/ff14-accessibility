using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One fishing hole of the current zone, from the static FishingSpot sheet.</summary>
/// <param name="Name">Localised place name ("Fallgourd Float").</param>
/// <param name="Level">Required fishing level (GatheringLevel).</param>
/// <param name="Position">World X/Z; Y is unknown (map data is 2D) and 0 -
/// resolve via navmesh before walking.</param>
public sealed record FishingSpotInfo(string Name, int Level, Vector3 Position);

/// <summary>
/// Fishing accessibility. A blind player cannot see where the water is, so the
/// first job is answering "where can I fish?". The game's own FishingSpot sheet
/// is the authoritative, complete catalogue: every rod-fishing hole with its
/// zone, name, required level and map-PIXEL coordinates (0..2048). We read that
/// and convert to world positions with the verified PlacesService pixel formula,
/// then the existing walk guide / navmesh can reach them.
///
/// Verified via ilspycmd + real data read from the game's sqpack (2026-07-25):
/// FishingSpot.TerritoryType@52 (zone), PlaceName@60, X@64/Z@66 (map pixels,
/// all rows fall in 108..1948), Radius@58, GatheringLevel@68. 333 catalogued
/// spots total.
///
/// STILL TO CONFIRM LIVE (one walk): that the converted world position lands on
/// the fishing hole - the last-mile check the compass announcement doubles as.
/// The bite announcement (FishingState) is a separate, later step.
/// </summary>
public sealed class FishingService
{
    private readonly IObjectTable  _objectTable;
    private readonly IClientState  _clientState;
    private readonly IDataManager  _data;
    private readonly PlacesService _places;
    private readonly TolkService   _tolk;
    private readonly IPluginLog    _log;

    public FishingService(
        IObjectTable objectTable,
        IClientState clientState,
        IDataManager data,
        PlacesService places,
        TolkService tolk,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _data        = data;
        _places      = places;
        _tolk        = tolk;
        _log         = log;
    }

    /// <summary>
    /// All fishing spots of the CURRENT zone, sorted nearest-first from the
    /// player. World positions come from the map-pixel conversion; Y is 0.
    /// Empty when the zone has no fishing spots or no player is loaded.
    /// </summary>
    public List<FishingSpotInfo> GetSpotsInCurrentZone()
    {
        var result = new List<FishingSpotInfo>();
        var player = _objectTable.LocalPlayer;
        if (player == null) return result;

        var territory = (ushort)_clientState.TerritoryType;
        var sheet = _data.GetExcelSheet<FishingSpot>();
        if (sheet == null) return result;

        foreach (var row in sheet.Where(r => r.TerritoryType.RowId == territory))
        {
            if (row.X == 0 && row.Z == 0) continue;
            var name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var world = _places.MapPixelToWorld(row.X, row.Z);
            if (world is not { } pos) continue;

            result.Add(new FishingSpotInfo(name, row.GatheringLevel, pos));
        }

        var playerPos = player.Position;
        return result
            .OrderBy(s => PlacesService.Distance2D(playerPos, s.Position))
            .ToList();
    }

    /// <summary>
    /// Speaks the fishing spots of the current zone, nearest first, each with
    /// required level, distance and compass direction - so a blind fisher knows
    /// where they can fish and which way to head. Also logs every spot.
    /// </summary>
    public void AnnounceSpotsInCurrentZone()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var spots = GetSpotsInCurrentZone();
        if (spots.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Angelplätze in dieser Zone.");
            _log.Info($"[Fish] Gebiet {_clientState.TerritoryType}: keine FishingSpot-Einträge.");
            return;
        }

        var playerPos = player.Position;
        var lines = new List<string>();
        _log.Info($"[Fish] Gebiet {_clientState.TerritoryType}: {spots.Count} Angelplätze");
        foreach (var s in spots)
        {
            var dist    = PlacesService.Distance2D(playerPos, s.Position);
            var compass = CompassDirection(playerPos, s.Position);
            lines.Add($"{s.Name}, Stufe {s.Level}, {dist:F0} Meter {compass}");
            _log.Info($"[Fish]   '{s.Name}' Stufe={s.Level} Welt=({s.Position.X:F1}|{s.Position.Z:F1}) " +
                      $"Dist={dist:F0} m {compass}");
        }

        _tolk.SpeakInterrupt($"{spots.Count} Angelplätze: " + string.Join(". ", lines) + ".");
    }

    /// <summary>
    /// Diagnostic: logs every object within 50 m with its kind, name, data id
    /// and distance. Purpose: find out whether the physical fishing holes (the
    /// ripples you cast at, especially the several piers of a city harbour that
    /// the single FishingSpot sheet row cannot distinguish) show up as game
    /// objects - if they do, they can be browsed like NPCs. Read-only.
    /// Run it standing ON a pier next to a real fishing hole.
    /// </summary>
    public void ProbeNearbyObjects()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var playerPos = player.Position;
        var hits = _objectTable
            .Where(o => o != null)
            .Select(o => (Obj: o, Dist: PlacesService.Distance2D(playerPos, o.Position)))
            .Where(x => x.Dist <= 50f)
            .OrderBy(x => x.Dist)
            .ToList();

        _log.Info($"[FishObj] === {hits.Count} Objekte in 50 m, Spieler=({playerPos.X:F1}|{playerPos.Z:F1}) " +
                  $"Gebiet={_clientState.TerritoryType} ===");
        foreach (var (obj, dist) in hits)
        {
            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = "(kein Name)";
            _log.Info($"[FishObj]   Art={obj.ObjectKind} DataId={obj.BaseId} Dist={dist:F1} m " +
                      $"Name='{name}' Welt=({obj.Position.X:F1}|{obj.Position.Z:F1})");
        }

        _tolk.SpeakInterrupt($"{hits.Count} Objekte in 50 Metern im Log.");
    }

    /// <summary>
    /// Eight-point compass bearing from <paramref name="from"/> to
    /// <paramref name="to"/>. Convention (verified, game-api.md): north = -Z,
    /// east = +X, bearing = atan2(dx, -dz) with 0 deg = north, 90 deg = east.
    /// </summary>
    private static string CompassDirection(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        var deg = MathF.Atan2(dx, -dz) * 180f / MathF.PI;
        if (deg < 0) deg += 360f;

        string[] names =
        {
            "nördlich", "nordöstlich", "östlich", "südöstlich",
            "südlich", "südwestlich", "westlich", "nordwestlich",
        };
        var index = (int)MathF.Round(deg / 45f) % 8;
        return names[index];
    }
}
