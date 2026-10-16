using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private sealed class PerformanceFacts
    {
        public readonly List<string> Parts = [];
        public string State = string.Empty;
        public string Switches = string.Empty;
    }

    private string _perfStateSnapshot = string.Empty;
    private string _perfSwitchSnapshot = string.Empty;
    private long _perfStateAt;

    private unsafe void AnnouncePerformanceMode(AtkUnitBase* addon)
    {
        var facts = CollectPerformanceFacts(addon);
        _perfStateSnapshot = facts.State;
        _perfSwitchSnapshot = facts.Switches;
        _perfStateAt = Environment.TickCount64;
        if (facts.Parts.Count > 0) _tolk.Speak(string.Join(". ", facts.Parts));
    }

    public unsafe void PerformanceModeTick()
    {
        if (!IsAddonVisible("PerformanceMode"))
        {
            _perfStateSnapshot = _perfSwitchSnapshot = string.Empty;
            return;
        }
        var now = Environment.TickCount64;
        if (now - _perfStateAt < 400) return;
        _perfStateAt = now;
        var ptr = _gameGui.GetAddonByName("PerformanceMode");
        if (ptr.IsNull) return;
        var facts = CollectPerformanceFacts((AtkUnitBase*)(nint)ptr);
        if (_perfStateSnapshot.Length == 0 && _perfSwitchSnapshot.Length == 0)
        {
            _perfStateSnapshot = facts.State;
            _perfSwitchSnapshot = facts.Switches;
            if (facts.Parts.Count > 0) _tolk.Speak(string.Join(". ", facts.Parts));
            return;
        }
        if (facts.Switches.Length > 0 && facts.Switches != _perfSwitchSnapshot)
        {
            _perfSwitchSnapshot = facts.Switches;
            _tolk.Speak(facts.Switches);
        }
        if (facts.State.Length > 0 && facts.State != _perfStateSnapshot)
        {
            _perfStateSnapshot = facts.State;
            _tolk.Speak(facts.State);
        }
    }

    private unsafe PerformanceFacts CollectPerformanceFacts(AtkUnitBase* addon)
    {
        var keys = new List<(float X, string Key, string Note)>();
        var help = new List<(float Y, string Effect, string Key)>();
        var lines = new List<(float Y, string Text)>();
        var boxes = 0;
        var boxesOn = 0;
        var plain = 0;
        var uld = &addon->UldManager;
        for (var i = 0; i < uld->NodeListCount; i++)
            WalkPerformanceNode(uld->NodeList[i], 0, keys, help, lines, ref boxes, ref boxesOn, ref plain);
        var facts = new PerformanceFacts();
        if (keys.Count > 0)
            facts.Parts.Add(AccessibilityStrings.PerformanceKeyboard(string.Join(", ",
                keys.OrderBy(key => key.X).Select(key => key.Key + " — " + AccessibilityStrings.PerformanceNote(key.Note)))));
        if (help.Count > 0)
            facts.Parts.Add(AccessibilityStrings.PerformanceHelpKeys(string.Join(", ",
                help.OrderBy(item => item.Y).Select(item => item.Key + " — " + item.Effect))));
        foreach (var line in lines.OrderBy(item => item.Y)) facts.Parts.Add(line.Text);
        if (boxes + plain > 0) facts.Parts.Add(AccessibilityStrings.PerformanceSwitches(boxes, boxesOn, plain));
        facts.State = string.Join(" ", lines.OrderBy(item => item.Y).Select(item => item.Text)).Trim();
        facts.Switches = boxes + plain > 0 ? AccessibilityStrings.PerformanceSwitches(boxes, boxesOn, plain) : string.Empty;
        return facts;
    }

    private unsafe void WalkPerformanceNode(AtkResNode* node, int depth,
        List<(float X, string Key, string Note)> keys,
        List<(float Y, string Effect, string Key)> help,
        List<(float Y, string Text)> lines,
        ref int boxes, ref int boxesOn, ref int plain)
    {
        if (node == null || depth > 8) return;
        if ((int)node->Type >= 1000)
        {
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
            {
                var type = (int)component->GetComponentType();
                if (type == 0)
                {
                    var texts = PerformanceVisibleTexts(component).OrderBy(t => t.Y).ToList();
                    if (texts.Count >= 2 && texts[0].Text != texts[^1].Text)
                    {
                        keys.Add((node->ScreenX, texts[0].Text, texts[^1].Text));
                        return;
                    }
                }
                else if (type == 3 && node->Width <= 64 && node->Height <= 64)
                {
                    boxes++;
                    if (((AtkComponentButton*)component)->IsChecked) boxesOn++;
                    return;
                }
                else if (type == 1 && node->Width <= 64 && node->Height <= 64
                         && PerformanceVisibleTexts(component).Count == 0)
                {
                    plain++;
                    return;
                }
                else if (type == 19)
                {
                    foreach (var item in PerformanceVisibleTexts(component)) lines.Add((item.Y, item.Text));
                    return;
                }
                else if (type == 9)
                {
                    var childUld = &component->UldManager;
                    for (var i = 0; i < childUld->NodeListCount; i++)
                    {
                        var child = childUld->NodeList[i];
                        if (child == null || (int)child->Type < 1000) continue;
                        var sub = ((AtkComponentNode*)child)->Component;
                        if (sub == null || (int)sub->GetComponentType() != 14) continue;
                        var texts = PerformanceVisibleTexts(sub).OrderBy(t => t.X).ToList();
                        if (texts.Count >= 2) help.Add((child->ScreenY, texts[^1].Text, texts[0].Text));
                    }
                    return;
                }
            }
        }
        for (var child = node->ChildNode; child != null; child = child->PrevSiblingNode)
            WalkPerformanceNode(child, depth + 1, keys, help, lines, ref boxes, ref boxesOn, ref plain);
    }

    private unsafe static List<(float X, float Y, string Text)> PerformanceVisibleTexts(AtkComponentBase* component)
    {
        var texts = new List<(float, float, string)>();
        var uld = &component->UldManager;
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null || node->Type != NodeType.Text || !node->IsVisible()) continue;
            var text = AtkText.ReadClean((AtkTextNode*)node).Trim();
            if (text.Length > 0) texts.Add((node->ScreenX, node->ScreenY, text));
        }
        return texts;
    }

    public unsafe void DumpPerformanceDetail()
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PerformanceDumpFailed);
            return;
        }
        var output = new StringBuilder($"=== Auftrittssonde | {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
        var addonCount = 0;
        var budget = 800;
        var units = &manager->AllLoadedUnitsList;
        for (var i = 0; i < units->Count && i < 256; i++)
        {
            var addon = units->Entries[i].Value;
            if (addon == null || !addon->NameString.Contains("Perf", StringComparison.OrdinalIgnoreCase)) continue;
            addonCount++;
            output.AppendLine($"=== ADDON: {addon->NameString} | sichtbar={addon->IsVisible} | Knoten={addon->UldManager.NodeListCount} ===");
            for (var nodeIndex = 0; nodeIndex < addon->UldManager.NodeListCount && budget > 0; nodeIndex++)
                DumpPerformanceNode(output, addon->UldManager.NodeList[nodeIndex], 0, nodeIndex, ref budget);
        }
        if (addonCount == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PerformanceDumpNoAddon);
            return;
        }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FF14_Auftritt.txt");
            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            _log.Info("[Auftrittssonde] Gespeichert: " + path);
            _tolk.SpeakInterrupt(AccessibilityStrings.PerformanceDumpSaved(addonCount, 800 - budget));
        }
        catch (Exception ex)
        {
            _log.Warning("[Auftrittssonde] Datei-Fehler: " + ex.Message);
            _tolk.SpeakInterrupt(AccessibilityStrings.PerformanceDumpFailed);
        }
    }

    private unsafe void DumpPerformanceNode(StringBuilder output, AtkResNode* node,
        int depth, int index, ref int budget)
    {
        if (node == null || depth > 8 || budget <= 0) return;
        budget--;
        var type = (int)node->Type;
        var detail = string.Empty;
        if (node->Type == NodeType.Text)
            detail = " text=\"" + AtkText.Read((AtkTextNode*)node).Replace("\n", "\\n").Trim() + "\"";
        else if (type >= 1000)
        {
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
            {
                var componentType = component->GetComponentType();
                detail = $" [CT={componentType} Ch={component->UldManager.NodeListCount}]";
                if (componentType is ComponentType.CheckBox or ComponentType.RadioButton)
                    detail += ((AtkComponentButton*)component)->IsChecked ? " [Checked]" : " [unchecked]";
                var tip = _tooltips.TryGetTooltipDeep(node);
                if (!string.IsNullOrWhiteSpace(tip)) detail += " Tooltip=\"" + tip.Trim() + "\"";
            }
        }
        output.AppendLine($"{new string(' ', depth * 2)}[{index}] id={node->NodeId} T{type} " +
                          $"F=0x{(ushort)node->NodeFlags:X4} {(node->IsVisible() ? "V" : "-")} " +
                          $"@{node->ScreenX:F0},{node->ScreenY:F0} {node->Width}x{node->Height}{detail}");
        if (type < 1000) return;
        var comp = ((AtkComponentNode*)node)->Component;
        if (comp == null) return;
        for (var i = 0; i < comp->UldManager.NodeListCount && budget > 0; i++)
            DumpPerformanceNode(output, comp->UldManager.NodeList[i], depth + 1, i, ref budget);
    }
}
