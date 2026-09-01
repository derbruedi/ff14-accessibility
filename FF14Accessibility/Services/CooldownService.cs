using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// Watches the ability cooldowns on the player's action bars and, the moment an
/// ability finishes its cooldown, plays a short "ready" tone and speaks its
/// name - a blind player has no cooldown icons to glance at.
///
/// Only real abilities (oGCDs) are tracked: weaponskills and spells all share
/// the ~2.5 s global cooldown and would fire nonstop, so anything whose total
/// recast is at or below the GCD is skipped. Edge-triggered on charge count, so
/// nothing is announced until an ability actually comes back up.
///
/// Runs every frame from Plugin.OnFrameworkUpdate. ActionManager cooldown API
/// (GetRecastTime/GetCurrentCharges/GetMaxCharges), see docs/game-api.md ->
/// "Cooldown". Two probe-verified runtime facts drive the design (2026-07-31):
///   * GetCurrentCharges is the authoritative availability signal. IsRecast-
///     TimerActive is NOT: during the global-cooldown roll it briefly flips true
///     for an off-GCD ability even though its charge is untouched, which fired a
///     false "ready" every few seconds when the edge hung off it.
///   * GetRecastTime returns the real recast ONLY while a charge is missing and
///     reads 0 the instant the ability is ready - so the total is captured while
///     on cooldown and remembered to classify oGCD vs GCD on the rising edge.
/// </summary>
public sealed class CooldownService
{
    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly CueService   _cue;
    private readonly TolkService  _tolk;
    // Der zweite Sprachkanal. Die Bereit-Meldung geht hierueber, weil sie
    // mitten im Kampf faellt und der Screenreader dort von der naechsten Zeile
    // (Zauberleiste, Chat) geschnitten wird - User-Ansage 2026-08-31.
    private readonly WarningVoiceService _warnVoice;
    private readonly Configuration _config;
    private readonly IPluginLog   _log;

    public CooldownService(IClientState clientState, IDataManager data, CueService cue,
                           TolkService tolk, WarningVoiceService warnVoice,
                           Configuration config, IPluginLog log)
    {
        _clientState = clientState;
        _data        = data;
        _cue         = cue;
        _tolk        = tolk;
        _warnVoice   = warnVoice;
        _config      = config;
        _log         = log;
    }

    // Any action whose total recast is at or below this counts as a global-
    // cooldown skill (weaponskill/spell) and is ignored. The GCD is 2.5 s base
    // and only ever SHORTER with skill/spell speed; real ability cooldowns are
    // far longer (15 s+), so 3 s cleanly separates the two without relying on
    // the build-specific GCD recast-group id.
    private const float GcdRecastCeiling = 3.0f;

    // StandardHotbars = Hotbars[0..9]; 16 slots exist per bar (UI uses 12).
    private const int StandardBarCount = 10;
    private const int SlotsPerBar      = 16;

    // actionId -> usable charge count seen last frame; the rising edge of this is
    // the "a fresh use is available" signal for every tracked ability.
    private readonly Dictionary<uint, uint> _lastCharges = new();
    // actionId -> total recast (s) captured WHILE on cooldown. GetRecastTime
    // reads 0 the moment an ability is ready (probe-verified 2026-07-31), so this
    // is the only place its value is trustworthy; used to classify GCD vs oGCD on
    // the ready-edge, where GetRecastTime itself is already back to 0.
    private readonly Dictionary<uint, float> _recastTotal = new();
    // Reused each frame to dedupe actions that sit on several slots/bars.
    private readonly HashSet<uint> _seen = new();

    private byte _trackedJob = byte.MaxValue;

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public unsafe void Update()
    {
        if (!_config.AnnounceSkillReady) return;
        if (!_clientState.IsLoggedIn) return;

        var am      = ActionManager.Instance();
        var hotbars = RaptureHotbarModule.Instance();
        var ps      = PlayerState.Instance();
        if (am == null || hotbars == null || ps == null) return;

        // Reset tracking on a job change so switching jobs never fires a stale
        // "ready" (the new job's actions start from a clean slate).
        if (ps->CurrentClassJobId != _trackedJob)
        {
            _trackedJob = ps->CurrentClassJobId;
            _lastCharges.Clear();
            _recastTotal.Clear();
        }

        var level = (uint)ps->CurrentLevel;

        _seen.Clear();
        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsPerBar; slot++)
        {
            var s = hotbars->GetSlotById((uint)bar, (uint)slot);
            if (s == null) continue;

            // ALLGEMEINE AKTIONEN GEHOEREN DAZU (User 2026-08-31: "eine meldung
            // fehlt wenn sprint wieder verfuegbar ist"). Sprint liegt NICHT als
            // Action auf der Leiste, sondern als GeneralAction - ein eigener
            // Slot-Typ, den diese Schleife bisher wortlos uebersprungen hat.
            // Belegt: GeneralAction-Zeile 4 "Sprint" verweist auf Action 3, und
            // deren Recast100ms ist 600, also 60 s (Sheet-Dump 2026-08-31).
            switch (s->CommandType)
            {
                case RaptureHotbarModule.HotbarSlotType.Action:
                    var id = s->CommandId;
                    if (id == 0 || !_seen.Add(id)) continue;   // dedupe across slots/bars
                    EvaluateAction(am, id, level);
                    break;

                case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                    EvaluateGeneralAction(am, s);
                    break;
            }
        }
    }

    /// <summary>
    /// Eine ALLGEMEINE Aktion auf der Leiste (Sprint, Rueckfuehrung, Ausgraben …).
    /// Sie laeuft bewusst nicht durch <see cref="EvaluateAction"/>:
    ///
    /// <para>
    /// LADUNGEN KOMMEN VOM SLOT, nicht vom ActionManager. Der Slot rechnet die
    /// Zahl selbst aus, die auch sein Symbol anzeigt
    /// (<c>GetApparentIconRecastCharges</c>, laut FFXIVClientStructs-Doku "0 oder
    /// 1", wenn die Aktion keine Ladungen kennt) - und zwar unabhaengig vom
    /// Slot-Typ. <c>GetCurrentCharges</c> dagegen nimmt nur eine Action-Id und
    /// waere fuer eine GeneralAction-Zeile eine Verwechslung zweier Nummernkreise.
    /// </para>
    ///
    /// <para>
    /// DIE RESTZEIT kommt aus dem ActionManager, aber mit dem Typ, den das Spiel
    /// selbst fuer diesen Slot-Typ nennt (<c>GetActionTypeForSlotType</c>). Der
    /// Name und der Schluessel kommen dagegen von der ECHTEN Aktion hinter der
    /// Zeile (GeneralAction.Action), damit "Sprint" auch Sprint heisst und sich
    /// die Zeilennummer 4 nicht mit der Aktion 4 in denselben Toepfen mischt.
    /// </para>
    /// </summary>
    private unsafe void EvaluateGeneralAction(ActionManager* am, RaptureHotbarModule.HotbarSlot* s)
    {
        var row = s->CommandId;
        if (row == 0) return;

        // Zeilen ohne echte Aktion dahinter (Springen, Limitrausch, Faerben …)
        // haben keinen Recast, den man ansagen koennte.
        if (!_data.GetExcelSheet<GeneralAction>().TryGetRow(row, out var general)) return;
        var id = general.Action.RowId;
        if (id == 0 || !_seen.Add(id)) return;

        // Instanzmethode, obwohl sie laut Doku nichts aus dem Slot liest - also
        // ueber den Slot selbst aufgerufen.
        var type = s->GetActionTypeForSlotType(s->CommandType);
        if ((uint)type == uint.MaxValue) return;   // laut Doku: kein Typ gefunden

        var charges = s->GetApparentIconRecastCharges();

        if (charges < 1)
        {
            var recast = am->GetRecastTime(type, row);
            if (recast > 0f) _recastTotal[id] = recast;
        }

        if (_lastCharges.TryGetValue(id, out var prev) && charges > prev
            && _recastTotal.TryGetValue(id, out var total) && total > GcdRecastCeiling)
            Announce(id, charges, 1);

#if DEBUG
        // Zeigt beim Testen, ob dieser Zweig ueberhaupt Zahlen bekommt: ob der
        // Recast unter dem genannten Typ ankommt, ist NICHT bewiesen, nur
        // dokumentiert. Nur bei Aenderung, sonst schriebe es jeden Frame.
        if (!_lastCharges.TryGetValue(id, out var before) || before != charges)
            _log.Info($"[CooldownProbe] Allgemein Zeile={row} Aktion={id} Typ={type} " +
                      $"Ladungen={charges} gemerkter Recast=" +
                      $"{(_recastTotal.TryGetValue(id, out var t) ? t.ToString("0.0") : "-")}");
#endif

        _lastCharges[id] = charges;
    }

    private unsafe void EvaluateAction(ActionManager* am, uint id, uint level)
    {
        var maxCharges = ActionManager.GetMaxCharges(id, level);
        if (maxCharges < 1) maxCharges = 1;
        var charges = am->GetCurrentCharges(id);

        // Charge count is the authoritative "is a use available" signal - unlike
        // IsRecastTimerActive it is NOT polluted by the shared global-cooldown
        // lockout (probe-verified 2026-07-31: during the GCD roll IsRecast-
        // TimerActive briefly flips true while charges stay full and GetRecastTime
        // reads 0). While a charge is missing the ability is on its OWN cooldown,
        // which is the only moment GetRecastTime returns the real recast - capture
        // it here to classify oGCD vs GCD on the rising edge below.
        if (charges < maxCharges)
        {
            var recast = am->GetRecastTime(ActionType.Action, id);
            if (recast > 0f) _recastTotal[id] = recast;
        }

        // Announce on the rising edge of usable charges (a fresh charge landed),
        // but only for real abilities: GCD weaponskills/spells cycle their single
        // charge every ~2.5 s and would fire nonstop, so the remembered recast
        // must exceed GcdRecastCeiling. The TryGetValue guards mean a freshly seen
        // action never fires on sight, and one never caught on cooldown (no
        // remembered recast) stays silent until it has been used at least once.
        if (_lastCharges.TryGetValue(id, out var prev) && charges > prev
            && _recastTotal.TryGetValue(id, out var total) && total > GcdRecastCeiling)
            Announce(id, charges, maxCharges);

        _lastCharges[id] = charges;
    }

    private void Announce(uint id, uint charges, ushort maxCharges)
    {
        var name = ActionName(id);
        if (string.IsNullOrEmpty(name)) return;

        var text = maxCharges > 1
            ? AccessibilityStrings.SkillChargeReady(name, charges, maxCharges)
            : AccessibilityStrings.SkillReady(name);

        _cue.PlaySkillReadyTone();
        // Warnstimme zuerst, Screenreader als Rueckfall: Speak() gibt false
        // zurueck, wenn der Kanal aus oder nicht verfuegbar ist - dann darf die
        // Meldung nicht still verlorengehen.
        if (!_warnVoice.Speak(text)) _tolk.Speak(text);
        _log.Info($"[Cooldown] Bereit: '{name}' id={id} charges={charges}/{maxCharges}");
    }

    private string ActionName(uint id)
    {
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(id, out var row))
        {
            var n = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        return string.Empty;
    }
}
