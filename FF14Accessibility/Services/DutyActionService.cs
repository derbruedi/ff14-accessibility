using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// Die kleine Sonderaktions-Leiste, die manche Auftraege und Freibriefe einblenden
/// ("Duty Actions") - fangen, betaeuben, ein Geraet ausloesen. Sie erscheint erst,
/// wenn der Inhalt sie braucht, und verschwindet danach wieder.
///
/// WARUM ES DIESEN DIENST BRAUCHT, und das ist keine Bequemlichkeit: im
/// Live-Tastenbelegungs-Dump vom 2026-08-09 (679 Eintraege) gibt es fuer diese
/// Leiste KEINE EINZIGE Belegung - das Spiel erwartet dort einen Mausklick. Ohne
/// Mod ist sie per Tastatur ueberhaupt nicht erreichbar, und ein blinder Spieler
/// erfaehrt zudem nie, dass sie aufgetaucht ist. Beides loest dieser Dienst:
/// er sagt das Auftauchen an und legt die Ausloesung auf zwei Tasten.
///
/// Quelle (siehe docs/game-api.md -> "Quest-Gegenstaende im Kampf", Abschnitt B):
/// <c>DutyActionManager.GetInstanceIfReady()</c> ist null, solange es keine Leiste
/// gibt; <c>NumValidSlots</c> sagt, wie viele Plaetze belegt sind, <c>ActionId[]</c>
/// nennt je Platz die Aktion aus dem Action-Sheet. Ausgeloest wird ueber
/// <c>RaptureHotbarModule.ExecuteDutyActionSlot</c> - dieselbe Methode, die das
/// Spiel beim Mausklick benutzt, es wird also nichts nachgebaut.
///
/// NICHT VERWECHSELN mit dem Schluesselgegenstand einer Quest (EventItem, der
/// haeufigere Fall) und nicht mit Freibriefen, die ein EMOTE verlangen - der
/// Fang-Freibrief "Dodos an Bord" etwa will das Emote "Beruhigen" und hat gar
/// keine Sonderaktionsleiste. Die drei Wege sind getrennt.
/// </summary>
public sealed class DutyActionService
{
    private readonly IDataManager  _data;
    private readonly TolkService   _tolk;
    private readonly CueService    _cue;
    private readonly Configuration _config;
    private readonly IPluginLog    _log;

    /// <summary>Erzeugt den Dienst.</summary>
    public DutyActionService(IDataManager data, TolkService tolk, CueService cue,
                             Configuration config, IPluginLog log)
    {
        _data   = data;
        _tolk   = tolk;
        _cue    = cue;
        _config = config;
        _log    = log;
    }

    // Wie viele Plaetze die Struktur fuehrt (ActionId[5]). Es sind selten mehr als
    // zwei belegt, aber die Zahl kommt aus der Struktur und nicht aus der Annahme.
    private const int SlotCount = 5;

    // Welche Aktion zuletzt auf welchem Platz lag. 0 = Platz war leer. Nur die
    // AENDERUNG wird angesagt: die Leiste steht waehrend des ganzen Auftrags da,
    // und sie jede Sekunde zu nennen waere dieselbe Dauerbeschallung, die die
    // Freibrief-Meldung hatte.
    private readonly uint[] _lastActionIds = new uint[SlotCount];
    private bool _hadBar;

    /// <summary>
    /// Wird jeden Frame aus Plugin.OnFrameworkUpdate gerufen. Sagt an, wenn die
    /// Leiste auftaucht, sich aendert oder wieder verschwindet.
    /// </summary>
    public unsafe void Update()
    {
        if (!_config.AnnounceDutyActions) return;

        var mgr = DutyActionManager.GetInstanceIfReady();
        if (mgr == null)
        {
            // Leiste weg: Zustand vergessen, damit der naechste Auftrag sie wieder
            // als neu ansagt.
            if (_hadBar)
            {
                _hadBar = false;
                for (var i = 0; i < SlotCount; i++) _lastActionIds[i] = 0;
                _log.Info("[Sonderaktion] Leiste verschwunden.");
            }
            return;
        }

        var changed = false;
        for (var i = 0; i < SlotCount; i++)
        {
            var id = mgr->ActionId[i];
            if (id == _lastActionIds[i]) continue;
            _lastActionIds[i] = id;
            if (id != 0) changed = true;
        }

        if (!changed) { _hadBar = true; return; }

        // Ein Ton VOR der Ansage: das Auftauchen der Leiste ist der Moment, auf den
        // ein sehender Spieler reagiert, und ein Ton kommt schneller an als ein Satz.
        _cue.PlaySkillReadyTone();

        var names = new List<string>();
        for (var i = 0; i < SlotCount; i++)
            if (_lastActionIds[i] != 0)
                names.Add(AccessibilityStrings.DutyActionSlot(i + 1, ActionName(_lastActionIds[i])));

        if (names.Count > 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.DutyActionsAvailable(
                string.Join(", ", names), _config.KeyDutyAction1));
            _log.Info($"[Sonderaktion] Leiste: {string.Join(" | ", names)} " +
                      $"(NumValidSlots={mgr->NumValidSlots}, ActionsPresent={mgr->ActionsPresent})");
        }
        _hadBar = true;
    }

    /// <summary>
    /// Loest den Platz <paramref name="slot"/> (1-basiert, wie in der Ansage) aus.
    /// Das Ergebnis von <c>ExecuteDutyActionSlot</c> wird AUSGEWERTET und nicht
    /// weggeworfen: das Spiel lehnt die Ausfuehrung ab, wenn die Aktion gerade
    /// nicht geht, und ohne Rueckmeldung stuende der Spieler vor genau der Stille,
    /// die dieses Plugin beseitigen soll.
    /// </summary>
    public unsafe void Execute(int slot)
    {
        var index = slot - 1;
        if (index < 0 || index >= SlotCount) return;

        var mgr = DutyActionManager.GetInstanceIfReady();
        if (mgr == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoDutyActions);
            return;
        }

        var id = mgr->ActionId[index];
        if (id == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.DutyActionSlotEmpty(slot));
            return;
        }

        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoDutyActions);
            _log.Warning("[Sonderaktion] RaptureHotbarModule ist null - Ausloesung nicht moeglich.");
            return;
        }

        var name = ActionName(id);
        var ok   = hotbar->ExecuteDutyActionSlot((uint)index);
        _log.Info($"[Sonderaktion] Platz {slot} ausgeloest: '{name}' (id={id}) -> {ok}");

        // Bei Erfolg NICHT den Namen nachsprechen: die Aktion hat eine Wirkzeit und
        // das Spiel meldet sich selbst (Toast, Systemmeldung), und mitten im Kampf
        // ist jede zusaetzliche Silbe eine, die die naechste Warnung verzoegert.
        if (!ok) _tolk.SpeakInterrupt(AccessibilityStrings.DutyActionRefused(name));
    }

    /// <summary>Sagt die Leiste auf Wunsch noch einmal an - fuer den Fall, dass die
    /// automatische Ansage in einer lauten Kampfphase untergegangen ist.</summary>
    public unsafe void Announce()
    {
        var mgr = DutyActionManager.GetInstanceIfReady();
        if (mgr == null)
        {
            // PROTOKOLLIERT, und das war beim ersten Test der Mangel: die Taste
            // meldete "keine Sonderaktion" und hinterliess KEINE Zeile - damit
            // liess sich nicht unterscheiden, ob wirklich keine Leiste da war
            // oder ob der Zugriff daneben griff. Jetzt sagt das Log es.
            _log.Info("[Sonderaktion] Abfrage: GetInstanceIfReady ist null - keine Leiste in diesem Inhalt.");
            _tolk.SpeakInterrupt(AccessibilityStrings.NoDutyActions);
            return;
        }

        var names = new List<string>();
        for (var i = 0; i < SlotCount; i++)
        {
            var id = mgr->ActionId[i];
            if (id != 0) names.Add(AccessibilityStrings.DutyActionSlot(i + 1, ActionName(id)));
        }

        if (names.Count == 0)
        {
            // Der andere Fall: es GIBT eine Leiste, aber alle Plaetze sind leer.
            // Die beiden auseinanderzuhalten entscheidet, wo man weitersucht.
            _log.Info($"[Sonderaktion] Abfrage: Leiste vorhanden, aber alle Plaetze leer " +
                      $"(NumValidSlots={mgr->NumValidSlots}, ActionsPresent={mgr->ActionsPresent}).");
            _tolk.SpeakInterrupt(AccessibilityStrings.NoDutyActions);
            return;
        }

        _log.Info($"[Sonderaktion] Abfrage: {string.Join(" | ", names)}");
        _tolk.SpeakInterrupt(AccessibilityStrings.DutyActionsAvailable(
            string.Join(", ", names), _config.KeyDutyAction1));
    }

    private string ActionName(uint actionId)
    {
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return AccessibilityStrings.AnAbility;
    }
}
