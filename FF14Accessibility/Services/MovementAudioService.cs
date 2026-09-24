using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Free-walk audio cues: a one-shot bump when the character runs into an NPC,
/// player or solid scenery, and distinct rising/falling tones shortly before a
/// jump step or a drop. Driven every frame; mute rules live in
/// <see cref="CueService"/> / <see cref="GameWindowFocus"/>.
/// </summary>
public sealed class MovementAudioService
{
    /// <summary>Horizontal metres that count as "moved" this frame.</summary>
    private const float MovementEpsilon = 0.08f;

    /// <summary>How long after the last step the player still counts as walking.</summary>
    private const double RecentlyMovedSeconds = 0.45;

    /// <summary>How long without progress before a bump may fire (against a blocker).</summary>
    private const double StallSeconds = 0.18;

    /// <summary>Cooldown after a bump so holding into a wall does not rattle.</summary>
    private const double BumpCooldownSeconds = 1.0;

    /// <summary>How far ahead to look for height changes (metres).</summary>
    private const float LedgeProbeDistance = 2.0f;

    /// <summary>Same threshold as AutoWalkService.LedgeAnnounceRise / VerticalHint.</summary>
    private const float HeightThreshold = 1.5f;

    /// <summary>Cooldown after a ledge cue so stairs do not spam every frame.</summary>
    private const double LedgeCooldownSeconds = 1.2;

    /// <summary>Hysteresis: clear the "ledge ahead" latch only when |ΔY| falls below this.</summary>
    private const float LedgeClearThreshold = 0.8f;

    private readonly IObjectTable _objectTable;
    private readonly ICondition _condition;
    private readonly ObstacleService _obstacles;
    private readonly NavmeshIpc _nav;
    private readonly CueService _cue;
    private readonly Configuration _config;
    private readonly IPluginLog _log;
    private readonly Func<bool> _autoWalkBusy;

    private Vector3 _lastPosition;
    private bool _havePosition;
    private DateTime _lastMovedAt = DateTime.MinValue;
    private DateTime _stallSince = DateTime.MinValue;
    private bool _stalling;
    private DateTime _lastBumpAt = DateTime.MinValue;
    private DateTime _lastLedgeAt = DateTime.MinValue;
    private LedgeKind _ledgeLatched = LedgeKind.None;

    private enum LedgeKind { None, Jump, Drop }

    public MovementAudioService(
        IObjectTable objectTable,
        ICondition condition,
        ObstacleService obstacles,
        NavmeshIpc nav,
        CueService cue,
        Configuration config,
        IPluginLog log,
        Func<bool> autoWalkBusy)
    {
        _objectTable = objectTable;
        _condition = condition;
        _obstacles = obstacles;
        _nav = nav;
        _cue = cue;
        _config = config;
        _log = log;
        _autoWalkBusy = autoWalkBusy;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        if (!_config.AnnounceMovementCues || _config.MovementCueVolume <= 0f)
        {
            ResetMotion();
            return;
        }

        if (!GameWindowFocus.IsActive)
        {
            ResetMotion();
            return;
        }

        // Auto-walk already names blockers in speech; free-walk cues would only add noise.
        if (_autoWalkBusy())
        {
            ResetMotion();
            return;
        }

        if (_condition[ConditionFlag.InFlight] || _condition[ConditionFlag.Jumping])
        {
            ResetMotion();
            return;
        }

        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            ResetMotion();
            return;
        }

        var now = DateTime.UtcNow;
        var pos = player.Position;

        if (!_havePosition)
        {
            _lastPosition = pos;
            _havePosition = true;
            return;
        }

        var horizontal = HorizontalDistance(pos, _lastPosition);
        if (horizontal >= MovementEpsilon)
        {
            _lastMovedAt = now;
            _stalling = false;
            _lastPosition = pos;
            // Cleared the ledge latch once the player is moving again past a flat stretch.
            UpdateLedgeWhileMoving(player, now);
            return;
        }

        _lastPosition = pos;

        var recentlyMoved = (now - _lastMovedAt).TotalSeconds <= RecentlyMovedSeconds;
        if (!recentlyMoved)
        {
            _stalling = false;
            _ledgeLatched = LedgeKind.None;
            return;
        }

        // Still "walking" but not advancing: candidate bump.
        if (!_stalling)
        {
            _stalling = true;
            _stallSince = now;
        }

        if ((now - _stallSince).TotalSeconds >= StallSeconds)
            TryBump(player, now);

        // Ledge probe also while stalled into a climb (same facing).
        UpdateLedgeWhileMoving(player, now);
    }

    private void TryBump(Dalamud.Game.ClientState.Objects.Types.IGameObject player, DateTime now)
    {
        if ((now - _lastBumpAt).TotalSeconds < BumpCooldownSeconds) return;

        if (!TryFacingDirection(player, out var dir)) return;

        var ahead = player.Position + dir * 1.5f;
        if (!_obstacles.TryFindBlocker(player.Position, ahead, out var description))
            return;

        _lastBumpAt = now;
        _cue.PlayBumpTone();
        _log.Info($"[Bewegung] Anstoß{(description != null ? $": {description}" : "")}.");
    }

    private void UpdateLedgeWhileMoving(Dalamud.Game.ClientState.Objects.Types.IGameObject player, DateTime now)
    {
        if (!_nav.IsReady) return;
        if (!TryFacingDirection(player, out var dir)) return;

        var probe = player.Position + dir * LedgeProbeDistance;
        // Prefer floor under the probe; fall back to nearest mesh in a tall box.
        var floor = _nav.PointOnFloor(probe, 2f)
                    ?? _nav.NearestPoint(probe, 2f, 8f);

        LedgeKind kind;
        if (floor == null)
        {
            // No walkable surface ahead in range: treat as a gap that needs a jump
            // (or a void) - rising cue, same as climb.
            kind = LedgeKind.Jump;
        }
        else
        {
            var dy = floor.Value.Y - player.Position.Y;
            if (dy <= -HeightThreshold) kind = LedgeKind.Drop;
            else if (dy >= HeightThreshold) kind = LedgeKind.Jump;
            else kind = LedgeKind.None;
        }

        if (kind == LedgeKind.None)
        {
            if (_ledgeLatched != LedgeKind.None && floor != null
                && MathF.Abs(floor.Value.Y - player.Position.Y) < LedgeClearThreshold)
            {
                _ledgeLatched = LedgeKind.None;
            }
            return;
        }

        if (kind == _ledgeLatched) return;
        if ((now - _lastLedgeAt).TotalSeconds < LedgeCooldownSeconds) return;

        _ledgeLatched = kind;
        _lastLedgeAt = now;
        if (kind == LedgeKind.Drop)
        {
            _cue.PlayDropAheadTone();
            _log.Info("[Bewegung] Absturz voraus.");
        }
        else
        {
            _cue.PlayJumpAheadTone();
            _log.Info("[Bewegung] Sprung/Stufe voraus.");
        }
    }

    private static bool TryFacingDirection(
        Dalamud.Game.ClientState.Objects.Types.IGameObject player, out Vector3 direction)
    {
        // Camera-relative movement (game default): forward is where the camera looks.
        var yaw = FacingService.CameraFacing() ?? player.Rotation;
        direction = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        return direction.LengthSquared() > 0.0001f;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private void ResetMotion()
    {
        _havePosition = false;
        _stalling = false;
        _ledgeLatched = LedgeKind.None;
    }
}
