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
    private readonly IPluginLog _log;

    public RouteService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;
        // Subscribing is always safe - the gates only fail on INVOKE while
        // vnavmesh is not loaded (IpcNotReadyError).
        _navIsReady  = pluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _navPathfind = pluginInterface.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>("vnavmesh.Nav.Pathfind");
    }

    /// <summary>
    /// Queues a ground pathfind and returns the pending task, or null when
    /// vnavmesh is missing or its mesh is not ready yet. The task can fault
    /// (the mesh unloads mid-query on zone changes) - callers must check
    /// IsCompletedSuccessfully, not just IsCompleted.
    /// </summary>
    public Task<List<Vector3>>? RequestPath(Vector3 from, Vector3 to)
    {
        // try-catch: IPC into a foreign plugin (vnavmesh may be missing/loading)
        try
        {
            if (!_navIsReady.InvokeFunc()) return null;
            return _navPathfind.InvokeFunc(from, to, false);
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
    private static readonly string[] CompassWords =
        { "Norden", "Nordosten", "Osten", "Südosten", "Süden", "Südwesten", "Westen", "Nordwesten" };

    private static int SectorOf(float dx, float dz)
    {
        var bearing = Math.Atan2(dx, -dz) * (180.0 / Math.PI); // 0 = north, 90 = east
        var sector = (int)Math.Round(bearing / 45.0);
        return ((sector % 8) + 8) % 8;
    }

    /// <summary>One spoken route segment: metres in one compass direction.</summary>
    public readonly record struct RouteSegment(float Distance, int Sector);

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
    /// The spoken route preview: "Weg zu X, 62 Meter: 25 Meter nach Norden,
    /// dann 30 Meter nach Nordosten, dann weiter." Compass words on purpose -
    /// relative directions are meaningless several segments ahead; the live
    /// guidance during the walk stays relative to the player's heading.
    /// </summary>
    public string DescribeRoute(string targetName, IReadOnlyList<Vector3> waypoints)
    {
        var segments = BuildSegments(waypoints);
        if (segments.Count == 0) return $"Weg zu {targetName}: praktisch am Ziel.";

        var total = segments.Sum(s => s.Distance);
        var sb = new StringBuilder($"Weg zu {targetName}, {total:F0} Meter: ");
        var spoken = Math.Min(segments.Count, MaxSpokenSegments);
        for (var i = 0; i < spoken; i++)
        {
            if (i > 0) sb.Append(", dann ");
            sb.Append($"{segments[i].Distance:F0} Meter nach {CompassWords[segments[i].Sector]}");
        }
        if (segments.Count > MaxSpokenSegments) sb.Append(", dann weiter");
        sb.Append('.');

        // Compass audit: first hop vector next to its spoken word (see mapping note above).
        var first = segments[0];
        _log.Info($"[Route] Vorschau '{targetName}': {waypoints.Count} Wegpunkte, {segments.Count} Segmente, " +
                  $"gesamt {total:F0} m; Segment 1 = {first.Distance:F0} m {CompassWords[first.Sector]} " +
                  $"(Start ({waypoints[0].X:F0}|{waypoints[0].Z:F0}))");
        return sb.ToString();
    }
}
