using System.Globalization;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

internal enum SystemSoundChannel { Master, Music, Effects, Voice, System, Ambient, Performance, Self, Party, OtherPlayers }

internal static unsafe class SystemVolumeNodes
{
    // Verified against ui/uld/configsystem.uld from the installed game. Labels
    // are RIGHT of the slider, in the same row, not above or before it.
    internal readonly record struct Binding(uint Slider, uint Parent, uint Label, uint TextRow, SystemSoundChannel Channel);
    internal static readonly Binding[] Bindings = [
        new(113,112,115,4047,SystemSoundChannel.Master), new(117,116,119,4048,SystemSoundChannel.Music),
        new(121,120,123,4049,SystemSoundChannel.Effects), new(125,124,127,4050,SystemSoundChannel.Voice),
        new(129,128,131,4051,SystemSoundChannel.System), new(133,132,135,4052,SystemSoundChannel.Ambient),
        new(137,136,139,8732,SystemSoundChannel.Performance), new(143,142,144,4127,SystemSoundChannel.Self),
        new(146,145,147,4128,SystemSoundChannel.Party), new(149,148,150,4129,SystemSoundChannel.OtherPlayers)
    ];

    internal static bool TryLabel(AtkUnitBase* addon, AtkResNode* control,
        Func<nint, bool> visible, Func<nint, string> readText, out string label)
    {
        label = string.Empty;
        if (addon == null || control == null || !visible((nint)control)) return false;
        foreach (var binding in Bindings)
        {
            if (binding.Slider != control->NodeId) continue;
            var parent = control->ParentNode;
            if (parent == null || parent->NodeId != binding.Parent) return false;
            for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null || node->NodeId != binding.Label || node->Type != NodeType.Text
                    || node->ParentNode != parent || !visible((nint)node)) continue;
                var raw = readText((nint)node).Trim();
                if (raw.Length == 0) return false;
                label = Loc.IsRussian ? AccessibilityStrings.SystemVolumeName(binding.Channel) : raw;
                return true;
            }
            return false;
        }
        return false;
    }
}

internal sealed class SystemVolumeEdit
{
    internal readonly record struct Target(nint Addon, nint Control, nint Slider, string Label, int Value);
    internal Target Original { get; }
    internal string Text { get; private set; }
    private bool replace = true;
    internal SystemVolumeEdit(Target target) { Original = target; Text = target.Value.ToString(CultureInfo.InvariantCulture); }
    internal bool Matches(Target current) => current == Original;
    internal void Digit(int digit)
    {
        if (digit is < 0 or > 9) return;
        if (replace) Text = string.Empty;
        replace = false;
        if (Text.Length < 4) Text += (char)('0' + digit);
    }
    internal void Backspace() { Text = replace ? string.Empty : Text.Length > 0 ? Text[..^1] : Text; replace = false; }
    internal void Clear() { Text = string.Empty; replace = false; }
    internal bool TryValue(out int value) => int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
        && Text.Length <= 3 && value is >= 0 and <= 100;
    internal bool Apply(Func<Target?> readCurrent, Action<nint, int> setNative, out int value)
    {
        if (!TryValue(out value) || readCurrent() != Original) return false;
        setNative(Original.Slider, value);
        return readCurrent() == Original with { Value = value };
    }
}
