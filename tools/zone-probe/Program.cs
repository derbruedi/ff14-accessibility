// zoneprobe - what stands at these coordinates?
//
// Offline against the installed sqpack. Answers the question a walk that stalls
// always raises and that no in-game probe answers well: is there something in
// the way, or does the navigation mesh simply not reach?
//
// The layout files are the same source vnavmesh itself uses. Checked against its
// decompiled code (SceneExtractor.cs:165/177/305): it takes the live layout from
// the game's memory plus the .pcb collision files from the sqpack. There is no
// route database anywhere - Recast voxelises that geometry into a walkable
// surface at runtime. So the geometry below IS the input; if a wall shows up
// here, it is a wall for the mesh too.

using System.Globalization;
using System.Numerics;
using Lumina;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK")
             ?? @"K:\SteamLibrary\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";

if (args.Length < 4)
{
    Console.WriteLine("zoneprobe <territoryId> <x> <z> <radius> [y]");
    Console.WriteLine();
    Console.WriteLine("  Lists every layout object within <radius> metres of (x|z), nearest first.");
    Console.WriteLine("  Give <y> to also report the height difference; without it height is ignored.");
    Console.WriteLine();
    Console.WriteLine("  Example - the New Gridania border to Central Shroud:");
    Console.WriteLine("    zoneprobe 132 154.5 155.5 12 -12.9");
    Console.WriteLine();
    Console.WriteLine($"  sqpack: {sqpack}  (override with FFXIV_SQPACK)");
    return 1;
}

var territoryId = uint.Parse(args[0], CultureInfo.InvariantCulture);
var centreX     = float.Parse(args[1], CultureInfo.InvariantCulture);
var centreZ     = float.Parse(args[2], CultureInfo.InvariantCulture);
var radius      = float.Parse(args[3], CultureInfo.InvariantCulture);
float? centreY  = args.Length > 4 ? float.Parse(args[4], CultureInfo.InvariantCulture) : null;

if (!Directory.Exists(sqpack))
{
    Console.Error.WriteLine($"sqpack not found: {sqpack}");
    return 1;
}

var game = new GameData(sqpack, new LuminaOptions
{
    PanicOnSheetChecksumMismatch = false,
    DefaultExcelLanguage = Language.German,
});

var territory = game.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
if (territory == null)
{
    Console.Error.WriteLine($"Territory {territoryId} is not in the sheet.");
    return 1;
}

var bg = territory.Value.Bg.ExtractText();
if (string.IsNullOrEmpty(bg) || !bg.Contains("/level/"))
{
    Console.Error.WriteLine($"Territory {territoryId} has no usable Bg path (was: '{bg}').");
    return 1;
}

var levelDirectory = "bg/" + bg[..(bg.LastIndexOf("/level/", StringComparison.Ordinal) + 7)];
var placeName      = territory.Value.PlaceName.ValueNullable?.Name.ExtractText() ?? "?";

Console.WriteLine($"Territory {territoryId} ({placeName})");
Console.WriteLine($"Layout:    {levelDirectory}");
Console.WriteLine($"Centre:    ({centreX:F1}|{(centreY.HasValue ? centreY.Value.ToString("F1", CultureInfo.InvariantCulture) : "-")}|{centreZ:F1}), radius {radius:F1} m");
Console.WriteLine();

// Every layout file of the zone, not just the one that looks relevant: which file
// a piece of scenery lives in is a level-designer decision, not a rule.
string[] layoutFiles =
{
    "bg.lgb", "planmap.lgb", "planevent.lgb", "planner.lgb", "planlive.lgb", "vfx.lgb", "sound.lgb",
};

var found = new List<Hit>();
var filesRead = 0;

foreach (var file in layoutFiles)
{
    var path = levelDirectory + file;
    LgbFile lgb;
    try
    {
        lgb = game.GetFile<LgbFile>(path);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {file}: not readable ({ex.Message})");
        continue;
    }

    if (lgb == null) continue;
    filesRead++;

    foreach (var layer in lgb.Layers)
    {
        foreach (var instance in layer.InstanceObjects)
        {
            var t = instance.Transform;
            var position = new Vector3(t.Translation.X, t.Translation.Y, t.Translation.Z);
            var flat = Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(centreX, centreZ));
            if (flat > radius) continue;

            found.Add(new Hit(
                flat,
                position,
                new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z),
                t.Rotation.Y,
                instance.AssetType,
                layer.Name ?? "?",
                file,
                Describe(instance)));
        }
    }
}

Console.WriteLine($"{filesRead} layout file(s) read, {found.Count} object(s) inside the radius.");
Console.WriteLine();

// Grouped by type first - a list of 60 scenery pieces hides the one collision box
// that matters, and the type is what decides whether a thing blocks at all.
foreach (var group in found.GroupBy(h => h.Type).OrderBy(g => g.Min(h => h.Distance)))
{
    Console.WriteLine($"--- {group.Key} ({group.Count()}) ---");
    foreach (var hit in group.OrderBy(h => h.Distance))
    {
        var height = centreY.HasValue
            ? $" dy={hit.Position.Y - centreY.Value,6:F1}"
            : "";
        Console.WriteLine(
            $"  {hit.Distance,6:F1} m  ({hit.Position.X,7:F1}|{hit.Position.Y,7:F1}|{hit.Position.Z,7:F1}){height}" +
            $"  scale=({hit.Scale.X:F1}|{hit.Scale.Y:F1}|{hit.Scale.Z:F1})" +
            $"  yaw={hit.Yaw * 180f / MathF.PI,4:F0}  [{hit.Layer}/{hit.File}]  {hit.Detail}");
    }
    Console.WriteLine();
}

return 0;

// The one field per type that says what the object actually is. Anything without
// a useful field is left blank rather than filled with a guess.
static string Describe(LayerCommon.InstanceObject instance)
{
    switch (instance.AssetType)
    {
        case LayerEntryType.BG:
        {
            var bg = (LayerCommon.BGInstanceObject)instance.Object;
            var model = Path.GetFileName(bg.AssetPath ?? "");
            var collision = string.IsNullOrEmpty(bg.CollisionAssetPath)
                ? "no .pcb"
                : Path.GetFileName(bg.CollisionAssetPath);
            return $"{model}  collision={bg.CollisionType} ({collision})";
        }
        case LayerEntryType.CollisionBox:
        {
            var box = (LayerCommon.CollisionBoxInstanceObject)instance.Object;
            return $"shape={box.ParentData.TriggerBoxShape} enabled={box.ParentData.Enabled} " +
                   $"pushPlayerOut={box.PushPlayerOut}";
        }
        case LayerEntryType.ExitRange:
        {
            var exit = (LayerCommon.ExitRangeInstanceObject)instance.Object;
            return $"-> territory {exit.TerritoryType}, running direction " +
                   $"{exit.PlayerRunningDirection * 180f / MathF.PI:F0} deg";
        }
        case LayerEntryType.EventObject:
        {
            var eobj = (LayerCommon.EventInstanceObject)instance.Object;
            return $"baseId={eobj.ParentData.BaseId}";
        }
        case LayerEntryType.DoorRange:
        case LayerEntryType.NaviMeshRange:
        case LayerEntryType.ClickableRange:
        case LayerEntryType.EventRange:
            return "";
        default:
            return "";
    }
}

internal readonly record struct Hit(
    float Distance,
    Vector3 Position,
    Vector3 Scale,
    float Yaw,
    LayerEntryType Type,
    string Layer,
    string File,
    string Detail);
