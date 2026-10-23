using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private readonly Dictionary<string, SquadronAnnouncement> _squadronAnnouncements = [];
    private readonly Dictionary<string, long> _squadronPollAt = [];
    private readonly SquadronReward _squadronReward = new();
    private string _lastSquadronName = "";

    private void ResetSquadron(string name)
    {
        _squadronAnnouncements.Remove(name);
        _squadronPollAt.Remove(name);
        if (_lastSquadronName == name) _lastSquadronName = "";
    }

    private static unsafe string ReadSquadronNode(nint node) => AtkText.Read((AtkTextNode*)node);

    private unsafe string ReadSquadronReward(nint address)
    {
        var node = (AtkComponentNode*)address;
        if (node == null || node->Component == null) return "";
        var icon = FindSlotIcon(node->Component);
        if (icon == null || icon->IconId == 0) return "";
        var name = _squadronReward.Resolve(icon->IconId, Loc.Mode, GameDisplayText.Revision,
            _data.GetExcelSheet<Lumina.Excel.Sheets.Item>(), row => row.Icon,
            row => RussianGameText.Name(_data, row, x => x.Name));
        if (name.Length == 0) return "";
        var quantity = ReadIconQuantity(icon);
        return quantity.Length > 0 ? AccessibilityStrings.RewardItemQuantity(quantity, name) : name;
    }

    private unsafe void UpdateSquadron(string name, AtkUnitBase* addon)
    {
        var now = Environment.TickCount64;
        if (addon == null || !addon->IsVisible) { ResetSquadron(name); return; }
        var focus = LiveFocus();
        if (addon->NumBlockingAddons != 0 || IsChatInputActive()
            || (focus != null && !NativeControlScope.Owns(addon, focus))) return;
        if (_squadronPollAt.TryGetValue(name, out var next) && now < next) return;
        _squadronPollAt[name] = now + 100;
        _lastSquadronName = name;
        if (!_squadronAnnouncements.TryGetValue(name, out var state))
            _squadronAnnouncements[name] = state = new SquadronAnnouncement();
        var training = name == "GcArmyTraining";
        // Training preview changes while browsing. Announce only the opening
        // hint automatically; all numeric details are read on demand.
        var text = state.Update(training ? AccessibilityStrings.SquadronTraining
            : SquadronReader.Page(name, addon, ReadSquadronNode, ReadSquadronReward), true, now);
        if (text.Length == 0) return;
        _log.Info($"[Squadron] {name}: {text}");
        // Initial/default focus must not cut the details off with a bare button
        // label. No action or selection is changed by this announcement.
        _dialogOpenedAt = DateTime.UtcNow;
        var key = AccessibilityStrings.SpokenKeyLabel(_config.KeyReadUI);
        _tolk.SpeakInterrupt(text + " " + (training ? AccessibilityStrings.SquadronTrainingReadHelp(key)
            : AccessibilityStrings.SquadronReadHelp(key)));
    }

    private unsafe bool TryReadSquadronFocus(AtkResNode* focus, out string text)
    {
        text = "";
        var addon = FindAddonForNode(focus);
        if (addon == null || !SquadronReader.Supports(addon->NameString)) return false;
        if (SquadronReader.IsItemReward(addon->NameString, addon, focus) && _lastFocusedItemName.Length > 0) return false;
        text = SquadronReader.Focus(addon->NameString, addon, focus, ReadSquadronNode,
            p => _tooltips.TryGetTooltip((AtkResNode*)p), ReadSquadronReward);
        // Claim this window even when its current row is hidden/stale. Falling
        // through would let the generic reader speak the rejected old values.
        return true;
    }

    private unsafe bool TryReadSquadron()
    {
        if (IsChatInputActive()) return false;
        var focus = LiveFocus();
        var addon = focus == null ? null : FindAddonForNode(focus);
        if (addon == null && focus == null && _lastSquadronName.Length > 0)
        {
            var pointer = _gameGui.GetAddonByName(_lastSquadronName);
            if (!pointer.IsNull) addon = (AtkUnitBase*)(nint)pointer;
        }
        if (addon == null || !SquadronReader.Supports(addon->NameString)
            || !addon->IsVisible || addon->NumBlockingAddons != 0) return false;
        var training = addon->NameString == "GcArmyTraining";
        var text = training ? SquadronReader.TrainingDetails(addon, focus, ReadSquadronNode,
            p => _tooltips.TryGetTooltip((AtkResNode*)p))
            : SquadronReader.Page(addon->NameString, addon, ReadSquadronNode, ReadSquadronReward);
        if (!_squadronAnnouncements.TryGetValue(addon->NameString, out var state))
            _squadronAnnouncements[addon->NameString] = state = new SquadronAnnouncement();
        // Consume a pending automatic read, so it cannot interrupt this command.
        state.Update(training ? AccessibilityStrings.SquadronTraining : text, true, Environment.TickCount64, repeat: true);
        _dialogOpenedAt = DateTime.UtcNow;
        _log.Info($"[Squadron] Repeat {addon->NameString}: {text}");
        _tolk.SpeakInterrupt(text.Length > 0 ? text : AccessibilityStrings.SquadronLoading);
        return true;
    }
}
