using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

public sealed class CombatService
{
    private readonly IObjectTable   _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly IDataManager   _data;
    private readonly TolkService    _tolk;
    private readonly Configuration  _config;
    private readonly IPluginLog     _log;

    private bool _wasInCombat   = false;
    private int  _lastHpPercent = 100;

    // Level-up tracking (per active job, so a job switch is not a "level up").
    private short _lastLevel = -1;
    private byte  _lastLevelJobId;

    // Current-target tracking for HP thresholds and cast announcements.
    private ulong _targetId;
    private int   _lastTargetHpPercent = 100;
    private bool  _targetWasCasting;
    private uint  _lastCastActionId;

    private static readonly int[] HpThresholds = [75, 50, 25, 10];

    public CombatService(
        IObjectTable objectTable,
        ITargetManager targetManager,
        IDataManager data,
        TolkService tolk,
        Configuration config,
        IPluginLog log)
    {
        _objectTable   = objectTable;
        _targetManager = targetManager;
        _data          = data;
        _tolk          = tolk;
        _config        = config;
        _log           = log;
    }

    // Wird jeden Frame aus Plugin.OnFrameworkUpdate aufgerufen
    public void Update()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        TrackLevelUp();

        var inCombat = (player.StatusFlags & StatusFlags.InCombat) != 0;

        if (inCombat && !_wasInCombat)
        {
            _lastHpPercent = HpPercent(player.CurrentHp, player.MaxHp);
            _tolk.Speak("Kampf.");
        }
        else if (!inCombat && _wasInCombat)
        {
            _tolk.Speak("Kampf vorbei.");
        }
        _wasInCombat = inCombat;

        UpdateTarget(inCombat);

        if (!inCombat) return;

        var hp = HpPercent(player.CurrentHp, player.MaxHp);
        foreach (var threshold in HpThresholds)
        {
            if (_lastHpPercent > threshold && hp <= threshold)
            {
                _tolk.SpeakInterrupt($"HP: {player.CurrentHp} von {player.MaxHp}.");
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
    private void UpdateTarget(bool inCombat)
    {
        var target = _targetManager.Target as IBattleChara;
        var targetId = target?.GameObjectId ?? 0;

        // Reset the per-target state whenever the target changes.
        if (targetId != _targetId)
        {
            _targetId = targetId;
            _lastTargetHpPercent = target != null ? HpPercent(target.CurrentHp, target.MaxHp) : 100;
            _targetWasCasting = false;
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
                    _tolk.SpeakInterrupt($"Ziel HP: {target.CurrentHp} von {target.MaxHp}.");
                    break;
                }
            }
            _lastTargetHpPercent = hp;
        }

        // Cast announcement: fire once per cast (rising edge, or a new action
        // while still casting). Lets a blind player react to a big enemy skill.
        if (_config.AnnounceEnemyCast)
        {
            var casting = target.IsCasting;
            var castId = target.CastActionId;
            var newCast = casting && (!_targetWasCasting || castId != _lastCastActionId);
            if (newCast)
            {
                var name = CastActionName(castId);
                _tolk.SpeakInterrupt($"Gegner wirkt {name}.");
                _log.Info($"[Combat] Gegner-Cast: id={castId} name='{name}' " +
                          $"unterbrechbar={target.IsCastInterruptible}");
                _lastCastActionId = castId;
            }
            _targetWasCasting = casting;
        }
    }

    private string CastActionName(uint actionId)
    {
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var action))
        {
            var name = action.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return "eine Fähigkeit";
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
            _tolk.SpeakInterrupt($"Stufe {level} erreicht.");
            _log.Info($"[Level] Level-Up: job={job} {_lastLevel} -> {level}");
        }
        _lastLevel = level;
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
            _tolk.SpeakInterrupt("Nicht eingeloggt.");
            return;
        }

        var ps = PlayerState.Instance();
        if (ps == null)
        {
            _tolk.SpeakInterrupt("Stufe nicht verfügbar.");
            return;
        }

        var level  = ps->CurrentLevel;
        var needed = ps->GetCurrentClassJobNeededExp();
        if (needed == 0)
        {
            _tolk.SpeakInterrupt($"Stufe {level}, Maximalstufe erreicht.");
            _log.Info($"[Level] Stufe={level} (Max)");
            return;
        }

        var cur  = ps->GetCurrentClassJobExp();
        var left = needed > cur ? needed - cur : 0;
        _tolk.SpeakInterrupt($"Stufe {level}. Noch {left} Erfahrungspunkte bis zur nächsten Stufe.");
        _log.Info($"[Level] Stufe={level} exp={cur}/{needed} left={left}");
    }

    // Auf Tastendruck: aktueller HP/MP-Status (eigen + Ziel)
    public void AnnounceStatus()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt("Nicht eingeloggt.");
            return;
        }

        var text = player.MaxMp > 0
            ? $"HP {player.CurrentHp} von {player.MaxHp}, MP {player.CurrentMp} von {player.MaxMp}."
            : $"HP {player.CurrentHp} von {player.MaxHp}.";

        if (_targetManager.Target is IBattleChara target && target.MaxHp > 0)
        {
            var name = target.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = "Ziel";
            text += $" {name}, HP {target.CurrentHp} von {target.MaxHp}.";
        }

        _tolk.SpeakInterrupt(text);
    }

    private static int HpPercent(uint current, uint max) =>
        max == 0 ? 0 : (int)(current * 100u / max);
}
