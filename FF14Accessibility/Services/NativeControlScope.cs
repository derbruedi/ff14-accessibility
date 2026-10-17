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
        if (!AtkText.IsReadable(addon) || !AtkText.IsReadable(focus)) return false;
        var ancestors = new HashSet<nint>();
        for (var node = focus; ancestors.Count < 32 && AtkText.IsReadable(node); node = node->ParentNode)
        {
            if (!ancestors.Add((nint)node)) break;
            if (node == addon->RootNode) return true;
        }
        return NativeDialogButtons.ContainsAny(addon->UldManager.NodeList,
            addon->UldManager.NodeListCount, focus, out _);
    }

    internal static AtkResNode* Control(AtkUnitBase* addon, AtkResNode* focus)
    {
        if (!Owns(addon, focus)) return null;
        var current = focus;
        for (var up = 0; up < 32 && AtkText.IsReadable(current); up++, current = current->ParentNode)
            if ((int)current->Type >= 1000) return current;
        // One index for all overlapping lists, including components without
        // a parent chain. Reverse edges identify the innermost owning control.
        var parents = new Dictionary<nint, HashSet<nint>>();
        var visited = new HashSet<nint>();
        var pending = new Queue<(nint Node, int Depth)>();
        var count = addon->UldManager.NodeListCount;
        if (count > 512 || !AtkText.IsReadable(addon->UldManager.NodeList)) return null;
        var slots = (int)count;
        for (var i = 0; i < count; i++) pending.Enqueue(((nint)addon->UldManager.NodeList[i], 0));
        while (pending.TryDequeue(out var next))
        {
            if (next.Node == 0 || !visited.Add(next.Node)) continue;
            if (visited.Count > 2048) return null;
            var node = (AtkResNode*)next.Node;
            if (!AtkText.IsReadable(node) || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (!AtkText.IsReadable(comp) || comp->UldManager.NodeListCount > 512
                || !AtkText.IsReadable(comp->UldManager.NodeList)) continue;
            if (next.Depth >= 8) return null;
            slots += comp->UldManager.NodeListCount;
            if (slots > 8192) return null;
            for (var i = 0; i < comp->UldManager.NodeListCount; i++)
            {
                var child = (nint)comp->UldManager.NodeList[i];
                if (child == 0 || child == next.Node) continue;
                if (!parents.TryGetValue(child, out var owners)) parents[child] = owners = [];
                owners.Add(next.Node);
                pending.Enqueue((child, next.Depth + 1));
            }
        }
        var candidates = new HashSet<nint>();
        var outer = new HashSet<nint>();
        var search = new Queue<nint>();
        search.Enqueue((nint)focus);
        while (search.TryDequeue(out var child))
        {
            if (!parents.TryGetValue(child, out var owners)) continue;
            foreach (var owner in owners)
            {
                if (child != (nint)focus) outer.Add(owner);
                if (candidates.Add(owner)) search.Enqueue(owner);
            }
        }
        candidates.ExceptWith(outer);
        return candidates.Count == 1 ? (AtkResNode*)candidates.Single() : null;
    }
}
