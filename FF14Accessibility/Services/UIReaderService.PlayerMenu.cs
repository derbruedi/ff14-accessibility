using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private DateTime _playerMenuProbeUntil;
    private bool _playerMenuProbeDone = true;
    private bool _playerMenuSawVisible;
    private readonly List<string> _playerMenuOpenedWindows = [];

    public unsafe bool OpenPlayerContextMenu(nint address)
    {
        if (address == nint.Zero) return false;
        var hud = AgentHUD.Instance();
        if (hud == null) return false;

        hud->OpenContextMenuFromTarget((CSGameObject*)address);
        _playerMenuProbeUntil = DateTime.UtcNow.AddSeconds(6);
        _playerMenuProbeDone = false;
        _playerMenuSawVisible = false;
        _playerMenuOpenedWindows.Clear();
        return true;
    }

    public unsafe void UpdatePlayerMenuProbe()
    {
        if (_playerMenuProbeDone) return;
        var addonPtr = _gameGui.GetAddonByName("ContextMenu");
        var addon = addonPtr.IsNull ? null : (AtkUnitBase*)(nint)addonPtr;
        var visible = addon != null && addon->IsVisible;
        var list = visible ? FindListInAddon(addon) : null;
        var count = list != null ? GetListEntryCount(list) : 0;
        if (visible) _playerMenuSawVisible = true;
        if (count <= 0 && DateTime.UtcNow <= _playerMenuProbeUntil) return;

        _playerMenuProbeDone = true;
        if (count > 0)
        {
            var entries = new List<string>();
            for (var i = 0; i < Math.Min(count, 32); i++)
                entries.Add($"{i + 1}. {ReadListItemText(list, i)}");
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerMenuEntries(count, string.Join(". ", entries)));
        }
        else if (!_playerMenuSawVisible && _playerMenuOpenedWindows.Count > 0)
            _tolk.SpeakInterrupt(AccessibilityStrings.PlayerMenuWindowsOpened(
                _playerMenuOpenedWindows.Count, string.Join(", ", _playerMenuOpenedWindows)));
        else
            _tolk.SpeakInterrupt(_playerMenuSawVisible
                ? AccessibilityStrings.PlayerMenuNoList : AccessibilityStrings.PlayerMenuNotOpened);
    }

    private void RecordPlayerMenuWindow(string name)
    {
        if (!_playerMenuProbeDone && DateTime.UtcNow <= _playerMenuProbeUntil &&
            !string.IsNullOrEmpty(name) && !_playerMenuOpenedWindows.Contains(name))
            _playerMenuOpenedWindows.Add(name);
    }
}
