using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
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
    private readonly Configuration _config;
    private readonly IPluginLog   _log;

    public CooldownService(IClientState clientState, IDataManager data, CueService cue,
                           TolkService tolk, Configuration config, IPluginLog log)
    {
        _clientState = clientState;
        _data        = data;
        _cue         = cue;
        _tolk        = tolk;
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

#if DEBUG
    // Audit probe: throttled to once per second so a single combat test produces
    // a readable snapshot of exactly what the scanner sees. Delete with the probe
    // once the feature is verified in-game.
    private long _lastProbeMs;
    private bool _probeThisFrame;
    private int  _probeActionCount;
#endif

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

#if DEBUG
        var nowMs = System.Environment.TickCount64;
        _probeThisFrame = nowMs - _lastProbeMs >= 1000;
        if (_probeThisFrame) { _lastProbeMs = nowMs; _probeActionCount = 0; }
#endif

        _seen.Clear();
        for (var bar = 0; bar < StandardBarCount; bar++)
        for (var slot = 0; slot < SlotsPerBar; slot++)
        {
            var s = hotbars->GetSlotById((uint)bar, (uint)slot);
            if (s == null) continue;
            if (s->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
            {
#if DEBUG
                // Sprint and other General Actions land here: the scanner only
                // handles Action slots, so this line explains why they never fire.
                if (_probeThisFrame && s->CommandId != 0 &&
                    s->CommandType == RaptureHotbarModule.HotbarSlotType.GeneralAction)
                    _log.Info($"[CooldownProbe]   skip GeneralAction id={s->CommandId} (bar {bar} slot {slot})");
#endif
                continue;
            }

            var id = s->CommandId;
            if (id == 0 || !_seen.Add(id)) continue;   // dedupe across slots/bars

            EvaluateAction(am, id, level);
        }

#if DEBUG
        if (_probeThisFrame)
            _log.Info($"[CooldownProbe] job={ps->CurrentClassJobId} lvl={level} " +
                      $"actionSlotsFound={_probeActionCount} (>{GcdRecastCeiling}s recast = tracked oGCD)");
#endif
    }

    private unsafe void EvaluateAction(ActionManager* am, uint id, uint level)
    {
#if DEBUG
        // Log every scanned Action BEFORE the GCD filter, so the snapshot shows
        // both the GCD skills we drop and the oGCDs we keep, with their real
        // runtime recast/charge/active values.
        if (_probeThisFrame)
        {
            _probeActionCount++;
            var rt  = am->GetRecastTime(ActionType.Action, id);
            var mc  = ActionManager.GetMaxCharges(id, level);
            var act = am->IsRecastTimerActive(ActionType.Action, id);
            var ch  = am->GetCurrentCharges(id);
            _log.Info($"[CooldownProbe]   id={id} '{ActionName(id)}' recast={rt:F1}s " +
                      $"maxCharges={mc} charges={ch} active={act} " +
                      $"{(rt > GcdRecastCeiling ? "-> TRACKED" : "-> skipped (GCD)")}");
        }
#endif

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

        _cue.PlaySkillReadyTone();
        _tolk.Speak(maxCharges > 1
            ? AccessibilityStrings.SkillChargeReady(name, charges, maxCharges)
            : AccessibilityStrings.SkillReady(name));
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
