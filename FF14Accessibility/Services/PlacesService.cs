using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One named waypoint of the current zone, from the static map symbols.</summary>
/// <param name="Name">Display name (place name, target zone or aetheryte name).</param>
/// <param name="TypeLabel">Spoken type: Übergang / Ätheryt / Aethernet / Ort.</param>
/// <param name="Position">World X/Z from the map marker; Y is NOT known (map
/// data is 2D) and set to 0 - resolve via navmesh before walking.</param>
/// <param name="IsZoneTransition">True for exits into another map.</param>
/// <param name="TargetMapId">Destination map for transitions, 0 otherwise.</param>
/// <param name="IsWaterSpot">True for fishing spots: the position is the CENTRE
/// of the water, not walkable ground. The walk resolves it to the nearest bank
/// (wide horizontal search) instead of the generic 10 m floor snap, which misses
/// a bank tens of metres out and lands the player away from the water.</param>
public sealed record PlaceDestination(
    string Name,
    string TypeLabel,
    Vector3 Position,
    bool IsZoneTransition,
    uint TargetMapId,
    bool IsWaterSpot = false);

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

    /// <summary>Inverse of <see cref="PixelToWorld"/>: world coordinate back to
    /// map pixel (0..2048). Solving world = (pixel-1024)*100/scale - offset for
    /// pixel gives pixel = (world+offset)*scale/100 + 1024.</summary>
    private static float WorldToPixel(float world, ushort sizeFactor, short offset)
        => (world + offset) * sizeFactor / 100f + 1024f;

    /// <summary>
    /// Converts the player-facing map coordinates shown in-game (the 1..~42
    /// values, e.g. 24.1 / 21.0) to a world position on the CURRENT map. Y is
    /// left 0 - the caller snaps it onto the walkable mesh via navmesh.
    /// Inverse of the display formula above: display = 2*pixel/scale + 1, so
    /// pixel = (display - 1) * scale / 2, then PixelToWorld. Verified against
    /// live data: map 24.1 / 21.0 in Central Thanalan -> world ~131 / -24,
    /// matching the parsonage object positions from the object probe.
    /// Returns null when the current map is unknown.
    /// </summary>
    public Vector3? MapCoordToWorld(float mapX, float mapY)
    {
        var mapId = _clientState.MapId;
        if (mapId == 0) return null;

        if (!_data.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
        {
            _log.Info($"[Goto] Map {mapId} nicht im Sheet - keine Umrechnung.");
            return null;
        }

        var pixelX = (mapX - 1f) * map.SizeFactor / 2f;
        var pixelY = (mapY - 1f) * map.SizeFactor / 2f;
        var worldX = PixelToWorld(pixelX, map.SizeFactor, map.OffsetX);
        var worldZ = PixelToWorld(pixelY, map.SizeFactor, map.OffsetY);
        _log.Info($"[Goto] Karte {mapX:0.0}/{mapY:0.0} (Map {mapId}, Scale {map.SizeFactor}, " +
                  $"Off {map.OffsetX}/{map.OffsetY}) -> Welt {worldX:0.0}/{worldZ:0.0}");
        return new Vector3(worldX, 0f, worldZ);
    }

    /// <summary>
    /// Converts a world position on the CURRENT map to the player-facing map
    /// coordinates shown in-game (the 1..~42 values). Exact inverse of
    /// <see cref="MapCoordToWorld"/> - the same verified formula solved for the
    /// map coordinate: pixel = WorldToPixel(world), coord = 2*pixel/scale + 1.
    /// Height (world.Y) is irrelevant - the map is 2D, so only X and Z are used.
    /// Returns null when the current map is unknown.
    /// </summary>
    public (float X, float Y)? WorldToMapCoord(Vector3 world)
    {
        var mapId = _clientState.MapId;
        if (mapId == 0) return null;

        if (!_data.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
        {
            _log.Info($"[Coords] Map {mapId} nicht im Sheet - keine Umrechnung.");
            return null;
        }

        var pixelX = WorldToPixel(world.X, map.SizeFactor, map.OffsetX);
        var pixelY = WorldToPixel(world.Z, map.SizeFactor, map.OffsetY);
        var mapX = pixelX * 2f / map.SizeFactor + 1f;
        var mapY = pixelY * 2f / map.SizeFactor + 1f;
        return (mapX, mapY);
    }

    /// <summary>
    /// Converts a raw map-PIXEL coordinate pair (0..2048, the convention used by
    /// the MapMarker and FishingSpot sheets) to a world position on the CURRENT
    /// map. Y is left 0 - the caller snaps it onto the walkable mesh via navmesh.
    /// Verified against real FishingSpot data (2026-07-25): all sheet X/Z sit in
    /// 108..1948, i.e. inside the 0..2048 pixel range, and convert to sensible
    /// in-zone map coordinates. Returns null when the current map is unknown.
    /// </summary>
    public Vector3? MapPixelToWorld(float pixelX, float pixelY)
    {
        var mapId = _clientState.MapId;
        if (mapId == 0) return null;

        if (!_data.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
        {
            _log.Info($"[Goto] Map {mapId} nicht im Sheet - keine Pixel-Umrechnung.");
            return null;
        }

        var worldX = PixelToWorld(pixelX, map.SizeFactor, map.OffsetX);
        var worldZ = PixelToWorld(pixelY, map.SizeFactor, map.OffsetY);
        return new Vector3(worldX, 0f, worldZ);
    }

    /// <summary>Spoken type label of the player-set map flag.</summary>
    public const string FlagTypeLabel = "Markierung";

    /// <summary>
    /// The player-set / party map flag of the CURRENT map, or null when no flag
    /// is set or it belongs to another map. In group play the flag is how
    /// sighted players direct each other ("go to the flag"), so it is the most
    /// socially important coordinate in the game for a blind player.
    ///
    /// Verified via ilspycmd (FFXIVClientStructs, 2026-07-18):
    /// AgentMap.FlagMarkerCount (byte @23294) counts the set flags,
    /// AgentMap.FlagMapMarkers is a Span of one FlagMapMarker with
    /// TerritoryId@56, MapId@60, XFloat@64, YFloat@68. Unlike the static map
    /// symbols these are WORLD coordinates, not map pixels: AgentMap's own
    /// SetFlagMapMarker(territoryId, mapId, Vector3 world) stores world.X in
    /// XFloat and world.Z in YFloat. Height is still unknown (the map is 2D)
    /// and resolved via navmesh before walking, like every other waypoint.
    /// </summary>
    public unsafe PlaceDestination? GetFlagMarker()
    {
        var agent = AgentMap.Instance();
        if (agent == null || agent->FlagMarkerCount == 0) return null;

        var flag = agent->FlagMapMarkers[0];
        if (flag.MapId != _clientState.MapId) return null;

        return new PlaceDestination(
            AccessibilityStrings.FlagName,   // spoken name (bilingual)
            FlagTypeLabel,                    // internal type identity (stays German)
            new Vector3(flag.XFloat, 0f, flag.YFloat),
            IsZoneTransition: false,
            TargetMapId: 0);
    }

    /// <summary>All named waypoints of the current map. Read fresh per call.</summary>
    public List<PlaceDestination> GetPlaces()
    {
        var result = new List<PlaceDestination>();
        var mapId = _clientState.MapId;
        if (mapId == 0) return result;

        // The flag is a waypoint like any other, so it flows into the browser,
        // the walk guide and the auto-walk through the existing path.
        var flag = GetFlagMarker();
        if (flag != null) result.Add(flag);

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
                    name = AccessibilityStrings.TransitionToName(name);
                    type = "Übergang";
                    isTransition = true;
                    break;

                case 3: // Ätheryt (DataKey = Aetheryte-Zeile)
                    name = m.DataKey.TryGetValue<Aetheryte>(out var aetheryte)
                        ? aetheryte.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty
                        : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) name = subtext;
                    if (string.IsNullOrWhiteSpace(name)) name = AccessibilityStrings.AetheryteFallbackName;
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

    // ── Orte auf FREMDEN Karten (Jagdtagebuch, zonenübergreifende Ziele) ──
    //
    // GetPlaces above only ever answers for the map the player stands on. A
    // hunting log habitat ("Zentrales La Noscea, Sommerfurt") is usually
    // somewhere else, so both steps - zone name to map, and area name to
    // position - have to work without being there.

    // Zone PlaceName row -> map row. Sheet data, built once.
    private Dictionary<uint, uint>? _mapByZonePlace;

    /// <summary>
    /// The map whose zone name is <paramref name="placeNameRowId"/>, or 0 when
    /// no map carries that place name. Measured 2026-08-17: all 647 hunting log
    /// zone entries resolve this way.
    /// </summary>
    public uint FindMapByPlaceName(uint placeNameRowId)
    {
        if (placeNameRowId == 0) return 0;
        if (_mapByZonePlace == null)
        {
            _mapByZonePlace = [];
            foreach (var map in _data.GetExcelSheet<Map>())
            {
                var pn = map.PlaceName.RowId;
                if (pn == 0) continue;
                // First map wins: later rows are the same zone re-issued for
                // instances, and their markers are the same ones.
                _mapByZonePlace.TryAdd(pn, map.RowId);
            }
            _log.Info($"[Orte] Zonen-Namenstabelle: {_mapByZonePlace.Count} Karten.");
        }
        return _mapByZonePlace.GetValueOrDefault(placeNameRowId);
    }

    /// <summary>
    /// World position of the named area's map marker on the GIVEN map, or null
    /// when that map has no marker for it. This is the centre of the area the
    /// game labels on its map - the same spot a sighted player aims for when
    /// the hunting log names a habitat, not the monster itself. Y is unknown
    /// (map data is 2D) and resolved via navmesh before walking.
    /// </summary>
    /// <remarks>
    /// DER NAME ZÄHLT MIT, NICHT NUR DIE ZEILE. Das Spiel führt für denselben
    /// Ort mehrere PlaceName-Zeilen: das Jagdtagebuch nennt als Lebensraum
    /// "Halatali" (Zeile 49, der Inhalt), Karte und Zonen-Layout kennen ihn als
    /// Zeile 305 (der Ort in der Welt). Der reine Zeilenvergleich fand deshalb
    /// nichts und die Mod antwortete "auf der Karte nicht verzeichnet", obwohl
    /// der Marker direkt daneben lag (im Spiel-Log belegt, 2026-09-02).
    ///
    /// Die Zeile gewinnt weiterhin: erst wird sie gesucht, der Name ist nur der
    /// Ersatzweg. Gibt es mehrere Marker desselben Namens (auf Karte 22 zweimal
    /// "Chocobo-Ställe"), gewinnt der erste - dieselbe Regel wie zuvor.
    /// </remarks>
    public Vector3? FindMarkerPosition(uint mapId, uint placeNameRowId)
        => FindMarkerPosition(mapId, placeNameRowId, string.Empty);

    /// <inheritdoc cref="FindMarkerPosition(uint,uint)"/>
    /// <param name="placeNameText">Angezeigter Name des Ortes für den
    /// Ersatzweg. Leer = nur die Zeile zählt.</param>
    public Vector3? FindMarkerPosition(uint mapId, uint placeNameRowId, string placeNameText)
    {
        if (mapId == 0 || (placeNameRowId == 0 && placeNameText.Length == 0)) return null;
        if (!_data.GetExcelSheet<Map>().TryGetRow(mapId, out var map)) return null;
        if (!_data.GetSubrowExcelSheet<MapMarker>().TryGetRow(map.MapMarkerRange, out var markers)) return null;

        Vector3 At(MapMarker m) => new(
            PixelToWorld(m.X, map.SizeFactor, map.OffsetX),
            0f,
            PixelToWorld(m.Y, map.SizeFactor, map.OffsetY));

        foreach (var m in markers)
            if (placeNameRowId != 0 && m.PlaceNameSubtext.RowId == placeNameRowId)
                return At(m);

        var wanted = placeNameText.Trim();
        if (wanted.Length == 0) return null;

        foreach (var m in markers)
        {
            var text = m.PlaceNameSubtext.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
            if (text.Length == 0 || !string.Equals(text, wanted, StringComparison.OrdinalIgnoreCase)) continue;
            _log.Info($"[Orte] '{wanted}' über den Namen gefunden (Zeile {placeNameRowId} steht auf keinem Marker " +
                      $"der Karte {mapId}, Marker führt Zeile {m.PlaceNameSubtext.RowId}).");
            return At(m);
        }
        return null;
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
    /// Wie viele Zonenwechsel von der aktuellen Karte bis zu jeder erreichbaren
    /// Karte - EIN Breitensuchlauf ueber denselben Uebergangsgraphen, den
    /// <see cref="FindFirstHopToMap"/> pro Ziel durchlaeuft.
    ///
    /// Warum es das gibt: die weltweite Inhaltsliste hat 155 Ziele, und mehrere
    /// Inhalte haben Eingaenge in verschiedenen Zonen. "Welcher davon ist der
    /// naechste" 155 mal einzeln zu fragen hiesse 155 Breitensuchen pro
    /// Tastendruck; hier ist es einer, dessen Ergebnis alle beantwortet. Karten,
    /// die kein Uebergang erreicht (Instanz-Gebiete, Faehre-nur-Inseln), fehlen
    /// im Ergebnis - genau die Auskunft "da kommt man nicht hin laufend".
    /// </summary>
    public Dictionary<uint, int> GetHopDistances()
    {
        var distance = new Dictionary<uint, int>();
        var start = _clientState.MapId;
        if (start == 0) return distance;

        distance[start] = 0;
        var queue = new Queue<uint>();
        queue.Enqueue(start);
        // Dieselbe Schranke wie in FindFirstHopToMap: der Kartengraph ist klein,
        // und eine Obergrenze haelt einen kaputten Sheet-Zyklus aus dem Frame.
        while (queue.Count > 0 && distance.Count < 500)
        {
            var current = queue.Dequeue();
            foreach (var next in GetTransitionTargets(current))
            {
                if (distance.ContainsKey(next)) continue;
                distance[next] = distance[current] + 1;
                queue.Enqueue(next);
            }
        }
        return distance;
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
        // TypeLabel is an internal identity string (matched as "Ätheryt"/"Aethernet"
        // elsewhere), so it must not be translated - the spoken hint uses only the
        // game-provided Name and a translated frame.
        return AccessibilityStrings.NoPathAetheryteHint(shard.Name);
    }

    /// <summary>2D distance (X/Z) - marker positions carry no usable height.</summary>
    public static float Distance2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
