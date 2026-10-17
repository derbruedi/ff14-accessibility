using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>A virtual list's visible renderer slot is not its logical row.
/// Recycled renderers carry the current logical identity in ListItemIndex.</summary>
internal static unsafe class NativeListRows
{
    internal static int IndexAtSlot(AtkComponentList* list, int slot)
    {
        if (!AtkText.IsReadable(list) || list->AllocatedItemRendererListLength is <= 0 or > 512
            || slot < 0 || slot >= list->AllocatedItemRendererListLength
            || !AtkText.IsReadable(list->ItemRendererList)) return -1;
        var renderer = list->ItemRendererList[slot].AtkComponentListItemRenderer;
        if (!AtkText.IsReadable(renderer)) return -1;
        var index = renderer->ListItemIndex;
        if (list->ListLength > 0) return index >= 0 && index < list->ListLength ? index : -1;
        // Journal's TreeList has no base ListLength. Its indices address the
        // renderer slots, as measured in the existing Journal reader.
        return IsTreeList(list) ? slot : -1;
    }

    internal static AtkComponentListItemRenderer* Renderer(AtkComponentList* list, int index)
    {
        if (!AtkText.IsReadable(list) || index < 0
            || list->AllocatedItemRendererListLength is <= 0 or > 512
            || !AtkText.IsReadable(list->ItemRendererList)) return null;
        AtkComponentListItemRenderer* found = null;
        for (var slot = 0; slot < list->AllocatedItemRendererListLength; slot++)
        {
            if (IndexAtSlot(list, slot) != index) continue;
            var renderer = list->ItemRendererList[slot].AtkComponentListItemRenderer;
            if (found != null && found != renderer) return null;
            found = renderer;
        }
        return found;
    }

    private static bool IsTreeList(AtkComponentList* list)
        => AtkText.IsReadable(list->UldManager.Objects)
            && ((AtkUldComponentInfo*)list->UldManager.Objects)->ComponentType == ComponentType.TreeList;
}
