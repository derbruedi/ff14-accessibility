using System.Reflection;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Quest = Lumina.Excel.Sheets.Quest;

if (args.Length != 3) throw new ArgumentException("HarmoniaQuestCheck <game/sqpack> <HarmoniaEngine/packs> <report.json>");
using var game = new GameData(args[0]);
var data = new GameDataReader(game);
var log = DispatchProxy.Create<IPluginLog, SilentLog>();
var names = HarmoniaQuestNames.LoadInstalled(args[1], data, log);
if (names.Count == 0) throw new Exception("No compatible installed quest names.");
var service = new QuestMarkerService(null!, data, log, args[1]);
var kindForLabel = typeof(QuestMarkerService).GetMethod("KindForLabel", BindingFlags.Instance | BindingFlags.NonPublic)!;
var rowsForLabel = typeof(QuestMarkerService).GetMethod("QuestRows", BindingFlags.Instance | BindingFlags.NonPublic)!;
Loc.Mode = LanguageMode.Russian;
var checks = 0; var counts = new Dictionary<string,int>();
foreach (var group in names.GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
{
    var expected = group.Select(n => data.GetExcelSheet<Quest>().GetRowOrDefault(n.RowId))
        .Where(q => q.HasValue && q.Value.JournalGenre.RowId != 0).Select(q => q!.Value).ToArray();
    if (expected.Length == 0) continue;
    var rows = (List<Quest>)rowsForLabel.Invoke(service, [group.Key])!;
    if (expected.Any(q => rows.All(r => r.RowId != q.RowId))) throw new Exception("Pack row lost: " + group.Key);
    var kinds = rows.Select(q => QuestMarkerService.KindForSection(q.JournalGenre.Value.JournalCategory.Value.JournalSection.RowId))
        .Where(k => k != QuestKind.Unknown).Distinct().ToArray();
    var expectedKind = kinds.Length == 1 ? kinds[0] : QuestKind.Unknown;
    var actual = (QuestKind)kindForLabel.Invoke(service,[group.Key])!;
    if (actual != expectedKind) throw new Exception("Wrong quest kind: " + group.Key);
    counts[actual.ToString()] = counts.GetValueOrDefault(actual.ToString()) + 1; checks++;
}
var samples = new Dictionary<string,QuestKind> { ["Уровень угрозы повышен"] = QuestKind.MainStory,
    ["Слушатель часто опаздывает"] = QuestKind.MainStory, ["Кочующие мародёры"] = QuestKind.MainStory };
foreach (var sample in samples)
    if ((QuestKind)kindForLabel.Invoke(service,[sample.Key])! != sample.Value) throw new Exception("Recorded failure remains: " + sample.Key);
Loc.Mode = LanguageMode.English;
foreach (var sample in samples)
    if (((List<Quest>)rowsForLabel.Invoke(service,[sample.Key])!).Count != 0) throw new Exception("Russian aliases escaped the language guard.");
Loc.Mode = LanguageMode.Russian;
foreach (var sample in samples)
    if ((QuestKind)kindForLabel.Invoke(service,[sample.Key])! != sample.Value) throw new Exception("Russian alias cache failed to refresh.");
foreach(var kind in new[]{QuestKind.MainStory,QuestKind.SideQuest,QuestKind.Job})
    if (counts.GetValueOrDefault(kind.ToString()) == 0) throw new Exception("Quest category untested: " + kind);
File.WriteAllText(args[2],JsonSerializer.Serialize(new{ PackNames=names.Count, CheckedNames=checks, Kinds=counts,
    RecordedSamples=samples.Keys, NativeGameChecked=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Verified {names.Count} pack quest names, {checks} distinct names, all three recorded failures, and language/cache switching. Live FFXIV remains untested.");

public class SilentLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method!.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
}
