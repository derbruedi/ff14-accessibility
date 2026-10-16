using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private ItemSlotService.OwnedSlot _focusedOwnedSlot;
    private ItemFocusIdentity _itemFocusIdentity;
    private bool _retryItemFocusSpeech;

    internal readonly record struct ItemFocusIdentity(nint Node, uint Icon, string Quantity,
        ItemSlotService.OwnedSlot OwnedSlot, bool Translate);

    // Runs before the name cache and description dwell, even if the game's
    // focused node stayed the same while its page, sort order or item changed.
    private unsafe void RefreshItemFocus(AtkResNode* node)
    {
        AtkComponentIcon* icon = null;
        AtkComponentBase* component = null;
        var depth = 0;
        for (var current = node; current != null && depth++ < 4; current = current->ParentNode)
        {
            if ((int)current->Type < 1000) continue;
            component = ((AtkComponentNode*)current)->Component;
            if (component != null) icon = FindSlotIcon(component);
            break;
        }
        var iconId = icon == null ? 0 : icon->IconId;
        var quantity = icon == null ? string.Empty : ReadIconQuantity(icon);
        _focusedOwnedSlot = component == null || icon == null
            ? default : _itemSlots.ReadOwnedSlot(component, iconId, quantity);
        var identity = new ItemFocusIdentity((nint)node, iconId, quantity,
            _focusedOwnedSlot, Loc.IsRussianItemActionText);
        ObserveItemFocus(identity);
    }

    internal void ObserveItemFocus(ItemFocusIdentity identity)
    {
        if (identity == _itemFocusIdentity) return;
        _itemFocusIdentity = identity;
        _lastFocusedNodePtr = 0;
        _lastFocusedItemName = string.Empty;
        _lastFocusedItemId = 0;
        _itemDwellArmed = false;
        _itemDwellId = 0;
        _itemDeferNode = 0;
        _retryItemFocusSpeech = false;
    }

    private unsafe bool TryReadArmouryFilterFocus(AtkResNode* focus, out string text)
        => ReadArmouryFilterFocus(focus, FindAddonNameForNode(focus), out text);

    internal static unsafe bool ReadArmouryFilterFocus(AtkResNode* focus, string addonName, out string text)
    {
        text = string.Empty;
        if (addonName != "ArmouryBoard") return false;
        var depth = 0;
        for (var node = focus; node != null && depth++ < 4; node = node->ParentNode)
        {
            if ((int)node->Type < 1000) continue;
            // The shipped ULD has exactly these 12 icon-only category controls.
            // ButtonClick params 1/2 on nodes 7/8 were observed in the fresh log.
            var label = ArmouryFilterLabel(node->NodeId);
            if (label.Length == 0) return false;
            var component = ((AtkComponentNode*)node)->Component;
            if (component == null) return false;
            var matches = false;
            var eventCount = 0;
            for (var ev = node->AtkEventManager.Event; ev != null && eventCount++ < 32; ev = ev->NextEvent)
                if (ev->State.EventType == AtkEventType.ButtonClick
                    && ev->Param == node->NodeId - 6) { matches = true; break; }
            if (!matches) return false;
            text = AccessibilityStrings.ArmourySlotFilter(label);
            return true;
        }
        return false;
    }

    internal static string ArmouryFilterLabel(uint nodeId) => nodeId switch
    {
        7 => AccessibilityStrings.ArmouryMainHand,
        8 => AccessibilityStrings.SlotHead, 9 => AccessibilityStrings.SlotBody,
        10 => AccessibilityStrings.SlotHands, 11 => AccessibilityStrings.SlotLegs,
        12 => AccessibilityStrings.SlotFeet, 13 => AccessibilityStrings.SlotOffHand,
        14 => AccessibilityStrings.SlotEars, 15 => AccessibilityStrings.SlotNeck,
        16 => AccessibilityStrings.SlotWrists, 17 => AccessibilityStrings.SlotRing,
        18 => AccessibilityStrings.SlotSoulCrystal,
        _ => string.Empty,
    };
}
