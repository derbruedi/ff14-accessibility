using System.Text.Json;
using System.IO.Compression;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

if (args.Length < 3) throw new ArgumentException("Names <sqpack> <snapshot.json> <report.json> [catalog.json.gz]");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var names = new Dictionary<string, Dictionary<uint, string>>();
var sources = new Dictionary<string, Dictionary<uint, byte[]>>();
var jobs = new HashSet<string> { "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL" };
Capture<CraftAction>("CraftActionName", x => x.Name);
Capture<BuddyAction>("BuddyActionName", x => x.Name);
Capture<GeneralAction>("GeneralActionName", x => x.Name);
Capture<Lumina.Excel.Sheets.Action>("ActionName", x => x.Name,
    x => x.IsPlayerAction && jobs.Contains(x.ClassJobCategory.ValueNullable?.Name.ExtractText() ?? ""));
var options = new JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(args[1], JsonSerializer.Serialize(names, options));
var counts = names.ToDictionary(x => x.Key, x => x.Value.Count);
var versionPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "ffxivgame.ver");
var version = File.ReadAllText(versionPath).Trim();
var gaps = new List<string>();
if (args.Length == 4)
{
    using var stream = File.OpenRead(args[3]);
    using var gzip = new GZipStream(stream, CompressionMode.Decompress);
    var catalog = JsonSerializer.Deserialize<Dictionary<string, Dictionary<uint, byte[][]>>>(gzip)!;
    foreach (var (sheet, rows) in sources)
        foreach (var (id, source) in rows)
            if (!catalog.TryGetValue(sheet, out var translated) || !translated.TryGetValue(id, out var pair)
                || pair.Length != 2 || !source.AsSpan().SequenceEqual(pair[0])
                || string.IsNullOrWhiteSpace(new ReadOnlySeString(pair[1]).ExtractText()))
                gaps.Add($"{sheet}/{id}");
    if (gaps.Count == 0) Console.WriteLine($"Verified {sources.Sum(x => x.Value.Count)} exact names against the installed game.");
}
File.WriteAllText(args[2], JsonSerializer.Serialize(new { game_version=version, rows=counts,
    catalog_checked=args.Length == 4, missing_or_changed=gaps,
    snapshot_sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[1]))) }, options) + "\n");
if (gaps.Count > 0) throw new InvalidDataException($"Missing or stale names: {string.Join(", ", gaps)}");
Console.WriteLine(JsonSerializer.Serialize(counts));

void Capture<T>(string key, Func<T, ReadOnlySeString> name, Func<T, bool>? include=null) where T:struct,IExcelRow<T>
{
    names[key] = new(); sources[key] = new();
    foreach (var row in game.Excel.GetSheet<T>())
    {
        if (include != null && !include(row)) continue;
        var field = name(row);
        if (field.IsEmpty) continue;
        var text = field.ExtractText();
        // This catalog stores plain names. A new control payload needs explicit support.
        if (!System.Text.Encoding.UTF8.GetBytes(text).AsSpan().SequenceEqual(field.Data.Span))
            throw new InvalidDataException($"Non-plain name: {key}/{row.RowId}");
        names[key][row.RowId] = text;
        sources[key][row.RowId] = field.Data.ToArray();
    }
}
