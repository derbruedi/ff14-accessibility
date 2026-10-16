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
    private readonly Configuration _config;
    private readonly IPluginLog   _log;
    private readonly JobProcService _procs;

    public CooldownService(IClientState clientState, IDataManager data, CueService cue,
                           Configuration config, IPluginLog log, IObjectTable objects)
    {
        _clientState = clientState;
        _data        = data;
        _cue         = cue;
        _config      = config;
        _log         = log;
        _procs       = new JobProcService(objects, clientState, data, log);
    }

    // Any action whose total recast is at or below this counts as a global-
    // cooldown skill (weaponskill/spell) and is ignored. The GCD is 2.5 s base
    // and only ever SHORTER with skill/spell speed; real ability cooldowns are
    // far longer (15 s+), so 3 s cleanly separates the two without relying on
    // the build-specific GCD recast-group id.
    private const float GcdRecastCeiling = 3.0f;

    // Alles ab dieser Id ist eine Zeile aus dem CraftAction-Sheet und damit
    // keine Action im Sinne des ActionManager (kleinste: 100001 Basic
    // Synthesis). Siehe die Erklaerung in EvaluateAction.
    private const uint CraftActionIdFloor = 100000;

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
    public unsafe void Update(ReadyAnnouncementBatch announcements)
    {
        if (!_config.AnnounceSkillReady || !_clientState.IsLoggedIn)
        {
            _procs.Reset();
            _lastCharges.Clear();
            _recastTotal.Clear();
            _trackedJob = byte.MaxValue;
            return;
        }

        var before = announcements.Count;
        _procs.Collect(announcements);
        if (announcements.Count > before) _cue.PlaySkillReadyTone();

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
                    EvaluateAction(am, id, level, announcements);
                    break;

                case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                    EvaluateGeneralAction(am, s, announcements);
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
    private unsafe void EvaluateGeneralAction(ActionManager* am, RaptureHotbarModule.HotbarSlot* s, ReadyAnnouncementBatch announcements)
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
            Announce(id, charges, 1, announcements);

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

    private unsafe void EvaluateAction(ActionManager* am, uint id, uint level, ReadyAnnouncementBatch announcements)
    {
        // ABSTURZ-SCHUTZ (2026-09-26, Nutzer-Report): eine Handwerksaktion auf
        // der Leiste legte Dalamud mit einer Zugriffsverletzung (C0000005) in
        // Client::Game::ActionManager.GetMaxCharges+0x23 um. Der Crash-Stack war
        // CooldownService.Update -> EvaluateAction -> GetMaxCharges, also genau
        // diese Zeile, und zwar im laufenden Bild-Takt, nicht beim Belegen.
        //
        // Ursache: der Hotbar-Slot einer Handwerksaktion traegt die Zeilen-Id aus
        // dem CraftAction-Sheet (100001 Basic Synthesis … 100371 Tricks of the
        // Trade auf Stufe 19), NICHT die Id einer Action-Zeile. GetMaxCharges ist
        // eine statische Bindung direkt in den Client (ohne thisPtr, also auch
        // ohne Instanz-Null-Check) und indiziert intern die Action-Liste des
        // Spielers; eine Id, die dort nicht existiert, laeuft aus dem Gatter und
        // reisst den Prozess mit. ActionName() unten liefert fuer so eine Id
        // laengst einen leeren String - dieselbe Pruefung, nur frueher.
        //
        // Die Mod kennzeichnet Handwerksaktionen nirgends als solche; der
        // Id-Bereich ist das einzige Signal, das der Slot hergibt. Handwerks-
        // aktionen brauchen hier nichts: sie haben keine Ladungen, und ein
        // GCD-Ausschluss ueber GcdRecastCeiling filtert sie ohnehin heraus
        // (Basis-Synthese 2,5 s).
        if (id >= CraftActionIdFloor) return;

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
            Announce(id, charges, maxCharges, announcements);

        _lastCharges[id] = charges;
    }

    private void Announce(uint id, uint charges, ushort maxCharges, ReadyAnnouncementBatch announcements)
    {
        var name = ActionName(id);
        if (string.IsNullOrEmpty(name)) return;

        var text = maxCharges > 1
            ? AccessibilityStrings.SkillChargeReady(name, charges, maxCharges)
            : AccessibilityStrings.SkillReady(name);

        _cue.PlaySkillReadyTone();
        announcements.Add(text);
        _log.Info($"[Cooldown] Bereit: '{name}' id={id} charges={charges}/{maxCharges}");
    }

    private string ActionName(uint id)
    {
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(id, out var row))
        {
            var n = RussianGameText.Name(_data, row, x => x.Name);
            if (!string.IsNullOrWhiteSpace(n)) return n;
        }
        return string.Empty;
    }
}
