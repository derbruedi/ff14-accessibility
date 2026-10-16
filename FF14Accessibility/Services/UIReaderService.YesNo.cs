using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private enum YesNoPress { None, Confirm, Cancel }

    private YesNoPress _yesNoPress;
    private long _yesNoPressAt;
    private bool _yesNoPressReported;
    private string _ynPressedQuestion = string.Empty;
    private bool _holdConfirmActive;
    private long _holdConfirmStart;
    private nint _holdConfirmAddon;
    private nint _holdConfirmNode;
    private string _holdConfirmQuestion = string.Empty;

    public unsafe void YesNoPressTick()
    {
        if (_yesNoPress == YesNoPress.None) return;
        var ptr = _gameGui.GetAddonByName("SelectYesno");
        if (ptr.IsNull || !((AtkUnitBase*)(nint)ptr)->IsVisible)
        {
            _yesNoPress = YesNoPress.None;
            return;
        }
        if ((nint)ptr != _ynPressedAddon) { _yesNoPress = YesNoPress.None; return; }
        if (_yesNoPressReported || Environment.TickCount64 - _yesNoPressAt < 2500) return;
        _yesNoPressReported = true;
        var text = ReadYesNoQuestion((AtkUnitBase*)(nint)ptr);
        if (text == _ynPressedQuestion)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.YesNoStillOpen);
            _log.Warning("[NativeButton] SelectYesno still open after a requested press; acceptance unconfirmed.");
        }
        _yesNoPress = YesNoPress.None;
    }

    private unsafe bool TryStartHeldConfirm(AtkUnitBase* addon, NativeDialogButtons.Choice choice)
    {
        if (_holdConfirmActive) return true;
        var node = (AtkResNode*)choice.Node;
        if (!choice.Held || choice.Index != 0 || !NativeDialogButtons.Available(node)) return false;
        _holdConfirmAddon = (nint)addon;
        _holdConfirmNode = choice.Node;
        _holdConfirmQuestion = ReadYesNoQuestion(addon);
        DispatchHoldEvent(node, (AtkEventType)6);
        if (!DispatchHoldEvent(node, (AtkEventType)3)) return false;
        _holdConfirmActive = true;
        _holdConfirmStart = Environment.TickCount64;
        return true;
    }

    public unsafe void YesNoHoldTick()
    {
        if (!_holdConfirmActive) return;
        var ptr = _gameGui.GetAddonByName("SelectYesno");
        if (ptr.IsNull || !((AtkUnitBase*)(nint)ptr)->IsVisible)
        {
            _holdConfirmActive = false;
            return;
        }
        var addon = (AtkUnitBase*)(nint)ptr;
        if ((nint)addon != _holdConfirmAddon || ReadYesNoQuestion(addon) != _holdConfirmQuestion)
        { _holdConfirmActive = false; return; }
        var node = (AtkResNode*)_holdConfirmNode;
        if (!NativeDialogButtons.Available(node)) { _holdConfirmActive = false; return; }
        var button = (AtkComponentHoldButton*)((AtkComponentNode*)node)->Component;
        var now = Environment.TickCount64;
        if (!TrySelectYesNo(out var selectedAddon, out var selected) || selectedAddon != _holdConfirmAddon
            || !selected.Held || selected.Node != _holdConfirmNode || now - _holdConfirmStart > 10000)
        {
            // Moving away cancels the gesture; never continue it on a new dialog.
            DispatchHoldEvent(node, (AtkEventType)7);
            DispatchHoldEvent(node, (AtkEventType)4);
            _holdConfirmActive = false;
            return;
        }
        // Let the game's own hold timer decide when its requirement is met.
        if (!button->IsTargetReached && !button->IsEventFired) return;
        RecordYesNoPress(addon, selected.Index);
        _holdConfirmActive = false;
        if (!DispatchHoldEvent(node, (AtkEventType)4))
        {
            _yesNoPress = YesNoPress.None;
            _tolk.SpeakInterrupt(AccessibilityStrings.ButtonNotResponding);
        }
    }

    private unsafe bool DispatchHoldEvent(AtkResNode* node, AtkEventType type)
    {
        if (node == null || !IsReadable(node)) return false;
        var evt = FindEventOfType(node, type, new List<string>(), 2);
        if (evt == null || evt->Listener == null || !IsReadable(evt->Listener)) return false;
        var data = default(AtkEventData);
        evt->Listener->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, &data);
        return true;
    }
}
