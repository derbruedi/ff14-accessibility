using Dalamud.Game;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

internal static class NativeItemIdChecks
{
    internal sealed record Report(int Rows, int Assertions, string RecordedItemName, string RecordedItemLabel);

    internal static Report Run(GameDataReader data, GameDescriptionService descriptions, IPluginLog log)
    {
        var savedMode = Loc.Mode;
        var savedTranslation = Loc.TranslateItemsAndActions;
        var savedClient = data.Language;
        var assertions = 0;
        var recordedName = string.Empty;
        var recordedLabel = string.Empty;
        try
        {
            data.Language = ClientLanguage.English;
            var rows = data.GetExcelSheet<Item>().Where(row => row.CanBeHq && !row.Name.IsEmpty)
                .Select(row => row.RowId).ToArray();
            Require(rows.Contains(1908u), "Item 1908 from the player's log is missing from the HQ table.");
            Loc.Mode = LanguageMode.Russian;
            foreach (var client in new[] { ClientLanguage.English, ClientLanguage.German,
                ClientLanguage.French, ClientLanguage.Japanese })
            {
                data.Language = client;
                var inventory = new InventoryService(null!, data, null!, new Configuration(), null!, log, descriptions);
                foreach (var translate in new[] { true, false })
                {
                    Loc.TranslateItemsAndActions = translate;
                    foreach (var baseId in rows)
                    {
                        var item = new ArmouryListItem(InventoryType.ArmoryMainHand, 9, baseId + 1_000_000,
                            1, true, "offline instance", "");
                        Require(item.BaseItemId == baseId, "Armoury description resolved another row.");
                        Require(ItemSlotService.GetBaseItemId(item.ItemId) == baseId,
                            "Inventory focus resolved another row.");
                        var label = inventory.ResolveItemLabel(item.BaseItemId);
                        Require(label == inventory.ResolveItemLabel(baseId), "HQ changed the item label.");
                        Require(!label.Contains(AccessibilityStrings.ItemFallback(item.ItemId), StringComparison.Ordinal)
                            && !label.Contains(AccessibilityStrings.ItemFallback(baseId), StringComparison.Ordinal),
                            "A known HQ item was read as a numeric fallback.");
                        if (baseId == 1908 && client == ClientLanguage.English && translate)
                        {
                            recordedName = inventory.ResolveItemName(item.BaseItemId);
                            recordedLabel = label + AccessibilityStrings.HighQuality;
                        }
                    }
                }
            }
            return new(rows.Length, assertions, recordedName, recordedLabel);
        }
        finally
        {
            Loc.Mode = savedMode;
            Loc.TranslateItemsAndActions = savedTranslation;
            data.Language = savedClient;
        }

        void Require(bool success, string message)
        {
            assertions++;
            if (!success) throw new Exception(message);
        }
    }
}
