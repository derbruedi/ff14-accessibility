using System.Globalization;

namespace FF14Accessibility.Services;

public static class AccessibilityStrings
{
    private static bool IsGerman =>
        string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "de",
            StringComparison.OrdinalIgnoreCase);

    public static string TitleScreen => IsGerman ? "Titelbildschirm" : "Title screen";
    public static string MainMenu => IsGerman ? "Hauptmenü" : "Main menu";
    public static string Back => IsGerman ? "Zurück" : "Back";
    public static string NoHelpAvailable => IsGerman ? "Keine Hilfe verfügbar" : "No help available";
    public static string HelpForTitle => IsGerman
        ? "Enter öffnet das Hauptmenü. Strg+F1 sagt diese Hilfe erneut an."
        : "Press Enter to open the main menu. Press Ctrl+F1 to hear this help again.";
    public static string HelpForTitleMenu => IsGerman
        ? "Pfeil hoch und runter zum Wechseln, Enter zum Bestätigen, Escape zurück, Strg+F1 für Hilfe."
        : "Use up and down arrow keys to move, Enter to confirm, Escape to go back, Ctrl+F1 for help.";

    public static string Confirmed(string item) =>
        IsGerman ? $"Auswahl bestätigt: {item}" : $"Confirmed: {item}";

    public static string MenuPosition(string item, int index, int count) =>
        IsGerman ? $"{item}, {index} von {count}" : $"{item}, {index} of {count}";

    // ── Keybind-Dump (/acc keys) ─────────────────────────────────────
    public static string KeybindDumpSaved(int boundCount, int conflictCount) =>
        IsGerman
            ? $"Tastenbelegung gespeichert: {boundCount} Aktionen mit Taste, {conflictCount} Konflikte mit Plugin-Tasten. Datei auf dem Desktop, Details im Log."
            : $"Keybinds saved: {boundCount} bound actions, {conflictCount} conflicts with plugin keys. File on desktop, details in log.";

    public static string KeybindDumpFailed =>
        IsGerman
            ? "Tastenbelegung konnte nicht gelesen werden. Details im Log."
            : "Could not read keybinds. See log for details.";

    // ── ConfigSystem ─────────────────────────────────────────────────
    public static string ConfigSystem =>
        IsGerman ? "Systemeinstellungen" : "System Configuration";

    public static string ConfigSystemSaved =>
        IsGerman ? "Einstellungen gespeichert" : "Settings saved";

    public static string ConfigSystemDiscarded =>
        IsGerman ? "Änderungen verworfen" : "Changes discarded";

    public static string HelpForConfigSystem => IsGerman
        ? "Pfeile hoch und runter wechseln Option. Links und rechts ändern Wert oder Tab. Enter speichert, Escape verwirft, Strg+F1 für Hilfe."
        : "Up and down arrows move between options. Left and right change value or tab. Enter saves, Escape discards, Ctrl+F1 for help.";

    public static string CheckboxOn  => IsGerman ? "an"  : "on";
    public static string CheckboxOff => IsGerman ? "aus" : "off";

    public static string OptionPosition(string label, string value, int index, int count) =>
        IsGerman
            ? $"{label}, {value}, {index} von {count}"
            : $"{label}, {value}, {index} of {count}";

    public static string TabPosition(string label, int index, int count) =>
        IsGerman
            ? $"{label}, Tab {index} von {count}"
            : $"{label}, tab {index} of {count}";

    // ── Fenster-Ansage (F2 / /acc win) ───────────────────────────────
    public static string ActiveWindow(string name, int visibleCount) =>
        IsGerman
            ? $"Aktives Fenster: {name}. {visibleCount} Fenster sichtbar, Liste im Log."
            : $"Active window: {name}. {visibleCount} windows visible, list written to log.";

    public static string NoWindowFocused(int visibleCount) =>
        IsGerman
            ? $"Kein Fenster fokussiert. {visibleCount} Fenster sichtbar, Liste im Log."
            : $"No window focused. {visibleCount} windows visible, list written to log.";

    public static string UiManagerUnavailable =>
        IsGerman ? "Fenster-Liste nicht verfügbar." : "Window list not available.";

    public static string DumpSaved(int addonCount, int nodeCount) =>
        IsGerman
            ? $"UI Dump auf Desktop gespeichert. {addonCount} Fenster, {nodeCount} Nodes."
            : $"UI dump saved to desktop. {addonCount} windows, {nodeCount} nodes.";

    public static string AddonNotOpen(string names) =>
        IsGerman ? $"Addon {names} nicht offen." : $"Addon {names} not open.";

    // ── Ok-Taste (Enter in Lobby/Charaktererstellung) ────────────────
    public static string OkPressed  => IsGerman ? "Ok" : "Ok";
    public static string NoOkButton => IsGerman ? "Kein Ok-Knopf gefunden." : "No Ok button found.";

    // ── Charaktererstellung: Volk & Geschlecht ───────────────────────
    public static string GenderMale   => IsGerman ? "männlich" : "male";
    public static string GenderFemale => IsGerman ? "weiblich" : "female";

    // ── SelectYesno ──────────────────────────────────────────────────
    public static string DialogButtons(string confirm, string cancel) =>
        IsGerman
            ? $"{confirm} oder {cancel}? Links und rechts wechseln, Enter wählt aus."
            : $"{confirm} or {cancel}? Left and right to switch, Enter to select.";

    // ── Datenzentrums-Auswahl (TitleDCWorldMap) ──────────────────────
    public static string DCSelected(string dc, IReadOnlyCollection<string> worlds) =>
        worlds.Count > 0
            ? (IsGerman
                ? $"{dc} ausgewählt. Welten: {string.Join(", ", worlds)}. Zum Bestätigen den Ok-Knopf drücken."
                : $"{dc} selected. Worlds: {string.Join(", ", worlds)}. Press the Ok button to confirm.")
            : (IsGerman ? $"{dc} ausgewählt." : $"{dc} selected.");
}