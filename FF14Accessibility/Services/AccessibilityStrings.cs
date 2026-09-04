using System;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text;

namespace FF14Accessibility.Services;

// [Chat-Puffer] `partial`, damit die Zeichenketten der Chat-Puffer und des
// Einstellungsmenüs in AccessibilityStrings.Chat.cs stehen können: diese Datei ist
// groß und ändert sich in fast jeder Version. Das ist die einzige Änderung an ihr.
public static partial class AccessibilityStrings
{
    // Language is driven by the config-backed Loc provider ("/acc lang"),
    // NOT the OS culture directly. Auto still falls back to the OS culture.
    private static bool IsGerman => Loc.IsGerman;

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

    /// <summary>GrandCompanyExchange (seal quartermaster) row: item name, seal
    /// price, amount already owned. The generic reader announced the bare
    /// "0, 1.060, name" without labels; this makes the columns explicit.</summary>
    public static string GrandCompanyRow(string name, string price, string owned) =>
        IsGerman ? $"{name}, {price} Staatstaler, Besitz {owned}"
                 : $"{name}, {price} seals, {owned} owned";

    /// <summary>Announces the active category tab of a shop/window, e.g. the
    /// GrandCompanyExchange tabs (Waffen/Rüstung/...).</summary>
    public static string CategoryLabel(string name) =>
        IsGerman ? $"Kategorie {name}." : $"Category {name}.";

    /// <summary>
    /// Rank tier button in the left column of the seal shop. Those buttons carry
    /// no text at all - the game draws the rank insignia of the ranks they cover
    /// (GCScripShopCategory.Tier 1-3, GrandCompanyRank.Tier; the node counts 4/4/3
    /// match the ranks per tier exactly, dump 2026-08-19). The names come from the
    /// player's own Grand Company rank sheet, so the announcement says what the
    /// insignia show.
    /// </summary>
    public static string GcRankTier(int index, int count, string firstRank, string lastRank)
    {
        var range = firstRank.Length > 0 && lastRank.Length > 0
            ? (IsGerman ? $", {firstRank} bis {lastRank}" : $", {firstRank} to {lastRank}")
            : string.Empty;
        return IsGerman ? $"Rangstufe {index} von {count}{range}"
                        : $"Rank tier {index} of {count}{range}";
    }

    /// <summary>Appended to a tab/button announcement when it is the active one.</summary>
    public static string SelectedSuffix => IsGerman ? ", ausgewählt" : ", selected";

    // ── Reittier-Verzeichnis (MountNoteBook) ─────────────────────────
    /// <summary>Active view tab of the mount guide (Favorites/Normal/Search).</summary>
    public static string MountViewFavorites => IsGerman ? "Favoriten." : "Favorites.";
    public static string MountViewNormal    => IsGerman ? "Alle Reittiere." : "All mounts.";
    public static string MountViewSearch    => IsGerman ? "Suche." : "Search.";

    /// <summary>Page tab of the mount guide (1-based).</summary>
    public static string MountPage(int page) =>
        IsGerman ? $"Seite {page}." : $"Page {page}.";

    /// <summary>Spoken when the focus lands on the mount guide's search box.</summary>
    public static string MountSearchField => IsGerman ? "Reittier suchen, Eingabefeld." : "Mount search, text field.";

    /// <summary>Spoken when the focus lands on the minion guide's search box.</summary>
    public static string MinionSearchField => IsGerman ? "Begleiter suchen, Eingabefeld." : "Minion search, text field.";

    // ── Zauberbuch der Blaumagie (AOZNotebook) ───────────────────────
    /// <summary>
    /// A spell tile in the blue magic spellbook grid: name plus its number in
    /// the book. The tile itself carries only the number ("Nr. 14") - without
    /// the name, browsing the grid told the player nothing (log 2026-09-02).
    /// The tick mark says the spell sits in the active set being edited.
    /// </summary>
    public static string AozSpellTile(string name, byte number, bool selected)
    {
        var head = IsGerman ? $"{name}, Nr. {number}" : $"{name}, no. {number}";
        if (!selected) return head + ".";
        return IsGerman ? $"{head}, ausgewählt." : $"{head}, selected.";
    }

    /// <summary>Spell tile whose action could not be resolved - position only.</summary>
    public static string AozUnknownSpell(int position) =>
        IsGerman ? $"Zauberfeld {position}." : $"Spell tile {position}.";

    /// <summary>One of the 24 active-command slots, filled.</summary>
    public static string AozActiveSlot(int slot, string name, byte number) =>
        IsGerman ? $"Platz {slot}, {name}, Nr. {number}."
                 : $"Slot {slot}, {name}, no. {number}.";

    /// <summary>One of the 24 active-command slots, empty.</summary>
    public static string AozActiveSlotEmpty(int slot) =>
        IsGerman ? $"Platz {slot}, leer." : $"Slot {slot}, empty.";

    /// <summary>The focused spell is already learned.</summary>
    public static string AozLearned => IsGerman ? "Erlernt." : "Learned.";

    /// <summary>The focused spell is not learned yet.</summary>
    public static string AozNotLearned => IsGerman ? "Noch nicht erlernt." : "Not learned yet.";

    /// <summary>
    /// Overview spoken when the spellbook opens: which tab, how many spells
    /// learned, how many command slots filled. Both counters are read from the
    /// window's own fields ("1/124", "1/24"), never recomputed.
    /// </summary>
    public static string AozOverview(int tab, int tabCount, string learned, string active)
    {
        var parts = new List<string>();
        parts.Add(IsGerman ? $"Zauberbuch der Blaumagie, Reiter {tab} von {tabCount}."
                           : $"Blue magic spellbook, tab {tab} of {tabCount}.");
        if (!string.IsNullOrWhiteSpace(learned))
            parts.Add(IsGerman ? $"Erlernt {learned.Trim()}." : $"Learned {learned.Trim()}.");
        if (!string.IsNullOrWhiteSpace(active))
            parts.Add(IsGerman ? $"Aktive Kommandos {active.Trim()}."
                               : $"Active commands {active.Trim()}.");
        return string.Join(" ", parts);
    }

    // ── Umschalt-Zustaende (Checkbox / Radiobutton) ──────────────────
    /// <summary>Checkbox is ticked / unticked.</summary>
    public static string StateOn  => IsGerman ? "an" : "on";
    public static string StateOff => IsGerman ? "aus" : "off";
    /// <summary>Radio button is the selected option.</summary>
    public static string RadioSelected => IsGerman ? "ausgewählt" : "selected";
    /// <summary>
    /// Radio button is one of the options, but NOT the active one.
    ///
    /// <para>
    /// Existiert, weil das Schweigen hier aktiv in die Irre fuehrt: in der
    /// Spielersuche klangen "Vorname" und "Nachname" beim Durchtabben identisch,
    /// und der User hielt das Anfokussieren fuer das Umschalten (Meldung
    /// 2026-08-24, im Log ohne jedes Auswahl-Ereignis auf dem zweiten Knopf).
    /// Eine Auswahl, die man nicht hoert, ist keine.
    /// </para>
    /// </summary>
    public static string RadioNotSelected => IsGerman ? "nicht ausgewählt" : "not selected";
    /// <summary>Control-type word for a checkbox, so the user knows it is a
    /// toggle they can flip - not just an informational label.</summary>
    public static string SwitchControl => IsGerman ? "Schalter" : "switch";

    /// <summary>
    /// A switch that has NO name of its own - neither text nor tooltip - named by
    /// the heading of its row plus where it sits in that row.
    ///
    /// <para>
    /// WARUM DIE POSITION UND NICHT DER NAME: Die vier Sprach-Ankreuzfelder der
    /// Spielersuche tragen nur ein Buchstabenbild. Aus welchem Bild welche Sprache
    /// wird, sagt keine nachschlagbare Quelle - die Reihenfolge waere geraten, und
    /// eine geratene Sprache ist schlimmer als eine ehrliche Nummer, weil der
    /// Spieler danach den falschen Schalter umlegt. "Sprache, 3 von 4" ist
    /// gemessen: die Ueberschrift steht links in derselben Zeile, die Position
    /// ergibt sich aus den Bildschirmkoordinaten.
    /// </para>
    /// </summary>
    public static string GroupMemberPosition(string group, int index, int count) =>
        IsGerman ? $"{group}, {index} von {count}" : $"{group}, {index} of {count}";
    /// <summary>Control is greyed out / not currently changeable (NodeFlags.Enabled
    /// cleared) - e.g. a sub-toggle while its master switch is off.</summary>
    public static string StateDisabled => IsGerman ? "ausgegraut" : "greyed out";

    // ── Einstellungen der Inhaltssuche (ContentsFinderSetting) ───────
    /// <summary>The four language boxes of the duty-finder settings. Named by
    /// their position, which is the order the game's own configuration lists
    /// them in (ContentsFinderUseLangTypeJA / EN / DE / FR).</summary>
    public static string DutyLanguageJapanese => IsGerman ? "Japanisch" : "Japanese";
    public static string DutyLanguageEnglish  => IsGerman ? "Englisch"  : "English";
    public static string DutyLanguageGerman   => IsGerman ? "Deutsch"   : "German";
    public static string DutyLanguageFrench   => IsGerman ? "Französisch" : "French";

    /// <summary>Heading of the language row, so a box is not heard as a loose
    /// switch ("Sprache Deutsch, Schalter, an").</summary>
    public static string DutyLanguageGroup => IsGerman ? "Sprache" : "Language";

    /// <summary>The window's own help text for the option under the focus,
    /// spoken on its own after a short dwell - prefixed so the user knows what
    /// is being read.</summary>
    public static string SettingHelp(string help) =>
        IsGerman ? $"Erklärung: {help}" : $"Explanation: {help}";

    // ── Sprachumschaltung (/acc lang) ────────────────────────────────
    public static string LanguageGerman  => IsGerman ? "Deutsch" : "German";
    public static string LanguageEnglish => IsGerman ? "Englisch" : "English";

    public static string LanguageSet(string language) =>
        IsGerman ? $"Sprache auf {language} umgestellt." : $"Language set to {language}.";

    public static string LanguageAuto(string language) =>
        IsGerman
            ? $"Sprache folgt jetzt Windows: {language}."
            : $"Language now follows Windows: {language}.";

    public static string LanguageUsage =>
        IsGerman
            ? "Sprache wählen mit: /acc lang de, /acc lang en oder /acc lang auto."
            : "Choose a language with: /acc lang de, /acc lang en or /acc lang auto.";

    public static string UnknownCommand =>
        IsGerman ? "Unbekannter Befehl. Tippe /acc help für Hilfe." : "Unknown command. Type /acc help for help.";

    // ── Keybind-Dump (/acc keys) ─────────────────────────────────────
    /// <summary>
    /// Short conflict notice for the automatic dump at login. The full sentence
    /// below is for the explicit "/acc keys" call - at login it arrived in the
    /// middle of the HUD build-up and was cut off anyway (user 2026-08-06).
    /// Only the conflict count is actionable there: a plugin key is dead.
    /// </summary>
    public static string KeybindConflictsShort(int conflictCount) =>
        IsGerman
            ? (conflictCount == 1 ? "1 Tastenkonflikt." : $"{conflictCount} Tastenkonflikte.")
            : (conflictCount == 1 ? "1 key conflict." : $"{conflictCount} key conflicts.");

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

    // ── Triple Triad (Kartenspiel) ───────────────────────────────────
    // Fields read directly from AddonTripleTriad (Board/BlueDeck/RedDeck,
    // ilspycmd-verified). Numbers are pre-formatted by the service (1-9, 10 -> "A")
    // so the digit/A convention stays language-independent.
    public static string CardGameTitle => IsGerman ? "Kartenspiel" : "Card game";

    /// <summary>The four edge numbers of a card, in a fixed clockwise-from-top order.</summary>
    public static string CardSides(string up, string right, string down, string left) =>
        IsGerman
            ? $"oben {up}, rechts {right}, unten {down}, links {left}"
            : $"top {up}, right {right}, bottom {down}, left {left}";

    /// <summary>Owner of a card that sits on the board or in a hand.</summary>
    public static string CardOwnerYours => IsGerman ? "deine" : "yours";
    public static string CardOwnerEnemy => IsGerman ? "gegnerische" : "enemy";

    /// <summary>One board cell (1-based), either empty or holding a card.</summary>
    public static string BoardCellEmpty(int cell) =>
        IsGerman ? $"Feld {cell}: leer" : $"Cell {cell}: empty";

    public static string BoardCellCard(int cell, string owner, string sides) =>
        IsGerman ? $"Feld {cell}: {owner}, {sides}" : $"Cell {cell}: {owner}, {sides}";

    /// <summary>One hand card (1-based).</summary>
    public static string HandCard(int index, string sides) =>
        IsGerman ? $"Karte {index}: {sides}" : $"Card {index}: {sides}";

    /// <summary>Focus announcement for a single card (board cell or hand card).</summary>
    public static string FocusBoardCell(int cell, string content) =>
        IsGerman ? $"Feld {cell}, {content}" : $"Cell {cell}, {content}";

    public static string FocusHandCard(int index, int count, string sides) =>
        IsGerman ? $"Handkarte {index} von {count}, {sides}" : $"Hand card {index} of {count}, {sides}";

    public static string CardGameNotOpen =>
        IsGerman ? "Kartenspiel ist nicht offen." : "Card game is not open.";

    public static string BoardIntro(int yours, int enemy) =>
        IsGerman
            ? $"Brett. Deine Karten {yours}, gegnerische {enemy}."
            : $"Board. Your cards {yours}, enemy {enemy}.";

    public static string HandIntro(int count) =>
        IsGerman ? $"Deine Hand, {count} Karten." : $"Your hand, {count} cards.";

    public static string HandEmpty =>
        IsGerman ? "Keine Handkarten mehr." : "No hand cards left.";

    // HYPOTHESE (in-game zu verifizieren): TurnState NormalMove/MaskedMove = du bist
    // am Zug, Waiting = Gegner/warten. Der Rohwert wird zusaetzlich geloggt.
    public static string YourTurn => IsGerman ? "Du bist am Zug." : "Your turn.";
    public static string WaitingTurn => IsGerman ? "Warten." : "Waiting.";

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
    /// <summary>Fallback button labels, used only when the dialog's own button
    /// nodes carry no text - normally the labels are READ from the game.</summary>
    public static string YesWord => IsGerman ? "Ja" : "Yes";
    public static string NoWord  => IsGerman ? "Nein" : "No";
    public static string DialogButtons(string confirm, string cancel) =>
        IsGerman
            ? $"{confirm} oder {cancel}? Links und rechts wechseln, Enter wählt aus."
            : $"{confirm} or {cancel}? Left and right to switch, Enter to select.";

    // ── Navigation: Himmelsrichtungen, relative Richtung, Distanz ─────
    // Sprachabhängige Kompass-Wörter (0 = Norden .. 7 = Nordwesten). Property,
    // KEIN static readonly Array: "/acc lang" schaltet zur Laufzeit um, ein
    // eingefrorenes Array würde die alte Sprache behalten.
    private static readonly string[] CompassDe =
        { "Norden", "Nordosten", "Osten", "Südosten", "Süden", "Südwesten", "Westen", "Nordwesten" };
    private static readonly string[] CompassEn =
        { "North", "Northeast", "East", "Southeast", "South", "Southwest", "West", "Northwest" };

    public static string[] CompassWords => IsGerman ? CompassDe : CompassEn;

    // Adjective/adverb compass forms for "&lt;distance&gt; meters &lt;dir&gt;"
    // spot lines (0 = North .. 7 = Northwest).
    private static readonly string[] CompassAdjDe =
        { "nördlich", "nordöstlich", "östlich", "südöstlich", "südlich", "südwestlich", "westlich", "nordwestlich" };
    private static readonly string[] CompassAdjEn =
        { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
    public static string[] CompassAdjectives => IsGerman ? CompassAdjDe : CompassAdjEn;

    /// <summary>A spot list line: name, level, distance and compass bearing
    /// (shared by the fishing- and gathering-spot read-outs).</summary>
    public static string SpotListLine(string name, int level, float distance, string compass) =>
        IsGerman
            ? $"{name}, Stufe {level}, {distance:F0} Meter {compass}"
            : $"{name}, level {level}, {distance:F0} meters {compass}";

    /// <summary>
    /// Relative-to-heading direction word for a signed angle in degrees
    /// (negative = left, 0 = ahead).
    ///
    /// <para>
    /// NICHT MEHR DIE GESPROCHENE RICHTUNG. Seit 2026-08-23 sagt der Mod
    /// Himmelsrichtungen (<c>RouteService.CompassAdjective</c>) - die haengen
    /// nicht an der Blickrichtung und koennen darum nicht auf die falsche Seite
    /// zeigen, woran diese hier jahrelang krankte. Was hier noch haengt, ist die
    /// Debug-Sonde <c>[NavDirProbe]</c>, die damit die Seite des PEIL-TONS misst;
    /// der bleibt relativ. Bleibt ausserdem als Rueckweg, falls sich der Kompass
    /// im Spiel als der schlechtere Weg erweist.
    /// </para>
    /// </summary>
    public static string RelativeDirection(double relativeAngle) => relativeAngle switch
    {
        < -135 => IsGerman ? "hinter links"  : "behind to the left",
        < -45  => IsGerman ? "links"         : "left",
        < -15  => IsGerman ? "leicht links"  : "slightly left",
        <= 15  => IsGerman ? "geradeaus"     : "straight ahead",
        <= 45  => IsGerman ? "leicht rechts" : "slightly right",
        <= 135 => IsGerman ? "rechts"        : "right",
        _      => IsGerman ? "hinter rechts" : "behind to the right",
    };

    /// <summary>Spoken distance: very close as a phrase, otherwise metres, then
    /// kilometres. Mirrors the mod's metre convention (not in-game yalms).</summary>
    public static string FormatDistance(float distance) =>
        distance < 2f    ? (IsGerman ? "direkt neben dir" : "right next to you") :
        distance < 100f  ? (IsGerman ? $"{distance:F0} Meter"     : $"{distance:F0} meters") :
                           (IsGerman ? $"{distance / 1000:F1} Kilometer" : $"{distance / 1000:F1} kilometers");

    // ── Objekt-Browser: Kategorie-Labels & -Ansagen (NavigationService) ─
    /// <summary>The spoken name of an object-browser category in the active
    /// language. Identity is the NavCategory key; this is display only.</summary>
    internal static string CategoryLabel(NavCategory cat) => cat switch
    {
        NavCategory.All              => IsGerman ? "Alles"             : "Everything",
        NavCategory.Npcs             => IsGerman ? "NPCs"              : "NPCs",
        NavCategory.Merchants        => IsGerman ? "Händler"           : "Merchants",
        NavCategory.Enemies          => IsGerman ? "Gegner"            : "Enemies",
        NavCategory.Allies           => IsGerman ? "Verbündete"        : "Allies",
        NavCategory.Players          => IsGerman ? "Spieler"           : "Players",
        NavCategory.Objects          => IsGerman ? "Objekte"           : "Objects",
        // NICHT "Dungeons": die Kategorie haelt auch Prüfungs-, Raid- und
        // PvP-Türen, und die Ansage nennt Inhalt und Art ohnehin. Das deutsche Wort
        // ist das des SPIELS, keine nach Gefühl gewählte Übersetzung - der Client
        // nennt die Inhaltssuche "Inhaltssuche", den Zufallsinhalt "Zufallsinhalt",
        // und "There are no duties available" heisst dort "Keine Inhalte vorhanden"
        // (Addon 2500/2509, in beiden Sprachen gedumpt). Ein Duty ist also ein
        // "Inhalt", und der Spieler hört das Wort im Spiel bereits.
        NavCategory.Duties           => IsGerman ? "Inhalte"           : "Duties",
        NavCategory.QuestNpcs        => IsGerman ? "Quest-NPCs"        : "Quest NPCs",
        NavCategory.QuestObjects     => IsGerman ? "Quest-Objekte"     : "Quest objects",
        NavCategory.QuestEnemies     => IsGerman ? "Quest-Gegner"      : "Quest enemies",
        NavCategory.GatheringNodes   => IsGerman ? "Sammelpunkte"      : "Gathering nodes",
        NavCategory.Fates            => "FATEs",
        NavCategory.HuntingTargets   => IsGerman ? "Jagdziele"          : "Hunting targets",
        // "Jagdziele der Gesellschaft" statt "Jagdtagebuch der Staatlichen
        // Gesellschaft": die Beschriftung wird bei jedem Blättern gesprochen,
        // und sie steht direkt hinter "Jagdziele" - das Wort, das die beiden
        // unterscheidet, gehört nach vorn und der Rest muss kurz bleiben.
        NavCategory.GrandCompanyHunt => IsGerman ? "Jagdziele der Gesellschaft" : "Grand company targets",
        NavCategory.BlueMagic        => IsGerman ? "Blaumagie"          : "Blue magic",
        NavCategory.FishingSpots     => IsGerman ? "Angelplätze"       : "Fishing spots",
        // Bewusst NICHT "Dungeons", obwohl der Wunsch so formuliert war: die Liste
        // haelt auch Prüfungen und Raids. Und bewusst nicht noch einmal "Inhalte" -
        // die Kategorie darüber heisst so und zeigt nur die Türen in der Nähe; das
        // Wort "alle" ist genau der Unterschied zwischen beiden.
        NavCategory.WorldDuties      => IsGerman ? "Alle Inhalte"      : "All duties",
        NavCategory.Aetherytes       => IsGerman ? "Ätheryten"         : "Aetherytes",
        NavCategory.QuestGoals       => IsGerman ? "Quest-Ziele"       : "Quest goals",
        NavCategory.AcceptableQuests => IsGerman ? "Annehmbare Quests" : "Available quests",
        NavCategory.Levequests       => IsGerman ? "Freibriefe"        : "Levequests",
        NavCategory.Waypoints        => IsGerman ? "Wegpunkte"         : "Waypoints",
        // Das Wort des Users (2026-08-29). Es steht bewusst neben "Inhalte" und
        // "Alle Inhalte", ohne sich mit ihnen zu schneiden: jene beiden führen zu
        // einer TÜR, diese führt DURCH das, was hinter ihr liegt.
        NavCategory.DungeonRoute     => IsGerman ? "Dungeon"           : "Dungeon",
        _                            => cat.ToString(),
    };

    /// <summary>What a merchant deals in, spoken in place of the generic "NPC"
    /// while browsing the merchant category.</summary>
    internal static string ShopKindWord(ShopKind kind) => kind switch
    {
        ShopKind.GilShop  => IsGerman ? "Laden"   : "shop",
        ShopKind.Exchange => IsGerman ? "Tausch"  : "exchange",
        _                 => IsGerman ? "Händler" : "merchant",
    };

    /// <summary>
    /// Was hinter einer Tür liegt, gesprochen an der Stelle des generischen
    /// "Objekt", während der Spieler die Kategorie Inhalte durchblättert. User:
    /// *"right now they are just called entrance instead of having more useful
    /// names like the name of the dungeon."* Deshalb trägt das hier den NAMEN und
    /// die Stufe, nicht nur ein Kategoriewort.
    ///
    /// Der Objektname wird unmittelbar davor gesprochen, das Ergebnis liest sich
    /// also als *"Eingang, Dungeon: Das Tam-Tara-Grab, Stufe 16, 12 Meter, Norden."*
    ///
    /// NAME UND STUFE GEHÖREN DEM SPIEL - aus ContentFinderCondition, in der
    /// Client-Sprache; der Mod übersetzt und erfindet hier nichts. Nur das
    /// Kategoriewort im Singular ist mod-eigen, und es ist der Singular dessen, was
    /// der deutsche Client selbst für diese Inhaltsart schreibt (ContentType, in
    /// beiden Sprachen gedumpt: 2 'Dungeons', 4 'Prüfungen', 5 'Raids', 6 'PvP').
    /// Jede andere Art behält das Wort des Spiels wörtlich, statt verworfen oder
    /// geraten zu werden.
    /// </summary>
    internal static string DutyEntrance(string dutyName, uint contentType, ushort level, string gameTypeName)
    {
        var category = contentType switch
        {
            2 => IsGerman ? "Dungeon" : "dungeon",
            4 => IsGerman ? "Prüfung" : "trial",
            5 => IsGerman ? "Raid"    : "raid",
            6 => "PvP",
            _ => gameTypeName,
        };

        // Ein Inhalt ohne Stufenanforderung sagt nichts über eine Stufe, statt
        // "Stufe 0" zu sagen.
        var withLevel = level > 0
            ? (IsGerman ? $"{dutyName}, Stufe {level}" : $"{dutyName}, level {level}")
            : dutyName;

        return category.Length > 0 ? $"{category}: {withLevel}" : withLevel;
    }

    // The word "Kategorie"/"Category" is deliberately NOT spoken in front of the
    // name (user 2026-08-04): the player just pressed the category key, so the
    // context is already clear - only the name carries information. The chat
    // history has always announced its categories this way; the object browser
    // now matches it.
    public static string CategoryQuestCount(string label, int here, int away) =>
        away > 0
            ? (IsGerman
                ? $"{label}: {here} im Gebiet, {away} in anderen Gebieten."
                : $"{label}: {here} in this area, {away} in other areas.")
            : (IsGerman
                ? $"{label}: {here} im Gebiet."
                : $"{label}: {here} in this area.");

    public static string CategoryWaypointCount(int count, int exits) =>
        exits > 0
            ? (IsGerman
                ? $"Wegpunkte: {count} im Gebiet, davon {exits} Übergänge."
                : $"Waypoints: {count} in this area, {exits} of them exits.")
            : (IsGerman
                ? $"Wegpunkte: {count} im Gebiet."
                : $"Waypoints: {count} in this area.");

    public static string CategoryAetheryteCount(int count) =>
        IsGerman
            ? $"Ätheryten: {count} im Gebiet."
            : $"Aetherytes: {count} in this area.";

    // ── FATEs: aktive Welt-Ereignisse der Zone ──
    public static string CategoryFateCount(int active, int preparing) =>
        preparing > 0
            ? (IsGerman
                ? $"FATEs: {active} aktiv, {preparing} starten gleich."
                : $"FATEs: {active} active, {preparing} starting soon.")
            : (IsGerman
                ? $"FATEs: {active} aktiv."
                : $"FATEs: {active} active.");

    /// <summary>One FATE line: name, level, then either the completion percent or,
    /// for a not-yet-started FATE, a "starting soon" note.</summary>
    public static string FateEntry(string name, int level, byte progress, bool preparing) =>
        IsGerman
            ? $"{name}, Stufe {level}, {(preparing ? "startet gleich" : $"{progress} Prozent")}"
            : $"{name}, level {level}, {(preparing ? "starting soon" : $"{progress} percent")}";

    public static string NoFatesInZone =>
        IsGerman ? "Keine FATEs in diesem Gebiet." : "No FATEs in this area.";

    // ── Jagdziele: offene Monster des aktuellen Jagdtagebuch-Rangs ──
    public static string CategoryHuntingCount(int total, int here) =>
        here > 0
            ? (IsGerman
                ? $"Jagdziele: {total} offen, {here} in diesem Gebiet."
                : $"Hunting targets: {total} open, {here} in this area.")
            : (IsGerman
                ? $"Jagdziele: {total} offen, keines in diesem Gebiet."
                : $"Hunting targets: {total} open, none in this area.");

    /// <summary>One hunting log line: the monster and how many kills are still missing.</summary>
    public static string HuntingTargetEntry(string monster, int killed, int required) =>
        IsGerman
            ? $"{monster}, {killed} von {required} erlegt"
            : $"{monster}, {killed} of {required} killed";

    /// <summary>Said instead of the habitat when a live specimen is in range -
    /// the distance and direction that follow lead to the monster itself.</summary>
    public static string HuntingMonsterNearby =>
        IsGerman ? "in der Nähe" : "nearby";

    /// <summary>The area a hunting log monster lives in, as the log names it.</summary>
    public static string HuntingArea(string area) =>
        area.Length == 0 ? string.Empty : (IsGerman ? $"lebt in {area}" : $"lives in {area}");

    public static string HuntingNoRoute(string monster, string zone) =>
        IsGerman
            ? $"{monster} lebt in {zone}. Dorthin führt kein Weg über Gebietsübergänge."
            : $"{monster} lives in {zone}. No route there over zone transitions.";

    public static string HuntingAreaUnknown(string monster, string area) =>
        area.Length > 0
            ? (IsGerman
                ? $"{monster} lebt in {area}. Dieses Gebiet ist auf der Karte nicht verzeichnet."
                : $"{monster} lives in {area}. That area is not marked on the map.")
            : (IsGerman
                ? $"Für {monster} ist kein Ort bekannt."
                : $"No location known for {monster}.");

    public static string NoHuntingTargets =>
        IsGerman
            ? "Keine offenen Jagdziele in diesem Rang."
            : "No open hunting targets in this rank.";

    // ── Areal absuchen ──
    //
    // Ein Lebensraum ist oft ein GEBIET aus mehreren Stücken, nicht ein Punkt
    // ("Sandtor" sind sechs Teilstücke über rund 500 mal 400 Meter). Die Zahl
    // muss mitgesprochen werden: ohne sie klingt die Entfernung wie die zum
    // Monster, und der Spieler landet auf einem leeren Fleck und hält das für
    // einen Fehler (genau so passiert, 2026-09-02).
    /// <summary>One search point of a habitat: which piece it is, and how to get
    /// there. The piece's own name is spoken when it has one.</summary>
    public static string HuntingSearchPart(string spotName, int index, int count,
                                           string distance, string direction) =>
        spotName.Length > 0
            ? (IsGerman
                ? $"Suchpunkt {index} von {count}, {spotName}, {distance}, {direction}."
                : $"search point {index} of {count}, {spotName}, {distance}, {direction}.")
            : (IsGerman
                ? $"Suchpunkt {index} von {count}, {distance}, {direction}."
                : $"search point {index} of {count}, {distance}, {direction}.");

    /// <summary>Said the moment a specimen of the selected hunting target turns
    /// up in the object table - the point of the whole search.</summary>
    public static string HuntingTargetInRange(string monster, string distance, string direction) =>
        IsGerman
            ? $"{monster} in Reichweite, {distance}, {direction}."
            : $"{monster} in range, {distance}, {direction}.";

    // ── Jagdziele der Staatlichen Gesellschaft ──
    //
    // Der Name der Gesellschaft steht in der Kopfansage, damit hörbar ist,
    // WESSEN Liste da läuft - die Zuordnung ist das Einzige an dieser Kategorie,
    // das vom Spielstand abhängt.
    public static string CategoryCompanyHuntCount(string company, int total, int here)
    {
        var label = company.Length > 0
            ? company
            : (IsGerman ? "Jagdziele der Gesellschaft" : "Grand company targets");
        return here > 0
            ? (IsGerman
                ? $"{label}: {total} offen, {here} in diesem Gebiet."
                : $"{label}: {total} open, {here} in this area.")
            : (IsGerman
                ? $"{label}: {total} offen, keines in diesem Gebiet."
                : $"{label}: {total} open, none in this area.");
    }

    public static string NoCompanyHuntTargets =>
        IsGerman
            ? "Keine offenen Jagdziele der Gesellschaft in diesem Rang."
            : "No open grand company targets in this rank.";

    // ── Blaumagie: die noch fehlenden Zauber und ihr Fundort ──
    //
    // Bewusster Unterschied zu den Jagdzielen daneben: dort nennt die Zeile das
    // MONSTER, hier nur den ORT. Das Spiel fuehrt fuer Blaumagie keine Zuordnung
    // Zauber -> Monster (siehe AozSpellSourceService), und ein erfundener
    // Monstername waere schlimmer als keiner.
    public static string CategoryBlueMagicCount(int total, int here) =>
        here > 0
            ? (IsGerman
                ? $"Blaumagie: {total} Zauber fehlen, {here} in diesem Gebiet."
                : $"Blue magic: {total} spells missing, {here} in this area.")
            : (IsGerman
                ? $"Blaumagie: {total} Zauber fehlen, keiner in diesem Gebiet."
                : $"Blue magic: {total} spells missing, none in this area.");

    /// <summary>One blue magic line: the spell and its number in the book.</summary>
    public static string BlueMagicEntry(string spell, byte number) =>
        IsGerman ? $"{spell}, Nr. {number}" : $"{spell}, no. {number}";

    /// <summary>
    /// Said when the player already stands in the spell's own area. The sheet
    /// names the zone and NOTHING finer - unlike the hunting log, which knows
    /// the habitat - so there is no closer point to walk to. Saying so is the
    /// honest answer; the carrier has to be found via the enemy category.
    /// </summary>
    public static string BlueMagicHere =>
        IsGerman
            ? "hier in diesem Gebiet. Das Spiel nennt keine genauere Stelle, such den Träger über die Kategorie Gegner."
            : "here in this area. The game names no closer spot; find the carrier via the enemy category.";

    /// <summary>The zone plus how many transitions away it is.</summary>
    public static string InAreaWithHops(string zone, int hops) =>
        hops <= 0
            ? InArea(zone)
            : (IsGerman
                // "Gebietswechsel" ist im Deutschen in Ein- und Mehrzahl gleich.
                ? $"im Gebiet {zone}, {hops} Gebietswechsel entfernt."
                : $"in the area {zone}, {hops} {(hops == 1 ? "transition" : "transitions")} away.");

    /// <summary>The spell is learned inside an instance, named by it.</summary>
    public static string BlueMagicInDuty(string duty) =>
        IsGerman ? $"in der Instanz {duty}." : $"in the duty {duty}.";

    /// <summary>
    /// The game names no place at all for this spell. True for exactly 14 of the
    /// 124: thirteen Masked Carnivale rewards and the starting spell (measured
    /// offline 2026-09-02 - their location field is empty, not merely unmapped).
    /// </summary>
    public static string BlueMagicNoPlace =>
        IsGerman
            ? "kein Fundort in der Welt, Belohnung aus dem Maskierten Karneval oder Startzauber."
            : "no location in the world; a Masked Carnivale reward or the starting spell.";

    public static string NoBlueMagicTargets =>
        IsGerman
            ? "Es fehlt kein Blaumagie-Zauber."
            : "No blue magic spells missing.";

    public static string BlueMagicNoRoute(string spell, string zone) =>
        IsGerman
            ? $"{spell} gibt es in {zone}. Dorthin führt kein Weg über Gebietsübergänge."
            : $"{spell} is found in {zone}. No route there over zone transitions.";

    public static string BlueMagicNoDoor(string spell, string duty) =>
        IsGerman
            ? $"{spell} gibt es in {duty}. Zu diesem Eingang ist kein Ort hinterlegt."
            : $"{spell} is found in {duty}. No location is recorded for that entrance.";

    // ── Alle Inhalte: die weltweite Dungeon-, Prüfungs- und Raid-Liste ──

    /// <summary>
    /// Kategorie-Ansage der weltweiten Inhaltsliste. Beide Zahlen zählen: die
    /// erste sagt, wie lang die Liste ist, die zweite, wie viel davon der Spieler
    /// heute betreten darf. Die zweite fällt weg, wenn das Spiel die
    /// Freischaltfrage nicht beantwortet - geraten wird sie nicht.
    /// </summary>
    public static string CategoryWorldDutyCount(int total, int unlocked) =>
        IsGerman
            ? $"Alle Inhalte: {total}, davon {unlocked} freigeschaltet."
            : $"All duties: {total}, {unlocked} of them unlocked.";

    // ── Dungeon: die Stationen des Wegs, in Reihenfolge ──

    /// <summary>
    /// Kopfansage der Kategorie. Nennt die Zahl der Stationen UND die, bei der
    /// der Browser gerade steht - die zweite Zahl ist die eigentliche Auskunft:
    /// "wie weit bin ich" ist beim Durchqueren eines Dungeons die Frage, nicht
    /// "wie lang ist die Liste".
    /// </summary>
    public static string CategoryDungeonCount(int total, int next) =>
        IsGerman
            ? $"Dungeon: {total} Stationen, weiter bei {next}."
            : $"Dungeon: {total} stations, continuing at {next}.";

    /// <summary>
    /// Für diesen Ort liegt keine Wegdatei vor. Wird gesagt und nicht
    /// verschwiegen: die Kategorie erscheint nur, wo es eine gibt, also ist ein
    /// leerer Fall hier immer ein Hinweis, dass etwas mit der Datei nicht stimmt.
    /// </summary>
    public static string NoDungeonRoute =>
        IsGerman
            ? "Für diesen Ort ist kein Weg hinterlegt."
            : "No route is stored for this place.";

    /// <summary>Wie eine Station heißt, die weder Art noch Namen trägt. Nur der
    /// Auto-Lauf braucht das Wort wirklich - er sagt "Laufe zu ...", und dort
    /// darf nichts Leeres stehen.</summary>
    public static string DungeonWaypointWord =>
        IsGerman ? "Wegpunkt" : "waypoint";

    /// <summary>Die Art einer Station, gesprochen. Ein reiner Wegpunkt trägt
    /// keine Art - er heißt nur nach seiner Nummer, alles andere wäre Füllwort.</summary>
    public static string DungeonStepKindWord(DungeonStepKind kind) => kind switch
    {
        DungeonStepKind.Interact => IsGerman ? "benutzen"      : "interact",
        DungeonStepKind.Boss     => IsGerman ? "Boss"          : "boss",
        DungeonStepKind.Treasure => IsGerman ? "Schatztruhe"   : "treasure coffer",
        DungeonStepKind.Jump     => IsGerman ? "springen"      : "jump",
        _                        => string.Empty,
    };

    /// <summary>
    /// Eine Station im Browser.
    ///
    /// <para>
    /// DIE NUMMER STEHT VORN, anders als in jeder anderen Kategorie. Dort ist die
    /// Zählung eine Nebenauskunft am Satzende ("3 von 12"); hier IST sie die
    /// Auskunft - die Reihenfolge ist der ganze Grund, warum es die Kategorie
    /// gibt, und der Spieler soll sie hören, bevor der Rest des Satzes läuft.
    /// </para>
    /// </summary>
    public static string DungeonStepEntry(
        int number, int total, string kindWord, string name, string distance, string direction)
    {
        var head = IsGerman ? $"{number} von {total}" : $"{number} of {total}";

        // Art und Name sind beide oft da, oft nur eines, manchmal keines - ein
        // Wegpunkt hat weder das eine noch das andere. Zusammensetzen statt
        // vier Formatvarianten zu pflegen.
        var what = string.Join(", ", new[] { kindWord, name }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrEmpty(what)) what = DungeonWaypointWord;

        return $"{head}: {what}, {distance}, {direction}.";
    }

    /// <summary>Die letzte Station ist erreicht. Der Dungeon endet mit einem
    /// Boss, also ist das eine echte Auskunft und kein Listenende.</summary>
    public static string DungeonRouteEnd =>
        IsGerman
            ? "Letzte Station des Wegs."
            : "Last station of the route.";

    /// <summary>Die Liste ist leer - kann nur passieren, wenn die Sheets nicht lesbar waren.</summary>
    public static string NoWorldDuties =>
        IsGerman
            ? "Keine Inhalte in der Liste."
            : "No duties in the list.";

    /// <summary>
    /// Dieser Inhalt ist noch nicht freigeschaltet. Steht früh im Satz, weil es
    /// entscheidet, ob Entfernung und Weg den Spieler überhaupt interessieren.
    /// Kommt aus der spieleigenen Prüfung, nicht aus einem Stufenvergleich.
    /// </summary>
    public static string DutyLocked =>
        IsGerman ? "gesperrt" : "locked";

    /// <summary>
    /// Zu diesem Eingang führt kein Weg über Zonenübergänge - z.B. weil er selbst
    /// in einer Instanz steht. Wird gesagt statt verschwiegen: sonst drückt der
    /// Spieler eine Taste, die nichts tun kann.
    /// </summary>
    public static string DutyNoWalkingRoute =>
        IsGerman
            ? "kein Laufweg dorthin."
            : "no walking route there.";

    /// <summary>
    /// Der Auto-Lauf wurde auf einen Inhalt in einer anderen Zone gedrückt, zu
    /// der kein Übergang führt. Der Zonenname bleibt drin: er sagt dem Spieler,
    /// wonach er suchen muss (Ätheryt, Schiff), statt ihn ratlos zu lassen.
    /// </summary>
    // ── Peil-Ton (BeaconService) ──

    /// <summary>Peil-Ton eingeschaltet.</summary>
    public static string TargetBeaconOn =>
        IsGerman
            // Wortlaut geaendert 2026-08-23: der Ton verstummt NICHT mehr beim
            // Ausrichten (siehe BeaconService). Ein gesprochener Satz, der ein
            // Verhalten verspricht, das es nicht mehr gibt, schickt den Spieler
            // auf die Suche nach einem Fehler, der keiner ist.
            ? "Peil-Ton an. Er wird mittig und ruhig, wenn du richtig stehst."
            : "Target beacon on. It centres and slows once you are lined up.";

    /// <summary>Peil-Ton ausgeschaltet.</summary>
    public static string TargetBeaconOff =>
        IsGerman ? "Peil-Ton aus." : "Target beacon off.";

    /// <summary>
    /// Name einer Zielart, gesprochen im Klangtest (/acc soundtest) vor der
    /// zugehörigen Stimme - sonst hört man acht Töne und weiß nicht, welcher
    /// wofür steht.
    /// </summary>
    public static string BeaconKindName(BeaconKind kind) => kind switch
    {
        BeaconKind.Enemy        => IsGerman ? "Gegner"          : "Enemy",
        BeaconKind.Npc          => IsGerman ? "NPC"             : "NPC",
        BeaconKind.Object       => IsGerman ? "Objekt"          : "Object",
        BeaconKind.Gathering    => IsGerman ? "Sammelpunkt"     : "Gathering node",
        BeaconKind.Transition   => IsGerman ? "Übergang"        : "Transition",
        BeaconKind.Aetheryte    => IsGerman ? "Ätheryt"         : "Aetheryte",
        BeaconKind.Quest        => IsGerman ? "Quest-Ziel"      : "Quest objective",
        BeaconKind.DutyEntrance => IsGerman ? "Inhalts-Eingang" : "Duty entrance",
        _                       => IsGerman ? "Ziel"            : "Target",
    };

    public static string DutyNoRouteTo(string dutyName, string zoneName) =>
        IsGerman
            ? $"{dutyName} liegt in {zoneName}. Dorthin führt kein Weg über Zonenübergänge."
            : $"{dutyName} is in {zoneName}. No route there over zone transitions.";

    // ── Freibriefe (Levequests): Geber-NPCs + Ziele + Gegner des laufenden Leves ──

    /// <summary>Category count. The enemy part is only spoken while a battle leve
    /// is running - outside that there are no leve enemies, and a permanent
    /// "0 Gegner" would make the player look for something that cannot be there.</summary>
    public static string CategoryLevequestCount(int givers, int goals, int enemies) =>
        IsGerman
            ? enemies > 0
                ? $"Freibriefe: {enemies} Gegner, {givers} Geber, {goals} Ziele."
                : $"Freibriefe: {givers} Geber, {goals} Ziele."
            : enemies > 0
                ? $"Levequests: {enemies} enemies, {givers} givers, {goals} goals."
                : $"Levequests: {givers} givers, {goals} goals.";

    /// <summary>Spoken role prefix so the player knows whether a leve destination
    /// is the Levemete (accept/hand in), the objective (do the task) or one of
    /// the enemies the running leve asks them to kill.</summary>
    public static string LeveRolePrefix(QuestMarkerRole role) => role switch
    {
        QuestMarkerRole.LeveGiver     => IsGerman ? "Freibrief-Geber: " : "Levequest giver: ",
        QuestMarkerRole.LeveObjective => IsGerman ? "Freibrief-Ziel: "  : "Levequest goal: ",
        QuestMarkerRole.LeveEnemy     => IsGerman ? "Freibrief-Gegner: " : "Levequest enemy: ",
        _                             => string.Empty,
    };

    /// <summary>How many of this monster the leve wants, appended to an enemy
    /// entry ("Freibrief-Gegner: Stolper-Fungus, 7 gesucht, 12 Meter ...").
    /// The count is what the leve DEMANDS, not what is still missing: the
    /// progress lives in the duty list, the demand in the leve data.</summary>
    public static string LeveEnemyWanted(int required) =>
        IsGerman ? $", {required} gesucht" : $", {required} wanted";

    // ── Aufgabenliste des laufenden Inhalts (Freibrief, Dungeon, FATE) ──
    //
    // Diese Zeilen stehen bei einem sehenden Spieler am Bildschirmrand. Der Text
    // selbst kommt vom Spiel und wird NICHT uebersetzt - nur der Fortschritt
    // dahinter bekommt hier seine Worte.

    public static string NoActiveTasks =>
        IsGerman
            ? "Keine laufende Aufgabe."
            : "No task running.";

    /// <summary>Header before a director's lines: its own name.</summary>
    public static string TasksOf(string title) =>
        IsGerman ? $"Aufgaben: {title}." : $"Tasks: {title}.";

    /// <summary>Header when the director has no name of its own.</summary>
    public static string TasksHeading =>
        IsGerman ? "Aufgaben." : "Tasks.";

    public static string TodoFraction(int current, int needed) =>
        needed > 0
            ? IsGerman ? $", {current} von {needed}" : $", {current} of {needed}"
            : TodoCount(current);

    public static string TodoCount(int current) =>
        IsGerman ? $", {current}" : $", {current}";

    public static string TodoPercent(int percent) =>
        IsGerman ? $", {percent} Prozent" : $", {percent} percent";

    /// <summary>Remaining time of a task line, minutes when it is worth it.</summary>
    public static string TodoTimeLeft(int seconds)
    {
        var minutes = seconds / 60;
        var rest = seconds % 60;
        if (IsGerman)
            return minutes > 0 ? $", noch {minutes} Minuten {rest} Sekunden" : $", noch {rest} Sekunden";
        return minutes > 0 ? $", {minutes} minutes {rest} seconds left" : $", {rest} seconds left";
    }

    /// <summary>Marks a task line the game shows as done.</summary>
    public static string TodoDone => IsGerman ? ", erledigt" : ", done";

    public static string NoLevequests =>
        IsGerman
            ? "Keine Freibriefe. Erst bei einem Freibrief-Geber annehmen."
            : "No levequests. Accept one from a levemete first.";

    // Fishing spots (Angelplätze). Type label used when the spot flows through
    // the shared PlaceDestination path; entry adds the required fishing level.
    public static string FishingSpotType => IsGerman ? "Angelplatz" : "Fishing spot";

    public static string FishingSpotEntry(string name, int level) =>
        IsGerman
            ? $"{name}, Stufe {level}"
            : $"{name}, level {level}";

    public static string CategoryFishingCount(int count) =>
        IsGerman
            ? $"Angelplätze: {count} im Gebiet."
            : $"Fishing spots: {count} in this area.";

    public static string NoFishingSpots =>
        IsGerman ? "Keine Angelplätze in diesem Gebiet." : "No fishing spots in this area.";

    /// <summary>Spoken the moment the game reports the player can cast from where
    /// they stand and face - the orientation cue a blind fisher rotates until
    /// they hear (FishingEventHandler.CanFish flips true in the ready stance).</summary>
    public static string FishReady =>
        IsGerman ? "Angelbereit." : "Ready to fish.";

    /// <summary>Spoken on a bite - strike now (FishingState -> Bite).</summary>
    public static string FishBite =>
        IsGerman ? "Biss!" : "Bite!";

    public static string CategoryObjectCount(string label, int count) =>
        IsGerman
            ? $"{label}: {count} in der Nähe."
            : $"{label}: {count} nearby.";

    public static string NoObjectsInRange(string label, float range) =>
        IsGerman
            ? $"Keine {label} in {range:F0} Metern."
            : $"No {label} within {range:F0} meters.";

    // ── Objekt-/Ziel-Ansagen (NavigationService) ─────────────────────
    // "Unbenannt" removed 2026-08-08: it said nothing about what the thing was,
    // and every caller now uses UnnamedOfKind (or a resolved name) instead.

    /// <summary>Spoken "N of M" position counter for browser cycling (no period).</summary>
    /// <summary>The word between the two numbers of a counter. Exposed because
    /// code that RECOGNISES a counter it printed earlier (see UIReaderService,
    /// IsSpokenProgress) must not hard-code the German "von" - that comparison
    /// silently stops matching the moment the announcement speaks English.</summary>
    public static string CounterConnector => IsGerman ? "von" : "of";

    public static string Counter(int index, int count) =>
        $"{index} {CounterConnector} {count}";

    /// <summary>Same "x of y" form for values that arrive as text (a progress
    /// display read from the UI, e.g. "3/5"), where parsing them to numbers
    /// would only risk losing what the game actually printed.</summary>
    public static string Counter(string index, string count) =>
        $"{index} {CounterConnector} {count}";

    /// <summary>Trailing warning when the game refused to set the target
    /// (leading space, appended to a target announcement).</summary>
    public static string NotTargetedSuffix => IsGerman ? " Achtung, nicht anvisiert." : " Warning, not targeted.";

    public static string TargetPrefix => IsGerman ? "Ziel: " : "Target: ";

    public static string Tracking(string name)      => IsGerman ? $"Verfolge {name}." : $"Tracking {name}.";
    public static string TargetNotFound(string name)=> IsGerman ? $"Ziel {name} nicht gefunden." : $"Target {name} not found.";
    public static string TargetReached(string name) => IsGerman ? $"Ziel erreicht: {name}." : $"Target reached: {name}.";
    public static string TargetDirection(string name, string distance, string direction) =>
        IsGerman ? $"{name}: {distance}, {direction}." : $"{name}: {distance}, {direction}.";

    public static string TrackingStopped => IsGerman ? "Zielverfolgung beendet." : "Target tracking stopped.";
    public static string WalkTargetLost  => IsGerman ? "Gehhilfe: Ziel verloren." : "Walk guide: target lost.";
    public static string NoGameTarget    => IsGerman ? "Kein Ziel anvisiert." : "No target selected.";
    public static string NoNearbyObjects => IsGerman ? "Keine Objekte in der Nähe." : "No objects nearby.";
    public static string NearbyList(string joined) => IsGerman ? $"In der Nähe: {joined}" : $"Nearby: {joined}";

    /// <summary>"No target. Select an object with Page Down first." (object browser hint).
    /// The object browser moved off N onto the Page keys in V5.31, so the hint
    /// names Page Down (KeyNextObject default) now, not the old N.</summary>
    public static string NoTargetSelectN => IsGerman
        ? "Kein Ziel. Erst mit Bild ab ein Objekt wählen."
        : "No target. Select an object with Page Down first.";

    /// <summary>"No target set. Select an object with Page Down first." (direction key).</summary>
    public static string NoTargetTracked => IsGerman
        ? "Kein Ziel gesetzt. Erst mit Bild ab ein Objekt wählen."
        : "No target set. Select an object with Page Down first.";

    /// <summary>Type name for an object kind, spoken after the object name.</summary>
    public static string ObjectKindName(ObjectKind kind) => kind switch
    {
        ObjectKind.Pc             => IsGerman ? "Spieler"     : "Player",
        ObjectKind.BattleNpc      => IsGerman ? "Kampf-NPC"   : "Combat NPC",
        ObjectKind.EventNpc       => IsGerman ? "NPC"         : "NPC",
        ObjectKind.Treasure       => IsGerman ? "Schatz"      : "Treasure",
        ObjectKind.Aetheryte      => IsGerman ? "Ätheryt"     : "Aetheryte",
        ObjectKind.GatheringPoint => IsGerman ? "Sammelpunkt" : "Gathering node",
        ObjectKind.EventObj       => IsGerman ? "Objekt"      : "Object",
        // The game's own class for usable housing furniture (Chocobo-Stall,
        // Briefkasten, Diarium-Pult). "Einrichtung" rather than "Möbel": it also
        // covers the stable and the mailbox, which are not furniture in the
        // everyday sense but are exactly this kind to the game.
        ObjectKind.HousingEventObject => IsGerman ? "Einrichtung" : "Furnishing",
        ObjectKind.Companion      => IsGerman ? "Begleiter"   : "Companion",
        ObjectKind.Retainer       => IsGerman ? "Gehilfe"     : "Retainer",
        ObjectKind.Mount          => IsGerman ? "Reittier"    : "Mount",
        _                         => kind.ToString(),
    };

    /// <summary>
    /// Stand-in for an object the GAME itself leaves nameless - "Objekt ohne
    /// Namen", "NPC ohne Namen". Says which kind of thing it is and makes clear
    /// that the missing name is the game's, not a failure of the mod (user
    /// decision 2026-08-08). Verified offline the same day: for every nameless
    /// object in the log, the game's own name sheets are empty too.
    /// </summary>
    public static string UnnamedOfKind(ObjectKind kind) => IsGerman
        ? $"{ObjectKindName(kind)} ohne Namen"
        : $"{ObjectKindName(kind)} with no name";

    /// <summary>
    /// The two objects the game names with an ICON instead of a word. Both words
    /// are the game's own vocabulary, not an invention of this mod - see
    /// ObjectNameService.IconNamed for where each one is quoted from.
    /// </summary>
    public static string GardenBed => IsGerman ? "Beet"        : "Garden bed";
    public static string MailBox   => IsGerman ? "Postkasten"  : "Mailbox";

    /// <summary>
    /// Appended to an object's name to say which quest it serves: "Zielort für
    /// Narben im Wald". The game calls 1667 different props "Zielort", so the
    /// name alone identifies nothing (user report 2026-08-08).
    /// </summary>
    public static string ForQuest(string quest) => IsGerman
        ? $" für {quest}"
        : $" for {quest}";

    /// <summary>
    /// Appended to a zone transition to say where it leads: "Ausgang nach
    /// Neu-Gridania".
    /// </summary>
    public static string LeadsToArea(string area) => IsGerman
        ? $" nach {area}"
        : $" to {area}";

    /// <summary>
    /// Appended to an object the player has already stood next to: "Truhe 2,
    /// Schatz, schon besucht". In a dungeon several things carry one name, and
    /// which of them one has already dealt with is the thing a sighted player
    /// reads off the room they remember walking through (user wish 2026-08-08).
    /// Leading comma and space, so it slots into the description like the kind.
    /// </summary>
    public static string AlreadyVisited => IsGerman ? ", schon besucht" : ", already visited";

    /// <summary>Quest hint from a nameplate icon id, or empty for none.</summary>
    public static string QuestMarkerHint(uint iconId) => iconId switch
    {
        0                     => string.Empty,
        >= 71001 and <= 71006 => IsGerman ? "Quest verfügbar" : "Quest available",
        >= 71021 and <= 71046 => IsGerman ? "Quest aktiv"     : "Quest active",
        >= 71000 and <= 71999 => IsGerman ? "Quest"           : "Quest",
        _                     => string.Empty,
    };

    /// <summary>Gathering-node description ("Gathering node" / "&lt;type&gt;, level N").
    /// <paramref name="type"/> is the game-provided node type; may be empty.</summary>
    public static string GatheringNodeFallback => IsGerman ? "Sammelpunkt" : "Gathering node";
    public static string GatheringNodeDesc(string type, int level) =>
        level > 0
            ? (IsGerman ? $"{type}, Stufe {level}" : $"{type}, level {level}")
            : type;

    // ── Quest-Ziel-Ansage (zusammengesetzt) ──────────────────────────
    public static string NoAcceptableQuests => IsGerman ? "Keine annehmbaren Quests in der Nähe." : "No available quests nearby.";
    public static string NoQuestGoals       => IsGerman ? "Keine Quest-Ziele. Erst eine Quest annehmen." : "No quest goals. Accept a quest first.";
    public static string StoryPrefix        => IsGerman ? "Story: " : "Story: ";

    /// <summary>
    /// The kind of quest, spoken in front of the quest name. Every known kind is
    /// named, side quests included: silence would leave the player unable to tell
    /// "side quest" from "feature broken" (user 2026-08-06). Only
    /// <see cref="QuestKind.Unknown"/> stays empty - there we have nothing to
    /// back a claim with. Main story keeps the wording players already know from
    /// <see cref="StoryPrefix"/>.
    /// </summary>
    public static string QuestKindPrefix(QuestKind kind) => kind switch
    {
        QuestKind.MainStory  => StoryPrefix,
        QuestKind.Job        => IsGerman ? "Job: " : "Job: ",
        QuestKind.BeastTribe => IsGerman ? "Freundesvolk: " : "Beast tribe: ",
        QuestKind.Chronicle  => IsGerman ? "Chronik: " : "Chronicle: ",
        QuestKind.SideQuest  => IsGerman ? "Nebenauftrag: " : "Side quest: ",
        QuestKind.Other      => IsGerman ? "Sonstiges: " : "Other: ",
        _                    => string.Empty,
    };
    public static string LevelPrefix(int level) => IsGerman ? $"Stufe {level}, " : $"Level {level}, ";
    public static string InArea(string zone)    => IsGerman ? $"im Gebiet {zone}." : $"in the area {zone}.";
    public static string InAnotherArea       => IsGerman ? "in einem anderen Gebiet." : "in another area.";
    public static string NumpadWalksToTransition => IsGerman ? " Nummernblock 3 läuft zum Übergang." : " Numpad 3 walks to the transition.";

    /// <summary>
    /// Whether the player stands inside the marker's goal circle. A sighted
    /// player sees that circle on the map; without it, a distance like "75 Meter"
    /// says nothing about being in the right place - and on a search leve the
    /// enemies only appear once the player is inside.
    /// </summary>
    public static string InsideGoalCircle => IsGerman ? ", im Zielkreis" : ", inside the goal area";

    /// <summary>How far the player still is from the EDGE of the goal circle.
    /// <paramref name="distance"/> is already formatted.</summary>
    public static string ToGoalCircle(string distance) =>
        IsGerman ? $", noch {distance} bis zum Zielkreis" : $", {distance} to the goal area";

    /// <summary>The "get there via &lt;transition&gt;" clause of a cross-zone quest
    /// announcement, including the count of remaining transitions.</summary>
    public static string RouteViaHop(string hopName, string distance, string direction, int extraHops) =>
        IsGerman
            ? $" Dorthin über {hopName}, {distance}, {direction}" +
              (extraHops > 0 ? $", danach noch {extraHops} weitere Übergänge." : ".")
            : $" Get there via {hopName}, {distance}, {direction}" +
              (extraHops > 0 ? $", then {extraHops} more transitions." : ".");

    // ── Wegpunkte / Gehhilfe / Routen-Ansagen ────────────────────────
    /// <summary>Appended to a marker selection when the real object behind it
    /// was taken as the game target - that is what makes it usable, and the
    /// player has no other way to tell.</summary>
    public static string MarkerTargeted => IsGerman ? "Angezielt." : "Targeted.";

    public static string NoAetherytesFound => IsGerman ? "Keine Ätheryten in diesem Gebiet gefunden." : "No aetherytes found in this area.";
    public static string NoWaypointsFound  => IsGerman ? "Keine Wegpunkte in diesem Gebiet gefunden." : "No waypoints found in this area.";
    public static string NoNavmeshStraightLine => IsGerman ? "Kein Wegenetz, führe in Luftlinie." : "No navmesh, guiding in a straight line.";
    public static string ComputingRoute    => IsGerman ? "Weg wird berechnet." : "Computing route.";
    public static string NewRoute(string direction) => IsGerman ? $"Neuer Weg: {direction}." : $"New route: {direction}.";
    public static string ComputingRouteTo(string name) => IsGerman ? $"Berechne Weg zu {name}." : $"Computing route to {name}.";
    public static string NoNavmeshPlugin   => IsGerman
        ? "Kein Wegenetz. Das Plugin vnavmesh fehlt oder lädt noch."
        : "No navmesh. The vnavmesh plugin is missing or still loading.";
    public static string NewFlagMarker(string distance, string compass) =>
        IsGerman ? $"Neue Markierung, {distance}, {compass}." : $"New flag, {distance}, {compass}.";

    /// <summary>", up"/", down" vertical hint appended to a guide step; "" when level.</summary>
    public static string VerticalUp   => IsGerman ? ", aufwärts" : ", up";
    public static string VerticalDown => IsGerman ? ", abwärts"  : ", down";

    // ── Routen-Vorschau (RouteService.DescribeRoute) ─────────────────
    public static string RoutePracticallyThere(string name) =>
        IsGerman ? $"Weg zu {name}: praktisch am Ziel." : $"Route to {name}: practically there.";
    public static string RouteHeader(string name, float total) =>
        IsGerman ? $"Weg zu {name}, {total:F0} Meter: " : $"Route to {name}, {total:F0} meters: ";
    public static string RouteSegment(float distance, string compass) =>
        IsGerman ? $"{distance:F0} Meter nach {compass}" : $"{distance:F0} meters {compass}";
    public static string RouteThen => IsGerman ? ", dann " : ", then ";
    public static string RouteAndOn => IsGerman ? ", dann weiter" : ", then onward";

    /// <summary>Der Hoehenanteil des Weges, angehaengt an die Vorschau. Getrennt
    /// nach Auf und Ab, weil eine Verrechnung die Treppe verschweigen wuerde, ueber
    /// die es hoch und wieder herunter geht.</summary>
    public static string RouteClimb(float up, float down)
    {
        if (up > 0f && down > 0f)
            return IsGerman
                ? $" Dabei {up:F0} Meter aufwärts und {down:F0} Meter abwärts."
                : $" Along the way {up:F0} meters up and {down:F0} meters down.";
        if (up > 0f)
            return IsGerman ? $" Dabei {up:F0} Meter aufwärts." : $" Along the way {up:F0} meters up.";
        return IsGerman ? $" Dabei {down:F0} Meter abwärts." : $" Along the way {down:F0} meters down.";
    }

    // ── Datenzentrums-Auswahl (TitleDCWorldMap) ──────────────────────
    public static string DCSelected(string dc, IReadOnlyCollection<string> worlds) =>
        worlds.Count > 0
            ? (IsGerman
                ? $"{dc} ausgewählt. Welten: {string.Join(", ", worlds)}. Zum Bestätigen den Ok-Knopf drücken."
                : $"{dc} selected. Worlds: {string.Join(", ", worlds)}. Press the Ok button to confirm.")
            : (IsGerman ? $"{dc} ausgewählt." : $"{dc} selected.");

    // ════════════════════════════════════════════════════════════════
    //  UIReaderService - Fenster-, Listen- und Menue-Ansagen
    //  NOTE: Only the mod's OWN announcement frames are translated here.
    //  Strings that MATCH against the game UI (button labels like "Schließen",
    //  "Bestätigen", journal headers) stay in the game-client language and are
    //  handled by the separate client-language-robustness work, NOT via /acc lang.
    // ════════════════════════════════════════════════════════════════

    // ── Listen / Menue (Social, generische Auswahl) ──────────────────
    /// <summary>List summary: "&lt;selection&gt;, N entries" or "Menu, N entries"
    /// when nothing is selected.</summary>
    public static string ListSummary(string selection, int count) =>
        selection.Length > 0
            ? (IsGerman ? $"{selection}, {count} Einträge" : $"{selection}, {count} entries")
            : (IsGerman ? $"Menü, {count} Einträge"        : $"Menu, {count} entries");

    public static string NoEntries       => IsGerman ? "Keine Einträge" : "No entries";
    public static string NoEntriesSuffix => IsGerman ? ", keine Einträge" : ", no entries";

    /// <summary>", N entries" plus optional ": &lt;selection&gt;", appended to a tab line.</summary>
    public static string ListEntriesSuffix(int count, string selection) =>
        IsGerman
            ? $", {count} Einträge{(selection.Length > 0 ? $": {selection}" : string.Empty)}"
            : $", {count} entries{(selection.Length > 0 ? $": {selection}" : string.Empty)}";

    public static string SocialTabHeader(string label, int index, int total) =>
        IsGerman
            ? $"{label}, Registerkarte {index} von {total}"
            : $"{label}, tab {index} of {total}";

    public static string OnlineWindowPrefix(string rest) =>
        IsGerman ? $"Online-Fenster. {rest}" : $"Online window. {rest}";

    // ── Text-Eingabe-Echo (beim Tippen) ──────────────────────────────
    public static string InputEmpty => IsGerman ? "leer" : "empty";
    public static string Deleted(string removed) => IsGerman ? $"{removed} gelöscht" : $"{removed} deleted";

    // ── Benachrichtigung (ActivateNotification) ──────────────────────
    public static string NoOpenNotification => IsGerman ? "Keine offene Benachrichtigung." : "No open notification.";
    public static string NotificationNotResponding => IsGerman ? "Benachrichtigung reagiert nicht." : "Notification not responding.";

    // ── ContentsTutorial-Popup (Freischaltungen) ─────────────────────
    // NOTE: The actual close-button match ("Schließen") lives in the service and
    // stays in the game-client language (Teil 2), these are the spoken frames.
    public static string PageOf(int current, int total) =>
        IsGerman ? $" Seite {current} von {total}." : $" Page {current} of {total}.";

    /// <summary>
    /// The window's own page indicator, passed through as it reads ("1/2").
    /// Used where the game already formats it and only the word is missing.
    /// </summary>
    public static string PageLabel(string page) =>
        IsGerman ? $"Seite {page}" : $"Page {page}";

    /// <summary>Next page, for windows whose paging button carries no text.</summary>
    public static string NextPage => IsGerman ? "Weiter" : "Next";

    /// <summary>
    /// A control that exists but is switched off right now - a paging button on
    /// the first page, for instance. Without this the focus lands on a button
    /// that looks like any other and the player presses into the void, never
    /// learning why nothing happens.
    /// </summary>
    public static string ControlUnavailable(string label) =>
        IsGerman ? $"{label}, nicht verfügbar" : $"{label}, unavailable";
    public static string EnterCloses    => IsGerman ? " Enter schließt." : " Press Enter to close.";
    public static string EnterPagesOn   => IsGerman ? " Enter blättert weiter." : " Press Enter to continue.";
    public static string Closed         => IsGerman ? "Geschlossen." : "Closed.";
    public static string CloseButtonNotResponding => IsGerman ? "Schließen-Knopf reagiert nicht." : "Close button not responding.";
    public static string NextButtonNotResponding  => IsGerman ? "Weiter-Knopf reagiert nicht." : "Next button not responding.";

    // ── Bestiarium (MonsterNote) ─────────────────────────────────────
    public static string BestiaryNotOpen   => IsGerman ? "Bestiarium ist nicht geöffnet." : "The bestiary is not open.";
    public static string BestiaryListNotFound => IsGerman ? "Bestiarium-Liste nicht gefunden." : "Bestiary list not found.";
    public static string NoMonstersInList  => IsGerman ? "Keine Monster in dieser Liste." : "No monsters in this list.";
    public static string LivesIn(string habitat) => IsGerman ? $", lebt in {habitat}" : $", lives in {habitat}";
    public static string BestiaryOverview(int count, string rows) =>
        IsGerman ? $"Bestiarium, {count} Monster. {rows}" : $"Bestiary, {count} monsters. {rows}";
    /// <summary>A rank picker row: which rank, and how many of its ten entries are done.</summary>
    public static string BestiaryRankRow(string rank, int done, int total) => done >= total
        ? (IsGerman ? $"Rang {rank}, alle {total} Einträge erledigt" : $"Rank {rank}, all {total} entries complete")
        : (IsGerman ? $"Rang {rank}, {done} von {total} Einträgen erledigt" : $"Rank {rank}, {done} of {total} entries done");

    // ── Gegenstand abliefern (Request / delivery) ────────────────────
    // "Hand Over" is the EN client's button; verify against an EN dump in Teil 2.
    public static string DeliveryOpen => IsGerman
        ? "Gegenstand abliefern. Drücke Strg F3 für die passenden Gegenstände, dann auswählen und Übergeben."
        : "Hand over item. Press Ctrl F3 for the matching items, then select and Hand Over.";
    public static string DeliveryItems(IReadOnlyList<string> items) => items.Count switch
    {
        0 => IsGerman ? "Keine passenden Gegenstände im Beutel gefunden." : "No matching items found in your bag.",
        1 => IsGerman
                ? $"Ein passender Gegenstand: {items[0]}. Auswählen und dann Übergeben drücken."
                : $"One matching item: {items[0]}. Select it, then press Hand Over.",
        _ => IsGerman
                ? $"{items.Count} passende Gegenstände: {string.Join(", ", items)}. Auswählen und dann Übergeben drücken."
                : $"{items.Count} matching items: {string.Join(", ", items)}. Select one, then press Hand Over.",
    };

    // ── Zufaelliges Aussehen (CharaMake RandomLook) ──────────────────
    public static string NoAppearanceWindow => IsGerman
        ? "Kein Aussehen-Fenster offen. Nur im Schritt Aussehen der Charaktererschaffung."
        : "No appearance window open. Only during the Appearance step of character creation.";
    public static string RandomAppearanceNotFound => IsGerman ? "Knopf Zufälliges Aussehen nicht gefunden." : "Random appearance button not found.";
    public static string RandomAppearanceNotResponding => IsGerman ? "Knopf Zufälliges Aussehen reagiert nicht." : "Random appearance button not responding.";
    public static string RandomAppearancePressed => IsGerman ? "Zufälliges Aussehen gedrückt." : "Random appearance pressed.";

    // ── Seitenwechsel / Reiter (generisch) ───────────────────────────
    /// <summary>Konfigurationsseite mit der Anzahl ihrer Einstellungen. Die Zahl
    /// ist die Antwort auf eine echte Frage des Users (2026-08-18): die Seite
    /// meldete sich nur mit ihrer ersten Ueberschrift, die zufaellig genauso
    /// heisst wie die erste Einstellung darunter — "ich frage mich obs noch
    /// andere Menuepunkte ausser Grafik-Voreinstellungen gibt". Ein sehender
    /// Spieler sieht die ganze Seite auf einen Blick; die Zahl ist das
    /// Gegenstueck dazu.</summary>
    public static string ConfigPageWithCount(string heading, int count) =>
        IsGerman
            ? $"{heading}, {count} {(count == 1 ? "Einstellung" : "Einstellungen")}"
            : $"{heading}, {count} {(count == 1 ? "setting" : "settings")}";

    public static string TabPressedNoPageChange => IsGerman ? "Reiter gedrückt, aber kein Seitenwechsel erkannt." : "Tab pressed, but no page change detected.";
    public static string TabNotResponding => IsGerman ? "Reiter reagiert nicht." : "Tab not responding.";

    // ── Datenzentrum / Gamepad / Uebung / Menue ──────────────────────
    public static string ChooseDataCenter => IsGerman ? "Datenzentrum wählen." : "Choose a data center.";
    public static string GamepadCalibration => IsGerman ? "Gamepad-Kalibrierung. Escape zum Schließen." : "Gamepad calibration. Press Escape to close.";
    public static string ExerciseStarted => IsGerman ? "Übung gestartet." : "Exercise started.";
    public static string BeginButtonNotResponding => IsGerman ? "Beginnen-Knopf reagiert nicht." : "Begin button not responding.";
    public static string NoActiveMenu => IsGerman ? "Kein aktives Menü." : "No active menu.";

    // ── Dump (/acc dump) ─────────────────────────────────────────────
    public static string NoActiveAddonToDump => IsGerman ? "Kein aktives Addon für Dump gefunden." : "No active addon found to dump.";
    public static string NoAddonName => IsGerman ? "Kein Addon-Name. Beispiel: /acc dump TitleDCWorldMap" : "No addon name. Example: /acc dump TitleDCWorldMap";
    public static string DumpFileError => IsGerman ? "Dump nur im Dalamud-Log. Datei-Fehler." : "Dump only in the Dalamud log. File error.";
    public static string UnknownWindowDumped(int count) =>
        IsGerman
            ? $"Kein bekanntes Fenster. {count} sichtbare Fenster gedumpt, Liste im Log."
            : $"No known window. Dumped {count} visible windows, list in the log.";

    // ── Zusammengesetzte Ansagen (UIReader Etappe 2) ─────────────────
    /// <summary>" item: " / " items: " count label for a gathered/read item list.</summary>
    public static string ItemsCountLabel(int count) =>
        IsGerman ? (count == 1 ? " Gegenstand: " : " Gegenstände: ")
                 : (count == 1 ? " item: " : " items: ");

    /// <summary>The word "Level" / "Stufe" - used both standalone and to expand
    /// the game's abbreviated level label.</summary>
    public static string LevelWord => IsGerman ? "Stufe" : "Level";
    public static string LevelSuffix(int level) => IsGerman ? $", Stufe {level}" : $", level {level}";
    public static string NameWithLevel(string name, int level) =>
        IsGerman ? $"{name}, Stufe {level}" : $"{name}, level {level}";
    public static string AmountLabel(string yield) => IsGerman ? $"Menge {yield}" : $"Amount {yield}";
    public static string UnknownItem(uint iconId) =>
        IsGerman ? $"Unbekannter Gegenstand, Icon {iconId}" : $"Unknown item, icon {iconId}";

    // ── Konfig-Steuerelemente (Slider / Dropdown / Eingabefeld) ──────
    public static string SliderDesc(string label, string value, int min, int max) =>
        IsGerman
            ? $"{label}, Regler, {value}, von {min} bis {max}."
            : $"{label}, slider, {value}, from {min} to {max}.";
    // Short form for 0..100 percentage sliders (volumes): the "%" already implies
    // the range, so drop "slider" and "from 0 to 100" - the long form got cut off
    // by the next control while navigating quickly (user report 2026-07-27).
    public static string SliderPercent(string label, string value) =>
        IsGerman ? $"{label}, {value} %" : $"{label}, {value}%";
    public static string DropdownDesc(string label, string value) =>
        IsGerman ? $"{label}, Auswahlliste, {value}." : $"{label}, dropdown, {value}.";
    /// <summary>One option while stepping through an OPENED drop-down. Names the
    /// stored choice, which a sighted player sees highlighted in the list.</summary>
    public static string DropdownOption(string option, int index, int count, bool selected) =>
        IsGerman
            ? $"{option}, {index} von {count}{(selected ? ", ausgewählt" : "")}"
            : $"{option}, {index} of {count}{(selected ? ", selected" : "")}";
    // ── Zur Wegrichtung drehen (Numpad5) ─────────────────────────────
    /// <summary>Spoken after the player was turned towards the guide point.</summary>
    public static string FaceAligned(string distance) =>
        IsGerman ? $"Ausgerichtet. {distance} geradeaus." : $"Aligned. {distance} straight ahead.";

    /// <summary>The key was pressed without a walk guide running.</summary>
    public static string FaceNoRoute =>
        IsGerman
            ? "Kein Weg aktiv. Erst ein Ziel wählen."
            : "No route active. Pick a destination first.";

    /// <summary>Guide point and player are on the same spot - no direction to turn to.</summary>
    public static string FaceAlreadyThere =>
        IsGerman ? "Du stehst schon am Wegpunkt." : "You are already at the waypoint.";

    /// <summary>Stand-in when no label text can be found next to a control.</summary>
    public static string NoLabel => IsGerman ? "Ohne Beschriftung" : "Unlabelled";

    /// <summary>The browsed history category is a real chat channel, but its
    /// internal number has not been measured yet, so the mod will not switch to
    /// it rather than risk sending into the wrong channel.</summary>
    public static string ChannelNotAvailable(string channel) =>
        IsGerman
            ? $"Kanal {channel} kann noch nicht gesetzt werden."
            : $"Channel {channel} cannot be set yet.";

    /// <summary>Browsing the tell history, but no message carries a player the
    /// mod could answer.</summary>
    public static string NoTellPartner =>
        IsGerman
            ? "Kein Flüster-Partner zum Antworten."
            : "No tell partner to answer.";

    /// <summary>The game refused the tell target - said out loud, because a
    /// silent failure would look like the message is on its way.</summary>
    public static string TellTargetFailed(string target) =>
        IsGerman
            ? $"Flüstern an {target} nicht möglich."
            : $"Cannot set tell target {target}.";
    public static string InputFieldValue(string typed) =>
        typed.Length > 0
            ? (IsGerman ? $"Eingabefeld: {typed}" : $"Input field: {typed}")
            : (IsGerman ? "Eingabefeld, leer"     : "Input field, empty");

    /// <summary>
    /// An input field that carries no label of its own, named by the control that
    /// says what it is for.
    ///
    /// <para>
    /// Gebaut fuer die Spielersuche: das Namensfeld hat keine eigene Beschriftung,
    /// und was hineingehoert, entscheidet der ausgewaehlte Knopf daneben
    /// (Vorname / Nachname). Ohne diesen Namen sagte das Feld nur "5/15, Azumi" -
    /// ein Zaehler und ein Wort, aber nicht, wonach ueberhaupt gesucht wird.
    /// </para>
    /// </summary>
    public static string NamedInputFieldValue(string label, string typed) =>
        typed.Length > 0
            ? (IsGerman ? $"{label}, Eingabefeld: {typed}" : $"{label}, input field: {typed}")
            : (IsGerman ? $"{label}, Eingabefeld, leer"    : $"{label}, input field, empty");

    // ── Zahl mit ihrer Beschriftung ──────────────────────────────────
    /// <summary>
    /// A number followed by what it counts: "49.457 Gil",
    /// "1.652/10.000 Legionstaler", "350 Errungenschaftspunkte". Used wherever
    /// the game shows a bare figure next to an icon and the word comes from the
    /// game itself (currency rows, the achievement window header).
    ///
    /// The order is the user's decision (2026-08-16), not a default - the name
    /// goes BEHIND the number. Both halves are the game's own words in the
    /// CLIENT language, so this format adds no words of its own and reads the
    /// same in both mod languages.
    /// </summary>
    public static string AmountWithLabel(string amount, string label) => $"{amount} {label}";

    // ── Belohnungs-Zeile (JournalResult) ─────────────────────────────
    // Currency type is only a UI image, so the mod labels amounts by position.
    public static string[] RewardCurrencyLabels =>
        IsGerman ? new[] { "Erfahrung", "Gil" } : new[] { "EXP", "Gil" };
    public static string MoreReward => IsGerman ? "weitere Vergütung" : "further reward";
    /// <summary>Prefix spoken in front of the whole quest-completion reward summary.</summary>
    public static string RewardPrefix => IsGerman ? "Belohnung: " : "Reward: ";
    /// <summary>A reward item with a quantity: German "&lt;qty&gt; mal &lt;name&gt;",
    /// English just "&lt;qty&gt; &lt;name&gt;" (no "times").</summary>
    public static string RewardItemQuantity(string qty, string name) =>
        IsGerman ? $"{qty} mal {name}" : $"{qty} {name}";
    /// <summary>A reward item followed by its description - name first, then the
    /// description, like the ability tooltips (period so the reader pauses).</summary>
    public static string RewardItemWithDescription(string label, string description) =>
        $"{label}. {description}";

    /// <summary>The tooltip description spoken on its own after the focus has
    /// dwelled on an inventory item (the name was already announced when the
    /// focus landed) - prefixed so the user knows what is being read.</summary>
    public static string ItemDescription(string description) =>
        IsGerman ? $"Beschreibung: {description}" : $"Description: {description}";

    /// <summary>
    /// What a row in an exchange window costs, appended to the item name. The
    /// currency is already inflected by the caller from the game's own Singular /
    /// Plural sheet columns, so it is inserted verbatim.
    ///
    /// Without a resolved currency only the bare number is spoken: an invented
    /// unit ("2 Marken") would be worse than none, because the same window trades
    /// certificates, seals, tokens and coins.
    /// </summary>
    public static string ShopPrice(uint count, string currency) =>
        currency.Length > 0
            ? (IsGerman ? $", für {count} {currency}" : $", for {count} {currency}")
            : (IsGerman ? $", Preis {count}"          : $", price {count}");

    /// <summary>How many of the currency the player holds, appended after the
    /// price. Only spoken when the number actually changed - see the caller.</summary>
    public static string ShopOwned(int owned) =>
        IsGerman ? $", du hast {owned}" : $", you have {owned}";

    // ── Inventar-Reiter (Inventory) ──────────────────────────────────
    /// <summary>The active inventory bag tab, announced on switch. The label is
    /// the game's own tab number ("1".."4").</summary>
    public static string InventoryTab(string label) =>
        IsGerman ? $"Tasche {label}" : $"Bag {label}";
    /// <summary>Fallback for an inventory tab the game leaves unlabeled - so the
    /// user still hears that focus reached a tab, without inventing a number.</summary>
    public static string InventoryTabOther =>
        IsGerman ? "Inventar, weiterer Reiter" : "Inventory, other tab";

    // ── Keybind-Zeile (Config) ───────────────────────────────────────
    public static string KeyBindingLine(string label, IReadOnlyList<string> keys) =>
        keys.Count > 0
            ? (IsGerman ? $"{label}, Taste {string.Join(", ", keys)}" : $"{label}, key {string.Join(", ", keys)}")
            : (IsGerman ? $"{label}, keine Taste" : $"{label}, no key");

    // ── Anfaenger-Arena (BeginnersMansionProblem) ────────────────────
    // "Beginner's Arena" is the EN content name; verify against an EN dump (Teil 2).
    public static string ArenaTitle => IsGerman ? "Anfänger-Arena" : "Beginner's Arena";
    public static string ArenaExercise(string exercise) =>
        IsGerman ? $". Übung: {exercise}" : $". Exercise: {exercise}";
    public static string ArenaEnterBegins => IsGerman ? ". Enter beginnt." : ". Press Enter to begin.";

    // ── Benachrichtigung aktivieren ──────────────────────────────────
    public static string Activating(string text) => IsGerman ? $"Aktiviere: {text}" : $"Activating: {text}";
    public static string NotificationActivated => IsGerman ? "Benachrichtigung aktiviert" : "Notification activated";

    // ════════════════════════════════════════════════════════════════
    //  CombatService / VitalsService - Kampf, Vitalwerte, Level
    // ════════════════════════════════════════════════════════════════
    public static string NotLoggedIn => IsGerman ? "Nicht eingeloggt." : "Not logged in.";
    public static string CombatStart => IsGerman ? "Kampf." : "Combat.";
    public static string CombatEnd => IsGerman ? "Kampf vorbei." : "Combat over.";
    public static string AoeWarningOn  => IsGerman ? "Flächenwarnung an." : "Area warning on.";
    public static string AoeWarningOff => IsGerman ? "Flächenwarnung aus." : "Area warning off.";

    /// <summary>
    /// Bar fill as a whole percent - the same reading a sighted player takes off
    /// the bar, which is why HP/MP/GP are announced this way and not as raw
    /// numbers (user decision 2026-08-07).
    /// <para>
    /// Floored, so "50 Prozent" never means "a hair under half". The one
    /// exception is the bottom: 5 of 5000 HP floors to 0, and "HP 0 Prozent"
    /// would sound like death - anything above zero therefore reports at
    /// least 1 percent. Zero is reserved for an empty bar.
    /// </para></summary>
    private static int Percent(uint cur, uint max)
    {
        if (max == 0) return 0;
        var percent = (int)(cur * 100u / max);
        return percent == 0 && cur > 0 ? 1 : percent;
    }

    /// <summary>Eigene HP: als ZAHL, weil das Spiel sie als Zahl anzeigt.
    /// Das Ziel behaelt den Prozentwert (<see cref="TargetHpSentence"/>) - dort
    /// zeigt das Spiel nie eine Zahl an.</summary>
    public static string HpSentence(uint cur, uint max) =>
        IsGerman ? $"HP: {cur} von {max}." : $"HP: {cur} of {max}.";
    public static string TargetHpSentence(uint cur, uint max) =>
        IsGerman ? $"Ziel HP: {Percent(cur, max)} Prozent." : $"Target HP: {Percent(cur, max)} percent.";

    // ── Aktions-Form (ActionShapeService) ───────────────────────────
    //  Der Tooltip nennt die Zahl ("Radius, 5y"), die FORM zeichnet das Spiel nur.
    //  Diese Woerter sind der Text-Ersatz dafuer.

    /// <summary>Kreis. Beim Kreis stimmt das Wort "Radius" des Tooltips.</summary>
    public static string ShapeCircle => IsGerman ? "Kreis" : "circle";

    /// <summary>Kegel, dessen Telegraph-Grafik keinen Winkel nennt. Sagt KEINEN
    /// Winkel statt eines geratenen.</summary>
    public static string ShapeCone => IsGerman ? "Kegel" : "cone";

    /// <summary>Kegel mit dem vollen Winkel aus dem Grafiknamen (gl_fan090 = 90).</summary>
    public static string ShapeConeWithAngle(float fullAngleDeg) =>
        IsGerman ? $"Kegel, {fullAngleDeg:0.#} Grad" : $"cone, {fullAngleDeg:0.#} degrees";

    /// <summary>Linie beziehungsweise Rechteck. Die halbe Breite (XAxisModifier)
    /// ist unbestaetigt und wird deshalb nicht gesprochen.</summary>
    public static string ShapeLine => IsGerman ? "Linie" : "line";

    /// <summary>Wie die Form an die Tooltip-Ansage angehaengt wird.</summary>
    public static string ShapeSuffix(string shape) =>
        IsGerman ? $"Form: {shape}" : $"Shape: {shape}";

    /// <summary>", HP X percent" fragment appended to a target announcement.</summary>
    public static string TargetHpFragment(uint cur, uint max) =>
        IsGerman ? $", HP {Percent(cur, max)} Prozent" : $", HP {Percent(cur, max)} percent";

    /// <summary>", Stufe 12, HP 40 Prozent" - der Anhang fuer eine Zielansage.
    /// Die Stufe kommt aus ICharacter.Level, die HP bleiben prozentual.
    /// Fehlt eines von beidem, faellt genau dieser Teil weg.</summary>
    public static string TargetLevelHpFragment(byte level, uint cur, uint max)
    {
        var lvl = level > 0
            ? (IsGerman ? $", Stufe {level}" : $", level {level}")
            : string.Empty;
        return max > 0 ? lvl + TargetHpFragment(cur, max) : lvl;
    }

    /// <summary>", schon gezaehmt" - der Gegner traegt bereits den Status
    /// "Besaenftigung" (Status 213, Sheet-Text "Zahm und greift nicht mehr an.").
    /// Bei einem Fang-Freibrief ist das der Unterschied zwischen einem Ziel, das
    /// noch zaehlt, und einem, an dem die Emote-Aufforderung nur "ist bereits
    /// zahm" zurueckgibt.</summary>
    public static string AlreadyTamed =>
        IsGerman ? ", schon gezähmt" : ", already tamed";

    /// <summary>", rasend, nicht zähmbar" - an dem Gegner ist ein Besänftigen
    /// misslungen (Status 214 "Aufstachelung", Sheet-Text "Nach misslungener
    /// Besänftigung noch wilder als zuvor."). Das Spiel weist einen weiteren
    /// Versuch mit "ist in Raserei verfallen und lässt sich nicht beruhigen" ab.
    /// Der Grund steht mit dabei, weil "rasend" allein nur einen Zustand nennt
    /// und nicht, was er für den Spieler bedeutet.</summary>
    public static string Agitated =>
        IsGerman ? ", rasend, nicht zähmbar" : ", agitated, cannot be tamed";

    /// <summary>HP als Zahl, MP als Prozentwert und nur wenn die Klasse Mana hat.
    /// MP bleibt prozentual, weil MaxMp seit Patch 5.0 fuer JEDE Klasse auf JEDEM
    /// Level 10000 ist - "MP 10000 von 10000" ist keine Zahl, die ein Spieler
    /// irgendwo sieht; das Partyfenster zeichnet daraus "100.00%".</summary>
    public static string VitalStatus(uint hp, uint hpMax, uint mp, uint mpMax, bool hasMp) =>
        hasMp
            ? (IsGerman ? $"HP {hp} von {hpMax}, MP {Percent(mp, mpMax)} Prozent."
                        : $"HP {hp} of {hpMax}, MP {Percent(mp, mpMax)} percent.")
            : (IsGerman ? $"HP {hp} von {hpMax}." : $"HP {hp} of {hpMax}.");

    /// <summary>" &lt;name&gt;, HP X percent." target clause appended to the status readout.</summary>
    public static string TargetStatusClause(string name, uint cur, uint max) =>
        IsGerman ? $" {name}, HP {Percent(cur, max)} Prozent." : $" {name}, HP {Percent(cur, max)} percent.";

    public static string TargetFallbackName => IsGerman ? "Ziel" : "Target";

    // GP (Sammelpunkte) - the DE client says "SP", the EN client "GP".
    public static string NoGatheringPoints => IsGerman ? "Keine Sammelpunkte. SP gibt es nur als Sammler." : "No gathering points. GP only exists for gatherers.";
    public static string GpValue(uint cur, uint max) =>
        IsGerman ? $"SP {Percent(cur, max)} Prozent." : $"GP {Percent(cur, max)} percent.";

    public static string EnemyCasts(string action) => IsGerman ? $"Gegner wirkt {action}." : $"Enemy casts {action}.";

    /// <summary>Cast warning naming the caster - used when the casting enemy is
    /// NOT the player's current target, so it is clear the danger comes from
    /// somewhere else.</summary>
    public static string NamedEnemyCasts(string enemy, string action) =>
        IsGerman ? $"{enemy} wirkt {action}." : $"{enemy} casts {action}.";

    /// <summary>Cast warning for a spell aimed AT THE PLAYER. Since 2026-08-18 every
    /// cast of the current target is announced, so the plain wording above no longer
    /// implies "at you" - the aimed case has to say so explicitly.</summary>
    public static string EnemyCastsAtYou(string action) =>
        IsGerman ? $"Gegner wirkt {action} auf dich." : $"Enemy casts {action} on you.";

    /// <summary>Cast warning naming the caster AND stating that it is aimed at the
    /// player - the most urgent combination, so both facts are spoken.</summary>
    public static string NamedEnemyCastsAtYou(string enemy, string action) =>
        IsGerman ? $"{enemy} wirkt {action} auf dich." : $"{enemy} casts {action} on you.";
    public static string AnAbility => IsGerman ? "eine Fähigkeit" : "an ability";

    // ── Gefahrenfläche eines Gegner-Casts (Form + Vorwarnung) ────────
    // Wunsch des Users 2026-08-19: cactbot hat für Levelinhalte praktisch keine
    // Trigger, also muss die Ansage selbst sagen, WAS auf den Boden kommt und ob
    // man drinsteht. Form und Grösse stehen im Action-Sheet (CastType/EffectRange),
    // werden also gelesen und nicht geraten - siehe CombatService.DescribeCastShape.
    // Die Form hängt als eigener Satz hinter der Cast-Ansage, statt in sie hinein:
    // "Gegner wirkt X auf dich." + " Kegel, 6 Meter." liest sich sonst als
    // "6 Meter auf dich".

    /// <summary>Form plus Ausdehnung. Das Formwort kommt von ActionShapeService, also
    /// aus derselben Quelle wie im Fähigkeiten-Tooltip ("Kreis", "Kegel, 90 Grad",
    /// "Linie"); hier kommt nur die Zahl dazu, weil im Kampf kein Tooltip danebensteht,
    /// der sie schon genannt hätte.</summary>
    public static string AoeShapeWithRange(string shape, int meters) =>
        IsGerman ? $"{shape}, {meters} Meter" : $"{shape}, {meters} meters";

    /// <summary>Form, die ausdrücklich AUF DEM SPIELER liegt - der Fall, in dem
    /// Weglaufen und nicht Ausweichen die richtige Antwort ist.</summary>
    public static string AoeShapeWithRangeOnYou(string shape, int meters) =>
        IsGerman ? $"{shape} um dich, {meters} Meter" : $"{shape} on you, {meters} meters";

    /// <summary>Der Spieler stand schon beim Beginn des Casts in der Fläche.
    /// Hängt an der Cast-Ansage.</summary>
    public static string AoeStandingInIt(float seconds) =>
        IsGerman ? $"Du stehst drin, {AoeSeconds(seconds)}."
                 : $"You are in it, {AoeSeconds(seconds)}.";

    /// <summary>Der Spieler ist WÄHREND eines laufenden Casts in die Fläche
    /// hineingelaufen. Eigener Satz mit "Achtung", weil hier keine Cast-Ansage
    /// davorsteht, an der man ihn festmachen könnte.</summary>
    public static string AoeEnteredZone(float seconds) =>
        IsGerman ? $"Achtung, du stehst drin, {AoeSeconds(seconds)}."
                 : $"Careful, you are in it, {AoeSeconds(seconds)}.";

    /// <summary>
    /// Wohin man ausweichen kann — die Richtung, die ein sehender Spieler in
    /// einem Blick sieht. Hängt hinter „du stehst drin", weil sie erst dann
    /// gebraucht wird. Richtung und Weite kommen aus derselben Rechnung wie der
    /// Peil-Ton, der danach die Feinausrichtung übernimmt.
    /// </summary>
    public static string EscapeDirection(string direction, string distance) =>
        IsGerman ? $"Raus nach {direction}, {distance}."
                 : $"Escape {direction}, {distance}.";

    /// <summary>
    /// Es gibt keinen erreichbaren sicheren Punkt in Reichweite. MUSS gesagt
    /// werden: der Peil-Ton schweigt in diesem Fall, und Stille ist für einen
    /// blinden Spieler sonst nicht von „ausgerichtet" zu unterscheiden.
    /// </summary>
    public static string EscapeNoneFound =>
        IsGerman ? "Kein sicherer Weg gefunden." : "No safe spot found.";

    /// <summary>Restliche Cast-Zeit als hörbares Zeitbudget. Aufgerundet, damit aus
    /// 0,4 Sekunden nicht "0 Sekunden" wird; unter einer Sekunde bleibt nur noch
    /// "sofort", weil eine Zahl dort nichts mehr nützt.</summary>
    private static string AoeSeconds(float seconds)
    {
        var whole = (int)MathF.Ceiling(seconds);
        if (whole <= 0) return IsGerman ? "sofort" : "now";
        if (whole == 1) return IsGerman ? "1 Sekunde" : "1 second";
        return IsGerman ? $"{whole} Sekunden" : $"{whole} seconds";
    }

    /// <summary>Setzt die Gefahren-Angaben hinter eine fertige Cast-Ansage. Beide
    /// Teile sind eigene Sätze, damit die Sprachausgabe zwischen ihnen atmet und
    /// der wichtigste Teil ("du stehst drin") ganz hinten als Letztes hängen
    /// bleibt.</summary>
    public static string CastWithDanger(string sentence, string shape, string standing)
    {
        if (!string.IsNullOrEmpty(shape))    sentence += $" {shape}.";
        if (!string.IsNullOrEmpty(standing)) sentence += $" {standing}";
        return sentence;
    }

    // ── Sonderaktionen im Auftrag (Duty Actions) ─────────────────────
    // Die Leiste, die manche Auftraege einblenden und die das Spiel NUR per
    // Mausklick anbietet (Tastenbelegungs-Dump 2026-08-09: keine Belegung).

    /// <summary>Ein Platz der Leiste, mit seiner Nummer - die Nummer ist die Taste,
    /// die ihn ausloest.</summary>
    public static string DutyActionSlot(int slot, string action) =>
        IsGerman ? $"{slot}: {action}" : $"{slot}: {action}";

    /// <summary>Die Leiste ist aufgetaucht oder hat sich geaendert. Nennt die Taste
    /// mit, weil sie selten gebraucht wird und man sie sonst genau dann sucht,
    /// wenn keine Zeit dafuer ist.</summary>
    public static string DutyActionsAvailable(string actions, string key) =>
        IsGerman ? $"Sonderaktion verfügbar, {actions}. Taste {key}."
                 : $"Duty action available, {actions}. Key {key}.";

    /// <summary>Es gibt gerade keine Sonderaktionsleiste.</summary>
    public static string NoDutyActions =>
        IsGerman ? "Keine Sonderaktion vorhanden." : "No duty action available.";

    /// <summary>Die Taste zeigt auf einen Platz, den dieser Auftrag nicht belegt.</summary>
    public static string DutyActionSlotEmpty(int slot) =>
        IsGerman ? $"Sonderaktion {slot} ist leer." : $"Duty action {slot} is empty.";

    /// <summary>Das Spiel hat die Ausfuehrung abgelehnt. Bewusst OHNE Grund: den
    /// nennt das Spiel selbst per Fehlermeldung, und den hier zu erfinden waere
    /// eine Behauptung ueber Kampflogik.</summary>
    public static string DutyActionRefused(string action) =>
        IsGerman ? $"{action} geht gerade nicht." : $"{action} not possible right now.";

    // ── Level / Erfahrung ────────────────────────────────────────────
    public static string LevelReached(int level) => IsGerman ? $"Stufe {level} erreicht." : $"Reached level {level}.";
    public static string LevelNotAvailable => IsGerman ? "Stufe nicht verfügbar." : "Level not available.";
    public static string LevelMax(int level) => IsGerman ? $"Stufe {level}, Maximalstufe erreicht." : $"Level {level}, maximum level reached.";
    public static string LevelExpLeft(int level, int left) =>
        IsGerman
            ? $"Stufe {level}. Noch {left} Erfahrungspunkte bis zur nächsten Stufe."
            : $"Level {level}. {left} experience points to the next level.";
    // Live-Ansage bei jedem XP-Gewinn (kurz gehalten, laeuft im Kampf oft).
    public static string XpGained(int amount) =>
        IsGerman ? $"{amount} Erfahrung." : $"{amount} experience.";

    // Ruhebereich (Sichelmond an der EP-Leiste): dort sammelt sich der
    // Erholungsbonus an, auch offline.
    public static string RestedAreaEntered =>
        IsGerman ? "Ruhebereich. Erholungsbonus sammelt sich." : "Rested area. Rested bonus is accumulating.";
    public static string RestedAreaLeft =>
        IsGerman ? "Ruhebereich verlassen." : "Left the rested area.";
    // Zusatz zur Stufen-Ansage (Strg+L). Fuehrendes Leerzeichen, weil die Teile
    // an den Stufen-Satz angehaengt werden.
    public static string RestedAreaNow =>
        IsGerman ? " Im Ruhebereich." : " In a rested area.";
    public static string RestedAreaNot =>
        IsGerman ? " Kein Ruhebereich." : " Not in a rested area.";
    public static string RestedBonusPercent(int percent) =>
        IsGerman
            ? $" Erholungsbonus für {percent} Prozent einer Stufe."
            : $" Rested bonus for {percent} percent of a level.";
    public static string RestedBonusEmpty =>
        IsGerman ? " Kein Erholungsbonus." : " No rested bonus.";
    public static string RestedNotAvailable =>
        IsGerman ? "Erholungsbonus nicht verfügbar." : "Rested bonus not available.";

    // ── Ausruestungsset-Markierung ───────────────────────────────────
    // Das Symbol, das dem sehenden Spieler sagt "steckt in einem gespeicherten
    // Set" - also NICHT verkaufen. Wortwahl wie im Spiel (Addon 756/11993).
    // Kurzform fuer Listen, in denen sie hinter jedem Gegenstand stehen kann.
    public static string InGearsetShort =>
        IsGerman ? ", im Ausrüstungsset" : ", in a gear set";
    // Welche EIGENEN Klassen das Teil tragen koennen - die Frage vorm Verkaufen.
    // Ein- und Mehrzahl getrennt, sonst stolpert die Ansage bei einer Klasse.
    public static string ForYourClasses(string classes, int count) =>
        IsGerman
            ? (count == 1 ? $", für deine Klasse {classes}" : $", für deine Klassen {classes}")
            : (count == 1 ? $", for your {classes}" : $", for your classes {classes}");
    // Langform fuer den einzelnen Gegenstand, wo Platz fuer die Warnung ist.
    public static string InGearsetWarning =>
        IsGerman
            ? " Achtung: in einem Ausrüstungsset gespeichert, nicht verkaufen."
            : " Careful: saved in a gear set, do not sell.";

    // ════════════════════════════════════════════════════════════════
    //  EquipmentService - Ausruestung
    // ════════════════════════════════════════════════════════════════
    public static string HighQuality => IsGerman ? " Hoch-Qualität" : " high quality";
    public static string NoEquipmentWorn => IsGerman ? "Keine Ausrüstung angelegt." : "No equipment worn.";
    public static string SlotsFree(int empty) => IsGerman ? $" {empty} Plätze frei." : $" {empty} slots free.";
    public static string EquipmentList(string parts, string emptyNote) =>
        IsGerman ? $"Ausrüstung: {parts}.{emptyNote}" : $"Equipment: {parts}.{emptyNote}";
    public static string ItemFallback(uint id) => IsGerman ? $"Gegenstand {id}" : $"Item {id}";

    public static string EquipChangeInProgress => IsGerman ? "Ausrüstungswechsel läuft schon." : "Equipment change already in progress.";
    public static string EquipModuleUnavailable => IsGerman ? "Ausrüstungsmodul nicht verfügbar." : "Equipment module not available.";
    public static string ApplyingRecommendedGear => IsGerman ? "Lege empfohlene Ausrüstung an." : "Applying recommended equipment.";
    public static string EquipChangeFailed => IsGerman ? "Ausrüstungswechsel fehlgeschlagen." : "Equipment change failed.";
    public static string EquipChangeDidntWork => IsGerman ? "Ausrüstungswechsel hat nicht geklappt." : "Equipment change did not work.";
    public static string EquipResult(int changed) =>
        changed > 0
            ? (IsGerman ? $"Empfohlene Ausrüstung angelegt, {changed} Teile gewechselt." : $"Recommended equipment applied, {changed} pieces changed.")
            : (IsGerman
                ? "Ausrüstung unverändert. Entweder schon optimal, oder Wechsel gerade nicht möglich."
                : "Equipment unchanged. Either already optimal, or a change is not possible right now.");

    /// <summary>Spoken equipment-slot label (mod wording, not the game's).</summary>
    public static string SlotEquipment  => IsGerman ? "Ausrüstung"   : "Equipment";
    public static string SlotWeapon     => IsGerman ? "Waffe"        : "Weapon";
    public static string SlotOffHand    => IsGerman ? "Nebenhand"    : "Off hand";
    public static string SlotHead       => IsGerman ? "Kopf"         : "Head";
    public static string SlotBody       => IsGerman ? "Rumpf"        : "Body";
    public static string SlotHands      => IsGerman ? "Hände"        : "Hands";
    public static string SlotWaist      => IsGerman ? "Gürtel"       : "Waist";
    public static string SlotLegs       => IsGerman ? "Beine"        : "Legs";
    public static string SlotFeet       => IsGerman ? "Füße"         : "Feet";
    public static string SlotEars       => IsGerman ? "Ohren"        : "Ears";
    public static string SlotNeck       => IsGerman ? "Hals"         : "Neck";
    public static string SlotWrists     => IsGerman ? "Handgelenke"  : "Wrists";
    public static string SlotRing       => IsGerman ? "Ring"         : "Ring";
    public static string SlotSoulCrystal=> IsGerman ? "Jobkristall"  : "Soul Crystal";

    // ════════════════════════════════════════════════════════════════
    //  GearInfoService - Stufe & Tragbarkeit
    // ════════════════════════════════════════════════════════════════
    public static string GearLevel(uint level) => IsGerman ? $"Stufe {level}" : $"Level {level}";
    public static string Wearable(string level) => IsGerman ? $"{level}, tragbar" : $"{level}, wearable";
    public static string NotWearable(string level, string reason) =>
        IsGerman ? $"{level}, nicht tragbar, {reason}" : $"{level}, not wearable, {reason}";
    public static string FromLevel(uint level) => IsGerman ? $"ab Stufe {level}" : $"from level {level}";
    public static string OnlyForClass(string forWho) => IsGerman ? $"nur für {forWho}" : $"only for {forWho}";
    public static string DifferentClassNeeded => IsGerman ? "andere Klasse nötig" : "different class required";
    public static string NotForYourRace => IsGerman ? "nicht für dein Volk" : "not for your race";

    // ── Werte eines Ausrüstungsteils (zum Vergleichen) ──
    // Die Attributnamen selbst kommen aus dem BaseParam-Sheet in Spielsprache
    // und werden NICHT hier übersetzt - sie werden gelesen, nicht erfunden.
    public static string ItemLevelValue(uint level) =>
        IsGerman ? $"Gegenstandsstufe {level}" : $"item level {level}";
    public static string DefensePhysValue(int v) =>
        IsGerman ? $"Verteidigung {v}" : $"defence {v}";
    public static string DefenseMagValue(int v) =>
        IsGerman ? $"Magieabwehr {v}" : $"magic defence {v}";
    public static string DamagePhysValue(int v) =>
        IsGerman ? $"Angriff {v}" : $"physical damage {v}";
    public static string DamageMagValue(int v) =>
        IsGerman ? $"Magieschaden {v}" : $"magic damage {v}";
    /// <summary>Weapon delay, given in seconds (the game stores milliseconds).</summary>
    public static string DelayValue(double seconds) =>
        IsGerman ? $"Verzögerung {seconds:0.0} Sekunden" : $"delay {seconds:0.0} seconds";
    /// <summary>One attribute bonus, e.g. "Stärke plus 4" - name from the sheet.</summary>
    public static string AttributeValue(string name, int v) =>
        IsGerman
            ? $"{name} {(v < 0 ? "minus" : "plus")} {Math.Abs(v)}"
            : $"{name} {(v < 0 ? "minus" : "plus")} {Math.Abs(v)}";
    public static string MateriaSlots(int n) => IsGerman
        ? (n == 1 ? "1 Materia-Slot" : $"{n} Materia-Slots")
        : (n == 1 ? "1 materia slot" : $"{n} materia slots");

    // ════════════════════════════════════════════════════════════════
    //  Plugin.cs - Start, Koordinaten-Lauf, Himmelsrichtung, Hilfe
    // ════════════════════════════════════════════════════════════════
    /// <summary>Startup greeting. <paramref name="version"/> is the raw "5.58"
    /// string; the dots are spoken out per language so the screen reader does
    /// not run the digits together.</summary>
    public static string VersionReady(string version) =>
        IsGerman
            ? $"FF14 Accessibility Version {version.Replace(".", " Punkt ")} bereit."
            : $"FF14 Accessibility version {version.Replace(".", " point ")} ready.";

    // Koordinaten-Lauf (Goto/Copy clipboard coords)
    public static string ClipboardUnreadable =>
        IsGerman ? "Zwischenablage konnte nicht gelesen werden." : "Could not read the clipboard.";
    public static string NoCoordsInClipboard =>
        IsGerman
            ? "Keine Koordinaten in der Zwischenablage gefunden. Erst die Zahlen kopieren, dann die Taste drücken."
            : "No coordinates found on the clipboard. Copy the numbers first, then press the key.";
    public static string MapUnknownConvert =>
        IsGerman ? "Aktuelle Karte unbekannt, kann nicht umrechnen." : "Current map unknown, cannot convert.";
    /// <summary>Walk-target name for a clipboard coordinate (feeds the later
    /// "walking to / arrived at &lt;name&gt;" announcements).</summary>
    public static string CoordsName(float mapX, float mapY) =>
        IsGerman ? $"Koordinaten {mapX:0.0}, {mapY:0.0}" : $"Coordinates {mapX:0.0}, {mapY:0.0}";
    public static string WalkingToCoords(float mapX, float mapY) =>
        IsGerman ? $"Laufe zu Koordinaten {mapX:0.0}, {mapY:0.0}." : $"Walking to coordinates {mapX:0.0}, {mapY:0.0}.";
    public static string PositionUnknown =>
        IsGerman ? "Position unbekannt." : "Position unknown.";
    public static string MapUnknownCoords =>
        IsGerman ? "Aktuelle Karte unbekannt, kann Koordinaten nicht bestimmen." : "Current map unknown, cannot determine coordinates.";
    public static string ClipboardNotWritable =>
        IsGerman ? "Zwischenablage konnte nicht beschrieben werden." : "Could not write to the clipboard.";
    public static string CoordsCopied(float mapX, float mapY) =>
        IsGerman ? $"Koordinaten {mapX:0.0}, {mapY:0.0} kopiert." : $"Coordinates {mapX:0.0}, {mapY:0.0} copied.";

    // Gathering walk-to (shared by /acc gathergo and GatheringService)
    public static string NoGatheringSpotsJob =>
        IsGerman ? "Keine Sammelstellen für deinen Beruf in dieser Zone." : "No gathering spots for your job in this area.";
    public static string GatheringSpotName(int level) =>
        IsGerman ? $"Sammelstelle, Stufe {level}" : $"Gathering spot, level {level}";

    // Himmelsrichtung (compass heading toggle)
    public static string HeadingOn(string direction) =>
        direction.Length > 0
            ? (IsGerman ? $"Himmelsrichtung an. {direction}." : $"Compass heading on. {direction}.")
            : (IsGerman ? "Himmelsrichtung an." : "Compass heading on.");
    public static string HeadingOff =>
        IsGerman ? "Himmelsrichtung aus." : "Compass heading off.";

    /// <summary>Spoken at the start of "/acc soundtest" (audition the cue sounds).</summary>
    public static string SoundTestRunning =>
        IsGerman
            ? "Klangtest: Navigations-Ton von vorn, rechts, hinten, dann Wegpunkt und Ankunft, dann HP- und Mana-Töne."
            : "Sound test: navigation tone from ahead, right, behind, then waypoint and arrival, then HP and mana tones.";

    // Labels spoken before each HP/MP tone in the sound test, so the audition is
    // self-explaining.
    public static string SoundTestHpHeal    => IsGerman ? "HP, Heilung"       : "HP, healing";
    public static string SoundTestHpDamage  => IsGerman ? "HP, Schaden"       : "HP, damage";
    public static string SoundTestHpCritical=> IsGerman ? "HP, kritisch"      : "HP, critical";
    public static string SoundTestMpGain    => IsGerman ? "Mana, Aufladung"   : "Mana, restored";
    public static string SoundTestMpSpend   => IsGerman ? "Mana, Verbrauch"   : "Mana, spent";

    // Gruppen-Heilmonitor: die Nummer sagt WER, die Tonhoehe sagt WIE SCHLIMM.
    /// <summary>Spoken before the heal-monitor audition in "/acc soundtest".</summary>
    public static string SoundTestPartyMonitor =>
        IsGerman
            ? "Heilmonitor: Nummer drei bei vollem Leben, halb, und fast tot."
            : "Heal monitor: number three at full health, half, and nearly dead.";

    /// <summary>Spoken when the heal monitor is switched on.</summary>
    public static string PartyMonitorOn =>
        IsGerman ? "Heilmonitor an" : "Heal monitor on";

    /// <summary>Spoken when the heal monitor is switched off.</summary>
    public static string PartyMonitorOff =>
        IsGerman ? "Heilmonitor aus" : "Heal monitor off";

    /// <summary>Spoken when the monitor is on but the clips are still loading.</summary>
    public static string PartyMonitorPreparing =>
        IsGerman
            ? "Heilmonitor an, Klänge werden noch geladen."
            : "Heal monitor on, sounds are still loading.";

    /// <summary>Spoken for the roster readout when the player is not in a party.</summary>
    public static string PartyMonitorNoParty =>
        IsGerman ? "Keine Gruppe" : "No party";

    /// <summary>Introduces the numbered roster readout.</summary>
    public static string PartyMonitorRosterIntro =>
        IsGerman ? "Gruppe" : "Party";

    /// <summary>
    /// Wird beim Anvisieren eines Gruppenmitglieds angehaengt, solange der
    /// Heilmonitor laeuft: die Nummer, die der Monitor spricht - und zugleich die
    /// Zieltaste. Fuehrendes Komma, weil es immer angehaengt wird.
    /// </summary>
    public static string PartyPositionSuffix(int position) =>
        IsGerman ? $", Gruppe {position}" : $", party {position}";

    // Quest-/Marker-Ziel nicht auflösbar
    public static string QuestInAnotherZoneNoHop(string quest) =>
        IsGerman
            ? $"{quest} ist in einem anderen Gebiet und ich finde keinen Übergang dorthin."
            : $"{quest} is in another area and I can't find a transition there.";
    public static string NoWalkablePointAt(string name) =>
        IsGerman ? $"Kein begehbarer Punkt am {name} gefunden." : $"No walkable point found at {name}.";
    public static string NoWalkablePointNear(string name) =>
        IsGerman ? $"Kein begehbarer Punkt bei {name} gefunden." : $"No walkable point found near {name}.";

    // Bestiarium: nächstes lebendes Exemplar / Lebensraum
    public static string NoMonsterNearby(string monster) =>
        IsGerman ? $"Kein {monster} in der Nähe." : $"No {monster} nearby.";
    public static string NoMonsterNearbyHabitat(string monster, string habitat) =>
        IsGerman ? $"Kein {monster} in der Nähe. Lebt in {habitat}." : $"No {monster} nearby. Lives in {habitat}.";

    /// <summary>Standalone "not targeted" warning (Bestiary walk); the leading-space
    /// variant is <see cref="NotTargetedSuffix"/>.</summary>
    public static string NotTargetedWarning =>
        IsGerman ? "Achtung, nicht anvisiert." : "Warning, not targeted.";

    /// <summary>The full "/acc help" readout: every plugin hotkey and command.
    /// Keys are the current defaults (Page keys, Numpad 3, Plus - kept in sync
    /// with <see cref="Configuration"/>).</summary>
    public static string HelpFull => IsGerman
        ? "Tasten: " +
          "Bild ab, nächstes Objekt ansagen und anvisieren. " +
          "Bild auf, vorheriges Objekt. " +
          "Strg+Bild ab, Kategorie vorwärts. " +
          "Strg+Bild auf, Kategorie zurück. " +
          "Strg+Nummernblock 3, Gehhilfe an oder aus, folgt dem Wegenetz um Hindernisse. " +
          "Nummernblock 3, automatisch zum Ziel laufen. " +
          "Plus, dem anvisierten Ziel folgen an oder aus. " +
          "Strg+Nummernblock 5, Weg zum Ziel ansagen ohne zu laufen. " +
          "F, zum Ziel hindrehen. W, laufen. " +
          "Strg+F1, diese Hilfe. " +
          "Strg+F2, aktives Fenster. " +
          "Strg+F10, Menü vorlesen. " +
          "Strg+F11, Sprache stoppen. " +
          "Strg+Entfernen, HP und MP ansagen. " +
          "Entfernen, HP des anvisierten Ziels ansagen. " +
          "Strg+F9, gewählte Aktionsleiste vorlesen. " +
          "Strg+F6, angelegte Ausrüstung vorlesen. " +
          "Strg+F7, empfohlene Ausrüstung anlegen. " +
          "Strg+F8, zufälliges Aussehen in der Charaktererschaffung. " +
          "Strg+Nummernblock 0, Skill-Menü öffnen: Nummernblock 8 und 2 blättern, Nummernblock 0 wählt, Nummernblock Komma zurück. " +
          "Strg+Umschalt+F6, Spur aufzeichnen an oder aus: eine Stelle, die das Wegenetz nicht kennt, einmal selbst ablaufen. " +
          "Strg+Umschalt+F7, Aufgabenliste des laufenden Inhalts vorlesen: Freibrief, Dungeon oder FATE. " +
          "Befehle: " +
          "/acc nav, Richtung zum Ziel. " +
          "/acc set, Aktuelles Ziel verfolgen. " +
          "/acc clear, Ziel aufheben. " +
          "/acc near, Objekte in der Nähe. " +
          "/acc status, HP und MP ansagen. " +
          "/acc ui, Menü vorlesen. " +
          "/acc win, Aktives Fenster ansagen. " +
          "/acc keys, Spiel-Tastenbelegung auf den Desktop speichern. " +
          "/acc cooldowns, Fähigkeit-bereit-Ansage an oder aus. " +
          "/acc fly, Fliegen beim Auto-Lauf an oder aus, und sagt ob es hier geht. " +
          "/acc trails, aufgezeichnete Spuren in diesem Gebiet auflisten. " +
          "/acc trail del und die Nummer, eine Spur löschen. " +
          "/acc stop, Sprache stoppen."
        : "Keys: " +
          "Page Down, announce and target the next object. " +
          "Page Up, previous object. " +
          "Ctrl+Page Down, next category. " +
          "Ctrl+Page Up, previous category. " +
          "Ctrl+Numpad 3, walk guide on or off, follows the navmesh around obstacles. " +
          "Numpad 3, walk to the target automatically. " +
          "Plus, follow the current target on or off. " +
          "Ctrl+Numpad 5, describe the route to the target without walking. " +
          "F, turn toward the target. W, move forward. " +
          "Ctrl+F1, this help. " +
          "Ctrl+F2, active window. " +
          "Ctrl+F10, read the current menu. " +
          "Ctrl+F11, stop speech. " +
          "Ctrl+Delete, announce HP and MP. " +
          "Delete, announce the current target's HP. " +
          "Ctrl+F9, read the selected hotbar. " +
          "Ctrl+F6, read worn equipment. " +
          "Ctrl+F7, apply recommended equipment. " +
          "Ctrl+F8, random appearance in character creation. " +
          "Ctrl+Numpad 0, open the skill menu: Numpad 8 and 2 to browse, Numpad 0 selects, Numpad decimal to go back. " +
          "Ctrl+Shift+F6, record a trail on or off: walk a stretch the navmesh does not know once yourself. " +
          "Ctrl+Shift+F7, read the task list of whatever is running: levequest, duty or FATE. " +
          "Commands: " +
          "/acc nav, direction to the target. " +
          "/acc set, track the current target. " +
          "/acc clear, clear the target. " +
          "/acc near, nearby objects. " +
          "/acc status, announce HP and MP. " +
          "/acc ui, read the current menu. " +
          "/acc win, announce the active window. " +
          "/acc keys, save the game's key bindings to the desktop. " +
          "/acc cooldowns, ability-ready announcements on or off. " +
          "/acc fly, flying during auto-walk on or off, and whether it works here. " +
          "/acc trails, list the trails recorded in this area. " +
          "/acc trail del and the number, delete a trail. " +
          "/acc stop, stop speech.";

    // ════════════════════════════════════════════════════════════════
    //  AutoWalkService - Auto-Lauf, Ziel folgen, Wegenetz-Aufbau
    // ════════════════════════════════════════════════════════════════
    public static string FollowNoTarget =>
        IsGerman ? "Kein Ziel zum Folgen. Erst ein Ziel anwählen." : "No target to follow. Select a target first.";
    public static string FollowSelf =>
        IsGerman ? "Das bist du selbst." : "That is you.";
    public static string Following(string name) =>
        IsGerman ? $"Folge {name}." : $"Following {name}.";
    public static string FollowStopped =>
        IsGerman ? "Folgen beendet." : "Follow stopped.";
    public static string FollowStoppedZone =>
        IsGerman ? "Folgen beendet, Gebiet gewechselt." : "Follow stopped, zone changed.";
    public static string FollowTargetGone(string name) =>
        IsGerman ? $"{name} ist weg. Folgen beendet." : $"{name} is gone. Follow stopped.";
    public static string FollowAbortedNoResponse =>
        IsGerman ? "Folgen abgebrochen, vnavmesh antwortet nicht." : "Follow aborted, vnavmesh not responding.";
    public static string FollowAbortedUnavailable =>
        IsGerman ? "Folgen abgebrochen, vnavmesh nicht verfügbar." : "Follow aborted, vnavmesh not available.";

    public static string MeshLoading =>
        IsGerman ? "Wegenetz wird geladen." : "Loading navmesh.";
    public static string MeshPercent(int percent) =>
        IsGerman ? $"Wegenetz {percent} Prozent." : $"Navmesh {percent} percent.";
    public static string MeshReady =>
        IsGerman ? "Wegenetz fertig geladen." : "Navmesh loaded.";
    public static string MeshAborted =>
        IsGerman ? "Wegenetz-Aufbau abgebrochen." : "Navmesh build aborted.";
    public static string MeshStillLoading(float percent) =>
        IsGerman ? $"Wegenetz lädt noch, {percent:F0} Prozent. Gleich nochmal versuchen."
                 : $"Navmesh still loading, {percent:F0} percent. Try again shortly.";
    public static string MeshNotReady =>
        IsGerman ? "Wegenetz ist noch nicht bereit. Gleich nochmal versuchen." : "Navmesh is not ready yet. Try again shortly.";
    public static string PathfindBusy =>
        IsGerman ? "Wegfindung läuft schon. Gleich nochmal versuchen." : "Pathfinding is already running. Try again shortly.";
    public static string AutoWalkUnavailable =>
        IsGerman ? "Auto-Lauf nicht verfügbar. Das Plugin vnavmesh fehlt oder ist nicht geladen."
                 : "Auto-walk not available. The vnavmesh plugin is missing or not loaded.";

    public static string WalkingTo(string name) =>
        IsGerman ? $"Laufe zu {name}." : $"Walking to {name}.";

    /// <summary>
    /// Der Lauf startet zu einem Ersatzpunkt, weil das Ziel unter etwas Begehbarem
    /// steht (Bruecke, Unterfuehrung, Keller - siehe
    /// <c>AutoWalkService.TryStepOutFromUnderCeiling</c>). Sagt die Entfernung
    /// gleich mit: sonst klingt der Lauf wie jeder andere, endet aber ein Stueck
    /// vom Ziel entfernt.
    /// </summary>
    public static string WalkingToBelowLedge(string name, float metres) =>
        IsGerman ? $"{name} liegt unter einem Vorsprung. Laufe bis auf {metres:F0} Meter heran."
                 : $"{name} is under an overhang. Walking to within {metres:F0} meters of it.";

    /// <summary>Ankunft am Ersatzpunkt: das Ziel selbst liegt noch die genannte
    /// Strecke in der genannten Himmelsrichtung. Richtung statt links/rechts aus
    /// demselben Grund wie ueberall sonst in der Navigation.</summary>
    public static string ArrivedBelowLedge(string name, float metres, string direction) =>
        IsGerman ? $"Angekommen. {name} ist {MetersRemaining(metres)} nach {direction}, unter dem Vorsprung."
                 : $"Arrived. {name} is {MetersRemaining(metres)} to the {direction}, under the overhang.";

    // ── Fliegen (V5.96) ───────────────────────────────────────────────────────
    // Der Auto-Lauf fliegt, wo das Spiel es zulaesst. Die Ansagen halten den
    // Spieler ueber jeden Wechsel des Fortbewegungsmittels auf dem Laufenden:
    // wer nicht sieht, dass die Figur auf einem Reittier sitzt, muss es hoeren -
    // sonst erklaert nichts, warum die Tasten sich ploetzlich anders anfuehlen.

    /// <summary>Der Lauf wird ein Flug. Gesprochen statt <see cref="WalkingTo"/>,
    /// nicht zusaetzlich - zwei Saetze zum Start waeren eine Ansage zu viel.</summary>
    public static string FlyingTo(string name) =>
        IsGerman ? $"Fliege zu {name}." : $"Flying to {name}.";

    /// <summary>Angekommen und gelandet. Eigener Satz statt
    /// <see cref="TargetReached"/>, weil die Landung mit zum Ergebnis gehoert:
    /// erst am Boden laesst sich reden, sammeln und kaempfen.</summary>
    public static string FlightArrived(string name) =>
        IsGerman ? $"Angekommen und gelandet: {name}." : $"Arrived and landed: {name}.";

    /// <summary>Der Spieler fliegt, aber es kam kein Flugweg zustande. Der Lauf
    /// geht am Boden weiter, und genau das sagt der Satz - Stille waere hier von
    /// einem Abbruch nicht zu unterscheiden.</summary>
    public static string NoFlightPathWalkingInstead =>
        IsGerman ? "Kein Flugweg gefunden, ich laufe."
                 : "No flight route found, walking instead.";

    /// <summary>
    /// Die Suche nach dem Flugweg laeuft noch. Gesprochen erst, wenn sie laenger
    /// dauert als <c>AutoWalkService.FlightSearchNoticeS</c> - bei kurzen Strecken
    /// ist sie in Sekundenbruchteilen durch und der Satz waere nur Laerm.
    ///
    /// <para>WARUM ES DIESEN SATZ BRAUCHT: die Voxel-Suche im Luftraum brauchte
    /// fuer 800 bis 900 Meter gemessene 19 bis 25 Sekunden (Log 2026-09-01,
    /// 21:11 bis 21:13). So lange Stille ist von einem Absturz nicht zu
    /// unterscheiden.</para>
    /// </summary>
    public static string FlightPathSearching =>
        IsGerman ? "Suche den Flugweg."
                 : "Searching for a flight route.";

    /// <summary>Die Notbremse hat gezogen: vnavmesh rechnet nach
    /// <c>AutoWalkService.FlightStartTimeoutS</c> immer noch. Es wird NICHT auf den
    /// Bodenweg ausgewichen - eine laufende Suche laesst sich nicht abbrechen und
    /// wuerde spaeter in den Bodenlauf hineinsteuern.</summary>
    public static string FlightSearchTooSlow =>
        IsGerman ? "Die Suche nach dem Flugweg dauert zu lange. Ich breche ab."
                 : "The flight route search is taking too long. Stopping.";

    /// <summary>Eine Wegsuche laeuft noch, eine zweite nimmt vnavmesh nicht an
    /// (<c>AsyncMoveRequest.MoveTo</c> gibt dann false zurueck). Der Spieler wird
    /// darum gebeten, es gleich noch einmal zu versuchen.</summary>
    public static string PathSearchStillBusy =>
        IsGerman ? "Die Wegsuche läuft noch. Gleich noch einmal versuchen."
                 : "A path search is still running. Try again in a moment.";

    // ── Update über das Menü (V5.97) ──────────────────────────────────────────
    // Bis hierher hiess ein Update: Spiel beenden, Installer starten. Das Plugin
    // kann sich selbst erneuern, weil seine geladene DLL nicht gesperrt ist und
    // Dalamud bei einer Aenderung neu laedt - siehe UpdateService.

    /// <summary>Menuepunkt und Titel der Update-Ebene.</summary>
    public static string UpdateTitle =>
        IsGerman ? "Aktualisierung" : "Update";

    /// <summary>Zeile, die die laufende Fassung nennt.</summary>
    public static string UpdateInstalledVersion(string version) =>
        IsGerman ? $"Installiert: Fassung {version}"
                 : $"Installed: version {version}";

    /// <summary>Menuepunkt: beim Server nachfragen.</summary>
    public static string UpdateCheckNow =>
        IsGerman ? "Nach Aktualisierung suchen" : "Check for update";

    /// <summary>Quittung beim Start der Abfrage.</summary>
    public static string UpdateChecking =>
        IsGerman ? "Suche nach einer neuen Fassung."
                 : "Checking for a new version.";

    /// <summary>Es gibt nichts Neues.</summary>
    public static string UpdateUpToDate(string version) =>
        IsGerman ? $"Fassung {version} ist die neueste."
                 : $"Version {version} is the latest.";

    /// <summary>Es gibt etwas Neues.</summary>
    public static string UpdateAvailable(string version) =>
        IsGerman ? $"Fassung {version} ist verfügbar."
                 : $"Version {version} is available.";

    /// <summary>Menuepunkt: die gefundene Fassung einspielen.</summary>
    public static string UpdateInstallNow(string version) =>
        IsGerman ? $"Fassung {version} jetzt einspielen"
                 : $"Install version {version} now";

    /// <summary>Quittung beim Start des Einspielens.</summary>
    public static string UpdateInstalling(string version) =>
        IsGerman ? $"Lade Fassung {version}. Einen Moment."
                 : $"Downloading version {version}. One moment.";

    /// <summary>
    /// Fertig geschrieben. Das Neuladen macht Dalamud danach von selbst - deshalb
    /// wird hier angekuendigt und nicht gemeldet: die naechste Stimme, die der
    /// Spieler hoert, ist die des frisch geladenen Plugins.
    /// </summary>
    public static string UpdateInstalledRestarting(string version) =>
        IsGerman ? $"Fassung {version} eingespielt. Das Plugin lädt sich jetzt neu."
                 : $"Version {version} installed. The plugin is reloading now.";

    /// <summary>Die Abfrage oder das Einspielen ist gescheitert.</summary>
    public static string UpdateFailed =>
        IsGerman ? "Die Aktualisierung hat nicht geklappt. Einzelheiten stehen im Log."
                 : "The update failed. Details are in the log.";

    /// <summary>
    /// Die neue Fassung aendert Tolk oder die NVDA-Bruecke. Beide haengen im
    /// Spielprozess und lassen sich nicht ersetzen, solange es laeuft.
    /// </summary>
    public static string UpdateNeedsInstaller =>
        IsGerman ? "Diese Aktualisierung ändert Dateien, die das laufende Spiel " +
                   "festhält. Bitte das Spiel beenden und den Installer benutzen."
                 : "This update changes files the running game holds open. " +
                   "Please close the game and use the installer.";

    /// <summary>
    /// Das Plugin stammt aus dem Dalamud-Repository, nicht vom Installer. Dort
    /// gehoeren die Dateien Dalamud, und ein Update von hier aus wuerde seine
    /// Buchfuehrung ueberschreiben.
    /// </summary>
    public static string UpdateNotSelfManaged =>
        IsGerman ? "Diese Installation wird von Dalamud verwaltet und kann sich " +
                   "nicht selbst aktualisieren."
                 : "This installation is managed by Dalamud and cannot update itself.";

    /// <summary>
    /// Im Kampf oder waehrend eines Auto-Laufs wird nicht eingespielt: das
    /// Neuladen reisst mitten in der Bewegung alle Tasten und Ansagen weg.
    /// </summary>
    public static string UpdateBusyNow =>
        IsGerman ? "Nicht im Kampf oder während einer Laufstrecke. " +
                   "Bitte danach noch einmal."
                 : "Not during combat or while a walk is running. " +
                   "Please try again afterwards.";

    /// <summary>
    /// Der Flug hat vor dem Ziel aufgegeben. Bewusst NICHT der Wortlaut des
    /// Bodenlaufs: "hier endet der begehbare Weg" waere hundert Meter ueber dem
    /// Boden schlicht falsch. Nennt Richtung und - wenn nennenswert - die Hoehe,
    /// damit der Spieler nach der Landung weiss, wohin es weitergeht.
    /// </summary>
    public static string FlightEndedRemaining(float distance, string direction, float rise) =>
        MathF.Abs(rise) >= 5f
            ? (IsGerman
                ? $"Flug beendet und gelandet. Noch {MetersRemaining(distance)} nach {direction}, " +
                  $"{(rise > 0 ? "das Ziel liegt höher" : "das Ziel liegt tiefer")}."
                : $"Flight ended and landed. {MetersRemaining(distance)} to the {direction}, " +
                  $"{(rise > 0 ? "the target is higher up" : "the target is further down")}.")
            : (IsGerman
                ? $"Flug beendet und gelandet. Noch {MetersRemaining(distance)} nach {direction}."
                : $"Flight ended and landed. {MetersRemaining(distance)} to the {direction}.");

    /// <summary>
    /// Warum hier nicht geflogen wird. Nur auf Nachfrage gesprochen (siehe
    /// <c>/acc fly</c>) - bei jedem Lauf gesagt waere es in Staedten eine
    /// Dauerschleife.
    /// </summary>
    public static string FlightBlockedReason(FlightBlock reason) => reason switch
    {
        FlightBlock.NoVolume => IsGerman
            ? "Hier kann nicht geflogen werden. In Städten, Dungeons und Innenräumen gibt es keine Flugstrecken."
            : "Flying is not possible here. Cities, dungeons and interiors have no air routes.",
        FlightBlock.NoMount => IsGerman
            ? "Hier sind keine Reittiere erlaubt, also wird auch nicht geflogen."
            : "Mounts are not allowed here, so there is no flying either.",
        FlightBlock.AetherCurrents => IsGerman
            ? "Laut Spielstand fehlen dir hier noch Ätherströme."
            : "According to your progress you are still missing aether currents here.",
        _ => IsGerman ? "Hier gibt es Flugstrecken." : "There are air routes here.",
    };

    /// <summary>Die angeforderte Landung laeuft (<c>/acc land</c>). Gesprochen
    /// beim Start, weil der Sinkflug je nach Hoehe ein paar Sekunden dauert und
    /// ohne Ansage nichts zu passieren scheint.</summary>
    public static string Landing =>
        IsGerman ? "Lande." : "Landing.";

    /// <summary>Unten und abgestiegen.</summary>
    public static string Landed =>
        IsGerman ? "Gelandet." : "Landed.";

    /// <summary>Landen angefordert, ohne auf einem Reittier zu sitzen.</summary>
    public static string NotFlying =>
        IsGerman ? "Du sitzt auf keinem Reittier." : "You are not on a mount.";

    /// <summary>
    /// Zustandsansage des Umschalters (<c>/acc fly</c>). Sagt beim Einschalten
    /// gleich dazu, dass der Spieler selbst aufsitzen und abheben muss - sonst
    /// wartet er auf einen Flug, den das Plugin nie anfaengt.
    /// </summary>
    public static string FlightToggled(bool enabled) =>
        IsGerman
            ? (enabled
                ? "Fliegen beim Auto-Lauf ein. Ruf dein Reittier und heb selbst ab, dann fliegt der Auto-Lauf."
                : "Fliegen beim Auto-Lauf aus. Es wird immer gelaufen.")
            : (enabled
                ? "Flying during auto-walk on. Summon a mount and take off yourself, then auto-walk flies."
                : "Flying during auto-walk off. It will always walk.");

    public static string AutoWalkStopped =>
        IsGerman ? "Auto-Lauf gestoppt." : "Auto-walk stopped.";
    public static string ArrivedNewZone =>
        IsGerman ? "Angekommen, neues Gebiet erreicht." : "Arrived, reached a new area.";
    public static string AutoWalkAbortedNoResponse =>
        IsGerman ? "Auto-Lauf abgebrochen, vnavmesh antwortet nicht." : "Auto-walk aborted, vnavmesh not responding.";

    /// <summary>Distance-remaining fragment: metres, or an "unknown" phrase for NaN.</summary>
    public static string MetersRemaining(float distance) =>
        float.IsNaN(distance)
            ? (IsGerman ? "Ziel unbekannt" : "target unknown")
            : (IsGerman ? $"{distance:F0} Meter" : $"{distance:F0} meters");
    public static string StillToGo(float distance) =>
        IsGerman ? $"Noch {MetersRemaining(distance)}." : $"{MetersRemaining(distance)} remaining.";
    public static string AutoWalkEndedRemaining(float distance) =>
        IsGerman ? $"Auto-Lauf beendet, noch {MetersRemaining(distance)}."
                 : $"Auto-walk ended, {MetersRemaining(distance)} remaining.";
    public static string StuckRemaining(float distance) =>
        IsGerman ? $"Ich stecke fest, noch {MetersRemaining(distance)}. Auto-Lauf beendet."
                 : $"I'm stuck, {MetersRemaining(distance)} remaining. Auto-walk ended.";
    /// <summary>Same, with the culprit named (see <see cref="ObstacleService"/>).
    /// "Ich stecke fest" says nothing about what to do; the blocker does.</summary>
    public static string StuckBehind(string blocker, float distance) =>
        IsGerman ? $"Ich komme nicht weiter, {blocker} steht im Weg. Noch {MetersRemaining(distance)}. Auto-Lauf beendet."
                 : $"I cannot get any further, {blocker} is in the way. {MetersRemaining(distance)} remaining. Auto-walk ended.";
    public static string NoPathTo(string name, string hint) =>
        IsGerman ? $"Kein Weg zu {name} gefunden.{hint}" : $"No path to {name} found.{hint}";
    /// <summary>
    /// Appended to the stuck message in a housing ward. Names the cause AND the
    /// one-line remedy, because neither is the player's doing: the mesh vnavmesh
    /// built on entering the zone predates the houses (see
    /// AutoWalkService.TrailHint for the measurement), and rebuilding it fixes
    /// the walk outright.
    /// </summary>
    /// <summary>
    /// Spoken once on entering a housing ward, when the mesh gets rebuilt so it
    /// knows the houses. Says WHY the wait happens - a build starting by itself
    /// would otherwise be an unexplained ten seconds of progress numbers.
    /// </summary>
    public static string HousingMeshRebuilding => IsGerman
        ? "Wohngebiet. Wegenetz wird neu gebaut, damit die Häuser darin stehen."
        : "Housing ward. Rebuilding the navigation mesh so it includes the houses.";

    public static string HousingFenceHint => IsGerman
        ? " Das Wegenetz ist hier älter als die Häuser. Mit dem Befehl vnav rebuild neu bauen lassen."
        : " The navigation mesh here is older than the houses. Rebuild it with the vnav rebuild command.";
    /// <summary>The walk ran as far as the walkable mesh goes. Says the direction
    /// too, because "still 454 metres" without a bearing leaves the player with
    /// nothing to do next.</summary>
    public static string WalkMeshEndsHere(float distance, string direction) =>
        IsGerman ? $"Weiter komme ich nicht, hier endet der begehbare Weg. Noch {MetersRemaining(distance)} nach {direction}."
                 : $"This is as far as the walkable path goes. {MetersRemaining(distance)} to the {direction}.";

    /// <summary>
    /// Same dead end, but the remaining metres are mostly VERTICAL. Without this
    /// the walk reports "6 metres to the south-west" while the target sits on a
    /// ledge overhead, and the player walks in circles looking for it on their own
    /// level. <paramref name="rise"/> is signed: positive means the target is
    /// above, negative below.
    /// </summary>
    public static string WalkMeshEndsBelowOrAbove(float distance, string direction, float rise)
    {
        var flat = WalkMeshEndsHere(distance, direction);
        var metres = MathF.Abs(rise);
        return IsGerman
            ? $"{flat} Davon {metres:F0} Meter {(rise > 0 ? "nach oben" : "nach unten")} - " +
              $"das Ziel liegt {(rise > 0 ? "ueber" : "unter")} dir."
            : $"{flat} {metres:F0} meters of that {(rise > 0 ? "up" : "down")} - " +
              $"the target is {(rise > 0 ? "above" : "below")} you.";
    }
    /// <summary>Refuses a walk that would not move the character at all.</summary>
    public static string AlreadyAtTarget(string name) =>
        IsGerman ? $"Du bist schon bei {name}." : $"You are already at {name}.";

    /// <summary>The "no path, near &lt;aetheryte&gt;" hint appended to a no-path
    /// announcement (empty when no aetheryte is close). The aetheryte name is
    /// game text; only the frame is translated.</summary>
    public static string NoPathAetheryteHint(string aetheryteName) =>
        IsGerman
            ? $" Das Ziel liegt nahe dem Ätheryt {aetheryteName}. Reise per Aethernet dorthin."
            : $" The destination is near the aetheryte {aetheryteName}. Travel there via the aethernet.";

    // ── Orts-Namen (PlacesService) - der gesprochene Name, NICHT der interne
    //    TypeLabel (der bleibt als Identität deutsch, siehe PlacesService). ──
    /// <summary>Spoken name of the map flag waypoint.</summary>
    public static string FlagName => IsGerman ? "Markierung" : "Flag";
    /// <summary>Spoken name of a zone transition to a named map.</summary>
    public static string TransitionToName(string name) =>
        IsGerman ? $"Übergang nach {name}" : $"Transition to {name}";
    /// <summary>Fallback spoken name for an unnamed aetheryte.</summary>
    public static string AetheryteFallbackName => IsGerman ? "Ätheryt" : "Aetheryte";

    // ════════════════════════════════════════════════════════════════
    //  NavigationService - Gehhilfe (walk guide)
    // ════════════════════════════════════════════════════════════════
    public static string WalkGuideEnded =>
        IsGerman ? "Gehhilfe beendet." : "Walk guide ended.";
    public static string WalkGuideOff =>
        IsGerman ? "Gehhilfe aus." : "Walk guide off.";
    public static string WalkGuideOn(string name) =>
        IsGerman ? $"Gehhilfe an: {name}." : $"Walk guide on: {name}.";

    /// <summary>Gehhilfe startet auf einen Ersatzpunkt, weil das Ziel unter etwas
    /// Begehbarem steht. Eine einzige Zeile - ein zweiter Interrupt gleich danach
    /// wuerde die erste abschneiden.</summary>
    public static string WalkGuideOnBelowLedge(string name, float metres) =>
        IsGerman ? $"Gehhilfe an: {name}. Liegt unter einem Vorsprung, führe bis auf {metres:F0} Meter heran."
                 : $"Walk guide on: {name}. It is under an overhang, guiding to within {metres:F0} meters of it.";
    public static string NoPathStraightLine(string hint) =>
        IsGerman ? $"Kein Weg gefunden, führe in Luftlinie.{hint}" : $"No path found, guiding in a straight line.{hint}";
    // ════════════════════════════════════════════════════════════════
    //  TrailService - selbst abgelaufene Spuren ueber Netzluecken
    // ════════════════════════════════════════════════════════════════
    public static string TrailRecordingStarted => IsGerman
        ? "Spur wird aufgezeichnet. Lauf die Stelle jetzt ab und drueck die Taste am Ende noch einmal."
        : "Recording a trail. Walk the stretch now and press the key again at the end.";
    public static string TrailRecordingCancelledZone => IsGerman
        ? "Spur verworfen, du hast das Gebiet verlassen."
        : "Trail discarded, you left the area.";
    public static string TrailTooShort => IsGerman
        ? "Zu kurz, keine Spur gespeichert."
        : "Too short, no trail saved.";
    public static string TrailSaved(string name, float length) => IsGerman
        ? $"Spur gespeichert: {name}, {MetersRemaining(length)}."
        : $"Trail saved: {name}, {MetersRemaining(length)}.";
    /// <summary>Said out loud, not just logged: a trail that only works downhill
    /// is a promise the plugin cannot keep in reverse, and being stranded on the
    /// far side is exactly what happened in-game on 2026-08-09.</summary>
    public static string TrailOneWayOnly(float drop) => IsGerman
        ? $"Achtung, diese Spur ueberwindet {MetersRemaining(drop)} Hoehe und gilt deshalb nur in Laufrichtung. Fuer den Rueckweg zeichne bitte eine eigene Spur auf."
        : $"Careful: this trail covers {MetersRemaining(drop)} of height, so it only counts in the direction you walked it. Record a separate trail for the way back.";
    public static string TrailDefaultName(int number) => IsGerman
        ? $"Verbindung {number}" : $"Crossing {number}";
    public static string TrailNoneHere => IsGerman
        ? "Keine Spuren in diesem Gebiet." : "No trails in this area.";
    public static string TrailCount(int count) => IsGerman
        ? $"{count} Spuren in diesem Gebiet." : $"{count} trails in this area.";
    public static string TrailListEntry(int number, string name, float length, bool bothWays) => IsGerman
        ? $"{number}: {name}, {MetersRemaining(length)}, {(bothWays ? "in beide Richtungen" : "nur in Laufrichtung")}."
        : $"{number}: {name}, {MetersRemaining(length)}, {(bothWays ? "both ways" : "one way only")}.";
    public static string TrailUnknownNumber => IsGerman
        ? "Diese Nummer gibt es hier nicht." : "No trail with that number here.";
    public static string TrailDeleted(string name) => IsGerman
        ? $"Spur geloescht: {name}." : $"Trail deleted: {name}.";
    public static string TrailCommandHelp => IsGerman
        ? "Sag Schrägstrich acc trails zum Auflisten, oder Schrägstrich acc trail del und die Nummer zum Löschen."
        : "Use slash acc trails to list them, or slash acc trail del and the number to delete one.";
    /// <summary>The auto-walk ran out of mesh and is taking a recorded trail.</summary>
    public static string TrailTaking(string name) => IsGerman
        ? $"Hier endet das Wegenetz, ich nehme {name}."
        : $"The navmesh ends here; taking {name}.";
    public static string TrailFinished => IsGerman
        ? "Spur zu Ende, ich laufe normal weiter."
        : "End of the trail, continuing normally.";
    /// <summary>Crossing a measured gap in the mesh (MeshBridgeService). Named
    /// separately from a recorded trail because the player did not record it and
    /// would otherwise wonder which trail is meant.</summary>
    public static string BridgeCrossing(string name) => IsGerman
        ? $"Das Wegenetz hat hier eine Luecke, ich gehe ueber {name}."
        : $"There is a gap in the navmesh here; crossing at {name}.";
    /// <summary>The push into a zone line achieved nothing. Says what is true -
    /// something is in the way - rather than leaving the player guessing why
    /// nothing happened.</summary>
    public static string TransitionNudgeFailed(string name) => IsGerman
        ? $"Ich komme nicht in {name} hinein, da steht etwas im Weg."
        : $"I cannot get into {name}; something is in the way.";

    /// <summary>Same, but the culprit is known (see <see cref="ObstacleService"/>).
    /// Knowing WHAT blocks decides what to do: another player moves on by
    /// themselves, a barrier never will.</summary>
    public static string TransitionNudgeBlocked(string name, string blocker) => IsGerman
        ? $"Ich komme nicht in {name} hinein, {blocker} steht im Weg."
        : $"I cannot get into {name}; {blocker} is in the way.";

    /// <summary>An obstacle named for its own sake, without a walk around it.</summary>
    public static string BlockedBy(string blocker) => IsGerman
        ? $"{blocker} steht im Weg."
        : $"{blocker} is in the way.";

    /// <summary>A switched-on collision box that pushes the player out. Deliberately
    /// not called a wall: it is invisible and has no model, and the point of saying
    /// it at all is that it will not move - turn around.</summary>
    public static string ObstacleBarrier => IsGerman
        ? "eine unsichtbare Absperrung"
        : "an invisible barrier";

    /// <summary>Scenery carrying collision - a crate, a fence, a gate. The game
    /// keeps no speakable name for these (measured with zone-probe 2026-08-22:
    /// only model and collision file names such as f1t0_a0_taru1.mdl), and
    /// inventing one would be a guess. The abbreviation goes to the log instead.</summary>
    public static string ObstacleScenery => IsGerman
        ? "ein festes Hindernis"
        : "solid scenery";
    /// <summary>vnavmesh threw our fixed point list away and started routing on
    /// its own (OnStuck + RetryOnStuck) - from here on nothing is under our
    /// control, so the walk ends honestly instead of drifting off.</summary>
    public static string TrailLost => IsGerman
        ? "Ich komme auf der Spur nicht durch, Lauf beendet."
        : "I cannot get through on the trail; walk ended.";

    /// <summary>The walk guide ran out of walkable mesh. Unlike the auto-walk
    /// nothing is stopped - the player does the walking - so the line says what
    /// actually changes: guidance continues as the crow flies.</summary>
    public static string GuideMeshEndsHere(float distance, string direction) =>
        IsGerman ? $"Hier endet der begehbare Weg. Noch {MetersRemaining(distance)} nach {direction}, ich führe ab jetzt in Luftlinie."
                 : $"This is where the walkable path ends. {MetersRemaining(distance)} to the {direction}; guiding in a straight line from here.";

    // ════════════════════════════════════════════════════════════════
    //  HotbarService - Aktionsleiste & Skill-Browser
    // ════════════════════════════════════════════════════════════════
    public static string HotbarUnavailable =>
        IsGerman ? "Aktionsleiste nicht verfügbar." : "Hotbar not available.";
    public static string HotbarEmpty(int bar) =>
        IsGerman ? $"Aktionsleiste {bar} ist leer." : $"Hotbar {bar} is empty.";
    public static string HotbarPrefix(int bar) =>
        IsGerman ? $"Aktionsleiste {bar}. " : $"Hotbar {bar}. ";
    /// <summary>Slot label: main bar is "key X", other bars name bar+slot/key.</summary>
    public static string SlotMainKey(string key) =>
        IsGerman ? $"Taste {key}" : $"key {key}";
    public static string SlotBarKey(int bar, string key) =>
        IsGerman ? $"Leiste {bar}, Taste {key}" : $"bar {bar}, key {key}";
    public static string SlotBarSlot(int bar, int slot) =>
        IsGerman ? $"Leiste {bar}, Slot {slot}" : $"bar {bar}, slot {slot}";
    public static string TargetSlotCurrent(string slotLabel, string current) =>
        IsGerman ? $"Ziel-{slotLabel}: {current}" : $"Target {slotLabel}: {current}";
    public static string NoSkillSelected =>
        IsGerman ? "Kein Skill gewählt. Erst mit dem Skill-Browser blättern." : "No skill selected. Browse with the skill browser first.";
    public static string NoTargetSlot =>
        IsGerman ? "Keine Ziel-Taste gewählt. Erst die Ziel-Taste wählen." : "No target slot selected. Select the target slot first.";
    public static string AssignFailed =>
        IsGerman ? "Belegen fehlgeschlagen." : "Assignment failed.";
    public static string SkillAssigned(string name, string slotLabel) =>
        IsGerman ? $"{name} liegt jetzt auf {slotLabel}." : $"{name} is now on {slotLabel}.";
    public static string AssignFailedNoChange =>
        IsGerman ? "Belegen fehlgeschlagen, die Taste hat sich nicht geändert." : "Assignment failed, the key did not change.";
    public static string PlayerDataNotReady =>
        IsGerman ? "Spielerdaten noch nicht bereit." : "Player data not ready yet.";
    public static string NoSkillsFound =>
        IsGerman ? "Keine Skills gefunden." : "No skills found.";
    /// <summary>Bare "slot N" label (no bar), used in the hotbar read-out.</summary>
    public static string SlotNumberWord(int slot) =>
        IsGerman ? $"Slot {slot}" : $"slot {slot}";
    /// <summary>Target-bar summary: how many slots are filled, plus a warning
    /// when the bar has no keys bound.</summary>
    public static string TargetBarSummary(int bar, int filled, int total, bool anyKey) =>
        IsGerman
            ? $"Ziel-Leiste {bar}, {filled} von {total} belegt{(anyKey ? "" : ", keine Tasten zugewiesen")}."
            : $"Target bar {bar}, {filled} of {total} filled{(anyKey ? "" : ", no keys assigned")}.";
    /// <summary>One browsed skill: name, level, where it currently sits (optional)
    /// and its position in the list.</summary>
    public static string SkillBrowseEntry(string name, int level, string? location, int index, int count) =>
        IsGerman
            ? $"{name}, Stufe {level}{(location != null ? $", liegt auf {location}" : "")}, {index} von {count}"
            : $"{name}, level {level}{(location != null ? $", on {location}" : "")}, {index} of {count}";

    /// <summary>One browsed item: name, stack size, quality, where it currently
    /// sits (optional) and its position in the list. The count is spoken because
    /// a stack of one is a different decision than a stack of twenty.</summary>
    public static string ItemBrowseEntry(string name, int quantity, bool isHq, string? location, int index, int count) =>
        IsGerman
            ? $"{name}{(isHq ? HighQuality : "")}, {quantity} Stück{(location != null ? $", liegt auf {location}" : "")}, {index} von {count}"
            : $"{name}{(isHq ? HighQuality : "")}, {quantity}{(location != null ? $", on {location}" : "")}, {index} of {count}";

    // ── Skill-Zuweisungs-Menü (modal, Nummernblock) ──
    /// <summary>Spoken when the modal skill menu opens, with the browse hint.</summary>
    public static string SkillMenuOpened(int count) =>
        IsGerman
            ? $"Skill-Zuweisung, {count} Skills. Nummernblock 8 und 2 blättern, 4 oder 6 wechselt die Liste, Nummernblock 0 wählt, Nummernblock Komma zurück."
            : $"Skill assignment, {count} skills. Numpad 8 and 2 to browse, 4 or 6 switches the list, Numpad 0 selects, Numpad decimal to go back.";

    /// <summary>Spoken when the menu switches to the carried-item list.</summary>
    public static string ItemMenuOpened(int count) =>
        IsGerman
            ? $"Gegenstände, {count} Einträge. Nummernblock 8 und 2 blättern, 4 oder 6 wechselt die Liste, Nummernblock 0 wählt, Nummernblock Komma zurück."
            : $"Items, {count} entries. Numpad 8 and 2 to browse, 4 or 6 switches the list, Numpad 0 selects, Numpad decimal to go back.";

    /// <summary>Spoken when the menu switches to the general-action list
    /// (Absteigen, Reittier-Roulette, Sprint, Teleport ...).</summary>
    public static string GeneralActionMenuOpened(int count) =>
        IsGerman
            ? $"Allgemeine Aktionen, {count} Einträge. Nummernblock 8 und 2 blättern, 4 oder 6 wechselt die Liste, Nummernblock 0 wählt, Nummernblock Komma zurück."
            : $"General actions, {count} entries. Numpad 8 and 2 to browse, 4 or 6 switches the list, Numpad 0 selects, Numpad decimal to go back.";

    /// <summary>Spoken when the menu switches to the mount list.</summary>
    public static string MountMenuOpened(int count) =>
        IsGerman
            ? $"Reittiere, {count} Einträge. Nummernblock 8 und 2 blättern, 4 oder 6 wechselt die Liste, Nummernblock 0 wählt, Nummernblock Komma zurück."
            : $"Mounts, {count} entries. Numpad 8 and 2 to browse, 4 or 6 switches the list, Numpad 0 selects, Numpad decimal to go back.";

    /// <summary>One browsed entry that has nothing but a name: general actions
    /// and mounts. Same shape as the other browse entries so the menu sounds
    /// consistent no matter which list is open.</summary>
    public static string PlainBrowseEntry(string name, string? location, int index, int count) =>
        IsGerman
            ? $"{name}{(location != null ? $", liegt auf {location}" : "")}, {index} von {count}"
            : $"{name}{(location != null ? $", on {location}" : "")}, {index} of {count}";

    /// <summary>Spoken when the menu switches to the quest-item list.</summary>
    public static string QuestItemMenuOpened(int count) =>
        IsGerman
            ? $"Quest-Gegenstände, {count} Einträge. Nummernblock 8 und 2 blättern, 4 oder 6 wechselt die Liste, Nummernblock 0 wählt, Nummernblock Komma zurück."
            : $"Quest items, {count} entries. Numpad 8 and 2 to browse, 4 or 6 switches the list, Numpad 0 selects, Numpad decimal to go back.";

    /// <summary>One browsed quest item: name, how many are left, its cast time
    /// and where it already sits. The cast time matters in a fight - three
    /// seconds of standing still is a decision.</summary>
    public static string QuestItemBrowseEntry(string name, int quantity, byte castTime, string? location, int index, int count) =>
        IsGerman
            ? $"{name}, {quantity} Stück{(castTime > 0 ? $", Wirkzeit {castTime} Sekunden" : "")}{(location != null ? $", liegt auf {location}" : "")}, {index} von {count}"
            : $"{name}, {quantity}{(castTime > 0 ? $", cast time {castTime} seconds" : "")}{(location != null ? $", on {location}" : "")}, {index} of {count}";

    /// <summary>Spoken when stepping the source list finds nothing else with
    /// entries - the player stays where they are.</summary>
    public static string SkillMenuNoOtherSource =>
        IsGerman
            ? "Keine andere Liste verfügbar."
            : "No other list available.";

    /// <summary>Announced when usable quest items arrive. Says what the loot
    /// channel does not: that they DO something, and how to reach them.</summary>
    public static string QuestItemReceived(string joined) =>
        IsGerman
            ? $"Quest-Gegenstand zum Benutzen: {joined}. Mit Strg und Nummernblock 0 auf die Leiste legen."
            : $"Usable quest item: {joined}. Put it on a bar with Ctrl and Numpad 0.";

    // ── Zugang zum Ziel (Aufgangs-Erkennung) ─────────────────────────
    // Wenn das Ziel auf einer Fläche liegt, die im Wegenetz nicht an unserer
    // hängt (Schiffsdeck, Balkon, Empore), läuft der Auto-Lauf sonst stumm
    // gegen nichts. Diese Meldungen sagen stattdessen, WIE NAH man herankommt.

    /// <summary>Approach search: nothing selected to check.</summary>
    public static string ApproachNoTarget =>
        IsGerman
            ? "Kein Ziel gewählt. Erst ein Ziel anvisieren oder im Objekt-Browser auswählen."
            : "No destination selected. Target something first, or pick it in the object browser.";

    /// <summary>Approach search: started (it takes a moment, so say so).</summary>
    public static string ApproachChecking(string target) =>
        IsGerman ? $"Prüfe den Weg zu {target}." : $"Checking the route to {target}.";

    /// <summary>Approach search: a continuous route exists.</summary>
    public static string ApproachReachable(string target, float distance) =>
        IsGerman
            ? $"Zu {target} führt ein durchgehender Weg, {distance:F0} Meter."
            : $"There is a continuous route to {target}, {distance:F0} meters.";

    /// <summary>Approach search: no route, and no reachable spot nearby either.</summary>
    public static string ApproachNone(string target) =>
        IsGerman
            ? $"Zu {target} führt kein Weg, und in der Nähe gibt es keinen erreichbaren Punkt. Der Zugang liegt weiter weg."
            : $"No route to {target}, and no reachable spot nearby either. The way in is further off.";

    /// <summary>Approach search: names the closest reachable spot, how to get
    /// there and how the destination sits relative to it.</summary>
    public static string ApproachFound(string target, float walkDistance, string compass,
                                       float gapDistance, float heightDiff)
    {
        var hoehe = heightDiff switch
        {
            >= 1f => IsGerman ? $", {heightDiff:F0} Meter über dir" : $", {heightDiff:F0} meters above you",
            <= -1f => IsGerman ? $", {-heightDiff:F0} Meter unter dir" : $", {-heightDiff:F0} meters below you",
            _ => string.Empty,
        };
        return IsGerman
            ? $"Kein durchgehender Weg zu {target}. Ich laufe zum nächstmöglichen Punkt, " +
              $"{walkDistance:F0} Meter nach {compass}. Von dort ist das Ziel noch " +
              $"{gapDistance:F0} Meter entfernt{hoehe}."
            : $"No continuous route to {target}. Walking to the closest spot instead, " +
              $"{walkDistance:F0} meters {compass}. From there the destination is " +
              $"{gapDistance:F0} meters away{hoehe}.";
    }

    /// <summary>Destination name for the walk to the near side of a gap.</summary>
    public static string GapCrossSpotName =>
        IsGerman ? "Übergangsstelle" : "crossing point";

    /// <summary>Now crossing a gap the navigation mesh does not cover.</summary>
    public static string GapCrossing =>
        IsGerman
            ? "Übergangsstelle erreicht. Überquere die Lücke."
            : "Crossing point reached. Crossing the gap now.";

    /// <summary>The game's collision module could not be reached.</summary>
    public static string GroundProbeUnavailable =>
        IsGerman
            ? "Die Kollisionsabfrage des Spiels ist nicht erreichbar."
            : "The game's collision query is unavailable.";

    /// <summary>Result of the ground probe: how much floor was found and how
    /// much of it the navigation mesh does not cover.</summary>
    public static string GroundProbeResult(int hits, int withoutMesh) =>
        IsGerman
            ? $"Bodenmessung fertig. {hits} Treffer, davon {withoutMesh} ohne Wegenetz."
            : $"Ground probe done. {hits} hits, {withoutMesh} of them without navigation mesh.";

    /// <summary>The crossing was surveyed for one zone only and we are elsewhere.</summary>
    public static string GapCrossWrongZone =>
        IsGerman
            ? "Diesen Übergang gibt es nur auf den Unteren Decks."
            : "This crossing only exists on the Lower Decks.";

    /// <summary>Neither side of the gap can be walked to from where we stand.</summary>
    public static string GapCrossNoSide =>
        IsGerman
            ? "Von hier aus führt kein Weg zur Übergangsstelle."
            : "No route to the crossing point from here.";

    /// <summary>The walk to the crossing point did not arrive, so no crossing.</summary>
    public static string GapCrossTooFar =>
        IsGerman
            ? "Übergang abgebrochen - die Übergangsstelle wurde nicht erreicht."
            : "Crossing cancelled - the crossing point was not reached.";

    /// <summary>Name for the walk to an approach spot - the walk announcements
    /// must not claim we are heading for the destination itself.</summary>
    public static string ApproachSpotName(string target) =>
        IsGerman ? $"Zugang zu {target}" : $"way in to {target}";

    /// <summary>Name for the walk to the near side of a crossing. Like
    /// <see cref="ApproachSpotName"/> this only ever surfaces in a failure
    /// announcement - a crossing that works stays silent, the same way the
    /// near-miss redirect does.</summary>
    public static string CrossingSpotName(string target) =>
        IsGerman ? $"Übergang zu {target}" : $"crossing to {target}";

    /// <summary>Auto-walk refused to start: the destination hangs on a separate
    /// patch of the navigation mesh, so walking there is impossible.</summary>
    public static string TargetUnreachable(string target) =>
        IsGerman
            ? $"{target} ist nicht erreichbar - dorthin führt kein Weg."
            : $"{target} cannot be reached - no route leads there.";

    // Es gab hier drei Ansagen rund um den Fall "Weg endet kurz vorm Ziel"
    // (Umleitung, Restfahrt, Restweg). Der User hat sie am 2026-08-07 direkt
    // nach dem Bau abgelehnt: "das ist evtl zu viel info, ich werd ja sehen wie
    // weit er vom ziel weg ist". Der Ablauf laeuft jetzt still durch; endet er
    // ohne Ankunft, greift AutoWalkEndedRemaining wie bei jedem anderen Lauf.

    /// <summary>Debug probe: the slot it wants to test is not free.</summary>
    public static string ProbeSlotOccupied =>
        IsGerman
            ? "Sonde braucht Taste 12 der ersten Leiste frei."
            : "Probe needs key 12 on the first bar to be free.";

    /// <summary>Debug probe: finished, results are in the log.</summary>
    public static string ProbeDone =>
        IsGerman ? "Sonde fertig, Ergebnis im Log." : "Probe finished, results in the log.";

    /// <summary>Spoken when the player carries nothing that can go on a bar.</summary>
    public static string NoUsableItems =>
        IsGerman
            ? "Keine benutzbaren Gegenstände in der Tasche."
            : "No usable items in your bag.";
    /// <summary>Spoken after a skill is chosen: now pick the target key.</summary>
    public static string SkillMenuPickTarget(string skillName, int count) =>
        IsGerman
            ? $"{skillName} gewählt. Ziel-Taste wählen, {count} verfügbar. Nummernblock 8 und 2 blättern, Nummernblock 0 belegt, Nummernblock Komma zurück."
            : $"{skillName} selected. Choose a target key, {count} available. Numpad 8 and 2 to browse, Numpad 0 assigns, Numpad decimal to go back.";
    /// <summary>One browsed target key: its label, what is on it now, position in list.</summary>
    public static string SkillMenuTargetEntry(string slotLabel, string current, int index, int count) =>
        IsGerman
            ? $"{slotLabel}, aktuell {current}, {index} von {count}"
            : $"{slotLabel}, currently {current}, {index} of {count}";
    public static string SkillMenuClosed =>
        IsGerman ? "Skill-Menü geschlossen." : "Skill menu closed.";
    public static string SkillMenuNoTargets =>
        IsGerman ? "Keine belegbaren Tasten gefunden." : "No assignable keys found.";

    // ── CooldownService: Fähigkeit wieder bereit ──
    public static string SkillReady(string name) =>
        IsGerman ? $"{name} bereit." : $"{name} ready.";
    public static string SkillChargeReady(string name, uint charges, ushort maxCharges) =>
        IsGerman
            ? $"{name} bereit, {charges} von {maxCharges} Ladungen."
            : $"{name} ready, {charges} of {maxCharges} charges.";
    public static string SkillReadyAnnounceOn =>
        IsGerman ? "Fähigkeit-bereit-Ansage an." : "Ability-ready announcements on.";
    public static string SkillReadyAnnounceOff =>
        IsGerman ? "Fähigkeit-bereit-Ansage aus." : "Ability-ready announcements off.";

    // ── JobGaugeService: Job-Anzeige, etwas ist wieder verfügbar ──
    // Die Namen der Primae sind Eigennamen und in beiden Sprachen gleich; die
    // Sätze drumherum nicht.
    public static string GaugeIfritReady =>
        IsGerman ? "Ifrit bereit" : "Ifrit ready";
    public static string GaugeTitanReady =>
        IsGerman ? "Titan bereit" : "Titan ready";
    public static string GaugeGarudaReady =>
        IsGerman ? "Garuda bereit" : "Garuda ready";
    // Bewusst ohne Stapelzahl: wie das Spiel die Zahl kodiert, ist noch nicht
    // gemessen (siehe JobGaugeService.CollectSummoner).
    public static string GaugeAetherflowReady =>
        IsGerman ? "Ätherfluss bereit" : "Aetherflow ready";
    public static string GaugeAttunementType(byte type) => type switch
    {
        1 => IsGerman ? "Rubin"   : "Ruby",
        2 => IsGerman ? "Topas"   : "Topaz",
        3 => IsGerman ? "Smaragd" : "Emerald",
        _ => IsGerman ? "Einstimmung" : "attunement",
    };
    public static string GaugeAttunement(string type, byte count) =>
        IsGerman ? $"{type} {count}" : $"{type} {count}";
    public static string GaugeNothingReady =>
        IsGerman ? "Nichts bereit." : "Nothing ready.";
    public static string GaugeNoneForJob =>
        IsGerman
            ? "Für diesen Job gibt es keine Anzeige."
            : "This job has no gauge.";

    // ════════════════════════════════════════════════════════════════
    //  EmoteService
    // ════════════════════════════════════════════════════════════════
    public static string NoEmoteSelected =>
        IsGerman ? "Kein Emote gewählt. Erst durchblättern." : "No emote selected. Browse first.";
    public static string EmoteUnavailable =>
        IsGerman ? "Emote nicht verfügbar." : "Emote not available.";
    public static string EmoteFailed =>
        IsGerman ? "Emote fehlgeschlagen." : "Emote failed.";
    public static string EmotesNotReady =>
        IsGerman ? "Emotes noch nicht bereit." : "Emotes not ready yet.";
    public static string NoEmotesAvailable =>
        IsGerman ? "Keine Emotes verfügbar." : "No emotes available.";
    /// <summary>One browsed emote: name, chat command (optional), list position.</summary>
    public static string EmoteBrowseEntry(string name, string command, int index, int count) =>
        IsGerman
            ? $"{name}{(command.Length > 0 ? $", Befehl {command}" : "")}, {index} von {count}"
            : $"{name}{(command.Length > 0 ? $", command {command}" : "")}, {index} of {count}";

    // ════════════════════════════════════════════════════════════════
    //  DalamudPluginsService - Plugin-Liste
    // ════════════════════════════════════════════════════════════════
    public static string NoPluginSelected =>
        IsGerman ? "Kein Plugin gewählt. Erst durchblättern." : "No plugin selected. Browse first.";
    public static string PluginNoSettings(string name) =>
        IsGerman ? $"{name} hat keine Einstellungen." : $"{name} has no settings.";
    public static string PluginSettingsOpened(string name) =>
        IsGerman ? $"Einstellungen von {name} geöffnet. Das Fenster ist nicht vorlesbar."
                 : $"Opened settings of {name}. The window cannot be read aloud.";
    public static string PluginSettingsCantOpen(string name) =>
        IsGerman ? $"Einstellungen von {name} lassen sich nicht öffnen." : $"Cannot open settings of {name}.";
    public static string PluginListUnavailable =>
        IsGerman ? "Plugin-Liste nicht verfügbar." : "Plugin list not available.";
    public static string NoPluginsInstalled =>
        IsGerman ? "Keine Plugins installiert." : "No plugins installed.";
    // Plugin-Zustandswörter (Describe / BuildOverview)
    public static string PluginVersionLabel(string version) =>
        IsGerman ? $"Version {version}" : $"version {version}";
    public static string PluginLoaded    => IsGerman ? "geladen" : "loaded";
    public static string PluginNotLoaded => IsGerman ? "nicht geladen" : "not loaded";
    public static string PluginOutdated  => IsGerman ? "veraltet" : "outdated";
    public static string PluginBanned    => IsGerman ? "gesperrt" : "banned";
    public static string PluginDev       => IsGerman ? "Entwickler-Plugin" : "dev plugin";
    public static string PluginHasConfig => IsGerman ? "hat Einstellungen" : "has settings";
    public static string PluginAllLoaded => IsGerman ? "alle geladen" : "all loaded";
    public static string PluginCountNotLoaded(int n) => IsGerman ? $"{n} nicht geladen" : $"{n} not loaded";
    public static string PluginCountOutdated(int n)  => IsGerman ? $"{n} veraltet" : $"{n} outdated";
    public static string PluginCountBanned(int n)    => IsGerman ? $"{n} gesperrt" : $"{n} banned";
    public static string PluginOverview(int total, string state) =>
        IsGerman ? $"{total} Plugins, {state}." : $"{total} plugins, {state}.";

    // ════════════════════════════════════════════════════════════════
    //  FishingService (spoken parts; the /acc fishobj probe stays German)
    // ════════════════════════════════════════════════════════════════
    public static string FishingSpotsList(int count, string joined) =>
        IsGerman ? $"{count} Angelplätze: {joined}." : $"{count} fishing spots: {joined}.";
    public static string NoFishingSpotNearEnough(string name, float distance) =>
        IsGerman ? $"Kein Angelplatz nah genug. Nächster: {name}, {distance:F0} Meter. Stell dich an die Angelstelle und drück erneut."
                 : $"No fishing spot close enough. Nearest: {name}, {distance:F0} meters. Stand at the fishing spot and press again.";
    public static string MapUnknownCantRemember =>
        IsGerman ? "Aktuelle Karte unbekannt, kann die Stelle nicht merken." : "Current map unknown, cannot remember this spot.";
    public static string FishingSpotRemembered(string name, float mapX, float mapY) =>
        IsGerman ? $"Angelplatz {name} hier gemerkt: Karte {mapX:F1}, {mapY:F1}."
                 : $"Fishing spot {name} remembered here: map {mapX:F1}, {mapY:F1}.";

    // ════════════════════════════════════════════════════════════════
    //  GatheringService
    // ════════════════════════════════════════════════════════════════
    public static string GatheringSpotsList(int count, string joined) =>
        IsGerman ? $"{count} Sammelstellen: {joined}." : $"{count} gathering spots: {joined}.";

    // ════════════════════════════════════════════════════════════════
    //  InventoryService
    // ════════════════════════════════════════════════════════════════
    public static string InventoryEmpty =>
        IsGerman ? "Inventar ist leer." : "Inventory is empty.";
    public static string GilUnavailable =>
        IsGerman ? "Gil-Stand nicht verfügbar." : "Gil amount not available.";
    public static string KeyItemsLabel(string joined) =>
        IsGerman ? $"Schlüsselgegenstände: {joined}" : $"Key items: {joined}";
    public static string BagLabel(int count, string joined) =>
        IsGerman ? $"Tasche, {count} Gegenstände: {joined}" : $"Bag, {count} items: {joined}";
    /// <summary>A stacked item: "&lt;name&gt; times &lt;count&gt;" plus an optional
    /// HQ suffix. Single items are announced by the caller without this frame.</summary>
    public static string ItemStack(string name, int quantity, string hqSuffix) =>
        IsGerman ? $"{name} mal {quantity}{hqSuffix}" : $"{name} times {quantity}{hqSuffix}";
    public static string KeyItemFallback(uint id) =>
        IsGerman ? $"Schlüsselgegenstand {id}" : $"Key item {id}";

    // ════════════════════════════════════════════════════════════════
    //  LootRollService - Beute auswuerfeln (Bedarf / Gier / Passen)
    // ════════════════════════════════════════════════════════════════
    /// <summary>Announced the moment a roll opens.</summary>
    public static string LootRollStarted(string name, int count, string options) =>
        IsGerman
            ? $"Verlosung: {name}{(count > 1 ? $" mal {count}" : "")}. {options}"
            : $"Loot roll: {name}{(count > 1 ? $" times {count}" : "")}. {options}";

    /// <summary>Spoken after the roll window was handed the keyboard focus.</summary>
    public static string LootRollFocused =>
        IsGerman
            ? "Verlosungs-Fenster im Fokus. Mit dem Nummernblock auswählen."
            : "Loot roll window focused. Use the numpad to choose.";

    /// <summary>Spoken when the focus key is pressed without a roll window up.</summary>
    public static string LootRollNoWindow =>
        IsGerman ? "Kein Verlosungs-Fenster offen." : "No loot roll window open.";

    /// <summary>Spoken when the player asks and nothing is being rolled for.</summary>
    public static string LootRollNone =>
        IsGerman ? "Zurzeit wird nichts verlost." : "Nothing is being rolled for.";

    /// <summary>Header of the on-demand readout.</summary>
    public static string LootRollList(int count, string joined) =>
        IsGerman ? $"{count} Verlosungen. {joined}" : $"{count} loot rolls. {joined}";

    /// <summary>One entry of the on-demand readout.</summary>
    public static string LootRollEntry(string name, int count, string options, string ownRoll) =>
        IsGerman
            ? $"{name}{(count > 1 ? $" mal {count}" : "")}, {options}{(ownRoll.Length > 0 ? $", {ownRoll}" : "")}"
            : $"{name}{(count > 1 ? $" times {count}" : "")}, {options}{(ownRoll.Length > 0 ? $", {ownRoll}" : "")}";

    /// <summary>
    /// One row of the roll window while stepping through the list. The gear
    /// block ("Stufe 15, tragbar, Gegenstandsstufe 20, Verteidigung 31") sits
    /// right behind the name, where the game's own tooltip puts it, and is ""
    /// for everything that is not equipment.
    /// </summary>
    public static string LootRollRow(string name, int count, string gear, string options, string remaining) =>
        IsGerman
            ? $"{name}{(count > 1 ? $" mal {count}" : "")}" +
              $"{(gear.Length      > 0 ? $", {gear}"      : "")}" +
              $"{(options.Length  > 0 ? $", {options}"  : "")}" +
              $"{(remaining.Length > 0 ? $", {remaining}" : "")}"
            : $"{name}{(count > 1 ? $" times {count}" : "")}" +
              $"{(gear.Length      > 0 ? $", {gear}"      : "")}" +
              $"{(options.Length  > 0 ? $", {options}"  : "")}" +
              $"{(remaining.Length > 0 ? $", {remaining}" : "")}";

    /// <summary>Seconds left before the roll expires.</summary>
    public static string LootRollRemaining(int seconds) =>
        IsGerman ? $"noch {seconds} Sekunden" : $"{seconds} seconds left";

    /// <summary>What the player may still do - the game's RollState in words.</summary>
    public static string LootOptionsNeedGreedPass =>
        IsGerman ? "Bedarf, Gier oder Passen möglich" : "need, greed or pass";
    public static string LootOptionsGreedPass =>
        IsGerman ? "nur Gier oder Passen möglich" : "greed or pass only";
    public static string LootOptionsPassOnly =>
        IsGerman ? "nur Passen möglich" : "pass only";
    public static string LootOptionsDone =>
        IsGerman ? "schon gewürfelt" : "already rolled";
    public static string LootOptionsUnavailable =>
        IsGerman ? "nicht verfügbar" : "unavailable";

    /// <summary>What the player already did, with the rolled number.</summary>
    public static string LootRolledNeed(byte value) =>
        IsGerman ? $"du hast Bedarf gewürfelt, {value}" : $"you rolled need, {value}";
    public static string LootRolledGreed(byte value) =>
        IsGerman ? $"du hast Gier gewürfelt, {value}" : $"you rolled greed, {value}";
    public static string LootRolledPass =>
        IsGerman ? "du hast gepasst" : "you passed";
    public static string LootRolledWon =>
        IsGerman ? "du hast den Gegenstand erhalten" : "you were awarded the item";

    // ════════════════════════════════════════════════════════════════
    //  MessageHistoryService - Nachlese-Kanäle
    // ════════════════════════════════════════════════════════════════
    // [Chat-Puffer] Fuer das NEUE Chatsystem ist ChatCategoryName entfallen. Dessen
    // Puffer sind keine feste Aufzaehlung des Plugins mehr, sondern die Kanaele und
    // Register des SPIELS, und die tragen ihre Namen selbst: eine LogFilter-Zeile
    // ihren Zeilennamen, ein Register das, was der Spieler dort eingetippt hat. Eine
    // uebersetzte Liste daneben wuerde Dinge umbenennen, die dem Spieler gehoeren.
    // Die drei Puffer, die keine Register sind, stehen in AccessibilityStrings.Chat.cs.
    //
    // Das ALTE Chatsystem hat seine feste Kategorienliste weiterhin, und die braucht
    // ihre Namen - daher LegacyChatCategoryName. Wortgleich zum frueheren
    // ChatCategoryName, damit der Spieler beim Umschalten dieselben Woerter hoert
    // wie vorher.
    public static string LegacyChatCategoryName(LegacyChatHistoryService.Category category) => category switch
    {
        LegacyChatHistoryService.Category.Dialogue     => IsGerman ? "Dialoge"            : "Dialogue",
        LegacyChatHistoryService.Category.Say          => IsGerman ? "Sagen"              : "Say",
        LegacyChatHistoryService.Category.Shout        => IsGerman ? "Rufen"              : "Shout",
        LegacyChatHistoryService.Category.Party        => IsGerman ? "Gruppe"             : "Party",
        LegacyChatHistoryService.Category.Alliance     => IsGerman ? "Allianz"            : "Alliance",
        LegacyChatHistoryService.Category.Tell         => IsGerman ? "Flüstern"           : "Tell",
        LegacyChatHistoryService.Category.FreeCompany  => IsGerman ? "Freie Gesellschaft" : "Free Company",
        LegacyChatHistoryService.Category.System       => IsGerman ? "System"             : "System",
        LegacyChatHistoryService.Category.Loot         => IsGerman ? "Beute"              : "Loot",
        _                                              => category.ToString(),
    };

    // ── Umschalter zwischen altem und neuem Chatsystem ──────────────

    /// <summary>Menu row that switches between the two chat systems.</summary>
    public static string OptChatSystem =>
        IsGerman ? "Chatsystem" : "Chat system";

    /// <summary>The old system's name, as the player knows it from v5.83.</summary>
    public static string ChatSystemLegacyName =>
        IsGerman ? "gewohnt, feste Kanäle" : "classic, fixed channels";

    /// <summary>The PR #5 system's name: buffers follow the game's own tabs.</summary>
    public static string ChatSystemNewName =>
        IsGerman ? "neu, Register des Spiels" : "new, the game's tabs";

    /// <summary>The menu row's label, naming the system in force.</summary>
    public static string OptChatSystemRow(bool legacy) =>
        IsGerman
            ? $"Chatsystem: {(legacy ? ChatSystemLegacyName : ChatSystemNewName)}"
            : $"Chat system: {(legacy ? ChatSystemLegacyName : ChatSystemNewName)}";

    /// <summary>Spoken the moment the switch is flipped. Says that nothing was
    /// lost, because that is the first thing a player wonders about a buffer
    /// they cannot see.</summary>
    public static string ChatSystemSwitched(bool legacy) =>
        IsGerman
            ? $"Chatsystem {(legacy ? ChatSystemLegacyName : ChatSystemNewName)}. Beide Nachlesen laufen mit, es ist nichts verloren."
            : $"Chat system {(legacy ? ChatSystemLegacyName : ChatSystemNewName)}. Both histories keep recording, nothing was lost.";

    /// <summary>Spoken when a key belonging to the OTHER system is pressed.
    /// Silence would read as a broken key - the player cannot see that the key
    /// simply has no counterpart in the system they switched to.</summary>
    public static string ChatKeyOnlyInNewSystem =>
        IsGerman
            ? "Diese Taste gehört zum neuen Chatsystem."
            : "That key belongs to the new chat system.";

    public static string CategoryEmpty(string category) =>
        IsGerman ? $"{category}, leer" : $"{category}, empty";
    public static string CategorySummary(string category, int count) =>
        count == 0
            ? (IsGerman ? $"{category}, leer" : $"{category}, empty")
            : (IsGerman ? $"{category}, {count} {(count == 1 ? "Nachricht" : "Nachrichten")}"
                        : $"{category}, {count} {(count == 1 ? "message" : "messages")}");
    public static string HistoryStart =>
        IsGerman ? "Anfang des Verlaufs." : "Start of history.";
    public static string HistoryEnd =>
        IsGerman ? "Ende des Verlaufs." : "End of history.";

    // ════════════════════════════════════════════════════════════════
    //  ChatReaderService - gesprochene Kanal-Präfixe
    //  (spoken BEFORE a chat line, e.g. "Says from X: ...")
    // ════════════════════════════════════════════════════════════════
    /// <summary>Channel prefix for an incoming chat line ("" = no prefix).</summary>
    public static string ChatPrefix(XivChatType type) => type switch
    {
        XivChatType.Say           => IsGerman ? "Sagt"        : "Says",
        XivChatType.Shout         => IsGerman ? "Ruft"        : "Shouts",
        XivChatType.Party         => IsGerman ? "Gruppe"      : "Party",
        XivChatType.Alliance      => IsGerman ? "Allianz"     : "Alliance",
        XivChatType.TellIncoming  => IsGerman ? "Flüstert"    : "Tells",
        XivChatType.FreeCompany   => IsGerman ? "FC"          : "FC",
        XivChatType.SystemMessage => IsGerman ? "System"      : "System",
        XivChatType.ErrorMessage  => IsGerman ? "Fehler"      : "Error",
        XivChatType.TellOutgoing  => IsGerman ? "Flüstert an" : "Tells",
        XivChatType.Yell          => IsGerman ? "Brüllt"      : "Yells",
        XivChatType.CrossParty    => IsGerman ? "Gruppe"      : "Party",
        XivChatType.Echo          => IsGerman ? "Echo"        : "Echo",
        XivChatType.Gathering     => "",   // full sentence, no channel prefix
        XivChatType.LootNotice    => "",   // full sentence, no channel prefix
        // An NPC speaking needs no channel word - the name in front of the line
        // says everything "Chat von ..." would have said, only shorter.
        XivChatType.NPCDialogue   => "",
        XivChatType.NPCDialogueAnnouncements => "",
        _                         => IsGerman ? "Chat"        : "Chat",
    };

    /// <summary>Prefix for the player's OWN messages ("You say: ...").</summary>
    public static string OwnChatPrefix(XivChatType type) => type switch
    {
        XivChatType.Say          => IsGerman ? "Du sagst"      : "You say",
        XivChatType.Shout        => IsGerman ? "Du rufst"      : "You shout",
        XivChatType.Yell         => IsGerman ? "Du brüllst"    : "You yell",
        XivChatType.Party        => IsGerman ? "Du zur Gruppe" : "You to party",
        XivChatType.CrossParty   => IsGerman ? "Du zur Gruppe" : "You to party",
        XivChatType.Alliance     => IsGerman ? "Du zur Allianz": "You to alliance",
        XivChatType.FreeCompany  => IsGerman ? "Du zur FC"     : "You to FC",
        XivChatType.TellOutgoing => IsGerman ? "Du flüsterst"  : "You tell",
        _                        => IsGerman ? "Du"            : "You",
    };

    /// <summary>Outgoing-tell addressee clause (" to X"), appended after the prefix.</summary>
    public static string ChatAddressee(string name) =>
        IsGerman ? $" an {name}" : $" to {name}";

    /// <summary>A chat line with a named sender: "&lt;prefix&gt; from &lt;sender&gt;: &lt;message&gt;".</summary>
    public static string ChatFromLine(string prefix, string sender, string message) =>
        IsGerman ? $"{prefix} von {sender}: {message}" : $"{prefix} from {sender}: {message}";

    // ════════════════════════════════════════════════════════════════
    //  BeaconService
    // ════════════════════════════════════════════════════════════════
    public static string BeaconUnavailable =>
        IsGerman ? "Ton-Beacon nicht verfügbar." : "Audio beacon not available.";

    // ════════════════════════════════════════════════════════════════
    //  UIReaderService - Restpunkte (Benachrichtigung, Countdown)
    // ════════════════════════════════════════════════════════════════
    /// <summary>Notification popup hint; <paramref name="key"/> is the configured
    /// accept hotkey so it stays correct after a rebind.</summary>
    public static string NotificationAccept(string key) =>
        IsGerman ? $"Benachrichtigung. Mit {key} annehmen." : $"Notification. Press {key} to accept.";
    public static string SecondsToJoin(int seconds) =>
        IsGerman ? $"Noch {seconds} Sekunden zum Beitreten." : $"{seconds} seconds left to join.";

    // ════════════════════════════════════════════════════════════════
    //  Nachzuegler aus dem Sprach-Audit 2026-08-03
    //  Alles hier war noch hart deutsch mitten im Service-Code und wurde
    //  gesprochen. Die englischen Fassungen benennen die Sache, sie sind
    //  KEINE gelesenen Client-Begriffe - wo der englische Client ein
    //  anderes Wort fuehrt, gewinnt spaeter das gelesene Wort.
    // ════════════════════════════════════════════════════════════════

    // ── Sammel-Fenster (Gathering) ──────────────────────────────────
    public static string GatherChance(string percent) =>
        IsGerman ? $"Chance {percent} Prozent" : $"Chance {percent} percent";
    public static string GatherBonus(string percent) =>
        IsGerman ? $"Bonus {percent} Prozent" : $"Bonus {percent} percent";
    public static string GatherRare   => IsGerman ? "rar" : "rare";
    public static string GatherHidden => IsGerman ? "verborgen" : "hidden";
    /// <summary>Remaining uses of a gathering node ("Belastbarkeit 4 von 4").</summary>
    public static string GatherIntegrity(string current, string max) =>
        IsGerman ? $"Belastbarkeit {current} von {max}" : $"Integrity {current} of {max}";

    // ── Handwerker-Notizbuch (RecipeNote) ───────────────────────────
    //  Die Werte selbst (Klasse, "Stufe 5", Zahlen) sind GELESENER Client-Text
    //  und werden unveraendert durchgereicht - hier stehen nur die Bindewoerter.
    /// <summary>Spoken once when the crafting log opens: window plus the class
    /// whose recipes are shown ("Handwerker-Notizbuch, Alchemist, Stufe 5").</summary>
    public static string RecipeNoteOpened(string jobAndLevel) =>
        IsGerman ? $"Handwerker-Notizbuch, {jobAndLevel}"
                 : $"Crafting log, {jobAndLevel}";
    /// <summary>A list row with its position ("Destilliertes Wasser, Stufe 1, 3 von 12").</summary>
    public static string RowWithPosition(string row, int index, int total) =>
        IsGerman ? $"{row}, {index} von {total}" : $"{row}, {index} of {total}";
    /// <summary>Progress needed to finish the craft (client label "Fertig mit").</summary>
    public static string RecipeDifficulty(string value) =>
        IsGerman ? $"Fertig mit {value}" : $"Progress needed {value}";
    /// <summary>Durability the craft starts with (client label "Belastbar bis").</summary>
    public static string RecipeDurability(string value) =>
        IsGerman ? $"Belastbar bis {value}" : $"Durability {value}";
    public static string RecipeMaxQuality(string value) =>
        IsGerman ? $"Qualität maximal {value}" : $"Maximum quality {value}";
    /// <summary>Starting quality granted by HQ materials - only said when it is not zero.</summary>
    public static string RecipeStartQuality(string value) =>
        IsGerman ? $"Startqualität {value}" : $"Starting quality {value}";
    /// <summary>How many can be made from what the player carries.</summary>
    public static string RecipeCraftable(string value) =>
        IsGerman ? $"Herstellbar {value}" : $"Craftable {value}";
    /// <summary>How many of the RESULT item the player already owns.</summary>
    public static string RecipeInBag(string value) =>
        IsGerman ? $"Im Beutel {value}" : $"In bag {value}";
    /// <summary>One material line. NQ and HQ are always both named (user decision
    /// 2026-08-08): HQ material raises starting quality, so a silent zero would
    /// hide a real choice.</summary>
    public static string RecipeMaterial(string name, string needed, string nq, string hq) =>
        IsGerman ? $"{name}, {needed} benötigt, {nq} NQ, {hq} HQ"
                 : $"{name}, {needed} needed, {nq} NQ, {hq} HQ";
    /// <summary>A crystal row. The window shows crystals as icons only - it
    /// carries no name node (ilspycmd 2026-08-08: CrystalNodes has Image but no
    /// Name), so the element stays unnamed rather than guessed.</summary>
    public static string RecipeCrystal(string needed, string owned) =>
        IsGerman ? $"Kristall, {needed} benötigt, {owned} im Beutel"
                 : $"Crystal, {needed} needed, {owned} in bag";
    /// <summary>Said instead of the values when no recipe is selected yet.</summary>
    public static string RecipeNoSelection =>
        IsGerman ? "Kein Rezept ausgewählt." : "No recipe selected.";

    // ── Inventar / Gegenstands-Slots ────────────────────────────────
    /// <summary>An item with its stack count. German needs the "mal" connector,
    /// English just puts the number first.</summary>
    public static string ItemQuantity(string qty, string name) =>
        IsGerman ? $"{qty} mal {name}" : $"{qty} {name}";
    /// <summary>A visible but empty inventory/equipment slot.</summary>
    public static string EmptySlot => IsGerman ? "Leer" : "Empty";

    // ── Listen / Reiter ohne eigene Beschriftung ────────────────────
    /// <summary>Icon-only tab: position alone, no label to announce.</summary>
    public static string TabPositionOnly(int index, int count) =>
        IsGerman ? $"Reiter {index} von {count}." : $"Tab {index} of {count}.";
    public static string EmptyList => IsGerman ? "Leere Liste." : "Empty list.";
    public static string DialogWord => IsGerman ? "Dialog." : "Dialog.";

    // ── Weltenwahl (TitleDCWorldMap) ────────────────────────────────
    public static string DataCenterRegions(string regions) =>
        IsGerman ? $"Datenzentrum wählen. Regionen: {regions}"
                 : $"Choose a data center. Regions: {regions}";

    // ── Gil-Depot (Bank / Gehilfen-Truhe) ───────────────────────────
    public static string BankTitle    => IsGerman ? "Gil-Depot" : "Gil storage";
    public static string BankDeposit  => IsGerman ? "Hinterlegen" : "Deposit";
    public static string BankWithdraw => IsGerman ? "Entnehmen" : "Withdraw";
    public static string BankAmount(string amount) =>
        IsGerman ? $"Betrag {amount}." : $"Amount {amount}.";
    /// <summary>One balance line: who, the balance now, the balance afterwards.</summary>
    public static string BankBalance(string owner, string now, string after) =>
        IsGerman ? $"{owner}: derzeit {now}, danach {after}."
                 : $"{owner}: currently {now}, then {after}.";
    /// <summary>Label of the storage side of the window (the retainer's chest).</summary>
    public static string BankChestOwner(string name) =>
        IsGerman ? $"Truhe {name}" : $"Chest {name}";
    /// <summary>Typing echo: the amount plus the balance it would leave behind.</summary>
    public static string BankAmountWithBalance(string amount, string owner, string after) =>
        IsGerman ? $"Betrag {amount}, {owner} danach {after}."
                 : $"Amount {amount}, {owner} then {after}.";

    // ── Chat-Eingabezeile ───────────────────────────────────────────
    public static string ChatInput => IsGerman ? "Chat-Eingabe" : "Chat input";
    public static string ChatInputWithChannel(string channel) =>
        IsGerman ? $"Chat-Eingabe, {channel}" : $"Chat input, {channel}";

    // ── Quest-Detailfenster ─────────────────────────────────────────
    public static string QuestObjectiveText(string objectives) =>
        IsGerman ? $"Ziel: {objectives}. " : $"Objective: {objectives}. ";

    // ── Bestiarium: Lebensraum ──────────────────────────────────────
    // The habitat clause itself is LivesIn (further up) - one wording for both
    // the list overview and the single-row announcement.
    /// <summary>Connector between the spawn areas of one monster.</summary>
    public static string HabitatJoin => IsGerman ? ", oder " : ", or ";

    // ── Plugin-Liste ────────────────────────────────────────────────
    public static string UnnamedPlugin => IsGerman ? "Unbenanntes Plugin" : "Unnamed plugin";

    // -- Charaktererstellung: Schritt "Aussehen" ---------------------
    // Die Menue-Namen und die Namen der einzelnen Eintraege kommen aus dem
    // spieleigenen Lobby-Sheet, also in der Client-Sprache. Uebersetzt sind hier
    // nur die Bindewoerter.

    /// <summary>Ein Menuepunkt des Aussehen-Schritts. Ein LEERES Label heisst
    /// "dasselbe Menue wie eben" - "Hautfarbe" auf jedem Pfeiltastendruck zu
    /// wiederholen, waehrend man ueber 192 Farbfelder streicht, ist unbenutzbar.
    /// <paramref name="shape"/> ist die mod-eigene Beschreibung des BILDES auf
    /// einem Icon-Eintrag, oder null wo keine geschrieben ist. Sie steht ganz
    /// hinten, hinter der Position: die Position ist das, wonach der Spieler
    /// steuert, und die Beschreibung ist der Teil, den der naechste
    /// Pfeiltastendruck gefahrlos abschneiden darf.</summary>
    public static string CharaMakeOption(string label, string name, int index, int count, string? shape = null)
    {
        var head = string.IsNullOrEmpty(label) ? string.Empty : label + ", ";
        var body = string.IsNullOrEmpty(name) ? string.Empty : name + ", ";
        if (index <= 0)
            return IsGerman ? $"{head}{body}Auswahl unbekannt" : $"{head}{body}selection unknown";
        var text = $"{head}{body}" + Counter(index, count);
        return string.IsNullOrEmpty(shape) ? text : $"{text}, {shape}";
    }

    /// <summary>
    /// EINMAL am Ende der Aussehen-Zusammenfassung gesagt, und nur dann, wenn diese
    /// Zusammenfassung wirklich eine der mod-eigenen Bildbeschreibungen enthielt.
    /// Die Icon-Gitter haben in den Spieldaten weder Namen noch Beschreibung, diese
    /// Worte hat also der Mod geschrieben - und ein blinder Spieler kann Mod-Text
    /// nicht von Spiel-Text unterscheiden, ausser man sagt es ihm. Nicht bei jedem
    /// Pfeiltastendruck: einmal pro Zusammenfassung genuegt, oefter kostet mehr als
    /// es informiert.
    /// </summary>
    public static string CharaMakeAuthoredNote =>
        IsGerman ? "Die Bildbeschreibungen stammen vom Mod, nicht vom Spiel."
                 : "The picture descriptions come from the mod, not from the game.";

    /// <summary>
    /// Was Eintrag 1 eines Typ-0-Form-Menues IST. User: *"every type 1 ... had no
    /// description ... I'm assuming this means unmodified, but the mod needs to say
    /// that it's basically the base value for the face."*
    ///
    /// Genau richtig gelesen, und die Daten sagen dasselbe: ein Typ-0-Eintrag ist ein
    /// Morph-Target auf dem Gesichtsmodell, Eintrag 1 ist das unveraenderte Mesh und
    /// die Eintraege 2..N sind die Formen a..N-1. Eintrag 1 fehlt in der Messtabelle
    /// also KONSTRUKTIONSBEDINGT - es gibt keine Verschiebung zu messen, weil er das
    /// ist, wogegen alles andere gemessen wird.
    ///
    /// Schweigen war der falsche Weg, das zu sagen: jeder andere Eintrag bekommt eine
    /// Beschreibung, ein Eintrag ohne liest sich also als "noch nicht geschrieben"
    /// statt als "das ist die Ausgangsform". Das ist KEINE mod-eigene
    /// Bildbeschreibung, sondern eine Aussage ueber die Daten des Spiels, und deshalb
    /// bewusst nicht von <see cref="CharaMakeAuthoredNote"/> abgedeckt.
    /// </summary>
    public static string CharaMakeShapeBase =>
        IsGerman ? "unverändert" : "unmodified";

    /// <summary>Welches Auge eine Farbe betrifft - nur gesprochen, wenn die beiden
    /// sich unterscheiden. Das Spiel hat dafuer keinen eigenen Text: das Lobby-Sheet
    /// benennt den Schalter "Odd Eyes" (Zeile 2125), aber nie die beiden Haelften des
    /// Fensters. Die Worte sind also mod-eigen, und das ist richtig - einem sehenden
    /// Spieler wird die Trennung ausschliesslich dadurch vermittelt, in welche
    /// Haelfte des Fensters er schaut.</summary>
    public static string EyeLeft  => IsGerman ? "linkes Auge" : "left eye";

    /// <summary>Siehe <see cref="EyeLeft"/>.</summary>
    public static string EyeRight => IsGerman ? "rechtes Auge" : "right eye";

    /// <summary>Eine Farbe OHNE Label und OHNE Position, fuer den Fall dass der
    /// Fokus-Leser die Position schon gesagt hat und dies dahinter eingereiht wird.
    /// Das Gegenstueck zur reinen Bildbeschreibung bei den Icon-Gittern.</summary>
    public static string CharaMakeColourOnly(string colour, int group, int shade, string eye)
    {
        var head = string.IsNullOrEmpty(eye) ? string.Empty : eye + ", ";
        return IsGerman
            ? $"{head}{colour}, Gruppe {group} Ton {shade}"
            : $"{head}{colour}, group {group} shade {shade}";
    }

    /// <summary>Ein Farbmenue, dessen SCHALTER AUS ist - die Lippenfarbe ohne
    /// aufgetragenen Lippenstift. Keine Farbe, keine Position: auf dem Gesicht ist
    /// nichts zu beschreiben, und eine Feldnummer wuerde den Spieler glauben lassen,
    /// dass doch etwas da ist. <paramref name="state"/> ist das WORT DES SPIELS
    /// dafuer (eine Lobby-Zeile, an der Aufrufstelle gelesen), dieser Satz braucht
    /// also keine eigene Uebersetzung.</summary>
    public static string CharaMakeColourOff(string label, string state)
        => string.IsNullOrEmpty(label) ? state : $"{label}, {state}";

    /// <summary>Ein Farbfeld. Das Farbwort steht mit Absicht vorne: es ist der Teil,
    /// der "wie sieht mein Charakter aus" beantwortet, und der Teil, den ein Spieler
    /// beim Durchstreichen des Gitters braucht, bevor die naechste Ansage ihn
    /// unterbricht. Gruppe und Ton beschreiben den Aufbau der Palette selbst - jede
    /// Palette in human.cmp besteht aus Rampen zu acht Toenen.</summary>
    public static string CharaMakeColour(string label, string? colour, int index, int count, int group, int shade)
    {
        var head = string.IsNullOrEmpty(label) ? string.Empty : label + ", ";
        // Kein Farbwort heisst: die Position ist alles, was die Zeile hat.
        if (colour == null)
            return head + Counter(index, count);
        var pos = $"{Counter(index, count)}, ";
        return IsGerman
            ? $"{head}{colour}, {pos}Gruppe {group} Ton {shade}"
            : $"{head}{colour}, {pos}group {group} shade {shade}";
    }

    /// <summary>Ein 0-100-Schieberegler. Die beiden Endbezeichnungen sind die Worte
    /// des Spiels fuer die Extreme ("Klein"/"Gross"), und die sind es, die einer
    /// nackten Zahl ueberhaupt Bedeutung geben.</summary>
    public static string CharaMakeSlider(string label, int value, string low, string high)
    {
        // Die Endbezeichnungen fallen weg, sobald der Spieler in EINEM Regler
        // arbeitet: "Klein bis Gross" braucht man einmal, nicht bei jedem Schritt.
        if (string.IsNullOrEmpty(low) || string.IsNullOrEmpty(high))
            return IsGerman ? $"{label}, {value} von 100" : $"{label}, {value} of 100";
        return IsGerman
            ? $"{label}, {value} von 100, {low} bis {high}"
            : $"{label}, {value} of 100, {low} to {high}";
    }

    /// <summary>Ein Schalter der Gesichtsmerkmals-Bitmaske. Die Zahl ist das BIT,
    /// nicht eine Menueposition: das Sheet sagt nicht, welches Bit zu welchem
    /// Typ-4-Menue gehoert, also wird nichts zugeschrieben, was sich nicht belegen
    /// laesst.</summary>
    public static string CharaMakeFeatureBit(string label, int number, bool on) =>
        IsGerman ? $"{label} {number}, {(on ? "an" : "aus")}"
                 : $"{label} {number}, {(on ? "on" : "off")}";

    /// <summary>Derselbe Schalter, aber mit NAMEN statt Nummer. User: *"facial
    /// features have no descriptions, and neither do tattoos. those need descriptions
    /// so the player knows what they are toggling off and on."* Erreichbar, seit die
    /// Zuordnung Reihe-zu-Bit im Spiel gemessen wurde: das 5-Eintraege-Menue
    /// Gesichtsmerkmale, Eintrag "1 von 5", kippte Bit 0 - also Reihe i = Bit
    /// i-1.</summary>
    public static string CharaMakeFeatureNamed(string label, int number, string what, bool on) =>
        IsGerman ? $"{label} {number}, {what}, {(on ? "an" : "aus")}"
                 : $"{label} {number}, {what}, {(on ? "on" : "off")}";

    /// <summary>Eine Typ-4-Reihe beim MARKIEREN: was sie ist und ob sie gerade an
    /// ist. Die Position spricht der Fokus-Leser bereits, hier stehen nur die beiden
    /// Teile, die er nicht wissen kann.</summary>
    public static string CharaMakeFeatureRow(string what, bool on) =>
        IsGerman ? $"{what}, {(on ? "an" : "aus")}" : $"{what}, {(on ? "on" : "off")}";

    /// <summary>Nur der Zustand, fuer ein Merkmal ohne geschriebene Beschreibung -
    /// "aus" ist auch dann die Ansage wert, wenn sich die Sache nicht benennen
    /// laesst.</summary>
    public static string CharaMakeFeatureState(bool on) =>
        IsGerman ? (on ? "an" : "aus") : (on ? "on" : "off");

    public static string CharaMakeFeatureLabel => IsGerman ? "Merkmal" : "Feature";

    /// <summary>Eine Aussehen-Kategorie, deren aktueller Wert sich nicht als EINE
    /// Position benennen laesst - die Typ-4-Bitmasken-Menues, bei denen das Sheet
    /// nicht sagt, welches Bit zu welchem Menue gehoert. Die Anzahl ist das, was sich
    /// ehrlich sagen laesst, und der User hat genau danach gefragt: *"the total number
    /// of selections per value is useful information to have"*.</summary>
    public static string CharaMakeCategory(string label, int count) =>
        IsGerman ? $"{label}, {count} Einträge" : $"{label}, {count} entries";

    /// <summary>
    /// Die ZWEITE Achse des Stimmen-Waehlers - die Hoerprobe, die das Spiel abspielt,
    /// damit Stimmen vergleichbar sind (User: *"there are categories like laugh,
    /// grunt, thinking etc"*). NUR POSITION, solange das Spiel keinen Namen liefert:
    /// die sieben Knoepfe sind reine Icon-Radiobuttons ohne Textknoten irgendwo im
    /// Fenster, und die beiden Sheets, die nach den Namen aussahen, enthalten sie
    /// nicht. Eine erfundene Liste waere eine selbstbewusste Luege darueber, was der
    /// Spieler gerade hoert.
    ///
    /// Die Position ist hier NICHT entbehrlich, auch wenn das Kopfwort "Hoerprobe"
    /// die Art der Zeile schon nennt: am User gemessen sagten mit abgeschalteten
    /// Positionen alle sieben Zeilen nur das eine Wort, und der Wechsel zwischen
    /// ihnen war nicht hoerbar.
    /// </summary>
    public static string CharaMakeVoiceSample(string name, int index, int count)
    {
        var head = IsGerman ? "Hörprobe" : "Sample";
        var lead = string.IsNullOrEmpty(name) ? head : $"{head}, {name}";
        return $"{lead}, {Counter(index, count)}";
    }

    /// <summary>Die Klasse, die die Erstellungs-Vorschau gerade zeigt. Nur der Name -
    /// die BESCHREIBUNG der Klasse liegt wie bei jedem anderen Erstellungsschritt auf
    /// der Vorlese-Taste.</summary>
    public static string CharaMakeClass(string name) => name;

    /// <summary>Ersatzbezeichnung fuer den Stimmen-Waehler. Normal wird das
    /// Lobby-Label des Spiels benutzt; das hier deckt eine Zeile ohne Stimmen-Menue
    /// ab.</summary>
    public static string CharaMakeVoiceLabel => IsGerman ? "Stimme" : "Voice";

    /// <summary>
    /// Markiert in der Aussehen-Zusammenfassung einen Wert, den das SPIEL selbst
    /// geaendert hat, nicht der Spieler. Die Charaktererstellung bildet Werte wirklich
    /// menueuebergreifend um - Hrothgar-Gesicht 1 zu waehlen verschiebt das
    /// Frisur-Byte mit - und genau so etwas kann ein blinder Spieler sonst nicht
    /// bemerken. Bewusst NICHT in dem Moment gesprochen, in dem es passiert: das
    /// wuerde die Position unterbrechen, nach der der Spieler gerade steuert, und
    /// zwar ueber ein Menue, in dem er gar nicht ist.
    /// </summary>
    public static string CharaMakeChangedByGame =>
        IsGerman ? "vom Spiel geändert" : "changed by the game";

    /// <summary>Gesagt, wenn das Aussehen nicht gelesen werden kann, weil das
    /// Vorschau-Modell nicht eindeutig ist. Niemals Schweigen: der Spieler koennte
    /// das nicht von "nichts zu melden" unterscheiden.</summary>
    public static string CharaMakeNoPreview =>
        IsGerman ? "Vorschau-Modell nicht eindeutig, Aussehen nicht lesbar."
                 : "Preview model not identifiable, appearance cannot be read.";

    // ── Tiefes Gewoelbe ─────────────────────────────────────────────
    //
    // Jeder NAME und jede BESCHREIBUNG, die im Gewoelbe gesprochen wird, ist die des
    // Spiels und kommt aus dessen eigenen Sheets. Die Woerter hier sind nur der Rahmen
    // darum: welche Art von Wirkung eine Zeile ist, wo ein Platz sitzt, ob er leer ist.

    /// <summary>Ein ebenenweiter Zustand (DeepDungeonStatus).</summary>
    public static string DeepKindFloor => IsGerman ? "Ebene" : "Floor";

    /// <summary>Ein ebenenweites Verbot (DeepDungeonBan).</summary>
    public static string DeepKindBan => IsGerman ? "Verbot" : "Restriction";

    /// <summary>Eine ebenenweite Gefahr (DeepDungeonDanger).</summary>
    public static string DeepKindDanger => IsGerman ? "Gefahr" : "Hazard";

    /// <summary>Pilgerpfad: die auf dieser Ebene laufende Besonderheit.</summary>
    public static string DeepKindGimmick => IsGerman ? "Diese Ebene" : "This floor";

    /// <summary>Pilgerpfad: die fuer die naechste Ebene vorgemerkte Besonderheit.</summary>
    public static string DeepKindGimmickNext => IsGerman ? "Nächste Ebene" : "Next floor";

    /// <summary>Eine laufende Pomander-Wirkung.</summary>
    public static string DeepKindItemEffect => IsGerman ? "Gegenstand" : "Item effect";

    /// <summary>Eine Zeile der Gewoelbe-Wirkungen: woher sie kommt und wie das Spiel sie
    /// nennt. Nur der NAME, nicht die Beschreibung - diese Zeilen haengen an der
    /// Ebenen-Taste, und die soll eine Antwort geben und keinen Vortrag halten. Die
    /// vollstaendige Beschreibung steht im Fenster Charakterinfo an dem Platz, zu dem sie
    /// gehoert.</summary>
    public static string DeepEffectRow(string kind, string name) => $"{kind}: {name}";

    /// <summary>
    /// Welches Gewoelbe und welche Ebene davon. Beide Hauptwoerter gehoeren dem SPIEL -
    /// der Name des Gewoelbes und das Wort, mit dem der Ergebnisschirm die Zahl
    /// beschriftet - hier kommt nur die Wortstellung dazu.
    /// </summary>
    public static string DeepFloorLine(string dungeon, string floorWord, int floor)
    {
        var number = floorWord.Length > 0 ? $"{floorWord} {floor}" : floor.ToString();
        return dungeon.Length > 0 ? $"{dungeon}, {number}" : number;
    }

    /// <summary>
    /// Gesagt, wenn die Ebenen-Taste ausserhalb eines Tiefen Gewoelbes gedrueckt wird.
    ///
    /// Sie ANTWORTET, statt still zu bleiben: Stille ist die eine Reaktion, die ein
    /// blinder Spieler nicht von einer kaputten Taste unterscheiden kann, und diese
    /// Taste wird aus Gewohnheit gedrueckt, sobald ein Lauf vorbei ist.
    /// </summary>
    public static string DeepFloorOutside =>
        IsGerman ? "Kein Tiefes Gewölbe." : "Not in a deep dungeon.";

    /// <summary>Kategorie-Bezeichnung fuer die Raumliste im Tiefen Gewoelbe.</summary>
    public static string DeepCategoryRooms => IsGerman ? "Räume" : "Rooms";

    /// <summary>Kategorie-Bezeichnung fuer die Truhen einer Ebene.</summary>
    public static string DeepCategoryTreasure => IsGerman ? "Schätze" : "Treasure";

    /// <summary>Kategorie-Bezeichnung fuer die beiden Leuchten.</summary>
    public static string DeepCategoryCairns => IsGerman ? "Leuchten" : "Cairns";

    /// <summary>Ein Raum, nach dem spieleigenen Index dafuer.</summary>
    public static string DeepRoomName(int index) => IsGerman ? $"Raum {index}" : $"Room {index}";

    /// <summary>Markiert den Raum, in dem der Spieler steht.</summary>
    public static string DeepRoomYouAreHere => IsGerman ? "hier" : "you are here";

    /// <summary>Markiert den Startraum der Ebene (RoomFlags.Home).</summary>
    public static string DeepRoomStart => IsGerman ? "Startraum" : "starting room";

    /// <summary>Wie viele Truhen der Director in einen Raum legt, im spieleigenen Wort
    /// fuer eine Truhe.</summary>
    public static string DeepRoomCoffers(int count, string cofferWord) =>
        count == 1 ? $"1 {cofferWord}" : $"{count} {cofferWord}";

    /// <summary>Wohin ein Raum sich oeffnet, aus seinen eigenen Verbindungs-Flags.</summary>
    public static string DeepRoomExits(System.Collections.Generic.IEnumerable<string> directions) =>
        (IsGerman ? "Ausgänge " : "exits ") + string.Join(", ", directions);

    public static string DirNorth => IsGerman ? "Norden" : "north";
    public static string DirEast  => IsGerman ? "Osten"  : "east";
    public static string DirSouth => IsGerman ? "Süden"  : "south";
    public static string DirWest  => IsGerman ? "Westen" : "west";

    /// <summary>
    /// Ein aufgedeckter Raum, fuer den es keinen begehbaren Punkt gibt.
    ///
    /// DIE FORMULIERUNG IST DIE KORREKTUR. Hier stand "noch nicht betreten", und das ist
    /// eine Behauptung ueber den SPIELER, die falsch sein kann - er kann laengst
    /// durchgelaufen sein, waehrend das Plugin neu geladen wurde. Was das Plugin
    /// tatsaechlich weiss, ist enger und betrifft es SELBST: der Director gibt Raeumen
    /// keine Koordinaten, der einzige begehbare Punkt ist also einer, auf dem es den
    /// Spieler stehen gesehen hat.
    /// </summary>
    public static string DeepRoomNoRoute => IsGerman ? "kein Weg bekannt" : "no route known";

    /// <summary>Ein Teil der Ebene, den das Spiel nicht als aufgedeckt fuehrt. Traegt ein
    /// Ziel und NICHTS darueber, was darin ist.</summary>
    public static string DeepRoomUnexplored(int index) =>
        IsGerman ? $"Unerforscht {index}" : $"Unexplored {index}";

    /// <summary>Gesagt, wenn die Raumliste abgefragt wird und die Ebene keine hergibt -
    /// ausserhalb eines Gewoelbes, oder solange den Raumdaten nicht zu trauen ist.</summary>
    public static string DeepNoRooms =>
        IsGerman ? "Keine Raumdaten." : "No room data.";

    /// <summary>
    /// Wo ein Platz in seinem Abschnitt sitzt ("3 von 16"), oder "", wenn der Spieler
    /// Listenpositionen abgeschaltet hat - der Aufrufer muss dann auch das Trennzeichen
    /// weglassen, sonst endet die Zeile in einem haengenden Komma.
    /// </summary>
    public static string DeepSlotPosition(int index, int count) => Counter(index, count);

    /// <summary>
    /// Eine Aetherpool-Zeile mit der Staerke, die das Fenster nur im Symbol zeichnet.
    /// Der NAME ist der des Spiels, und die Form "+N" ebenfalls - dessen eigene
    /// Chat-Zeile lautet *"Deine Aetherpool-Waffe flackert. Ihre Stärke ist jetzt +5."*
    /// </summary>
    public static string DeepGearStrength(string name, int strength) => $"{name} +{strength}";

    /// <summary>Ein Platz, den dieses Gewoelbe gar nicht benutzt.</summary>
    public static string DeepSlotEmpty => IsGerman ? "leerer Platz" : "empty slot";

    /// <summary>
    /// Ein Platz, den das Gewoelbe SEHR WOHL benutzt, mit der Anzahl im Besitz -
    /// einschliesslich keiner. Null ist hier eine echte Antwort und kein Grund zu
    /// schweigen: das Fenster zeichnet das Symbol ausgegraut, ein sehender Spieler sieht
    /// also, WOFUER der Platz ist, bevor er einen besitzt.
    /// </summary>
    public static string DeepSlotCount(string name, int count) =>
        IsGerman ? $"{name} mal {count}" : $"{name} times {count}";

    /// <summary>Ein Pomander, dessen Wirkung gerade laeuft.</summary>
    public static string DeepEffectActive => IsGerman ? "aktiv" : "active";

    /// <summary>Ein Pomander, dessen Wirkung nicht laeuft.</summary>
    public static string DeepEffectInactive => IsGerman ? "nicht aktiv" : "not active";

    /// <summary>Ein Pomander, den das Spiel gerade verweigert (Items[i].IsUsable false).</summary>
    public static string DeepItemUnusable => IsGerman ? "nicht verwendbar" : "not usable";
}
