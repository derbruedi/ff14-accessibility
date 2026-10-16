using Dalamud.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using DetailKind = FFXIVClientStructs.FFXIV.Client.Enums.DetailKind;

namespace FF14Accessibility.Services;

public sealed partial class GameDescriptionService
{
    private readonly Dictionary<(string Name, int Level, LanguageMode Mode, bool Translate, int Display), (DetailKind Kind, uint Id)[]> _menuMatches = new();
    private readonly Dictionary<(Type Sheet, int Display), Dictionary<string, List<uint>>> _displayMenuNames = new();

    internal ActionMenuText.Entry FromActionMenuLabel(string label, string panelDescription = "", IReadOnlySet<uint>? menuTraitIds = null)
    {
        var (name, level) = ActionMenuText.ParseLabel(label);
        var key = (name, level, Loc.Mode, Loc.TranslateItemsAndActions, GameDisplayText.Revision);
        if (!_menuMatches.TryGetValue(key, out var matches))
        {
            var found = new List<(DetailKind, uint)>();
            FindMenuRows<Trait>(name, level, row => row.Name, row => row.Level, DetailKind.Trait, found);
            FindMenuRows<Lumina.Excel.Sheets.Action>(name, level, row => row.Name, row => row.ClassJobLevel, DetailKind.Action, found);
            FindMenuRows<CraftAction>(name, 0, row => row.Name, _ => 0, DetailKind.CraftingAction, found);
            FindMenuRows<GeneralAction>(name, 0, row => row.Name, _ => 0, DetailKind.GeneralAction, found);
            FindMenuRows<PetAction>(name, 0, row => row.Name, _ => 0, DetailKind.PetOrder, found);
            FindMenuRows<BuddyAction>(name, 0, row => row.Name, _ => 0, DetailKind.BuddyAction, found);
            matches = found.Distinct().ToArray();
            if (_menuMatches.Count >= 512) _menuMatches.Clear();
            _menuMatches[key] = matches;
        }
        var fallback = new ActionMenuText.Entry(name, panelDescription);
        return ActionMenuText.Consensus(matches.Where(m => m.Kind != DetailKind.Trait || menuTraitIds == null || menuTraitIds.Contains(m.Id))
            .Select(m => new ActionMenuText.Entry(MenuName(m.Kind, m.Id), MenuDescription(m.Kind, m.Id))), fallback);
    }

    private void FindMenuRows<T>(string name, int level, Func<T, ReadOnlySeString> nameField, Func<T, int> levelField,
        DetailKind kind, List<(DetailKind, uint)> found) where T : struct, IExcelRow<T>
    {
        // Current and English names cover translated UI files and an English
        // client. Exact matches only; duplicate names require consensus later.
        var current = _data.GetExcelSheet<T>();
        var english = _data.GetExcelSheet<T>(ClientLanguage.English);
        foreach (var row in current)
        {
            if (level > 0 && levelField(row) != level) continue;
            var match = nameField(row).ExtractText().Trim().Equals(name, StringComparison.OrdinalIgnoreCase)
                || (english.TryGetRow(row.RowId, out var en) && nameField(en).ExtractText().Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match) found.Add((kind, row.RowId));
        }
        if (GameDisplayText.IsAvailable && _data.Language == ClientLanguage.English)
        {
            var displayKey = (typeof(T), GameDisplayText.Revision);
            if (!_displayMenuNames.TryGetValue(displayKey, out var index))
            {
                index = new(StringComparer.OrdinalIgnoreCase);
                foreach (var row in current)
                    if (GameDisplayText.Find(_data, row, nameField) is { } display)
                    {
                        var label = display.ExtractText().Trim();
                        if (!index.TryGetValue(label, out var ids)) index[label] = ids = [];
                        ids.Add(row.RowId);
                    }
                if (_displayMenuNames.Count >= 12) _displayMenuNames.Clear();
                _displayMenuNames[displayKey] = index;
            }
            if (index.TryGetValue(name, out var matches))
                foreach (var id in matches)
                    if (current.TryGetRow(id, out var row) && (level == 0 || levelField(row) == level))
                        found.Add((kind, id));
        }
        if (GameTextTranslation.ShouldTranslate(typeof(T).Name) && _russianNames != null)
            foreach (var id in _russianNames.FindNameCandidates(typeof(T).Name + "Name", name))
                if (current.TryGetRow(id, out var row) && (level == 0 || levelField(row) == level)
                    && MenuName(kind, id).Equals(name, StringComparison.OrdinalIgnoreCase))
                    found.Add((kind, id));
    }

    internal string MenuName(DetailKind kind, uint id) => kind switch
    {
        DetailKind.Action => ActionName(id), DetailKind.CraftingAction => CraftActionName(id),
        DetailKind.Trait => TraitName(id), DetailKind.GeneralAction => GeneralActionName(id),
        DetailKind.PetOrder => PetActionName(id), DetailKind.BuddyAction or DetailKind.BuddyOrder => BuddyActionName(id),
        DetailKind.MainCommand => MainCommandName(id), _ => "",
    };
    internal string MenuDescription(DetailKind kind, uint id) => kind switch
    {
        DetailKind.Action => Action(id), DetailKind.CraftingAction => CraftAction(id),
        DetailKind.Trait => Trait(id), DetailKind.GeneralAction => GeneralAction(id),
        DetailKind.PetOrder => PetAction(id), DetailKind.BuddyAction or DetailKind.BuddyOrder => BuddyAction(id),
        DetailKind.MainCommand => MainCommand(id), _ => "",
    };
}
