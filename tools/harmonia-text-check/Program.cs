using System.Reflection;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.Evaluator;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using DetailKind = FFXIVClientStructs.FFXIV.Client.Enums.DetailKind;
using GameAction = Lumina.Excel.Sheets.Action;

if (args.Length != 3) throw new ArgumentException("HarmoniaTextCheck <game/sqpack> <selected.hpk> <report.json>");
using var game = new GameData(args[0]);
var data = new GameDataReader(game);
var fixture = new PackFixture(args[1], data);
var log = DispatchProxy.Create<IPluginLog, SilentLog>();
var evaluator = new RecordingEvaluator();
var descriptions = new GameDescriptionService(data, evaluator, log);
Loc.Mode = LanguageMode.Russian;
GameDisplayText.Configure(data, fixture.Read);
var assertions = 0; var fields = 0; var ambiguous = 0;
var counts = new Dictionary<string, int>();
var ambiguousExamples = new List<string>();
var copiedAmbiguous = 0;
void Check(bool passed, string message) { assertions++; if (!passed) throw new Exception(message); }
foreach (var cell in fixture.Cells)
{
    if (cell.Original.IsEmpty || cell.Original.Span.SequenceEqual(cell.Display.Data.Span)) continue;
    var shown = GameDisplayText.Find(cell.Sheet, cell.Id, cell.Original);
    if (shown == null)
    {
        ambiguous++;
        if (ambiguousExamples.Count < 10) ambiguousExamples.Add($"{cell.Sheet}/{cell.Id}/{cell.Column}");
        continue;
    }
    Check(shown.Value.Data.Span.SequenceEqual(cell.Display.Data.Span), $"Wrong display cell {cell.Sheet}/{cell.Id}/{cell.Column}");
    counts[cell.Sheet] = counts.GetValueOrDefault(cell.Sheet) + 1;
    if (cell.Sheet is "ENpcResident" or "Pet" or "Fate" or "Addon" or "XBMPet")
        if (fixture.Read(cell.Sheet, cell.Id, cell.Original.ToArray()) is not { } copied
            || !copied.Data.Span.SequenceEqual(cell.Display.Data.Span)) copiedAmbiguous++;
}

foreach (var translate in new[] { true, false })
{
    Loc.TranslateItemsAndActions = translate;
    Names<Item>(x => x.Name); Names<EventItem>(x => x.Name);
    Names<GameAction>(x => x.Name); Names<CraftAction>(x => x.Name);
    Names<Trait>(x => x.Name); Names<BuddyAction>(x => x.Name);
    Names<GeneralAction>(x => x.Name); Names<PetAction>(x => x.Name);
    Names<MainCommand>(x => x.Name); Names<Emote>(x => x.Name);
    Names<Mount>(x => x.Singular, "Singular"); Names<Companion>(x => x.Singular, "Singular");
    Names<Status>(x => x.Name); Names<ClassJob>(x => x.Name); Names<BaseParam>(x => x.Name);
    Names<PlaceName>(x => x.Name); Names<BNpcName>(x => x.Singular, "Singular");
    Names<ENpcResident>(x => x.Singular, "Singular"); Names<EObjName>(x => x.Singular, "Singular");
    Names<Quest>(x => x.Name); Names<GatheringType>(x => x.Name);
    Descriptions<Item>(x => x.Description, descriptions.Item);
    Descriptions<ActionTransient>(x => x.Description, descriptions.Action);
    Descriptions<CraftAction>(x => x.Description, descriptions.CraftAction);
    Descriptions<TraitTransient>(x => x.Description, descriptions.Trait);
    Descriptions<BuddyAction>(x => x.Description, descriptions.BuddyAction);
    Descriptions<GeneralAction>(x => x.Description, descriptions.GeneralAction);
    Descriptions<PetAction>(x => x.Description, descriptions.PetAction);
    Descriptions<MainCommand>(x => x.Description, descriptions.MainCommand);
    Descriptions<Status>(x => x.Description, descriptions.Status);
    Descriptions<AozActionTransient>(x => x.Description, descriptions.AozDescription);
    Descriptions<AozActionTransient>(x => x.Stats, descriptions.AozStats);
}

Loc.TranslateItemsAndActions = true;
var authorFields = 0;
Author<ENpcResident>("ENpcResidentName", x => x.Singular);
Author<Pet>("PetName", x => x.Name);
Author<Fate>("FateName", x => x.Name);
Author<Addon>("AddonText", x => x.Text);
foreach (var row in data.GetExcelSheet<RawRow>(name: "XBMPet"))
    if (fixture.Read("XBMPet", row.RowId, row.ReadStringColumn(8).Data) is { } display)
    {
        Check(RussianAuthorText.Translate("XBMPetDescription", row.RowId, row.ReadStringColumn(8).Data.Span, "Старый перевод") == display.ExtractText(), "Wrong pet description field");
        authorFields++;
    }
var inventory = new InventoryService(null!, data, null!, new Configuration(), null!, log, descriptions);
var names = GameNameIndex.Build(data.GetExcelSheet<Item>().Select(row =>
    (row.RowId, row.Name.ExtractText(), fixture.Read("Item", row.RowId, row.Name.Data)?.ExtractText())));
var itemAliases = 0;
foreach (var (name, id) in names)
{
    Check(inventory.ResolveItemIdByName(name) == id, "Wrong item identity for translated label");
    itemAliases++;
}
var gear = new GearInfoService(data, log);
var gearAliases = 0;
foreach (var (name, id) in names)
    if (data.GetExcelSheet<Item>().TryGetRow(id, out var item) && item.EquipSlotCategory.RowId != 0)
    {
        Check(gear.ItemLabelByName(name) == EquipmentSpeech.Name(data, item), "Wrong translated shop equipment");
        gearAliases++;
    }

var menus = 0;
foreach (var row in data.GetExcelSheet<Trait>().Where(x => x.ClassJob.RowId == 25 && x.Level > 0))
{
    if (fixture.Read("Trait", row.RowId, row.Name.Data) is not { } name) continue;
    var allowed = new HashSet<uint> { row.RowId };
    var label = name.ExtractText() + "\nУр. " + row.Level;
    var entry = descriptions.FromActionMenuLabel(label, "Панель Prima", allowed);
    Check(entry.Name == descriptions.TraitName(row.RowId), "Translated trait menu identity");
    Check(entry.Description == descriptions.Trait(row.RowId), "Translated trait menu description");
    menus++;
}

var actionMenus = new Dictionary<string, int>();
var ambiguousMenus = new List<string>();
var menuRows = new List<(DetailKind Kind, uint Id, string Name)>();
MenuRows<GameAction>(DetailKind.Action, x => x.Name);
MenuRows<CraftAction>(DetailKind.CraftingAction, x => x.Name);
MenuRows<GeneralAction>(DetailKind.GeneralAction, x => x.Name);
MenuRows<PetAction>(DetailKind.PetOrder, x => x.Name);
MenuRows<BuddyAction>(DetailKind.BuddyAction, x => x.Name);
var unique = menuRows.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() == 1).Select(x => x.Single());
foreach (var group in unique.GroupBy(x => x.Kind))
    foreach (var row in group.Take(5))
    {
        var entry = descriptions.FromActionMenuLabel(row.Name, "Панель Prima");
        Check(entry.Name == descriptions.MenuName(row.Kind, row.Id), "Translated action menu name");
        var expectedDescription = descriptions.MenuDescription(row.Kind, row.Id);
        if (entry.Description != expectedDescription)
        {
            var cache = (Dictionary<(string, int, LanguageMode, bool, int), (DetailKind Kind, uint Id)[]>)
                typeof(GameDescriptionService).GetField("_menuMatches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(descriptions)!;
            var matches = cache[(row.Name, 0, Loc.Mode, Loc.TranslateItemsAndActions, GameDisplayText.Revision)];
            var variants = matches.Select(m => new ActionMenuText.Entry(descriptions.MenuName(m.Kind, m.Id), descriptions.MenuDescription(m.Kind, m.Id))).Distinct().Count();
            Check(variants > 1 && entry.Description == "Панель Prima", $"Wrong menu fallback {row.Kind}/{row.Id}: {row.Name}");
            ambiguousMenus.Add($"{row.Kind}/{row.Id}: {row.Name}");
        }
        else Check(true, "Exact translated menu description");
        actionMenus[group.Key.ToString()] = actionMenus.GetValueOrDefault(group.Key.ToString()) + 1;
    }

var quests = new QuestMarkerService(null!, data, log);
var questRows = typeof(QuestMarkerService).GetMethod("QuestRows", BindingFlags.NonPublic | BindingFlags.Instance)!;
var questKind = typeof(QuestMarkerService).GetMethod("KindForLabel", BindingFlags.NonPublic | BindingFlags.Instance)!;
var questLabels = fixture.Cells.Where(x => x.Sheet == "Quest" && x.Column == 0)
    .GroupBy(x => x.Display.ExtractText().Trim(), StringComparer.OrdinalIgnoreCase);
var questNames = 0;
var actualQuestNames = data.GetExcelSheet<Quest>().Where(q => q.JournalGenre.RowId != 0)
    .GroupBy(q => (fixture.Read("Quest", q.RowId, q.Name.Data) ?? q.Name).ExtractText().Trim(), StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
foreach (var group in questLabels)
{
    var rows = (List<Quest>)questRows.Invoke(quests, [group.Key])!;
    var expected = actualQuestNames.GetValueOrDefault(group.Key, []);
    Check(rows.Select(x => x.RowId).ToHashSet().SetEquals(expected.Select(x => x.RowId)), $"An old alias contaminated a current Prima quest label '{group.Key}'; actual {string.Join(',', rows.Select(x => x.RowId))}, expected {string.Join(',', expected.Select(x => x.RowId))}");
    var kinds = expected.Select(q => QuestMarkerService.KindForSection(q.JournalGenre.Value.JournalCategory.Value.JournalSection.RowId))
        .Where(k => k != QuestKind.Unknown).Distinct().ToArray();
    Check((QuestKind)questKind.Invoke(quests, [group.Key])! == (kinds.Length == 1 ? kinds[0] : QuestKind.Unknown), "Wrong Prima quest category");
    foreach (var cell in group)
        if (data.GetExcelSheet<Quest>().TryGetRow(cell.Id, out var q) && q.JournalGenre.RowId != 0)
            Check(rows.Any(x => x.RowId == q.RowId), "Translated quest lost its actual row");
    questNames++;
}

var firstItem = data.GetExcelSheet<Item>().First(row => fixture.Read("Item", row.RowId, row.Name.Data) != null);
var translatedItem = fixture.Read("Item", firstItem.RowId, firstItem.Name.Data)!.Value.ExtractText();
GameDisplayText.Configure(data, null);
Check(inventory.ResolveItemIdByName(translatedItem) == 0, "Unloaded translator left a stale item alias");
GameDisplayText.Configure(data, fixture.Read);
Check(inventory.ResolveItemIdByName(translatedItem) == firstItem.RowId, "Reloaded translator did not restore identity");
Loc.Mode = LanguageMode.English;
Check(GameDisplayText.Find("Item", firstItem.RowId, firstItem.Name.Data)?.ExtractText() == translatedItem, "Prompt language replaced translated game text");
Check(!GameTextTranslation.ShouldTranslate("Item"), "Russian speech catalogs escaped their language guard");
GameDisplayText.Configure(null, null);
var report = new { CheckedAt = DateTimeOffset.Now, PackCells = fixture.Cells.Count, fixture.ChangedSources,
    fixture.IncompatibleSheets, DisplayCells = counts.Values.Sum(), Sheets = counts, TypedFields = fields,
    Assertions = assertions, AmbiguousFields = ambiguous, CopiedSourceAmbiguities = copiedAmbiguous, AmbiguousExamples = ambiguousExamples,
    ItemAliases = itemAliases, GearAliases = gearAliases, TraitMenus = menus, QuestNames = questNames,
    AuthorFields = authorFields, ActionMenus = actionMenus, AmbiguousMenuFallbacks = ambiguousMenus, EvaluatorCalls = evaluator.Calls, NativeGameChecked = false, DynamicNumbersChecked = false };
File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Verified {assertions} assertions; {fields} typed fields, {itemAliases} item aliases, {gearAliases} equipment aliases, {menus} trait menus, {questNames} quest labels. Ambiguous fields: {ambiguous}. Native game and speech remain untested.");

void Names<T>(Func<T, ReadOnlySeString> field, string column = "Name") where T : struct, IExcelRow<T>
{
    foreach (var row in data.GetExcelSheet<T>())
    {
        if (fixture.Read(typeof(T).Name, row.RowId, field(row).Data) is not { } expected) continue;
        Check(RussianGameText.Text(data, row, field, column).Data.Span.SequenceEqual(expected.Data.Span), $"Wrong typed name {typeof(T).Name}/{row.RowId}");
        fields++;
    }
}
void MenuRows<T>(DetailKind kind, Func<T, ReadOnlySeString> field) where T : struct, IExcelRow<T>
{
    foreach (var row in data.GetExcelSheet<T>())
        if (fixture.Read(typeof(T).Name, row.RowId, field(row).Data) is { } display)
            menuRows.Add((kind, row.RowId, display.ExtractText()));
}
void Author<T>(string key, Func<T, ReadOnlySeString> field) where T : struct, IExcelRow<T>
{
    foreach (var row in data.GetExcelSheet<T>())
        if (fixture.Read(typeof(T).Name, row.RowId, field(row).Data) is { } display)
        {
            Check(RussianAuthorText.Translate(key, row.RowId, field(row).Data.Span, "Старый перевод") == display.ExtractText(), $"Wrong author field {key}/{row.RowId}");
            authorFields++;
        }
}
void Descriptions<T>(Func<T, ReadOnlySeString> field, Func<uint, string> read) where T : struct, IExcelRow<T>
{
    foreach (var row in data.GetExcelSheet<T>())
    {
        if (fixture.Read(typeof(T).Name, row.RowId, field(row).Data) is not { } expected || row.RowId == 0) continue;
        var before = evaluator.Calls;
        Check(read(row.RowId) == expected.ExtractText(), $"Wrong spoken description {typeof(T).Name}/{row.RowId}");
        Check(evaluator.Calls == before + 1 && evaluator.Last.Data.Span.SequenceEqual(expected.Data.Span), "SeString not evaluated intact");
        fields++;
    }
}

public class SilentLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
}
internal sealed class RecordingEvaluator : ISeStringEvaluator
{
    internal int Calls; internal ReadOnlySeString Last;
    public ReadOnlySeString Evaluate(ReadOnlySeString value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null)
    { Calls++; Last = value; return value; }
    public ReadOnlySeString Evaluate(ReadOnlySeStringSpan value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => Evaluate(new ReadOnlySeString(value.Data.ToArray()));
    public ReadOnlySeString EvaluateMacroString(string value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateMacroString(ReadOnlySpan<byte> value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromAddon(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromLobby(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromLogMessage(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public string EvaluateActStr(ActionKind kind, uint id, ClientLanguage? language = null) => throw new NotSupportedException();
    public string EvaluateObjStr(ObjectKind kind, uint id, ClientLanguage? language = null) => throw new NotSupportedException();
}
