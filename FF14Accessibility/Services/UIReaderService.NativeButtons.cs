using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private nint _ynPressedAddon;

    private unsafe AtkResNode* LiveFocus()
    {
        var stage = AtkStage.Instance();
        return stage == null || stage->AtkInputManager == null ? null : stage->AtkInputManager->FocusedNode;
    }

    public unsafe bool CanConfirmYesNo => TrySelectYesNo(out _, out _);
    public unsafe bool OwnsEnterButton => CanConfirmYesNo || FindFocusedButton() != null || OwnsContentsTutorialConfirm;

    private unsafe bool TrySelectYesNo(out nint address, out NativeDialogButtons.Choice choice)
    {
        address = 0; choice = default;
        if (IsChatInputActive()) return false;
        var ptr = _gameGui.GetAddonByName("SelectYesno");
        if (ptr.IsNull) return false;
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible || addon->NumBlockingAddons != 0) return false;
        var focus = LiveFocus();
        if (focus == null || FindAddonForNode(focus) != addon) return false;
        address = (nint)addon;
        return NativeDialogButtons.TrySelect((AddonSelectYesno*)addon, focus, out choice);
    }

    private unsafe void RecordYesNoPress(AtkUnitBase* addon, int index)
    {
        _yesNoPress = index == 1 ? YesNoPress.Cancel : YesNoPress.Confirm;
        _yesNoPressAt = Environment.TickCount64;
        _yesNoPressReported = false;
        _ynPressedQuestion = ReadYesNoQuestion(addon);
        _ynPressedAddon = (nint)addon;
    }

    private unsafe void ConfirmFocusedYesNo()
    {
        if (!TrySelectYesNo(out var address, out var choice))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.DialogChooseButton);
            return;
        }
        var addon = (AtkUnitBase*)address;
        if (choice.Held)
        {
            // Hold controls retain their registered hold events; a normal
            // callback must never bypass their confirmation requirement.
            if (choice.Index != 0 || !TryStartHeldConfirm(addon, choice))
                _tolk.SpeakInterrupt(AccessibilityStrings.ButtonNotResponding);
            return;
        }
        // Capture before dispatch: a successful callback can release the addon.
        RecordYesNoPress(addon, choice.Index);
        var value = stackalloc AtkValue[1]; value->SetInt(choice.Index);
        var accepted = addon->FireCallback(1, value, close: true);
        _log.Info($"[NativeButton] SelectYesno: live focused side={choice.Index}, callback close=true, accepted={accepted}.");
        if (!accepted)
        {
            _yesNoPress = YesNoPress.None;
            _tolk.SpeakInterrupt(AccessibilityStrings.ButtonNotResponding);
        }
    }

    private unsafe bool MoveYesNoFocus(int delta)
    {
        if (!TrySelectYesNo(out var address, out var selected)) return false;
        var addon = (AddonSelectYesno*)address;
        AtkComponentButton* target = null;
        for (var step = 1; step <= 3; step++)
        {
            var side = (selected.Index + (delta < 0 ? -step : step) + 3) % 3;
            target = NativeDialogButtons.ButtonForSide(addon, side);
            if (target != null) break;
        }
        if (target == null) return false;
        var focus = target->GetFocusNode();
        if (focus == null) return false;
        var stage = AtkStage.Instance();
        if (stage == null || stage->AtkInputManager == null
            || !stage->AtkInputManager->SetFocus(focus, (AtkUnitBase*)addon, 0)) return false;
        if (!NativeDialogButtons.TrySelect(addon, stage->AtkInputManager->FocusedNode, out var actual)) return false;
        var button = (AtkComponentButton*)((AtkComponentNode*)actual.Node)->Component;
        var text = AtkText.ReadClean(button->ButtonTextNode).Trim();
        if (text.Length > 0 && actual.Index != selected.Index) _tolk.SpeakInterrupt(text);
        _lastYesNoText = text;
        return true;
    }

    private unsafe AtkResNode* FindFocusedButton()
    {
        if (IsChatInputActive()) return null;
        var focus = LiveFocus(); var addon = FindAddonForNode(focus);
        if (addon == null || !addon->IsVisible || addon->NumBlockingAddons != 0) return null;
        var control = NativeControlScope.Control(addon, focus);
        if (control == null) return null;
        var comp = ((AtkComponentNode*)control)->Component;
        return IsReadable(comp) && comp->GetComponentType() == ComponentType.Button ? control : null;
    }

    private unsafe bool TryPressFocusedButton()
    {
        var node = FindFocusedButton();
        if (node == null) return false;
        if (!NativeDialogButtons.Available(node))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.DisabledButton(GetVisibleTextFromNodeTree(node)));
            return true;
        }
        var label = GetVisibleTextFromNodeTree(node);
        var addon = FindAddonForNode(node);
        var eventToSend = FindEventOfType(node, AtkEventType.ButtonClick, [], 2);
        if (eventToSend == null || !IsReadable(eventToSend->Listener))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ButtonNotResponding);
            return true;
        }
        var param = eventToSend->Param; var window = addon->NameString;
        var data = default(AtkEventData);
        eventToSend->Listener->ReceiveEvent(AtkEventType.ButtonClick, (int)param, eventToSend, &data);
        _log.Info($"[NativeButton] '{window}' '{label}': ButtonClick({param}) requested for focused button.");
        return true;
    }

    private unsafe bool TryReadYesNoDialog()
    {
        var ptr = _gameGui.GetAddonByName("SelectYesno");
        if (ptr.IsNull || !((AtkUnitBase*)(nint)ptr)->IsVisible) return false;
        var addon = (AtkUnitBase*)(nint)ptr;
        var (confirm, cancel) = ReadYesNoLabels(addon);
        var selected = TrySelectYesNo(out _, out var choice)
            ? ReadFirstTextInComponent((AtkResNode*)choice.Node) : string.Empty;
        _tolk.SpeakInterrupt($"{ReadYesNoQuestion(addon)} {AccessibilityStrings.DialogButtons(confirm, cancel)} {selected}".Trim());
        return true;
    }

    // CharacterInspect equipment cells measured in the installed ULD. An
    // unresolved cell gets a neutral label, never an invented item name.
    internal static string InspectControlLabel(string addonName, uint ownerId)
        => addonName == "CharacterInspect" && (ownerId == 17 || ownerId is >= 43 and <= 55)
            ? AccessibilityStrings.InspectGearSlot : string.Empty;

    private unsafe string ReadUnlabelledInspectSlot(AtkResNode* focus)
    {
        var addon = FindAddonForNode(focus);
        if (addon == null || addon->NameString != "CharacterInspect") return string.Empty;
        var depth = 0;
        for (var current = focus; current != null && depth < 4; current = current->ParentNode, depth++)
            if ((int)current->Type >= 1000) return InspectControlLabel(addon->NameString, current->NodeId);
        return string.Empty;
    }
}
