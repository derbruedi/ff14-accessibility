using System.Collections.Generic;
using Dalamud.Game.ClientState.JobGauge.Enums;
using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Plugin.Services;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// Watches the job gauge - the extra resource bar a job carries next to HP and
/// MP - and speaks the moment a resource becomes AVAILABLE again. A sighted
/// player glances at that bar between casts; without it a blind player only
/// learns a summon was ready by trying it and being refused.
///
/// <para>
/// EDGE-TRIGGERED, RISING ONLY (user's call 2026-08-31, verbatim: "eine voll
/// anzeige reicht nur wenn es leer war bzw nicht voll immer brauche ich die
/// anzeigen bzw ansagen nicht"). Nothing is spoken while a value merely stays
/// available, and nothing is spoken when it drops - spending a resource is the
/// player's own action and needs no report. The rising edge is the ONLY gate:
/// it fires in and out of combat alike (user's call 2026-08-31, verbatim: "die
/// meldungen ob was bereit ist kann auch ausserhalb vom kampf kommen").
/// </para>
///
/// <para>
/// SPOKEN ON THE WARNING VOICE (SAPI), NOT THE SCREEN READER - also the user's
/// call: NVDA is busy with the cast bar and the chat during a fight, and a
/// "ready" that arrives there gets cut off by the next line. The warning voice
/// is the second, independent channel built for exactly this. It falls back to
/// the screen reader when that channel is off or muted, so an announcement is
/// never lost silently. See <see cref="WarningVoiceService"/>.
/// </para>
///
/// <para>
/// ONLY WHAT THE PLAYER CAN ACTUALLY CAST (user's question 2026-08-31: "kann er
/// nur die primae ansagen die ich auch wirklich nutzen kann?"). An earlier draft
/// claimed an unlearned summon never sets its ready bit - that was an assumption,
/// never measured, and it is not what this class relies on any more. Each summon
/// is gated on its own required level, and that number is READ, not hardcoded:
/// the Action sheet carries <c>ClassJobLevel</c> per action, so the gate follows
/// the game through job reworks instead of rotting. Verified from the sheet
/// (German client, 2026-08-31): Rubin 25802 level 6, Topas 25803 level 15,
/// Smaragd 25804 level 22 (ClassJob 26 inheritance); Ifrit 25805 level 30,
/// Titan 25806 level 35, Garuda 25807 level 45 (ClassJob 27). The three ready
/// bits are the SAME for gem and primal - the gate uses the gem action so
/// Arcanist and low-level Summoner hear them; the spoken name switches to the
/// primal once that action is usable.
/// </para>
///
/// <para>
/// The action ids themselves ARE constants, and there is no way around it: the
/// gauge exposes bare bits with no reference back to an action, so the link
/// between "this bit" and "that summon" has to be stated once. Only the ids are
/// fixed - every level comes from the sheet.
/// </para>
///
/// <para>
/// Implemented for every job Dalamud exposes a gauge for (22 types). Each job
/// is one Collect method plus strings; counters announce at gauge capacity
/// (visual full), flags announce when they turn on. Every edge is level-gated
/// on the Action sheet (<c>ClassJobLevel</c>) of the linked spender/unlock —
/// same pattern as the SMN gems. Action-cost thresholds are NOT recomputed —
/// see docs/game-api.md.
/// </para>
/// </summary>
public sealed partial class JobGaugeService
{
    private const byte JobPaladin     = 19;
    private const byte JobMonk        = 20;
    private const byte JobWarrior     = 21;
    private const byte JobDragoon     = 22;
    private const byte JobBard        = 23;
    private const byte JobWhiteMage   = 24;
    private const byte JobBlackMage   = 25;
    private const byte JobArcanist    = 26;
    private const byte JobSummoner    = 27;
    private const byte JobScholar     = 28;
    private const byte JobNinja       = 30;
    private const byte JobMachinist   = 31;
    private const byte JobDarkKnight  = 32;
    private const byte JobAstrologian = 33;
    private const byte JobSamurai     = 34;
    private const byte JobRedMage     = 35;
    private const byte JobGunbreaker  = 37;
    private const byte JobDancer      = 38;
    private const byte JobReaper      = 39;
    private const byte JobSage        = 40;
    private const byte JobViper       = 41;
    private const byte JobPictomancer = 42;

    // Gauge capacities (visual full of the Dalamud/ClientStructs bar — not
    // Action PrimaryCostValue). Documented alongside Collect* methods.
    private const byte Cap100 = 100;
    private const byte CapAmmo = 3;
    private const byte CapChakra = 5;
    private const byte CapStacks3 = 3;
    private const byte CapFeathers = 4;
    private const byte CapEyes = 2;
    private const byte CapFirstminds = 2;
    private const byte CapPolyglot = 3;
    private const byte CapAstralSoul = 6;
    private const byte CapPaint = 5;
    private const byte CapRattling = 3;
    private const byte CapKazematoi = 5;

    // Gate action ids: link gauge fields to the Action sheet so ClassJobLevel
    // is READ, not hardcoded. Prefer the first PrimaryCost spender for that
    // resource; mode/flag edges use the unlock action (see docs/game-api.md).

    // SMN/ACN — gem ids gate the edge; primal ids pick the spoken name.
    private const uint ActionRuby    = 25802;
    private const uint ActionTopaz   = 25803;
    private const uint ActionEmerald = 25804;
    private const uint ActionIfrit   = 25805;
    private const uint ActionTitan   = 25806;
    private const uint ActionGaruda  = 25807;
    private const uint ActionFester  = 181;   // Aetherflow spender (ClassJob 26)

    // SAM
    private const uint ActionGekko          = 7481;
    private const uint ActionKasha          = 7482;
    private const uint ActionYukikaze       = 7480;
    private const uint ActionMidare         = 7487;
    private const uint ActionHissatsuShinten = 7490;
    private const uint ActionTsubame        = 16483;
    private const uint ActionShoha          = 16487;

    // Tanks
    private const uint ActionInnerBeast    = 49;
    private const uint ActionSheltron      = 3542;
    private const uint ActionBloodspiller  = 7392;
    private const uint ActionBlackestNight = 7393;
    private const uint ActionBurstStrike   = 16162;

    // Melee
    private const uint ActionSteelPeak       = 25761;
    private const uint ActionPerfectBalance  = 69;
    private const uint ActionMasterfulBlitz  = 25764;
    private const uint ActionPhantomRush     = 25769;
    private const uint ActionGeirskogul      = 3555;
    private const uint ActionWyrmwindThrust  = 25773;
    private const uint ActionHellfrogMedium  = 7401;
    private const uint ActionArmorCrush      = 3563;
    private const uint ActionBloodStalk      = 24389;
    private const uint ActionEnshroud        = 24394;
    private const uint ActionLemuresSlice    = 24399;
    private const uint ActionUncoiledFury    = 34633;
    private const uint ActionReawaken        = 34626;
    private const uint ActionSerpentsTail    = 35920;

    // Ranged / caster
    private const uint ActionApexArrow       = 16496;
    private const uint ActionPitchPerfect    = 7404;
    private const uint ActionRadiantFinale   = 25785;
    private const uint ActionHypercharge     = 17209;
    private const uint ActionRookAutoturret  = 2864;
    private const uint ActionFanDance        = 16007;
    private const uint ActionSaberDance      = 16005;
    private const uint ActionFoul            = 7422;
    private const uint ActionParadox         = 25797;
    private const uint ActionFlareStar       = 36989;
    private const uint ActionBlizzardIv      = 3576;
    private const uint ActionEnchantedRiposte = 7527;
    private const uint ActionVerflare        = 7525;
    private const uint ActionSubtractivePalette = 34683;
    private const uint ActionHolyInWhite     = 34662;
    private const uint ActionLivingMuse      = 35347;
    private const uint ActionSteelMuse       = 35348;
    private const uint ActionScenicMuse      = 35349;
    private const uint ActionMogOfTheAges    = 34676;
    private const uint ActionRetributionMadeen = 34677;

    // Healers
    private const uint ActionAfflatusSolace  = 16531;
    private const uint ActionAfflatusMisery  = 16535;
    private const uint ActionLustrate        = 189;
    private const uint ActionAetherpact      = 7437;
    private const uint ActionDruochole       = 24296;
    private const uint ActionToxikon         = 24304;
    private const uint ActionEukrasia        = 24290;
    private const uint ActionAstralDraw      = 37017;
    private const uint ActionMinorArcana     = 37022;

    private readonly IJobGauges          _gauges;
    private readonly IObjectTable        _objectTable;
    private readonly IDataManager        _data;
    private readonly TolkService         _tolk;
    private readonly CueService          _cue;
    private readonly Configuration       _config;
    private readonly IPluginLog          _log;

    /// <summary>Availability seen on the previous frame. The rising edge of an
    /// entry here is the whole trigger. Keyed by an internal name so one
    /// dictionary serves every job.</summary>
    private readonly Dictionary<string, bool> _lastAvailable = new();

    /// <summary>Everything that went available in the SAME frame.
    /// <see cref="WarningVoiceService.Speak"/> cancels whatever it is currently
    /// saying, so two separate calls would leave the player hearing only the
    /// second one. They go out as a single sentence instead.</summary>
    private readonly List<string> _becameAvailable = new();

    /// <summary>Ready tones queued for the same frame as <see cref="_becameAvailable"/>.
    /// Played back-to-back so multiple resources stay distinguishable.</summary>
    private readonly List<GaugeReadyCueId> _becameCues = new();

    private byte _trackedJob = byte.MaxValue;

    /// <summary>Required level per action id, as read from the Action sheet.
    /// Cached because the sheet answer never changes while the game runs, and
    /// this is asked every frame.</summary>
    private readonly Dictionary<uint, byte> _requiredLevel = new();

    /// <summary>The level the player had when the gate was last logged - so the
    /// log records the gate once per level, not once per frame. Debug only, like
    /// the probe that uses it: without the guard the release build warns about a
    /// field nobody reads.</summary>
#if DEBUG
    private byte _loggedGateLevel;
#endif

    public JobGaugeService(
        IJobGauges gauges,
        IObjectTable objectTable,
        IDataManager data,
        TolkService tolk,
        CueService cue,
        Configuration config,
        IPluginLog log)
    {
        _gauges      = gauges;
        _objectTable = objectTable;
        _data        = data;
        _tolk        = tolk;
        _cue         = cue;
        _config      = config;
        _log         = log;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update(ReadyAnnouncementBatch announcements)
    {
        if (!_config.AnnounceJobGauge) return;

        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var job = (byte)player.ClassJob.RowId;

        // A job change starts from a clean slate. Without this the first frame
        // on the new job compares against the old job's flags and fires a burst
        // of "ready" for resources the player never lost.
        if (job != _trackedJob)
        {
            _trackedJob = job;
            _lastAvailable.Clear();
            return;
        }

        _becameAvailable.Clear();
        _becameCues.Clear();

        var level = player.Level;
        switch (job)
        {
            case JobPaladin:     CollectPaladin(level); break;
            case JobMonk:        CollectMonk(level); break;
            case JobWarrior:     CollectWarrior(level); break;
            case JobDragoon:     CollectDragoon(level); break;
            case JobBard:        CollectBard(level); break;
            case JobWhiteMage:   CollectWhiteMage(level); break;
            case JobBlackMage:   CollectBlackMage(level); break;
            case JobArcanist:
            case JobSummoner:    CollectSummoner(level); break;
            case JobScholar:     CollectScholar(level); break;
            case JobNinja:       CollectNinja(level); break;
            case JobMachinist:   CollectMachinist(level); break;
            case JobDarkKnight:  CollectDarkKnight(level); break;
            case JobAstrologian: CollectAstrologian(level); break;
            case JobSamurai:     CollectSamurai(level); break;
            case JobRedMage:     CollectRedMage(level); break;
            case JobGunbreaker:  CollectGunbreaker(level); break;
            case JobDancer:      CollectDancer(level); break;
            case JobReaper:      CollectReaper(level); break;
            case JobSage:        CollectSage(level); break;
            case JobViper:       CollectViper(level); break;
            case JobPictomancer: CollectPictomancer(level); break;
            default: return;
        }

        if (_becameAvailable.Count == 0) return;

        // NO COMBAT GATE (user's call 2026-08-31, verbatim: "die meldungen ob
        // was bereit ist kann auch ausserhalb vom kampf kommen"). An earlier
        // draft dropped every edge outside combat; that also swallowed the one
        // announcement that follows a fight, when the gauge resets and the
        // summons come back. The rising edge alone decides now.
        var text = string.Join(", ", _becameAvailable) + ".";
        foreach (var cue in _becameCues)
            _cue.PlayGaugeReadyTone(cue);
        announcements.Add(text);
        _log.Info($"[Gauge] Verfuegbar geworden: {text}");
    }

    /// <summary>
    /// Summoner and Arcanist. Reads <see cref="SMNGauge"/>; the ready flags come
    /// straight from the game's own AetherFlags bit field, so no availability is
    /// recomputed here (FFXIVClientStructs SummonerGauge, offset 15). Same bits
    /// mean gem (Rubin/Topas/Smaragd) or primal (Ifrit/Titan/Garuda); the gem
    /// action gates the edge, the primal action picks the spoken name.
    /// </summary>
    private void CollectSummoner(byte level)
    {
        var g = _gauges.Get<SMNGauge>();
        if (g == null) return;

        // Gate on the gem summons so Hermetiker / SMN below 30 still hear the
        // rising edge; speak the primal name once that action is usable.
        Edge("smn.ruby", ActionRuby, level, g.IsIfritReady, SummonReadyLabel(level, ActionIfrit,
            AccessibilityStrings.GaugeRubyReady, AccessibilityStrings.GaugeIfritReady), GaugeReadyCueId.Ruby);
        Edge("smn.topaz", ActionTopaz, level, g.IsTitanReady, SummonReadyLabel(level, ActionTitan,
            AccessibilityStrings.GaugeTopazReady, AccessibilityStrings.GaugeTitanReady), GaugeReadyCueId.Topaz);
        Edge("smn.emerald", ActionEmerald, level, g.IsGarudaReady, SummonReadyLabel(level, ActionGaruda,
            AccessibilityStrings.GaugeEmeraldReady, AccessibilityStrings.GaugeGarudaReady), GaugeReadyCueId.Emerald);

        // Like Samurai "drei Sen": one extra edge when every learned gem track
        // is ready at once ("Leisten voll" / Karfunkel-Primae rufbar).
        var allGems = Usable(ActionRuby, level) && Usable(ActionTopaz, level)
            && Usable(ActionEmerald, level)
            && g.IsIfritReady && g.IsTitanReady && g.IsGarudaReady;
        Edge("smn.allgems", allGems, AccessibilityStrings.GaugeAllGemsReady, GaugeReadyCueId.AllGems);

#if DEBUG
        // Einmal je Stufe: was das Gatter gerade durchlaesst. Zeigt im Log
        // sofort, ob die Sheet-Stufen zur Wirklichkeit des Spielers passen.
        if (level != _loggedGateLevel)
        {
            _loggedGateLevel = level;
            _log.Info($"[GaugeProbe] Stufe={level} " +
                      $"Rubin>={RequiredLevel(ActionRuby)} Topas>={RequiredLevel(ActionTopaz)} " +
                      $"Smaragd>={RequiredLevel(ActionEmerald)} " +
                      $"Ifrit>={RequiredLevel(ActionIfrit)} Titan>={RequiredLevel(ActionTitan)} " +
                      $"Garuda>={RequiredLevel(ActionGaruda)}");
        }
#endif

        // Aetherflow: rising edge off zero, WITHOUT a count (encoding unmeasured —
        // see LogRawSummoner). Gate on Fester (ClassJob 26), not Energy Drain,
        // whose sheet level lies for Summoner.
        Edge("smn.aetherflow", ActionFester, level, g.AetherflowStacks > 0,
            AccessibilityStrings.GaugeAetherflowReady, GaugeReadyCueId.Aetherflow);

#if DEBUG
        LogRawSummoner(g);
#endif
    }

    /// <summary>Gem name until the primal action is usable; then the primal name
    /// (what sits on the hotbar after the job upgrade).</summary>
    private string SummonReadyLabel(byte level, uint primalAction, string gemLabel, string primalLabel) =>
        Usable(primalAction, level) ? primalLabel : gemLabel;

    /// <summary>
    /// Samurai. Reads <see cref="SAMGauge"/> — Sen flags, Kenki, MeditationStacks,
    /// and Kaeshi — straight from the game gauge (Dalamud wrappers over
    /// FFXIVClientStructs <c>SamuraiGauge</c>). No resource math is recomputed.
    /// </summary>
    private void CollectSamurai(byte level)
    {
        var g = _gauges.Get<SAMGauge>();
        if (g == null) return;

        Edge("sam.getsu", ActionGekko, level, g.HasGetsu, AccessibilityStrings.GaugeGetsuReady, GaugeReadyCueId.Getsu);
        Edge("sam.ka", ActionKasha, level, g.HasKa, AccessibilityStrings.GaugeKaReady, GaugeReadyCueId.Ka);
        Edge("sam.setsu", ActionYukikaze, level, g.HasSetsu, AccessibilityStrings.GaugeSetsuReady, GaugeReadyCueId.Setsu);
        Edge("sam.threesen", ActionMidare, level,
            g.HasGetsu && g.HasKa && g.HasSetsu,
            AccessibilityStrings.GaugeThreeSenReady, GaugeReadyCueId.ThreeSen);

        Edge("sam.kenki.full", ActionHissatsuShinten, level, g.Kenki >= 100,
            AccessibilityStrings.GaugeKenkiFull, GaugeReadyCueId.Kenki);

        // Shoha spends three Meditation stacks. Below that level the stacks may
        // already fill from Iaijutsu, but announcing them would name a spender
        // the player cannot use yet — same gate pattern as the SMN summons.
        Edge("sam.meditation.full", ActionShoha, level,
            g.MeditationStacks >= 3, AccessibilityStrings.GaugeMeditationFull, GaugeReadyCueId.Meditation);

        // Kaeshi enum (Dalamud, decompiled 2026-09-09): Higanbana=1, Goken=2,
        // Setsugekka=3, Namikiri=4. Tsubame-gaeshi cannot repeat Higanbana
        // (Action sheet description on id 16483), so only 2..4 mean a usable
        // follow-up. Zero is "none" (no named None member on the enum).
        var kaeshiReady = g.Kaeshi is Kaeshi.Goken or Kaeshi.Setsugekka or Kaeshi.Namikiri;
        Edge("sam.kaeshi", ActionTsubame, level, kaeshiReady,
            AccessibilityStrings.GaugeTsubameReady, GaugeReadyCueId.Tsubame);

#if DEBUG
        LogRawSamurai(g);
#endif
    }

    /// <summary>Speaks <paramref name="label"/> when <paramref name="available"/>
    /// goes from false to true. The first observation of a key only seeds the
    /// state - otherwise logging in with a full gauge would announce it.</summary>
    private void Edge(string key, bool available, string label, GaugeReadyCueId cue)
    {
        if (_lastAvailable.TryGetValue(key, out var was) && available && !was)
        {
            _becameAvailable.Add(label);
            _becameCues.Add(cue);
        }

        _lastAvailable[key] = available;
    }

    /// <summary>Rising edge when a counter reaches its gauge capacity (visual full).</summary>
    private void EdgeAtCap(string key, int value, int cap, string label, GaugeReadyCueId cue) =>
        Edge(key, value >= cap, label, cue);

    /// <summary>Capacity edge gated on an Action sheet level (same drop-key rule).</summary>
    private void EdgeAtCap(string key, uint actionId, byte level, int value, int cap, string label, GaugeReadyCueId cue) =>
        Edge(key, actionId, level, value >= cap, label, cue);

    /// <summary>Join parts for on-demand readout; empty → nothing ready.</summary>
    private void SpeakParts(List<string> parts) =>
        _tolk.Speak(parts.Count == 0
            ? AccessibilityStrings.GaugeNothingReady
            : string.Join(", ", parts) + ".");

    /// <summary>
    /// Same edge, but only for an action the player has actually learned. Below
    /// the required level the key is DROPPED rather than stored as false: after
    /// a level-up the next frame seeds it again, so the newly learned summon is
    /// not announced by the mere fact of learning it.
    /// </summary>
    private void Edge(string key, uint actionId, byte level, bool available, string label, GaugeReadyCueId cue)
    {
        var need = RequiredLevel(actionId);
        if (need == 0 || level < need)
        {
            _lastAvailable.Remove(key);
            return;
        }

        Edge(key, available, label, cue);
    }

    /// <summary>
    /// The level an action requires, straight from the Action sheet's
    /// <c>ClassJobLevel</c>. Returns 0 when the row is missing - the caller then
    /// stays silent rather than guessing a number.
    /// </summary>
    /// <summary>Ob der Spieler die Aktion auf seiner Stufe wirken kann. Eine
    /// fehlende Sheet-Zeile (Stufe 0) gilt als NICHT nutzbar - lieber still als
    /// eine Ansage auf einer geratenen Grundlage.</summary>
    private bool Usable(uint actionId, byte level)
    {
        var need = RequiredLevel(actionId);
        return need != 0 && level >= need;
    }

    private byte RequiredLevel(uint actionId)
    {
        if (_requiredLevel.TryGetValue(actionId, out var cached)) return cached;

        byte need = 0;
        if (_data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
            need = row.ClassJobLevel;
        else
            _log.Warning($"[Gauge] Aktion {actionId} steht nicht im Action-Sheet - Ansage bleibt aus.");

        _requiredLevel[actionId] = need;
        return need;
    }

    /// <summary>
    /// Spoken on demand, so the player can ask instead of waiting for an edge.
    /// Goes over the screen reader, not the warning voice: this one is a
    /// deliberate question, not an interruption during a cast.
    /// </summary>
    public void AnnounceCurrent()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var job = (byte)player.ClassJob.RowId;
        var level = player.Level;
        switch (job)
        {
            case JobPaladin:     AnnouncePaladin(level); break;
            case JobMonk:        AnnounceMonk(level); break;
            case JobWarrior:     AnnounceWarrior(level); break;
            case JobDragoon:     AnnounceDragoon(level); break;
            case JobBard:        AnnounceBard(level); break;
            case JobWhiteMage:   AnnounceWhiteMage(level); break;
            case JobBlackMage:   AnnounceBlackMage(level); break;
            case JobArcanist:
            case JobSummoner:    AnnounceSummoner(level); break;
            case JobScholar:     AnnounceScholar(level); break;
            case JobNinja:       AnnounceNinja(level); break;
            case JobMachinist:   AnnounceMachinist(level); break;
            case JobDarkKnight:  AnnounceDarkKnight(level); break;
            case JobAstrologian: AnnounceAstrologian(level); break;
            case JobSamurai:     AnnounceSamurai(level); break;
            case JobRedMage:     AnnounceRedMage(level); break;
            case JobGunbreaker:  AnnounceGunbreaker(level); break;
            case JobDancer:      AnnounceDancer(level); break;
            case JobReaper:      AnnounceReaper(level); break;
            case JobSage:        AnnounceSage(level); break;
            case JobViper:       AnnounceViper(level); break;
            case JobPictomancer: AnnouncePictomancer(level); break;
            default:
                _tolk.Speak(AccessibilityStrings.GaugeNoneForJob);
                break;
        }
    }

    private void AnnounceSummoner(byte lvl)
    {
        var g = _gauges.Get<SMNGauge>();
        if (g == null)
        {
            _tolk.Speak(AccessibilityStrings.GaugeNoneForJob);
            return;
        }

        // Same gem gate and name switch as the rising-edge path.
        var parts = new List<string>();
        if (Usable(ActionRuby, lvl) && g.IsIfritReady)
            parts.Add(SummonReadyLabel(lvl, ActionIfrit,
                AccessibilityStrings.GaugeRubyReady, AccessibilityStrings.GaugeIfritReady));
        if (Usable(ActionTopaz, lvl) && g.IsTitanReady)
            parts.Add(SummonReadyLabel(lvl, ActionTitan,
                AccessibilityStrings.GaugeTopazReady, AccessibilityStrings.GaugeTitanReady));
        if (Usable(ActionEmerald, lvl) && g.IsGarudaReady)
            parts.Add(SummonReadyLabel(lvl, ActionGaruda,
                AccessibilityStrings.GaugeEmeraldReady, AccessibilityStrings.GaugeGarudaReady));

        if (Usable(ActionRuby, lvl) && Usable(ActionTopaz, lvl) && Usable(ActionEmerald, lvl)
            && g.IsIfritReady && g.IsTitanReady && g.IsGarudaReady)
            parts.Add(AccessibilityStrings.GaugeAllGemsReady);

        if (Usable(ActionFester, lvl) && g.AetherflowStacks > 0)
            parts.Add(AccessibilityStrings.GaugeAetherflowReady);

        // Attunement is a countdown the player spends down, not an availability
        // edge, so it has no place in Update - but it is exactly what someone
        // asking "where do I stand" wants to hear.
        if (g.AttunementCount > 0)
            parts.Add(AccessibilityStrings.GaugeAttunement(
                AccessibilityStrings.GaugeAttunementType((byte)g.AttunementType),
                g.AttunementCount));

        _tolk.Speak(parts.Count == 0
            ? AccessibilityStrings.GaugeNothingReady
            : string.Join(", ", parts) + ".");
    }

    private void AnnounceSamurai(byte lvl)
    {
        var g = _gauges.Get<SAMGauge>();
        if (g == null)
        {
            _tolk.Speak(AccessibilityStrings.GaugeNoneForJob);
            return;
        }

        var parts = new List<string>();
        if (Usable(ActionGekko, lvl) && g.HasGetsu)
            parts.Add(AccessibilityStrings.GaugeGetsuReady);
        if (Usable(ActionKasha, lvl) && g.HasKa)
            parts.Add(AccessibilityStrings.GaugeKaReady);
        if (Usable(ActionYukikaze, lvl) && g.HasSetsu)
            parts.Add(AccessibilityStrings.GaugeSetsuReady);
        if (Usable(ActionMidare, lvl) && g.HasGetsu && g.HasKa && g.HasSetsu)
            parts.Add(AccessibilityStrings.GaugeThreeSenReady);

        if (Usable(ActionHissatsuShinten, lvl))
            parts.Add(AccessibilityStrings.GaugeKenkiAmount(g.Kenki));

        if (Usable(ActionShoha, lvl))
            parts.Add(AccessibilityStrings.GaugeMeditationAmount(g.MeditationStacks));

        if (Usable(ActionTsubame, lvl) &&
            g.Kaeshi is Kaeshi.Goken or Kaeshi.Setsugekka or Kaeshi.Namikiri)
            parts.Add(AccessibilityStrings.GaugeTsubameReady);

        SpeakParts(parts);
    }

#if DEBUG
    private byte _lastRawFlags      = byte.MaxValue;
    private byte _lastRawAttunement = byte.MaxValue;
    private int  _lastRawGlam       = -1;

    private byte _lastSamKenki      = byte.MaxValue;
    private byte _lastSamMeditation = byte.MaxValue;
    private byte _lastSamSen        = byte.MaxValue;
    private byte _lastSamKaeshi     = byte.MaxValue;

    /// <summary>
    /// Records the raw gauge bytes whenever they change. Zwei offene Fragen
    /// haengen daran, beide NICHT beantwortbar ohne Messung im Spiel:
    ///
    /// 1. WIE DER AETHERFLUSS CODIERT IST. Das Enum ist ein Bitfeld
    ///    (Aetherflow1 = 0x01, Aetherflow2 = 0x02, Aetherflow = 0x03), Dalamud
    ///    liefert als <c>AetherflowStacks</c> aber schlicht die unteren zwei
    ///    Bits als Zahl. Gemessen wurde bisher nur 0x02 - fuer ein Bitfeld mit
    ///    zwei vollen Stapeln waere 0x03 zu erwarten. Solange das nicht geklaert
    ///    ist, wird KEINE Anzahl gesprochen.
    /// 2. WELCHE KARFUNKEL-ART DAS FELD FUEHRT. <c>ReturnSummonGlam</c> kennt
    ///    Emerald/Topaz/Ruby/Carbuncle/Ifrit/Titan/Garuda, heisst aber "return"
    ///    summon - ob es die GERADE beschworene Art fuehrt oder die, zu der
    ///    nach Bahamut zurueckgekehrt wird, sagt keine der beiden DLLs.
    ///
    /// Delete together with this probe once both are measured.
    /// </summary>
    private void LogRawSummoner(SMNGauge g)
    {
        var flags = (byte)g.AetherFlags;
        var glam  = (int)g.ReturnSummonGlam;
        if (flags == _lastRawFlags && g.Attunement == _lastRawAttunement && glam == _lastRawGlam)
            return;

        _lastRawFlags      = flags;
        _lastRawAttunement = g.Attunement;
        _lastRawGlam       = glam;
        _log.Info($"[GaugeProbe] AetherFlags=0x{flags:X2} Stapel={g.AetherflowStacks} " +
                  $"Attunement=0x{g.Attunement:X2} Anzahl={g.AttunementCount} " +
                  $"Art={g.AttunementType} Karfunkel={g.ReturnSummonGlam} " +
                  $"Pet={g.ReturnSummon} eingestimmt=[{(g.IsIfritAttuned ? "Ifrit " : "")}" +
                  $"{(g.IsTitanAttuned ? "Titan " : "")}{(g.IsGarudaAttuned ? "Garuda" : "")}] " +
                  $"SummonTimer={g.SummonTimerRemaining} " +
                  $"AttunementTimer={g.AttunementTimerRemaining}");
    }

    /// <summary>
    /// Logs SAM gauge fields on change so Sen/Kenki/Meditation/Kaeshi edges can
    /// be checked against the live bar in-game.
    /// </summary>
    private void LogRawSamurai(SAMGauge g)
    {
        var sen    = (byte)g.Sen;
        var kaeshi = (byte)g.Kaeshi;
        if (g.Kenki == _lastSamKenki && g.MeditationStacks == _lastSamMeditation &&
            sen == _lastSamSen && kaeshi == _lastSamKaeshi)
            return;

        _lastSamKenki      = g.Kenki;
        _lastSamMeditation = g.MeditationStacks;
        _lastSamSen        = sen;
        _lastSamKaeshi     = kaeshi;
        _log.Info($"[GaugeProbe] SAM Sen=0x{sen:X2} Getsu={g.HasGetsu} Ka={g.HasKa} Setsu={g.HasSetsu} " +
                  $"Kenki={g.Kenki} Meditation={g.MeditationStacks} Kaeshi={g.Kaeshi}");
    }
#endif
}
