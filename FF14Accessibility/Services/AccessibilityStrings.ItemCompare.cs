using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Die Sprachbausteine für den Ausrüstungs-Vergleich (siehe
/// <see cref="ItemCompareService"/>).
///
/// Eigene Datei und deshalb <c>partial</c>, aus demselben Grund wie
/// <c>AccessibilityStrings.Chat.cs</c>: die Hauptdatei ist groß und ändert sich in
/// fast jeder Version, und dieser Beitrag soll sich in einem Stück wieder
/// entfernen lassen.
///
/// WAS HIER NICHT STEHT, UND WARUM: die Namen der Werte ("Magic Defense",
/// "Physical Damage"), die Zahlen, der Platz ("Slot: Body"), die Stufen
/// ("Item Level 27") und die Bonuszeilen ("Gathering +32") kommen FERTIG
/// FORMULIERT aus dem Vergleichsfenster des Spiels, in der Spielsprache (gemessen
/// 2026-08-30). Sie werden deshalb wörtlich weitergereicht und nicht hier
/// übersetzt - genau wie Gegenstands- und NPC-Namen überall sonst im Plugin.
/// Übersetzt wird nur, was das Plugin selbst dazu erfindet: das Urteil, der
/// Zeilenrahmen der Tabelle, die Richtungswörter und die paar eigenen
/// Spaltennamen.
/// </summary>
public static partial class AccessibilityStrings
{
    // ── Urteil (die Überschrift der Tabelle) ──────────────────────

    /// <summary>Alle veränderten Werte sind besser. <paramref name="changed"/> von
    /// <paramref name="total"/> verglichenen Werten.</summary>
    public static string CompareBetter(int changed, int total) =>
        IsGerman ? $"Besser in {changed} von {total}." : $"Better in {changed} of {total}.";

    /// <summary>Alle veränderten Werte sind schlechter.</summary>
    public static string CompareWorse(int changed, int total) =>
        IsGerman ? $"Schlechter in {changed} von {total}." : $"Worse in {changed} of {total}.";

    /// <summary>Gemischt: manche Werte besser, manche schlechter.</summary>
    public static string CompareMixed(int better, int worse, int total) =>
        IsGerman ? $"Besser in {better}, schlechter in {worse}, von {total}."
                 : $"Better in {better}, worse in {worse}, of {total}.";

    /// <summary>
    /// Kein einziger Wert verändert sich.
    ///
    /// DAS MUSS GESAGT WERDEN, es darf nicht einfach still bleiben: das Spiel
    /// LÄSST einen Unterschied von null WEG, statt "(0)" zu schreiben (gemessen
    /// 2026-08-30 an einem Teil, das mit dem getragenen identisch war). Ohne
    /// diesen Satz wäre "gleich gut" von "keine Daten" nicht zu unterscheiden.
    /// </summary>
    public static string CompareSame => IsGerman ? "Gleich wie angelegt." : "Same as equipped.";

    // ── Die Zeilen der Tabelle ────────────────────────────────────
    //
    // JEDE ZEILE NENNT BEIDE SPALTEN, in derselben Reihenfolge: erst der
    // Gegenstand aus der Tasche, dann der angelegte. Das ist die Form, die aus
    // dem Nebeneinander des Spielfensters wird - ein Bildschirmleser liest
    // nacheinander, also muss die Zeile selbst sagen, welche Zahl zu welcher
    // Seite gehört. "19, angelegt 7" braucht dafür kein Gedächtnis; eine reine
    // Zahlenspalte hätte eines gebraucht.

    /// <summary>Eine Zeile mit eigenem Spaltennamen: Wert, Tasche, angelegt.
    /// Der Doppelpunkt trennt den Namen hörbar vom ersten Wert.</summary>
    public static string CompareRow(string name, string mine, string equipped) =>
        IsGerman ? $"{name}: {mine}, angelegt {equipped}"
                 : $"{name}: {mine}, equipped {equipped}";

    /// <summary>Eine Zeile, deren beide Seiten das Spiel schon vollständig
    /// beschriftet hat ("Item Level 27") - hier wird nichts davorgesetzt.</summary>
    public static string CompareRowVerbatim(string mine, string equipped) =>
        IsGerman ? $"{mine}, angelegt {equipped}"
                 : $"{mine}, equipped {equipped}";

    /// <summary>
    /// Der Unterschied, hinten an die Zeile gehängt.
    ///
    /// Am ENDE der Zeile, nicht am Anfang: die zwei Zahlen sind das, was der
    /// Spieler vergleicht, und der Unterschied ist die Zusammenfassung davon.
    /// Wer ihn voranstellt, zwingt zum Warten auf die Zahlen, die ihn erklären.
    /// Die Größenordnung kommt bei den Werten als TEXT aus dem Spiel, damit kein
    /// Zahlenformat verlorengeht (Verzögerung steht dort mit Nachkommastellen).
    /// </summary>
    public static string CompareDelta(bool up, string amount) =>
        up ? (IsGerman ? $", plus {amount}"  : $", plus {amount}")
           : (IsGerman ? $", minus {amount}" : $", minus {amount}");

    /// <summary>
    /// Eine Seite, die es nicht gibt - kein angelegtes Teil, ein Wert, den nur
    /// eines der beiden Teile hat, ein leerer Materia-Platz.
    ///
    /// GESAGT STATT WEGGELASSEN: eine Zeile, die nur eine Zahl nennt, lässt offen,
    /// zu welcher der beiden Spalten sie gehört - und das ist genau die Frage, für
    /// die die Tabelle da ist.
    /// </summary>
    public static string CompareCellNone => IsGerman ? "nichts" : "none";

    // ── Eigene Spaltennamen ───────────────────────────────────────
    //
    // Nur für die vier Zeilen, für die das Spiel selbst keine Beschriftung
    // mitliefert. Alles andere trägt seinen Namen schon.

    /// <summary>Die erste Zeile: die zwei Gegenstände selbst. Sie ist die
    /// Kopfzeile der beiden Spalten - alles darunter ist "dieser gegen jenen".</summary>
    public static string CompareRowItem => IsGerman ? "Gegenstand" : "Item";

    /// <summary>Die Materia-Zeile: was eingesetzt ist, und wie viele Plätze das
    /// Teil überhaupt hat.</summary>
    public static string CompareRowMateria => IsGerman ? "Materia" : "Materia";

    /// <summary>Das Teil kann gar keine Materia aufnehmen.</summary>
    public static string CompareMateriaNoSockets =>
        IsGerman ? "keine Plätze" : "no sockets";

    /// <summary>
    /// Plätze vorhanden, aber nichts eingesetzt.
    ///
    /// DIESE ZEILE IST DER GRUND, WARUM ES DIE MATERIA-ZEILE GIBT. Vorher wurde
    /// ein leerer Platz einfach übersprungen, und ein Teil mit zwei freien Plätzen
    /// erzeugte gar keine Zeile - "nichts eingesetzt" war von "der Leser ist
    /// kaputt" nicht zu unterscheiden. Genau so wurde der Fehler auch gemeldet.
    /// </summary>
    public static string CompareMateriaEmpty(int sockets) =>
        IsGerman ? (sockets == 1 ? "1 freier Platz" : $"{sockets} freie Plätze")
                 : (sockets == 1 ? "1 empty socket" : $"{sockets} empty sockets");

    /// <summary>Eingesetzte Materia, dazu die Zahl der Plätze. Die Namen kommen
    /// wörtlich aus dem Fenster - ihre Form ist nie gemessen worden, also wird
    /// nichts daran zerlegt.</summary>
    public static string CompareMateriaMelded(string melded, int sockets) =>
        IsGerman ? (sockets > 0 ? $"{melded}, {sockets} Plätze" : melded)
                 : (sockets > 0 ? $"{melded}, {sockets} sockets" : melded);

    /// <summary>Die Klassenliste, wie das Spiel sie führt. Das ist die
    /// ABKÜRZUNGSLISTE ("ARC BRD") - siehe <see cref="CompareRowYourClasses"/>.</summary>
    public static string CompareRowClasses => IsGerman ? "Klassen" : "Classes";

    /// <summary>
    /// Die ausgeschriebenen Klassennamen für das Teil in der Tasche.
    ///
    /// EIGENE ZEILE, weil das Spiel in der Zeile darüber nur Kürzel schreibt, die
    /// ein Bildschirmleser buchstabiert. Und NUR für die Tasche, weil der Name
    /// aus <c>GearInfoService</c> eine Gegenstands-Id braucht und das Spiel hier
    /// nur eine veröffentlicht: die des Teils unter dem Zeiger.
    /// </summary>
    public static string CompareRowYourClasses(string classes) =>
        IsGerman ? $"Tragbar {classes}." : $"Wearable {classes}.";

    // ── Rahmen ────────────────────────────────────────────────────

    /// <summary>
    /// Der Ring an der ANDEREN Hand. Ringe sind der einzige Platz, den das Spiel
    /// doppelt vergleicht - jeder Unterschied in der Tabelle bezieht sich auf den
    /// Ring, den <c>SlotName</c> nennt, dieser hier ist der zweite (gemessen
    /// 2026-08-30).
    /// </summary>
    public static string CompareOtherRing(string name) =>
        IsGerman ? $"Anderer Ring: {name}." : $"Other ring: {name}.";

    /// <summary>Kein Vergleichsfenster offen - der Spieler steht nicht auf einem
    /// Ausrüstungsteil.</summary>
    public static string CompareUnavailable =>
        IsGerman ? "Kein Ausrüstungs-Vergleich offen." : "No gear comparison open.";
}
