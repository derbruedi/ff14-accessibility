using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    public unsafe bool OwnsSelectionDialogConfirm => TryGetSelectionDialog(out _, out _);

    private unsafe bool TryGetSelectionDialog(out nint address, out NativeSelectionDialog.Choice choice)
    {
        address = 0;
        choice = default;
        if (IsChatInputActive()) return false;
        var focus = LiveFocus();
        var addon = FindAddonForNode(focus);
        if (!NativeSelectionDialog.TryResolve(addon, focus, out choice)) return false;
        address = (nint)addon;
        return true;
    }

    public unsafe void ConfirmSelectionDialog()
    {
        // Re-read after claiming the key: speech caches and a previous frame's
        // list selection must never choose the answer.
        if (!TryGetSelectionDialog(out var address, out _)) return;
        var name = ((AtkUnitBase*)address)->NameString;
        var result = NativeSelectionDialog.Confirm((AtkUnitBase*)address, LiveFocus(), choice =>
        {
            _log.Info($"[SelectionDialog] {name}: native ListItemClick requested for focused row={choice.Index}.");
            NativeSelectionDialog.Dispatch(choice);
        });
        if (result == NativeSelectionDialog.Result.Unavailable)
        {
            _log.Info($"[SelectionDialog] {name}: focused row is unavailable.");
            _tolk.SpeakInterrupt(AccessibilityStrings.DialogChoiceUnavailable);
        }
    }
}
