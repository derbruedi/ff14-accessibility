using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private int _gatherCategorySeen = -1;
    private int _gatherCategorySpoken = -1;
    private int _gatherPaneSeen = -1;

    private unsafe (int Index, string Flags) CheckedGatherCategory(AtkUnitBase* addon)
    {
        var index = -1;
        var flags = new StringBuilder();
        for (var i = 0; i < 4; i++)
        {
            var component = FindTopComponent(addon, (uint)(8 + i));
            if (component == null || component->GetComponentType() != ComponentType.CheckBox) continue;
            var selected = ((AtkComponentButton*)component)->IsChecked;
            flags.Append($" [{8 + i}:{selected}]");
            if (selected && index < 0) index = i;
        }
        return (index, flags.ToString());
    }

    private string GatheringNoteCategoryName(int index)
    {
        var sheet = _data.GetExcelSheet<Lumina.Excel.Sheets.GatheringType>();
        return sheet.TryGetRow((uint)index, out var row) ? RussianGameText.Name(_data, row, x => x.Name).Trim() : string.Empty;
    }

    private unsafe void LogGatheringNoteCategoryChange(AtkUnitBase* addon)
    {
        var (index, flags) = CheckedGatherCategory(addon);
        if (index == _gatherCategorySeen) return;
        _gatherCategorySeen = index;
        _log.Info(index < 0
            ? $"[Sammel-Journal] Keine Liste als offen erkannt.{flags}"
            : $"[Sammel-Journal] Offene Liste: {index + 1}/4 '{GatheringNoteCategoryName(index)}'{flags}");
    }

    private unsafe int GatheringPaneOf(AtkUnitBase* addon, AtkResNode* node)
    {
        if (addon == null || node == null) return -1;
        var current = node;
        for (var depth = 0; current != null && depth < 8; depth++, current = current->ParentNode)
        {
            if ((int)current->Type < 1000) continue;
            var component = ((AtkComponentNode*)current)->Component;
            if (component == null) continue;
            for (var i = 0; i < 4; i++)
                if (component == FindTopComponent(addon, (uint)(8 + i))) return 0;
            if (component == FindTopComponent(addon, 16)) return 1;
            if (component == FindTopComponent(addon, 21)) return 2;
            if (component == FindTopComponent(addon, 19)) return 3;
        }
        var ids = new uint[] { 16, 21, 19 };
        for (var pane = 0; pane < ids.Length; pane++)
        {
            var top = FindTopComponent(addon, ids[pane]);
            if (top == null) continue;
            var uld = &top->UldManager;
            for (var i = 0; i < uld->NodeListCount; i++)
            {
                var child = uld->NodeList[i];
                if (child == null || (int)child->Type < 1000) continue;
                var component = ((AtkComponentNode*)child)->Component;
                if (component != null && (child == node || IsNodeInComponent(component, node))) return pane + 1;
            }
        }
        return -1;
    }

    private unsafe string GatheringContextPrefix(AtkResNode* node, string text)
    {
        if (text.Length == 0 || FindAddonNameForNode(node) != GatheringNoteAddon) return text;
        var ptr = _gameGui.GetAddonByName(GatheringNoteAddon);
        var addon = ptr.IsNull ? null : (AtkUnitBase*)(nint)ptr;
        if (addon == null || !addon->IsVisible) return text;
        var context = new List<string>();
        var category = CheckedGatherCategory(addon).Index;
        if (category >= 0 && category != _gatherCategorySpoken)
        {
            _gatherCategorySpoken = category;
            var label = GatheringNoteCategoryName(category);
            context.Add(label.Length > 0 ? label : AccessibilityStrings.GatherNoteListPosition(category + 1, 4));
        }
        var pane = GatheringPaneOf(addon, node);
        if (pane >= 0 && pane != _gatherPaneSeen)
        {
            _gatherPaneSeen = pane;
            var label = AccessibilityStrings.GatherNotePane(pane);
            if (label.Length > 0) context.Add(label);
        }
        return context.Count > 0 ? string.Join(", ", context) + ": " + text : text;
    }

    private unsafe List<nint> CollectSupplyRows(AtkUnitBase* addon)
    {
        var rows = new List<(uint Id, nint Ptr)>();
        var uld = &addon->UldManager;
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node != null && (int)node->Type == 1008 && IsEffectivelyVisible(node)
                && !string.IsNullOrWhiteSpace(ReadRowText(node, 4)))
                rows.Add((node->NodeId, (nint)node));
        }
        return rows.OrderBy(row => row.Id).Select(row => row.Ptr).ToList();
    }

    private unsafe string ReadSupplyRow(AtkResNode* row)
    {
        var name = ReadRowText(row, 4);
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var count = ReadRowText(row, 7);
        _log.Info($"[Versorgung] Platz '{name}' angefordert='{count}' zweiteZahl='{ReadRowText(row, 8)}'");
        return string.IsNullOrWhiteSpace(count)
            ? AccessibilityStrings.SupplyRowNoCount(name)
            : AccessibilityStrings.SupplyRow(name, count);
    }

    private unsafe bool TryReadSupplyPaneDetail()
    {
        var ptr = _gameGui.GetAddonByName("ContentsInfoDetail");
        var addon = ptr.IsNull ? null : (AtkUnitBase*)(nint)ptr;
        if (addon == null || !addon->IsVisible) return false;
        var firstLabel = ReadTopLevelText(addon, 3);
        var secondLabel = ReadTopLevelText(addon, 16);
        if (firstLabel.Length == 0 && secondLabel.Length == 0) return false;
        var first = new StringBuilder();
        var second = new StringBuilder();
        foreach (var rowPtr in CollectSupplyRows(addon))
        {
            var row = (AtkResNode*)rowPtr;
            var value = ReadSupplyRow(row);
            if (value.Length == 0) continue;
            (row->NodeId > 16 ? second : first).Append(value).Append(". ");
        }
        var sections = new List<string>();
        if (first.Length > 0) sections.Add(SupplySection(firstLabel, first.ToString().Trim()));
        if (second.Length > 0) sections.Add(SupplySection(secondLabel, second.ToString().Trim()));
        var message = sections.Count > 0 ? string.Join(" ", sections) : AccessibilityStrings.SupplyPaneEmpty;
        _tolk.SpeakInterrupt(message);
        return true;
    }

    private static string SupplySection(string label, string rows)
        => label.Length > 0 ? AccessibilityStrings.SupplySection(label, rows) : AccessibilityStrings.SupplyPaneIntro(rows);
}
