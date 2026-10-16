using System.IO.Compression;
using System.Text.Json;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

if (args.Length != 3) throw new ArgumentException("Usage: Verify <sqpack> <catalog.json.gz> <report.json>");
using var input = File.OpenRead(args[1]);
using var gzip = new GZipStream(input, CompressionMode.Decompress);
var entries = JsonSerializer.Deserialize<Dictionary<string, Dictionary<uint, byte[][]>>>(gzip)!;
var invalid = new List<string>();
foreach (var (table, rows) in entries)
    foreach (var (id, pair) in rows)
        if (pair.Length != 2 || !Valid(new ReadOnlySeStringSpan(pair[0])) || !Valid(new ReadOnlySeStringSpan(pair[1])))
            invalid.Add($"{table}/{id}");
if (invalid.Count > 0)
{
    foreach (var k in invalid.Take(3))
    {
        var key = k.Split('/'); var pair = entries[key[0]][uint.Parse(key[1])];
        Console.WriteLine($"{k}: EN {Valid(new ReadOnlySeStringSpan(pair[0]))} / RU {Valid(new ReadOnlySeStringSpan(pair[1]))}");
    }
    throw new InvalidDataException($"{invalid.Count} invalid SeStrings; first: " + string.Join(", ", invalid.Take(10)));
}
Console.WriteLine($"Validated {entries.Sum(s => s.Value.Count)} source/translation pairs with installed Lumina.");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var catalog = RussianDescriptionCatalog.Load();
var results = new Dictionary<string, object>();
var gaps = new Dictionary<string, List<object>>();
var uncovered = 0;
Check<Item>("Item", x => x.Description);
Check<ActionTransient>("Action", x => x.Description);
Check<CraftAction>("CraftAction", x => x.Description);
Check<TraitTransient>("Trait", x => x.Description);
Check<BuddyAction>("BuddyAction", x => x.Description);
Check<GeneralAction>("GeneralAction", x => x.Description);
Check<PetAction>("PetAction", x => x.Description);
Check<MainCommand>("MainCommand", x => x.Description);
Check<AozActionTransient>("AozDescription", x => x.Description);
Check<AozActionTransient>("AozStats", x => x.Stats);
File.WriteAllText(args[2], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.ChangeExtension(args[2], ".gaps.json"), JsonSerializer.Serialize(gaps, new JsonSerializerOptions { WriteIndented = true }));
if (uncovered != 0) throw new InvalidDataException($"{uncovered} descriptions are missing or stale; see the saved report.");

static bool Valid(ReadOnlySeStringSpan data)
{
    // Lumina's whole-string Validate currently returns false even on success.
    // Validate each payload, including its expressions, instead.
    foreach (var payload in data)
    {
        if (payload.Type == ReadOnlySePayloadType.Invalid) return false;
        if (payload.Type == ReadOnlySePayloadType.Text && !payload.Validate()) return false;
        if (payload.Type == ReadOnlySePayloadType.Macro)
            foreach (var expression in payload)
                if (!ValidExpression(expression)) return false;
    }
    return true;
}

static bool ValidExpression(ReadOnlySeExpressionSpan expression)
{
    if (expression.TryGetInt(out _) || expression.TryGetPlaceholderExpression(out _)) return true;
    if (expression.TryGetString(out var text)) return Valid(text);
    if (expression.TryGetParameterExpression(out _, out var a)) return ValidExpression(a);
    return expression.TryGetBinaryExpression(out _, out a, out var b) && ValidExpression(a) && ValidExpression(b);
}

void Check<T>(string table, Func<T, ReadOnlySeString> description) where T : struct, IExcelRow<T>
{
    var missing = new List<uint>();
    var changed = new List<uint>();
    var total = 0;
    var matched = 0;
    var details = new List<object>();
    foreach (var row in game.Excel.GetSheet<T>())
    {
        var text = description(row);
        if (text.IsEmpty) continue;
        total++;
        if (!entries[table].ContainsKey(row.RowId)) missing.Add(row.RowId);
        else if (catalog.Find(table, row.RowId, text.Data.Span, true) == null) changed.Add(row.RowId);
        else matched++;
        if (missing.Contains(row.RowId) || changed.Contains(row.RowId))
            details.Add(new { Id = row.RowId, Current = text.ToString(), CurrentBase64 = Convert.ToBase64String(text.Data.Span),
                Expected = entries[table].TryGetValue(row.RowId, out var pair) ? new ReadOnlySeString(pair[0]).ToString() : "" });
    }
    results[table] = new { Total = total, Matched = matched, Missing = missing, Changed = changed };
    gaps[table] = details;
    uncovered += missing.Count + changed.Count;
    Console.WriteLine($"{table}: {matched}/{total} match installed game; {missing.Count} missing, {changed.Count} changed.");
}
