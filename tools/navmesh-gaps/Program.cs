// navmeshgaps - where does the walkable mesh fall apart, and where is it worth mending?
//
// Runs offline against the cache files vnavmesh has already built, so a blind
// player does not have to find a gap by walking into it. That was the whole point
// of the tool: "ich kann das nicht selber ablaufen weil ich den weg nicht weiss".
//
// WHY GAPS EXIST AT ALL, read off vnavmesh's own build code:
// NavmeshBuilder.cs:74 derives walkableRadius = ceil(AgentRadius / CellSize) = 2
// cells, and RcAreas.ErodeWalkableArea (line 235) shaves that off EVERY edge. A
// gangplank narrower than a metre is gone afterwards. The two repair mechanisms do
// not catch it either: EDGE_CLIMB_DOWN spans -3.2..-1.5 m and EDGE_JUMP -500..-1.5 m,
// so both need at least 1.5 m of drop, and both are off by default.
//
// vnavmesh has a name for the remedy. Navmesh.AreaId.Shortcut is documented as
// "walking through a gap that recast thinks is too narrow" - which is exactly the
// case this tool looks for. Fixes get written by hand as LinkPoints in
// Customizations/Z<id><Name>.cs; the Limsa gangplank is one, and the coordinates
// in it match what we measured on 2026-08-06 to within 15 cm.
//
// WHAT THIS TOOL DOES NOT DO: it does not decide. It reports candidates, and each
// one still has to be looked at - two surfaces half a metre apart are sometimes a
// gangplank and sometimes a railing nobody should climb. Auto-generating links
// from geometry alone is what got the player locked onto a plateau in V5.78.

using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using Navmesh;

// vnavmesh.dll is referenced but deliberately NOT copied next to this tool: it
// must be the very build that wrote these cache files, and a stale copy would be
// the kind of silent mismatch that costs an afternoon. Resolve it from the
// installation instead. Registered first so it is in place before any type from
// that assembly is touched.
var vnavDir = Environment.GetEnvironmentVariable("VNAV_DIR")
              ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                              "XIVLauncher", "devPlugins", "vnavmesh");

// Dalamud's own directory too: Lumina drags in Microsoft.Extensions.ObjectPool,
// which lives there rather than beside vnavmesh.
var dalamudDir = Environment.GetEnvironmentVariable("DALAMUD_HOME")
                 ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                 "XIVLauncher", "addon", "Hooks", "dev");

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var dir in new[] { vnavDir, dalamudDir })
    {
        var candidate = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
    }
    return null;
};

var cacheDir = Environment.GetEnvironmentVariable("VNAV_CACHE")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                               "XIVLauncher", "pluginConfigs", "vnavmesh", "meshcache");

// Defaults describe the erosion case, not real cliffs: a bit over two cell widths
// horizontally, and less than the agent's climb height vertically.
var maxGap = 1.5f;
var maxDrop = 1.0f;
// Erosion leaves a lot of one- and two-polygon confetti behind - the top of a
// crate, a ledge, a step. Limsa alone splits into 352 surfaces, of which only a
// handful are places anyone would want to reach. Ten polygons is roughly a square
// where a character fits; the ship deck that started all this has 129.
var minComponent = 10;
string filter = null;

// Point mode: replays exactly what the plugin asks vnavmesh before it starts a
// walk (AutoWalkService.ProbeReachable -> Query.Mesh.NearestPointReachable with a
// tight radius), so a destination can be judged BEFORE going in-game.
Vector3? from = null;
var probes = new List<Vector3>();
var probeRadiusXZ = 2.0f;   // same values ZoneBorderService passes
var probeRadiusY = 5.0f;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--gap" when i + 1 < args.Length:   maxGap = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--drop" when i + 1 < args.Length:  maxDrop = float.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--min" when i + 1 < args.Length:   minComponent = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--from" when i + 1 < args.Length:  from = ParseVec(args[++i]); break;
        case "--at" when i + 1 < args.Length:    probes.Add(ParseVec(args[++i])); break;
        case "--radius" when i + 1 < args.Length:
        {
            var parts = args[++i].Split(',');
            probeRadiusXZ = float.Parse(parts[0], CultureInfo.InvariantCulture);
            if (parts.Length > 1) probeRadiusY = float.Parse(parts[1], CultureInfo.InvariantCulture);
            break;
        }
        case "-h" or "--help":                   Usage(); return 0;
        default:                                 filter = args[i]; break;
    }
}

void Usage()
{
    Console.WriteLine("navmeshgaps [zone-filter] [--gap <m>] [--drop <m>] [--min <polys>]");
    Console.WriteLine();
    Console.WriteLine("  Splits each cached navmesh into connected surfaces and reports the");
    Console.WriteLine("  narrowest crossing between them - the places a LinkPoints line could mend.");
    Console.WriteLine();
    Console.WriteLine("  zone-filter   substring of the cache file name, e.g. s1t2 for Limsa");
    Console.WriteLine($"  --gap <m>     widest horizontal gap to report (default {maxGap:F1})");
    Console.WriteLine($"  --drop <m>    largest height difference to report (default {maxDrop:F1})");
    Console.WriteLine($"  --min <n>     ignore surfaces smaller than this many polygons (default {minComponent})");
    Console.WriteLine();
    Console.WriteLine("  Point mode - can this spot be walked to, before testing in-game:");
    Console.WriteLine("  --from x,y,z  the character's position (defines what 'reachable' means)");
    Console.WriteLine("  --at x,y,z    a destination to judge; repeatable, judged in order");
    Console.WriteLine($"  --radius xz[,y]  search box around each point (default {probeRadiusXZ:F1},{probeRadiusY:F1}");
    Console.WriteLine("                   - the values the plugin itself passes)");
    Console.WriteLine();
    Console.WriteLine($"  cache:    {cacheDir}  (override with VNAV_CACHE)");
    Console.WriteLine($"  vnavmesh: {vnavDir}  (override with VNAV_DIR)");
}

if (!Directory.Exists(cacheDir))
{
    Console.Error.WriteLine($"Cache directory not found: {cacheDir}");
    return 1;
}

var files = Directory.GetFiles(cacheDir, "*.navmesh")
    .Where(f => filter == null || Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No cache files{(filter != null ? $" matching '{filter}'" : "")} in {cacheDir}");
    return 1;
}

Console.WriteLine($"{files.Count} mesh(es), gap <= {maxGap:F1} m, drop <= {maxDrop:F1} m, surfaces >= {minComponent} polys");

// Cache names start with the zone's Bg path, slashes replaced by underscores
// (NavmeshManager.GetCacheKey). That mapping is not reversible - the path contains
// underscores of its own - so build it forwards from the sheet instead.
var byBg = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
var zoneNames = new Dictionary<uint, string>();
try
{
    var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK")
                 ?? @"K:\SteamLibrary\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";
    var game = new Lumina.GameData(sqpack, new Lumina.LuminaOptions { PanicOnSheetChecksumMismatch = false });
    foreach (var row in game.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>())
    {
        var bg = row.Bg.ExtractText();
        if (string.IsNullOrEmpty(bg)) continue;
        // First territory wins: several share one Bg (a zone and its instanced
        // twin), and it is the customization we are after, not the row.
        if (byBg.TryAdd(bg.Replace('/', '_'), row.RowId))
            zoneNames[row.RowId] = row.PlaceName.ValueNullable?.Name.ExtractText() ?? "?";
    }
    Console.WriteLine($"{byBg.Count} zone layouts known from the sheet.");
}
catch (Exception ex)
{
    Console.WriteLine($"WARNING: no sqpack ({ex.Message}) - zone customizations cannot be applied,");
    Console.WriteLine("         so already-linked crossings will show up as gaps.");
}

uint? TerritoryOf(string cacheName)
{
    var head = cacheName.Split("__")[0];
    return byBg.TryGetValue(head, out var id) ? id : null;
}

Console.WriteLine();

var totalCandidates = 0;
// Thousands of candidates across 60-odd zones are unreadable in file order, and a
// blind reader cannot skim for the big numbers. The ranking at the end is the part
// worth acting on.
var ranking = new List<(int Gained, string Zone, float Gap, float Drop, Vector3 A, Vector3 B)>();

foreach (var file in files)
{
    var name = Path.GetFileNameWithoutExtension(file);
    DtNavMesh mesh;
    int customizationVersion;
    string applied;
    try
    {
        (mesh, customizationVersion, applied) = Load(file, TerritoryOf);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--- {name}: not readable ({ex.Message})");
        continue;
    }

    if (probes.Count > 0)
    {
        var zoneName = TerritoryOf(name) is uint pid && zoneNames.TryGetValue(pid, out var pn) ? pn : "?";
        Console.WriteLine($"--- {zoneName}  [{name}]");
        Console.WriteLine($"    cache built at customization v{customizationVersion}; {applied}.");
        JudgePoints(mesh, from, probes, probeRadiusXZ, probeRadiusY);
        Console.WriteLine();
        continue;
    }

    var surfaces = SplitIntoSurfaces(mesh, out var polyCount);
    if (surfaces.Count <= 1)
    {
        Console.WriteLine($"--- {name}: {polyCount} polys in one piece - nothing to mend.");
        continue;
    }

    var big = surfaces.Where(s => s.Count >= minComponent).ToList();
    var zone = TerritoryOf(name) is uint zid && zoneNames.TryGetValue(zid, out var zn) ? zn : "?";
    Console.WriteLine($"--- {zone}  [{name}]");
    Console.WriteLine($"    {polyCount} polys, {surfaces.Count} separate surfaces, " +
                      $"{big.Count} of them >= {minComponent} polys.");
    Console.WriteLine($"    cache built at customization v{customizationVersion}; {applied}.");

    var found = Report(mesh, big, maxGap, maxDrop, zone, ranking);
    totalCandidates += found;
    if (found == 0)
        Console.WriteLine($"    no crossing within {maxGap:F1} m / {maxDrop:F1} m - these are real drops, not eroded edges.");
    Console.WriteLine();
}

Console.WriteLine($"{totalCandidates} candidate crossing(s) in total.");

if (ranking.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("=== BIGGEST GAINS ACROSS ALL ZONES ===");
    Console.WriteLine();
    foreach (var (gained, zone, gap, drop, a, b) in ranking.OrderByDescending(e => e.Gained).Take(25))
    {
        Console.WriteLine($"{gained,7} polys  {zone,-32}  gap {gap:F2} m, drop {drop:F2} m");
        Console.WriteLine($"                ({a.X:F1}|{a.Y:F1}|{a.Z:F1}) <-> ({b.X:F1}|{b.Y:F1}|{b.Z:F1})");
    }

    Console.WriteLine();
    Console.WriteLine("Each line is a PROPOSAL, not a verdict. Check what actually stands there");
    Console.WriteLine("(tools/zone-probe) before putting it into a NavmeshCustomization, and bump");
    Console.WriteLine("that customization's Version so existing caches get rebuilt.");
}
return 0;

// The cache stores its customization version in the header, and Deserialize
// insists on being told the same number. Read it first, then rewind - that way any
// zone can be opened, not only ones whose customization we happen to know.
static (DtNavMesh Mesh, int CustomizationVersion, string Applied) Load(string path, Func<string, uint?> territoryOf)
{
    Navmesh.Navmesh navmesh;
    int customizationVersion;
    using (var stream = File.OpenRead(path))
    using (var reader = new BinaryReader(stream))
    {
        reader.ReadUInt32();                            // magic, checked by Deserialize
        reader.ReadUInt32();                            // format version, likewise
        customizationVersion = reader.ReadInt32();
        stream.Position = 0;
        // Feeding back the version we just read bypasses the mismatch check on
        // purpose - the point is to open ANY cache, including a stale one. Which
        // is why the number gets reported: a zone whose customization has moved on
        // shows gaps here that the game itself no longer has.
        navmesh = Navmesh.Navmesh.Deserialize(reader, customizationVersion);
    }

    // THE CACHE DOES NOT CONTAIN THE HAND-WRITTEN LINKS. NavmeshManager writes the
    // file first and calls CustomizeMesh afterwards (lines 314-316), and calls it
    // again after loading (289-290) - so the links only ever exist at runtime.
    // Without replaying them here, the tool would cheerfully propose the Limsa
    // gangplank that has been linked since August. Same call the plugin makes.
    var name = Path.GetFileNameWithoutExtension(path);
    var territory = territoryOf(name);
    var applied = "no customization";

    if (territory is uint id)
    {
        var type = typeof(NavmeshCustomization).Assembly.DefinedTypes.FirstOrDefault(
            t => t.IsSubclassOf(typeof(NavmeshCustomization)) &&
                 t.GetCustomAttributes<CustomizationTerritoryAttribute>().Any(a => a.TerritoryID == id));

        if (type != null && Activator.CreateInstance(type) is NavmeshCustomization customization)
        {
            customization.CustomizeMesh(navmesh, FestivalLayers(name));
            applied = $"territory {id}, {type.Name} v{customization.Version} applied";
        }
        else
        {
            applied = $"territory {id}, none";
        }
    }

    return (navmesh.Mesh, customizationVersion, applied);
}

// Third field of the cache key is the festival layer list, hex and dot-separated
// (NavmeshManager.GetCacheKey). Empty for most zones.
static List<uint> FestivalLayers(string cacheName)
{
    var parts = cacheName.Split("__");
    var result = new List<uint>();
    if (parts.Length < 3 || parts[2].Length == 0) return result;
    foreach (var piece in parts[2].Split('.', StringSplitOptions.RemoveEmptyEntries))
        if (uint.TryParse(piece, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            result.Add(v);
    return result;
}

// Flood fill over the same polygon links the pathfinder walks (mirrors
// NavmeshQuery.FindReachableMeshPolys), so "separate" here means exactly what it
// means to a path request: no route, at any length.
static List<List<long>> SplitIntoSurfaces(DtNavMesh mesh, out int polyCount)
{
    var all = new List<long>();
    for (var t = 0; t < mesh.GetMaxTiles(); t++)
    {
        var tile = mesh.GetTile(t);
        if (tile?.data?.header == null) continue;
        var baseRef = mesh.GetPolyRefBase(tile);
        for (var p = 0; p < tile.data.header.polyCount; p++)
            all.Add(baseRef | (long)p);
    }

    polyCount = all.Count;
    var seen = new HashSet<long>();
    var surfaces = new List<List<long>>();

    foreach (var start in all)
    {
        if (seen.Contains(start)) continue;

        var surface = new List<long>();
        var queue = new Stack<long>();
        queue.Push(start);

        while (queue.Count > 0)
        {
            var next = queue.Pop();
            if (!seen.Add(next)) continue;
            surface.Add(next);

            mesh.GetTileAndPolyByRefUnsafe(next, out var tile, out var poly);
            for (var i = tile.polyLinks[poly.index]; i != DtNavMesh.DT_NULL_LINK; i = tile.links[i].next)
            {
                var neighbour = tile.links[i].refs;
                if (neighbour != 0) queue.Push(neighbour);
            }
        }

        surfaces.Add(surface);
    }

    surfaces.Sort((a, b) => b.Count.CompareTo(a.Count));
    return surfaces;
}

static int Report(DtNavMesh mesh, List<List<long>> surfaces, float maxGap, float maxDrop,
                  string zone, List<(int, string, float, float, Vector3, Vector3)> ranking)
{
    // Only border vertices can face another surface: an edge with a neighbour on
    // this side is interior and can never be the narrow spot. On a town mesh this
    // throws away the large majority of the points before the search starts.
    var borders = surfaces.Select(s => BorderVertices(mesh, s)).ToList();

    // Bucket by a grid of the search radius, so each point only looks at the nine
    // cells around it instead of every point of every other surface.
    var cell = MathF.Max(maxGap, 0.5f);
    var grid = new Dictionary<(int, int, int), List<(int Surface, Vector3 P)>>();
    for (var s = 0; s < borders.Count; s++)
        foreach (var v in borders[s])
        {
            var key = Key(v, cell);
            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new();
            list.Add((s, v));
        }

    // Best crossing per surface PAIR - one line per connection is what a
    // customization needs, not every vertex that happens to be close.
    var best = new Dictionary<(int, int), (float Gap, Vector3 A, Vector3 B)>();

    for (var s = 0; s < borders.Count; s++)
        foreach (var a in borders[s])
        {
            var (kx, ky, kz) = Key(a, cell);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            for (var dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue((kx + dx, ky + dy, kz + dz), out var list)) continue;
                foreach (var (other, b) in list)
                {
                    if (other <= s) continue;   // each pair once, and never with itself
                    var flat = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                    var drop = MathF.Abs(a.Y - b.Y);
                    if (flat > maxGap || drop > maxDrop) continue;

                    var pair = (s, other);
                    if (best.TryGetValue(pair, out var cur) && cur.Gap <= flat) continue;
                    best[pair] = (flat, a, b);
                }
            }
        }

    // Ranked by how much ground a link would OPEN UP - the smaller of the two
    // surfaces - not by how narrow the gap is. A 30 cm seam between two ledges is
    // tidy and worthless; the one worth writing down is the one that reaches a
    // deck, a courtyard, an upper floor.
    foreach (var (pair, hit) in best.OrderByDescending(e => Math.Min(surfaces[e.Key.Item1].Count, surfaces[e.Key.Item2].Count))
                                    .ThenBy(e => e.Value.Gap))
    {
        var (a, b) = (hit.A, hit.B);
        var gained = Math.Min(surfaces[pair.Item1].Count, surfaces[pair.Item2].Count);
        ranking.Add((gained, zone, hit.Gap, MathF.Abs(a.Y - b.Y), a, b));
        Console.WriteLine($"    opens up {gained} polys: surface {pair.Item1} ({surfaces[pair.Item1].Count}) <-> " +
                          $"surface {pair.Item2} ({surfaces[pair.Item2].Count}), " +
                          $"gap {hit.Gap:F2} m, drop {MathF.Abs(a.Y - b.Y):F2} m");
        Console.WriteLine($"        LinkPoints(mesh, new({F(a.X)}, {F(a.Y)}, {F(a.Z)}), " +
                          $"new({F(b.X)}, {F(b.Y)}, {F(b.Z)}), Navmesh.AreaId.Shortcut);");
        Console.WriteLine($"        LinkPoints(mesh, new({F(b.X)}, {F(b.Y)}, {F(b.Z)}), " +
                          $"new({F(a.X)}, {F(a.Y)}, {F(a.Z)}), Navmesh.AreaId.Shortcut);");
    }

    return best.Count;
}

static Vector3 ParseVec(string s)
{
    var p = s.Split(',');
    if (p.Length != 3) throw new ArgumentException($"expected x,y,z but got '{s}'");
    return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture),
                       float.Parse(p[1], CultureInfo.InvariantCulture),
                       float.Parse(p[2], CultureInfo.InvariantCulture));
}

// Mirrors NavmeshQuery.FindNearestPointOnMesh(p, xz, y, allowUnreachable: false):
// find the nearest polygon inside the search box, then ask whether it hangs
// together with where the character stands. vnavmesh does the second half with a
// FloodFillAwareFilter; here it is the surface split, which answers the same
// question - is there any route at all.
static void JudgePoints(DtNavMesh mesh, Vector3? from, List<Vector3> probes, float radiusXZ, float radiusY)
{
    var query = new DtNavMeshQuery(mesh);
    var filter = new DtQueryDefaultFilter();
    var extents = new RcVec3f(radiusXZ, radiusY, radiusXZ);

    long NearestPoly(Vector3 p, out Vector3 onMesh)
    {
        query.FindNearestPoly(new RcVec3f(p.X, p.Y, p.Z), extents, filter, out var reference, out var pt, out _);
        onMesh = new Vector3(pt.X, pt.Y, pt.Z);
        return reference;
    }

    HashSet<long> reachable = null;
    List<Vector3> reachableBorder = null;
    if (from is Vector3 origin)
    {
        var startRef = NearestPoly(origin, out var startOn);
        if (startRef == 0)
        {
            Console.WriteLine($"    FROM ({origin.X:F1}|{origin.Y:F1}|{origin.Z:F1}): no mesh within the search box - " +
                              "reachability cannot be judged, only 'is there floor'.");
        }
        else
        {
            reachable = Flood(mesh, startRef);
            Console.WriteLine($"    FROM ({origin.X:F1}|{origin.Y:F1}|{origin.Z:F1}) -> " +
                              $"({startOn.X:F1}|{startOn.Y:F1}|{startOn.Z:F1}), " +
                              $"connected surface has {reachable.Count} polys.");
        }
    }

    for (var i = 0; i < probes.Count; i++)
    {
        var p = probes[i];
        var reference = NearestPoly(p, out var on);
        if (reference == 0)
        {
            Console.WriteLine($"    #{i + 1} ({p.X:F1}|{p.Y:F1}|{p.Z:F1}): REJECTED - no mesh within " +
                              $"{radiusXZ:F1} m / {radiusY:F1} m.");
            continue;
        }

        var shift = Vector3.Distance(p, on);
        var verdict = reachable == null ? "mesh here" : reachable.Contains(reference) ? "ACCEPTED" : "REJECTED - mesh here, but no route from FROM";
        Console.WriteLine($"    #{i + 1} ({p.X:F1}|{p.Y:F1}|{p.Z:F1}): {verdict}, " +
                          $"snaps to ({on.X:F1}|{on.Y:F1}|{on.Z:F1}), {shift:F2} m away.");

        // Cut off from the character: describe the piece of ground the destination
        // sits on, and where it comes closest to the ground the character stands
        // on. That closest pair is the only place a way up could be - everywhere
        // else the two surfaces are further apart still.
        if (reachable != null && !reachable.Contains(reference))
        {
            reachableBorder ??= BorderVertices(mesh, reachable.ToList());
            DescribeIsland(mesh, reference, reachableBorder);
        }
    }
}

/// <summary>
/// Reports the detached surface a rejected point belongs to: how big it is, how far
/// it stretches, and the narrowest place between it and the character's own ground.
/// No gap or drop limit here - the answer "the nearest way over is 40 m away and
/// 12 m up" is worth just as much as a 1,3 m seam, because it says there is none.
/// </summary>
static void DescribeIsland(DtNavMesh mesh, long reference, List<Vector3> reachableBorder)
{
    var island = Flood(mesh, reference).ToList();
    var border = BorderVertices(mesh, island);
    if (border.Count == 0) return;

    var min = border[0];
    var max = border[0];
    foreach (var v in border)
    {
        min = Vector3.Min(min, v);
        max = Vector3.Max(max, v);
    }

    Console.WriteLine($"        detached surface: {island.Count} polys, " +
                      $"spans {max.X - min.X:F1} x {max.Z - min.Z:F1} m, " +
                      $"height {min.Y:F1} to {max.Y:F1} m.");

    // Two different questions, so two different winners. The closest pair says how
    // near the two surfaces ever come; the flattest pair says whether a crossing
    // could be walked BACK. A step of 3 m is a one-way drop - driving a player down
    // it strands them, which is exactly what the gangplank taught us.
    var bestGap = float.MaxValue;
    var bestFrom = Vector3.Zero;
    var bestTo = Vector3.Zero;
    var flatRise = float.MaxValue;
    var flatFrom = Vector3.Zero;
    var flatTo = Vector3.Zero;

    foreach (var a in border)
        foreach (var b in reachableBorder)
        {
            var gap = Vector3.Distance(a, b);
            if (gap < bestGap)
            {
                bestGap = gap;
                bestFrom = b;
                bestTo = a;
            }

            // Only pairs that are within reach at all can be a crossing - a pair
            // level with each other but 80 m apart says nothing.
            var across = MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
            if (across > 3f) continue;
            var rise = MathF.Abs(a.Y - b.Y);
            if (rise >= flatRise) continue;
            flatRise = rise;
            flatFrom = b;
            flatTo = a;
        }

    if (bestGap == float.MaxValue) return;

    Console.WriteLine($"        closest approach: {Pair(bestFrom, bestTo)}");
    if (flatRise < float.MaxValue && flatFrom != bestFrom)
        Console.WriteLine($"        flattest within 3 m: {Pair(flatFrom, flatTo)}");
    else if (flatRise == float.MaxValue)
        Console.WriteLine("        no pair within 3 m across - nothing to step over anywhere.");
}

static string Pair(Vector3 from, Vector3 to)
{
    var across = MathF.Sqrt((from.X - to.X) * (from.X - to.X) + (from.Z - to.Z) * (from.Z - to.Z));
    return $"({from.X:F1}|{from.Y:F1}|{from.Z:F1}) -> ({to.X:F1}|{to.Y:F1}|{to.Z:F1}), " +
           $"{across:F2} m across, {to.Y - from.Y:+0.00;-0.00;0.00} m up";
}

static HashSet<long> Flood(DtNavMesh mesh, long start)
{
    var seen = new HashSet<long>();
    var queue = new Stack<long>();
    queue.Push(start);
    while (queue.Count > 0)
    {
        var next = queue.Pop();
        if (!seen.Add(next)) continue;
        mesh.GetTileAndPolyByRefUnsafe(next, out var tile, out var poly);
        for (var i = tile.polyLinks[poly.index]; i != DtNavMesh.DT_NULL_LINK; i = tile.links[i].next)
            if (tile.links[i].refs != 0) queue.Push(tile.links[i].refs);
    }
    return seen;
}

static string F(float v) => v.ToString("F5", CultureInfo.InvariantCulture) + "f";

static (int, int, int) Key(Vector3 v, float cell)
    => ((int)MathF.Floor(v.X / cell), (int)MathF.Floor(v.Y / cell), (int)MathF.Floor(v.Z / cell));

static List<Vector3> BorderVertices(DtNavMesh mesh, List<long> surface)
{
    var result = new List<Vector3>();
    var seen = new HashSet<(int, int, int)>();

    foreach (var reference in surface)
    {
        mesh.GetTileAndPolyByRefUnsafe(reference, out var tile, out var poly);
        for (var k = 0; k < poly.vertCount; k++)
        {
            // neis[k] == 0 means this edge has no neighbour inside the tile and no
            // external link either - a genuine border of the walkable surface.
            if (poly.neis[k] != 0) continue;

            foreach (var idx in new[] { poly.verts[k], poly.verts[(k + 1) % poly.vertCount] })
            {
                var v = new Vector3(tile.data.verts[idx * 3], tile.data.verts[idx * 3 + 1], tile.data.verts[idx * 3 + 2]);
                // Round to centimetres before de-duplicating: neighbouring polygons
                // repeat the same corner, and the raw floats differ in the last bit.
                var key = ((int)MathF.Round(v.X * 100), (int)MathF.Round(v.Y * 100), (int)MathF.Round(v.Z * 100));
                if (seen.Add(key)) result.Add(v);
            }
        }
    }

    return result;
}
