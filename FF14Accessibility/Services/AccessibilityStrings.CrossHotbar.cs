namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string CrossBarName(int set) => IsGerman ? $"Kreuzleiste {set}" : $"Crossbar {set}";
    public static string CrossBarState(bool shared) => IsGerman
        ? (shared ? "geteilt zwischen Jobs" : "für diesen Job")
        : (shared ? "shared across jobs" : "for this job");
    public static string KeyboardBarChoice => IsGerman ? "Tastaturleisten" : "Keyboard hotbars";
    public static string BarPickerOpened => IsGerman
        ? "Leiste auswählen. Nummernblock 8 und 2 wechseln die Leiste, 0 öffnet ihre Tasten, Komma schließt."
        : "Choose a bar. Numpad 8 and 2 change bars, 0 opens its buttons, decimal closes.";
    public static string CrossButtonsOpened => IsGerman
        ? "Nummernblock 8 und 2 lesen die Tasten, 4 und 6 wechseln die Leiste, 0 wählt eine Taste zum Belegen, Komma zurück."
        : "Numpad 8 and 2 read buttons, 4 and 6 change bars, 0 chooses a button to assign, decimal goes back.";
    public static string CrossReadUnavailable => IsGerman
        ? "Normale Kreuzleiste nicht verfügbar. Zum Auswählen /acc crossbar eingeben."
        : "Normal crossbar unavailable. Use /acc crossbar to choose a set.";
    public static string CrossAssignPvpBlocked => IsGerman
        ? "Dieses Belegungsmenü ist nur für PvE. In einem PvE-Gebiet erneut versuchen."
        : "This assignment menu is for PvE. Try again in a PvE area.";
    public static string CrossJobChanged => IsGerman
        ? "Job oder Anmeldung geändert. Belegungsmenü erneut öffnen."
        : "Job or login changed. Reopen the assignment menu.";

    public static string CrossSlotLabel(int set, int slot)
    {
        var code = CrossHotbarLayout.SlotCode(slot);
        var side = code[0] == 'L' ? "L2" : "R2";
        var direction = code[2] switch
        {
            'L' => IsGerman ? "links" : "left",
            'U' => IsGerman ? "oben" : "up",
            'R' => IsGerman ? "rechts" : "right",
            _ => IsGerman ? "unten" : "down",
        };
        var button = code[1] == 'D'
            ? (IsGerman ? $"Steuerkreuz {direction}" : $"D-pad {direction}")
            : code[2] switch
            {
                'L' => IsGerman ? "Quadrat, linke Aktionstaste" : "Square, left face button",
                'U' => IsGerman ? "Dreieck, obere Aktionstaste" : "Triangle, top face button",
                'R' => IsGerman ? "Kreis, rechte Aktionstaste" : "Circle, right face button",
                _ => IsGerman ? "Kreuz, untere Aktionstaste" : "Cross, bottom face button",
            };
        return $"{CrossBarName(set)}, {side} + {button}";
    }
}
