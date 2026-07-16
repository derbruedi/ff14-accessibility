using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One named waypoint of the current zone, from the static map symbols.</summary>
/// <param name="Name">Display name (place name, target zone or aetheryte name).</param>
/// <param name="TypeLabel">Spoken type: Übergang / Ätheryt / Aethernet / Ort.</param>
/// <param name="Position">World X/Z from the map marker; Y is NOT known (map
/// data is 2D) and set to 0 - resolve via navmesh before walking.</param>
/// <param name="IsZoneTransition">True for exits into another map.</param>
/// <param name="TargetMapId">Destination map for transitions, 0 otherwise.</param>
public sealed record PlaceDestination(
    string Name,
    string TypeLabel,
    Vector3 Position,
    bool IsZoneTransition,
    uint TargetMapId);

/// <summary>
/// Reads the static map symbols (zone exits, aetherytes, named places) of the
/// player's current map from the Lumina "MapMarker" sheet. These are the
/// waypoints a sighted player sees on the map - including the exits needed to
/// reach quest targets in other zones (user request 2026-07-11).
///
/// Sheet layout verified via ilspycmd (Lumina.Excel.Sheets.MapMarker):
/// X@8/Y@10 = map PIXEL coordinates (0..2048), Icon@0, PlaceNameSubtext@2,
/// DataType@15 with DataKey@4: 1/2 = Map (zone transition target),
/// 3 = Aetheryte, 4 = PlaceName (aethernet shard). Rows are grouped by
/// Map.MapMarkerRange (subrow sheet).
/// </summary>
public sealed class PlacesService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPluginLog   _log;

    public PlacesService(IDataManager data, IClientState clientState, IPluginLog log)
    {
        _data        = data;
        _clientState = clientState;
        _log         = log;
    }

    /// <summary>
    /// Converts a map-marker pixel coordinate (0..2048) to a world coordinate.
    /// Derivation from Dalamud's own decompiled MapUtil (2026-07-11):
    /// display = 0.02*offset + 2048/scale + 0.02*world + 1 and
    /// display = 2*pixel/scale + 1 (checked: pixel 0 -> 1.0, pixel 2048 ->
    /// 42.0 at SizeFactor 100) => world = (pixel - 1024) * 100/scale - offset.
    /// </summary>
    private static float PixelToWorld(float pixel, ushort sizeFactor, short offset)
        => (pixel - 1024f) * 100f / sizeFactor - offset;

    /// <summary>All named waypoints of the current map. Read fresh per call.</summary>
    public List<PlaceDestination> GetPlaces()
    {
        var result = new List<PlaceDestination>();
        var mapId = _clientState.MapId;
        if (mapId == 0) return result;

        var mapSheet = _data.GetExcelSheet<Map>();
        if (!mapSheet.TryGetRow(mapId, out var map))
        {
            _log.Warning($"[Orte] Map-Zeile {mapId} nicht gefunden.");
            return result;
        }

        var markerSheet = _data.GetSubrowExcelSheet<MapMarker>();
        if (!markerSheet.TryGetRow(map.MapMarkerRange, out var markers))
        {
            _log.Info($"[Orte] Keine MapMarker-Zeile für Range {map.MapMarkerRange} (Map {mapId}).");
            return result;
        }

        foreach (var m in markers)
        {
            string name;
            string type;
            var isTransition = false;
            var targetMapId = 0u;

            var subtext = m.PlaceNameSubtext.ValueNullable?.Name.ExtractText() ?? string.Empty;

            switch (m.DataType)
            {
                case 1:
                case 2: // Übergang in eine andere Karte (DataKey = Ziel-Map)
                    name = string.Empty;
                    if (m.DataKey.TryGetValue<Map>(out var targetMap))
                    {
                        targetMapId = targetMap.RowId;
                        name = targetMap.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                    }
                    if (string.IsNullOrWhiteSpace(name)) name = subtext;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    name = $"Übergang nach {name}";
                    type = "Übergang";
                    isTransition = true;
                    break;

                case 3: // Ätheryt (DataKey = Aetheryte-Zeile)
                    name = m.DataKey.TryGetValue<Aetheryte>(out var aetheryte)
                        ? aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty
                        : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) name = subtext;
                    if (string.IsNullOrWhiteSpace(name)) name = "Ätheryt";
                    type = "Ätheryt";
                    break;

                case 4: // Aethernet-Scherbe (DataKey = PlaceName)
                    name = m.DataKey.TryGetValue<PlaceName>(out var placeName)
                        ? placeName.Name.ExtractText()
                        : subtext;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    type = "Aethernet";
                    break;

                default: // benannter Ort (Gebäude, Gilde, Marktbrett ...)
                    name = subtext;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    type = "Ort";
                    break;
            }

            var pos = new Vector3(
                PixelToWorld(m.X, map.SizeFactor, map.OffsetX),
                0f, // Y unbekannt (Kartendaten sind 2D) - vor dem Lauf via Navmesh auflösen
                PixelToWorld(m.Y, map.SizeFactor, map.OffsetY));

            result.Add(new PlaceDestination(name, type, pos, isTransition, targetMapId));
        }

        _log.Info($"[Orte] Map {mapId} (Range {map.MapMarkerRange}): {result.Count} Wegpunkte gelesen.");
        return result;
    }

    // ── Zonen-Routing: welcher Übergang führt Richtung Ziel-Karte? ──

    // Static transition graph (map id -> reachable map ids), built lazily
    // from the same MapMarker data. Sheet data never changes at runtime.
    private readonly Dictionary<uint, List<uint>> _transitionCache = [];

    private List<uint> GetTransitionTargets(uint mapId)
    {
        if (_transitionCache.TryGetValue(mapId, out var cached)) return cached;

        var targets = new List<uint>();
        var mapSheet = _data.GetExcelSheet<Map>();
        if (mapSheet.TryGetRow(mapId, out var map)
            && _data.GetSubrowExcelSheet<MapMarker>().TryGetRow(map.MapMarkerRange, out var markers))
        {
            foreach (var m in markers)
            {
                if (m.DataType is not (1 or 2)) continue;
                if (m.DataKey.TryGetValue<Map>(out var target) && target.RowId != 0)
                    targets.Add(target.RowId);
            }
        }
        _transitionCache[mapId] = targets;
        return targets;
    }

    /// <summary>Zone name for announcements ("im Gebiet X"), from the map's PlaceName.</summary>
    public string GetMapName(uint mapId)
    {
        return _data.GetExcelSheet<Map>().TryGetRow(mapId, out var map)
            ? map.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Finds the transition IN THE CURRENT MAP that is the first hop of the
    /// shortest transition route to the target map (BFS over the static
    /// map-transition graph). Null when no route exists (e.g. only reachable
    /// by teleport or ferry). Blind players cannot see the world map - this
    /// answers "WHERE do I have to walk?" for quests in other zones.
    /// </summary>
    public PlaceDestination? FindFirstHopToMap(uint targetMapId, out int hops)
    {
        hops = 0;
        var start = _clientState.MapId;
        if (start == 0 || targetMapId == 0 || start == targetMapId) return null;

        // BFS with parent tracking, depth-limited (map graph is small).
        var parent  = new Dictionary<uint, uint> { [start] = start };
        var queue   = new Queue<uint>();
        queue.Enqueue(start);
        var found = false;
        while (queue.Count > 0 && parent.Count < 500)
        {
            var current = queue.Dequeue();
            if (current == targetMapId) { found = true; break; }
            foreach (var next in GetTransitionTargets(current))
            {
                if (parent.ContainsKey(next)) continue;
                parent[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!found)
        {
            _log.Info($"[Orte] Keine Übergangs-Route von Map {start} nach Map {targetMapId}.");
            return null;
        }

        // Walk back from the target to the map whose parent is the start:
        // that is the first hop, and its transition marker sits in OUR map.
        var hop = targetMapId;
        hops = 1;
        while (parent[hop] != start)
        {
            hop = parent[hop];
            hops++;
        }

        var transition = GetPlaces().FirstOrDefault(p => p.IsZoneTransition && p.TargetMapId == hop);
        _log.Info($"[Orte] Route Map {start} -> {targetMapId}: {hops} Übergänge, erster: " +
                  (transition?.Name ?? $"KEIN Marker für Ziel-Map {hop} gefunden"));
        return transition;
    }

    /// <summary>
    /// The aetheryte or aethernet shard of the current zone closest to
    /// <paramref name="position"/> (2D distance, marker Y is unknown), or null.
    /// Travel hint for unreachable destinations: city levels are often joined
    /// only by lifts/aethernet, so the shard nearest to the destination is the
    /// game's intended way there.
    /// </summary>
    public PlaceDestination? NearestAetheryteTo(Vector3 position)
    {
        return GetPlaces()
            .Where(p => p.TypeLabel is "Ätheryt" or "Aethernet")
            .OrderBy(p => Distance2D(position, p.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Travel hint for a destination the navmesh cannot reach: city levels are
    /// often separate mesh islands joined only by lifts/aethernet - walking can
    /// NEVER cross that gap, the game's own transport is the way. Names the
    /// aetheryte/shard closest to the destination when one is plausibly "at"
    /// it (within 100 m); empty string otherwise (open-world targets).
    /// Shared by the auto-walk and the route preview.
    /// </summary>
    public string BuildNoPathHint(Vector3 destination)
    {
        var shard = NearestAetheryteTo(destination);
        if (shard == null) return string.Empty;
        var dist = Distance2D(shard.Position, destination);
        if (dist > 100f) return string.Empty;
        _log.Info($"[Nav] Kein-Weg-Tipp: {shard.TypeLabel} {shard.Name} liegt {dist:F0} m vom Ziel.");
        return $" Das Ziel liegt nahe {shard.TypeLabel} {shard.Name}. Reise per Aethernet dorthin.";
    }

    /// <summary>2D distance (X/Z) - marker positions carry no usable height.</summary>
    public static float Distance2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
