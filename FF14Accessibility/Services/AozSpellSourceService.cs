using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaAozAction = Lumina.Excel.Sheets.AozAction;
using LuminaAozActionTransient = Lumina.Excel.Sheets.AozActionTransient;
using LuminaContentFinderCondition = Lumina.Excel.Sheets.ContentFinderCondition;
using LuminaPlaceName = Lumina.Excel.Sheets.PlaceName;

namespace FF14Accessibility.Services;

/// <summary>Woher ein Blaumagie-Zauber zu holen ist.</summary>
public enum AozSourceKind
{
    /// <summary>Das Spiel nennt keinen Fundort (Karneval-Belohnung, Startzauber).</summary>
    None,
    /// <summary>Ein Gebiet der offenen Welt - dorthin kann man laufen.</summary>
    World,
    /// <summary>Eine Instanz - erreichbar ueber ihren Eingang.</summary>
    Duty,
}

/// <summary>
/// Ein Blaumagie-Zauber als ZIEL: was er ist und wohin man dafuer muss.
/// </summary>
/// <param name="Number">Nummer im Zauberbuch, 1 bis 124.</param>
/// <param name="SpellName">Name des Zaubers.</param>
/// <param name="ActionId">Aktions-Id (fuer die Sonde und das Nachschlagen).</param>
/// <param name="UnlockLink">Freischalt-Verweis, ueber den "schon erlernt?" laeuft.</param>
/// <param name="Kind">Art des Fundorts.</param>
/// <param name="PlaceName">Gebiets- oder Instanzname, gesprochen.</param>
/// <param name="MapId">Karte des Gebiets (nur bei <see cref="AozSourceKind.World"/>), sonst 0.</param>
/// <param name="InstanceContentId">InstanceContent-Zeile (nur bei <see cref="AozSourceKind.Duty"/>), sonst 0.</param>
public sealed record AozSpellTarget(
    byte   Number,
    string SpellName,
    uint   ActionId,
    uint   UnlockLink,
    AozSourceKind Kind,
    string PlaceName,
    uint   MapId,
    uint   InstanceContentId);

/// <summary>
/// Die Blaumagie-Zauber als Wegweiser: welche noch fehlen, und wo sie zu holen
/// sind. Gegenstueck zum Jagdtagebuch (<see cref="HuntingLogService"/>), das
/// dieselbe Frage fuer die Monster des Jagdrangs beantwortet.
///
/// <para>
/// WAS DAS SPIEL NICHT HERGIBT, und deshalb steht es hier gleich am Anfang: es
/// gibt KEINE Zuordnung Zauber → Monster. Geprueft am 2026-09-02 gegen alle drei
/// Aoz-Sheets (<c>AozActionXdQZ</c> existiert nicht einmal) und gegen
/// <c>MonsterNoteTarget</c>, das fuer das Jagdtagebuch genau diese Zuordnung
/// fuehrt - <c>BNpcName</c> plus Gebiet plus Unterort. Bei Blaumagie steht an
/// dieser Stelle nur ein Fundort. Die Monsternamen kommen zwar in den
/// Beschreibungstexten vor ("Angriffszauber der Kraken"), aber das ist Prosa und
/// kein Datenfeld. Diese Kategorie fuehrt deshalb zum ORT, nicht zum Monster.
/// </para>
///
/// <para>
/// OFFLINE GEMESSEN (2026-09-02, installiertes sqpack, deutsche Fassung):
/// <list type="bullet">
/// <item>33 Zauber liegen in 21 Gebieten der offenen Welt. ALLE 33 lassen sich
///   ueber ihren PlaceName auf eine Karte abbilden - die Uebergangs-Route kann
///   also fuer jeden von ihnen greifen.</item>
/// <item>77 Zauber liegen in 62 Instanzen. ALLE 77 fuehren ueber ihre
///   ContentFinderCondition auf eine InstanceContent-Zeile - denselben
///   Schluessel, den <see cref="DutyEntranceService"/> fuer die Tueren fuehrt.</item>
/// <item>13 Zauber sind Karneval-Belohnungen und einer ist der Startzauber; zu
///   ihnen nennt das Spiel keinen Ort. Sie werden als solche angesagt, statt
///   einen Ort zu erfinden.</item>
/// </list>
/// </para>
///
/// <para>
/// "SCHON ERLERNT?" LAeUFT UeBER <c>UnlockLink</c>. Alle 124 Zauber tragen im
/// Action-Sheet einen, alle verschieden (102 bis 461), keiner ueber 0x10000 -
/// es sind also echte Freischalt-Verweise und keine Quest-Pruefungen. Zum
/// Vergleich: gewoehnliche Klassen-Aktionen tragen dort 0. Gefragt wird
/// <c>UIState.IsUnlockLinkUnlocked</c>. Das ist der einzige Weg, der auch
/// DRAUSSEN funktioniert - im Zauberbuch selbst traegt die Sichtbarkeit des
/// Hinweises "Noch nicht erlernt." den Zustand, aber das Fenster ist beim
/// Durchblaettern des Objekt-Browsers nicht offen.
/// </para>
/// </summary>
public sealed unsafe class AozSpellSourceService
{
    private readonly IDataManager _data;
    private readonly PlacesService _places;
    private readonly IPluginLog _log;

    private List<AozSpellTarget>? _all;
    private uint? _blueMageJob;
    private bool _loggedUnlockSummary;

    public AozSpellSourceService(IDataManager data, PlacesService places, IPluginLog log)
    {
        _data   = data;
        _places = places;
        _log    = log;
    }

    /// <summary>Alle 124 Zauber mit ihrem Fundort, unabhaengig vom Fortschritt.</summary>
    public IReadOnlyList<AozSpellTarget> GetAll() => _all ??= Build();

    /// <summary>
    /// Die Zauber, die dem Spieler noch FEHLEN. Leer, solange das Spiel die
    /// Freischaltfrage nicht beantwortet - dann bietet der Browser die Kategorie
    /// gar nicht erst an, statt eine erfundene Liste zu zeigen.
    /// </summary>
    public List<AozSpellTarget> GetMissing()
    {
        var state = UIState.Instance();
        if (state == null) return new List<AozSpellTarget>();

        var all     = GetAll();
        var missing = new List<AozSpellTarget>();
        var known   = 0;
        foreach (var t in all)
        {
            if (state->IsUnlockLinkUnlocked(t.UnlockLink)) known++;
            else missing.Add(t);
        }

        // Einmal pro Sitzung: die Gesamtzahl gegen das, was das Zauberbuch im
        // Kopf fuehrt ("Erlernt: 1/124"). Stimmen sie nicht ueberein, taugt
        // UnlockLink fuer Blaumagie nicht - und das faellt hier auf, nicht erst
        // beim Spieler.
        if (!_loggedUnlockSummary)
        {
            _loggedUnlockSummary = true;
            _log.Info($"[AozZiel] Freischaltung ueber UnlockLink: {known} von {all.Count} erlernt, " +
                      $"{missing.Count} offen. GEGENPROBE: dieselbe Zahl muss im Zauberbuch " +
                      $"unter \"Erlernt\" stehen.");
        }

        return missing;
    }

    /// <summary>
    /// Die Klasse, zu der die Blaumagie-Zauber gehoeren - AUS DEN SHEETS
    /// abgeleitet, nicht als Zahl hingeschrieben.
    ///
    /// <para>
    /// Gemessen am 2026-09-02: alle 124 Zauber zeigen auf dieselbe Klasse, und
    /// das ist ClassJob 36 "Blaumagier" (BMA). Weil die Zuordnung eindeutig ist,
    /// kann die Id von dort kommen statt aus dem Gedaechtnis - eine
    /// hartgeschriebene 36 waere eine Behauptung, die niemand nachpruefen kann.
    /// </para>
    ///
    /// <para>
    /// NICHT ueber <c>ClassJob.IsLimitedJob</c>: das Flag traegt auch der
    /// Bestienbaendiger (ClassJob 43), es ist also kein Erkennungsmerkmal fuer
    /// Blaumagie. Auch das ist gemessen, nicht vermutet.
    /// </para>
    ///
    /// <para>0, wenn die Sheets nichts hergeben - dann bleibt die Kategorie verborgen.</para>
    /// </summary>
    public uint BlueMageJobId
    {
        get
        {
            if (_blueMageJob.HasValue) return _blueMageJob.Value;

            var jobs = new HashSet<uint>();
            var actions = _data.GetExcelSheet<LuminaAozAction>();
            if (actions != null)
                foreach (var row in actions)
                {
                    // Zeile 0 von AozAction ist die Leerzeile des Sheets. Sie
                    // wird uebersprungen wie ueberall sonst - ohne das zeigte
                    // sie auf Action-Zeile 0, und DEREN ClassJob ist nicht 0,
                    // sondern uint.MaxValue ("kein Job" als -1 kodiert).
                    // Gemessen 2026-09-02: genau das liess die Ableitung zwei
                    // Klassen sehen, also gab sie 0 zurueck und die Kategorie
                    // blieb verborgen (Log 18:45:20).
                    if (row.Action.RowId == 0) continue;
                    if (row.Action.ValueNullable is not { } act) continue;

                    var jobId = act.ClassJob.RowId;
                    // Beide Formen von "kein Job" abfangen, nicht nur die eine.
                    if (jobId == 0 || jobId == uint.MaxValue) continue;
                    jobs.Add(jobId);
                }

            // Nur bei EINER Klasse ist die Ableitung eindeutig. Streuen die
            // Zauber jemals ueber mehrere, ist die Annahme hinfaellig - dann
            // lieber nichts behaupten als die haeufigste raten.
            _blueMageJob = jobs.Count == 1 ? jobs.First() : 0u;
            _log.Info($"[AozZiel] Blaumagier-Klasse aus den Sheets: {_blueMageJob} " +
                      $"({jobs.Count} Klasse(n) bei den Zaubern gefunden).");
            return _blueMageJob.Value;
        }
    }

    /// <summary>
    /// Die fehlenden Zauber IN DER REIHENFOLGE DES ZAUBERBUCHS, also nach ihrer
    /// Nummer (Wunsch des Users 2026-09-02: "kannst du das so sortieren wie es
    /// im buch ist").
    ///
    /// <para>
    /// DASS DAS BUCH NACH NUMMER SORTIERT IST, ist am Log vom 2026-09-02
    /// abgelesen und nicht angenommen: beim Seitenblättern sprang dieselbe
    /// Rasterposition in Sechzehnerschritten (Nr. 16, 32, 48, 64, 80), und
    /// innerhalb einer Seite lief es fortlaufend (Nr. 80, 79, 78 ... 65). Also
    /// 16 Kacheln je Seite, aufsteigend nach Nummer - und
    /// <c>AozActionTransient.Number</c> laeuft lueckenlos von 1 bis 124.
    /// </para>
    ///
    /// <para>
    /// WARUM NICHT NACH ERREICHBARKEIT (so war es bis zu diesem Wunsch): eine
    /// nach Entfernung sortierte Liste ordnet sich bei jedem Zonenwechsel neu.
    /// Die Buchreihenfolge steht fest, ist mit dem Zauberbuch abgleichbar, und
    /// der zehnte Eintrag ist morgen noch der zehnte. Was in dieser Zone zu
    /// holen ist, sagt weiterhin die Kopfansage der Kategorie.
    /// </para>
    /// </summary>
    public List<AozSpellTarget> GetMissingInBookOrder() =>
        GetMissing().OrderBy(t => t.Number).ToList();

    /// <summary>
    /// Baut die Liste einmalig aus den Sheets. Nur Sheet-Daten - der
    /// Spielfortschritt wird bei jeder Abfrage frisch erfragt, nie gecacht.
    /// </summary>
    private List<AozSpellTarget> Build()
    {
        var result = new List<AozSpellTarget>();

        var actions    = _data.GetExcelSheet<LuminaAozAction>();
        var transients = _data.GetExcelSheet<LuminaAozActionTransient>();
        if (actions == null || transients == null)
        {
            _log.Warning("[AozZiel] Sheets nicht verfuegbar.");
            return result;
        }

        var world = 0; var duty = 0; var none = 0; var noMap = 0;
        foreach (var row in actions)
        {
            if (row.Action.RowId == 0) continue;
            var action = row.Action.ValueNullable;
            if (action is not { } act) continue;

            var name = act.Name.ExtractText()?.Trim() ?? string.Empty;
            if (name.Length == 0) continue;

            var t = transients.GetRowOrDefault(row.RowId);
            if (t is not { } tv) continue;

            var kind      = AozSourceKind.None;
            var place     = string.Empty;
            var mapId     = 0u;
            var contentId = 0u;

            switch (tv.LocationKey)
            {
                // 1 = Gebiet der offenen Welt. Die Karte ist der Schluessel fuer
                // die Uebergangs-Route (siehe PlacesService.FindFirstHopToMap).
                case 1:
                    place = tv.Location.GetValueOrDefault<LuminaPlaceName>()?.Name.ExtractText()?.Trim()
                            ?? string.Empty;
                    mapId = _places.FindMapByPlaceName(tv.Location.RowId);
                    if (place.Length > 0)
                    {
                        kind = AozSourceKind.World;
                        world++;
                        if (mapId == 0) noMap++;
                    }
                    break;

                // 4 = Instanz. Ueber ContentFinderCondition.Content auf dieselbe
                // InstanceContent-Zeile, die DutyEntranceService fuer die Tueren
                // fuehrt - damit haengt die Kategorie am vorhandenen Wegweiser
                // statt an einem zweiten, eigenen.
                case 4:
                    var cfc = tv.Location.GetValueOrDefault<LuminaContentFinderCondition>();
                    place     = cfc?.Name.ExtractText()?.Trim() ?? string.Empty;
                    contentId = cfc?.Content.RowId ?? 0;
                    if (place.Length > 0)
                    {
                        kind = AozSourceKind.Duty;
                        duty++;
                    }
                    break;

                // 2 und 3 tragen durchweg RowId 0 - das Spiel nennt hier keinen
                // Ort. Nicht erfinden, sondern als ortlos ansagen.
                default:
                    none++;
                    break;
            }

            result.Add(new AozSpellTarget(
                tv.Number, name, row.Action.RowId, act.UnlockLink.RowId,
                kind, place, mapId, contentId));
        }

        _log.Info($"[AozZiel] {result.Count} Zauber geladen: {world} in der Welt " +
                  $"({noMap} davon ohne Karte), {duty} in Instanzen, {none} ohne Fundort.");
        return result;
    }
}
