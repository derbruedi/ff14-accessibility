using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

internal static unsafe class TableNodeReader
{
    internal static bool IsVisible(AtkResNode* node)
    {
        var depth = 0;
        for (; node != null && depth < 64; depth++, node = node->ParentNode)
            if ((node->NodeFlags & NodeFlags.Visible) == 0 || node->IsDrawDisabled) return false;
        return node == null; // A broken/cyclic ancestry is not a visible element.
    }

    internal static void Collect(AtkResNode* node, List<TableReader.Cell> cells, HashSet<nint> visited,
        Func<nint, string> readText, Func<nint, string?> readTooltip, int depth = 0, string group = "")
    {
        if (node == null || depth > 16 || visited.Count >= 4096 || !visited.Add((nint)node) || !IsVisible(node)) return;
        if (node->Type == NodeType.Text)
        {
            Add(node, readText((nint)node), cells, group);
            return;
        }
        if ((int)node->Type < 1000) return;
        var comp = ((AtkComponentNode*)node)->Component;
        if (comp == null) return;
        var before = cells.Count;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
            Collect(comp->UldManager.NodeList[i], cells, visited, readText, readTooltip, depth + 1, ((nint)node).ToString());
        if (cells.Count == before) Add(node, readTooltip((nint)node) ?? "", cells, ((nint)node).ToString());
    }

    private static void Add(AtkResNode* node, string text, List<TableReader.Cell> cells, string group)
    {
        if (!string.IsNullOrWhiteSpace(text))
            cells.Add(new(((nint)node).ToString(), text, node->ScreenX, node->ScreenY, node->Height, group));
    }
}
