using System;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

public sealed partial class UIReaderService
{
    private string _lastCompanionBranch = string.Empty;

    private unsafe string BuddySkillBranchContext(AtkResNode* node, string label)
    {
        if (FindAddonNameForNode(node) != "BuddySkill") return label;
        var current = node;
        for (var depth = 0; current != null && depth < 3;
             current = current->ParentNode, depth++)
        {
            if ((int)current->Type < 1000) continue;
            var component = ((AtkComponentNode*)current)->Component;
            if (component == null || component->GetComponentType() != ComponentType.Button) return label;
            var slot = ReadComponentTextById(component, 5).Trim();
            if (!uint.TryParse(slot, out var slotNumber) || slotNumber is < 1 or > 10) return label;
            var parent = current->ParentNode;
            if (parent == null || (int)parent->Type < 1000) return label;
            var branchComponent = ((AtkComponentNode*)parent)->Component;
            if (branchComponent == null) return label;
            var branch = ReadComponentTextById(branchComponent, 3).Trim();
            var level = ReadComponentTextById(branchComponent, 4).Trim();
            if (branch.Length == 0 || level.Length == 0) return label;
            if (branch == _lastCompanionBranch) return label;
            _lastCompanionBranch = branch;
            return AccessibilityStrings.CompanionSkillWithBranch(label, branch, level);
        }
        return label;
    }
}
