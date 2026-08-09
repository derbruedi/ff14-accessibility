using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the navigation mesh vnavmesh has cached for a zone and answers the one
/// question its IPC cannot: do two spots belong to the same connected surface?
///
/// WHY THIS EXISTS. A destination can sit on ground the mesh KNOWS but that has
/// no polygon link to the surface the player stands on (measured 2026-08-09,
/// Westliches Thanalan: 29 polygons against 17.570, no overlap). Walking there
/// is then impossible by pathfinding alone, and the only way across is to drive
/// the last few metres blind - which requires knowing exactly where the two
/// surfaces come closest. The vnavmesh IPC offers "is there floor here" and
/// "does a route run from A to B"; the second answers connectivity but costs
/// ~37 ms per query (measured from the log), far too slow for a grid dense
/// enough to find an edge. Probing instead of measuring was tried and failed:
/// a ring grid missed the edge between its spokes, and a flood over ground
/// samples ran straight over it (703 sampled cells, 91 of them actually on the
/// destination's surface).
///
/// WHY THE CACHE FILE. vnavmesh keeps no static handle on its NavmeshManager,
/// so the running instance cannot be reached by reflection. The cache file it
/// loads from is the same data, and <c>Navmesh.Navmesh.Deserialize</c> is a
/// public entry point. Everything here is best-effort: any failure leaves
/// <see cref="Ready"/> false and the auto-walk falls back on what it did before.
/// </summary>
public sealed class NavmeshCacheService : IDisposable
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    /// <summary>Directories vnavmesh may be installed in, dev build first.</summary>
    private static readonly string[] PluginDirs =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     @"XIVLauncher\devPlugins\vnavmesh"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     @"XIVLauncher\installedPlugins\vnavmesh"),
    };

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"XIVLauncher\pluginConfigs\vnavmesh\meshcache");

    private Assembly? _vnav, _detour, _core;
    private string? _pluginDir;
    private bool _loadFailed;

    /// <summary>Mesh currently held, and the territory it belongs to. One zone
    /// at a time: the file is megabytes, and a walk only ever asks about the
    /// zone the player is standing in.</summary>
    private object? _mesh;
    private object? _query;
    private ushort _meshTerritory;

    public NavmeshCacheService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log = log;
    }

    /// <summary>Whether the vnavmesh assemblies could be loaded at all.</summary>
    public bool Ready => !_loadFailed && EnsureAssemblies();

    // ── loading ──────────────────────────────────────────────────────────

    private bool EnsureAssemblies()
    {
        if (_vnav != null) return true;
        if (_loadFailed) return false;

        // try-catch: loading a foreign plugin's assemblies is best-effort by
        // design - a missing or updated vnavmesh must not take the mod down.
        try
        {
            _pluginDir = PluginDirs.FirstOrDefault(d => File.Exists(Path.Combine(d, "vnavmesh.dll")));
            if (_pluginDir == null)
            {
                _log.Info("[Netz] vnavmesh.dll in keinem der bekannten Verzeichnisse gefunden - " +
                          "die Flaechenanalyse bleibt aus.");
                _loadFailed = true;
                return false;
            }

            // The dependencies (DotRecast) sit next to it and are not on any
            // probing path of ours, so they are resolved by hand.
            AssemblyLoadContext.Default.Resolving += ResolveFromPluginDir;
            _vnav = Assembly.LoadFrom(Path.Combine(_pluginDir, "vnavmesh.dll"));
            _detour = Assembly.LoadFrom(Path.Combine(_pluginDir, "DotRecast.Detour.dll"));
            _core = Assembly.LoadFrom(Path.Combine(_pluginDir, "DotRecast.Core.dll"));
            _log.Info($"[Netz] vnavmesh-Assemblies geladen aus {_pluginDir}.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"[Netz] vnavmesh-Assemblies nicht ladbar ({ex.Message}) - " +
                         "die Flaechenanalyse bleibt aus.");
            _loadFailed = true;
            return false;
        }
    }

    private Assembly? ResolveFromPluginDir(AssemblyLoadContext ctx, AssemblyName name)
    {
        if (_pluginDir == null) return null;
        var probe = Path.Combine(_pluginDir, name.Name + ".dll");
        return File.Exists(probe) ? ctx.LoadFromAssemblyPath(probe) : null;
    }

    /// <summary>
    /// Loads the cache file for a territory. The file name is vnavmesh's own
    /// key: the TerritoryType sheet's Bg path with the slashes turned into
    /// underscores. Several files can share that prefix (one per layer setup),
    /// so the one whose mesh actually covers the player wins.
    /// </summary>
    private bool EnsureMesh(ushort territory, Vector3 near)
    {
        if (_mesh != null && _meshTerritory == territory) return true;
        if (!EnsureAssemblies()) return false;

        // try-catch: file may be absent, truncated, or written by a vnavmesh
        // whose format we no longer understand.
        try
        {
            if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var row))
            {
                _log.Info($"[Netz] Gebiet {territory} steht nicht im TerritoryType-Sheet.");
                return false;
            }
            var bg = row.Bg.ExtractText();
            if (string.IsNullOrEmpty(bg)) return false;
            var prefix = bg.Replace('/', '_');

            if (!Directory.Exists(CacheDir))
            {
                _log.Info($"[Netz] Kein Wegenetz-Zwischenspeicher unter {CacheDir}.");
                return false;
            }
            var files = Directory.GetFiles(CacheDir, prefix + "*.navmesh");
            if (files.Length == 0)
            {
                _log.Info($"[Netz] Fuer Gebiet {territory} ({prefix}) liegt kein Wegenetz im " +
                          "Zwischenspeicher - dort war der Spieler noch nie, seit vnavmesh laeuft.");
                return false;
            }

            object? best = null;
            var bestDist = float.MaxValue;
            foreach (var file in files)
            {
                var candidate = Deserialize(file);
                if (candidate == null) continue;
                var q = MakeQuery(candidate);
                var hit = Nearest(q, near, 5f, 5f);
                var dist = hit == null ? float.MaxValue : Vector3.Distance(hit.Value.Pos, near);
                _log.Info($"[Netz] {Path.GetFileName(file)}: Spieler {dist:F2} m vom Netz.");
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = candidate;
            }
            if (best == null || bestDist > 5f)
            {
                _log.Info($"[Netz] Keine der {files.Length} Dateien deckt die Spielerposition ab.");
                return false;
            }

            _mesh = best;
            _query = MakeQuery(best);
            _meshTerritory = territory;
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"[Netz] Wegenetz aus dem Zwischenspeicher nicht lesbar: {ex.Message}");
            return false;
        }
    }

    private object? Deserialize(string file)
    {
        using var fs = File.OpenRead(file);
        using var br = new BinaryReader(fs);
        // CustomizationVersion sits at file offset 8 and has to be handed back
        // exactly, otherwise Deserialize throws.
        fs.Seek(8, SeekOrigin.Begin);
        var custom = br.ReadInt32();
        fs.Seek(0, SeekOrigin.Begin);
        var method = _vnav!.GetType("Navmesh.Navmesh")!
            .GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static)!;
        var navmesh = method.Invoke(null, new object[] { br, custom });
        return navmesh?.GetType().GetProperty("Mesh")?.GetValue(navmesh);
    }

    // ── DotRecast plumbing ───────────────────────────────────────────────

    private object MakeQuery(object mesh)
        => Activator.CreateInstance(_detour!.GetType("DotRecast.Detour.DtNavMeshQuery")!, mesh)!;

    private object MakeFilter()
        => Activator.CreateInstance(_detour!.GetType("DotRecast.Detour.DtQueryDefaultFilter")!)!;

    private object Vec(Vector3 v)
        => Activator.CreateInstance(
            _core!.GetType("DotRecast.Core.Numerics.RcVec3f") ?? _core.GetType("DotRecast.Core.RcVec3f")!,
            v.X, v.Y, v.Z)!;

    private static Vector3 FromVec(object v)
    {
        var t = v.GetType();
        float G(string n)
        {
            var f = t.GetField(n);
            return f != null ? (float)f.GetValue(v)! : (float)t.GetProperty(n)!.GetValue(v)!;
        }
        return new Vector3(G("X"), G("Y"), G("Z"));
    }

    private (long Ref, Vector3 Pos)? Nearest(object query, Vector3 p, float xz, float y)
    {
        var m = query.GetType().GetMethod("FindNearestPoly")!;
        var args = new object?[] { Vec(p), Vec(new Vector3(xz, y, xz)), MakeFilter(), 0L, null, false };
        m.Invoke(query, args);
        var refs = (long)args[3]!;
        return refs == 0 ? null : (refs, FromVec(args[4]!));
    }

    /// <summary>Breadth-first over polygon links - the same mechanism vnavmesh
    /// uses in FindReachableMeshPolys, so "same surface" here means exactly
    /// what it means to the pathfinder.</summary>
    private HashSet<long> Flood(object mesh, long start, int limit)
    {
        var getTileAndPoly = mesh.GetType().GetMethod("GetTileAndPolyByRef")!;
        var seen = new HashSet<long> { start };
        var queue = new Queue<long>();
        queue.Enqueue(start);
        while (queue.Count > 0 && seen.Count < limit)
        {
            var cur = queue.Dequeue();
            var args = new object?[] { cur, null, null };
            getTileAndPoly.Invoke(mesh, args);
            if (args[1] is not { } tile || args[2] is not { } poly) continue;

            var polyIndex = (int)poly.GetType().GetField("index")!.GetValue(poly)!;
            var polyLinks = (int[])tile.GetType().GetField("polyLinks")!.GetValue(tile)!;
            var links = (System.Collections.IList)tile.GetType().GetField("links")!.GetValue(tile)!;

            var i = polyLinks[polyIndex];
            while (i >= 0 && i < links.Count)
            {
                var link = links[i]!;
                var lt = link.GetType();
                var refs = (long)lt.GetField("refs")!.GetValue(link)!;
                var next = (int)lt.GetField("next")!.GetValue(link)!;
                if (refs != 0 && seen.Add(refs)) queue.Enqueue(refs);
                if (next == i) break;
                i = next;
            }
        }
        return seen;
    }

    // ── the question the auto-walk actually asks ─────────────────────────

    /// <summary>Polygons a flood may visit before giving up. A zone surface runs
    /// to a few tens of thousands; this is well clear of that and still bounded
    /// should the link walk ever meet a cycle we did not foresee.</summary>
    private const int FloodLimit = 200_000;

    /// <summary>A crossing: step off <paramref name="From"/> onto
    /// <paramref name="To"/>, which lies on the destination's surface.</summary>
    public readonly record struct Crossing(Vector3 From, Vector3 To, float Gap, float Rise);

    /// <summary>
    /// Finds where the player's surface and the destination's come closest
    /// enough to step across. Returns null when the two are the same surface
    /// (then no crossing is needed), when the mesh is unavailable, or when
    /// nothing is within stepping distance.
    ///
    /// Runs off the game thread - it deserializes megabytes and walks tens of
    /// thousands of polygons.
    /// </summary>
    /// <param name="maxGap">How far apart the two sides may be horizontally.</param>
    /// <param name="maxDrop">How far the far side may lie below the near side.</param>
    /// <param name="maxClimb">How far it may lie above.</param>
    public Crossing? FindCrossing(ushort territory, Vector3 me, Vector3 goal,
                                  float maxGap, float maxDrop, float maxClimb)
    {
        if (!EnsureMesh(territory, me)) return null;
        var mesh = _mesh!;
        var query = _query!;

        var mine = Nearest(query, me, 5f, 5f);
        var theirs = Nearest(query, goal, 5f, 5f);
        if (mine == null || theirs == null)
        {
            _log.Info("[Netz] Spieler oder Ziel liegt nicht auf dem zwischengespeicherten Netz.");
            return null;
        }

        var mySurface = Flood(mesh, mine.Value.Ref, FloodLimit);
        if (mySurface.Contains(theirs.Value.Ref))
        {
            _log.Info($"[Netz] Ziel haengt an derselben Flaeche wie der Spieler " +
                      $"({mySurface.Count} Polygone) - hier fehlt kein Uebergang.");
            return null;
        }
        var goalSurface = Flood(mesh, theirs.Value.Ref, FloodLimit);
        _log.Info($"[Netz] Spielerflaeche {mySurface.Count} Polygone, Zielflaeche {goalSurface.Count} - " +
                  "getrennt. Suche die engste Stelle.");

        // Polygon centres are what the link walk gives us, and they are close
        // enough: a polygon is metres across, and the crossing is judged by the
        // gap between the two surfaces, not by a single exact point.
        var getCenter = mesh.GetType().GetMethod("GetPolyCenter")!;
        Vector3 Center(long r) => FromVec(getCenter.Invoke(mesh, new object[] { r })!);

        var goalPts = goalSurface.Select(Center).ToList();
        var minePts = mySurface.Select(Center).ToList();

        Crossing? best = null;
        var bestCost = float.MaxValue;
        foreach (var g in goalPts)
            foreach (var m in minePts)
            {
                var gap = MathF.Sqrt((g.X - m.X) * (g.X - m.X) + (g.Z - m.Z) * (g.Z - m.Z));
                if (gap > maxGap) continue;
                var rise = g.Y - m.Y;
                if (rise > maxClimb || rise < -maxDrop) continue;
                // Prefer a short gap, a step down over a step up, and a landing
                // spot near the destination - in that order.
                var cost = gap + (rise > 0 ? rise * 6f : -rise * 0.4f) + Vector3.Distance(g, goal) * 0.2f;
                if (cost >= bestCost) continue;
                if (!MidPointIsSafe(query, m, g, maxDrop)) continue;
                bestCost = cost;
                best = new Crossing(m, g, gap, rise);
            }

        // Nothing adjoins the destination's surface - but we may be the ones
        // stuck. Measured 2026-08-09 16:57: after crossing ONTO a 29-polygon
        // plateau the player could reach nothing at all (0 of 57 approach
        // candidates), because the way back is a 2 m climb and the character
        // cannot climb. A crossing that only works one way is a trap, exactly
        // as the gangway was, so look for a way OFF our own surface too.
        if (best == null && mySurface.Count <= StuckSurfaceLimit)
            best = FindWayOff(mesh, query, mySurface, minePts, goal, maxGap, maxDrop, maxClimb);

        if (best is { } b)
            _log.Info($"[Netz] Engste ueberquerbare Stelle: von <{b.From.X:F1}, {b.From.Y:F1}, {b.From.Z:F1}> " +
                      $"nach <{b.To.X:F1}, {b.To.Y:F1}, {b.To.Z:F1}> - {b.Gap:F1} m waagerecht, " +
                      $"{b.Rise:F1} m Hoehe, von dort {Vector3.Distance(b.To, goal):F1} m bis zum Ziel.");
        else
            _log.Info($"[Netz] Die beiden Flaechen kommen sich nirgends auf {maxGap:F0} m nahe genug " +
                      $"(erlaubt {maxDrop:F0} m hinunter, {maxClimb:F0} m hinauf).");
        return best;
    }

    /// <summary>
    /// Whether the ground halfway across a crossing is not a chasm. The two
    /// ends are known mesh points, but nothing says what lies between them -
    /// and the character is steered across blind. A mid-point far below both
    /// ends means the gap is not an edge to step over but a hole to fall into.
    ///
    /// A missing sample is NOT a rejection: mesh is routinely absent exactly at
    /// an edge - that is what makes it a crossing in the first place. Only
    /// ground that is there and lies too deep counts against it.
    /// </summary>
    private bool MidPointIsSafe(object query, Vector3 from, Vector3 to, float maxDrop)
    {
        var mid = Vector3.Lerp(from, to, 0.5f);
        var lower = MathF.Min(from.Y, to.Y);
        // Wide vertical box on purpose: we want to SEE a deep floor if there is
        // one, so the probe has to reach down past the allowed drop.
        var hit = Nearest(query, mid with { Y = lower }, 1.5f, maxDrop + 4f);
        if (hit == null) return true;
        var below = lower - hit.Value.Pos.Y;
        if (below <= maxDrop) return true;
        _log.Info($"[Netz] Uebergang verworfen: auf halber Strecke liegt der Boden {below:F1} m " +
                  $"unter beiden Seiten - das ist kein Absatz, sondern ein Loch.");
        return false;
    }

    /// <summary>Above this many polygons a surface is the zone's main area, not
    /// a ledge someone is stranded on - and probing every one of its polygons
    /// for a way off would cost far more than it could ever return.</summary>
    private const int StuckSurfaceLimit = 600;

    /// <summary>How many candidate surfaces get a flood of their own. Each one
    /// costs a full link walk, and they are tried nearest-to-destination
    /// first.</summary>
    private const int WayOffCandidates = 6;

    /// <summary>
    /// Finds a step off the surface we are standing on, for when the
    /// destination's surface adjoins nothing of ours. Prefers a surface that
    /// contains the destination; failing that, the largest one found - which
    /// is the zone's main area and puts the ordinary pathfinder back in play.
    ///
    /// Probes outward from our own polygons and decides membership by polygon
    /// reference, not by height or distance: that is the whole advantage of
    /// reading the mesh directly, and it is what the discarded IPC probing
    /// could never do.
    /// </summary>
    private Crossing? FindWayOff(object mesh, object query, HashSet<long> mySurface,
                                 List<Vector3> minePts, Vector3 goal,
                                 float maxGap, float maxDrop, float maxClimb)
    {
        _log.Info($"[Netz] Kein Uebergang zur Zielflaeche, und die eigene Flaeche ist mit " +
                  $"{mySurface.Count} Polygonen klein - suche einen Ausweg von hier.");

        var found = new List<(long Ref, Vector3 To, Vector3 From, float Gap, float Rise)>();
        foreach (var m in minePts)
            for (var b = 0; b < 8; b++)
            {
                var angle = b * 2f * MathF.PI / 8f;
                foreach (var outDist in new[] { 1.5f, 2.5f, 3.5f })
                {
                    var probe = new Vector3(m.X + MathF.Sin(angle) * outDist, m.Y,
                                            m.Z + MathF.Cos(angle) * outDist);
                    foreach (var level in new[] { 0f, -2f, -4f })
                    {
                        var hit = Nearest(query, probe with { Y = m.Y + level }, 1f, 1.5f);
                        if (hit == null || mySurface.Contains(hit.Value.Ref)) continue;
                        var p = hit.Value.Pos;
                        var gap = MathF.Sqrt((p.X - m.X) * (p.X - m.X) + (p.Z - m.Z) * (p.Z - m.Z));
                        if (gap > maxGap) continue;
                        var rise = p.Y - m.Y;
                        if (rise > maxClimb || rise < -maxDrop) continue;
                        found.Add((hit.Value.Ref, p, m, gap, rise));
                    }
                }
            }

        if (found.Count == 0)
        {
            _log.Info("[Netz] Von dieser Flaeche fuehrt kein Schritt auf eine andere - " +
                      "weder hinunter noch hinueber.");
            return null;
        }

        // Nearest the destination first: if one of them carries the goal we
        // want to find it before spending floods on the rest.
        var ranked = found.OrderBy(f => Vector3.Distance(f.To, goal)).ToList();
        Crossing? fallback = null;
        var fallbackSize = 0;
        // Polygons of every surface already walked. Candidates land on the same
        // few surfaces over and over, and one flood per SURFACE is the point -
        // per candidate point it would be dozens of full link walks.
        var alreadyWalked = new HashSet<long>();
        var floods = 0;

        foreach (var cand in ranked)
        {
            if (floods >= WayOffCandidates) break;
            if (alreadyWalked.Contains(cand.Ref)) continue;
            floods++;

            var surface = Flood(mesh, cand.Ref, FloodLimit);
            alreadyWalked.UnionWith(surface);
            var goalHere = Nearest(query, goal, 5f, 5f);
            var carriesGoal = goalHere != null && surface.Contains(goalHere.Value.Ref);
            _log.Info($"[Netz] Ausweg-Kandidat <{cand.To.X:F1}, {cand.To.Y:F1}, {cand.To.Z:F1}>: " +
                      $"Flaeche mit {surface.Count} Polygonen, enthaelt das Ziel: {(carriesGoal ? "ja" : "nein")}, " +
                      $"{cand.Gap:F1} m waagerecht, {cand.Rise:F1} m Hoehe.");

            if (carriesGoal)
                return new Crossing(cand.From, cand.To, cand.Gap, cand.Rise);
            // Otherwise remember the biggest: that is the zone's main area, and
            // standing on it means the normal walk works again.
            if (surface.Count <= fallbackSize) continue;
            fallbackSize = surface.Count;
            fallback = new Crossing(cand.From, cand.To, cand.Gap, cand.Rise);
        }

        if (fallback != null)
            _log.Info($"[Netz] Kein Ausweg direkt zum Ziel - nehme die groesste Nachbarflaeche " +
                      $"({fallbackSize} Polygone), von dort traegt die normale Wegsuche wieder.");
        return fallback;
    }

    public void Dispose()
    {
        _mesh = null;
        _query = null;
        if (_pluginDir != null) AssemblyLoadContext.Default.Resolving -= ResolveFromPluginDir;
    }
}
