using Dalamud.Game.ClientState.Keys;
using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed partial class Plugin
{
    private bool _worldConfirmKeyOwned;

    // Retain ownership until physical key-up, even if the interaction opened a
    // dialog or inventory. A held Num0 must not also confirm that new window.
    private void SuppressOwnedWorldConfirmHold()
    {
        if (!_worldConfirmKeyOwned) return;
        if (!_keyWasDown[0x60]) _worldConfirmKeyOwned = false;
        else KeyState[VirtualKey.NUMPAD0] = false;
    }

    private bool HandleWorldObjectConfirmKey()
    {
        if (_worldConfirmKeyOwned) return true;
        if (!IsJustPressed("Numpad0")) return false;
        if (!ClientState.IsLoggedIn || !GameWindowFocus.IsActive || _textInputActive
            || _menu.IsOpen || _hotbar.IsSkillMenuOpen || _uiReader.HasActiveMenu
            || _uiReader.BlockingFocusedAddonForPlayerMenu() != null
            || !_navigation.HasWorldObjectConfirmSelection()) return false;
        if (!IsJustPressed("Numpad0", consume: true)) return false;
        _worldConfirmKeyOwned = true;
        _navigation.ConfirmSelectedWorldObject();
        return true;
    }
}
