using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private unsafe bool IsActionMenuEntry(AtkResNode* node)
        => node != null && FindAddonNameForNode(node) == "ActionMenu"
            && (FocusIsActionSlot(node) || ActionMenuText.ParseLabel(GetActionMenuSlotLabel(node)).Level > 0);

    private unsafe ActionMenuText.Entry ReadUnboundAction(AtkResNode* node)
    {
        var label = GetActionMenuSlotLabel(node);
        // An absent or delayed tooltip must not prevent translating the row.
        // The visible description is usable only when its name matches exactly.
        var fallback = TryReadActionDetailPanel(out var name, out var level, out var description)
            && ActionMenuText.Matches(name, label)
            && (ActionMenuText.ParseLabel(label).Level == 0 || level == 0 || ActionMenuText.ParseLabel(label).Level == level)
                ? description : "";
        HashSet<uint>? traitIds = null;
        var agent = AgentActionMenu.Instance();
        var owner = FindAddonForNode(node);
        if (agent != null && owner != null && agent->AddonId == owner->Id
            && agent->TraitList.LongCount is > 0 and <= 256)
        {
            // This is the menu's actual list, not a guess based on the player's
            // current job. Identical names across jobs can describe different effects.
            traitIds = [];
            foreach (ref var trait in agent->TraitList.AsSpan()) traitIds.Add(trait.ActionId);
        }
        return _descriptions.FromActionMenuLabel(label, fallback, traitIds);
    }

    private unsafe bool TryReadSelectedActionDescription()
    {
        var stage = AtkStage.Instance();
        var node = stage == null || stage->AtkInputManager == null ? null : stage->AtkInputManager->FocusedNode;
        if (node == null || FindAddonNameForNode(node) != "ActionMenu") return false;
        if (!IsActionMenuEntry(node)) { _tolk.SpeakInterrupt(AccessibilityStrings.ActionChooseEntry); return true; }
        var action = _tooltips.TryGetActionDeep(node, 6) ?? TryFindActionBindingNear(node);
        var entry = action is { } a
            ? new ActionMenuText.Entry(DescribeActionDetail(a.Kind, a.Id), ActionMenuDescription(a.Kind, a.Id))
            : ReadUnboundAction(node);
        if (entry.Name.Length == 0) entry = ReadUnboundAction(node);
        var desc = entry.Description.Length > 0 ? entry.Description : AccessibilityStrings.ActionDescriptionUnavailable;
        // Arm the exact same identity as automatic dwell; manual reading must
        // not be followed by an identical queued description 0.4 seconds later.
        _actionDwellId = action?.Id ?? 0;
        if (action is { } bound) _actionDwellKind = bound.Kind;
        _actionDetailDwellKey = action != null ? "" : ((nint)node).ToString() + ":" + GetActionMenuSlotLabel(node);
        _actionDwellDescSpoken = true;
        _tolk.SpeakInterrupt(entry.Name + ". " + desc);
        return true;
    }
}
