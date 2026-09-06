using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One gathering cluster or live node.</summary>
/// <param name="TypeName">GatheringType name ("Minenarbeiter (Herausbrechen)").</param>
/// <param name="Level">Required gathering level.</param>
/// <param name="Position">World position. Live nodes use the ObjectTable
/// position (real height); catalogue entries use ExportedGatheringPoint X/Z
/// with Y=0 until navmesh resolves height.</param>
/// <param name="GatheringPointBaseId">GatheringPointBase sheet RowId (also the
/// ExportedGatheringPoint key for catalogue rows).</param>
/// <param name="GatheringTypeId">GatheringType row id (0/1 miner, 2/3 botanist).</param>
/// <param name="CurrentlyUp">True when this entry is a live, targetable
/// GatheringPoint in the ObjectTable right now (same "can work this" signal
/// measured 2026-08-09 via IsTargetable).</param>
public sealed record GatherSpotInfo(
    string TypeName,
    int Level,
    Vector3 Position,
    uint GatheringPointBaseId,
    uint GatheringTypeId,
    bool CurrentlyUp = false);

/// <summary>
/// Gathering (Miner ore / Botanist wood) accessibility. A blind gatherer cannot
/// see where the nodes are, so - like <see cref="FishingService"/> for fishing
/// holes - the job is answering "where can I gather?" and walking to the spot,
/// including across map transitions.
///
/// Two layers (user request 2026-09-06):
/// 1. LIVE nodes in the current zone from the ObjectTable
///    (<see cref="Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint"/>
///    with <c>IsTargetable</c>) - re-read on every list build so a fresh spawn
///    appears the next time the category is opened or stepped. These sort first.
///    Sighted players with Truth of Mountains/Forests see related icons on the
///    map; our source is the game objects themselves, not the map UI.
/// 2. CATALOGUE from <see cref="GatheringPoint"/> + <see cref="ExportedGatheringPoint"/>
///    sheets for the current zone and neighbours (up to <see cref="MaxZoneHops"/>),
///    so empty ground still has a destination. Cached per territory; skipped when
///    a live node already covers that GatheringPointBase.
///
/// Job filter: Miner types 0+1, Botanist types 2+3 (GatheringType sheet names
/// verified 2026-07-27). Non-gatherers get an empty cross-zone list so the
/// category disappears from browsing entirely.
/// </summary>
public sealed class GatheringService
{
    private readonly IObjectTable  _objectTable;
    private readonly IClientState  _clientState;
    private readonly IDataManager  _data;
    private readonly PlacesService _places;
    private readonly TolkService   _tolk;
    private readonly IPluginLog    _log;

    // ClassJob row ids (Lumina ClassJob sheet): the two gathering classes.
    public const uint JobMiner    = 16;
    public const uint JobBotanist = 17;

    // GatheringType row ids: Miner does 0 (Mining) + 1 (Quarrying), Botanist does
    // 2 (Logging) + 3 (Harvesting). Verified by name in the probe log 2026-07-27
    // (type 1 = "Minenarbeiter (Herausbrechen)", 2/3 = "Gärtner (Abholzen/Abernten)").
    private static readonly uint[] MinerTypes    = { 0, 1 };
    private static readonly uint[] BotanistTypes = { 2, 3 };

    /// <summary>How many map transitions away a zone may be and still show up
    /// in <see cref="GetSpotsAcrossZones"/>. Sheet reads are cheap; the bound
    /// keeps the spoken list to the neighbourhood the player can walk to
    /// without a long Aetheryte hop - same reachability idea as DutyEntranceService.</summary>
    private const int MaxZoneHops = 2;

    // Territory -> catalogue spots, read once (sheet data never changes at
    // runtime). Live nodes are NOT cached - they are re-scanned each call.
    private readonly Dictionary<uint, List<GatherSpotInfo>> _byTerritory = [];

    public GatheringService(
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
    /// Gathering spots for the current zone: live targetable nodes first
    /// (fresh ObjectTable scan), then catalogue bases not already covered by a
    /// live node, both sorted by level then distance. When the player is not on
    /// a gathering class every type is returned for the catalogue half.
    /// </summary>
    public List<GatherSpotInfo> GetSpotsInCurrentZone()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return new List<GatherSpotInfo>();

        var territory = (uint)_clientState.TerritoryType;
        var allowedTypes = AllowedGatheringTypes(player.ClassJob.RowId);
        var playerPos = player.Position;

        var live = GetLiveSpotsInCurrentZone(allowedTypes);
        var liveBases = new HashSet<uint>(live.Select(s => s.GatheringPointBaseId));

        var catalog = GetAllSpotsInZone(territory)
            .Where(s => allowedTypes == null || allowedTypes.Contains(s.GatheringTypeId))
            .Where(s => !liveBases.Contains(s.GatheringPointBaseId));

        return live.Concat(catalog)
            .OrderByDescending(s => s.CurrentlyUp)
            .ThenBy(s => s.Level)
            .ThenBy(s => PlacesService.Distance2D(playerPos, s.Position))
            .ToList();
    }

    /// <summary>
    /// Reachable gathering spots: live targetable nodes in the current zone
    /// first (re-scanned every call), then catalogue entries for this zone and
    /// neighbours within <see cref="MaxZoneHops"/>, level-sorted. Empty when
    /// the player is not on a gathering class, or none are in range.
    /// </summary>
    public List<(GatherSpotInfo Spot, uint TerritoryId, uint MapId, bool InCurrentZone)> GetSpotsAcrossZones()
    {
        var result = new List<(GatherSpotInfo Spot, uint TerritoryId, uint MapId, bool InCurrentZone)>();
        var player = _objectTable.LocalPlayer;
        if (player == null) return result;

        var allowedTypes = AllowedGatheringTypes(player.ClassJob.RowId);
        if (allowedTypes == null) return result;   // not a gathering class - hide, not "show everything"

        var currentTerritory = (uint)_clientState.TerritoryType;
        var currentMap       = _clientState.MapId;
        var playerPos         = player.Position;

        // Live first: ObjectTable scan every time so a new spawn shows up on the
        // next category step (user 2026-09-06).
        var live = GetLiveSpotsInCurrentZone(allowedTypes);
        var liveBases = new HashSet<uint>(live.Select(s => s.GatheringPointBaseId));
        foreach (var spot in live)
            result.Add((spot, currentTerritory, currentMap, true));

        // Catalogue neighbourhood. Several Map rows can share one TerritoryType
        // (log 2026-09-06: Key 809 twice) - keep one entry per territory, the
        // shortest hop wins, or ToDictionary below throws and CycleCategory
        // swallows the category.
        var hopsByMap = _places.GetHopDistances();
        var territories = new List<(uint TerritoryId, uint MapId, int Hops)> { (currentTerritory, currentMap, 0) };
        foreach (var (mapId, hops) in hopsByMap)
        {
            if (mapId == currentMap || hops > MaxZoneHops) continue;
            var territoryId = _places.GetTerritoryOfMap(mapId);
            if (territoryId == 0 || territoryId == currentTerritory) continue;

            var existing = territories.FindIndex(t => t.TerritoryId == territoryId);
            if (existing >= 0)
            {
                if (hops < territories[existing].Hops)
                    territories[existing] = (territoryId, mapId, hops);
                continue;
            }
            territories.Add((territoryId, mapId, hops));
        }

        foreach (var (territoryId, mapId, _) in territories)
        {
            var inCurrentZone = territoryId == currentTerritory;
            foreach (var spot in GetAllSpotsInZone(territoryId))
            {
                if (!allowedTypes.Contains(spot.GatheringTypeId)) continue;
                // Live node already covers this base in the current zone - do not
                // also list the static catalogue centre for the same cluster.
                if (inCurrentZone && liveBases.Contains(spot.GatheringPointBaseId)) continue;
                result.Add((spot, territoryId, mapId, inCurrentZone));
            }
        }

        // Currently-up on this map first, then level, then this zone, then hops,
        // then nearer (user 2026-09-06).
        var hopsOf = territories.ToDictionary(t => t.TerritoryId, t => t.Hops);
        return result
            .OrderByDescending(x => x.Spot.CurrentlyUp)
            .ThenBy(x => x.Spot.Level)
            .ThenByDescending(x => x.InCurrentZone)
            .ThenBy(x => hopsOf.GetValueOrDefault(x.TerritoryId, MaxZoneHops + 1))
            .ThenBy(x => x.InCurrentZone
                ? PlacesService.Distance2D(playerPos, x.Spot.Position)
                : 0f)
            .ToList();
    }

    /// <summary>
    /// Live GatheringPoint objects in the ObjectTable that the game marks
    /// targetable (currently workable). Re-read every call - not cached.
    /// <see cref="GatheringPoint"/> sheet RowId is <c>BaseId</c> on the object
    /// (same path as NavigationService.GetGatheringInfo).
    /// </summary>
    private List<GatherSpotInfo> GetLiveSpotsInCurrentZone(uint[]? allowedTypes)
    {
        var result = new List<GatherSpotInfo>();
        var gpSheet = _data.GetExcelSheet<GatheringPoint>();
        if (gpSheet == null) return result;

        foreach (var obj in _objectTable)
        {
            if (obj == null || obj.ObjectKind != ObjectKind.GatheringPoint) continue;
            // Measured 2026-08-09: live workable nodes are IsTargetable; empty
            // placements of the same BaseId are not. That is "grade abbaubar".
            if (!obj.IsTargetable) continue;

            if (!gpSheet.TryGetRow(obj.BaseId, out var gp)) continue;
            var baseRef = gp.GatheringPointBase.ValueNullable;
            if (baseRef is not { } gpBase) continue;

            var typeId = gpBase.GatheringType.RowId;
            if (allowedTypes != null && !allowedTypes.Contains(typeId)) continue;

            var typeName = gpBase.GatheringType.ValueNullable?.Name.ExtractText() ?? "";
            result.Add(new GatherSpotInfo(
                typeName,
                gpBase.GatheringLevel,
                obj.Position,
                gp.GatheringPointBase.RowId,
                typeId,
                CurrentlyUp: true));
        }

#if DEBUG
        if (result.Count > 0)
            _log.Info($"[Gather] Live abbaubar in Zone: {result.Count}.");
#endif
        return result;
    }

    /// <summary>
    /// Every gathering spot of one zone, EVERY type, read once from the
    /// GatheringPoint + ExportedGatheringPoint sheets and cached. The job
    /// filter is applied by the caller, not here, so the same cache serves
    /// every class and survives a class change without re-reading.
    /// </summary>
    private List<GatherSpotInfo> GetAllSpotsInZone(uint territoryId)
    {
        if (_byTerritory.TryGetValue(territoryId, out var cached)) return cached;

        var result = new List<GatherSpotInfo>();
        _byTerritory[territoryId] = result;

        var gpSheet  = _data.GetExcelSheet<GatheringPoint>();
        var expSheet = _data.GetExcelSheet<ExportedGatheringPoint>();
        if (gpSheet == null || expSheet == null)
        {
            _log.Warning("[Gather] GatheringPoint/ExportedGatheringPoint Sheet fehlt.");
            return result;
        }

        // One spot per GatheringPointBase in this territory. Several GatheringPoint
        // rows share a base (timed / ephemeral variants of the same node); the
        // exported sheet is keyed by that Base RowId and holds one world X/Z.
        var seenBases = new HashSet<uint>();
        foreach (var gp in gpSheet)
        {
            if (gp.TerritoryType.RowId != territoryId) continue;

            var baseId = gp.GatheringPointBase.RowId;
            if (!seenBases.Add(baseId)) continue;

            if (!expSheet.TryGetRow(baseId, out var exp)) continue;
            var baseRef = gp.GatheringPointBase.ValueNullable;
            if (baseRef is not { } gpBase) continue;

            var typeId   = gpBase.GatheringType.RowId;
            var typeName = gpBase.GatheringType.ValueNullable?.Name.ExtractText() ?? "";
            // ExportedGatheringPoint.X/Y = raw world X/Z (not map pixels). Y height
            // is absent - same 2D pattern as FishingSpot; navmesh fills it on walk.
            var pos = new Vector3(exp.X, 0f, exp.Y);
            result.Add(new GatherSpotInfo(typeName, gpBase.GatheringLevel, pos, baseId, typeId));
        }

#if DEBUG
        _log.Info($"[Gather] Zone {territoryId}: {result.Count} Sammelstellen (alle Typen, Sheet).");
        foreach (var s in result)
            _log.Info($"[Gather]   Base={s.GatheringPointBaseId} Typ={s.GatheringTypeId}('{s.TypeName}') Stufe={s.Level} Welt=({s.Position.X:F1}|{s.Position.Z:F1})");
#endif

        return result;
    }

    /// <summary>
    /// Speaks the gathering spots of the current zone (live first), each with
    /// type, level, distance and compass bearing.
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NoGatheringSpotsJob);
            return;
        }

        var playerPos = player.Position;
        var lines = new List<string>();
        foreach (var s in spots)
        {
            var dist    = PlacesService.Distance2D(playerPos, s.Position);
            var compass = CompassDirection(playerPos, s.Position);
            var status = AccessibilityStrings.GatheringSpotStatus(s.CurrentlyUp);
            lines.Add(AccessibilityStrings.SpotListLine(
                ShortTypeName(s.TypeName), s.Level, dist, compass, status));
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.GatheringSpotsList(spots.Count, string.Join(". ", lines)));
    }

    /// <summary>The nearest gathering spot the active job can work, or null.
    /// Prefers a live targetable node when one exists.</summary>
    public GatherSpotInfo? GetNearestSpot()
    {
        var spots = GetSpotsInCurrentZone();
        return spots.Count > 0 ? spots[0] : null;
    }

    /// <summary>Allowed GatheringType ids for a class job; null = no filter (not a
    /// gathering class, so show everything rather than nothing).</summary>
    private static uint[]? AllowedGatheringTypes(uint classJobId) => classJobId switch
    {
        JobMiner    => MinerTypes,
        JobBotanist => BotanistTypes,
        _           => null,
    };

    /// <summary>Drops the parenthetical action ("Minenarbeiter (Herausbrechen)"
    /// -> "Minenarbeiter") for a shorter spoken label; the action verb is noise
    /// once the player is on the matching class.</summary>
    public static string ShortTypeName(string typeName)
    {
        var paren = typeName.IndexOf('(');
        return paren > 0 ? typeName[..paren].Trim() : typeName;
    }

    /// <summary>Eight-point compass bearing (north = -Z, east = +X), same
    /// convention as the rest of the mod (see FishingService/game-api.md).</summary>
    private static string CompassDirection(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        var deg = MathF.Atan2(dx, -dz) * 180f / MathF.PI;
        if (deg < 0) deg += 360f;

        var index = (int)MathF.Round(deg / 45f) % 8;
        return AccessibilityStrings.CompassAdjectives[index];
    }
}
