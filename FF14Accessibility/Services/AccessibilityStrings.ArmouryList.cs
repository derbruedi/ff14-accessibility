namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string ArmouryListBaseStats => L(
        "Grundwerte ohne HQ- und Materia-Boni",
        "Base values without high-quality or materia bonuses",
        "Базовые параметры без бонусов высокого качества и материи");
    public static string ArmouryListTitle(int count) => L(
        $"Arsenaltruhe, alle Gegenstände: {count}", $"Armoury Chest, all items: {count}",
        $"Арсенал, все предметы: {count}");
    public static string ArmouryListHelp => L(
        "2 und 8: eine Reihe nach unten oder oben. 4 und 6: vorheriger oder nächster Gegenstand. 7 und 9: vorherige oder nächste nicht leere Kategorie, auch ohne NumLock über Pos1 und Bild auf. 5: Beschreibung. 0: Spielmenü des Gegenstands. Escape oder Entf: schließen. Ende: letzter Gegenstand.",
        "2 and 8: move a row down or up. 4 and 6: previous or next item. 7 and 9: previous or next nonempty category, also with NumLock off via Home and Page Up. 5: description. 0: game item menu. Escape or Delete: close. End: last item.",
        "2 и 8: на строку вниз или вверх. 4 и 6: предыдущий или следующий предмет. 7 и 9: предыдущая или следующая непустая категория, также при выключенном NumLock через Home и Page Up. 5: описание. 0: игровое меню предмета. Escape или Delete: закрыть. End: последний предмет.");
    public static string ArmouryListCategory(string category, string selection) => L(
        $"Kategorie: {category}. {selection}", $"Category: {category}. {selection}",
        $"Категория: {category}. {selection}");
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
