using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length != 3) throw new ArgumentException("DungeonRouteCheck <sqpack> <plugin-config-directory> <report.json>");
using var game = new GameData(args[0]);
var data = new GameDataReader(game);
uint territory = 0;
var client = Proxy.Make<IClientState>(m => m.Name == "get_TerritoryType" ? territory : throw new NotSupportedException(m.Name));
var plugin = Proxy.Make<IDalamudPluginInterface>(m => m.Name == "GetPluginConfigDirectory" ? args[1] : throw new NotSupportedException(m.Name));
var log = Proxy.Make<IPluginLog>(_ => null);
var route = new DungeonRouteService(plugin, client, new ObjectNameService(data), log);
var rows = new List<object>();
var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
var cache = Path.Combine(appData, "XIVLauncher/pluginConfigs/vnavmesh/meshcache");
var errors = new List<string>();
var noStations = new List<uint>();
var ids = Directory.GetFiles(route.PathFolder, "*.json")
    .Select(p => Regex.Match(Path.GetFileName(p), @"^\((\d+)\)"))
    .Where(m => m.Success).Select(m => ushort.Parse(m.Groups[1].Value)).Distinct().Order();
foreach (var id in ids)
{
    territory = id;
    var steps = route.GetStepsForCurrentZone();
    if (steps.Count == 0)
    {
        noStations.Add(id);
        if (id is 171 or 1330) errors.Add($"No Dzemael stations in territory {id}.");
    }
    if (steps.Any(s => !float.IsFinite(s.Position.X) || !float.IsFinite(s.Position.Y) || !float.IsFinite(s.Position.Z)))
        errors.Add($"Invalid position in territory {id}.");
    var zone = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(id);
    var bg = zone?.Bg.ExtractText() ?? "";
    var prefix = bg.Replace('/', '_');
    rows.Add(new { TerritoryId = id, Stations = steps.Count, MechanismsWithId = steps.Count(s => s.Kind == DungeonStepKind.Interact && s.DataId != 0),
        Background = bg, CachedMeshes = prefix.Length > 0 && Directory.Exists(cache) ? Directory.GetFiles(cache, prefix + "*.navmesh").Length : 0 });
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
var vnavDir = Path.Combine(appData, "XIVLauncher/devPlugins/vnavmesh");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var path = Path.Combine(vnavDir, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var vnav = Assembly.LoadFrom(Path.Combine(vnavDir, "vnavmesh.dll"));
var service = vnav.GetType("Navmesh.Service")!;
service.GetProperty("Log", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, log);
var rebuildCheck = DungeonCompatibilityCheck.Run(vnav, log, errors);
var compatibility = DungeonMeshCompatibility.Apply(vnav);
if (compatibility is not (DungeonMeshCompatibility.Result.Applied or DungeonMeshCompatibility.Result.AlreadySupported))
    errors.Add("Installed vnavmesh cannot receive the verified Dzemael compatibility.");
var repeated = DungeonMeshCompatibility.Apply(vnav);
if (repeated != DungeonMeshCompatibility.Result.AlreadySupported) errors.Add("Compatibility did not remain idempotent.");
File.WriteAllText(args[2], JsonSerializer.Serialize(new { Passed = errors.Count == 0, Errors = errors, Zones = rows,
    NonPositionalRoutes = noStations, VnavVersion = vnav.GetName().Version?.ToString(), Compatibility = compatibility.ToString(),
    CompatibilityRepeated = repeated.ToString(),
    RebuildCheck = rebuildCheck,
    InGameVerified = false, MeshTraversalVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Checked {rows.Count} dungeon territories; errors: {errors.Count}. Report: {args[2]}");
if (errors.Count != 0) Environment.ExitCode = 1;

public class Proxy : DispatchProxy
{
    private Func<MethodInfo, object?> callback = null!;
    public static T Make<T>(Func<MethodInfo, object?> callback) where T : class
    {
        var value = Create<T, Proxy>();
        ((Proxy)(object)value).callback = callback;
        return value;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? arguments) => callback(method!);
}
