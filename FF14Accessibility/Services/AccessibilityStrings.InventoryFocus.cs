namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string FocusedItemUnavailable => L(
        "Gegenstand wird aktualisiert. Bitte noch einmal lesen.",
        "Item is updating. Please read again.",
        "Предмет обновляется. Повтори чтение.");
    public static string ArmouryMainHand => L("Haupthand", "Main hand", "Главная рука");
    public static string ArmourySlotFilter(string slot) => L(
        $"{slot}, Platzfilter", $"{slot}, slot filter", $"{slot}, фильтр слота");
    public static string SelectedObjectUnavailable(string name) => L(
        $"{name} ist derzeit nicht benutzbar. Wähle ein anderes Objekt.",
        $"{name} is currently unavailable for interaction. Choose another object.",
        $"{name} сейчас недоступен для взаимодействия. Выбери другой объект.");
    public static string SelectedObjectMissing(string name) => L(
        $"{name} ist nicht mehr in der Nähe. Wähle das Objekt erneut.",
        $"{name} is no longer nearby. Select the object again.",
        $"{name} больше нет рядом. Выбери объект заново.");
    public static string WorldObjectInteractionUnavailable => L(
        "Die Interaktion konnte nicht angefordert werden. Bitte versuche es erneut.",
        "Could not request interaction. Please try again.",
        "Не удалось запросить взаимодействие. Попробуй ещё раз.");
}
