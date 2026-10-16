namespace FF14Accessibility.Services;

/// <summary>Controls item/ability speech without changing plugin announcements.</summary>
internal static class GameTextTranslation
{
    internal static bool ShouldTranslate(string sheet) => Loc.IsRussian && (Loc.TranslateItemsAndActions || sheet is not
        ("Item" or "EventItem" or "Action" or "ActionTransient" or "CraftAction" or
         "Trait" or "TraitTransient" or "BuddyAction" or "GeneralAction" or "PetAction" or
         "AozAction" or "AozActionTransient" or "Status" or
         "DeepDungeonItem" or "DeepDungeonMagicStone" or "DeepDungeonEquipment"));

    /// <summary>Returns null for unrelated commands. Status/help never changes settings.</summary>
    internal static string? HandleCommand(string command, Configuration config, Action save)
    {
        var parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].ToLowerInvariant() is not ("translate" or "перевод")) return null;
        if (parts.Length == 1) return AccessibilityStrings.ItemActionTranslationState(config.TranslateItemsAndActions);
        if (parts.Length != 2) return AccessibilityStrings.ItemActionTranslationUsage;
        bool? enabled = parts[1].ToLowerInvariant() switch
        {
            "on" or "вкл" => true,
            "off" or "выкл" => false,
            _ => null,
        };
        if (enabled is null) return AccessibilityStrings.ItemActionTranslationUsage;
        config.TranslateItemsAndActions = enabled.Value;
        Loc.TranslateItemsAndActions = enabled.Value;
        save();
        return AccessibilityStrings.ItemActionTranslationState(enabled.Value);
    }
}
