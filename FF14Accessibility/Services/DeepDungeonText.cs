using System;
using Dalamud.Plugin.Services;
using Lumina.Text.ReadOnly;

namespace FF14Accessibility.Services;

/// <summary>
/// DAS FEHLENDE WORT IN JEDER POMANDER-BESCHREIBUNG.
///
/// User, 2026-08-12: *"the pomander descriptions are confirmed working, though as you
/// can see from the log, if they have multi-line descriptions, one line gets cut off.
/// EG: 'Decreases movement speed for enemies on the next' - the last word is probably
/// supposed to be floor."* Das letzte Wort IST "floor", und es war keine Zeile, die
/// verloren ging - es war ein Makro.
///
/// WAS IM SHEET WIRKLICH STEHT, Nutzlast fuer Nutzlast (Offline-Auszug
/// <c>tools/deepdungeon-dump pomander</c>, 2026-08-12):
///
/// <code>
///   Pomander of Flight
///     TEXT&lt;Decreases the number of enemies &gt;
///     MACRO&lt;If:[gnum109==4],in,on&gt;
///     TEXT&lt; the next &gt;
///     MACRO&lt;Switch:gnum109,floor,floor,floor,area&gt;
///     TEXT&lt;.&gt;
/// </code>
///
/// Die globale Zahl 109 ist das Tiefe Gewoelbe, in dem der Spieler steckt, und das
/// Spiel waehlt daraus sein eigenes Hauptwort: "floor" fuer den Palast der Toten,
/// Himmelsberg und Eureka Orthos, "area" fuer den Pilgerpfad - genau deshalb kann das
/// Wort hier auch nicht einfach hingeschrieben werden. <c>ExtractText()</c> liefert nur
/// die TEXT-Nutzlasten und laesst beide Makros fallen; so wurde aus "on the next floor."
/// das Fragment "the next ." und aus "Reveals the current floor's map" die Zeile
/// "Reveals the current 's map".
///
/// Diese Klasse macht also, was das Spiel macht: sie gibt die Zeichenkette an Dalamuds
/// <c>ISeStringEvaluator</c>, der die Makros gegen den Parameter-Anbieter des Spiels
/// aufloest. Hier wird nichts ersetzt, kein Wort fest verdrahtet, und das Ergebnis
/// stimmt in jeder Client-Sprache und in allen vier Tiefen Gewoelben.
///
/// Das anschliessende Glaetten auf eine Zeile bleibt unveraendert und wird weiterhin
/// gebraucht - Sheet-Text ist fuer ein Tooltip-Feld umbrochen, ein Screenreader will
/// einen Satz.
/// </summary>
public sealed class DeepDungeonText
{
    private readonly ISeStringEvaluator _evaluator;
    private readonly IPluginLog         _log;

    public DeepDungeonText(ISeStringEvaluator evaluator, IPluginLog log)
    {
        _evaluator = evaluator;
        _log       = log;
    }

    /// <summary>
    /// Die Lesart des Spiels fuer eine Sheet-Zeichenkette, geglaettet auf eine Zeile
    /// Sprache.
    ///
    /// try-catch, weil der Auswerter ueber einen Dalamud-Dienst laufenden Spielzustand
    /// liest - derselbe Grund, aus dem <see cref="DeepDungeonState.GetDirector"/> seinen
    /// eigenen Aufruf absichert. Ein Fehlschlag kostet die beiden aufgeloesten Woerter,
    /// nicht die Beschreibung: dann wird die rohe Extraktion benutzt und der Grund
    /// einmal protokolliert.
    /// </summary>
    public string Read(ReadOnlySeString text)
    {
        try
        {
            return Flatten(_evaluator.Evaluate(text).ExtractText());
        }
        catch (Exception ex)
        {
            if (_warned) return Flatten(text.ExtractText());
            _warned = true;
            _log.Warning($"[DeepText] SeString-Auswertung nicht moeglich ({ex.Message}) - "
                         + "Beschreibungen kommen ohne die Platzhalter des Spiels.");
            return Flatten(text.ExtractText());
        }
    }

    private bool _warned;

    /// <summary>
    /// Bringt einen umbrochenen Sheet-Text auf eine Zeile.
    ///
    /// An Leerraum trennen und mit einfachen Leerzeichen wieder zusammensetzen erledigt
    /// beides auf einmal: die Zeilenumbrueche, die das Tooltip-Feld braucht,
    /// verschwinden, und ebenso das doppelte Leerzeichen, das ein aufgeloestes Makro
    /// zwischen zwei Abschnitten hinterlassen kann.
    /// </summary>
    public static string Flatten(string text) =>
        string.Join(' ', text.Split(new[] { '\n', '\r', ' ', '\t' },
                                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
