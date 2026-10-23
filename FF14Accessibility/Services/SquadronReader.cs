using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Globalization;

namespace FF14Accessibility.Services;

/// <summary>Read only the visible squadron UI. Field bindings are checked against
/// the installed ULDs by tools/squadron-layout-check; no agent/game-state writes.</summary>
internal static unsafe class SquadronReader
{
    internal static bool Supports(string name) => name.StartsWith("GcArmy", StringComparison.Ordinal);
    private sealed record Part(nint[] Sources, string Text, bool FocusOnly = false);

    internal static string Page(string name, AtkUnitBase* addon, Func<nint, string> read,
        Func<nint, string>? readItem = null)
        => Join(Parts(name, addon, read, readItem).Where(p => !p.FocusOnly).Select(p => p.Text));

    internal static string Focus(string name, AtkUnitBase* addon, AtkResNode* focus,
        Func<nint, string> read, Func<nint, string?> tooltip, Func<nint, string>? readItem = null)
    {
        if (!Supports(name) || addon == null || !addon->IsVisible || addon->NumBlockingAddons != 0 || !Visible(focus)
            || !NativeControlScope.Owns(addon, focus)) return string.Empty;
        // Browsing training reads only its name. Live preview numbers and the
        // longer tooltip belong to the deliberate read command, not each frame.
        if (name == "GcArmyTraining") return TrainingFocus(addon, focus, read, tooltip);
        var control = NativeControlScope.Control(addon, focus);
        var parts = Parts(name, addon, read, readItem);
        // A stale renderer may retain focus/parent pointers after losing its
        // logical row. Do not bypass the live-row check through generic text.
        var ancestor = focus;
        for (var depth = 0; ancestor != null && depth < 64; ancestor = ancestor->ParentNode, depth++)
        {
            if (!IsType(Manager(ancestor), ComponentType.ListItemRenderer)) continue;
            var row = (nint)ancestor;
            if (!parts.Any(p => p.Sources.Any(s => NativeDialogButtons.Contains((AtkResNode*)row, (AtkResNode*)s))))
                return string.Empty;
            break;
        }
        // Structured row/attribute owners outrank the textless icon under focus.
        var matches = parts.Where(p => p.Sources.Any(s => (nint)focus == s || (nint)control == s
            || NativeDialogButtons.Contains((AtkResNode*)s, focus)
            || (control != null && NativeDialogButtons.Contains(control, (AtkResNode*)s)))).Select(p => p.Text);
        var text = Join(matches);
        if (text.Length == 0 && control != null) text = Read(control, read);
        var hint = tooltip((nint)focus) ?? (control == null ? null : tooltip((nint)control)) ?? "";
        if (text.Length == 0) return hint;
        return hint.Length > 0 && !text.Contains(hint, StringComparison.OrdinalIgnoreCase)
            ? Join([text, hint]) : text;
    }

    internal static bool IsItemReward(string name, AtkUnitBase* addon, AtkResNode* focus)
    {
        var id = name == "GcArmyExpedition" ? 22u : name == "GcArmyExpeditionResult" ? 15u : 0;
        return id != 0 && addon != null && NativeDialogButtons.Contains(Node(&addon->UldManager, id), focus);
    }

    private static List<Part> Parts(string name, AtkUnitBase* addon, Func<nint, string> read,
        Func<nint, string>? readItem)
    {
        List<Part> result = [];
        if (!Supports(name) || addon == null || !addon->IsVisible || addon->NumBlockingAddons != 0
            || !Valid(&addon->UldManager)) return result;
        var used = new HashSet<nint>();
        void Add(string text, params nint[] sources)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            result.Add(new(sources.Where(s => s != 0).ToArray(), text));
            foreach (var source in sources) Consume((AtkResNode*)source, used);
        }
        string Value(uint id) => Read(Node(&addon->UldManager, id), read);
        nint Address(uint id) => (nint)Node(&addon->UldManager, id);
        void Field(uint id, string label, uint icon = 0)
        {
            var value = Value(id);
            if (value.Length > 0) Add(Label(label, value), Address(id), Address(icon));
        }
        void Pair(uint label, uint value, string fallback)
        {
            var text = Value(value);
            if (text.Length > 0) Add(Label(Value(label) is { Length: > 0 } l ? l : fallback, text),
                Address(label), Address(value));
        }
        void Item(uint id)
        {
            var owner = Node(&addon->UldManager, id);
            if (!Visible(owner) || readItem == null) return;
            var item = readItem((nint)owner);
            if (item.Length > 0) Add(Label(AccessibilityStrings.SquadronReward, item), (nint)owner);
        }

        switch (name)
        {
            case "GcArmyExpedition":
                Pair(16, 17, AccessibilityStrings.SquadronCost);
                Field(21, AccessibilityStrings.SquadronExperience);
                Item(22);
                // Both triplets have separate current/required headings. Never
                // infer a selected mission from list hover or from a sheet row.
                foreach (var (id, heading, fallback) in new[] {
                    (40u, 39u, AccessibilityStrings.SquadronRequired),
                    (43u, 42u, AccessibilityStrings.SquadronCurrent) })
                {
                    var value = Attributes(Node(&addon->UldManager, id), read);
                    if (value.Length > 0) Add(Label(Value(heading) is { Length: > 0 } l ? l : fallback, value),
                        Address(heading), Address(id));
                }
                for (uint id = 35; id <= 37; id++)
                {
                    var index = id - 35;
                    var label = AttributeName((int)index);
                    var required = AttributeValue(Node(&addon->UldManager, 40), index, read);
                    var current = AttributeValue(Node(&addon->UldManager, 43), index, read);
                    var text = Join([Label(AccessibilityStrings.SquadronRequired, required),
                        Label(AccessibilityStrings.SquadronCurrent, current)]);
                    // These icons are focusable column labels. Page already has
                    // the two labelled triplets, so add them only in Focus below.
                    Consume(Node(&addon->UldManager, id), used);
                    if (text.Length > 0) result.Add(new([Address(id)], Label(label, text), FocusOnly: true));
                }
                AddRows(Node(&addon->UldManager, 44), memberList: false, read, Add);
                break;
            case "GcArmyMemberList":
                AddRows(Node(&addon->UldManager, 19), memberList: true, read, Add);
                break;
            case "GcArmyMenberProfile": // The game's addon really spells it Menber.
                Field(19, AccessibilityStrings.SquadronPhysical, 18);
                Field(21, AccessibilityStrings.SquadronMental, 20);
                Field(23, AccessibilityStrings.SquadronTactical, 22);
                var chemistry = Join([Value(25), Value(26), Value(27)]);
                Add(Label(AccessibilityStrings.SquadronChemistry, chemistry), Address(24), Address(25), Address(26), Address(27));
                // Player log key=70002: the tactic is dropdown 7 / text 2,
                // not one of the chemistry paragraphs below the attributes.
                Field(7, AccessibilityStrings.SquadronTactics);
                break;
            case "GcArmyTraining":
                for (uint id = 24; id <= 26; id++)
                    Add(TrainingAttribute((int)id - 24, Read(Child(Child(Node(&addon->UldManager, id), 2), 2), read),
                        Read(Child(Child(Node(&addon->UldManager, id), 2), 4), read)), Address(id));
                Pair(18, 19, "");
                break;
            case "GcArmyChangeClass":
                for (uint id = 28; id <= 32; id += 2) Field(id, AttributeName(((int)id - 28) / 2));
                break;
            case "GcArmyCapture":
                Pair(19, 20, AccessibilityStrings.SquadronCost);
                Field(24, AccessibilityStrings.SquadronReward);
                break;
            case "GcArmyExpeditionResult":
                Field(14, AccessibilityStrings.SquadronExperience);
                Item(15);
                break;
        }
        // The ordinary reader drops 1-character text and bare numbers. Keep
        // every visible value here, including zero, descriptions and button text.
        Collect(&addon->UldManager, read, used, result, 0);
        // Column hints belong to focus only, not to the whole-window readout.
        return result.OrderBy(p => Position(p.Sources.FirstOrDefault()).Y)
            .ThenBy(p => Position(p.Sources.FirstOrDefault()).X).ToList();
    }

    internal static string AttributeName(int index) => index switch
    {
        0 => AccessibilityStrings.SquadronPhysical,
        1 => AccessibilityStrings.SquadronMental,
        _ => AccessibilityStrings.SquadronTactical,
    };

    internal static string TrainingAttribute(int index, string current, string change)
    {
        if (current.Length == 0 && change.Length == 0) return "";
        var normalized = change.Trim().Replace('−', '-').Replace('＋', '+');
        string delta;
        if (long.TryParse(normalized, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            && number != long.MinValue && (number == 0 || normalized.StartsWith('+') || normalized.StartsWith('-')))
            delta = number == 0 ? AccessibilityStrings.SquadronTrainingUnchanged
                : number > 0 ? AccessibilityStrings.SquadronTrainingIncrease(number.ToString(CultureInfo.InvariantCulture))
                : AccessibilityStrings.SquadronTrainingDecrease((-number).ToString(CultureInfo.InvariantCulture));
        else delta = change.Length == 0 ? AccessibilityStrings.SquadronTrainingChangeUnknown
            : AccessibilityStrings.SquadronTrainingChange(change);
        return Label(AttributeName(index), Join([current.Length > 0 ? AccessibilityStrings.SquadronTrainingNow(current) : "", delta]));
    }

    private static string TrainingFocus(AtkUnitBase* addon, AtkResNode* focus, Func<nint, string> read,
        Func<nint, string?> tooltip)
    {
        for (uint id = 6; id <= 12; id++)
        {
            var option = Node(&addon->UldManager, id);
            if (!NativeDialogButtons.Contains(option, focus)) continue;
            var hint = tooltip((nint)focus) ?? tooltip((nint)option) ?? "";
            var firstLine = hint.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            return firstLine.Length > 0 ? firstLine : AccessibilityStrings.SquadronTrainingOption((int)id - 5);
        }
        for (uint id = 24; id <= 26; id++)
            if (NativeDialogButtons.Contains(Node(&addon->UldManager, id), focus)) return AttributeName((int)id - 24);
        var control = NativeControlScope.Control(addon, focus);
        return Read(control != null ? control : focus, read) is { Length: > 0 } text ? text
            : tooltip((nint)focus) ?? (control == null ? null : tooltip((nint)control)) ?? "";
    }

    internal static string TrainingDetails(AtkUnitBase* addon, AtkResNode* focus, Func<nint, string> read,
        Func<nint, string?> tooltip)
    {
        if (addon == null || !addon->IsVisible || addon->NumBlockingAddons != 0 || !Valid(&addon->UldManager)
            || (focus != null && (!Visible(focus) || !NativeControlScope.Owns(addon, focus)))) return "";
        // These are the actual visible preview fields, including the game's
        // cap-adjusted losses. Never derive them from a sheet bonus or hovered id.
        var selected = Read(Node(&addon->UldManager, 15), read);
        var focused = focus == null ? "" : TrainingFocus(addon, focus, read, tooltip);
        List<string> parts = [];
        if (selected.Length > 0) parts.Add(Label(AccessibilityStrings.SquadronTrainingPreview, selected));
        else parts.Add(AccessibilityStrings.SquadronTrainingPreview);
        if (focused.Length > 0 && selected.Length > 0 && focused != selected
            && Enumerable.Range(6, 7).Any(id => NativeDialogButtons.Contains(Node(&addon->UldManager, (uint)id), focus)))
            parts.Add(AccessibilityStrings.SquadronTrainingPreviewDiffers);
        var hasValues = false;
        for (uint id = 24; id <= 26; id++)
        {
            var values = Child(Node(&addon->UldManager, id), 2);
            var attribute = TrainingAttribute((int)id - 24, Read(Child(values, 2), read), Read(Child(values, 4), read));
            if (attribute.Length > 0) hasValues = true;
            parts.Add(attribute);
        }
        if (!hasValues) parts.Add(AccessibilityStrings.SquadronLoading);
        parts.Add(Read(Node(&addon->UldManager, 23), read));
        parts.Add(Read(Node(&addon->UldManager, 13), read));
        var time = Read(Node(&addon->UldManager, 19), read);
        parts.Add(Label(Read(Node(&addon->UldManager, 18), read), time));
        return Join(parts);
    }

    private static string Attributes(AtkResNode* node, Func<nint, string> read)
        => Join(Enumerable.Range(0, 3).Select(i => Label(AttributeName(i), AttributeValue(node, (uint)i, read))));

    private static string AttributeValue(AtkResNode* node, uint index, Func<nint, string> read)
        => Read(Child(node, 2 + index * 2), read);

    private static void AddRows(AtkResNode* container, bool memberList, Func<nint, string> read,
        Action<string, nint[]> add)
    {
        var pending = new Queue<nint>();
        var visited = new HashSet<nint>();
        pending.Enqueue((nint)container);
        while (pending.Count > 0 && visited.Count < 2048)
        {
            var node = (AtkResNode*)pending.Dequeue();
            if (!Visible(node) || !visited.Add((nint)node)) continue;
            var manager = Manager(node);
            if (manager == null || !Valid(manager)) continue;
            var nameNode = Node(manager, memberList ? 9u : 3u);
            var statNode = Node(manager, memberList ? 17u : 8u);
            // A row must have the measured text+attribute structure. Node ids
            // alone also occur on the list's scrollbar and enclosing window.
            if (nameNode != null && nameNode->Type == NodeType.Text && Manager(statNode) != null)
            {
                var memberName = Read(nameNode, read);
                if (memberName.Length > 0)
                {
                    var level = Read(Node(manager, memberList ? 7u : 10u), read);
                    var attributes = memberList
                        ? Join(Enumerable.Range(0, 3).Select(i => Label(AttributeName(i),
                            Read(Child((AtkResNode*)node, (uint)(17 + 2 * i)), read))))
                        : Attributes(statNode, read);
                    var tactics = memberList ? Read(Node(manager, 15), read) : "";
                    var chemistry = memberList ? Read(Node(manager, 22), read) : "";
                    add(Join([memberName, level.Length > 0 ? AccessibilityStrings.SquadronLevel(level) : "",
                        attributes, Label(AccessibilityStrings.SquadronTactics, tactics),
                        Label(AccessibilityStrings.SquadronChemistry, chemistry)]), [(nint)node]);
                    continue;
                }
            }
            foreach (var child in Children(node))
                if (pending.Count < 2048) pending.Enqueue(child);
        }
    }

    private static void Collect(AtkUldManager* manager, Func<nint, string> read,
        HashSet<nint> visited, List<Part> parts, int depth, AtkResNode* owner = null)
    {
        if (depth > 12 || !Valid(manager)) return;
        var children = owner == null
            ? Enumerable.Range(0, manager->NodeListCount).Select(i => (nint)manager->NodeList[i]).ToArray()
            : Children(owner);
        for (var i = children.Length - 1; i >= 0 && visited.Count < 4096; i--)
        {
            var node = (AtkResNode*)children[i];
            if (!Visible(node) || !visited.Add((nint)node)) continue;
            if (node->Type == NodeType.Text)
            {
                var text = read((nint)node).Trim();
                if (text.Length > 0) parts.Add(new([(nint)node], text));
            }
            else Collect(Manager(node), read, visited, parts, depth + 1, node);
        }
    }

    private static string Read(AtkResNode* node, Func<nint, string> read)
    {
        if (!Visible(node)) return "";
        if (node->Type == NodeType.Text) return read((nint)node).Trim();
        List<Part> parts = [];
        Collect(Manager(node), read, [], parts, 0, node);
        return Join(parts.OrderBy(p => Position(p.Sources[0]).Y)
            .ThenBy(p => Position(p.Sources[0]).X).Select(p => p.Text));
    }

    private static void Consume(AtkResNode* node, HashSet<nint> visited, int depth = 0)
    {
        if (node == null || depth > 12 || !visited.Add((nint)node) || visited.Count >= 4096) return;
        var manager = Manager(node);
        if (manager == null || !Valid(manager)) return;
        foreach (var child in Children(node)) Consume((AtkResNode*)child, visited, depth + 1);
    }

    private static bool IsType(AtkUldManager* manager, ComponentType type)
        => manager != null && AtkText.IsReadable(manager->Objects)
            && ((AtkUldComponentInfo*)manager->Objects)->ComponentType == type;

    private static nint[] Children(AtkResNode* node)
    {
        var manager = Manager(node);
        if (!Valid(manager)) return [];
        List<nint> result = [];
        var isList = IsType(manager, ComponentType.List);
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var child = manager->NodeList[i];
            // A virtual list's template/cloned row is not necessarily an active
            // logical row. Only NativeListRows can establish current identity.
            if (isList && IsType(Manager(child), ComponentType.ListItemRenderer)) continue;
            result.Add((nint)child);
        }
        if (!isList) return result.ToArray();
        var list = (AtkComponentList*)((AtkComponentNode*)node)->Component;
        if (list->AllocatedItemRendererListLength is < 0 or > 512) return result.ToArray();
        for (var slot = 0; slot < list->AllocatedItemRendererListLength; slot++)
        {
            var index = NativeListRows.IndexAtSlot(list, slot);
            var renderer = index < 0 ? null : NativeListRows.Renderer(list, index);
            if (renderer != null && renderer->OwnerNode != null) result.Add((nint)renderer->OwnerNode);
        }
        return result.Distinct().ToArray();
    }

    private static AtkUldManager* Manager(AtkResNode* node)
        => node != null && (int)node->Type >= 1000 && ((AtkComponentNode*)node)->Component != null
            ? &((AtkComponentNode*)node)->Component->UldManager : null;

    private static AtkResNode* Child(AtkResNode* node, uint id) => Node(Manager(node), id);
    private static AtkResNode* Node(AtkUldManager* manager, uint id)
    {
        if (!Valid(manager)) return null;
        for (var i = 0; i < manager->NodeListCount; i++)
            if (manager->NodeList[i] != null && manager->NodeList[i]->NodeId == id) return manager->NodeList[i];
        return null;
    }
    private static bool Valid(AtkUldManager* manager)
        => manager != null && manager->NodeList != null && manager->NodeListCount <= 512;
    private static bool Visible(AtkResNode* node) => node != null && TableNodeReader.IsVisible(node);
    private static (float X, float Y) Position(nint address)
    {
        var node = (AtkResNode*)address;
        float x = 0, y = 0;
        for (var depth = 0; node != null && depth < 64; depth++, node = node->ParentNode)
        { x += node->X; y += node->Y; }
        return (x, y);
    }
    private static string Label(string label, string value)
        => value.Length == 0 ? "" : label.Length == 0 ? value : $"{label}: {value}";
    private static string Join(IEnumerable<string> parts)
        => string.Join(". ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
