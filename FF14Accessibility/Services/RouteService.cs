using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Waypoint routes over the vnavmesh walkable-surface mesh WITHOUT moving the
/// character. vnavmesh separates pathfinding from movement: "Nav.Pathfind" is
/// a pure query (verified against the installed vnavmesh DLL via ilspycmd
/// 2026-07-16: the IPC gate wraps NavmeshManager.QueryPathBasic, an ASYNC
/// method - the gate therefore returns Task&lt;List&lt;Vector3&gt;&gt;, not the
/// finished list; callers poll the task each frame and never block the
/// framework thread). Also builds the spoken route preview: waypoint hops
/// folded into compass segments ("25 Meter nach Norden, dann 30 Meter nach
/// Nordosten"). Design: docs-de/ideen/ff14-route-guidance-guide.md.
/// </summary>
public sealed class RouteService
{
    private readonly ICallGateSubscriber<bool> _navIsReady;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>> _navPathfind;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, float, Task<List<Vector3>>> _navPathfindTolerance;
    private readonly IPluginLog _log;

    // Set once the tolerance gate has thrown - an older vnavmesh does not
    // register it, and retrying every single query would spam the log with the
    // same failure. One warning, then the plain gate for the rest of the session.
    private bool _toleranceGateMissing;

    public RouteService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        // Subscribing is always safe - the gates only fail on INVOKE while
        // vnavmesh is not loaded (IpcNotReadyError).
        _navIsReady  = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navPathfind = pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>("vnavmesh.Nav.Pathfind");
        // Same call with a goal radius. vnavmesh wraps the identical
        // QueryPathBasic, so the return type is the same Task (IPCProvider,
        // decompiled 2026-08-08).
        _navPathfindTolerance = pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, float, Task<List<Vector3>>>("vnavmesh.Nav.PathfindWithTolerance");
    }

    /// <summary>
    /// Queues a ground pathfind and returns the pending task, or null when
    /// vnavmesh is missing or its mesh is not ready yet. The task can fault
    /// (the mesh unloads mid-query on zone changes) - callers must check
    /// IsCompletedSuccessfully, not just IsCompleted.
    /// </summary>
    /// <param name="tolerance">
    /// Goal radius in metres, 0 for "exactly this point".
    ///
    /// WHAT IT DOES, decompiled 2026-08-08 rather than assumed: a value above 0
    /// swaps the A* heuristic for <c>GoalRadiusHeuristic</c>, which returns a
    /// cost of -1 once a node is within the radius - so the search accepts that
    /// node as the goal. Useful because our destinations are map markers and
    /// object positions, which routinely sit a little off the walkable surface.
    ///
    /// WHAT IT DOES NOT DO: the goal polygon is still looked up first, with
    /// vnavmesh's own default extent of 5 m (<c>PathfindMesh</c> calls
    /// <c>FindNearestMeshPoly(to)</c> with no arguments). A destination further
    /// than that from any mesh yields no polygon and therefore no route, no
    /// matter how large a tolerance is passed. Tolerance rescues "just off the
    /// surface", not "nowhere near it".
    /// </param>
    public Task<List<Vector3>>? RequestPath(Vector3 from, Vector3 to, float tolerance = 0f)
    {
        // try-catch: IPC into a foreign plugin (vnavmesh may be missing/loading)
        try
        {
            if (!_navIsReady.InvokeFunc()) return null;
            if (tolerance <= 0f || _toleranceGateMissing)
                return _navPathfind.InvokeFunc(from, to, false);

            try
            {
                return _navPathfindTolerance.InvokeFunc(from, to, false, tolerance);
            }
            catch (Exception ex)
            {
                // Gate absent (older vnavmesh): fall back rather than lose the
                // route entirely. Deliberately not silent, and said once.
                _toleranceGateMissing = true;
                _log.Warning($"[Route] Nav.PathfindWithTolerance nicht verfuegbar ({ex.Message}) - " +
                             "ab jetzt Wegsuche ohne Zieltoleranz.");
                return _navPathfind.InvokeFunc(from, to, false);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Route] Nav.Pathfind-IPC fehlgeschlagen");
            return null;
        }
    }

    // ── Spoken route preview: fold waypoint hops into compass segments ──

    // World -> compass mapping, derived from verified facts (docs/game-api.md):
    // the pixel->world formula maps map-pixel X to world X and map-pixel Y to
    // world Z with the same orientation, map images have their origin top-left
    // (pixel Y grows DOWN the map), and the in-game map is drawn north-up.
    // Hence north = -Z, east = +X and the bearing from north is atan2(dx, -dz).
    // Every preview logs its first segment vector so a flipped axis would show
    // up immediately in the first real-world test.
    // Language-aware compass words (see AccessibilityStrings.CompassWords). A
    // property, not a cached array: "/acc lang" switches language at runtime.
    private static string[] CompassWords => AccessibilityStrings.CompassWords;

    private static int SectorOf(float dx, float dz)
    {
        var bearing = Math.Atan2(dx, -dz) * (180.0 / Math.PI); // 0 = north, 90 = east
        var sector = (int)Math.Round(bearing / 45.0);
        return ((sector % 8) + 8) % 8;
    }

    /// <summary>
    /// The compass word for the bearing from <paramref name="from"/> to
    /// <paramref name="to"/> ("Nordosten"). Compass, not relative-to-heading:
    /// used for things the player plans around rather than steers towards right
    /// now - see the compass-vs-relative note in the route-guidance guide.
    /// </summary>
    public static string CompassWord(Vector3 from, Vector3 to)
        => CompassWords[SectorOf(to.X - from.X, to.Z - from.Z)];

    /// <summary>
    /// Dasselbe als Adjektiv ("östlich"), fuer Ansagen der Form
    /// "30 Meter, östlich" - siehe <see cref="CompassWord"/>.
    ///
    /// <para>
    /// SEIT 2026-08-23 IST DAS DIE GESPROCHENE RICHTUNG DES MODS, ueberall wo
    /// vorher "links"/"rechts" stand. Der Grund ist nicht Geschmack: eine
    /// Himmelsrichtung braucht die Blickrichtung des Spielers ueberhaupt nicht,
    /// sie faellt allein aus der Positionsdifferenz. Damit kann sie strukturell
    /// nicht auf die falsche Seite zeigen - anders als die relative Angabe, die
    /// genau daran jahrelang falsch war (siehe NavigationService.RelativeAngle)
    /// und die ausserdem von der Kamera abhaengt, solange `MoveMode` 0 ist.
    /// </para>
    ///
    /// <para>
    /// Was dabei VERLOREN geht und woanders aufgefangen wird: "geradeaus" gibt es
    /// im Kompass nicht, also fehlt die Bestaetigung "du laeufst richtig". Die
    /// traegt jetzt der Peil-Ton, der bei stimmender Ausrichtung mittig und ruhig
    /// wird (BeaconService - seit 2026-08-23 verstummt er dabei NICHT mehr).
    /// </para>
    /// </summary>
    public static string CompassAdjective(Vector3 from, Vector3 to)
        => AccessibilityStrings.CompassAdjectives[SectorOf(to.X - from.X, to.Z - from.Z)];

    /// <summary>
    /// The compass sector (0 = Norden .. 7 = Nordwesten) the player is FACING,
    /// from their rotation. The facing vector is (sin(rot), cos(rot)) in world
    /// XZ - the rotation convention verified in NavigationService.RelativeAngle
    /// (Live-Log 2026-07-10) - fed through the same <see cref="SectorOf"/>
    /// mapping as position bearings, so "you face north" and "north is that way"
    /// use one consistent compass.
    /// </summary>
    public static int HeadingSector(float rotation)
        => SectorOf(MathF.Sin(rotation), MathF.Cos(rotation));

    /// <summary>The compass word for a sector index; wraps, so any int is safe.</summary>
    public static string SectorWord(int sector) => CompassWords[((sector % 8) + 8) % 8];

    /// <summary>One spoken route segment: metres in one compass direction.</summary>
    public readonly record struct RouteSegment(float Distance, int Sector);

    /// <summary>Der Kompass-Sektor von einem Punkt zum anderen (0 = Norden).</summary>
    public static int SectorBetween(Vector3 from, Vector3 to)
        => SectorOf(to.X - from.X, to.Z - from.Z);

    /// <summary>
    /// Der Index des Wegpunkts, an dem das AKTUELLE Segment endet - also der
    /// letzte Punkt, den man noch in derselben Kompassrichtung erreicht, bevor
    /// der Weg abbiegt.
    ///
    /// <para>
    /// WOFUER: der Peil-Ton peilte bis 2026-08-23 den jeweils naechsten ROHEN
    /// Wegpunkt an. Beim Passieren sprang der Peilpunkt auf den uebernaechsten,
    /// der woanders liegt - der Ton rastete aus, man richtete sich neu aus, drei
    /// Meter weiter dasselbe. Gemessen an einer Route mit 5 Wegpunkten auf 72 m
    /// (Log 13:18): der Spieler meldete *"wenn ich mich ausrichte spinnt der
    /// Ton"*, und `rot` war dabei nachweislich unveraendert - es lag nie am
    /// Spieler, immer am springenden Peilpunkt.
    /// </para>
    ///
    /// <para>
    /// Die SPRACHE machte es laengst richtig: dieselbe Route wurde als
    /// "5 Wegpunkte, 2 Segmente" angesagt. Diese Methode holt den Ton auf
    /// denselben Stand und benutzt dieselbe Regel wie
    /// <see cref="BuildSegments"/> - inklusive der Sub-Meter-Ausnahme, denn ein
    /// Zentimeter-Huepfer an einer Tuerschwelle hat keine verlaessliche Richtung
    /// und darf ein Segment nicht zerschneiden.
    /// </para>
    /// </summary>
    /// <param name="from">Der Spieler - der laufende Abschnitt beginnt bei ihm,
    /// nicht beim letzten Wegpunkt, den er schon hinter sich hat.</param>
    public static int SegmentEndIndex(Vector3 from, IReadOnlyList<Vector3> waypoints, int cursor)
    {
        if (cursor < 0 || cursor >= waypoints.Count) return cursor;

        var sector = SectorBetween(from, waypoints[cursor]);
        var end = cursor;

        for (var i = cursor + 1; i < waypoints.Count; i++)
        {
            var dx = waypoints[i].X - waypoints[i - 1].X;
            var dz = waypoints[i].Z - waypoints[i - 1].Z;

            // Unter einem Meter: gehoert zum Segment, beendet es aber nicht -
            // dieselbe Schwelle wie in BuildSegments.
            if (dx * dx + dz * dz < 1f)
            {
                end = i;
                continue;
            }

            if (SectorOf(dx, dz) != sector) break;
            end = i;
        }

        return end;
    }

    /// <summary>
    /// Folds the waypoint hops into 8-sector compass segments: consecutive
    /// hops in the same sector merge into one; hops under 1 m never form a
    /// segment of their own (mesh jitter, door-threshold micro-corners) and
    /// are carried over into the next real segment instead.
    /// </summary>
    public static List<RouteSegment> BuildSegments(IReadOnlyList<Vector3> waypoints)
    {
        var segments = new List<RouteSegment>();
        var pending = 0f; // bucket for sub-metre hops
        for (var i = 1; i < waypoints.Count; i++)
        {
            var dx = waypoints[i].X - waypoints[i - 1].X;
            var dz = waypoints[i].Z - waypoints[i - 1].Z;
            var dist = MathF.Sqrt(dx * dx + dz * dz);
            if (dist < 1f)
            {
                pending += dist;
                continue;
            }

            var sector = SectorOf(dx, dz);
            if (segments.Count > 0 && segments[^1].Sector == sector)
                segments[^1] = new RouteSegment(segments[^1].Distance + dist + pending, sector);
            else
                segments.Add(new RouteSegment(dist + pending, sector));
            pending = 0f;
        }
        // Trailing sub-metre rest: add to the last segment so the total stays honest.
        if (pending > 0f && segments.Count > 0)
            segments[^1] = new RouteSegment(segments[^1].Distance + pending, segments[^1].Sector);
        return segments;
    }

    /// <summary>Spoken segment cap - longer routes end with "dann weiter"; the
    /// walk guide speaks the rest leg by leg, the preview only orients.</summary>
    private const int MaxSpokenSegments = 4;

    /// <summary>
    /// From how many metres of climb the route preview mentions height at all.
    /// The same 1,5 m as <c>AutoWalkService.LedgeAnnounceRise</c> and for the same
    /// reason: below that it is a kerb or a gentle slope, and naming it would turn
    /// every ordinary walk into a height report.
    /// </summary>
    private const float ClimbAnnounceThreshold = 1.5f;

    /// <summary>
    /// How much the route goes UP and how much it goes DOWN, in metres, summed
    /// over the waypoint hops. Kept apart on purpose: netting them out would
    /// silence exactly the case this exists for - a staircase up and back down
    /// again cancels to zero, and the player would hear nothing about either.
    ///
    /// <para>
    /// WHAT THIS CAN AND CANNOT SEE. The Y of a waypoint is its height on the
    /// walkable mesh, so a staircase between two waypoints is fully contained in
    /// their difference - that part is exact. What it cannot resolve is WHERE
    /// along the leg the climb sits: vnavmesh smooths the path with string
    /// pulling (<c>NavmeshManager.cs:155</c>), which places corners by the
    /// top-down projection, and a staircase running straight ahead makes no
    /// corner there. A rise and fall entirely BETWEEN two waypoints would also be
    /// missed, which needs a leg with no turn over a hump - rare, and the reason
    /// the wording says "along the way" rather than naming a spot.
    /// </para>
    /// </summary>
    public static (float Up, float Down) BuildClimb(IReadOnlyList<Vector3> waypoints)
    {
        var up = 0f;
        var down = 0f;
        for (var i = 1; i < waypoints.Count; i++)
        {
            var dy = waypoints[i].Y - waypoints[i - 1].Y;
            if (dy > 0f) up += dy;
            else down -= dy;
        }
        return (up, down);
    }

    /// <summary>
    /// The spoken route preview: "Weg zu X, 62 Meter: 25 Meter nach Norden,
    /// dann 30 Meter nach Nordosten, dann weiter." Compass words on purpose -
    /// relative directions are meaningless several segments ahead; the live
    /// guidance during the walk stays relative to the player's heading.
    /// </summary>
    public string DescribeRoute(string targetName, IReadOnlyList<Vector3> waypoints, Vector3? from = null)
    {
        // vnavmesh's waypoint list starts at the FIRST HOP, not at the character.
        // Measuring only between waypoints therefore drops the entire leg from
        // where the player stands to where the path begins - and a list holding
        // just one far-away point yields no segments at all, which announced a
        // 454 m walk as "practically there" (log 2026-08-10 08:04:45). Feeding
        // the player's position in as the first point makes the spoken total the
        // distance actually to be walked.
        if (from.HasValue)
        {
            var full = new List<Vector3>(waypoints.Count + 1) { from.Value };
            full.AddRange(waypoints);
            waypoints = full;
        }

        var segments = BuildSegments(waypoints);
        if (segments.Count == 0) return AccessibilityStrings.RoutePracticallyThere(targetName);

        var total = segments.Sum(s => s.Distance);
        var sb = new StringBuilder(AccessibilityStrings.RouteHeader(targetName, total));
        var spoken = Math.Min(segments.Count, MaxSpokenSegments);
        for (var i = 0; i < spoken; i++)
        {
            if (i > 0) sb.Append(AccessibilityStrings.RouteThen);
            sb.Append(AccessibilityStrings.RouteSegment(segments[i].Distance, CompassWords[segments[i].Sector]));
        }
        if (segments.Count > MaxSpokenSegments) sb.Append(AccessibilityStrings.RouteAndOn);
        sb.Append('.');

        // Height last, as its own sentence: it belongs to the whole route, not to
        // the segment it happens to follow, and gluing it onto the last compass
        // leg would read as if only that leg climbed.
        var (up, down) = BuildClimb(waypoints);
        var spokenUp = up >= ClimbAnnounceThreshold ? up : 0f;
        var spokenDown = down >= ClimbAnnounceThreshold ? down : 0f;
        if (spokenUp > 0f || spokenDown > 0f)
            sb.Append(AccessibilityStrings.RouteClimb(spokenUp, spokenDown));

        // Compass audit: first hop vector next to its spoken word (see mapping note above).
        var first = segments[0];
        _log.Info($"[Route] Vorschau '{targetName}': {waypoints.Count} Wegpunkte, {segments.Count} Segmente, " +
                  $"gesamt {total:F0} m; Segment 1 = {first.Distance:F0} m {CompassWords[first.Sector]} " +
                  $"(Start ({waypoints[0].X:F0}|{waypoints[0].Z:F0})); " +
                  $"Hoehe auf {up:F1} m / ab {down:F1} m");
        return sb.ToString();
    }
}
