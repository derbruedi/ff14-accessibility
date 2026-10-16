using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private SystemVolumeEdit? _systemVolumeEdit;
    private bool _systemVolumeDrain;
    public bool IsSystemVolumeEditing => _systemVolumeEdit != null;

    private unsafe bool TrySystemVolumeLabel(AtkUnitBase* addon, AtkResNode* owner, out string label)
        => SystemVolumeNodes.TryLabel(addon, owner, p => IsEffectivelyVisible((AtkResNode*)p),
            p => AtkText.Read((AtkTextNode*)p), out label);

    private unsafe bool TryGetSystemVolume(out SystemVolumeEdit.Target target)
    {
        target = default;
        var addon = (AtkUnitBase*)(nint)_gameGui.GetAddonByName("ConfigSystem");
        var stage = AtkStage.Instance();
        if (addon == null || !addon->IsVisible || stage == null || stage->AtkInputManager == null) return false;
        var focus = stage->AtkInputManager->FocusedNode;
        if (focus == null) return false;
        var owner = FindTopLevelOwner(addon, focus, out _);
        if (owner == null || (int)owner->Type < 1000 || !IsEffectivelyVisible(owner)
            || (owner->NodeFlags & NodeFlags.Enabled) == 0) return false;
        var comp = ((AtkComponentNode*)owner)->Component;
        if (comp == null || comp->GetComponentType() != ComponentType.Slider
            || !TrySystemVolumeLabel(addon, owner, out var label)) return false;
        var slider = (AtkComponentSlider*)comp;
        if (slider->MinValue != 0 || slider->MaxValue != 100 || slider->Value is < 0 or > 100) return false;
        target = new((nint)addon, (nint)owner, (nint)slider, label, slider->Value);
        return true;
    }

    public bool BeginSystemVolume()
    {
        if (!TryGetSystemVolume(out var target)) return false;
        _systemVolumeEdit = new(target);
        _tolk.SpeakInterrupt(AccessibilityStrings.SliderPercent(target.Label, target.Value.ToString())
            + ". " + AccessibilityStrings.SystemVolumeEditInstructions);
        return true;
    }

    public unsafe bool HandleSystemVolumeKeys(MenuInput input, bool windowActive, bool modifiers)
    {
        if (_systemVolumeDrain)
        {
            _systemVolumeDrain = input.AnyDown;
            if (_systemVolumeDrain) { input.ConsumeAll(); return true; }
        }
        var edit = _systemVolumeEdit;
        if (edit == null) return false;
        input.ConsumeAll();
        if (!windowActive || !TryGetSystemVolume(out var current) || !edit.Matches(current))
        {
            EndSystemVolume(AccessibilityStrings.SystemVolumeChanged);
            return true;
        }
        if (input.Just(0x1B)) { EndSystemVolume(AccessibilityStrings.SystemVolumeCancelled); return true; }
        if (modifiers) return true;
        if (input.Just(0x0D))
        {
            if (!edit.TryValue(out var value)) { _tolk.SpeakInterrupt(AccessibilityStrings.SystemVolumeRange); return true; }
            // Native event 29 updates the game's pending settings. Do not write
            // a config file or press Apply; saving remains the player's action.
            var applied = edit.Apply(() => TryGetSystemVolume(out var live) ? live : null,
                (slider, requested) => ((AtkComponentSlider*)slider)->SetValue(requested, true), out value);
            _log.Info($"[SystemVolume] id={((AtkResNode*)current.Control)->NodeId}, '{current.Label}', requested={value}, confirmed={applied}.");
            if (applied && _csFocusAddon == current.Addon && _csFocusTop == current.Control)
                _csFocusValue = value.ToString();
            EndSystemVolume(applied ? AccessibilityStrings.SliderPercent(current.Label, value.ToString())
                : AccessibilityStrings.SystemVolumeNotApplied);
            return true;
        }
        if (input.Just(0x08)) edit.Backspace();
        else if (input.Just(0x2E)) edit.Clear();
        else
        {
            var digit = -1;
            for (var i = 0; i <= 9; i++) if (input.Just(0x30 + i) || input.Just(0x60 + i)) { digit = i; break; }
            if (digit < 0) return true;
            edit.Digit(digit);
        }
        _tolk.SpeakInterrupt(AccessibilityStrings.SystemVolumeDraft(edit.Text));
        return true;
    }

    private void EndSystemVolume(string message)
    {
        _systemVolumeEdit = null;
        _systemVolumeDrain = true;
        _tolk.SpeakInterrupt(message);
    }
}
