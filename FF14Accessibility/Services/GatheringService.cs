using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One gathering cluster of the current zone (all node placements that
/// share a GatheringPoint), centred on the average of its placements.</summary>
/// <param name="TypeName">GatheringType name ("Minenarbeiter (Herausbrechen)").</param>
/// <param name="Level">Required gathering level.</param>
/// <param name="Position">World position (X/Z from the LGB; Y is the node height).</param>
public sealed record GatherSpotInfo(string TypeName, int Level, Vector3 Position, uint GatheringPointId, uint GatheringTypeId);

/// <summary>
/// Gathering (Miner ore / Botanist wood) accessibility. A blind gatherer cannot
/// see where the nodes are, so - like <see cref="FishingService"/> for fishing
/// holes - the job is answering "where can I gather in this zone?" and walking to
/// the spot, however far across the map.
///
/// Unlike fishing (clean FishingSpot sheet with a zone column), the zone->node
/// mapping is NOT in Excel. It lives in the territory's LGB layout file, which
/// Lumina can parse. Verified end to end (2026-07-27):
/// - ExportedGatheringPoint / LGB coordinates are RAW WORLD X/Z (matched live
///   spawned nodes to 5-12 m; pixel/map-coord interpretations were 1000s of m off).
/// - LGB InstanceObject with AssetType==Gathering carries a GatheringInstanceObject
///   whose GatheringPointId is the GatheringPoint sheet RowId, giving type + level.
///
/// So: load the current territory's LGB, take every Gathering placement, resolve
/// its GatheringPoint -> GatheringPointBase -> GatheringType/Level, filter to the
/// active gathering job, group placements that share a GatheringPoint into one
/// spot, and hand the nearest-first list to the existing walk guide.
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
    private const uint JobMiner    = 16;
    private const uint JobBotanist = 17;

    // GatheringType row ids: Miner does 0 (Mining) + 1 (Quarrying), Botanist does
    // 2 (Logging) + 3 (Harvesting). Verified by name in the probe log 2026-07-27
    // (type 1 = "Minenarbeiter (Herausbrechen)", 2/3 = "Gärtner (Abholzen/Abernten)").
    private static readonly uint[] MinerTypes    = { 0, 1 };
    private static readonly uint[] BotanistTypes = { 2, 3 };

    // The LGB files under a territory's level folder that can hold gathering nodes.
    private static readonly string[] LgbNames = { "planevent.lgb", "bg.lgb", "planmap.lgb", "planlive.lgb" };

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
    /// All gathering spots of the CURRENT zone that the active job can work,
    /// grouped per GatheringPoint and sorted nearest-first from the player. When
    /// the player is not on a gathering class every type is returned. Empty when
    /// no player is loaded or the zone has no matching nodes.
    /// </summary>
    public List<GatherSpotInfo> GetSpotsInCurrentZone()
    {
        var result = new List<GatherSpotInfo>();
        var player = _objectTable.LocalPlayer;
        if (player == null) return result;

        var territory = (ushort)_clientState.TerritoryType;
        if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var tt)) return result;
        var bg = tt.Bg.ExtractText();
        if (string.IsNullOrWhiteSpace(bg)) return result;

        var allowedTypes = AllowedGatheringTypes(player.ClassJob.RowId);
        var gpSheet = _data.GetExcelSheet<GatheringPoint>();

        // Collect every gathering placement from the territory's LGB files, keyed
        // by GatheringPoint so placements of the same node fold into one spot.
        var byPoint = new Dictionary<uint, (List<Vector3> Pts, uint TypeId, string TypeName, int Level)>();

        foreach (var lgbName in LgbNames)
        {
            var path = BuildLgbPath(bg, lgbName);
            LgbFile? lgb;
            try { lgb = _data.GetFile<LgbFile>(path); }
            catch (Exception ex) { _log.Warning(ex, $"[Gather] LGB laden fehlgeschlagen: {path}"); continue; }
#if DEBUG
            if (lgb == null) { _log.Info($"[Gather] LGB '{path}': NICHT GEFUNDEN"); continue; }
            var totalObjs = lgb.Layers.Sum(l => l.InstanceObjects.Length);
            var gathCount = lgb.Layers.Sum(l => l.InstanceObjects.Count(o => o.AssetType == LayerEntryType.Gathering));
            var sgCount   = lgb.Layers.Sum(l => l.InstanceObjects.Count(o => o.AssetType == LayerEntryType.SharedGroup));
            _log.Info($"[Gather] LGB '{path}': {lgb.Layers.Length} Layer, {totalObjs} Objekte, " +
                      $"Gathering={gathCount}, SharedGroup={sgCount}");
#else
            if (lgb == null) continue;
#endif

            foreach (var layer in lgb.Layers)
            {
                foreach (var obj in layer.InstanceObjects)
                {
                    if (obj.AssetType != LayerEntryType.Gathering) continue;
                    if (obj.Object is not LayerCommon.GatheringInstanceObject g) continue;

                    var gpId = g.GatheringPointId;
                    if (!gpSheet.TryGetRow(gpId, out var gp)) continue;
                    var baseRef = gp.GatheringPointBase.ValueNullable;
                    if (baseRef is not { } gpBase) continue;

                    var typeId = gpBase.GatheringType.RowId;
                    if (allowedTypes != null && !allowedTypes.Contains(typeId)) continue;

                    var t = obj.Transform.Translation;
                    var pos = new Vector3(t.X, t.Y, t.Z);

                    if (!byPoint.TryGetValue(gpId, out var entry))
                    {
                        var typeName = gpBase.GatheringType.ValueNullable?.Name.ExtractText() ?? "";
                        entry = (new List<Vector3>(), typeId, typeName, gpBase.GatheringLevel);
                        byPoint[gpId] = entry;
                    }
                    entry.Pts.Add(pos);
                }
            }
        }

        foreach (var (gpId, e) in byPoint)
        {
            if (e.Pts.Count == 0) continue;
            var centre = new Vector3(e.Pts.Average(p => p.X), e.Pts.Average(p => p.Y), e.Pts.Average(p => p.Z));
            result.Add(new GatherSpotInfo(e.TypeName, e.Level, centre, gpId, e.TypeId));
        }

#if DEBUG
        _log.Info($"[Gather] Zone {territory} Bg='{bg}': {byPoint.Count} Sammelstellen (Job-Filter {(AllowedGatheringTypes(player.ClassJob.RowId) == null ? "aus" : "an")})");
        foreach (var s in result)
            _log.Info($"[Gather]   GP={s.GatheringPointId} Typ={s.GatheringTypeId}('{s.TypeName}') Stufe={s.Level} Welt=({s.Position.X:F1}|{s.Position.Z:F1})");
#endif

        var playerPos = player.Position;
        return result
            .OrderBy(s => PlacesService.Distance2D(playerPos, s.Position))
            .ToList();
    }

    /// <summary>
    /// Speaks the gathering spots of the current zone, nearest first, each with
    /// type, level, distance and compass bearing - so a blind gatherer knows where
    /// they can gather and which way to head.
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
            lines.Add(AccessibilityStrings.SpotListLine(ShortTypeName(s.TypeName), s.Level, dist, compass));
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.GatheringSpotsList(spots.Count, string.Join(". ", lines)));
    }

    /// <summary>The nearest gathering spot the active job can work, or null.</summary>
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

    /// <summary>
    /// LGB path for a territory Bg. Bg is e.g. "ffxiv/wil_w1/fld/w1f1/level/w1f1";
    /// the layout files sit next to it as "bg/&lt;dir&gt;/level/&lt;name&gt;.lgb".
    /// </summary>
    private static string BuildLgbPath(string bg, string lgbName)
    {
        var slash = bg.LastIndexOf('/');
        var dir   = slash >= 0 ? bg[..(slash + 1)] : bg;
        return $"bg/{dir}{lgbName}";
    }

    /// <summary>Drops the parenthetical action ("Minenarbeiter (Herausbrechen)"
    /// -> "Minenarbeiter") for a shorter spoken label; the action verb is noise
    /// once the player is on the matching class.</summary>
    private static string ShortTypeName(string typeName)
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
