using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>The game's NPC and cutscene choice menus. A focused row need not be the
/// selected row: SelectedItemIndex can remain -1 throughout keyboard navigation.
/// Resolve the actual renderer, then send exactly one native list click.</summary>
internal static unsafe class NativeSelectionDialog
{
    internal readonly record struct Choice(nint List, int Index, bool Available);

    internal static bool TryResolve(AtkUnitBase* addon, AtkResNode* focus, out Choice choice)
    {
        choice = default;
        if (!AtkText.IsReadable(addon) || !addon->IsVisible || addon->NumBlockingAddons != 0
            || !AtkText.IsReadable(focus)) return false;
        AtkComponentList* list;
        int count;
        switch (addon->NameString)
        {
            case "SelectString":
                list = ((AddonSelectString*)addon)->PopupMenu.List;
                count = ((AddonSelectString*)addon)->PopupMenu.EntryCount;
                break;
            case "SelectIconString":
                list = ((AddonSelectIconString*)addon)->PopupMenu.List;
                count = ((AddonSelectIconString*)addon)->PopupMenu.EntryCount;
                break;
            case "CutSceneSelectString":
                list = ((AddonCutSceneSelectString*)addon)->OptionList;
                count = AtkText.IsReadable(list) ? list->ListLength : 0;
                break;
            default: return false;
        }
        return TryResolveRow(addon, list, count, focus, out choice);
    }

    internal static bool TryResolveRow(AtkUnitBase* addon, AtkComponentList* list, int count,
        AtkResNode* focus, out Choice choice)
    {
        choice = default;
        if (!AtkText.IsReadable(addon) || !addon->IsVisible || addon->NumBlockingAddons != 0
            || !AtkText.IsReadable(list) || count is <= 0 or > 512 || list->ListLength != count
            || list->AllocatedItemRendererListLength is <= 0 or > 512
            || !AtkText.IsReadable(list->ItemRendererList)
            || !NativeControlScope.Owns(addon, (AtkResNode*)list->OwnerNode)
            || !NativeControlScope.Owns(addon, focus)) return false;

        AtkComponentListItemRenderer* matched = null;
        for (var slot = 0; slot < list->AllocatedItemRendererListLength; slot++)
        {
            var entry = list->ItemRendererList + slot;
            var renderer = entry->AtkComponentListItemRenderer;
            if (!AtkText.IsReadable(renderer)) continue;
            var owner = (AtkResNode*)renderer->OwnerNode;
            if (!NativeDialogButtons.Contains(owner, focus)) continue;
            if (matched != null && matched != renderer) return false;
            var index = renderer->ListItemIndex;
            if (index < 0 || index >= count) return false;
            matched = renderer;
            choice = new((nint)list, index, !entry->IsDisabled
                && NativeDialogButtons.Available((AtkResNode*)list->OwnerNode)
                && NativeDialogButtons.Available(owner));
        }
        return matched != null;
    }

    internal enum Result { Unowned, Unavailable, Requested }

    internal static Result Confirm(AtkUnitBase* addon, AtkResNode* focus, Action<Choice> dispatch)
    {
        if (!TryResolve(addon, focus, out var choice)) return Result.Unowned;
        if (!choice.Available) return Result.Unavailable;
        dispatch(choice);
        return Result.Requested;
    }

    internal static void Dispatch(Choice choice)
    {
        // DispatchItemEvent populates the game's own row event data. SelectItem
        // alone only changes selection; a second synthetic click can activate the
        // next dialog or access a list already destroyed by the first callback.
        ((AtkComponentList*)choice.List)->DispatchItemEvent(choice.Index, AtkEventType.ListItemClick);
        // The native callback may have destroyed both the list and its window.
    }
}
