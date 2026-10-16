using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>CharacterStatus rows follow the actual containers in
/// ui/uld/characterstatus.uld, checked by tools/table-layout-check. Values and
/// translated labels always come from the visible window, never from the ULD.</summary>
internal static unsafe class CharacterStatusTable
{
    internal sealed record Group(uint Root, uint Heading, uint[] Stats);
    internal static readonly uint[] Pools = [2, 8, 14, 20];
    internal static readonly Group[] Groups =
    [
        new(26, 29, [31, 32, 33, 34, 35]), // Attributes, one column at a time.
        new(36, 39, [41, 42, 43]),
        new(44, 47, [49, 50]),
        new(51, 54, [56, 57]),
        new(58, 61, [63, 64, 65]),
        new(66, 69, [71, 72]), // Gathering/crafting panels share the same area.
        new(73, 76, [78, 79]),
        new(80, 83, [85]),
        new(86, 89, [91, 92]),
    ];

    internal static bool TryRead(AtkUnitBase* addon, string section, Func<nint, string> readText,
        out IReadOnlyList<TableReader.Row> rows)
    {
        var result = new List<TableReader.Row>();
        rows = result;
        if (addon == null) return false;
        // Check types and parentage before using the version-specific layout.
        // If it changes, the caller retains the generic visible-text reader.
        foreach (var id in Pools)
        {
            var root = Find(&addon->UldManager, id);
            if (!Child(root, 1, NodeType.Res)) return false;
            for (uint offset = 1; offset <= 3; offset++)
                if (!Child(Find(&addon->UldManager, id + offset), id, NodeType.Text)) return false;
        }
        foreach (var group in Groups)
        {
            if (!Child(Find(&addon->UldManager, group.Root), 1, NodeType.Res)
                || !Child(Find(&addon->UldManager, group.Heading - 2), group.Root, NodeType.Res)
                || !Child(Find(&addon->UldManager, group.Heading), group.Heading - 2, NodeType.Text)) return false;
            foreach (var id in group.Stats)
            {
                var node = Find(&addon->UldManager, id);
                if (node == null || (int)node->Type < 1000 || node->ParentNode == null
                    || node->ParentNode->NodeId != group.Root) return false;
            }
        }
        foreach (var id in Pools)
        {
            var label = Read(Find(&addon->UldManager, id + 1), readText);
            var current = Read(Find(&addon->UldManager, id + 2), readText);
            var maximum = Read(Find(&addon->UldManager, id + 3), readText);
            if (label == null || current == null) continue;
            var value = maximum == null ? current.Text
                : AccessibilityStrings.TableCurrentMaximum(current.Text, maximum.Text);
            result.Add(new(section, [label, current with { Text = value, Label = label.Text }]));
        }
        foreach (var group in Groups)
        {
            var heading = Read(Find(&addon->UldManager, group.Heading), readText);
            if (heading == null) continue;
            foreach (var id in group.Stats)
                if (TryStat(Find(&addon->UldManager, id), readText, out var cells))
                    result.Add(new(heading.Text, cells));
        }
        return result.Count > 0;
    }

    internal static bool TryStat(AtkResNode* node, Func<nint, string> readText,
        out IReadOnlyList<TableReader.Cell> cells)
    {
        cells = [];
        if (node == null || (int)node->Type < 1000 || !TableNodeReader.IsVisible(node)) return false;
        var component = ((AtkComponentNode*)node)->Component;
        if (component == null) return false;
        var labelNode = Find(&component->UldManager, 2);
        var valueNode = Find(&component->UldManager, 3);
        if (!Child(labelNode, 1, NodeType.Text) || !Child(valueNode, 1, NodeType.Text)
            || labelNode->ParentNode != valueNode->ParentNode) return false;
        var label = Read(labelNode, readText);
        var value = Read(valueNode, readText);
        if (label == null || value == null) return false;
        cells = [label, value with { Label = label.Text }];
        return true;
    }

    private static bool Child(AtkResNode* node, uint parent, NodeType type) => node != null
        && node->Type == type && node->ParentNode != null && node->ParentNode->NodeId == parent;

    private static TableReader.Cell? Read(AtkResNode* node, Func<nint, string> readText)
    {
        if (node == null || node->Type != NodeType.Text || !TableNodeReader.IsVisible(node)) return null;
        var text = readText((nint)node);
        return string.IsNullOrWhiteSpace(text) ? null : new(((nint)node).ToString(), text,
            node->ScreenX, node->ScreenY, node->Height);
    }

    private static AtkResNode* Find(AtkUldManager* manager, uint id)
    {
        if (manager->NodeList == null || manager->NodeListCount > 4096) return null;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node != null && node->NodeId == id) return node;
        }
        return null;
    }
}
