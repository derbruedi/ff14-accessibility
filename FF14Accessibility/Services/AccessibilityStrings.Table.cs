namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string TableHint(string key) => L($"Zeilenweise lesen: {key}.", $"Read by rows: {key}.", $"Читать по строкам: {key}.");
    public static string TableInstructions => L(
        "Lesemodus. Hoch/Runter: Zeile; Links/Rechts: Feld; Bild hoch/runter: zehn Zeilen; Tab: Abschnitt; Pos1/Ende: Anfang/Ende. Num5 oder Leertaste: wiederholen. Escape: zurück zur Spielsteuerung. Hier werden keine Aktionen ausgeführt.",
        "Reading mode. Up/Down: row; Left/Right: field; Page Up/Down: ten rows; Tab: section; Home/End: first/last. Numpad 5 or Space: repeat. Escape: return to game controls. This mode does not activate controls.",
        "Режим чтения. Вверх/вниз: строка; влево/вправо: поле; Page Up/Down: десять строк; Tab: раздел; Home/End: начало/конец. Num5 или пробел: повторить. Escape: вернуться к управлению игрой. Здесь кнопки только читаются, действия не выполняются.");
    public static string TableEmpty => L("Kein lesbarer Text im aktiven Fenster.", "No readable text in the active window.", "В активном окне нет доступного текста.");
    public static string TableClosed => L("Lesemodus beendet.", "Reading mode closed.", "Режим чтения закрыт.");
    public static string TableChanged => L("Fenster gewechselt. Lesemodus beendet.", "Window changed. Reading mode closed.", "Окно изменилось. Режим чтения закрыт.");
    public static string TableWindow => L("Fenster", "Window", "Окно");
    public static string TableRow(string section, int index, int total, string text) => L(
        $"{(section.Length > 0 ? section + ". " : "")}{text}. Zeile {index} von {total}.",
        $"{(section.Length > 0 ? section + ". " : "")}{text}. Row {index} of {total}.",
        $"{(section.Length > 0 ? section + ". " : "")}{text}. Строка {index} из {total}.");
    public static string TableCurrentMaximum(string current, string maximum) => L(
        $"{current} von {maximum}", $"{current} of {maximum}", $"{current} из {maximum}");
    public static string TableCell(int index, int total, string context, string text) => L(
        $"Feld {index} von {total}. {(context.Length > 0 ? context + ": " : "")}{text}",
        $"Field {index} of {total}. {(context.Length > 0 ? context + ": " : "")}{text}",
        $"Поле {index} из {total}. {(context.Length > 0 ? context + ": " : "")}{text}");
    public static string ActionReadHint(string key) => L($"Beschreibung wiederholen: {key}.", $"Repeat description: {key}.", $"Повторить описание умения: {key}.");
    public static string ActionDescriptionUnavailable => L("Keine bestätigte Beschreibung für diesen Eintrag.",
        "No confirmed description for this entry.", "Подтверждённое описание этого пункта недоступно.");
    public static string ActionChooseEntry => L("Zuerst eine Aktion auswählen.", "Select an action first.", "Сначала выберите умение.");
}
