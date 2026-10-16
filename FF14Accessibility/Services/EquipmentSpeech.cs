using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Equipment facts for speech only. Raw item names remain search keys.</summary>
public static class EquipmentSpeech
{
    public static string ComparisonSlot(string text)
    {
        if (!Loc.IsRussian) return text;
        // Keep the exact compared ring side, which is more specific than the
        // two alternative slots on an unequipped ring. Unknown labels stay intact.
        var prefix = new[] { "Slot: ", "Anlegeplatz: " }.FirstOrDefault(x => text.StartsWith(x, StringComparison.Ordinal));
        if (prefix == null) return text;
        var slot = text[prefix.Length..].Trim();
        var translated = slot switch
        {
            "Main Hand" or "Main hand" or "Haupthand" => AccessibilityStrings.EquipMainHand,
            "Off Hand" or "Off hand" or "Nebenhand" => AccessibilityStrings.SlotOffHand,
            "Head" or "Kopf" => AccessibilityStrings.SlotHead,
            "Body" or "Rumpf" => AccessibilityStrings.SlotBody,
            "Hands" or "Hände" => AccessibilityStrings.SlotHands,
            "Waist" or "Taille" => AccessibilityStrings.SlotWaist,
            "Legs" or "Beine" => AccessibilityStrings.SlotLegs,
            "Feet" or "Füße" => AccessibilityStrings.SlotFeet,
            "Ears" or "Ohren" => AccessibilityStrings.SlotEars,
            "Neck" or "Hals" => AccessibilityStrings.SlotNeck,
            "Wrists" or "Handgelenke" => AccessibilityStrings.SlotWrists,
            "Ring" => AccessibilityStrings.SlotRing,
            "Right Ring" or "Right ring" or "Rechter Ring" => AccessibilityStrings.EquipRightRing,
            "Left Ring" or "Left ring" or "Linker Ring" => AccessibilityStrings.EquipLeftRing,
            "Soul Crystal" or "Jobkristall" => AccessibilityStrings.SlotSoulCrystal,
            _ => null,
        };
        return translated == null ? text : AccessibilityStrings.ComparedEquipmentSlot(translated);
    }

    public static string Name(IDataManager data, Item item)
        => WithSlots(item, RussianGameText.Name(data, item, x => x.Name));

    public static string WithSlots(Item item, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var slots = Slots(item);
        return slots.Length == 0 ? name : $"{name}, {slots}";
    }

    public static string Slots(Item item)
        => item.EquipSlotCategory.RowId != 0 && item.EquipSlotCategory.ValueNullable is { } slot
            ? Slots(slot) : string.Empty;

    public static string Slots(EquipSlotCategory slot)
    {
        var available = new List<string>();
        var blocked = new List<string>();
        void Add(int value, string label)
        {
            if (value > 0) available.Add(label);
            if (value < 0) blocked.Add(label);
        }
        Add(slot.MainHand, AccessibilityStrings.EquipMainHand);
        Add(slot.OffHand, AccessibilityStrings.SlotOffHand);
        Add(slot.Head, AccessibilityStrings.SlotHead);
        Add(slot.Body, AccessibilityStrings.SlotBody);
        Add(slot.Gloves, AccessibilityStrings.SlotHands);
        Add(slot.Waist, AccessibilityStrings.SlotWaist);
        Add(slot.Legs, AccessibilityStrings.SlotLegs);
        Add(slot.Feet, AccessibilityStrings.SlotFeet);
        Add(slot.Ears, AccessibilityStrings.SlotEars);
        Add(slot.Neck, AccessibilityStrings.SlotNeck);
        Add(slot.Wrists, AccessibilityStrings.SlotWrists);
        // Both positive ring fields mean alternatives, not two occupied slots.
        if (slot.FingerL > 0 && slot.FingerR > 0)
            available.Add(AccessibilityStrings.EquipEitherRing);
        else
        {
            Add(slot.FingerL, AccessibilityStrings.EquipLeftRing);
            Add(slot.FingerR, AccessibilityStrings.EquipRightRing);
        }
        Add(slot.SoulCrystal, AccessibilityStrings.SlotSoulCrystal);
        if (available.Count == 0) return string.Empty;
        var text = AccessibilityStrings.EquipmentSlots(string.Join(", ", available));
        if (blocked.Count > 0) text += ", " + AccessibilityStrings.EquipmentBlockedSlots(string.Join(", ", blocked));
        return text;
    }

    public static string ClassCategory(IDataManager data, ClassJobCategory category)
    {
        var original = category.Name.ExtractText().Trim();
        if (!Loc.IsRussian) return original;
        if (!data.GetExcelSheet<ClassJobCategory>(ClientLanguage.English).TryGetRow(category.RowId, out var english))
            return original;
        var source = english.Name.ExtractText().Trim();
        var group = AccessibilityStrings.RussianEquipmentClassGroup(source);
        if (group != null) return group;
        var jobs = data.GetExcelSheet<ClassJob>(ClientLanguage.English);
        var names = new List<string>();
        foreach (var abbreviation in source.Split(new[] { ' ', ',', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var found = false;
            foreach (var job in jobs)
            {
                if (!string.Equals(job.Abbreviation.ExtractText(), abbreviation, StringComparison.Ordinal)) continue;
                var name = RussianGameText.Name(data, job, x => x.Name).Trim();
                if (name.Length == 0) return original;
                names.Add(name);
                found = true;
                break;
            }
            // Never partially translate an unknown list or infer a role from its length.
            if (!found) return original;
        }
        return names.Count == 0 ? original : string.Join(", ", names);
    }
}
