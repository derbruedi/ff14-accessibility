using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Die Sprachbausteine für die Chat-Puffer und für das Einstellungsmenü.
///
/// Bewusst eine eigene Datei und deshalb <c>partial</c>: die Hauptdatei ist groß und
/// ändert sich in fast jeder Version, und diese Erweiterung soll sich in einem Stück
/// wieder entfernen lassen. In <c>AccessibilityStrings.cs</c> steht dafür nur ein
/// einziges zusätzliches Wort - <c>partial</c>.
///
/// Gleiches Muster wie dort: eine Sprache pro Zeile, umgeschaltet über
/// <see cref="Loc.IsGerman"/>, damit "/acc lang" auch hier greift.
/// </summary>
public static partial class AccessibilityStrings
{
    // ── Menü-Rahmen (SpokenMenu) ──────────────────────────────────

    /// <summary>Kopfzeile beim Öffnen eines Menüs - der Titel und sonst nichts. Die
    /// Anzahl der Einträge steht nicht dabei: die unmittelbar folgende Zeile trägt
    /// ihre eigene Position ("1 von 15"), siehe <see cref="MenuEntry"/>.</summary>
    public static string MenuOpened(string title) => $"{title}.";

    /// <summary>Eine Menüzeile mit ihrer Position.</summary>
    public static string MenuEntry(string label, int index, int count) =>
        RowWithPosition(label, index, count);

    public static string MenuClosed => IsGerman ? "Menü geschlossen." : "Menu closed.";

    public static string MenuEmpty => IsGerman ? "Keine Einträge." : "No entries.";

    // ── Einstellungsmenü ──────────────────────────────────────────

    public static string OptionsTitle => IsGerman ? "Einstellungen" : "Settings";
    public static string OptionsSounds => IsGerman ? "Töne" : "Sounds";
    public static string OptionsAnnouncements => IsGerman ? "Ansagen" : "Announcements";

    /// <summary>Eine An/Aus-Zeile: "Kartenmarkierung, an".</summary>
    public static string OptionToggle(string name, bool on) =>
        IsGerman ? $"{name}, {(on ? "an" : "aus")}" : $"{name}, {(on ? "on" : "off")}";

    /// <summary>Wird im Moment des Umschaltens gesprochen. <c>Rebuild</c> frischt nur
    /// die Beschriftung auf und liest die Zeile bewusst nicht erneut vor - ohne diese
    /// Ansage wäre das Umschalten also stumm.</summary>
    public static string OptionToggled(string name, bool on) =>
        IsGerman ? $"{name} {(on ? "an" : "aus")}." : $"{name} {(on ? "on" : "off")}.";

    /// <summary>Eine Lautstärke-Zeile: "Beacon, 35 Prozent" oder "Beacon, aus".</summary>
    public static string OptionVolume(string name, float volume) =>
        volume <= 0f
            ? (IsGerman ? $"{name}, aus" : $"{name}, off")
            : (IsGerman ? $"{name}, {(int)MathF.Round(volume * 100)} Prozent"
                        : $"{name}, {(int)MathF.Round(volume * 100)} percent");

    /// <summary>Eine einzelne Stufe im Lautstärke-Untermenü.</summary>
    public static string VolumeStep(float volume) =>
        volume <= 0f
            ? (IsGerman ? "Aus" : "Off")
            : (IsGerman ? $"{(int)MathF.Round(volume * 100)} Prozent"
                        : $"{(int)MathF.Round(volume * 100)} percent");

    public static string VolumeSet(string name, float volume) =>
        volume <= 0f
            ? (IsGerman ? $"{name} aus." : $"{name} off.")
            : (IsGerman ? $"{name} auf {(int)MathF.Round(volume * 100)} Prozent."
                        : $"{name} at {(int)MathF.Round(volume * 100)} percent.");

    // Namen der einzelnen Einstellungen. Jede Zeile hier hat ein Feld in
    // Configuration und einen Dienst dahinter, der es liest - eine Beschriftung ohne
    // Funktion dahinter ist genau der Weg, auf dem eine tote Einstellung eine
    // Überarbeitung überlebt.
    public static string OptBeacon => IsGerman ? "Gehhilfe-Beacon" : "Walk-guide beacon";
    public static string OptRouteCues => IsGerman ? "Wegpunkt- und Ankunftston" : "Waypoint and arrival cues";

    public static string OptSkillReady => IsGerman ? "Fähigkeit bereit" : "Ability ready";
    public static string OptSkillReadyVolume => IsGerman ? "Fähigkeit bereit Lautstärke" : "Ability ready volume";
    public static string OptHeading => IsGerman ? "Himmelsrichtung" : "Compass heading";
    public static string OptTargetChanges => IsGerman ? "Zielwechsel" : "Target changes";
    public static string OptTargetHp => IsGerman ? "Ziel-Lebenspunkte" : "Target health";
    public static string OptEnemyCast => IsGerman ? "Gegner wirkt Aktion" : "Enemy casting";
    public static string OptMapFlag => IsGerman ? "Kartenmarkierung" : "Map flag";
    public static string OptErrorToasts => IsGerman ? "Fehlermeldungen" : "Error messages";
    public static string OptInfoToasts => IsGerman ? "Hinweismeldungen" : "Notice messages";

    // ── Namen der Puffer ──────────────────────────────────────────
    //
    // Ein Kanal-Puffer und ein Register-Puffer bekommen ihren Namen vom SPIEL - aus
    // der LogFilter-Zeile beziehungsweise aus dem, was der Spieler selbst als
    // Registernamen eingetippt hat. Übersetzt wird davon nichts. Nur die drei Puffer,
    // die keine Register sind, tragen einen Namen vom Plugin, und jeder sagt, was er
    // ist, nicht was ihn füllt.

    /// <summary>Der Puffer für Dialogfenster. Der eine Puffer, der kein Chat-Register
    /// ist und keines werden kann: der Chat bekommt die Zeile eines NPC erst, wenn der
    /// Spieler weitergeklickt hat, ein aus dem Chat gefüllter Dialogpuffer hinkte dem
    /// Bildschirm also immer einen Schritt hinterher. Gefüllt wird er statt dessen von
    /// den Talk- und _BattleTalk-Lesern.</summary>
    public static string BufferDialogue => IsGerman ? "Dialoge" : "Dialogue";

    /// <summary>Die eigenen Meldungen des Plugins - Toasts, Abmelde-Countdown,
    /// Fensteransagen. Die liefen nie über den Chatlog, also hält sie kein
    /// Register.</summary>
    public static string BufferSystem => IsGerman ? "Meldungen" : "Notices";

    /// <summary>Der einzelne Sammelpuffer, der nur benutzt wird, solange die
    /// Chatfilter des Spiels nicht lesbar sind. Siehe
    /// <see cref="ChatFiltersUnavailable"/>.</summary>
    public static string BufferChat => IsGerman ? "Chat" : "Chat";

    /// <summary>
    /// Ein ganzes Chat-Register in Ankunftsreihenfolge - das, was ein sehender Spieler
    /// sieht, wenn er auf dieses Register schaut.
    ///
    /// DAS IST DER EINZIGE SELBST VERGEBENE NAME IN DER PUFFERLISTE, und das ist
    /// Absicht. Jeder andere Puffer wird vom Spiel benannt. Für "das ganze Register"
    /// hat das Spiel kein Wort, weil es keine Pufferliste hat - es zeichnet das
    /// Register, und ein Auge überfliegt es. Der Addon-Sheet-Block der
    /// Chat-Einstellungen wurde daraufhin durchgesehen (Zeilen 1205-1290): dort stehen
    /// "Alle auswählen" und "Alle abwählen" für die Voreinstellungsknöpfe, aber nichts,
    /// was eine Ansicht benennt.
    ///
    /// Dass der Name vom Plugin kommt, ist der sichtbare Hinweis darauf, dass auch die
    /// GRUPPIERUNG vom Plugin kommt. Der INHALT nicht: eine Zeile liegt genau dann
    /// hier, wenn die Filterdaten des Spiels sagen, dass dieses Register sie zeigt.
    /// </summary>
    public static string BufferTabAll => IsGerman ? "Alles" : "All";

    /// <summary>
    /// Wird EINMAL gesagt, wenn der Filterzustand des Spiels nicht gelesen werden kann.
    /// Die Alternative wäre, dass die Pufferliste ohne Angabe eines Grundes falsch
    /// aussieht. Es landet weiterhin alles in einem Puffer und alles außer dem
    /// Kampflog wird weiterhin gesprochen - das Plugin fällt hörbar zurück, es wird
    /// nicht still.
    /// </summary>
    public static string ChatFiltersUnavailable =>
        IsGerman ? "Die Chat-Einstellungen des Spiels sind nicht lesbar. Der Chat läuft in einem Puffer."
                 : "The game's chat settings cannot be read. Chat is going to one buffer.";

    // ── Register wechseln, und was im neuen Register liegt ────────

    /// <summary>
    /// Wird gesagt, nachdem das Plugin das Chat-Register des Spiels gewechselt hat:
    /// welches Register es jetzt ist, wie viele seiner Puffer etwas enthalten, und der
    /// erste davon mit seiner Anzahl - ein Tastendruck beantwortet also "wo bin ich"
    /// und "was liegt hier" zusammen.
    ///
    /// Gezählt wird, worauf die Blättertaste tatsächlich stehenbleibt, nicht, wie viele
    /// Schalter das Register eingeschaltet hat. Ein Register mit vierzig eingeschalteten
    /// Kanälen, von denen zwei gesprochen haben, ist für den Spieler ein Register mit
    /// zwei Puffern; "vierzig" würde eine Filterliste beschreiben und keinen Verlauf.
    /// </summary>
    public static string ChatTabEntered(string tab, int buffers, string first, int count) =>
        IsGerman ? $"{tab}, {buffers} Puffer. {first}, {count}."
                 : $"{tab}, {buffers} buffers. {first}, {count}.";

    /// <summary>Wird gesagt, wenn die Registertaste den Chatlog des Spiels gar nicht
    /// erreicht. Nie Stille: der Spieler hätte sonst keine Möglichkeit, ein fehlendes
    /// Fenster von einer kaputten Taste zu unterscheiden.</summary>
    public static string ChatTabUnavailable =>
        IsGerman ? "Das Chatfenster ist nicht erreichbar."
                 : "The chat window cannot be reached.";

    // ── Einstellungen: eine Sprachschaltung je Chat-Register ──────

    /// <summary>Benannt nach dem, was das Spiel hat, denn genau das sind die Zeilen
    /// darunter: eine je Chat-Register, unter dem Namen des Registers selbst.</summary>
    public static string OptionsChatTabs => IsGerman ? "Chat-Register" : "Chat tabs";

    /// <summary>
    /// Die oberste Zeile im Untermenü eines Registers: wird dieses Register vorgelesen.
    ///
    /// JEDE ZEILE IN DIESEM ABSCHNITT SAGT "VORLESEN", und zwar mit Absicht. Die
    /// Schaltung des SPIELS entscheidet, ob eine Zeile überhaupt existiert - aus, heißt
    /// nicht angezeigt, nicht archiviert, nicht gesprochen. Die Schaltung HIER
    /// entscheidet nur, ob eine ohnehin vorhandene Zeile laut vorgelesen wird; aus,
    /// heißt archiviert und blätterbar, aber still. Beide sitzen im Kopf des Spielers
    /// nebeneinander, also muss die Zeile des Plugins das eine benennen, was sie
    /// anfasst. Ein Wort wie "stummschalten" beschreibt einen Zustand, ohne zu sagen,
    /// was verstummt, und das ist genau die Zweideutigkeit, die hier zu vermeiden ist.
    /// </summary>
    public static string OptChatTabMaster =>
        IsGerman ? "Register vorlesen" : "Read tab aloud";

    /// <summary>Die Gruppenzeile im Untermenü eines Kanals - die ganze Akteursgruppe
    /// auf einmal, über den Kästchen, in die das Spiel sie aufteilt. Gleiche
    /// Wortregel wie bei <see cref="OptChatTabMaster"/>.</summary>
    public static string OptChatChannelAll =>
        IsGerman ? "Ganze Gruppe vorlesen" : "Read whole group aloud";

    /// <summary>
    /// Die eine Schaltung für Zeilen, für die die Filterliste des Spiels gar kein
    /// Kästchen hat - die Anmeldehinweise, die Phishing-Warnung, ein eingehendes
    /// Tell, ein GM.
    ///
    /// Benannt nach dem, was sie abdeckt, und nicht nach einem Kanal, denn ein Kanal
    /// ist es nicht: es ist alles, was das Spiel nicht filterbar gemacht hat. Sie steht
    /// unten in der Registerliste, weil sie zu keinem Register gehört.
    /// </summary>
    public static string OptChatUnfiltered =>
        IsGerman ? "Meldungen ohne Spielfilter vorlesen" : "Read lines the game cannot filter";

    /// <summary>Die Zeile, wenn die Register nicht lesbar sind - eine Schaltung für den
    /// einen Sammelpuffer. Ein Abschnitt, der seinen Namen nennt und dann nichts
    /// anbietet, liest sich wie ein Fehler; also sagt er statt dessen, in welchem
    /// Zustand er ist.</summary>
    public static string OptChatFallback =>
        IsGerman ? "Chat vorlesen (Register nicht lesbar)" : "Read chat aloud (tabs unreadable)";
}
