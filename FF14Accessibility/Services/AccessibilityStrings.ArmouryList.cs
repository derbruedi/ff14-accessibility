namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string ArmouryListTitle(int count) => L(
        $"Arsenaltruhe, alle Gegenstände: {count}", $"Armoury Chest, all items: {count}",
        $"Арсенал, все предметы: {count}");
    public static string OptArmouryClassList => L(
        "Arsenal als Klassenliste", "Armoury Chest as class list", "Арсенал списком по классам");
    public static string ArmouryClassTitle(int count) => L(
        $"Arsenaltruhe, Klassen: {count}", $"Armoury Chest, classes: {count}",
        $"Арсенал, классы: {count}");
    public static string ArmouryClassHelp => L(
        "0 öffnet die Klasse. Plus zurück zu den Klassen. Entf schließt.",
        "0 opens the class. Plus returns to the classes. Delete closes.",
        "0 открывает класс. Плюс возвращает к классам. Delete закрывает.");
    public static string ArmouryClassRow(string name, int count) => L(
        $"{name}, {count} Gegenstände", $"{name}, {count} items", $"{name}, {count} предметов");
    public static string ArmouryUnknownClass => L(
        "Unbekannte Klasse", "Unknown class", "Неизвестный класс");
    public static string ArmouryListHelp => L(
        "Zuerst die Klassen. 0 öffnet die Klasse. Plus zurück zu den Klassen. In der Klasse: 2 und 8 eine Reihe, 4 und 6 der vorige oder nächste Gegenstand, 7 und 9 die vorige oder nächste Ausrüstungsart, 5 die Beschreibung, 0 das Spielmenü, Ende der letzte Gegenstand. Entf schließt.",
        "Classes come first. 0 opens the class. Plus returns to the classes. Inside a class: 2 and 8 move a row, 4 and 6 the previous or next item, 7 and 9 the previous or next equipment slot, 5 the description, 0 the game menu, End the last item. Delete closes.",
        "Сначала классы. 0 открывает класс. Плюс возвращает к классам. Внутри класса: 2 и 8 на строку, 4 и 6 предыдущий или следующий предмет, 7 и 9 предыдущий или следующий слот, 5 описание, 0 игровое меню, End последний предмет. Delete закрывает.");
    public static string ArmouryListCategory(string category, int count, string selection) => L(
        $"Kategorie: {category}, {count}. {selection}",
        $"Category: {category}, {count}. {selection}",
        $"Категория: {category}, {count}. {selection}");
    public static string ArmouryListUpdating => L(
        "Die Arsenaltruhe wird geladen. Bitte warten.", "The Armoury Chest is loading. Please wait.",
        "Арсенал загружается. Подожди.");
    public static string ArmouryListItemChanged => L(
        "Der Gegenstand hat sich geändert. Bitte erneut wählen.",
        "The item has changed. Please select it again.",
        "Предмет изменился. Выбери его заново.");
    public static string ArmouryListMenuFailed => L(
        "Das Spielmenü dieses Gegenstands konnte nicht geöffnet werden.",
        "Could not open this item's game menu.", "Не удалось открыть игровое меню этого предмета.");
}
