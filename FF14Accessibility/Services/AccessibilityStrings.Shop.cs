namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string ShopQuantityHint(string key) => L(
        $"Menge eingeben: {key}.", $"Enter quantity: {key}.", $"Ввести количество: {key}.");

    public static string ShopQuantityValue(string name, int value) => L(
        $"{name}, Menge {value}.", $"{name}, quantity {value}.", $"{name}, количество {value}.");

    public static string ShopQuantityRange(int minimum, int maximum) => L(
        $"Zulässig: {minimum} bis {maximum}.", $"Allowed: {minimum} to {maximum}.",
        $"Допустимое количество: от {minimum} до {maximum}.");

    public static string ShopQuantityInstructions => L(
        "Zahl eingeben. Enter übernimmt die Menge, Escape bricht ab. Der Kauf wird danach im Spiel bestätigt.",
        "Type a number. Enter applies the quantity, Escape cancels. Confirm the purchase in the game afterwards.",
        "Введите число. Enter — задать количество, Escape — отменить. Затем подтвердите покупку в игре.");

    public static string ShopQuantityDraft(string value) => L(
        $"Menge: {(value.Length == 0 ? "leer" : value)}.",
        $"Quantity: {(value.Length == 0 ? "empty" : value)}.",
        $"Количество: {(value.Length == 0 ? "пусто" : value)}.");

    public static string ShopQuantityUnavailable => L(
        "Eine Ware in der Kaufliste eines Gil-Händlers auswählen. Falls nötig zuerst die Mengenauswahl im Spiel öffnen und erneut versuchen.",
        "Select an item in a gil vendor's buy list. If needed, open the game's quantity control first and try again.",
        "Выберите товар в списке покупки за гил. Если поле количества ещё скрыто, откройте его обычным способом и повторите команду.");

    public static string ShopQuantityCancelled => L(
        "Mengeneingabe abgebrochen. Nichts geändert.", "Quantity entry cancelled. Nothing changed.",
        "Ввод количества отменён. Ничего не изменено.");

    public static string ShopQuantityChanged => L(
        "Ware oder Fenster geändert. Mengeneingabe abgebrochen.",
        "The item or window changed. Quantity entry cancelled.",
        "Товар или окно изменились. Ввод количества отменён.");

    public static string ShopQuantityApplied(string name, int value) => L(
        $"{name}, Menge {value} eingestellt. Kauf jetzt im Spiel bestätigen, normalerweise mit Num 0.",
        $"{name}, quantity set to {value}. Now confirm the purchase in the game, normally with numpad 0.",
        $"{name}, установлено количество {value}. Теперь подтвердите покупку в игре, обычно клавишей Num 0.");

    public static string ShopQuantityNotApplied => L(
        "Das Spiel hat die Menge nicht bestätigt. Menge vor dem Kauf prüfen.",
        "The game did not confirm the quantity. Check it before purchasing.",
        "Игра не подтвердила количество. Проверьте его перед покупкой.");
}
