using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var vnavDir = Path.Combine(appData, "XIVLauncher/devPlugins/vnavmesh");
var dalamudDir = Path.Combine(appData, "XIVLauncher/addon/Hooks/dev");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (var dir in new[] { vnavDir, dalamudDir })
    {
        var path = Path.Combine(dir, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
    }
    return null;
};
if (args.Length != 2) throw new ArgumentException("NavRouteCheck <game/sqpack> <report.json>");
Run(args[0], args[1], vnavDir, Path.Combine(appData, "XIVLauncher/pluginConfigs/vnavmesh/meshcache"));

static void Run(string sqpack, string output, string vnavDir, string cacheDir)
{
    using var game = new GameData(sqpack);
    var data = new GameDataReader(game);
    var log = DispatchProxy.Create<IPluginLog, QuietLog>();
    var assembly = Assembly.LoadFrom(Path.Combine(vnavDir, "vnavmesh.dll"));
    var service = assembly.GetType("Navmesh.Service")!;
    service.GetProperty("Log", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(null, log);
    var field = service.GetField("Log", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (field != null) field.SetValue(null, log);
    var queryType = assembly.GetType("Navmesh.NavmeshQuery")!;
    var pathMethod = queryType.GetMethods().Single(m => m.Name == "PathfindMesh");
    var nearestMethod = queryType.GetMethods().Single(m => m.Name == "FindNearestPointOnMesh");
    var travel = new TravelLayout(data, log);
    var entrances = new InteriorEntranceService(data, log);
    var checks = 0;
    var recorded = new List<object>();
    var questApproaches = new List<object>();
    var coverage = new List<object>();
    void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    var territory = data.GetExcelSheet<TerritoryType>().GetRow(130);
    var prefix = territory.Bg.ExtractText().Replace('/', '_');
    var caches = Directory.GetFiles(cacheDir, prefix + "*.navmesh");
    Check(caches.Length > 0, "Ul'dah cache missing; recorded path cannot be checked.");
    foreach (var cache in caches)
    {
        using var stream = File.OpenRead(cache); using var reader = new BinaryReader(stream);
        reader.ReadUInt32(); reader.ReadUInt32(); var version = reader.ReadInt32(); stream.Position = 0;
        var mesh = Navmesh.Navmesh.Deserialize(reader, version);
        var customType = assembly.DefinedTypes.FirstOrDefault(t => t.IsSubclassOf(typeof(Navmesh.NavmeshCustomization))
            && t.GetCustomAttributes<Navmesh.CustomizationTerritoryAttribute>().Any(a => a.TerritoryID == 130));
        if (customType != null && Activator.CreateInstance(customType) is Navmesh.NavmeshCustomization custom)
        {
            var parts = Path.GetFileNameWithoutExtension(cache).Split("__");
            var festivals = parts.Length < 3 ? [] : parts[2].Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => uint.Parse(p, System.Globalization.NumberStyles.HexNumber)).ToList();
            custom.CustomizeMesh(mesh, festivals);
        }
        var query = Activator.CreateInstance(queryType, mesh)!;
        Vector3? Probe(Vector3 p, float xz, float y) => (Vector3?)nearestMethod.Invoke(query, [p, xz, y, false]);
        List<Vector3> QueryPath(Vector3 from, Vector3 to, float range)
        {
            var parameters = pathMethod.GetParameters().Select(p => p.Name switch
            {
                "from" => (object)from, "to" => to, "useRaycast" => false, "useStringPulling" => true,
                "range" => range, "cancel" => CancellationToken.None, _ => null,
            }).ToArray();
            var result = (IEnumerable)pathMethod.Invoke(query, parameters)!;
            return result.Cast<object>().Select(p => p is Vector3 v ? v
                : (Vector3)(p.GetType().GetProperty("Position")?.GetValue(p) ?? p.GetType().GetField("Position")!.GetValue(p))!).ToList();
        }
        var from = new Vector3(-43.47116f, 83.99999f, -0.3066429f);
        var goal = new Vector3(-12.619263f, 82.99987f, 4.562378f);
        var direct = QueryPath(from, goal, 2.5f);
        Check(!NavigationPathCheck.Reaches(direct, goal, 2.5f), "Recorded disconnected airport destination was treated as reached.");
        var gate = entrances.LocalTransfers(70).Single(t => t.BaseId == 1004434 && t.WarpId == 131128);
        var choice = new NavigationPathCheck(from, goal, 2.5f, [new(goal), new(gate.Position, gate)],
            (f, t, r) => Task.FromResult(QueryPath(f, t, r)));
        for (var i = 0; i < 10 && !choice.Completed; i++) choice.Poll();
        Check(choice.Completed && choice.Result?.Transfer == gate,
            $"Airport must use the verified same-map attendant, cache {Path.GetFileName(cache)}.");
        Check(NavigationPathCheck.Reaches(QueryPath(from, gate.Position, 2.5f), gate.Position, 2.5f)
            && NavigationPathCheck.Reaches(QueryPath(gate.Arrival, goal, 2.5f), goal, 2.5f), "Either gate leg has no complete path.");
        var roadFrom = new Vector3(29.185736f, 6.999999f, -82.09829f);
        var targets = travel.WalkingBorders(13).Where(b => b.TargetMap == 14)
            .SelectMany(b => ZoneBorderService.Targets(b.Border, roadFrom, Probe)).ToArray();
        var road = new NavigationPathCheck(roadFrom, targets.First(), 5.2f,
            targets.Select(t => new NavigationPathCheck.Choice(t)), (f, t, r) => Task.FromResult(QueryPath(f, t, r)));
        for (var i = 0; i < 100 && !road.Completed; i++) road.Poll();
        Check(road.Result != null, "No bounded walking approach to the real downstairs border.");
        Check(!travel.WalkingBorders(13).Any(b => b.TargetMap == 73), "Downstairs exit was assigned to the Hustings Strip.");
        recorded.Add(new { Cache = Path.GetFileName(cache), SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cache))),
            CustomizationVersion = version, DirectPath = direct, DirectGoal = goal, AirportGate = gate,
            GateSelected = choice.Result, WalkingBorder = road.Result, PathQueries = choice.Requests + road.Requests });
        Console.WriteLine($"PASS {Path.GetFileName(cache)}: incomplete airport path rejected, exact gate and both legs checked, actual downstairs border selected.");
    }
    // Scan every saved mesh, using the installed query and its own customization.
    // A disconnected diagnostic route can be intentional (layers/story/doors),
    // so record it; never turn an offline result into a new walking bridge.
    var identity = new TravelMapIdentity(data);
    var levels = data.GetExcelSheet<Level>().Where(l => l.Type is 3 or 8 or 45
        && l.Map.RowId != 0 && l.Territory.RowId != 0).ToArray();
    var territories = data.GetExcelSheet<TerritoryType>().Where(t => !string.IsNullOrEmpty(t.Bg.ExtractText())).ToArray();
    foreach (var cache in Directory.GetFiles(cacheDir, "*.navmesh"))
    {
        var prefixName = Path.GetFileName(cache).Split("__")[0];
        var matches = territories.Where(t => t.Bg.ExtractText().Replace('/', '_') == prefixName).ToArray();
        if (matches.Length == 0) { coverage.Add(new { Cache = Path.GetFileName(cache), Status = "No matching game territory" }); continue; }
        // For shared terrain, use the installed customization's territory when
        // unique; otherwise use the oldest base territory for diagnostics only.
        // Preserve all possible IDs: this does not identify the active layer.
        var customized = matches.Where(t => assembly.DefinedTypes.Any(c => c.IsSubclassOf(typeof(Navmesh.NavmeshCustomization))
            && c.GetCustomAttributes<Navmesh.CustomizationTerritoryAttribute>().Any(a => a.TerritoryID == t.RowId))).ToArray();
        if (customized.Length > 1) { coverage.Add(new { Cache = Path.GetFileName(cache), Status = "Ambiguous customization", Territories = matches.Select(t => t.RowId).ToArray() }); continue; }
        var id = customized.Length == 1 ? customized[0].RowId : matches.Min(t => t.RowId);
        using var stream = File.OpenRead(cache); using var reader = new BinaryReader(stream);
        reader.ReadUInt32(); reader.ReadUInt32(); var version = reader.ReadInt32(); stream.Position = 0;
        var mesh = Navmesh.Navmesh.Deserialize(reader, version);
        var customType = assembly.DefinedTypes.FirstOrDefault(t => t.IsSubclassOf(typeof(Navmesh.NavmeshCustomization))
            && t.GetCustomAttributes<Navmesh.CustomizationTerritoryAttribute>().Any(a => a.TerritoryID == id));
        if (customType != null && Activator.CreateInstance(customType) is Navmesh.NavmeshCustomization custom)
        {
            var parts = Path.GetFileNameWithoutExtension(cache).Split("__");
            var festivals = parts.Length < 3 ? [] : parts[2].Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => uint.Parse(p, System.Globalization.NumberStyles.HexNumber)).ToList();
            custom.CustomizeMesh(mesh, festivals);
        }
        var query = Activator.CreateInstance(queryType, mesh)!;
        Vector3? Probe(Vector3 p, float xz, float y) => (Vector3?)nearestMethod.Invoke(query, [p, xz, y, false]);
        List<Vector3> QueryPath(Vector3 from, Vector3 to, float range)
        {
            var parameters = pathMethod.GetParameters().Select(p => p.Name switch
            { "from" => (object)from, "to" => to, "useRaycast" => false, "useStringPulling" => true,
                "range" => range, "cancel" => CancellationToken.None, _ => null }).ToArray();
            var result = (IEnumerable)pathMethod.Invoke(query, parameters)!;
            return result.Cast<object>().Select(p => p is Vector3 v ? v
                : (Vector3)(p.GetType().GetProperty("Position")?.GetValue(p) ?? p.GetType().GetField("Position")!.GetValue(p))!).ToList();
        }
        var layout = travel.ForTerritory(id);
        if (id == 128)
        {
            var level = data.GetExcelSheet<Level>().GetRow(1673081);
            Check(level.Object.RowId == 1000972 && level.Type == 8 && level.Map.RowId == 11,
                "Call of the Sea must retain its exact NPC, kind and upper-deck map.");
            var marker = new Vector3(level.X, level.Y, level.Z);
            Vector3? Reachable(Vector3 p, float xz, float y) => (Vector3?)nearestMethod.Invoke(query, [p, xz, y, true]);
            var approach = SelectionObjectResolver.Approach(marker, Reachable);
            Check(approach.HasValue, $"Call of the Sea approach rejected in {Path.GetFileName(cache)}.");
            var horizontal = Vector2.Distance(new(marker.X, marker.Z), new(approach!.Value.X, approach.Value.Z));
            Check(horizontal <= 3f && MathF.Abs(approach.Value.Y - marker.Y) <= 2f,
                "NPC approach left its bounded distance or floor.");
            // Player position observed at 14:03:24 in the reported log, not an
            // arbitrary point chosen from a different floor of this territory.
            var from = new Vector3(-82f, 35.5f, 115.3f);
            var path = QueryPath(from, approach.Value, AutoWalkService.StopRange);
            Check(NavigationPathCheck.Reaches(path, approach.Value, AutoWalkService.StopRange),
                $"Recorded upper-deck position has no complete path to the NPC approach in {Path.GetFileName(cache)}.");
            questApproaches.Add(new { Cache = Path.GetFileName(cache), level.RowId, BaseId = level.Object.RowId,
                Target = marker, Approach = approach, HorizontalGap = horizontal, OldDistanceLimitRejected = horizontal > 2.5f,
                From = from, Path = path, Reaches = true });
            Console.WriteLine($"PASS {Path.GetFileName(cache)}: exact quest NPC approach at {horizontal:F3} m, full path from recorded upper-deck position.");
        }
        var routes = new List<object>();
        var queries = 0;
        var complete = 0;
        foreach (var group in levels.Where(l => l.Territory.RowId == id)
            .GroupBy(l => identity.Canonical(layout.ResolveMap(new(l.X, l.Y, l.Z), l.Map.RowId))))
        {
            var points = group.Select(l => new { Level = l.RowId, Point = Probe(new(l.X, l.Y, l.Z), 1f, 2f) })
                .Where(p => p.Point is { } v && TravelLayout.Finite(v)).Take(24).ToArray();
            if (group.Key == 0 || points.Length == 0) continue;
            var from = points[0].Point!.Value;
            foreach (var target in points.Skip(1))
            {
                var goal = target.Point!.Value;
                var path = QueryPath(from, goal, AutoWalkService.StopRange); queries++;
                var reaches = NavigationPathCheck.Reaches(path, goal, AutoWalkService.StopRange);
                if (reaches) complete++;
                routes.Add(new { Map = group.Key, FromLevel = points[0].Level, TargetLevel = target.Level,
                    From = from, Goal = goal, Reaches = reaches, PathPoints = path.Count,
                    ActualEnd = path.Count >= 2 ? (Vector3?)path[^2] : null });
            }
            foreach (var border in travel.WalkingBorders(group.Key))
            {
                var targets = ZoneBorderService.Targets(border.Border, from, Probe);
                var check = new NavigationPathCheck(from, targets.FirstOrDefault(), 5.2f,
                    targets.Select(p => new NavigationPathCheck.Choice(p)), (f, t, r) => Task.FromResult(QueryPath(f, t, r)));
                for (var n = 0; n < 100 && !check.Completed; n++) check.Poll();
                queries += check.Requests; if (check.Result != null) complete++;
                routes.Add(new { Map = group.Key, BorderInstance = border.Border.InstanceId, border.TargetMap,
                    From = from, CandidateCount = targets.Count, Reaches = check.Result != null, Selected = check.Result });
            }
        }
        coverage.Add(new { Cache = Path.GetFileName(cache), Territory = id, CustomizationVersion = version,
            CandidateTerritories = matches.Select(t => t.RowId).ToArray(), ActiveTerritoryIdentified = matches.Length == 1,
            Status = "Queried", PathQueries = queries, CompleteRoutes = complete, Routes = routes,
            SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cache))) });
        Console.WriteLine($"AUDIT {Path.GetFileName(cache)}: territory {id}, {queries} queries, {complete}/{routes.Count} complete routes; diagnostics include inactive/isolated layers.");
    }
    Check(questApproaches.Count > 0, "Limsa saved mesh missing; reported quest approach cannot be checked.");
    File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = true, Checks = checks, Recorded = recorded, QuestApproaches = questApproaches, Coverage = coverage,
        InstalledVnavmeshSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(vnavDir, "vnavmesh.dll")))),
        ActualInstalledMeshQueryChecked = true, NativeFfxivChecked = false, InGameVerified = false },
        new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
}

public class QuietLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.ReturnType == typeof(void)
        ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
}
