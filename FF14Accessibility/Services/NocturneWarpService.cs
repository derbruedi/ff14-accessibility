using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace FF14Accessibility.Services;

/// <summary>
/// Warp-strike assist for the FFXV collab duty <b>Messenger of the Winds</b>
/// (DE „Durch den Sturm und zurück“, Territory 834) only. Modes:
/// <see cref="NocturneWarpMode.Off"/>, <see cref="NocturneWarpMode.Manual"/>
/// (sound + target when Warp is due), <see cref="NocturneWarpMode.Auto"/>
/// (target + <c>ExecuteDutyActionSlot</c>).
///
/// Sheet facts (offline sqpack 2026-09-24): Warp-Angriff 14595/14597,
/// Mistral-Schrei 14611 (circle r=30), Mikro-Explosion 14619, Tornado der Bosheit
/// 14613, BNpcName Monolith 1649 / Garuda 7893.
///
/// WORKAROUND: cast-progress thresholds (when to fire). The client has no
/// „warp now“ flag; sighted players read the cast bar.
/// </summary>
public sealed class NocturneWarpService
{
    private const ushort DutyTerritoryId = 834;

    private const uint WarpStrikeA = 14595;
    private const uint WarpStrikeB = 14597;
    private const uint MistralShriek = 14611;
    private const uint Microburst = 14619;
    private const uint WickedTornado = 14613;

    private const uint MonolithNameId = 1649;
    private const uint GarudaNameId = 7893;

    // WORKAROUND: no game cue for „warp now“ — fractions of Current/Total cast.
    private const float MistralWarpOutProgress = 0.08f;
    private const float MicroburstWarpProgress = 0.88f;
    private const float TornadoWarpProgress = 0.10f;

    private const float MinSecondsBetweenAssists = 0.55f;

    private readonly IClientState _clientState;
    private readonly IObjectTable _objects;
    private readonly ITargetManager _targets;
    private readonly CueService _cue;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private int _mistralPhase; // 0 idle, 1 assisted out to monolith, waiting for cast end
    private uint _mistralCastAction;
    private ulong _mistralCasterId;
    private ulong _handledCastKey;
    private long _lastAssistTicks;

    public NocturneWarpService(
        IClientState clientState,
        IObjectTable objects,
        ITargetManager targets,
        CueService cue,
        Configuration config,
        IPluginLog log)
    {
        _clientState = clientState;
        _objects = objects;
        _targets = targets;
        _cue = cue;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Every framework tick. No-ops outside Territory 834, without Warp on the
    /// duty bar, or when mode is Off — so it never runs for other content.
    /// </summary>
    public unsafe void Update()
    {
        var mode = _config.NocturneWarpMode;
        if (mode == NocturneWarpMode.Off) return;
        if (!GameWindowFocus.IsActive) return;
        if (!_clientState.IsLoggedIn) return;
        if (_clientState.TerritoryType != DutyTerritoryId)
        {
            ResetDutyState();
            return;
        }

        if (!TryFindWarpSlot(out var slot))
        {
            ResetDutyState();
            return;
        }

        var garuda = FindGaruda();
        if (garuda == null) return;

        // Finish Mistral: after shriek ends, assist warp back to Garuda.
        if (_mistralPhase == 1)
        {
            var stillShrieking = garuda.IsCasting
                && garuda.GameObjectId == _mistralCasterId
                && garuda.CastActionId == _mistralCastAction;
            if (!stillShrieking)
            {
                if (TryAssist(mode, slot, garuda, "Mistral→Garuda Link"))
                    _mistralPhase = 0;
            }
            return;
        }

        if (!garuda.IsCasting) return;

        var castId = garuda.CastActionId;
        var progress = CastProgress(garuda);
        var castKey = garuda.GameObjectId ^ ((ulong)castId << 32);

        if (castId == MistralShriek)
        {
            if (progress < MistralWarpOutProgress) return;
            if (_handledCastKey == castKey && _mistralPhase != 0) return;

            var mono = FindSafeMonolith(garuda.Position, 30f);
            if (mono == null) mono = FindFarthestMonolith(garuda.Position);
            if (mono == null) return;

            if (TryAssist(mode, slot, mono, "Mistral→Monolith"))
            {
                _handledCastKey = castKey;
                _mistralPhase = 1;
                _mistralCasterId = garuda.GameObjectId;
                _mistralCastAction = castId;
            }
            return;
        }

        if (castId == Microburst)
        {
            if (progress < MicroburstWarpProgress) return;
            if (_handledCastKey == castKey) return;

            var mono = FindFarthestMonolith(garuda.Position);
            if (mono == null) return;
            if (TryAssist(mode, slot, mono, "Microburst→Monolith"))
                _handledCastKey = castKey;
            return;
        }

        if (castId == WickedTornado)
        {
            if (progress < TornadoWarpProgress) return;
            if (_handledCastKey == castKey) return;
            if (TryAssist(mode, slot, garuda, "Tornado→Garuda"))
                _handledCastKey = castKey;
        }
    }

    private void ResetDutyState()
    {
        _mistralPhase = 0;
        _mistralCastAction = 0;
        _mistralCasterId = 0;
        _handledCastKey = 0;
    }

    private static float CastProgress(IBattleChara caster)
    {
        var total = caster.TotalCastTime;
        if (total <= 0.05f) return 0f;
        return Math.Clamp(caster.CurrentCastTime / total, 0f, 1f);
    }

    private unsafe bool TryFindWarpSlot(out uint slotIndex)
    {
        slotIndex = 0;
        var mgr = DutyActionManager.GetInstanceIfReady();
        if (mgr == null) return false;

        for (var i = 0; i < 5; i++)
        {
            var id = mgr->ActionId[i];
            if (id == WarpStrikeA || id == WarpStrikeB)
            {
                slotIndex = (uint)i;
                return true;
            }
        }
        return false;
    }

    private IBattleChara? FindGaruda()
    {
        IBattleChara? byName = null;
        foreach (var obj in _objects)
        {
            if (obj is not IBattleChara bc) continue;
            if (bc.ObjectKind != ObjectKind.BattleNpc) continue;
            if (!bc.IsTargetable || bc.IsDead) continue;
            if (bc.NameId == GarudaNameId) return bc;
            if (byName == null
                && bc.Name.TextValue.Equals("Garuda", StringComparison.OrdinalIgnoreCase))
                byName = bc;
        }
        return byName;
    }

    private IBattleChara? FindSafeMonolith(Vector3 fromGaruda, float minDistance)
    {
        IBattleChara? best = null;
        var bestDist = float.MinValue;
        foreach (var mono in EnumerateMonoliths())
        {
            var d = Dist2D(fromGaruda, mono.Position);
            if (d < minDistance) continue;
            if (d > bestDist)
            {
                bestDist = d;
                best = mono;
            }
        }
        return best;
    }

    private IBattleChara? FindFarthestMonolith(Vector3 fromGaruda)
    {
        IBattleChara? best = null;
        var bestDist = float.MinValue;
        foreach (var mono in EnumerateMonoliths())
        {
            var d = Dist2D(fromGaruda, mono.Position);
            if (d > bestDist)
            {
                bestDist = d;
                best = mono;
            }
        }
        return best;
    }

    private System.Collections.Generic.IEnumerable<IBattleChara> EnumerateMonoliths()
    {
        foreach (var obj in _objects)
        {
            if (obj is not IBattleChara bc) continue;
            if (!bc.IsTargetable || bc.IsDead) continue;
            if (bc.NameId == MonolithNameId) yield return bc;
            else if (bc.Name.TextValue.Contains("Monolith", StringComparison.OrdinalIgnoreCase))
                yield return bc;
        }
    }

    /// <summary>
    /// Sets the Warp target. Auto also executes the duty action; Manual only
    /// plays the cue so the player can press Umschalt+F10.
    /// </summary>
    private unsafe bool TryAssist(NocturneWarpMode mode, uint slotIndex, IGameObject target, string reason)
    {
        var now = Environment.TickCount64;
        if (now - _lastAssistTicks < (long)(MinSecondsBetweenAssists * 1000))
            return false;

        _targets.Target = target;
        if (_targets.Target?.GameObjectId != target.GameObjectId)
        {
            _log.Info($"[NocturneWarp] Target-Set abgelehnt ({reason}) id={target.GameObjectId:X}");
            return false;
        }

        if (mode == NocturneWarpMode.Manual)
        {
            _lastAssistTicks = now;
            _cue.PlaySkillReadyTone();
            _log.Info(
                $"[NocturneWarp] Manuell-Hinweis {reason} Ziel='{target.Name.TextValue}' " +
                $"(Umschalt+F10)");
            return true;
        }

        // Auto
        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null) return false;

        var ok = hotbar->ExecuteDutyActionSlot(slotIndex);
        _lastAssistTicks = now;
        _log.Info(
            $"[NocturneWarp] Auto {reason} Ziel='{target.Name.TextValue}' slot={slotIndex} -> {ok}");
        if (ok) _cue.PlaySkillReadyTone();
        return ok;
    }

    private static float Dist2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
