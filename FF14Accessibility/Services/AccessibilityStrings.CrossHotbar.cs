namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string CrossBarName(int set) =>
        L($"Kreuzleiste {set}", $"Crossbar {set}", $"Крестовая панель {set}");
    public static string CrossBarState(bool shared) =>
        L(shared ? "geteilt zwischen Jobs" : "fuer diesen Job",
          shared ? "shared across jobs" : "for this job",
          shared ? "общая для всех профессий" : "для этой профессии");
    public static string KeyboardBarChoice =>
        L("Tastaturleisten", "Keyboard hotbars", "Клавиатурные панели");
    public static string BarPickerOpened =>
        L("Leiste auswaehlen. Nummernblock 8 und 2 wechseln die Leiste, 0 oeffnet ihre Tasten, Komma schliesst.",
          "Choose a bar. Numpad 8 and 2 change bars, 0 opens its buttons, decimal closes.",
          "Выбери панель. Цифры 8 и 2 на нампаде переключают панель, 0 открывает её кнопки, запятая закрывает.");
    public static string CrossButtonsOpened =>
        L("Nummernblock 8 und 2 lesen die Tasten, 4 und 6 wechseln die Leiste, 0 waehlt eine Taste zum Belegen, Komma zurueck.",
          "Numpad 8 and 2 read buttons, 4 and 6 change bars, 0 chooses a button to assign, decimal goes back.",
          "Цифры 8 и 2 на нампаде читают кнопки, 4 и 6 переключают панель, 0 выбирает кнопку для назначения, запятая возвращает назад.");
    public static string CrossReadUnavailable =>
        L("Normale Kreuzleiste nicht verfuegbar. Zum Auswaehlen /acc crossbar eingeben.",
          "Normal crossbar unavailable. Use /acc crossbar to choose a set.",
          "Обычная крестовая панель недоступна. Чтобы выбрать, введи /acc crossbar.");
    public static string CrossAssignPvpBlocked =>
        L("Dieses Belegungsmenue ist nur fuer PvE. In einem PvE-Gebiet erneut versuchen.",
          "This assignment menu is for PvE. Try again in a PvE area.",
          "Это меню назначения только для PvE. Попробуй снова в зоне PvE.");
    public static string CrossJobChanged =>
        L("Job oder Anmeldung geaendert. Belegungsmenue erneut oeffnen.",
          "Job or login changed. Reopen the assignment menu.",
          "Профессия или вход сменились. Открой меню назначения заново.");

    public static string CrossSlotLabel(int set, int slot)
    {
        var code = CrossHotbarLayout.SlotCode(slot);
        var side = code[0] == 'L' ? "L2" : "R2";
        var direction = code[2] switch
        {
            'L' => L("links", "left", "влево"),
            'U' => L("oben", "up", "вверх"),
            'R' => L("rechts", "right", "вправо"),
            _ => L("unten", "down", "вниз"),
        };
        var button = code[1] == 'D'
            ? L($"Steuerkreuz {direction}", $"D-pad {direction}", $"крестовина {direction}")
            : code[2] switch
            {
                'L' => L("Quadrat, linke Aktionstaste", "Square, left face button", "квадрат, левая кнопка действия"),
                'U' => L("Dreieck, obere Aktionstaste", "Triangle, top face button", "треугольник, верхняя кнопка действия"),
                'R' => L("Kreis, rechte Aktionstaste", "Circle, right face button", "круг, правая кнопка действия"),
                _ => L("Kreuz, untere Aktionstaste", "Cross, bottom face button", "крест, нижняя кнопка действия"),
            };
        return $"{CrossBarName(set)}, {side} + {button}";
    }
}
