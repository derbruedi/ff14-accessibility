using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("Verify <game/sqpack> <report.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var data = new GameDataReader(game);
var log = DispatchProxy.Create<IPluginLog, LogProxy>();
var gear = new GearInfoService(data, log);
var cases = new Dictionary<string, string>();
void Equal(string key, string expected, string actual)
{
    if (expected != actual) throw new Exception($"{key}: expected '{expected}', got '{actual}'");
    cases[key] = actual;
}
Loc.Mode = LanguageMode.Russian;
// Real rows underlying the logged three visible shop buttons. Future rank
// tiers in GrandCompanyRank must not disable names of today's shop tiers.
var shopTiers = game.Excel.GetSheet<GCScripShopCategory>().Where(x => x.RowId > 0)
    .Select(x => x.Tier).Where(x => x > 0).Distinct().Order().ToArray();
var rankType = typeof(GearInfoService).Assembly.GetType("FF14Accessibility.Services.GrandCompanyRankText")!;
var rankReader = Activator.CreateInstance(rankType, data, log)!;
Equal("shop tiers from real reader", shopTiers.Max().ToString(),
    rankType.GetMethod("TierCount")!.Invoke(rankReader, null)!.ToString()!);
Equal("shop buttons in player log", "1,2,3", string.Join(',', shopTiers));
var futureRankTier = game.Excel.GetSheet<GrandCompanyRank>().Max(x => x.Tier);
if (futureRankTier <= shopTiers.Max()) throw new Exception("Expected unused future rank tiers in current game data");

// The same formatter used by Armoury collection, with real gear stat rows.
// Only player wearability is left out: no live character exists in this check.
var statsMethod = typeof(GearInfoService).GetMethod("DescribeStats", BindingFlags.NonPublic | BindingFlags.Instance)!;
var labelMethod = typeof(GearInfoService).Assembly.GetType("FF14Accessibility.Services.ArmouryListAccess")!
    .GetMethod("DescribeLabel", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var rawId in new uint[] { 1891, 1893, 1895, 1001895, 2662, 3017, 3539, 3771, 4198, 4305, 4093, 4422 })
{
    var baseId = Dalamud.Utility.ItemUtil.GetBaseId(rawId).ItemId;
    var row = game.Excel.GetSheet<Item>().GetRow(baseId);
    var stats = (string)statsMethod.Invoke(gear, [row])!;
    if (stats.Length == 0) throw new Exception($"Missing gear stats for {rawId}");
    var hq = rawId >= 1_000_000;
    Func<uint, string> name = id => inventoryLabel(id);
    Func<uint, bool, string> describe = (id, brief) =>
    {
        if (id != baseId || brief) throw new Exception($"Armoury requested shortened/wrong gear for {rawId}");
        return stats;
    };
    var label = (string)labelMethod.Invoke(null, [rawId, 1u, hq, false, name, describe])!;
    if (!label.Contains(stats)) throw new Exception($"Stats lost for {rawId}");
    cases[$"armoury real stats {rawId}"] = label;
}
string inventoryLabel(uint id) => EquipmentSpeech.Name(data, game.Excel.GetSheet<Item>().GetRow(id));
var categories = game.Excel.GetSheet<ClassJobCategory>(Language.English).Where(x => !x.Name.IsEmpty).ToList();
foreach (var category in categories)
{
    var translated = EquipmentSpeech.ClassCategory(data, category);
    if (Regex.IsMatch(translated, "[A-Za-z]") || !Regex.IsMatch(translated, "[А-Яа-яЁё]"))
        throw new Exception($"Untranslated category {category.RowId}: {translated}");
}
Equal("long tank list", "Гладиатор, Мародёр, Паладин, Воин, Тёмный рыцарь, Ганбрейкер",
    gear.TranslateClassLabel("GLA MRD PLD WAR DRK GNB"));
Equal("all classes", "Все классы", gear.TranslateClassLabel("All Classes"));
Equal("craft group", "Ремесленники", gear.TranslateClassLabel("Disciple of the Hand"));
Equal("unknown category preserved", "NEW UNKNOWN", gear.TranslateClassLabel("NEW UNKNOWN"));
Equal("no partial substitutions", "GLA future job", gear.TranslateClassLabel("GLA future job"));
var slots = game.Excel.GetSheet<EquipSlotCategory>();
Equal("ring alternatives", "Надевать: Кольцо на правой или левой руке", EquipmentSpeech.Slots(slots.GetRow(12)));
Equal("two handed weapon", "Надевать: Основная рука, Занимает также: Левая рука", EquipmentSpeech.Slots(slots.GetRow(13)));
Equal("cowl", "Надевать: Туловище, Занимает также: Голова", EquipmentSpeech.Slots(slots.GetRow(15)));
Equal("leg armour blocks feet", "Надевать: Ноги, Занимает также: Ступни", EquipmentSpeech.Slots(slots.GetRow(18)));
Equal("empty slots", "", EquipmentSpeech.Slots(slots.GetRow(0)));
Equal("compared right ring", "Сравниваемое место: Кольцо на правой руке", EquipmentSpeech.ComparisonSlot("Slot: Right Ring"));
Equal("compared left ring", "Сравниваемое место: Кольцо на левой руке", EquipmentSpeech.ComparisonSlot("Slot: Left Ring"));
Equal("unknown comparison preserved", "Slot: Unknown", EquipmentSpeech.ComparisonSlot("Slot: Unknown"));
Equal("full costume", "Надевать: Туловище, Занимает также: Голова, Руки, Ноги, Ступни", EquipmentSpeech.Slots(slots.GetRow(19)));
Equal("soul crystal", "Надевать: Камень души", EquipmentSpeech.Slots(slots.GetRow(17)));
Equal("off hand", "Надевать: Левая рука", EquipmentSpeech.Slots(slots.GetRow(2)));
var inventory = new InventoryService(null!, data, null!, null!, null!, log, null!);
var allItems = game.Excel.GetSheet<Item>();
foreach (var id in new uint[] { 1601, 2377, 3352, 4542, 2966 })
{
    var item = allItems.GetRow(id);
    Equal($"inventory label {id}", EquipmentSpeech.Name(data, item), inventory.ResolveItemLabel(id));
    if (inventory.ResolveItemName(id).Contains("Надевать:")) throw new Exception("Search name was decorated");
    Equal($"shop label {id}", EquipmentSpeech.Name(data, item), inventory.TranslateItemLabel(item.Name.ExtractText()));
    Equal($"comparison label {id}", EquipmentSpeech.Name(data, item), gear.ItemLabelByName(item.Name.ExtractText()));
}
var duplicateName = allItems.Where(x => x.EquipSlotCategory.RowId != 0 && !x.Name.IsEmpty)
    .GroupBy(x => x.Name.ExtractText()).FirstOrDefault(x => x.Count() > 1)?.Key;
if (duplicateName != null)
{
    Equal("ambiguous shop item", duplicateName, inventory.TranslateItemLabel(duplicateName));
    Equal("ambiguous comparison item", duplicateName, gear.ItemLabelByName(duplicateName));
}
Equal("unknown shop item", "Unknown equipment name", inventory.TranslateItemLabel("Unknown equipment name"));
var equipment = 0;
var nonEquipment = 0;
foreach (var item in game.Excel.GetSheet<Item>())
{
    if (item.Name.IsEmpty) continue;
    var label = EquipmentSpeech.Name(data, item);
    var positions = EquipmentSpeech.Slots(item);
    if (item.EquipSlotCategory.RowId == 0)
    {
        if (positions.Length != 0 || label.Contains("Надевать:")) throw new Exception($"False slot {item.RowId}");
        nonEquipment++;
    }
    else
    {
        if (positions.Length == 0 || !label.EndsWith(", " + positions)) throw new Exception($"Missing slots {item.RowId}");
        equipment++;
    }
}
foreach (var language in new[] { ClientLanguage.English, ClientLanguage.German, ClientLanguage.French, ClientLanguage.Japanese })
{
data.Language = language;
foreach (var category in data.GetExcelSheet<ClassJobCategory>(language))
{
    if (category.Name.IsEmpty) continue;
    Equal($"label {language}/{category.RowId}", EquipmentSpeech.ClassCategory(data, category), gear.TranslateClassLabel(category.Name.ExtractText()));
}
}
data.Language = ClientLanguage.English;
foreach (var mode in new[] { LanguageMode.English, LanguageMode.German })
{
    Loc.Mode = mode;
    Equal($"no Russian {mode}", "GLA MRD PLD WAR DRK GNB", gear.TranslateClassLabel("GLA MRD PLD WAR DRK GNB"));
    foreach (var category in categories)
        if (EquipmentSpeech.ClassCategory(data, category) != category.Name.ExtractText().Trim()) throw new Exception("Non-Russian changed");
}
Loc.Mode = LanguageMode.Russian;
Equal("switch back to Russian", "Все классы", gear.TranslateClassLabel("All Classes"));
var report = new { GameVersion = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "ffxivgame.ver")).Trim(),
    Categories = categories.Count, SlotRows = slots.Count, EquipmentItems = equipment, NonEquipmentItems = nonEquipment,
    Assertions = cases.Count, AmbiguousNamesTested = duplicateName == null ? 0 : 1, Cases = cases, InGameVerified = false };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Verified {categories.Count} categories, {equipment} equipment items, {nonEquipment} other items, {cases.Count} assertions. No live UI verification.");

public sealed class GameDataReader(GameData game) : IDataManager
{
    public ClientLanguage Language { get; set; } = ClientLanguage.English;
    public GameData GameData => game;
    public ExcelModule Excel => game.Excel;
    public bool HasModifiedGameDataFiles => false;
    private static Lumina.Data.Language ConvertLanguage(ClientLanguage? language) => language switch
    {
        ClientLanguage.German => Lumina.Data.Language.German,
        ClientLanguage.French => Lumina.Data.Language.French,
        ClientLanguage.Japanese => Lumina.Data.Language.Japanese,
        _ => Lumina.Data.Language.English,
    };
    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelRow<T>
        => game.Excel.GetSheet<T>(ConvertLanguage(language ?? Language), name);
    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelSubrow<T>
        => game.Excel.GetSubrowSheet<T>(ConvertLanguage(language ?? Language), name);
    public FileResource? GetFile(string path) => game.GetFile(path);
    public T? GetFile<T>(string path) where T : FileResource => game.GetFile<T>(path);
    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken = default) where T : FileResource => Task.FromResult(game.GetFile<T>(path) ?? throw new FileNotFoundException(path));
    public bool FileExists(string path) => game.FileExists(path);
}
public class LogProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
}
