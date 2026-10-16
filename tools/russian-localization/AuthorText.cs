using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("AuthorText <sqpack> <sources.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var pets = game.Excel.GetSheet<Pet>();
var places = game.Excel.GetSheet<PlaceName>();
var beasts = game.Excel.GetSheet<RawRow>(name: "XBMPet");
var monsters = game.Excel.GetSheet<BNpcName>();
var rows = new List<object>();
foreach (var row in beasts)
{
    if (row.RowId is 0 or > 50) continue;
    var petId = (uint)row.ReadInt32Column(0);
    var name = pets.GetRow(petId).Name;
    var habitatId = row.ReadUInt16Column(7);
    var desc = row.ReadStringColumn(8);
    rows.Add(new { number = row.RowId, pet_id = petId,
        name = name.ExtractText(), name_bytes = Convert.ToBase64String(name.Data.Span),
        description = desc.ExtractText(), description_bytes = Convert.ToBase64String(desc.Data.Span),
        place_id = habitatId, habitat = habitatId == 0 ? "" : places.GetRow(habitatId).Name.ExtractText(),
        monster_ids = monsters.Where(x => string.Equals(x.Singular.ExtractText(), name.ExtractText(),
            StringComparison.OrdinalIgnoreCase)).Select(x => x.RowId).ToArray() });
}
var addon = game.Excel.GetSheet<Addon>();
var german = game.Excel.GetSheet<Addon>(Language.German);
var lottery = addon.Where(x => x.RowId is >= 9260 and <= 9290)
    .Select(x => new { id = x.RowId, en = x.Text.ExtractText(),
        en_bytes = Convert.ToBase64String(x.Text.Data.Span), de = german.GetRow(x.RowId).Text.ExtractText() }).ToArray();
var fate = game.Excel.GetSheet<Fate>().GetRow(1409);
var levels = game.Excel.GetSheet<Level>();
var npcs = game.Excel.GetSheet<ENpcResident>();
var giverIds = game.Excel.GetSheet<Leve>().Select(x => x.LevelLevemete.RowId).Where(id => id != 0)
    .Distinct().Select(id => levels.GetRow(id)).Where(x => x.Type == 8 && x.Object.RowId != 0)
    .Select(x => x.Object.RowId).Distinct().Order().ToArray();
var givers = giverIds.Select(id => npcs.GetRow(id)).Select(x => new {
    id = x.RowId, name = x.Singular.ExtractText(), name_bytes = Convert.ToBase64String(x.Singular.Data.Span) }).ToArray();
var snapshot = new { game_version = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "ffxivgame.ver")).Trim(),
    pets = rows, lottery, givers, fate = new { id = fate.RowId, en = fate.Name.ExtractText(),
        en_bytes = Convert.ToBase64String(fate.Name.Data.Span) } };
File.WriteAllText(args[1], JsonSerializer.Serialize(snapshot, new JsonSerializerOptions {
    WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Captured {rows.Count} beasts and {lottery.Length} lottery strings.");
