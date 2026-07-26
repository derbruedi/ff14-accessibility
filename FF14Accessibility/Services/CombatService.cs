using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

public sealed class CombatService
{
    private readonly IObjectTable          _objectTable;
    private readonly ITargetManager        _targetManager;
    private readonly IDataManager          _data;
    private readonly TolkService           _tolk;
    private readonly Configuration         _config;
    private readonly MessageHistoryService _history;
    private readonly AoeWarningService     _aoeWarn;
    private readonly IPluginLog            _log;

    private bool _wasInCombat   = false;
    private int  _lastHpPercent = 100;

    // Level-up tracking (per active job, so a job switch is not a "level up").
    private short _lastLevel = -1;
    private byte  _lastLevelJobId;

    // XP-gain tracking. Baseline per active job (a job switch changes the EXP
    // value without any XP actually being earned); -1 = not yet baselined.
    private long _lastExp = -1;
    private byte _lastExpJobId;

    // Current-target tracking for HP thresholds and cast announcements.
    private ulong _targetId;
    private int   _lastTargetHpPercent = 100;
    private bool  _targetWasCastingAtMe;
    private uint  _lastCastActionId;

    private static readonly int[] HpThresholds = [75, 50, 25, 10];

#if DEBUG
    // Debug-only AoE-cast probe (AoeCastProbe): maps each nearby enemy cast to its
    // Lumina Action shape data (CastType/EffectRange/XAxisModifier/Omen) plus caster
    // geometry, so the CastType->shape numbers can be verified empirically instead of
    // guessed. Deduped per caster: casterId -> last logged cast action id. Compiled
    // out of release builds.
    private readonly Dictionary<ulong, uint> _probedCasts = new();
#endif

    public CombatService(
        IObjectTable objectTable,
        ITargetManager targetManager,
        IDataManager data,
        TolkService tolk,
        Configuration config,
        MessageHistoryService history,
        AoeWarningService aoeWarn,
        IPluginLog log)
    {
        _objectTable   = objectTable;
        _targetManager = targetManager;
        _data          = data;
        _tolk          = tolk;
        _config        = config;
        _history       = history;
        _aoeWarn       = aoeWarn;
        _log           = log;
    }

    // Wird jeden Frame aus Plugin.OnFrameworkUpdate aufgerufen
    public void Update()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        TrackLevelUp();
        TrackXpGain();

        // AoE danger tone. Runs regardless of the InCombat flag: a cast telegraph can
        // appear the instant before combat officially starts, and the flag lags.
        UpdateAoeWarning(player.GameObjectId, player.Position);

        var inCombat = (player.StatusFlags & StatusFlags.InCombat) != 0;

        if (inCombat && !_wasInCombat)
        {
            _lastHpPercent = HpPercent(player.CurrentHp, player.MaxHp);
            _tolk.Speak(AccessibilityStrings.CombatStart);
        }
        else if (!inCombat && _wasInCombat)
        {
            _tolk.Speak(AccessibilityStrings.CombatEnd);
        }
        _wasInCombat = inCombat;

        UpdateTarget(inCombat, player.GameObjectId);

        if (!inCombat) return;

        var hp = HpPercent(player.CurrentHp, player.MaxHp);
        foreach (var threshold in HpThresholds)
        {
            if (_lastHpPercent > threshold && hp <= threshold)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.HpSentence(player.CurrentHp, player.MaxHp));
                break;
            }
        }
        _lastHpPercent = hp;
    }

    /// <summary>
    /// Tracks the current target: announces its HP crossing thresholds during
    /// combat (so you hear your attacks working and when the enemy is nearly
    /// dead) and announces when the target starts casting an action.
    /// </summary>
    private void UpdateTarget(bool inCombat, ulong playerId)
    {
        var target = _targetManager.Target as IBattleChara;
        var targetId = target?.GameObjectId ?? 0;

        // Reset the per-target state whenever the target changes.
        if (targetId != _targetId)
        {
            _targetId = targetId;
            _lastTargetHpPercent = target != null ? HpPercent(target.CurrentHp, target.MaxHp) : 100;
            _targetWasCastingAtMe = false;
            _lastCastActionId = 0;
        }

        if (target == null) return;

        // Enemy HP thresholds - only in combat, where the number actually moves
        // and the announcement is relevant.
        if (inCombat && _config.AnnounceTargetHp)
        {
            var hp = HpPercent(target.CurrentHp, target.MaxHp);
            foreach (var threshold in HpThresholds)
            {
                if (_lastTargetHpPercent > threshold && hp <= threshold)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.TargetHpSentence(target.CurrentHp, target.MaxHp));
                    break;
                }
            }
            _lastTargetHpPercent = hp;
        }

        // Cast announcement: only casts aimed AT THE PLAYER (user request
        // 2026-07-25 - casts on others are noise). CastTargetObjectId is the
        // object the target is casting at (Dalamud IBattleChara, verified).
        // Fire once per cast (rising edge, or a new action while still casting);
        // tracking "casting at me" as the edge state also catches the target
        // swinging an in-progress cast onto the player.
        if (_config.AnnounceEnemyCast)
        {
            var castingAtMe = target.IsCasting && target.CastTargetObjectId == playerId;
            var castId = target.CastActionId;
            var newCast = castingAtMe && (!_targetWasCastingAtMe || castId != _lastCastActionId);
            if (newCast)
            {
                var name = CastActionName(castId);
                _tolk.SpeakInterrupt(AccessibilityStrings.EnemyCasts(name));
                _log.Info($"[Combat] Gegner-Cast auf mich: id={castId} name='{name}' " +
                          $"unterbrechbar={target.IsCastInterruptible}");
                _lastCastActionId = castId;
            }
            _targetWasCastingAtMe = castingAtMe;
        }
    }

    private string CastActionName(uint actionId)
    {
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var action))
        {
            var name = action.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return AccessibilityStrings.AnAbility;
    }

    /// <summary>
    /// Announces a level-up. Reads the active job's level straight from PlayerState
    /// (no UI scraping); fires only when the level RISES for the SAME job, so a job
    /// switch - which also changes CurrentLevel - does not trigger a false level-up.
    /// </summary>
    private unsafe void TrackLevelUp()
    {
        var ps = PlayerState.Instance();
        if (ps == null) return;

        var job   = ps->CurrentClassJobId;
        var level = ps->CurrentLevel;

        // First read after login or a job switch: set the baseline silently.
        if (_lastLevel < 0 || job != _lastLevelJobId)
        {
            _lastLevel = level;
            _lastLevelJobId = job;
            return;
        }

        if (level > _lastLevel)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LevelReached(level));
            _log.Info($"[Level] Level-Up: job={job} {_lastLevel} -> {level}");
        }
        _lastLevel = level;
    }

    /// <summary>
    /// Announces every XP gain for the active job. The current EXP comes straight
    /// from PlayerState (GetCurrentClassJobExp, ilspycmd-verified) - the same
    /// source the level-up tracker uses, no UI scraping. Fires when the value
    /// RISES; a job switch (which changes the value without any XP earned) and a
    /// level-up (the value drops back toward 0) only re-baseline silently - the
    /// level-up itself is already announced by TrackLevelUp. Spoken non-interrupt
    /// so an XP line never cuts off an HP warning or an enemy-cast alert, and
    /// archived to the "Beute" reread channel.
    /// </summary>
    private unsafe void TrackXpGain()
    {
        if (!_config.AnnounceXpGain) return;

        var ps = PlayerState.Instance();
        if (ps == null) return;

        var job    = ps->CurrentClassJobId;
        var needed = ps->GetCurrentClassJobNeededExp();
        // At max level NeededExp is 0 and no XP is earned - nothing to track.
        if (needed == 0) { _lastExp = -1; return; }

        var cur = (long)ps->GetCurrentClassJobExp();

        // First read after login, a job switch, or coming back from max level:
        // set the baseline silently so the first real gain reports a clean delta.
        if (_lastExp < 0 || job != _lastExpJobId)
        {
            _lastExp = cur;
            _lastExpJobId = job;
            return;
        }

        if (cur > _lastExp)
        {
            var gain = cur - _lastExp;
            _tolk.Speak(AccessibilityStrings.XpGained((int)gain));
            _history.Add(MessageHistoryService.Category.Loot, AccessibilityStrings.XpGained((int)gain));
            _log.Info($"[XP] +{gain} (job={job} {_lastExp} -> {cur}/{needed})");
        }
        // Always follow the value, including the level-up drop-back, so the next
        // real gain measures from the correct baseline instead of a huge jump.
        _lastExp = cur;
    }

    /// <summary>
    /// On key press: the active job's level and how much experience is left to the
    /// next level. Level, current and needed EXP come from PlayerState
    /// (ilspycmd-verified: CurrentLevel, GetCurrentClassJobExp,
    /// GetCurrentClassJobNeededExp). NeededExp is 0 at max level.
    /// </summary>
    public unsafe void AnnounceLevelExp()
    {
        if (_objectTable.LocalPlayer == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var ps = PlayerState.Instance();
        if (ps == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LevelNotAvailable);
            return;
        }

        var level  = ps->CurrentLevel;
        var needed = ps->GetCurrentClassJobNeededExp();
        if (needed == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LevelMax(level));
            _log.Info($"[Level] Stufe={level} (Max)");
            return;
        }

        var cur  = ps->GetCurrentClassJobExp();
        var left = needed > cur ? needed - cur : 0;
        _tolk.SpeakInterrupt(AccessibilityStrings.LevelExpLeft(level, (int)left));
        _log.Info($"[Level] Stufe={level} exp={cur}/{needed} left={left}");
    }

    // Auf Tastendruck: aktueller HP/MP-Status (eigen + Ziel)
    public void AnnounceStatus()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var text = AccessibilityStrings.VitalStatus(
            player.CurrentHp, player.MaxHp, player.CurrentMp, player.MaxMp, player.MaxMp > 0);

        if (_targetManager.Target is IBattleChara target && target.MaxHp > 0)
        {
            var name = target.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = AccessibilityStrings.TargetFallbackName;
            text += AccessibilityStrings.TargetStatusClause(name, target.CurrentHp, target.MaxHp);
        }

        _tolk.SpeakInterrupt(text);
    }

    // Auf Tastendruck: aktueller SP-Stand (Sammelpunkte, engl. GP). Sammler
    // verbrauchen SP fuer Sammel-Fertigkeiten; der Vorrat regeneriert sich mit
    // jedem Abbauversuch und ueber Zeit. Ein blinder Sammler kann den GP-Balken
    // nicht sehen, daher auf Tastendruck - Gegenstueck zur HP/MP-Ansage.
    public void AnnounceGatheringPoints()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        // CurrentGp/MaxGp lesen CharacterData.GatheringPoints/MaxGatheringPoints
        // direkt aus dem Spiel (verifiziert an Dalamud Character 2026-07-24). Nur
        // eine Sammlerklasse hat einen SP-Vorrat; ist MaxGp 0, gibt es nichts
        // anzusagen - kein erfundener Wert, sondern die Spielaussage "kein SP".
        if (player.MaxGp == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoGatheringPoints);
            return;
        }

        _log.Info($"[SP] {player.CurrentGp}/{player.MaxGp}");
        _tolk.SpeakInterrupt(AccessibilityStrings.GpValue(player.CurrentGp, player.MaxGp));
    }

    /// <summary>
    /// Drives the AoE danger tone: ON while the player stands inside the danger zone
    /// of any nearby enemy that is currently casting, OFF the instant they leave it
    /// or the cast ends. Because it keys off <c>IsCasting</c>, the tone naturally
    /// begins with the cast bar and stops when the spell resolves.
    ///
    /// GEOMETRY MODEL V1 (WORKAROUND): the danger zone is treated as a CIRCLE centred
    /// on the caster with radius = <c>Action.EffectRange</c> yalms. The clean solution
    /// is to read the real telegraph shape and origin from the cast's Omen/VFX object
    /// (a circle may be ground-placed, and cones/lines need the caster's facing) - but
    /// that data is not yet decoded (hard research path, see game-api.md "AoE-Form").
    /// This V1 is verified in-game against the Hall of the Novice circle; the parallel
    /// AoeCastProbe logs the true Omen shape so cones/lines/off-centre AoEs can be
    /// modelled next. Distance is horizontal (XZ) only - AoE telegraphs are ground
    /// planes, so a height difference must not hide or fake danger.
    /// </summary>
    private void UpdateAoeWarning(ulong playerId, Vector3 playerPos)
    {
        if (!_config.AnnounceAoeWarning) { _aoeWarn.SetActive(false); return; }

        var sheet = _data.GetExcelSheet<LuminaAction>();
        var inDanger = false;

        foreach (var obj in _objectTable)
        {
            if (obj is not IBattleChara bc) continue;
            // Only hostile combatants: friendly EventNpcs never threaten the player.
            if (bc.ObjectKind != ObjectKind.BattleNpc) continue;
            if (bc.GameObjectId == playerId) continue;
            if (!bc.IsCasting) continue;

            if (!sheet.TryGetRow(bc.CastActionId, out var row)) continue;
            if (row.EffectRange == 0) continue; // single-target / self-buff: no ground danger

            if (IsPlayerInAoe(bc, row, playerPos)) { inDanger = true; break; }
        }

        _aoeWarn.SetActive(inDanger);
    }

    /// <summary>
    /// Whether the player stands inside the danger zone of one enemy cast. The zone
    /// shape comes from the action's <c>CastType</c> (verified 2026-07-26 against the
    /// telegraph graphic name <c>Omen.Path</c>: 2 = circle 'general', 3 = cone
    /// 'gl_fan090', 4 = line/rect). All maths are horizontal (XZ) only.
    /// The caster's facing (sin rot, cos rot) uses the project's verified rotation
    /// convention. Unknown CastTypes fall back to a caster-centred circle so we
    /// over-warn rather than miss - a false alarm is safer than a silent hit.
    /// </summary>
    private bool IsPlayerInAoe(IBattleChara caster, LuminaAction row, Vector3 playerPos)
    {
        float range = row.EffectRange;
        var casterPos = caster.Position;
        var dx = playerPos.X - casterPos.X;
        var dz = playerPos.Z - casterPos.Z;
        var horiz2 = dx * dx + dz * dz;

        switch (row.CastType)
        {
            // Circle placed at the cast's target. Centre on the target object if it
            // is a real object we can resolve; otherwise fall back to the caster.
            // ASSUMPTION: ground-targeted circles whose centre lives only in the VFX
            // are not yet solvable (telegraph-reading step) - flagged, verify in-game.
            case 2:
            {
                var center = casterPos;
                var tid = caster.CastTargetObjectId;
                if (tid != 0 && tid != caster.GameObjectId)
                {
                    foreach (var o in _objectTable)
                        if (o.GameObjectId == tid) { center = o.Position; break; }
                }
                var cx = playerPos.X - center.X;
                var cz = playerPos.Z - center.Z;
                return cx * cx + cz * cz <= range * range;
            }

            // Cone from the caster along its facing. Half-angle parsed from the fan
            // number in the Omen path (gl_fan090 -> 90 deg total -> 45 deg half).
            case 3:
            {
                if (horiz2 > range * range) return false;
                var rel = Math.Abs(RelBearingDeg(casterPos, caster.Rotation, playerPos));
                return rel <= ConeHalfAngleDeg(row);
            }

            // Line/rectangle from the caster along its facing. Length = EffectRange,
            // ASSUMPTION half-width = XAxisModifier (verify in-game). Project the
            // player onto the facing axis: ahead within length, lateral within width.
            case 4:
            {
                var fx = MathF.Sin(caster.Rotation);
                var fz = MathF.Cos(caster.Rotation);
                var along = dx * fx + dz * fz;               // forward distance
                if (along < 0f || along > range) return false;
                var lateral = MathF.Abs(dx * fz - dz * fx);  // perpendicular distance
                var halfWidth = row.XAxisModifier > 0 ? row.XAxisModifier : 0.5f;
                return lateral <= halfWidth;
            }

            // Not-yet-verified shapes: caster-centred circle (over-warn, never miss).
            default:
                return horiz2 <= range * range;
        }
    }

    /// <summary>
    /// Cone half-angle in degrees. The full angle is encoded in the telegraph name,
    /// e.g. gl_fan090 = 90 deg, so 60/120 deg cones are handled too. Defaults to 45
    /// deg (a 90 deg cone) when the name carries no fan number.
    /// </summary>
    private static float ConeHalfAngleDeg(LuminaAction row)
    {
        if (row.Omen.ValueNullable is { } om)
        {
            var path = om.Path.ExtractText();
            var i = path.IndexOf("fan", StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                i += 3;
                var j = i;
                while (j < path.Length && char.IsDigit(path[j])) j++;
                if (j > i && int.TryParse(path.AsSpan(i, j - i), out var deg) && deg > 0)
                    return deg / 2f;
            }
        }
        return 45f;
    }

    /// <summary>
    /// Bearing from <paramref name="from"/> (facing <paramref name="rot"/>) to
    /// <paramref name="to"/> in degrees: 0 = directly ahead, positive = right.
    /// Uses the verified convention facing = (sin rot, cos rot).
    /// </summary>
    private static float RelBearingDeg(Vector3 from, float rot, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        return NormalizeDeg((MathF.Atan2(dx, dz) - rot) * 180f / MathF.PI);
    }

    private static float NormalizeDeg(float deg)
    {
        while (deg > 180f)  deg -= 360f;
        while (deg < -180f) deg += 360f;
        return deg;
    }

    private static int HpPercent(uint current, uint max) =>
        max == 0 ? 0 : (int)(current * 100u / max);

    /// <summary>
    /// DEBUG-only measuring probe for the AoE-dodge feature. Logs one line per
    /// nearby enemy cast (rising edge, deduped per caster) that pairs the live cast
    /// with its Lumina Action shape data - CastType (shape), EffectRange (radius),
    /// XAxisModifier (width for lines), Omen (telegraph graphic id) - plus the
    /// caster/player geometry. Its sole purpose is to verify EMPIRICALLY what each
    /// CastType byte means (circle / cone / line / donut ...) before any "you are
    /// standing in it" logic is built on those numbers, so the mapping is proven
    /// against what the player actually sees rather than guessed. Iterates the whole
    /// object table on purpose: in the Hall of the Novice the AoE often comes from an
    /// enemy the player is not currently targeting. Compiled out of release builds.
    /// </summary>
    public void AoeCastProbe()
    {
#if DEBUG
        var player = _objectTable.LocalPlayer;
        if (player == null) { _probedCasts.Clear(); return; }

        var playerId  = player.GameObjectId;
        var playerPos = player.Position;
        var sheet     = _data.GetExcelSheet<LuminaAction>();

        // Track which casters are still casting this frame, so a caster that has
        // finished can be dropped from the dedup map and its NEXT cast logs fresh.
        var seen = new HashSet<ulong>();

        foreach (var obj in _objectTable)
        {
            if (obj is not IBattleChara bc) continue;
            if (!bc.IsCasting) continue;
            if (bc.GameObjectId == playerId) continue; // never the player's own casts

            seen.Add(bc.GameObjectId);

            var castId = bc.CastActionId;
            // Already logged this exact cast for this caster? Skip until it changes.
            if (_probedCasts.TryGetValue(bc.GameObjectId, out var last) && last == castId)
                continue;
            _probedCasts[bc.GameObjectId] = castId;

            string name = "?", omenPath = "";
            byte castType = 0, range = 0, xmod = 0;
            uint omen = 0, omenAlt = 0;
            if (sheet.TryGetRow(castId, out var row))
            {
                name     = row.Name.ExtractText();
                castType = row.CastType;
                range    = row.EffectRange;
                xmod     = row.XAxisModifier;
                omen     = row.Omen.RowId;
                omenAlt  = row.OmenAlt.RowId;
                // Omen.Path is the telegraph graphic file; its name encodes the real
                // shape (gl_fan* = cone, gl_circle* = circle, gl_line*/rect* = line),
                // so this pins the shape from game data instead of guessing CastType.
                if (row.Omen.ValueNullable is { } om)
                    omenPath = om.Path.ExtractText();
            }

            var casterPos = bc.Position;
            var rot       = bc.Rotation;
            var atMe      = bc.CastTargetObjectId == playerId;
            var dist      = Vector3.Distance(casterPos, playerPos);

            // Relative bearing caster -> player using the project's verified rotation
            // convention (facing = (sin rot, cos rot); relAngle = atan2(dx,dz) - rot).
            // ~0 deg means the player stands directly in front of the caster - key for
            // telling cones/lines from circles once we read the logs.
            var dx     = playerPos.X - casterPos.X;
            var dz     = playerPos.Z - casterPos.Z;
            var relDeg = NormalizeDeg((MathF.Atan2(dx, dz) - rot) * 180f / MathF.PI);

            // Format floats with invariant culture up front so decimals are '.' and
            // never collide with the ',' field separators (German locale would print
            // "-10,1" for -10.1). Everything else in the line is int/byte/string.
            var inv       = System.Globalization.CultureInfo.InvariantCulture;
            var casterStr = $"({casterPos.X.ToString("F1", inv)};{casterPos.Y.ToString("F1", inv)};{casterPos.Z.ToString("F1", inv)})";
            var playerStr = $"({playerPos.X.ToString("F1", inv)};{playerPos.Y.ToString("F1", inv)};{playerPos.Z.ToString("F1", inv)})";

            _log.Info(
                $"[AoeProbe] caster='{bc.Name.TextValue}' id={bc.GameObjectId:X} " +
                $"cast='{name}' castId={castId} CastType={castType} EffectRange={range} " +
                $"XAxisModifier={xmod} Omen={omen}/{omenAlt} OmenPath='{omenPath}' atMe={atMe} " +
                $"dist={dist.ToString("F1", inv)} relBearing={relDeg.ToString("F0", inv)} " +
                $"rot={rot.ToString("F2", inv)} casterPos={casterStr} playerPos={playerStr} " +
                $"castTime={bc.CurrentCastTime.ToString("F1", inv)}/{bc.TotalCastTime.ToString("F1", inv)}");
        }

        // Forget casters that stopped casting, so re-casting the same action re-logs.
        if (_probedCasts.Count > 0)
        {
            foreach (var key in _probedCasts.Keys.Where(k => !seen.Contains(k)).ToList())
                _probedCasts.Remove(key);
        }
#endif
    }

}
