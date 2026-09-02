#if DEBUG
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer den Flugzustand: was sagt das Spiel gerade ueber dieses
/// Gebiet, das Reittier und die Aetherstroeme?
///
/// <para>
/// WAS SIE NOCH KLAeRT. Seit dem Umbau auf "der Spieler hebt selbst ab"
/// (2026-09-01) steuert <c>IsAetherCurrentZoneComplete</c> NICHTS mehr - ob
/// geflogen wird, entscheidet allein <c>ConditionFlag.InFlight</c>, und wer in
/// der Luft ist, darf offenkundig fliegen. Die Pruefung liefert nur noch die
/// Begruendung fuer <c>/acc fly</c>, und GENAU DAFUER ist sie noch ungemessen:
/// alle Gebiete des Grundspiels teilen sich Satz 19, haben aber gar keine
/// Aetherstroeme zu sammeln - dort haengt das Fliegen an der Hauptgeschichte.
/// Ob Satz 19 damit gesetzt wird, steht in keiner der beiden DLLs.
/// </para>
///
/// <para>
/// DER GEGENTEST IST DER SPIELER SELBST. Er kann fliegen. Meldet die Sonde in
/// einem Gebiet des Grundspiels <c>komplett=False</c>, waehrend er dort abhebt,
/// dann taugt die Zeile als Begruendung nicht und gehoert aus
/// <c>/acc fly</c> entfernt. Ein FEHLVERHALTEN loest sie nicht mehr aus - das ist
/// der Gewinn des Umbaus.
/// </para>
///
/// <para>
/// Ausserdem gemeldet: was das Spiel zu Absteigen (23) sagt, und zu den beiden
/// Reittier-Roulettes (24, 9), die das Plugin selbst nicht mehr benutzt - fuer
/// die Frage "warum komme ich hier nicht aufs Reittier" sind sie trotzdem die
/// schnellste Auskunft. <c>GetActionStatus</c> gibt 0 zurueck, wenn die Aktion
/// einsetzbar ist, sonst eine LogMessage-Nummer mit dem Grund.
/// </para>
///
/// <para>Aufruf: <c>/acc flyprobe</c>. Nach Abschluss des Features loeschen
/// (Konvention, siehe die uebrigen Sonden im Ordner).</para>
/// </summary>
public sealed class FlightProbe
{
    private const ulong NoTarget = 0xE0000000;

    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IDataManager _data;
    private readonly FlightService _flight;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public FlightProbe(IClientState clientState, ICondition condition, IDataManager data,
                       FlightService flight, TolkService tolk, IPluginLog log)
    {
        _clientState = clientState;
        _condition = condition;
        _data = data;
        _flight = flight;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>Writes the whole flight state to the Dalamud log and speaks a
    /// one-line summary, so the measurement can be taken without alt-tabbing.</summary>
    public unsafe void Dump()
    {
        var territoryId = _clientState.TerritoryType;
        var row = _data.GetExcelSheet<LuminaTerritoryType>()?.GetRowOrDefault(territoryId);

        _log.Info("──────── [Flugsonde] ────────");

        if (row == null)
        {
            _log.Warning($"[Flugsonde] Gebiet {territoryId} steht nicht im TerritoryType-Sheet.");
            _tolk.SpeakInterrupt("Flugsonde: Gebiet unbekannt.");
            return;
        }

        var t = row.Value;
        var use = t.TerritoryIntendedUse.RowId;
        var set = t.AetherCurrentCompFlgSet.RowId;
        var place = t.PlaceName.ValueNullable?.Name.ExtractText() ?? "?";

        _log.Info($"[Flugsonde] Gebiet {territoryId} \"{place}\"");
        _log.Info($"[Flugsonde]   TerritoryIntendedUse = {use}  " +
                  $"(vnavmesh baut ein Flugvolumen nur fuer 1, 47, 49)");
        _log.Info($"[Flugsonde]   Mount erlaubt        = {t.Mount}");
        _log.Info($"[Flugsonde]   AetherCurrentCompFlgSet = {set}  (0 = kein Satz genannt)");

        // Nur noch Begruendung, kein Schalter mehr. Setz sie neben die Tatsache,
        // ob der Spieler hier abheben kann - stimmen sie nicht ueberein, taugt die
        // Zeile als Auskunft nicht.
        var complete = set != 0 && IsComplete(set);
        _log.Info($"[Flugsonde]   IsAetherCurrentZoneComplete({set}) = {complete}   " +
                  "<<< nur Auskunft fuer /acc fly, steuert nichts");

        _log.Info($"[Flugsonde]   Flugstrecken vorhanden (steuert den Lauf): {_flight.HasFlightRoutes}");
        _log.Info($"[Flugsonde]   Auskunft fuer /acc fly: {_flight.Blocked()}");

        _log.Info($"[Flugsonde]   Zustand: aufgesessen={_flight.IsMounted}, " +
                  $"in der Luft={_flight.IsInFlight}, sitzt gerade auf={_flight.IsMounting}, " +
                  $"Mitfahrer={_condition[ConditionFlag.RidingPillion]}, " +
                  $"im Kampf={_condition[ConditionFlag.InCombat]}");

        var am = ActionManager.Instance();
        if (am == null)
        {
            _log.Warning("[Flugsonde]   ActionManager nicht verfuegbar.");
        }
        else
        {
            foreach (var (id, name) in new (uint, string)[]
                     {
                         (24, "Flugreittier-Roulette"),
                         (9,  "Reittier-Roulette"),
                         (23, "Absteigen"),
                     })
            {
                var status = am->GetActionStatus(ActionType.GeneralAction, id, NoTarget, false, false);
                _log.Info($"[Flugsonde]   GeneralAction {id,2} \"{name}\": Status {status} " +
                          $"({(status == 0 ? "einsetzbar" : "gesperrt")})");
            }
        }

        _log.Info("─────────────────────────────");

        // Gesprochen nur das Nötigste - der Rest steht im Log, und der Spieler
        // liest es dort ohnehin nach.
        _tolk.SpeakInterrupt(
            $"Flugsonde. Gebiet {place}. Nutzung {use}, Ätherstrom-Satz {set}, komplett {(complete ? "ja" : "nein")}. " +
            $"Urteil {_flight.Blocked()}.");
    }

    private unsafe bool IsComplete(uint set)
    {
        var state = PlayerState.Instance();
        if (state == null) return false;
        return state->IsAetherCurrentZoneComplete(set);
    }
}
#endif
