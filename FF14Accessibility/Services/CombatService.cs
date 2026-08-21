using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

public sealed class CombatService
{
    private readonly IObjectTable          _objectTable;
    private readonly ITargetManager        _targetManager;
    private readonly IGameGui              _gameGui;
    private readonly IDataManager          _data;
    private readonly TolkService           _tolk;
    private readonly Configuration         _config;
    private readonly MessageHistoryService _history;
    private readonly AoeWarningService     _aoeWarn;
    private readonly EscapeRouteService    _escape;
    // Der zweite Sprachkanal fuer die Kampfwarnungen - siehe SpeakWarning.
    private readonly WarningVoiceService   _warnVoice;
    private readonly IPluginLog            _log;

    // Gefahrenflaechen dieses Frames, fuer die Fluchtsuche. Feld statt lokaler
    // Liste, damit der Kampf-Frame nichts anlegt - er laeuft in jedem Bild.
    private readonly List<DangerZone> _zoneBuf = new();

    // Fluchtrichtung schon gesagt? Einmal je Gefahrenlage - siehe AnnounceEscapeOnce.
    private bool _escapeSpoken;

    // Shape describer, built here rather than injected - the same pattern
    // UIReaderService uses, and it needs nothing this service does not already hold.
    // Sharing the describer is what keeps an enemy cast and an ability tooltip from
    // naming the same geometry two different ways.
    private readonly ActionShapeService    _actionShape;

    // Nur fuer die Frage "laeuft gerade ein Freibrief?" - davon haengt ab, wie fein
    // die Ziel-HP im unteren Band angesagt werden (siehe ThresholdsFor).
    private readonly LevequestEnemyService _leveEnemies;

    private bool _wasInCombat   = false;
    private int  _lastHpPercent = 100;

    // Level-up tracking (per active job, so a job switch is not a "level up").
    private short _lastLevel = -1;
    private byte  _lastLevelJobId;

    // XP-gain tracking. Baseline per active job (a job switch changes the EXP
    // value without any XP actually being earned); -1 = not yet baselined.
    private long _lastExp = -1;
    private byte _lastExpJobId;

    // Rested-area tracking. null = not baselined yet (login, or the EP bar not
    // built), so the first reading only sets the state instead of announcing a
    // "you entered" for a place the player was already standing in.
    private bool? _wasInRestedArea;

#if DEBUG
    // Audit probe state, see RestedProbe: last logged tuple, so the log carries
    // one line per CHANGE instead of one per frame.
    private string _lastRestedProbe = string.Empty;
#endif

    // Current-target tracking for HP thresholds.
    private ulong _targetId;
    private int   _lastTargetHpPercent = 100;

    // Enemy casts aimed at the player, tracked per CASTER (not just the current
    // target): casterId -> the cast action already announced for them. Without
    // the per-caster key an enemy the player has not targeted would stay silent,
    // which is exactly the case the warning is for - several enemies around and
    // one of them picks you. Entries are dropped as soon as a caster stops
    // casting at the player, so the same spell warns again next time.
    private readonly Dictionary<ulong, uint> _castsAtMe = new();
    // Scratch sets for the cleanup pass, kept as fields so the per-frame sweep
    // allocates nothing.
    private readonly HashSet<ulong> _castsAtMeAlive = new();
    private readonly List<ulong>    _castsAtMeStale = new();

    // "You are standing in it" state, per CASTER: casterId -> the cast for which
    // that fact has already been spoken. Keyed the same way as _castsAtMe so both
    // are cleaned up in the same sweep. The entry is dropped the moment the player
    // leaves the zone, so walking out and back in warns again - which is the point,
    // the second entry is as deadly as the first.
    private readonly Dictionary<ulong, uint> _aoeInside = new();

    private static readonly int[] HpThresholds = [75, 50, 25, 10];

    // FEINE STUFEN FUER FANG-AUFTRAEGE. Manche Freibriefe wollen den Gegner
    // GESCHWAECHT, nicht tot ("schlag es nicht k. o."), und mit den groben Stufen
    // oben ist das nicht zu treffen: gemessen am 2026-08-19 lagen zwischen der
    // Ansage bei 25 Prozent (tatsaechlich 18) und der bei 10 Prozent (tatsaechlich
    // 2) genau drei Sekunden, danach war der Dodo besiegt und der Freibrief-Zaehler
    // stand weiter auf 0/3. Unterhalb von 30 Prozent wird deshalb alle 5 Prozent
    // angesagt - die Zahl im Satz ist ohnehin immer der ECHTE Wert, die Stufe
    // entscheidet nur, WANN gesprochen wird.
    private static readonly int[] HpThresholdsFine = [75, 50, 30, 25, 20, 15, 10, 5];

    // Ab hier unterscheiden sich die beiden Stufenreihen. Oberhalb davon muss also
    // gar nicht erst nachgesehen werden, ob ein Freibrief laeuft - das haelt die
    // Abfrage aus dem normalen Kampf heraus.
    private const int FineBandCeiling = 30;

    public CombatService(
        IObjectTable objectTable,
        ITargetManager targetManager,
        IGameGui gameGui,
        IDataManager data,
        TolkService tolk,
        Configuration config,
        MessageHistoryService history,
        AoeWarningService aoeWarn,
        EscapeRouteService escape,
        WarningVoiceService warnVoice,
        LevequestEnemyService leveEnemies,
        IPluginLog log)
    {
        _objectTable   = objectTable;
        _targetManager = targetManager;
        _gameGui       = gameGui;
        _data          = data;
        _tolk          = tolk;
        _config        = config;
        _history       = history;
        _aoeWarn       = aoeWarn;
        _escape        = escape;
        _warnVoice     = warnVoice;
        _leveEnemies   = leveEnemies;
        _log           = log;
        _actionShape   = new ActionShapeService(data, log);
    }

    /// <summary>
    /// Sagt eine KAMPFWARNUNG - und zwar auf dem eigenen Kanal, nicht ueber den
    /// Screenreader.
    ///
    /// WARUM DIESE VIER ANSAGEN ANDERS BEHANDELT WERDEN (Spielerwunsch
    /// 2026-08-21): der Screenreader hat eine einzige Sprachwarteschlange, und
    /// das Plugin raeumt sie selbst staendig ab. Ein Zielwechsel, eine Chatzeile
    /// oder die Stopptaste des Spielers loescht damit eine Warnung, die gerade
    /// laeuft. Bei einer Zeilenansage ist das richtig - bei "du stehst in der
    /// Flaeche" kostet es Leben.
    ///
    /// DER RUECKFALL IST DER WICHTIGE TEIL: uebernimmt der zweite Kanal nicht
    /// (keine Sprachausgabe im System, abgeschaltet, stumm gestellt), geht die
    /// Warnung wieder ueber den Screenreader. Sie darf unter keinen Umstaenden
    /// einfach verschwinden - eine ausbleibende Warnung ist von "keine Gefahr"
    /// nicht zu unterscheiden.
    ///
    /// NUR HIER, NICHT ALS ALLGEMEINER WEG: alles andere, was das Plugin sagt,
    /// gehoert weiter auf den Screenreader. Zwei Stimmen, die gleichzeitig
    /// reden, sind schlechter als eine - der zweite Kanal traegt nur, solange
    /// er die Ausnahme bleibt.
    /// </summary>
    private void SpeakWarning(string text)
    {
        if (_warnVoice.Speak(text)) return;
        _tolk.SpeakInterrupt(text);
    }

    // Wird jeden Frame aus Plugin.OnFrameworkUpdate aufgerufen
    public void Update()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        TrackLevelUp();
        TrackXpGain();
        TrackRestedArea();
#if DEBUG
        RestedProbe();
#endif

        // Enemy cast announcements + AoE danger tone. Runs regardless of the InCombat
        // flag: a cast telegraph can appear the instant before combat officially
        // starts, and the flag lags.
        UpdateEnemyCastWarnings(player.GameObjectId, player.Position, player.Rotation);

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
        }

        if (target == null) return;

        // Enemy HP thresholds - only in combat, where the number actually moves
        // and the announcement is relevant.
        if (inCombat && _config.AnnounceTargetHp)
        {
            var hp = HpPercent(target.CurrentHp, target.MaxHp);
            foreach (var threshold in ThresholdsFor(hp))
            {
                if (_lastTargetHpPercent > threshold && hp <= threshold)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.TargetHpSentence(target.CurrentHp, target.MaxHp));
                    break;
                }
            }
            _lastTargetHpPercent = hp;
        }

    }

    /// <summary>
    /// Welche HP-Stufenreihe fuer das Ziel gilt. Feiner, solange ein FREIBRIEF
    /// laeuft und das Ziel schon im unteren Band ist - dort entscheidet sich, ob ein
    /// Fang-Auftrag gelingt oder der Gegner k. o. geht.
    /// <para>
    /// WARUM JEDER FREIBRIEF UND NICHT NUR DIE FANG-AUFTRAEGE: ob ein Freibrief
    /// fangen oder toeten will, steht in keinem Feld, das dieses Projekt
    /// nachgemessen hat. Die Aufgabenzeile sagt es in Worten ("besaenftige"), aber
    /// auf uebersetzten Text zu pruefen wuerde im englischen Client sofort brechen.
    /// Also gilt die feine Reihe fuer jeden laufenden Freibrief; der Preis sind ein
    /// paar zusaetzliche Ansagen unter 30 Prozent auf einem Toetungs-Auftrag, und
    /// den kann der Spieler mit <c>FineTargetHpDuringLeve</c> abschalten.
    /// </para>
    /// <para>
    /// Die Abfrage laeuft ERST unterhalb von <see cref="FineBandCeiling"/>. Darueber
    /// sind beide Reihen gleich, und <c>GetRunningLeve</c> liest jedes Mal frisch
    /// die Director-Liste - das gehoert nicht in jeden Frame eines normalen Kampfes.
    /// </para>
    /// </summary>
    private int[] ThresholdsFor(int targetHpPercent)
    {
        if (targetHpPercent > FineBandCeiling) return HpThresholds;
        if (!_config.FineTargetHpDuringLeve)   return HpThresholds;
        return _leveEnemies.GetRunningLeve() != null ? HpThresholdsFine : HpThresholds;
    }

    /// <summary>
    /// Announces enemy casts in two cases: every cast of the player's CURRENT TARGET
    /// (user 2026-08-18 "alle zauber des bosses"), and casts aimed AT THE PLAYER from
    /// any nearby enemy (user 2026-08-06: "wenn ein gegner auf mich zielt bzw einen
    /// zauber auf mich zaubert, so dass man ausweichen kann").
    /// <para>
    /// WHY the target and not "every enemy": a boss throws most of its spells at the
    /// ground or at the tank, so the old aimed-at-me rule (2026-07-25) left boss fights
    /// almost silent - measured on the Stone Vigil dragon, which announced nothing at
    /// all because cactbot has no trigger for it either. The target is the one enemy
    /// the player deliberately picked, so it is the boss in practice, while trash packs
    /// the player is not fighting stay quiet. There is no reliable "is a boss" flag on
    /// IBattleChara to key off instead.
    /// </para>
    /// <para>
    /// The caster's name is only spoken when it is NOT the player's current
    /// target: for the target the player already knows who is meant, and the
    /// short form keeps the warning fast - it has to arrive while there is still
    /// time to move. "auf dich" is appended whenever the cast targets the player,
    /// because the plain sentence used to mean exactly that and now no longer does -
    /// without the suffix the dangerous case would sound like the harmless one.
    /// </para>
    /// Fires once per cast (rising edge per caster, or a new action while still
    /// casting), which also catches an enemy swinging an in-progress cast onto
    /// the player. Runs off the same enemy sweep as the AoE tone, so no extra
    /// per-frame scan is added.
    /// </summary>
    private void AnnounceCastAtMe(IBattleChara caster, ulong playerId, ulong targetId,
                                  LuminaAction? shapeRow, bool inZone)
    {
        var castId = caster.CastActionId;
        var known = _castsAtMe.TryGetValue(caster.GameObjectId, out var announced);
        if (known && announced == castId) return;      // already warned about this one

        _castsAtMe[caster.GameObjectId] = castId;

        var action = CastActionName(castId);
        var casterName = caster.Name.TextValue;
        var atMe = caster.CastTargetObjectId == playerId;
        var anonymous = caster.GameObjectId == targetId || string.IsNullOrWhiteSpace(casterName);
        var text = anonymous
            ? (atMe ? AccessibilityStrings.EnemyCastsAtYou(action)
                    : AccessibilityStrings.EnemyCasts(action))
            : (atMe ? AccessibilityStrings.NamedEnemyCastsAtYou(casterName, action)
                    : AccessibilityStrings.NamedEnemyCasts(casterName, action));

        // Shape and size of the ground danger, straight from the action row. Only
        // present when the action actually has a ground shape (EffectRange > 0).
        var shape = shapeRow is { } row ? DescribeCastShape(caster, row, playerId) : string.Empty;

        // Standing-in-it warning. Gated by the AoE option, not by the cast option:
        // it is the geometry feature speaking, and that geometry is the part still
        // awaiting in-game confirmation. Marking it as spoken here is what keeps
        // TrackAoeEntry from repeating the same fact one frame later.
        var standing = string.Empty;
        if (inZone && _config.AnnounceAoeWarning)
        {
            standing = AccessibilityStrings.AoeStandingInIt(RemainingCastTime(caster));
            _aoeInside[caster.GameObjectId] = castId;
        }

        SpeakWarning(AccessibilityStrings.CastWithDanger(text, shape, standing));
        _log.Info($"[Combat] Gegner-Cast: caster='{casterName}' id={castId} name='{action}' " +
                  $"aufMich={atMe} unterbrechbar={caster.IsCastInterruptible} " +
                  $"istZiel={caster.GameObjectId == targetId} " +
                  $"form='{shape}' drin={inZone} rest={RemainingCastTime(caster):F1}s");
    }

    /// <summary>
    /// Spoken description of a cast's danger zone: shape plus size, e.g. "Kegel,
    /// 90 Grad, 6 Meter".
    /// <para>
    /// The shape WORD comes from <see cref="ActionShapeService"/>, the same describer
    /// the ability tooltip uses - so the geometry is named identically whether the
    /// player reads a skill in a window or hears an enemy cast it, and there is only
    /// one place where a CastType is mapped to a word. That service stays SILENT for
    /// every CastType this project has not measured against the telegraph graphic
    /// (AoeShape.HasProvenShape), and that silence is carried through here on purpose:
    /// naming a shape we have not proven would send the player dodging INTO it. For an
    /// unproven type the cast is still announced, just without geometry.
    /// </para>
    /// <para>
    /// The SIZE is added here and not in the tooltip path because no tooltip is on
    /// screen during a fight to have said it already. It matters even when the player
    /// is standing in the zone: it says how FAR they have to move. A 5-metre circle is
    /// two steps, a 30-metre line is not something you outrun sideways.
    /// </para>
    /// </summary>
    private string DescribeCastShape(IBattleChara caster, LuminaAction row, ulong playerId)
    {
        var shape = _actionShape.Describe(row.RowId);
        if (string.IsNullOrEmpty(shape)) return string.Empty;

        int meters = row.EffectRange;

        // A circle centred on the player: say so, because then no DIRECTION is safe -
        // only distance is, and "Kreis, 5 Meter" alone would suggest sidestepping.
        var onYou = caster.CastTargetObjectId == playerId
                    && row.CastType is AoeShape.CastTypeCircle or AoeShape.CastTypeCircle5;

        return onYou
            ? AccessibilityStrings.AoeShapeWithRangeOnYou(shape, meters)
            : AccessibilityStrings.AoeShapeWithRange(shape, meters);
    }

    /// <summary>
    /// Seconds left on a running cast. <c>TotalCastTime</c>/<c>CurrentCastTime</c> are
    /// the game's own cast-bar values (game-api.md "Kampf"), so this is the same clock
    /// a sighted player watches fill up - it is read, never estimated. Clamped at 0
    /// because the bar can sit a frame past its total before the spell resolves.
    /// </summary>
    private static float RemainingCastTime(IBattleChara caster) =>
        MathF.Max(0f, caster.TotalCastTime - caster.CurrentCastTime);

    /// <summary>
    /// Speaks the moment the player WALKS INTO a danger zone whose cast is already
    /// running. The tone alone cannot carry this: it says "danger now" but never how
    /// long there is left, and a player who moved into a zone they never heard
    /// announced has no idea a cast is even in progress.
    /// <para>
    /// Only called for casts that are announced anyway (current target, or aimed at
    /// the player). That bound is deliberate and it is also what keeps the cost down:
    /// the geometry check would otherwise run per frame for every casting enemy in
    /// the zone.
    /// </para>
    /// </summary>
    private void TrackAoeEntry(IBattleChara caster, bool inZone)
    {
        var id = caster.GameObjectId;
        if (!inZone) { _aoeInside.Remove(id); return; }

        var castId = caster.CastActionId;
        if (_aoeInside.TryGetValue(id, out var warned) && warned == castId) return;
        _aoeInside[id] = castId;

        var remaining = RemainingCastTime(caster);
        SpeakWarning(AccessibilityStrings.AoeEnteredZone(remaining));
        _log.Info($"[Combat] In Flaeche gelaufen: caster='{caster.Name.TextValue}' " +
                  $"id={castId} rest={remaining:F1}s");
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
            _history.Add(MessageHistoryService.SystemKey, AccessibilityStrings.XpGained((int)gain));
            _log.Info($"[XP] +{gain} (job={job} {_lastExp} -> {cur}/{needed})");
        }
        // Always follow the value, including the level-up drop-back, so the next
        // real gain measures from the correct baseline instead of a huge jump.
        _lastExp = cur;
    }

    /// <summary>
    /// Announces entering and leaving a rested area (inn, city districts), where
    /// the rested bonus accumulates - the tutorial "Ruhebereiche" describes it as
    /// a crescent-moon icon appearing under the EXP bar, and that icon is exactly
    /// what is read here: <c>AddonExp.MoonIconNode</c> (ilspycmd 2026-08-13,
    /// AddonExp is the "_Exp" addon, field offset 632). Reading the game's own
    /// indicator keeps the announcement in step with what a sighted player sees,
    /// instead of second-guessing which zones count as rested.
    ///
    /// NOT ANNOUNCED YET: how much bonus has piled up. The amount is available
    /// (AgentHUD.ExpRestedExperience / AddonExp.RestedExp), but the unit that
    /// value counts in is not documented in the struct - RestedProbe measures it
    /// first, then the number gets a sentence.
    /// </summary>
    private unsafe void TrackRestedArea()
    {
        if (!_config.AnnounceRestedArea) return;

        var rested = ReadRestedAreaIndicator();
        // The EP bar is not built (loading screen, HUD element hidden): keep the
        // last known state instead of faking a "left the rested area".
        if (rested == null) return;

        // First reading after login or after the bar appears: remember silently.
        if (_wasInRestedArea == null)
        {
            _wasInRestedArea = rested;
            _log.Info($"[Rested] Ausgangszustand: imRuhebereich={rested}");
            return;
        }

        if (rested == _wasInRestedArea) return;

        _wasInRestedArea = rested;
        // Non-interrupting: entering an inn or a city usually comes with a burst
        // of other announcements, and this one is never urgent.
        _tolk.Speak(rested == true
            ? AccessibilityStrings.RestedAreaEntered
            : AccessibilityStrings.RestedAreaLeft);
        _log.Info($"[Rested] {(rested == true ? "betreten" : "verlassen")}");
    }

    /// <summary>
    /// Whether the crescent-moon indicator on the EXP bar is showing: true = in a
    /// rested area, false = not, null = no reading available because the "_Exp"
    /// addon or its icon node is not there right now (loading, HUD hidden, max
    /// level). Null is deliberately NOT folded into false - "I cannot see the bar"
    /// and "the moon is off" are different answers, and treating them alike would
    /// announce leaving a rested area on every loading screen.
    /// </summary>
    private unsafe bool? ReadRestedAreaIndicator()
    {
        var handle = _gameGui.GetAddonByName("_Exp");
        if (handle.IsNull) return null;

        var addon = (AddonExp*)(nint)handle;
        if (!addon->AtkUnitBase.IsVisible) return null;

        var moon = addon->MoonIconNode;
        if (moon == null) return null;

        return ((AtkResNode*)moon)->IsVisible();
    }

#if DEBUG
    /// <summary>
    /// Debug audit probe for the rested bonus: logs the moon indicator together
    /// with every rested-related value the client offers, one line per change.
    /// The structs name the fields but say nothing about the UNIT
    /// (AgentHUD.ExpRestedExperience and AddonExp.RestedExp are plain uints), so
    /// the sentence for the amount cannot be written from source alone. Walk into
    /// an inn and out again with this build, and the log shows how the numbers
    /// move relative to the level's needed EXP. Delete once the unit is pinned.
    /// </summary>
    private unsafe void RestedProbe()
    {
        var moon = ReadRestedAreaIndicator();

        uint addonRested = 0;
        var handle = _gameGui.GetAddonByName("_Exp");
        if (!handle.IsNull) addonRested = ((AddonExp*)(nint)handle)->RestedExp;

        var hud = AgentHUD.Instance();
        var hudRested = hud == null ? 0 : hud->ExpRestedExperience;
        var hudCur    = hud == null ? 0 : hud->ExpCurrentExperience;
        var hudNeeded = hud == null ? 0 : hud->ExpNeededExperience;
        var hudLevel  = hud == null ? 0 : hud->ExpLevel;

        var ps = PlayerState.Instance();
        var baseRested = ps == null ? 0 : ps->BaseRestedExperience;

        // The EXP bar draws the rested part as its own node (AtkComponentGaugeBar
        // .RestedExpNode @376, ilspycmd 2026-08-14) - that node IS what a sighted
        // player sees. Its width against the filled bar's scale settles the unit
        // question without needing a fight: if the game paints the rested stretch
        // at the same fraction that hudRested/hudNeeded gives, the field counts EXP.
        var bar = "kein Balken";
        if (!handle.IsNull)
        {
            var gauge = ((AddonExp*)(nint)handle)->ExperienceBarComponent;
            if (gauge != null)
            {
                var rest = (AtkResNode*)gauge->RestedExpNode;
                var fill = (AtkResNode*)gauge->PrimaryFill.MainFillNode;
                var back = (AtkResNode*)gauge->BackdropImageNode;
                var root = gauge->AtkComponentBase.OwnerNode;
                var vals = gauge->Values;
                bar = $"balkenWert={(vals.Length > 0 ? vals[0].ValueInt : -1)}/{(vals.Length > 1 ? vals[1].ValueInt : -1)} "
                    + $"skala={gauge->MinValue}..{gauge->MaxValue} "
                    + $"ruheNode={(rest == null ? "null" : $"b={rest->Width} x={rest->X} sx={rest->ScaleX} sichtbar={rest->IsVisible()}")} "
                    + $"fuellNode={(fill == null ? "null" : $"b={fill->Width} x={fill->X} sx={fill->ScaleX}")} "
                    + $"grund={(back == null ? "null" : $"b={back->Width} x={back->X}")} "
                    + $"wurzel={(root == null ? "null" : $"b={((AtkResNode*)root)->Width}")}";
            }
        }

        var line = $"mond={moon} addonRested={addonRested} hudRested={hudRested} "
                 + $"hudExp={hudCur}/{hudNeeded} stufe={hudLevel} basisRested={baseRested} {bar}";
        if (line == _lastRestedProbe) return;

        _lastRestedProbe = line;
        _log.Info($"[RestedProbe] {line}");
    }
#endif

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

    /// <summary>
    /// On key press: whether the player is standing in a rested area right now, and
    /// how much rested bonus is stored. The stored amount comes from
    /// AgentHUD.ExpRestedExperience rather than AddonExp.RestedExp, because the
    /// agent still holds the value while the "_Exp" addon is not built - measured
    /// 2026-08-14 19:22, where hudRested read 97638 with the addon absent.
    ///
    /// The amount is stated as a percentage of ONE LEVEL, which is the unit the
    /// game itself paints the value in. Nothing in the struct says what the uint
    /// counts (AddonExp has no text node and no formatting method for it, and the
    /// sheets carry no "rested bonus: x" line - only the entering/leaving messages
    /// 732/733), so the unit was measured against the bar the sighted player sees,
    /// AtkComponentGaugeBar.RestedExpNode (log 2026-08-14 19:36, level 41):
    ///   bar width 482, fill node 91 at 27523/163000 = 16.89%, rested node 375.
    /// Both nodes follow width = 471 * fraction + 11.5, and the check on that fit
    /// holds - fraction 1 gives 482.5, the full bar. The rested node therefore sits
    /// at 77.2%, which is exactly (27523 + 98283) / 163000. So RestedExp counts EXP
    /// points on the same scale as CurrentExp, and rested/needed is a percentage of
    /// a level. RestedProbe stays in until a second reading at a different EXP
    /// value confirms the fit.
    /// </summary>
    public unsafe void AnnounceRestedStatus()
    {
        if (_objectTable.LocalPlayer == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var moon = ReadRestedAreaIndicator();
        // null means the EP bar is not there (loading, HUD hidden), not that the
        // player left - in that case the area part is left unsaid rather than guessed.
        var text = moon switch
        {
            true  => AccessibilityStrings.RestedAreaNow,
            false => AccessibilityStrings.RestedAreaNot,
            _     => string.Empty,
        };

        var hud = AgentHUD.Instance();
        var ps  = PlayerState.Instance();
        var needed = ps == null ? 0 : ps->GetCurrentClassJobNeededExp();
        if (hud != null && needed > 0)
        {
            var stored  = hud->ExpRestedExperience;
            var percent = (int)Math.Round(stored * 100.0 / needed);
            // A bonus too small to round up to a full percent is still a bonus, so
            // it must not read as "none": only an empty pool says empty.
            text += stored > 0
                ? AccessibilityStrings.RestedBonusPercent(Math.Max(percent, 1))
                : AccessibilityStrings.RestedBonusEmpty;
            _log.Info($"[Rested] Abfrage: mond={moon} hudRested={stored}/{needed} = {percent}%");
        }
        else
        {
            // At max level there is no "next level" to express the pool against,
            // and without the agent there is no pool to read at all.
            _log.Info($"[Rested] Abfrage: mond={moon} hud={(hud != null)} needed={needed} - keine Bonus-Angabe");
        }

        if (text.Length == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.RestedNotAvailable);
            return;
        }

        // The parts carry a leading space from the level announcement they used to
        // append to; as a sentence of its own that space has to go.
        _tolk.SpeakInterrupt(text.TrimStart());
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
    /// One sweep over the nearby casting enemies that drives BOTH combat warnings:
    /// the spoken cast announcement (<c>AnnounceEnemyCast</c>) and the AoE danger tone
    /// plus its standing-in-it speech (<c>AnnounceAoeWarning</c>). They share a pass
    /// because they read the same three things - who is casting, what, and where.
    ///
    /// BUG FIXED 2026-08-19: this method used to bail out on the very first line when
    /// <c>AnnounceAoeWarning</c> was off - and that option ships OFF by default. Since
    /// the cast announcement lives inside this same sweep (moved here 2026-08-18), it
    /// was dead for every player who had not turned on an unrelated option. That is
    /// why the Stone Vigil boss announced nothing. Each option now gates only its own
    /// output; the sweep itself runs whenever either one is on.
    ///
    /// The tone is ON while the player stands inside the danger zone of any nearby
    /// enemy that is currently casting, OFF the instant they leave it or the cast
    /// ends. Because it keys off <c>IsCasting</c>, the tone naturally begins with the
    /// cast bar and stops when the spell resolves. Zone shapes come from
    /// <see cref="IsPlayerInAoe"/> (circle/cone/line, measured 2026-07-26). Distance
    /// is horizontal (XZ) only - AoE telegraphs are ground planes, so a height
    /// difference must not hide or fake danger.
    /// </summary>
    private void UpdateEnemyCastWarnings(ulong playerId, Vector3 playerPos, float playerRot)
    {
        var castOn   = _config.AnnounceEnemyCast;
        var aoeOn    = _config.AnnounceAoeWarning;
        // Die Fluchtrichtung haengt am selben Schalter wie der Warnton: sie ist
        // dessen zweite Haelfte. Der Ton sagt "du stehst falsch", die Richtung
        // sagt "dorthin" - getrennt abschaltbar waere nur die halbe Auskunft.
        var escapeOn = aoeOn;
        _zoneBuf.Clear();
        if (!castOn && !aoeOn)
        {
            _aoeWarn.SetActive(false);
            _escape.Clear();
            // Drop the memos too: with both features off nothing maintains them, and
            // a stale entry would swallow the first warning after they are switched
            // back on - exactly when the player is listening for it.
            _castsAtMe.Clear();
            _aoeInside.Clear();
            return;
        }

        var sheet = _data.GetExcelSheet<LuminaAction>();
        var inDanger = false;
        var targetId = _targetManager.Target?.GameObjectId ?? 0;

        foreach (var obj in _objectTable)
        {
            if (obj is not IBattleChara bc) continue;
            // Only hostile combatants: friendly EventNpcs never threaten the player.
            if (bc.ObjectKind != ObjectKind.BattleNpc) continue;
            if (bc.GameObjectId == playerId) continue;

            if (!bc.IsCasting)
            {
                // Cast over: forget it, so the same spell warns again next time.
                _castsAtMe.Remove(bc.GameObjectId);
                _aoeInside.Remove(bc.GameObjectId);
                continue;
            }

            var relevant = bc.GameObjectId == targetId || bc.CastTargetObjectId == playerId;

            // Ground geometry of this cast, evaluated BEFORE the announcement now:
            // the sentence carries the shape, and it can only carry what has already
            // been worked out. The row lookup is cheap and only needs the cast option;
            // the position maths is the expensive half (IsPlayerInAoe walks the object
            // table for target-centred circles), so it runs only when the AoE feature
            // is on AND somebody still needs the answer.
            var wantShape = castOn && relevant;
            var wantZone  = aoeOn  && (!inDanger || relevant);

            LuminaAction? shapeRow = null;
            var inZone = false;
            // Die Flucht braucht die Flaeche JEDES Werfers, nicht nur der
            // angesagten: der sichere Punkt muss aus allen zugleich heraus
            // liegen, sonst weicht man einer Flaeche in die naechste aus.
            if ((wantShape || wantZone || escapeOn)
                && sheet.TryGetRow(bc.CastActionId, out var row)
                && row.EffectRange > 0)         // single-target / self-buff: no ground danger
            {
                shapeRow = row;
                var zone = BuildZone(bc, row, playerId, out var followsPlayer);
                if (wantZone && zone is { } z)
                {
                    inZone = z.Contains(playerPos);
                    if (inZone) inDanger = true;
                }
                // NUR belegte Formen in die Fluchtsuche. Der Warnton darf eine
                // unbekannte Form vorsichtshalber als Kreis behandeln (lieber zu
                // oft warnen), aber eine erfundene Flaeche wuerde den Spieler
                // aktiv in eine Richtung schicken, fuer die es keinen Grund gibt.
                if (escapeOn && !followsPlayer && zone is { } proven && AoeShape.HasProvenShape(row.CastType))
                    _zoneBuf.Add(proven);
            }

            // Spoken warning for every cast of the current target, plus any cast
            // aimed at the player from elsewhere. Fires regardless of EffectRange -
            // a single-target spell has no ground shape but is exactly what the
            // player wants to hear about; it just goes out without a shape.
            if (wantShape)
                AnnounceCastAtMe(bc, playerId, targetId, shapeRow, inZone);
            else if (!relevant)
            {
                // Not worth announcing right now: drop the memos so the same spell
                // speaks again if this enemy later becomes the target or aims at us.
                _castsAtMe.Remove(bc.GameObjectId);
                _aoeInside.Remove(bc.GameObjectId);
            }

            // Player walked into the zone of a cast that was already running. Only
            // for casts we announce anyway: an unannounced enemy 30 m away is not
            // worth a per-frame geometry pass, and its zone is covered by the tone.
            if (relevant && aoeOn) TrackAoeEntry(bc, inZone);
        }

        // Drop entries of casters that left the object table entirely (pulled out
        // of range, died), so the dictionaries cannot grow without bound. _aoeInside
        // is keyed the same way and rides along in the same sweep.
        if (_castsAtMe.Count > 0 || _aoeInside.Count > 0)
        {
            _castsAtMeAlive.Clear();
            foreach (var obj in _objectTable)
                // Same kind filter as the loop above, and for the same reason:
                // IsCasting dereferences GetCastInfo() unchecked. This pass only
                // runs while _castsAtMe holds something, which is why it had not
                // thrown yet - the exposure is identical.
                if (obj is IBattleChara bc && bc.ObjectKind == ObjectKind.BattleNpc && bc.IsCasting)
                    _castsAtMeAlive.Add(bc.GameObjectId);
            foreach (var id in _castsAtMe.Keys)
                if (!_castsAtMeAlive.Contains(id)) _castsAtMeStale.Add(id);
            foreach (var id in _castsAtMeStale) _castsAtMe.Remove(id);
            _castsAtMeStale.Clear();

            foreach (var id in _aoeInside.Keys)
                if (!_castsAtMeAlive.Contains(id)) _castsAtMeStale.Add(id);
            foreach (var id in _castsAtMeStale) _aoeInside.Remove(id);
            _castsAtMeStale.Clear();
        }

        _aoeWarn.SetActive(inDanger);
        // Zuletzt, damit die Suche die Flaechen dieses Frames sieht: der Ton
        // sagt, DASS man falsch steht, die Flucht sagt, wohin.
        if (escapeOn)
        {
            _escape.Update(playerPos, _zoneBuf);
            AnnounceEscapeOnce(playerPos, playerRot);
        }
        else
        {
            _escape.Clear();
            _escapeSpoken = false;
        }
    }

    /// <summary>
    /// Sagt EINMAL je Gefahrenlage, wohin man ausweichen kann - danach fuehrt der
    /// Peil-Ton. Einmal, weil die Richtung sich mit jeder Drehung des Spielers
    /// aendert: sie jedes Mal neu zu sprechen waere ein Wortschwall, der genau in
    /// den Sekunden laeuft, in denen er rennen muss.
    ///
    /// "Kein sicherer Weg gefunden" MUSS dabei gesagt werden. Der Peil-Ton
    /// schweigt in diesem Fall, und Stille heisst bei ihm sonst "du stehst
    /// richtig" - ohne den Satz waere der gefaehrlichste Fall von dem
    /// beruhigendsten nicht zu unterscheiden.
    /// </summary>
    private void AnnounceEscapeOnce(Vector3 playerPos, float playerRot)
    {
        if (!_escape.InDanger)
        {
            _escapeSpoken = false;
            return;
        }
        if (_escapeSpoken) return;

        if (_escape.SafeSpot is { } spot)
        {
            _escapeSpoken = true;
            var rel  = RelBearingDeg(playerPos, playerRot, spot);
            var dist = Vector2.Distance(new Vector2(playerPos.X, playerPos.Z),
                                        new Vector2(spot.X, spot.Z));
            SpeakWarning(AccessibilityStrings.EscapeDirection(
                AccessibilityStrings.RelativeDirection(rel),
                AccessibilityStrings.FormatDistance(dist)));
            _log.Info($"[Flucht] Sicherer Punkt {rel:F0} Grad, {dist:F1} m.");
            return;
        }

        // Noch kein Ergebnis: die Suche laeuft gedrosselt und braucht ein paar
        // Frames. Erst wenn sie WIRKLICH nichts gefunden hat, wird das gesagt -
        // sonst kaeme die Absage regelmaessig eine Zehntelsekunde vor der
        // Richtung, die es dann doch gibt.
        if (_escape.SearchExhausted)
        {
            _escapeSpoken = true;
            SpeakWarning(AccessibilityStrings.EscapeNoneFound);
        }
    }

    /// <summary>
    /// Whether the player stands inside the danger zone of one enemy cast. The zone
    /// shape comes from the action's <c>CastType</c> (verified 2026-07-26 against the
    /// telegraph graphic name <c>Omen.Path</c>: 2 = circle 'general', 3 = cone
    /// 'gl_fan090', 4 = line/rect). All maths are horizontal (XZ) only.
    /// The caster's facing (sin rot, cos rot) uses the project's verified rotation
    /// convention. Unknown CastTypes fall back to a caster-centred circle so we
    /// over-warn rather than miss - a false alarm is safer than a silent hit.
    /// <para>
    /// The CastType numbers come from <see cref="AoeShape"/> rather than being written
    /// out here, so the tone and the spoken shape can only ever agree. That also pulled
    /// in the four types measured on 2026-08-09 (5, 8, 12, 13): a 30-metre LINE used to
    /// land in the default branch and be judged as a 30-metre circle around the caster -
    /// the exact V1 mistake, and the reason the tone could sound for a player standing
    /// safely behind the enemy.
    /// </para>
    /// </summary>
    private bool IsPlayerInAoe(IBattleChara caster, LuminaAction row, Vector3 playerPos)
        => BuildZone(caster, row, 0, out _) is { } zone && zone.Contains(playerPos);

    /// <summary>
    /// Dieselbe Geometrie wie oben, aber als FLAECHE statt als Ja/Nein: eine
    /// <see cref="DangerZone"/> laesst sich fuer jeden beliebigen Punkt
    /// auswerten, nicht nur fuer den, auf dem der Spieler steht. Genau das
    /// braucht die Fluchtsuche (<see cref="EscapeRouteService"/>).
    ///
    /// Es gibt sie ABSICHTLICH nur einmal: waeren Warnton und Fluchtrichtung
    /// zwei Rechnungen, koennte der Ton "du stehst drin" sagen, waehrend die
    /// Richtung in dieselbe Flaeche hinein zeigt. Der Warnton oben ruft deshalb
    /// dieselbe Flaeche auf.
    ///
    /// Null fuer Formen ohne belegte Geometrie. Der Warnton behandelt die weiter
    /// als Kreis um den Werfer (lieber zu oft warnen als einmal zu wenig), die
    /// FLUCHT bekommt sie dagegen gar nicht erst zu sehen: eine erfundene Form
    /// wuerde den Spieler aktiv in die falsche Richtung schicken.
    /// </summary>
    private DangerZone? BuildZone(IBattleChara caster, LuminaAction row, ulong playerId, out bool followsPlayer)
    {
        followsPlayer = false;
        float range = row.EffectRange;
        var casterPos = caster.Position;

        switch (row.CastType)
        {
            // Circle placed at the cast's target. Centre on the target object if it
            // is a real object we can resolve; otherwise fall back to the caster.
            // ASSUMPTION: ground-targeted circles whose centre lives only in the VFX
            // are not yet solvable (telegraph-reading step) - flagged, verify in-game.
            case AoeShape.CastTypeCircle:
            case AoeShape.CastTypeCircle5:
            {
                var center = casterPos;
                var tid = caster.CastTargetObjectId;
                if (tid != 0 && tid != caster.GameObjectId)
                {
                    foreach (var o in _objectTable)
                        if (o.GameObjectId == tid) { center = o.Position; break; }
                }
                // EIN KREIS AUF DEM SPIELER SELBST LAESST SICH NICHT VERLASSEN.
                // Seine Mitte wird jeden Frame neu auf die eigene Position
                // gesetzt, also ist jeder Fluchtpunkt in dem Moment veraltet, in
                // dem man ihn erreicht - die Suche wuerde einen endlos vor sich
                // hertreiben. Das Spiel meint hier auch etwas anderes: weglaufen
                // von den MITSPIELERN, nicht aus der Flaeche heraus. Genau das
                // sagt die Ansage schon (AoeShapeWithRangeOnYou, "Kreis um
                // dich"), und der Warnton brummt weiter. Nur die Wegweisung
                // haelt sich raus.
                followsPlayer = tid == playerId;
                return new DangerZone(DangerShape.Circle, center, range, 0f, 0f, 0f);
            }

            // Cone from the caster along its facing. Half-angle parsed from the fan
            // number in the Omen path (gl_fan090 -> 90 deg total -> 45 deg half).
            case AoeShape.CastTypeCone:
            case AoeShape.CastTypeCone13:
                return new DangerZone(DangerShape.Cone, casterPos, range, caster.Rotation,
                                      ConeHalfAngleDeg(row) * MathF.PI / 180f, 0f);

            // Line/rectangle from the caster along its facing. Length = EffectRange,
            // ASSUMPTION half-width = XAxisModifier (verify in-game).
            case AoeShape.CastTypeLine:
            case AoeShape.CastTypeLine8:
            case AoeShape.CastTypeLine12:
                return new DangerZone(DangerShape.Line, casterPos, range, caster.Rotation, 0f,
                                      row.XAxisModifier > 0 ? row.XAxisModifier : 0.5f);

            // Not-yet-verified shapes: caster-centred circle (over-warn, never miss).
            // Nur fuer den Warnton - siehe CollectZones, das sie auslaesst.
            default:
                return new DangerZone(DangerShape.Circle, casterPos, range, 0f, 0f, 0f);
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

}
