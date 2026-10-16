using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace FF14Accessibility.Services;

/// <summary>One description source for inventory, rewards, skill menus and notebooks.</summary>
public sealed partial class GameDescriptionService
{
    private readonly IDataManager _data;
    private readonly ISeStringEvaluator _evaluator;
    private readonly IPluginLog _log;
    private readonly RussianDescriptionCatalog? _russian;
    private readonly RussianDescriptionCatalog? _russianNames;
    private readonly HashSet<string> _reported = new();

    public GameDescriptionService(IDataManager data, ISeStringEvaluator evaluator, IPluginLog log)
    {
        _data = data;
        _evaluator = evaluator;
        _log = log;
        try { _russian = RussianDescriptionCatalog.Load(); }
        catch (Exception ex) { _log.Warning($"[Descriptions] Russian catalog unavailable: {ex.Message}"); }
        try { _russianNames = RussianDescriptionCatalog.Load("RussianActionNames"); }
        catch (Exception ex) { _log.Warning($"[Descriptions] Russian name catalog unavailable: {ex.Message}"); }
    }

    public string Item(uint id) => Read<Item>("Item", id, row => row.Description);
    public string Action(uint id) => Read<ActionTransient>("Action", id, row => row.Description);
    public string CraftAction(uint id) => Read<CraftAction>("CraftAction", id, row => row.Description);
    public string Trait(uint id) => Read<TraitTransient>("Trait", id, row => row.Description);
    public string BuddyAction(uint id) => Read<BuddyAction>("BuddyAction", id, row => row.Description);
    public string AozDescription(uint id) => Read<AozActionTransient>("AozDescription", id, row => row.Description);
    public string AozStats(uint id) => Read<AozActionTransient>("AozStats", id, row => row.Stats);
    public string CraftActionName(uint id) => Read<CraftAction>("CraftActionName", id, row => row.Name, names: true);
    public string ActionName(uint id) => Read<Lumina.Excel.Sheets.Action>("ActionName", id, row => row.Name, names: true);
    public string BuddyActionName(uint id) => Read<BuddyAction>("BuddyActionName", id, row => row.Name, names: true);
    public string GeneralActionName(uint id) => Read<GeneralAction>("GeneralActionName", id, row => row.Name, names: true);
    public string PetActionName(uint id) => Read<PetAction>("PetActionName", id, row => row.Name, names: true);
    public string GeneralAction(uint id) => Read<GeneralAction>("GeneralAction", id, row => row.Description);
    public string PetAction(uint id) => Read<PetAction>("PetAction", id, row => row.Description);
    public string MainCommandName(uint id) => Read<MainCommand>("MainCommandName", id, row => row.Name, names: true);
    public string MainCommand(uint id) => Read<MainCommand>("MainCommand", id, row => row.Description);
    public string EmoteName(uint id) => Read<Emote>("EmoteName", id, row => row.Name, names: true);
    public string ItemName(uint id)
    {
        var name = Read<Item>("ItemName", id, row => row.Name, names: true);
        return _data.GetExcelSheet<Item>().TryGetRow(id, out var item) ? EquipmentSpeech.WithSlots(item, name) : name;
    }
    public string EventItemName(uint id) => Read<EventItem>("EventItemName", id, row => row.Name, names: true);
    public string TraitName(uint id) => Read<Trait>("TraitName", id, row => row.Name, names: true);
    public string MountName(uint id) => Read<Mount>("MountName", id, row => row.Singular, names: true);
    public string CompanionName(uint id) => Read<Companion>("CompanionName", id, row => row.Singular, names: true);
    public string StatusName(uint id) => Read<Status>("StatusName", id, row => row.Name, names: true);
    public string Status(uint id) => Read<Status>("StatusDescription", id, row => row.Description, names: true);

    public string BuddyActionNameFromPanel(string fallback)
    {
        if (!Loc.IsRussianItemActionText && !GameDisplayText.IsAvailable) return fallback;
        string? translated = null;
        foreach (var row in _data.GetExcelSheet<BuddyAction>())
        {
            if (!PanelNameMatches(row, x => x.Name, fallback))
                continue;
            var name = BuddyActionName(row.RowId);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (translated != null && translated != name) return fallback;
            translated = name;
        }
        return translated ?? fallback;
    }

    public string TraitFromPanel(string name, int level, string fallback)
    {
        if (!Loc.IsRussianItemActionText && !GameDisplayText.IsAvailable) return fallback;
        string? resolved = null;
        foreach (var row in _data.GetExcelSheet<Trait>())
        {
            if (row.Level != level || !PanelNameMatches(row, x => x.Name, name))
                continue;
            var description = Trait(row.RowId);
            if (string.IsNullOrWhiteSpace(description)) continue;
            // Duplicate names shared by several jobs are safe only when their
            // descriptions agree. Never guess an id from a partial name.
            if (resolved != null && resolved != description) return fallback;
            resolved = description;
        }
        return resolved ?? fallback;
    }

    public string TraitNameFromPanel(string name, int level)
    {
        if (!Loc.IsRussianItemActionText && !GameDisplayText.IsAvailable) return name;
        string? resolved = null;
        foreach (var row in _data.GetExcelSheet<Trait>())
        {
            if (row.Level != level || !PanelNameMatches(row, x => x.Name, name))
                continue;
            var translated = TraitName(row.RowId);
            if (string.IsNullOrWhiteSpace(translated)) continue;
            if (resolved != null && resolved != translated) return name;
            resolved = translated;
        }
        return resolved ?? name;
    }

    private bool PanelNameMatches<T>(T row, Func<T, ReadOnlySeString> field, string label)
        where T : struct, IExcelRow<T>
        => string.Equals(field(row).ExtractText().Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(GameDisplayText.Find(_data, row, field)?.ExtractText().Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase);

    private string Read<T>(string table, uint id, Func<T, ReadOnlySeString> field, bool names = false)
        where T : struct, IExcelRow<T>
    {
        if (id == 0 || !_data.GetExcelSheet<T>().TryGetRow(id, out var row)) return string.Empty;
        var original = field(row);
        if (GameDisplayText.Find(_data, row, field) is { } displayed)
        {
            // Preserve Prima's SeString and dynamic parameters; never apply
            // the older catalog's terminology substitutions to its text.
            try { return _evaluator.Evaluate(displayed).ExtractText(); }
            catch (Exception ex) { ReportOnce(table, id, ex.Message); return displayed.ExtractText(); }
        }
        var catalog = names ? _russianNames : _russian;
        if (GameTextTranslation.ShouldTranslate(typeof(T).Name) && catalog != null)
        {
            try
            {
                if (_data.GetExcelSheet<T>(ClientLanguage.English).TryGetRow(id, out var englishRow))
                {
                    var translated = catalog.Find(table, id, field(englishRow).Data.Span, russian: true);
                    if (translated != null)
                    {
                        // Evaluate each time: levels/traits change, so a cached spoken
                        // string could give the wrong potency or crafting efficiency.
                        var result = _evaluator.Evaluate(new ReadOnlySeString(translated)).ExtractText();
                        if (!string.IsNullOrWhiteSpace(result))
                            return names && table.EndsWith("Name", StringComparison.Ordinal)
                                ? result : RussianDescriptionTerms.Translate(result, russian: true);
                    }
                    ReportOnce(table, id, "no matching Russian source");
                }
            }
            catch (Exception ex) { ReportOnce(table, id, ex.Message); }
        }
        // Keep the game text when a translation is missing, stale or unreadable.
        try { return _evaluator.Evaluate(original).ExtractText(); }
        catch (Exception ex)
        {
            ReportOnce(table, id, ex.Message);
            return original.ExtractText();
        }
    }

    private void ReportOnce(string table, uint id, string reason)
    {
        if (_reported.Add($"{table}:{id}"))
            _log.Debug($"[Descriptions] {table}/{id}: {reason}; using game text.");
    }
}
