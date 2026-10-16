using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>Resolve controls by pointer membership, never by a repeated node id.
/// Component-internal focus nodes do not always have a parent chain to the window.</summary>
internal static unsafe class NativeControlScope
{
    internal static string Text(AtkUnitBase* addon, AtkResNode* focus, Func<nint, string> read)
    {
        var control = Control(addon, focus);
        return read((nint)(control != null ? control : focus));
    }

    internal static bool Owns(AtkUnitBase* addon, AtkResNode* focus)
    {
        if (!AtkText.IsReadable(addon) || !AtkText.IsReadable(focus)
            || addon->UldManager.NodeListCount > 512 || !AtkText.IsReadable(addon->UldManager.NodeList)) return false;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            if (NativeDialogButtons.Contains(addon->UldManager.NodeList[i], focus)) return true;
        return false;
    }

    internal static AtkResNode* Control(AtkUnitBase* addon, AtkResNode* focus)
    {
        if (!Owns(addon, focus)) return null;
        var current = focus;
        for (var up = 0; up < 8 && AtkText.IsReadable(current); up++, current = current->ParentNode)
            if ((int)current->Type >= 1000) return current;
        var candidates = new List<nint>();
        var visited = new HashSet<nint>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            Collect(addon->UldManager.NodeList[i], focus, candidates, visited, 0);
        AtkResNode* result = null;
        foreach (var address in candidates)
        {
            var candidate = (AtkResNode*)address;
            // The innermost owner wins; unrelated owners mean malformed/ambiguous UI.
            if (result == null || NativeDialogButtons.Contains(result, candidate)) result = candidate;
            else if (!NativeDialogButtons.Contains(candidate, result)) return null;
        }
        return result;
    }

    private static void Collect(AtkResNode* node, AtkResNode* focus, List<nint> candidates,
        HashSet<nint> visited, int depth)
    {
        if (depth > 8 || visited.Count >= 2048 || !AtkText.IsReadable(node)
            || !visited.Add((nint)node) || (int)node->Type < 1000
            || !NativeDialogButtons.Contains(node, focus)) return;
        candidates.Add((nint)node);
        var comp = ((AtkComponentNode*)node)->Component;
        if (!AtkText.IsReadable(comp) || comp->UldManager.NodeListCount > 512
            || !AtkText.IsReadable(comp->UldManager.NodeList)) return;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
            Collect(comp->UldManager.NodeList[i], focus, candidates, visited, depth + 1);
    }
}
