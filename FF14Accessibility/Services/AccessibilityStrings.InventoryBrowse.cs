namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string WearConditionLabel => L("Zustand", "Condition", "Прочность");
    public static string SpiritbondLabel => L("Bindung", "Spiritbond", "Связь с духом");
    public static string InventoryMenuTitle => L("Inventar", "Inventory", "Инвентарь");

    public static string InventoryMenuHelp => L(
        "0 öffnet die Kategorie. Dort öffnet 0 das Spielmenü des Gegenstands. 4 zurück. Entf schließt.",
        "0 opens the category. There, 0 opens the item's game menu. 4 goes back. Delete closes.",
        "0 открывает категорию. Там 0 открывает игровое меню предмета. 4 назад. Delete закрывает.");

    public static string InventoryItemMenuHelp => L(
        "0 öffnet das Spielmenü. 4 zurück. Entf schließt.",
        "0 opens the game menu. 4 goes back. Delete closes.",
        "0 открывает игровое меню. 4 назад. Delete закрывает.");

    public static string InventoryContextFailed => L(
        "Das Spielmenü dieses Gegenstands konnte nicht geöffnet werden.",
        "Could not open this item's game menu.",
        "Не удалось открыть игровое меню этого предмета.");

    public static string InventoryContextChanged => L(
        "Der Gegenstand in diesem Fach hat sich geändert.",
        "The item in this slot has changed.",
        "Предмет в этой ячейке изменился.");

    public static string InventoryContextNoWindow => L(
        "Das Inventarfenster ist noch nicht bereit. Öffne es einmal mit I.",
        "The inventory window is not ready yet. Open it once with I.",
        "Окно инвентаря ещё не готово. Открой его один раз клавишей I.");

    public static string InventoryCategoryRow(string name, int count) => L(
        $"{name}, {count} Gegenstände",
        $"{name}, {count} items",
        $"{name}, {count} предметов");

    public static string InventoryKeyItemsCategory => L(
        "Schlüsselgegenstände", "Key items", "Ключевые предметы");

    public static string InventoryUnknownCategory => L(
        "Unbekannte Kategorie", "Unknown category", "Неизвестная категория");
}
