using System.IO.Compression;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

if (args.Length>=3 && args[1]=="--describe-item")
{
    using var probe=new GameData(args[0],new LuminaOptions { DefaultExcelLanguage=Language.English });
    foreach (var id in args.Skip(2).Select(uint.Parse))
    {
        var row=probe.Excel.GetSheet<Item>().GetRow(id);
        Console.WriteLine(JsonSerializer.Serialize(new { Id=id,Name=row.Name.ExtractText(),
            Description=row.Description.ExtractText(),Category=row.ItemUICategory.Value.Name.ExtractText() }));
    }
    return;
}
if (args.Length != 2 && args.Length != 4) throw new ArgumentException("GameText <sqpack> <snapshot.json.gz> [catalog.json.gz report.json], or <sqpack> --describe-item <id>...");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var sheets = new Dictionary<string, Dictionary<uint, Source>>();
Capture<Item>("ItemName", x => x.Name);
Capture<EventItem>("EventItemName", x => x.Name);
Capture<Lumina.Excel.Sheets.Action>("ActionName", x => x.Name);
Capture<CraftAction>("CraftActionName", x => x.Name);
Capture<GeneralAction>("GeneralActionName", x => x.Name);
Capture<BuddyAction>("BuddyActionName", x => x.Name);
Capture<Trait>("TraitName", x => x.Name);
Capture<PetAction>("PetActionName", x => x.Name);
Capture<Mount>("MountName", x => x.Singular);
Capture<Companion>("CompanionName", x => x.Singular);
Capture<Status>("StatusName", x => x.Name);
Capture<Status>("StatusDescription", x => x.Description);
Capture<Emote>("EmoteName", x => x.Name);
Capture<BaseParam>("BaseParamName", x => x.Name);
Capture<ClassJob>("ClassJobName", x => x.Name);
Capture<ItemUICategory>("ItemUICategoryName", x => x.Name);
Capture<GrandCompany>("GrandCompanyName", x => x.Name);
Capture<MainCommand>("MainCommandName", x => x.Name);
Capture<GatheringType>("GatheringTypeName", x => x.Name);
Capture<LogFilter>("LogFilterName", x => x.Name);
Capture<DeepDungeonItem>("DeepDungeonItemName", x => x.Name);
Capture<DeepDungeonItem>("DeepDungeonItemTooltip", x => x.Tooltip);
Capture<DeepDungeonMagicStone>("DeepDungeonMagicStoneName", x => x.Name);
Capture<DeepDungeonMagicStone>("DeepDungeonMagicStoneTooltip", x => x.Tooltip);
Capture<DeepDungeonEquipment>("DeepDungeonEquipmentName", x => x.Name);
Capture<DeepDungeonEquipment>("DeepDungeonEquipmentDescription", x => x.Description);
Capture<DeepDungeonFloorEffectUI>("DeepDungeonFloorEffectUIName", x => x.Name);
Capture<DeepDungeonFloorEffectUI>("DeepDungeonFloorEffectUIDescription", x => x.Description);
Capture<DeepDungeon4GimmickEffectTransient>("DeepDungeon4GimmickEffectTransientName", x => x.Name);
Capture<DeepDungeon4GimmickEffectTransient>("DeepDungeon4GimmickEffectTransientDescription", x => x.Description);
var version = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "ffxivgame.ver")).Trim();
var itemCategories = game.Excel.GetSheet<Item>().Where(x => !x.Name.IsEmpty)
    .ToDictionary(x => x.RowId, x => x.ItemUICategory.RowId);
var playerActions = game.Excel.GetSheet<Lumina.Excel.Sheets.Action>()
    .Where(x => !x.Name.IsEmpty && x.IsPlayerAction).Select(x => x.RowId).ToArray();
var jobActions = game.Excel.GetSheet<Lumina.Excel.Sheets.Action>()
    .Where(x => !x.Name.IsEmpty && x.ClassJobCategory.RowId != 0).Select(x => x.RowId).ToArray();
using (var output = File.Create(args[1]))
using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
    JsonSerializer.Serialize(gzip, new { game_version=version, sheets, item_categories=itemCategories,
        player_action_ids=playerActions, job_action_ids=jobActions }, new JsonSerializerOptions {
        Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
foreach (var (sheet, rows) in sheets) Console.WriteLine($"{sheet}: {rows.Count}, unique {rows.Values.Select(x=>x.text).Distinct().Count()}");
if (args.Length==4)
{
    using var input=File.OpenRead(args[2]);
    using var compressed=new GZipStream(input,CompressionMode.Decompress);
    var catalog=JsonSerializer.Deserialize<Dictionary<string,Dictionary<uint,byte[][]>>>(compressed)!;
    var problems=new List<string>();
    foreach (var (table,rows) in catalog)
        foreach (var (id,pair) in rows)
        {
            if (pair.Length!=2 || !sheets.TryGetValue(table,out var sources) || !sources.TryGetValue(id,out var source)
                || !source.bytes.AsSpan().SequenceEqual(pair[0]) || !Valid(new ReadOnlySeStringSpan(pair[1]))
                || string.IsNullOrWhiteSpace(new ReadOnlySeString(pair[1]).ExtractText()))
                problems.Add($"{table}/{id}");
        }
    var coverage=sheets.ToDictionary(x=>x.Key,x=>new {
        SourceRows=x.Value.Count,
        AcceptedRows=catalog.TryGetValue(x.Key,out var translated) ? translated.Count : 0,
        UnresolvedServerKeys=x.Value.Count(r=>r.Value.text.StartsWith("_rsv_",StringComparison.Ordinal)),
        MissingRows=x.Value.Where(r=>!r.Value.text.StartsWith("_rsv_",StringComparison.Ordinal)
            && (!catalog.TryGetValue(x.Key,out var found) || !found.ContainsKey(r.Key))).Select(r=>r.Key).ToArray() });
    File.WriteAllText(args[3],JsonSerializer.Serialize(new { GameVersion=version, Tables=coverage,
        StaleOrInvalid=problems, InGameVerified=false,
        CatalogSHA256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[2]))) },
        new JsonSerializerOptions { WriteIndented=true }));
    if (problems.Count!=0)throw new InvalidDataException($"Invalid or stale accepted translations: {string.Join(", ",problems.Take(10))}");
    Console.WriteLine($"Accepted {catalog.Sum(x=>x.Value.Count)} source/translation pairs match the installed game. See report for missing rows.");
}

static bool Valid(ReadOnlySeStringSpan data)
{
    foreach (var payload in data)
    {
        if (payload.Type==ReadOnlySePayloadType.Invalid) return false;
        if (payload.Type==ReadOnlySePayloadType.Text && !payload.Validate())return false;
        if (payload.Type==ReadOnlySePayloadType.Macro)
            foreach (var expression in payload)
                if (!ValidExpression(expression))return false;
    }
    return true;
}

static bool ValidExpression(ReadOnlySeExpressionSpan expression)
{
    if (expression.TryGetInt(out _) || expression.TryGetPlaceholderExpression(out _))return true;
    if (expression.TryGetString(out var text))return Valid(text);
    if (expression.TryGetParameterExpression(out _,out var a))return ValidExpression(a);
    return expression.TryGetBinaryExpression(out _,out a,out var b) && ValidExpression(a) && ValidExpression(b);
}

void Capture<T>(string key, Func<T, ReadOnlySeString> field) where T : struct, IExcelRow<T>
{
    var rows = new Dictionary<uint, Source>();
    foreach (var row in game.Excel.GetSheet<T>())
    {
        var source = field(row);
        if (source.IsEmpty || string.IsNullOrWhiteSpace(source.ExtractText())) continue;
        rows[row.RowId] = new Source(source.ExtractText(), source.Data.ToArray());
    }
    sheets[key] = rows;
}
record Source(string text, byte[] bytes);
