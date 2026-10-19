using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Where the player stands relative to the targeted enemy. Left and
/// right are the ENEMY's sides.</summary>
public enum PositionalZone
{
    /// <summary>Within 45° of the enemy's facing.</summary>
    Front,
    /// <summary>Enemy's left side, 45°-135° from its facing.</summary>
    FlankLeft,
    /// <summary>Enemy's right side, 45°-135° from its facing.</summary>
    FlankRight,
    /// <summary>More than 135° from the enemy's facing.</summary>
    Rear,
    /// <summary>The enemy ignores positionals (omnidirectional or status 3808).</summary>
    Omni,
}

/// <summary>How position changes are reported.</summary>
public enum PositionalOutput
{
    /// <summary>No automatic report; the on-demand key uses the screen reader.</summary>
    Off,
    /// <summary>Own SAPI voice (<see cref="PositionalVoiceService"/>).</summary>
    Voice,
    /// <summary>One tone per zone (<see cref="CueService.PlayPositionalTone"/>).</summary>
    Tone,
}

/// <summary>
/// Reports whether the player stands in front of, beside or behind the targeted
/// enemy - the information a sighted player reads off the target ring (gap at
/// the rear, closed ring for enemies without positionals). Pugilist/Monk need it
/// for Snap Punch / Pouncing Coeurl (flank) and Demolish (rear).
///
/// WHAT IS GAME DATA AND WHAT IS NOT (docs/game-api.md, "Positionals"):
/// <list type="bullet">
/// <item>"No positionals" is read, never guessed: <c>BNpcBase.IsOmnidirectional</c>
///   of the target's BaseId, and status 3808 "Directional Disregard", which some
///   bosses gain temporarily.</item>
/// <item>The game exposes no "player is behind" value (no such field or function
///   in ClientStructs, Dalamud or Lumina). The zone is computed from the enemy's
///   <c>Position</c>/<c>Rotation</c> and the player's <c>Position</c> with the
///   verified bearing convention (<see cref="NavigationService.RelativeAngle"/>).</item>
/// <item>The ±45° arcs are NOT from an official source. They are what WrathCombo
///   and RotationSolverReborn use. Boundaries are exact, without
///   hysteresis (user choice 2026-10-14).</item>
/// </list>
///
/// Reports on target change and on every zone change while an enemy is
/// targeted. For an enemy without positionals it says so once and then stays
/// silent; if status 3808 wears off, the zone is reported again.
/// </summary>
public sealed class PositionalService
{
    /// <summary>Status "Directional Disregard": "All action direction requirements
    /// are nullified." (Status sheet row 3808, patch 7.01+).</summary>
    private const uint DirectionalDisregardStatusId = 3808;

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IDataManager _data;
    private readonly PositionalVoiceService _voice;
    private readonly CueService _cue;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    // Per-target state. The omni flag is static sheet data of the target's
    // BaseId, so it is looked up once per target; the status is re-read every
    // frame because it comes and goes.
    private ulong _targetId;
    private bool _targetOmniBySheet;
    private PositionalZone? _lastZone;

    /// <summary>Wires the service to the game and output channels.</summary>
    public PositionalService(IObjectTable objectTable, ITargetManager targetManager, IDataManager data,
                             PositionalVoiceService voice, CueService cue, TolkService tolk,
                             Configuration config, IPluginLog log)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _data = data;
        _voice = voice;
        _cue = cue;
        _tolk = tolk;
        _config = config;
        _log = log;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        if (_config.PositionalMode == PositionalOutput.Off)
        {
            // Forget the target, so switching on reports the position at once.
            Reset();
            return;
        }

        if (!TryGetZone(out var target, out var zone, out var angle))
        {
            Reset();
            return;
        }

        if (_lastZone == zone) return;
        _lastZone = zone;

        LogZone("change", target, zone, angle);
        Report(zone, _config.PositionalMode);
    }

    /// <summary>On-demand key: says the current position, or that no enemy is
    /// targeted. Uses the configured channel; with the feature off it speaks
    /// through the screen reader, because a key press must always answer.</summary>
    public void AnnounceCurrent()
    {
        if (!TryGetZone(out var target, out var zone, out var angle))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.PositionalNoEnemy);
            return;
        }

        LogZone("key", target, zone, angle);
        Report(zone, _config.PositionalMode);
    }

    /// <summary>
    /// The zone for the current target, or false when no living enemy is
    /// targeted. <paramref name="angle"/> is the player's bearing seen from the
    /// enemy, in degrees: 0 = straight ahead of it, positive = its right side.
    /// </summary>
    private bool TryGetZone(out IBattleChara target, out PositionalZone zone, out double angle)
    {
        target = null!;
        zone = PositionalZone.Front;
        angle = 0;

        var player = _objectTable.LocalPlayer;
        if (player == null) return false;

        if (_targetManager.Target is not IBattleChara enemy) return false;
        if (enemy.ObjectKind != ObjectKind.BattleNpc || enemy.IsDead) return false;
        if (CombatSide.IsAlly(enemy)) return false;

        target = enemy;
        if (enemy.GameObjectId != _targetId)
        {
            _targetId = enemy.GameObjectId;
            _targetOmniBySheet = IsOmnidirectionalBySheet(enemy);
            _lastZone = null;
        }

        angle = NavigationService.RelativeAngle(enemy, player.Position);

        if (_targetOmniBySheet || HasDirectionalDisregard(enemy))
        {
            zone = PositionalZone.Omni;
            return true;
        }

        var abs = Math.Abs(angle);
        zone = abs <= 45.0  ? PositionalZone.Front
             : abs <= 135.0 ? (angle > 0 ? PositionalZone.FlankRight : PositionalZone.FlankLeft)
             : PositionalZone.Rear;
        return true;
    }

    /// <summary>
    /// <c>BNpcBase.IsOmnidirectional</c> of the enemy's BaseId (Lumina, packed
    /// bool at byte 35 bit 0). A missing row is logged and treated as
    /// directional - the plan approved by the user 2026-10-14, the same default
    /// Splatoon's TargetCircle script uses: claiming "no positionals" without
    /// data would hide information the ring shows.
    /// </summary>
    private bool IsOmnidirectionalBySheet(IBattleChara enemy)
    {
        if (_data.GetExcelSheet<BNpcBase>().TryGetRow(enemy.BaseId, out var row))
            return row.IsOmnidirectional;

        _log.Warning($"[Positional] No BNpcBase row for BaseId={enemy.BaseId} ('{enemy.Name}') - " +
                     "treated as having positionals.");
        return false;
    }

    private static bool HasDirectionalDisregard(IBattleChara enemy)
    {
        foreach (var status in enemy.StatusList)
            if (status.StatusId == DirectionalDisregardStatusId) return true;
        return false;
    }

    /// <summary>Sends one zone to the chosen channel; the screen reader takes
    /// over whenever the voice did not.</summary>
    private void Report(PositionalZone zone, PositionalOutput mode)
    {
        if (mode == PositionalOutput.Tone)
        {
            _cue.PlayPositionalTone(zone);
            return;
        }

        var text = AccessibilityStrings.PositionalZoneName(zone);
        if (mode == PositionalOutput.Voice && _voice.Speak(text)) return;
        _tolk.SpeakInterrupt(text);
    }

    private void LogZone(string trigger, IBattleChara target, PositionalZone zone, double angle) =>
        _log.Info($"[Positional] {trigger}: {zone} angle={angle:F1} target='{target.Name}' " +
                  $"BaseId={target.BaseId} omniSheet={_targetOmniBySheet} " +
                  $"status3808={HasDirectionalDisregard(target)}");

    private void Reset()
    {
        _targetId = 0;
        _targetOmniBySheet = false;
        _lastZone = null;
    }
}
