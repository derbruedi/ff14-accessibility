#if DEBUG
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaLogMessage = Lumina.Excel.Sheets.LogMessage;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer die eine Frage, an der die Job-Anzeigen haengen: WELCHES
/// SIGNAL DES SPIELS SAGT EHRLICH, OB EINE AKTION JETZT EINSETZBAR IST?
///
/// <para>
/// WARUM ES DIESE SONDE BRAUCHT. Aus dem Sheet ist abgelesen (2026-09-01), dass
/// jede Aktion ihre Kosten selbst nennt: <c>PrimaryCostType</c> +
/// <c>PrimaryCostValue</c>. Aber der Typ ist KEINE Menge - die drei
/// Beschwoerungen tragen Typ 71 mit den Werten 1/2/3, das ist eine Kennung,
/// welche Primae gemeint ist. Ein Vergleich "Anzeige >= Kosten" waere also
/// stellenweise schlicht falsch, und die Zuordnung "Typ 22 = Zorn-Anzeige" ist
/// aus der Job-Verteilung ERSCHLOSSEN, nicht dokumentiert: weder Dalamud noch
/// FFXIVClientStructs kennen ein Enum dafuer. Die Anzeige selbst nachzurechnen
/// hiesse Spiellogik im Mod zu verdoppeln - genau das, was die Projektregel
/// "Read, never recompute" verbietet.
/// </para>
///
/// <para>
/// DREI KANDIDATEN, alle vom Spiel selbst beantwortet, alle im Quellcode
/// vorhanden und keiner davon zur Laufzeit gemessen:
/// <list type="number">
/// <item><c>GetActionStatus</c> - gibt 0 zurueck, wenn die Aktion einsetzbar
/// ist, sonst eine LogMessage-Nummer mit dem Grund. Offene Frage: schlaegt
/// dort auch "kein Ziel" oder "ausser Reichweite" durch? Dann waere die
/// steigende Flanke Krach statt Auskunft, denn sie feuerte bei jedem
/// Zielwechsel. Deshalb wird sie hier DREIMAL gefragt: ohne Ziel, mit dem
/// aktuellen Ziel, und einmal mit Abklingzeit-Pruefung.</item>
/// <item><c>IsActionHighlighted</c> - das Leuchten des Symbols, also genau das,
/// was ein sehender Spieler sieht (Kombo-Fortsetzung, Procs). Offene Frage:
/// leuchtet es auch, wenn eine Anzeige voll genug ist?</item>
/// <item><c>CheckActionResources</c> - dem Namen nach die reine
/// Ressourcen-Pruefung, ohne Ziel und ohne Abklingzeit. Der dritte Parameter
/// ist im Quellcode ein blanker <c>void*</c> mit Vorgabe null; ob die Funktion
/// damit ueberhaupt sinnvoll antwortet, sagt keine der beiden DLLs.</item>
/// </list>
/// </para>
///
/// <para>
/// DIE FEHLERNUMMERN WERDEN GLEICH UEBERSETZT. Der Rueckgabewert von
/// <c>GetActionStatus</c> ist eine Zeile im LogMessage-Sheet; die Sonde schlaegt
/// sie nach und schreibt den Text mit ins Log. Ohne das waere jede Messung eine
/// Zahl, die eine zweite Runde zum Nachschlagen kostet.
/// </para>
///
/// <para>
/// ZUSAETZLICH DIE KOMBO. <c>ActionManager.Combo</c> fuehrt Timer und die
/// zuletzt gesetzte Kombo-Aktion, und im Sheet steht bei jeder Aktion unter
/// <c>ActionCombo</c>, worauf sie folgt. Beides wird mitgeloggt, weil die
/// Kombo-Ansage (der zweite Teil des Auftrags) auf denselben Zahlen steht und
/// im selben Testlauf mit abfaellt.
/// </para>
///
/// <para>
/// SCHNAPPSCHUSS STATT DAUERLOG - bewusst. Ein Frame-Log dieser Groesse waere
/// 60 Bloecke je Sekunde. Der Spieler loest die Messung an der Stelle aus, an
/// der die Frage steht (mitten in der Kombo, bei voller Anzeige), und genau
/// dieser eine Zustand landet im Log.
/// </para>
///
/// <para>Nach der Sonden-Konvention faellt diese Datei weg, sobald die
/// Mechanik steht.</para>
/// </summary>
public sealed class ActionSignalProbe
{
    // Dieselbe Zielkennung, die das Spiel selbst als Vorgabe benutzt, wenn
    // nichts anvisiert ist (ActionManager.UseAction, Vorgabewert 0xE0000000).
    private const ulong NoTarget = 3758096384uL;

    // StandardHotbars = Leisten 0..9, 16 Plaetze je Leiste - dieselben Grenzen
    // wie im CooldownService, damit beide dasselbe Feld sehen.
    private const int StandardBarCount = 10;
    private const int SlotsPerBar      = 16;

    private readonly IDataManager   _data;
    private readonly ITargetManager _targets;
    private readonly IJobGauges     _gauges;
    private readonly TolkService    _tolk;
    private readonly IPluginLog     _log;

    public ActionSignalProbe(IDataManager data, ITargetManager targets, IJobGauges gauges,
                             TolkService tolk, IPluginLog log)
    {
        _data    = data;
        _targets = targets;
        _gauges  = gauges;
        _tolk    = tolk;
        _log     = log;
    }

    /// <summary>Ein Schnappschuss aller Leisten-Aktionen mit allen Kandidaten-Signalen.</summary>
    public unsafe void Dump()
    {
        var am      = ActionManager.Instance();
        var hotbars = RaptureHotbarModule.Instance();
        var ps      = PlayerState.Instance();
        if (am == null || hotbars == null || ps == null)
        {
            _log.Warning("[ActionProbe] ActionManager/Hotbars/PlayerState nicht verfuegbar.");
            _tolk.Speak("Sonde nicht moeglich.");
            return;
        }

        var job    = ps->CurrentClassJobId;
        var level  = ps->CurrentLevel;
        var target = _targets.Target;

        _log.Info("[ActionProbe] ===================================================");
        _log.Info($"[ActionProbe] Job={job} Stufe={level} " +
                  $"Ziel={(target == null ? "keins" : $"{target.Name} (0x{target.GameObjectId:X})")}");
        _log.Info($"[ActionProbe] Kombo: Timer={am->Combo.Timer:0.00}s " +
                  $"letzteAktion={am->Combo.Action} ({NameOf(am->Combo.Action)})");
        LogGauge(job);
        _log.Info("[ActionProbe] Spalten: Status0=ohne Ziel, StatusZ=mit Ziel, " +
                  "StatusCD=mit Abklingzeit-Pruefung, Leuchtet, Ressourcen");

        var seen = new HashSet<uint>();
        var lines = new List<string>();

        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsPerBar; slot++)
        {
            var s = hotbars->GetSlotById((uint)bar, (uint)slot);
            if (s == null) continue;
            if (s->CommandType != RaptureHotbarModule.HotbarSlotType.Action) continue;

            var id = s->CommandId;
            if (id == 0 || !seen.Add(id)) continue;

            lines.Add(Describe(am, id, target?.GameObjectId ?? NoTarget));
        }

        foreach (var line in lines) _log.Info(line);
        _log.Info($"[ActionProbe] {lines.Count} Aktionen auf den Leisten.");
        _tolk.Speak($"Sonde: {lines.Count} Aktionen ins Log geschrieben.");
    }

    private unsafe string Describe(ActionManager* am, uint id, ulong targetId)
    {
        // Die Kosten stehen im Sheet - hier nur gelesen, nicht gedeutet.
        byte costType = 0, jobLevel = 0;
        ushort costValue = 0;
        uint combo = 0;
        var name = "?";
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(id, out var row))
        {
            name      = row.Name.ToString();
            costType  = row.PrimaryCostType;
            costValue = row.PrimaryCostValue;
            jobLevel  = row.ClassJobLevel;
            combo     = row.ActionCombo.RowId;
        }

        // Ohne Ziel und ohne Abklingzeit: das waere das Signal fuer eine reine
        // Ressourcen-Ansage. Mit Ziel daneben, um zu sehen, ob das Ziel
        // ueberhaupt hineinspielt - genau die offene Frage.
        var st0  = am->GetActionStatus(ActionType.Action, id, NoTarget, false, false);
        var stT  = am->GetActionStatus(ActionType.Action, id, targetId, false, false);
        var stCd = am->GetActionStatus(ActionType.Action, id, targetId, true,  true);

        var glow = am->IsActionHighlighted(ActionType.Action, id);
        var res  = am->CheckActionResources(ActionType.Action, id);

        return $"[ActionProbe] id={id,6} {Trim(name, 24)} St={jobLevel,3} " +
               $"KTyp={costType,3} KWert={costValue,4} folgtAuf={combo,6} " +
               $"Status0={st0}{Reason(st0)} StatusZ={stT}{Reason(stT)} " +
               $"StatusCD={stCd}{Reason(stCd)} Leuchtet={(glow ? "JA" : "nein")} " +
               $"Ressourcen={res}{Reason(res)}";
    }

    /// <summary>Der Klartext hinter einer Statusnummer. 0 heisst einsetzbar und
    /// hat keine Zeile; jede andere Nummer ist eine LogMessage-Zeile.</summary>
    private string Reason(uint status)
    {
        if (status == 0) return "(ok)";
        if (_data.GetExcelSheet<LuminaLogMessage>().TryGetRow(status, out var msg))
        {
            var text = msg.Text.ToString().Replace("\n", " ").Trim();
            if (text.Length > 0) return $"(\"{Trim(text, 44)}\")";
        }
        return "(unbekannt)";
    }

    /// <summary>Die Anzeige der beiden Jobs, um die es zuerst geht. Bewusst nur
    /// diese zwei: die Sonde soll den Testlauf belegen, nicht 22 Jobs
    /// vorwegnehmen.</summary>
    private void LogGauge(byte job)
    {
        switch (job)
        {
            case 21:   // Krieger
                var war = _gauges.Get<Dalamud.Game.ClientState.JobGauge.Types.WARGauge>();
                if (war != null) _log.Info($"[ActionProbe] Zorn-Anzeige={war.BeastGauge}");
                break;
            case 27:   // Beschwoerer
                var smn = _gauges.Get<Dalamud.Game.ClientState.JobGauge.Types.SMNGauge>();
                if (smn != null)
                    _log.Info($"[ActionProbe] AetherFlags=0x{(byte)smn.AetherFlags:X2} " +
                              $"Stapel={smn.AetherflowStacks} Ifrit={smn.IsIfritReady} " +
                              $"Titan={smn.IsTitanReady} Garuda={smn.IsGarudaReady}");
                break;
            case 3:    // Marodeur - hat nachweislich keine Anzeige (Sheet 2026-09-01)
                _log.Info("[ActionProbe] Marodeur: keine Job-Anzeige (keine Aktion mit Anzeige-Kosten).");
                break;
        }
    }

    private string NameOf(uint actionId)
    {
        if (actionId == 0) return "keine";
        return _data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row)
            ? row.Name.ToString()
            : "?";
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s.PadRight(max) : s.Substring(0, max);
}
#endif
