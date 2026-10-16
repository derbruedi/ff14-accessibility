using System.Text.RegularExpressions;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>ContentsTutorial layout and arrow directions measured in the
/// 2026-10-15 Sastasha tutorial: 8 = previous, 9 = next, 10 = acknowledgement,
/// 11 = close. Only the owning window's visible controls may supply a label.</summary>
internal static unsafe class ContentsTutorialText
{
    internal const string AddonName = "ContentsTutorial";

    internal static AtkResNode* Node(AtkUnitBase* addon, uint id)
    {
        if (addon == null || addon->UldManager.NodeList == null || addon->UldManager.NodeListCount > 256) return null;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null && node->NodeId == id) return node;
        }
        return null;
    }

    internal static string ReadText(AtkUnitBase* addon, uint id)
    {
        var node = Node(addon, id);
        if (node == null || node->Type != NodeType.Text || !TableNodeReader.IsVisible(node)
            || AtkText.Read((AtkTextNode*)node).Length == 0) return string.Empty;
        var textNode = (AtkTextNode*)node;
        // Parse before sanitizing: the raw 0x10 payload is a line break. Dropping
        // it as markup joined the page heading directly to its first sentence.
        var text = Dalamud.Memory.MemoryHelper.ReadSeString(
            (nint)(byte*)textNode->NodeText.StringPtr, textNode->NodeText.Length).TextValue;
        return Paragraphs(text);
    }

    internal static string Paragraphs(string text)
    {
        text = Regex.Replace(text, @"(?<=[^\s.!?:;…])\r?\n(?:[ \t]*\r?\n)+", ". ");
        return TolkService.Sanitize(Regex.Replace(text, @"[\r\n]+", " "));
    }

    internal static string Page(AtkUnitBase* addon, string repeatKey)
    {
        if (addon == null || !addon->IsVisible) return string.Empty;
        var heading = ReadText(addon, 2);
        var body = ReadText(addon, 5);
        // On opening, the game briefly exposes a previous tutorial's title
        // without its body (observed "Bozjan Southern Front" before "Dungeons").
        if (heading.Length == 0 || body.Length == 0) return string.Empty;
        var page = ReadText(addon, 7);
        var parts = page.Split('/');
        var counter = parts.Length == 2 && int.TryParse(parts[0], out var current)
            && int.TryParse(parts[1], out var total) && current > 0 && total >= current
            ? AccessibilityStrings.PageOf(current, total) : string.Empty;
        return $"{heading}.{counter} {body} {AccessibilityStrings.ContentsTutorialHelp(repeatKey)}".Trim();
    }

    internal static string FocusLabel(AtkUnitBase* addon, AtkResNode* focus)
    {
        if (addon == null || !addon->IsVisible || focus == null) return string.Empty;
        ReadOnlySpan<uint> ids = [8, 9, 10, 11];
        foreach (var id in ids)
        {
            var owner = Node(addon, id);
            if (owner == null || !TableNodeReader.IsVisible(owner)
                || !TableNodeReader.IsVisible(focus) || !NativeDialogButtons.Contains(owner, focus)) continue;
            if (id == 8) return AccessibilityStrings.PreviousPage;
            if (id == 9) return AccessibilityStrings.NextPage;
            if (id == 11) return AccessibilityStrings.CloseControl;
            if ((int)owner->Type < 1000) return string.Empty;
            var component = ((AtkComponentNode*)owner)->Component;
            if (!AtkText.IsReadable(component)) return string.Empty;
            return $"{AccessibilityStrings.TutorialUnderstood}, {AccessibilityStrings.SwitchControl}, "
                + (((AtkComponentButton*)component)->IsChecked ? AccessibilityStrings.StateOn : AccessibilityStrings.StateOff);
        }
        return string.Empty;
    }

    internal static AtkResNode* DefaultButton(AtkUnitBase* addon)
    {
        if (addon == null || !addon->IsVisible || addon->NumBlockingAddons != 0) return null;
        var close = Node(addon, 11);
        // A visible but disabled close button can require acknowledgement.
        // Never tick that checkbox or bypass its enabled state for the player.
        if (close != null && TableNodeReader.IsVisible(close))
            return NativeDialogButtons.Available(close) ? close : null;
        var next = Node(addon, 9);
        return NativeDialogButtons.Available(next) ? next : null;
    }

    internal static bool UsesNativeToggle(AtkUnitBase* addon, AtkResNode* focus)
        => addon != null && addon->IsVisible && focus != null
            && NativeDialogButtons.Contains(Node(addon, 10), focus);
}
