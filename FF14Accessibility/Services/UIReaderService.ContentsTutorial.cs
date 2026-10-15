using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private readonly SelectOkAnnouncement _tutorialAnnouncement = new();

    private unsafe void OnContentsTutorialUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        var visible = addon != null && addon->IsVisible;
        var text = _tutorialAnnouncement.Update(
            ContentsTutorialText.Page(addon, AccessibilityStrings.SpokenKeyLabel(_config.KeyReadUI)),
            visible, Environment.TickCount64);
        if (text.Length == 0) return;
        _log.Info($"[Tutorial] ContentsTutorial: '{text}'");
        // The game's initial focus must not cut the page off with an arrow name.
        // Moving to another control after this short opening guard reads it normally.
        _dialogOpenedAt = DateTime.UtcNow;
        _tolk.SpeakInterrupt(text);
    }

    private unsafe bool TryReadContentsTutorialFocus(AtkResNode* focus, out string text)
    {
        text = string.Empty;
        var addon = FindAddonForNode(focus);
        if (addon == null || addon->NameString != ContentsTutorialText.AddonName) return false;
        text = ContentsTutorialText.FocusLabel(addon, focus);
        return text.Length > 0;
    }

    private unsafe bool TryReadContentsTutorial()
    {
        var ptr = _gameGui.GetAddonByName(ContentsTutorialText.AddonName);
        if (ptr.IsNull) return false;
        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible || addon->NumBlockingAddons != 0) return false;
        var focus = LiveFocus();
        if (focus != null && FindAddonForNode(focus) != addon) return false;
        var text = _tutorialAnnouncement.Update(
            ContentsTutorialText.Page(addon, AccessibilityStrings.SpokenKeyLabel(_config.KeyReadUI)),
            true, Environment.TickCount64, repeat: true);
        _tolk.SpeakInterrupt(text.Length > 0 ? text : AccessibilityStrings.TutorialLoading);
        return true;
    }

    private unsafe bool OwnsContentsTutorialConfirm
    {
        get
        {
            if (IsChatInputActive()) return false;
            var ptr = _gameGui.GetAddonByName(ContentsTutorialText.AddonName);
            if (ptr.IsNull) return false;
            var addon = (AtkUnitBase*)(nint)ptr;
            var focus = LiveFocus();
            return FindAddonForNode(focus) == addon && !ContentsTutorialText.UsesNativeToggle(addon, focus)
                && ContentsTutorialText.DefaultButton(addon) != null;
        }
    }

    // Enter normally uses the actual focused button's original event. Only the
    // no-button fallback needs a default: the right-hand arrow, then visible close.
    private unsafe void AdvanceOrCloseContentsTutorial(AtkUnitBase* addon)
    {
        var focus = LiveFocus();
        if (FindAddonForNode(focus) != addon || ContentsTutorialText.UsesNativeToggle(addon, focus)) return;
        var button = ContentsTutorialText.DefaultButton(addon);
        var before = ContentsTutorialText.ReadText(addon, 7);
        var id = button == null ? 0 : button->NodeId;
        if (button == null || !DispatchClick(button))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ButtonNotResponding);
            return;
        }
        _log.Info($"[Tutorial] Default button id={id} requested, page before='{before}'.");
    }
}
