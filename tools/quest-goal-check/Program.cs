using System.Numerics;
using System.Text.Json;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("QuestGoalCheck <game/sqpack> <report.json>");
using var game = new GameData(args[0]);
var resolver = new QuestObjectiveTarget(new GameDataReader(game));
var checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
QuestDestination Area(uint quest, uint level, float radius)
{
    var row = game.Excel.GetSheet<Level>().GetRow(level);
    Check(row.Type == 51 && row.Territory.RowId == 155 && row.Map.RowId == 53, "Recorded marker changed");
    return new(game.Excel.GetSheet<Quest>().GetRow(quest).Name.ExtractText(), "", new(row.X, row.Y, row.Z),
        radius, 155, 53, true, QuestKind.SideQuest, 40, TargetLevelType: 51, QuestId: quest, ObjectiveLevelId: level);
}
var man = Area(66484, 4298916, 50);
var serum = Area(66485, 4308186, 20);
var ice = resolver.Resolve(man, 1);
var rescue = resolver.Resolve(serum, 1);
Check(ice.TargetBaseId == 2002185 && ice.TargetLevelType == 45 && ice.AreaTargetLevelId == 4298909,
    "Pickaxe step must target the quest's ice wall, not the lower ledge or future corpse");
Check(MathF.Abs(ice.Position.Y - 276.803f) < .01f, "Ice wall lost its real height");
Check(rescue.TargetLevelType == 49 && rescue.AreaTargetLevelId == 4298945 && rescue.TargetBaseId == 0,
    "Rescue must use the quest's exact EventRange");
Check(MathF.Abs(rescue.Position.Y - 254.54f) < .01f && MathF.Abs(rescue.Radius - 5.27859f) < .01f,
    "Rescue lost the trigger height/radius");
Check(ice.ObjectiveLevelId == man.ObjectiveLevelId && rescue.ObjectiveLevelId == serum.ObjectiveLevelId,
    "Refreshing must retain the native marker identity");
foreach (var area in new[] { man, serum })
foreach (byte? sequence in new byte?[] { null, 0, 255, 99 })
    Check(resolver.Resolve(area, sequence) == area, "Unknown/inactive step must not guess an actor");
Check(resolver.Resolve(man with { QuestId = 0 }, 1).AreaTargetLevelId == 0, "Ambiguous quest name must not guess identity");
Check(resolver.Resolve(man with { MapId = 999 }, 1).AreaTargetLevelId == 0, "Another map must not receive the ice wall");
Check(resolver.Resolve(man with { Radius = 1 }, 1).AreaTargetLevelId == 0, "Actor outside the actual circle must not replace it");
Check(resolver.Resolve(man with { TargetBaseId = 123, TargetLevelType = 8 }, 1).AreaTargetLevelId == 0,
    "An exact native target must never be replaced");
Check(QuestObjectiveTarget.TriggerPoint(rescue, rescue.Radius / 4,
    (_, _, _) => new(-925.25f, 231.75f, -14)) == null, "Logged lower ground was accepted again");
File.WriteAllText(args[1], JsonSerializer.Serialize(new { Passed = true, Checks = checks, Ice = ice, Rescue = rescue,
    InGameVerified = false, MeshTraversalVerified = false }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
Console.WriteLine($"PASS: {checks} checks against installed quest/Level data. Live movement was not tested.");
