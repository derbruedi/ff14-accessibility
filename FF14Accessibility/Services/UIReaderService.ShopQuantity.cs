using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private ShopQuantityEdit? _shopQuantityEdit;
    private bool _shopQuantityDrainKeys;
    public bool IsShopQuantityEditing => _shopQuantityEdit != null;

    // Includes game navigation/confirm keys: no number may buy, move the cursor
    // or activate an action underneath this spoken editor. Modifiers stay native.
    public static IEnumerable<int> ShopQuantityKeys => Enumerable.Range(0x30, 10)
        .Concat(Enumerable.Range(0x41, 26)).Concat(Enumerable.Range(0x60, 16))
        .Concat(Enumerable.Range(0x70, 24)) // Function keys may run hotbar actions too.
        .Concat(new[] { 0x08, 0x09, 0x0C, 0x0D, 0x1B, 0x20, 0x21, 0x22, 0x23, 0x24,
            0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E,
            0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE2 });

    public unsafe bool BeginShopQuantity(bool reportUnavailable = true)
    {
        if (!TryGetShopQuantity(out var target))
        {
            _log.Info("[ShopQuantity] No unambiguous visible quantity field in the focused gil-shop buy row.");
            if (reportUnavailable) _tolk.SpeakInterrupt(AccessibilityStrings.ShopQuantityUnavailable);
            return false;
        }
        _shopQuantityEdit = new ShopQuantityEdit(target);
        _itemDwellArmed = false;
        _tolk.SpeakInterrupt(AccessibilityStrings.ShopQuantityValue(ShopItemName(target.ItemName), target.Value)
            + " " + AccessibilityStrings.ShopQuantityRange(Math.Max(1, target.Minimum), target.Maximum)
            + " " + AccessibilityStrings.ShopQuantityInstructions);
        _log.Info($"[ShopQuantity] Editing '{target.ItemName}', value={target.Value}, min={target.Minimum}, max={target.Maximum}.");
        return true;
    }

    public unsafe bool HandleShopQuantityKeys(MenuInput input, bool windowActive, bool modifiers)
    {
        if (_shopQuantityDrainKeys)
        {
            _shopQuantityDrainKeys = input.AnyDown;
            if (_shopQuantityDrainKeys) { input.ConsumeAll(); return true; }
        }
        var edit = _shopQuantityEdit;
        if (edit == null) return false;
        input.ConsumeAll();
        if (!windowActive || !TryGetShopQuantity(out var current) || !edit.Matches(current))
        {
            EndShopQuantity(AccessibilityStrings.ShopQuantityChanged);
            return true;
        }
        if (input.Just(0x1B))
        {
            EndShopQuantity(AccessibilityStrings.ShopQuantityCancelled);
            return true;
        }
        if (modifiers) return true;
        if (input.Just(0x0D))
        {
            if (!edit.TryValue(out var value))
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ShopQuantityRange(Math.Max(1, current.Minimum), current.Maximum));
                return true;
            }
            // This is the numeric component's own value-change path. No shop
            // purchase callback, packet or automatic confirmation is issued.
            var numeric = (AtkComponentNumericInput*)current.Input;
            numeric->InnerSetValue(value, true, false);
            var applied = TryGetShopQuantity(out var after)
                && after == current with { Value = value };
            _log.Info($"[ShopQuantity] Requested quantity={value}, confirmed={applied}. Purchase remains manual.");
            EndShopQuantity(applied
                ? AccessibilityStrings.ShopQuantityApplied(ShopItemName(current.ItemName), value)
                : AccessibilityStrings.ShopQuantityNotApplied);
            return true;
        }
        if (input.Just(0x08)) edit.Backspace();
        else if (input.Just(0x2E)) edit.Clear();
        else
        {
            var digit = -1;
            for (var i = 0; i <= 9; i++)
                if (input.Just(0x30 + i) || input.Just(0x60 + i)) { digit = i; break; }
            if (digit < 0) return true;
            edit.Digit(digit);
        }
        _tolk.SpeakInterrupt(AccessibilityStrings.ShopQuantityDraft(edit.Text));
        return true;
    }

    private void EndShopQuantity(string message)
    {
        _shopQuantityEdit = null;
        _shopQuantityDrainKeys = true; // Enter/Escape cannot leak while held.
        _tolk.SpeakInterrupt(message);
    }

    private string ShopItemName(string raw)
    {
        var id = _inventory.ResolveItemIdByName(raw);
        return id == 0 ? raw : _inventory.ResolveItemName(id);
    }

    private unsafe bool TryGetShopQuantity(out ShopQuantityEdit.Target target)
    {
        target = default;
        if (IsChatInputActive() || IsAddonVisible("SelectYesno") || IsAddonVisible("ContextMenu")) return false;
        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkInputManager == null) return false;
        var focus = stage->AtkInputManager->FocusedNode;
        var addon = FindAddonForNode(focus);
        if (addon == null || !addon->IsVisible || addon->NameString != "Shop") return false;
        var list = ((AddonShop*)addon)->BuyList;
        if (list == null || list->OwnerNode == null) return false;
        var row = ShopQuantityNodes.FindRow(focus, (AtkResNode*)list->OwnerNode);
        if (row == null || !IsEffectivelyVisible(row) || (row->NodeFlags & NodeFlags.Enabled) == 0) return false;
        var numeric = ShopQuantityNodes.FindInput(row);
        if (numeric == null || numeric->OwnerNode == null || !IsEffectivelyVisible((AtkResNode*)numeric->OwnerNode)
            || (numeric->OwnerNode->NodeFlags & NodeFlags.Enabled) == 0) return false;
        var name = ReadRowText(row, RowNameNodeId);
        target = new ShopQuantityEdit.Target((nint)addon, (nint)row, (nint)numeric,
            name, numeric->Data.Min, numeric->Data.Max, numeric->Value);
        return ShopQuantityEdit.IsUsable(target);
    }

    private unsafe bool TryReadShopQuantityFocus(AtkResNode* node, out string text)
    {
        text = string.Empty;
        if (!TryGetShopQuantity(out var target)) return false;
        var numeric = (AtkComponentNumericInput*)target.Input;
        if (!ShopQuantityNodes.IsWithin(node, (AtkResNode*)numeric->OwnerNode)) return false;
        text = AccessibilityStrings.ShopQuantityValue(ShopItemName(target.ItemName), target.Value)
            + " " + AccessibilityStrings.ShopQuantityHint(AccessibilityStrings.SpokenKeyLabel(_config.KeyShopQuantity));
        return true;
    }

    private unsafe string AppendShopQuantityHint(AtkResNode* node, string text)
    {
        if (!TryGetShopQuantity(out var target)) return text;
        var numeric = (AtkComponentNumericInput*)target.Input;
        if (ShopQuantityNodes.IsWithin(node, (AtkResNode*)numeric->OwnerNode)) return text;
        return text + ". " + AccessibilityStrings.ShopQuantityValue(ShopItemName(target.ItemName), target.Value)
            + " " + AccessibilityStrings.ShopQuantityHint(AccessibilityStrings.SpokenKeyLabel(_config.KeyShopQuantity));
    }
}

internal static unsafe class ShopQuantityNodes
{
    internal static bool IsWithin(AtkResNode* node, AtkResNode* ancestor)
    {
        if (ancestor == null) return false;
        for (var depth = 0; node != null && depth < 64; depth++, node = node->ParentNode)
            if (node == ancestor) return true;
        return false;
    }

    internal static AtkResNode* FindRow(AtkResNode* focus, AtkResNode* buyList,
        Func<nint, ComponentType>? componentType = null)
    {
        if (!IsWithin(focus, buyList)) return null;
        for (var depth = 0; focus != null && focus != buyList && depth < 16; depth++, focus = focus->ParentNode)
        {
            if ((int)focus->Type < 1000) continue;
            var comp = ((AtkComponentNode*)focus)->Component;
            if (comp != null && TypeOf(comp, componentType) == ComponentType.ListItemRenderer) return focus;
        }
        return null;
    }

    internal static AtkComponentNumericInput* FindInput(AtkResNode* row,
        Func<nint, ComponentType>? componentType = null)
    {
        if (row == null || (int)row->Type < 1000) return null;
        var found = new HashSet<nint>();
        Collect(((AtkComponentNode*)row)->Component, found, new HashSet<nint>(), 0, componentType);
        return found.Count == 1 ? (AtkComponentNumericInput*)found.Single() : null;
    }

    private static ComponentType TypeOf(AtkComponentBase* comp, Func<nint, ComponentType>? componentType)
        => componentType == null ? comp->GetComponentType() : componentType((nint)comp);

    private static void Collect(AtkComponentBase* comp, HashSet<nint> found, HashSet<nint> visited,
        int depth, Func<nint, ComponentType>? componentType)
    {
        if (comp == null || depth > 8 || comp->UldManager.NodeList == null || !visited.Add((nint)comp)) return;
        for (var i = 0; i < comp->UldManager.NodeListCount && i < 512; i++)
        {
            var node = comp->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var child = ((AtkComponentNode*)node)->Component;
            if (child == null) continue;
            if (TypeOf(child, componentType) == ComponentType.NumericInput) found.Add((nint)child);
            else Collect(child, found, visited, depth + 1, componentType);
        }
    }
}
