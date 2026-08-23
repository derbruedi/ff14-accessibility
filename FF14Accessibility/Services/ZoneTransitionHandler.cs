using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// The last two metres into a zone line. A walk can end just short of the trigger
/// box - vnavmesh aims for its stop range, the mesh may thin out before the border,
/// and standing next to a zone line does nothing at all. This nudges the character
/// the rest of the way in.
///
/// <para>
/// WHAT IS MEASURED. The trigger fires on entering the <c>ExitRange</c> box, and
/// <c>Transform.Scale</c> of that box is the HALF extent - established in-game on
/// 2026-08-22 with the ZoneExitProbe, twice through the same border in both
/// directions: under the half reading the character was inside roughly a second
/// before the change, under the full reading never, while the change demonstrably
/// happened. So "inside the box" is a fact we can aim at, not a guess.
/// </para>
///
/// <para>
/// WHY THE PUSH IS DRIVEN WITH <c>Path.MoveTo</c> rather than synthetic key
/// presses: it is the same call the recorded trails use, it steers without
/// pathfinding (so a thin mesh cannot refuse it), and it stops when we stop it. A
/// held-down key would keep pushing into whatever is in the way.
/// </para>
///
/// <para>
/// AND WHY IT IS NARROW. The push only ever runs when the border is already within
/// <see cref="MaxNudgeDistance"/> and the character is not moving any more. It
/// stops the moment the zone changes, the time runs out, or nothing moves - a nudge
/// that achieves nothing must not turn into a character pressed against a wall.
/// </para>
/// </summary>
public sealed class ZoneTransitionHandler
{
    /// <summary>Only nudge when the border is this close. Beyond that the walk did
    /// not "just fall short" - something is in the way, and pushing blindly would
    /// grind the character into it (the barrels at the Gridania gate, measured with
    /// tools/zone-probe on 2026-08-22).</summary>
    private const float MaxNudgeDistance = 6f;

    /// <summary>How long the push may run. The measured transition delay was 0,8 to
    /// 1,0 s in both directions, plus walking time for a couple of metres.</summary>
    private const double NudgeSeconds = 3.0;

    /// <summary>Give up early when nothing moves - pushing into geometry.</summary>
    private const double StallSeconds = 1.2;

    /// <summary>Counts as movement (below this is jitter against collision).</summary>
    private const float MovementEpsilon = 0.3f;

    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly NavmeshIpc _nav;
    private readonly TolkService _tolk;
    /// <summary>Names the culprit when the push achieves nothing. Optional - without
    /// it the announcement stays the honest vague one it was before.</summary>
    private readonly ObstacleService? _obstacles;
    private readonly IPluginLog _log;

    private bool _active;
    private DateTime _startedAt;
    private DateTime _lastMoveAt;
    private Vector3 _lastPosition;
    private Vector3 _target;
    private ushort _startTerritory;
    private string _name = string.Empty;

    public ZoneTransitionHandler(IObjectTable objectTable, IClientState clientState,
                                 NavmeshIpc nav, TolkService tolk, ObstacleService? obstacles,
                                 IPluginLog log)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _nav = nav;
        _tolk = tolk;
        _obstacles = obstacles;
        _log = log;
    }

    public bool IsActive => _active;

    /// <summary>
    /// Pushes into the zone line at <paramref name="target"/>, which must lie
    /// INSIDE the trigger box. Returns false when the border is too far off to be
    /// walked into blindly - the caller then reports the honest remaining distance
    /// instead.
    /// </summary>
    public bool Nudge(Vector3 target, string name)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return false;

        var distance = Vector3.Distance(player.Position, target);
        if (distance > MaxNudgeDistance)
        {
            _log.Info($"[Uebergang] Kein Anschieben: {name} ist {distance:F1} m entfernt " +
                      $"(hoechstens {MaxNudgeDistance:F0} m).");
            return false;
        }

        // Face it first, and face it with the camera too - forward is where the
        // CAMERA looks in the game's standard movement mode. FacingService is the
        // one place that knows the half-turn offset on DirH.
        FacingService.FaceTowards(player, target);

        if (!_nav.MoveAlong(new List<Vector3> { target }))
        {
            _log.Warning($"[Uebergang] Anschieben nach {name} konnte nicht gestartet werden.");
            return false;
        }

        _active = true;
        _target = target;
        _name = name;
        _startedAt = DateTime.UtcNow;
        _lastMoveAt = _startedAt;
        _lastPosition = player.Position;
        _startTerritory = (ushort)_clientState.TerritoryType;

        _log.Info($"[Uebergang] Schiebe in {name} hinein: {distance:F1} m nach " +
                  $"({target.X:F1}|{target.Y:F1}|{target.Z:F1}).");
        return true;
    }

    /// <summary>Called every frame; does nothing unless a push is running.</summary>
    public void Update()
    {
        if (!_active) return;

        // The zone changed - that was the whole point, and it has to be checked
        // first: after a change the old position is meaningless.
        if ((ushort)_clientState.TerritoryType != _startTerritory)
        {
            _log.Info($"[Uebergang] Zonenwechsel erreicht ({_name}).");
            Stop(silent: true);
            return;
        }

        var player = _objectTable.LocalPlayer;
        if (player == null) { Stop(silent: true); return; }

        var now = DateTime.UtcNow;
        if (Vector3.Distance(player.Position, _lastPosition) >= MovementEpsilon)
        {
            _lastPosition = player.Position;
            _lastMoveAt = now;
        }

        if ((now - _lastMoveAt).TotalSeconds > StallSeconds)
        {
            var left = Vector3.Distance(player.Position, _target);
            _log.Info($"[Uebergang] Anschieben bringt nichts - keine Bewegung, noch {left:F1} m. " +
                      "Da steht etwas im Weg.");
            Stop(silent: false);
            return;
        }

        if ((now - _startedAt).TotalSeconds > NudgeSeconds)
        {
            var left = Vector3.Distance(player.Position, _target);
            _log.Info($"[Uebergang] Anschieben abgelaufen, noch {left:F1} m bis {_name}.");
            Stop(silent: false);
        }
    }

    /// <summary>Ends the push. Always stops vnavmesh - a push left running would
    /// keep steering after the walk that started it is long over.</summary>
    public void Stop(bool silent)
    {
        if (!_active) return;
        _active = false;
        _nav.Stop();
        if (silent) return;

        // Name the culprit if it can be named. "Da steht etwas im Weg" leaves the
        // player with no way to decide what to do next; "ein Spieler steht im Weg"
        // means wait a moment, "eine unsichtbare Absperrung" means turn around.
        var player = _objectTable.LocalPlayer;
        var blocker = player != null ? _obstacles?.DescribeBlocker(player.Position, _target) : null;
        _tolk.SpeakInterrupt(blocker != null
            ? AccessibilityStrings.TransitionNudgeBlocked(_name, blocker)
            : AccessibilityStrings.TransitionNudgeFailed(_name));
    }
}
