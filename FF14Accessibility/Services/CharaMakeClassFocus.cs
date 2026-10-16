using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

internal static unsafe class CharaMakeClassFocus
{
    internal static string ReadLabel(AtkUnitBase* selector, AtkResNode* focused)
    {
        if (selector == null || !selector->IsVisible || focused == null || !TableNodeReader.IsVisible(focused)) return string.Empty;
        var belongs = false;
        var depth = 0;
        for (var node = focused; node != null && depth++ < 16; node = node->ParentNode)
        {
            for (var i = 0; i < selector->UldManager.NodeListCount; ++i)
                if (selector->UldManager.NodeList[i] == node) { belongs = true; break; }
            if (belongs) break;
        }
        if (!belongs) return string.Empty;
        depth = 0;
        for (var node = focused; node != null && depth++ < 4; node = node->ParentNode)
        {
            if (!TableNodeReader.IsVisible(node)) return string.Empty;
            if (node->Type == NodeType.Text) return AtkText.ReadClean((AtkTextNode*)node).Trim();
            if ((int)node->Type < 1000) continue;
            var component = ((AtkComponentNode*)node)->Component;
            if (component == null) return string.Empty;
            string label = string.Empty;
            for (var i = 0; i < component->UldManager.NodeListCount; ++i)
            {
                var child = component->UldManager.NodeList[i];
                if (child == null || child->Type != NodeType.Text || !TableNodeReader.IsVisible(child)) continue;
                var text = AtkText.ReadClean((AtkTextNode*)child).Trim();
                if (text.Length == 0) continue;
                if (label.Length > 0 && label != text) return string.Empty;
                label = text;
            }
            return label;
        }
        return string.Empty;
    }
}
