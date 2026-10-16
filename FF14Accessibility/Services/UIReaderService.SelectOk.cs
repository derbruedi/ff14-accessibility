using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private readonly SelectOkAnnouncement _selectOkAnnouncement = new();

    private bool IsSuppressedCharaSelect(string name)
        => (name == "CharaSelect" || name.StartsWith("_CharaSelect", StringComparison.Ordinal))
           && IsAddonVisible("SelectOk");

    private unsafe void OnSelectOkUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)(nint)args.Addon;
        if (addon == null) return;
        var visible = addon->IsVisible;
        var text = _selectOkAnnouncement.Update(visible ? ReadAllTexts(addon) : string.Empty,
            visible, Environment.TickCount64);
        if (text.Length == 0) return;
        _dialogOpenedAt = DateTime.UtcNow;
        _log.Info($"[LoginDialog] body: {text}");
        _tolk.SpeakInterrupt(text);
    }

    private unsafe bool TryReadSelectOk()
    {
        var pointer = _gameGui.GetAddonByName("SelectOk");
        if (pointer.IsNull) return false;
        var addon = (AtkUnitBase*)(nint)pointer;
        if (!addon->IsVisible) return false;
        var text = _selectOkAnnouncement.Update(ReadAllTexts(addon), true, Environment.TickCount64, repeat: true);
        if (text.Length > 0)
        {
            _dialogOpenedAt = DateTime.UtcNow;
            _tolk.SpeakInterrupt(text);
        }
        return true;
    }
}
