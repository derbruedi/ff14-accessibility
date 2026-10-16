namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string DialogChooseButton => L("Wähle zuerst eine verfügbare Schaltfläche mit Num4/6.",
        "First select an available button with Num4/6.", "Сначала выберите доступную кнопку с помощью Num4 или Num6.");
    public static string ButtonNotResponding => L("Die Schaltfläche hat nicht reagiert. Das Fenster bleibt offen.",
        "The button did not respond. The window remains open.", "Кнопка не ответила. Окно остаётся открытым.");
    public static string DialogHelp => L("Num4/6 wählt die echte Schaltfläche, Num0 oder Enter bestätigt, Escape bricht ab.",
        "Num4/6 selects a button, Num0 or Enter confirms, Escape cancels.",
        "Num4 и Num6 выбирают кнопку. Num0 или Enter нажимает выбранную кнопку. Escape отменяет. Стрелки влево и вправо тоже выбирают кнопку.");
    public static string SystemMenuHelp => L("Num8/2 navigiert, Num0 bestätigt, Escape geht zurück. Abmelden und Beenden öffnen eine Bestätigung.",
        "Num8/2 navigates, Num0 confirms, Escape goes back. Log out and exit open a confirmation.",
        "Num8 и Num2 выбирают пункт, Num0 открывает его, Escape возвращает назад. Выход в главное меню и завершение игры открывают окно подтверждения; в нём Num4 и Num6 выбирают кнопку, Num0 или Enter нажимает её.");
    public static string FocusedButtonHelp => L("Num8/2 und Num4/6 wechseln den Fokus; Num0 oder Enter betätigt die Schaltfläche. Escape geht zurück.",
        "Num8/2 and Num4/6 move the focus; Num0 or Enter presses the button. Escape goes back.",
        "Num8 и Num2, Num4 и Num6 перемещают выбор. Num0 или Enter нажимает выбранную кнопку. Escape возвращает назад.");
    public static string DisabledButton(string label) => L($"{label}, nicht verfügbar", $"{label}, unavailable", $"{label}, недоступно");
    public static string InspectGearSlot => L("Ausrüstungsplatz", "Equipment slot", "Ячейка экипировки");
}
