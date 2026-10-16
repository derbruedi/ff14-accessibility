using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private TableReader? _tableReader;
    private string _tableAddonName = string.Empty;
    private nint _tableAddonAddress;
    private int _tableCharacterTab = -1;
    private bool _tableDrainKeys;
    public bool IsTableReading => _tableReader != null;
    public static IEnumerable<int> TableKeys => ShopQuantityKeys;

    private static bool IsCharacterPanel(string name) => name is "Character" or "CharacterStatus"
        or "CharacterProfile" or "CharacterClass" or "CharacterRepute";

    private unsafe AtkUnitBase* TableOwner()
    {
        var stage = AtkStage.Instance();
        var owner = stage == null || stage->AtkInputManager == null ? null
            : FindAddonForNode(stage->AtkInputManager->FocusedNode);
        if (owner == null || !owner->IsVisible || IsSuppressedCombatHud(owner->NameString)) return null;
        if (IsCharacterPanel(owner->NameString))
        {
            var character = _gameGui.GetAddonByName("Character");
            if (!character.IsNull && ((AtkUnitBase*)(nint)character)->IsVisible)
                return (AtkUnitBase*)(nint)character;
        }
        return owner;
    }

    public unsafe bool BeginTableReading()
    {
        if (IsChatInputActive() || IsShopQuantityEditing) return false;
        var owner = TableOwner();
        if (owner == null) { _tolk.SpeakInterrupt(AccessibilityStrings.TableEmpty); return false; }
        var reader = new TableReader();
        if (!reader.Refresh(ReadTableRows(owner, trace: true))) { _tolk.SpeakInterrupt(AccessibilityStrings.TableEmpty); return false; }
        _tableReader = reader;
        _tableAddonName = owner->NameString;
        _tableAddonAddress = (nint)owner;
        _tableCharacterTab = owner->NameString == "Character" ? ((AddonCharacter*)owner)->TabIndex : -1;
        _itemDwellArmed = false;
        _actionDwellDescSpoken = true;
        _tolk.SpeakInterrupt(AccessibilityStrings.TableInstructions + " " + reader.SpeakRow());
        _log.Info($"[TableReader] Open {_tableAddonName}: {reader.Rows.Count} rows, read-only.");
        for (var i = 0; i < reader.Rows.Count; i++)
        {
            var row = reader.Rows[i];
            _log.Info($"[TableReader] Row {i + 1}: section='{row.Section}' fields=[{string.Join(" | ", row.Cells.Select(c => c.Text))}]");
        }
        return true;
    }

    public unsafe bool HandleTableKeys(MenuInput input, bool windowActive, bool textInput, bool ctrl, bool shift, bool alt)
    {
        if (_tableDrainKeys)
        {
            _tableDrainKeys = input.AnyDown;
            if (_tableDrainKeys) { input.ConsumeAll(); return true; }
        }
        var reader = _tableReader;
        if (reader == null) return false;
        input.ConsumeAll();
        var owner = TableOwner(); // Resolve anew; never dereference the saved address.
        if (!windowActive || textInput || owner == null || (nint)owner != _tableAddonAddress
            || owner->NameString != _tableAddonName
            || (_tableCharacterTab >= 0 && ((AddonCharacter*)owner)->TabIndex != _tableCharacterTab))
        {
            EndTableReading(AccessibilityStrings.TableChanged);
            return true;
        }
        if (input.Just(0x1B) || input.Just(0x6E)) { EndTableReading(AccessibilityStrings.TableClosed); return true; }
        if (ctrl || alt) return true;
        var rowDelta = input.JustAny(0x26, 0x68) ? -1 : input.JustAny(0x28, 0x62) ? 1
            : input.Just(0x21) ? -10 : input.Just(0x22) ? 10
            : input.Just(0x24) ? -int.MaxValue : input.Just(0x23) ? int.MaxValue / 2 : 0;
        var colDelta = input.JustAny(0x25, 0x64) ? -1 : input.JustAny(0x27, 0x66) ? 1 : 0;
        if (rowDelta == 0 && colDelta == 0 && !input.JustAny(0x09, 0x20, 0x65, 0x0C, 0x0D, 0x60)) return true;
        if (!reader.Refresh(ReadTableRows(owner))) { EndTableReading(AccessibilityStrings.TableEmpty); return true; }
        if (input.JustAny(0x0D, 0x60)) { _tolk.SpeakInterrupt(AccessibilityStrings.TableInstructions); return true; }
        if (rowDelta != 0) reader.MoveRow(rowDelta);
        if (colDelta != 0) reader.MoveColumn(colDelta);
        if (input.Just(0x09)) reader.MoveSection(shift ? -1 : 1);
        _tolk.SpeakInterrupt(colDelta != 0 ? reader.SpeakCell() : reader.SpeakRow());
        return true;
    }

    private void EndTableReading(string text)
    {
        _tableReader = null;
        _tableDrainKeys = true;
        _lastFocusedNodePtr = 0;
        _tolk.SpeakInterrupt(text);
    }

    private unsafe IReadOnlyList<TableReader.Row> ReadTableRows(AtkUnitBase* owner, bool trace = false)
    {
        var rows = new List<TableReader.Row>();
        // In C the requested attributes come first; window controls follow them.
        if (owner->NameString != "Character") AddTableRows(owner, rows, trace);
        // Character attributes/profile are separate addons. Include only actual
        // visible children, or the known Character panels, never another window.
        var manager = RaptureAtkUnitManager.Instance();
        if (manager != null)
            for (var i = 0; i < manager->AllLoadedUnitsList.Count && i < 256; i++)
            {
                var child = manager->AllLoadedUnitsList.Entries[i].Value;
                if (child == null || child == owner || !child->IsVisible) continue;
                if ((owner->Id != 0 && (child->ParentId == owner->Id || child->HostId == owner->Id))
                    || (owner->NameString == "Character" && IsCharacterPanel(child->NameString)))
                {
                    if (child->NameString is "ActionDetail" or "ItemDetail" or "Tooltip") continue;
                    AddTableRows(child, rows, trace);
                }
            }
        if (owner->NameString == "Character") AddTableRows(owner, rows, trace);
        return rows;
    }

    private unsafe void AddTableRows(AtkUnitBase* addon, List<TableReader.Row> rows, bool trace = false)
    {
        var title = ReadWindowTitle(addon);
        if (string.IsNullOrWhiteSpace(title)) title = addon->NameString switch
        {
            "CharacterStatus" => AccessibilityStrings.CharacterTabFallback(0),
            "CharacterProfile" => AccessibilityStrings.CharacterTabFallback(1),
            "CharacterClass" => AccessibilityStrings.CharacterTabFallback(2),
            "CharacterRepute" => AccessibilityStrings.CharacterTabFallback(3),
            _ => AccessibilityStrings.TableWindow,
        };
        if (addon->NameString == "CharacterStatus"
            && CharacterStatusTable.TryRead(addon, title,
                ptr => FlattenDescription(AtkText.Read((AtkTextNode*)ptr)), out var statusRows))
        {
            if (trace) _log.Info("[TableReader] CharacterStatus: verified container layout.");
            rows.AddRange(statusRows);
            return;
        }
        if (trace && addon->NameString == "CharacterStatus")
            _log.Info("[TableReader] CharacterStatus: layout unavailable; reading separate visible controls.");
        var cells = new List<TableReader.Cell>();
        var visited = new HashSet<nint>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            CollectTableCells(addon->UldManager.NodeList[i], cells, visited, 0);
        rows.AddRange(TableReader.Arrange(title, cells));
    }

    private unsafe void CollectTableCells(AtkResNode* node, List<TableReader.Cell> cells, HashSet<nint> visited, int depth)
        => TableNodeReader.Collect(node, cells, visited,
            ptr => FlattenDescription(AtkText.Read((AtkTextNode*)ptr)),
            ptr => FindTooltipInSubtree((AtkResNode*)ptr) is { } tip ? FlattenDescription(tip) : null, depth);

    private unsafe bool TryReadCharacterStat(AtkResNode* node, out string text)
    {
        text = string.Empty;
        if (FindAddonNameForNode(node) != "CharacterStatus") return false;
        for (var i = 0; node != null && i < 6; i++, node = node->ParentNode)
        {
            if ((int)node->Type < 1000) continue;
            if (CharacterStatusTable.TryStat(node,
                ptr => FlattenDescription(AtkText.Read((AtkTextNode*)ptr)), out var stat))
            {
                text = $"{stat[0].Text}: {stat[1].Text}";
                return true;
            }
            var cells = new List<TableReader.Cell>();
            CollectTableCells(node, cells, [], 0);
            if (cells.Count < 2) return false;
            text = string.Join("; ", TableReader.Arrange("", cells).SelectMany(r => r.Cells).Select(c => c.Text));
            return true;
        }
        return false;
    }
}
