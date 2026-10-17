using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>Selection comes from the game's focused node and actual button
/// pointers. Translated labels, cached speech and node focus flags are never
/// used to choose an answer.</summary>
internal static unsafe class NativeDialogButtons
{
    internal readonly record struct Choice(nint Node, int Index, bool Held);

    internal static bool Contains(AtkResNode* owner, AtkResNode* focus, int depth = 0)
    {
        if (!AtkText.IsReadable(owner) || !AtkText.IsReadable(focus) || depth > 4) return false;
        if (owner == focus) return true;
        var parent = focus;
        for (var i = 0; i < 32 && AtkText.IsReadable(parent); i++, parent = parent->ParentNode)
            if (parent == owner) return true;
        if ((int)owner->Type < 1000) return false;
        var comp = ((AtkComponentNode*)owner)->Component;
        if (!AtkText.IsReadable(comp) || comp->UldManager.NodeListCount > 512
            || !AtkText.IsReadable(comp->UldManager.NodeList)) return false;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
            if (Contains(comp->UldManager.NodeList[i], focus, depth + 1)) return true;
        return false;
    }

    internal static bool Available(AtkResNode* node)
    {
        if (!AtkText.IsReadable(node) || (node->NodeFlags & NodeFlags.Enabled) == 0) return false;
        var current = node;
        for (var i = 0; i < 32 && current != null; i++, current = current->ParentNode)
            if (!AtkText.IsReadable(current) || (current->NodeFlags & NodeFlags.Visible) == 0
                || current->IsDrawDisabled) return false;
        return current == null;
    }

    internal static bool TrySelect(AddonSelectYesno* addon, AtkResNode* focus, out Choice choice)
    {
        choice = default;
        if (!AtkText.IsReadable(addon) || !AtkText.IsReadable(focus)) return false;
        for (var index = 0; index < 9; index++)
        {
            var button = ButtonAt(addon, index);
            if (!AtkText.IsReadable(button)) continue;
            var node = (AtkResNode*)button->OwnerNode;
            if (!Available(node) || !Contains(node, focus)) continue;
            choice = new((nint)node, index % 3, index >= 6);
            return true;
        }
        return false;
    }

    internal static AtkComponentButton* ButtonForSide(AddonSelectYesno* addon, int side)
    {
        if (!AtkText.IsReadable(addon) || side is < 0 or > 2) return null;
        for (var i = side; i < 9; i += 3)
        {
            var button = ButtonAt(addon, i);
            if (AtkText.IsReadable(button) && Available((AtkResNode*)button->OwnerNode)) return button;
        }
        return null;
    }

    internal static bool UsesHoldConfirm(AddonSelectYesno* addon)
    {
        if (!AtkText.IsReadable(addon)) return false;
        var button = ButtonForSide(addon, 0);
        return AtkText.IsReadable(button) && button == (AtkComponentButton*)addon->AtkComponentHoldButton278;
    }

    private static AtkComponentButton* ButtonAt(AddonSelectYesno* addon, int index) => index switch
    {
        0 => addon->YesButton, 1 => addon->NoButton, 2 => addon->AtkComponentButton238,
        3 => addon->AtkComponentButton260, 4 => addon->AtkComponentButton268, 5 => addon->AtkComponentButton270,
        6 => (AtkComponentButton*)addon->AtkComponentHoldButton278,
        7 => (AtkComponentButton*)addon->AtkComponentHoldButton280,
        8 => (AtkComponentButton*)addon->AtkComponentHoldButton288, _ => null,
    };
}
