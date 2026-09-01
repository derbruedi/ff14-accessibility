using System.Collections.Generic;
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
/// (German client, 2026-08-31): Ifrit-Beschwoerung id 25805 level 30,
/// Titan-Beschwoerung id 25806 level 35, Garuda-Beschwoerung id 25807 level 45 -
/// all three carry ClassJob 27, so their level is the SUMMONER level.
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
/// Currently implemented: Summoner. The per-job part is deliberately one
/// method, so a second job is that method plus its strings.
/// </para>
/// </summary>
public sealed class JobGaugeService
{
    private const byte JobSummoner = 27;

    // Die Beschwoerungen hinter den drei Bereit-Bits. Nur die IDs stehen hier -
    // die Stufe dazu kommt aus dem Action-Sheet (siehe Klassenkommentar).
    private const uint ActionIfrit  = 25805;
    private const uint ActionTitan  = 25806;
    private const uint ActionGaruda = 25807;

    private readonly IJobGauges          _gauges;
    private readonly IObjectTable        _objectTable;
    private readonly IDataManager        _data;
    private readonly WarningVoiceService _warnVoice;
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
        WarningVoiceService warnVoice,
        TolkService tolk,
        CueService cue,
        Configuration config,
        IPluginLog log)
    {
        _gauges      = gauges;
        _objectTable = objectTable;
        _data        = data;
        _warnVoice   = warnVoice;
        _tolk        = tolk;
        _cue         = cue;
        _config      = config;
        _log         = log;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
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

        switch (job)
        {
            case JobSummoner: CollectSummoner(player.Level); break;
            default: return;
        }

        if (_becameAvailable.Count == 0) return;

        // NO COMBAT GATE (user's call 2026-08-31, verbatim: "die meldungen ob
        // was bereit ist kann auch ausserhalb vom kampf kommen"). An earlier
        // draft dropped every edge outside combat; that also swallowed the one
        // announcement that follows a fight, when the gauge resets and the
        // summons come back. The rising edge alone decides now.
        var text = string.Join(", ", _becameAvailable) + ".";
        _cue.PlaySkillReadyTone();
        if (!_warnVoice.Speak(text)) _tolk.Speak(text);
        _log.Info($"[Gauge] Verfuegbar geworden: {text}");
    }

    /// <summary>
    /// Summoner. Reads <see cref="SMNGauge"/>; the ready flags come straight
    /// from the game's own AetherFlags bit field, so no availability is
    /// recomputed here (FFXIVClientStructs SummonerGauge, offset 15).
    /// </summary>
    private void CollectSummoner(byte level)
    {
        var g = _gauges.Get<SMNGauge>();
        if (g == null) return;

        // Nur was der Spieler auf seiner Stufe auch wirken kann. Ohne dieses
        // Gatter haengt die Ansage daran, ob das Spiel die Bits fuer eine noch
        // nicht gelernte Beschwoerung setzt - und das ist NICHT gemessen.
        Edge("smn.ifrit",  ActionIfrit,  level, g.IsIfritReady,  AccessibilityStrings.GaugeIfritReady);
        Edge("smn.titan",  ActionTitan,  level, g.IsTitanReady,  AccessibilityStrings.GaugeTitanReady);
        Edge("smn.garuda", ActionGaruda, level, g.IsGarudaReady, AccessibilityStrings.GaugeGarudaReady);

#if DEBUG
        // Einmal je Stufe: was das Gatter gerade durchlaesst. Zeigt im Log
        // sofort, ob die Sheet-Stufen zur Wirklichkeit des Spielers passen.
        if (level != _loggedGateLevel)
        {
            _loggedGateLevel = level;
            _log.Info($"[GaugeProbe] Stufe={level} Ifrit>={RequiredLevel(ActionIfrit)} " +
                      $"Titan>={RequiredLevel(ActionTitan)} Garuda>={RequiredLevel(ActionGaruda)}");
        }
#endif

        // Aetherflow: the rising edge off zero only, and deliberately WITHOUT a
        // count. Dalamud derives the value by masking the low two bits of the
        // flag byte, and whether that yields a stack count (2) or a pair of set
        // bits (3) when both stacks are up is stated by neither DLL. Naming a
        // number here would be a guess; "available" is true either way. The
        // debug probe below records the raw value, so the count can be added
        // once it is measured instead of assumed.
        Edge("smn.aetherflow", g.AetherflowStacks > 0, AccessibilityStrings.GaugeAetherflowReady);

#if DEBUG
        LogRawSummoner(g);
#endif
    }

    /// <summary>Speaks <paramref name="label"/> when <paramref name="available"/>
    /// goes from false to true. The first observation of a key only seeds the
    /// state - otherwise logging in with a full gauge would announce it.</summary>
    private void Edge(string key, bool available, string label)
    {
        if (_lastAvailable.TryGetValue(key, out var was) && available && !was)
            _becameAvailable.Add(label);

        _lastAvailable[key] = available;
    }

    /// <summary>
    /// Same edge, but only for an action the player has actually learned. Below
    /// the required level the key is DROPPED rather than stored as false: after
    /// a level-up the next frame seeds it again, so the newly learned summon is
    /// not announced by the mere fact of learning it.
    /// </summary>
    private void Edge(string key, uint actionId, byte level, bool available, string label)
    {
        var need = RequiredLevel(actionId);
        if (need == 0 || level < need)
        {
            _lastAvailable.Remove(key);
            return;
        }

        Edge(key, available, label);
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

        if ((byte)player.ClassJob.RowId != JobSummoner)
        {
            _tolk.Speak(AccessibilityStrings.GaugeNoneForJob);
            return;
        }

        var g = _gauges.Get<SMNGauge>();
        if (g == null)
        {
            _tolk.Speak(AccessibilityStrings.GaugeNoneForJob);
            return;
        }

        // Dasselbe Stufen-Gatter wie bei den Flanken: die Nachfrage darf nichts
        // nennen, was der Spieler gar nicht wirken kann.
        var lvl = player.Level;
        var parts = new List<string>();
        if (Usable(ActionIfrit,  lvl) && g.IsIfritReady)  parts.Add(AccessibilityStrings.GaugeIfritReady);
        if (Usable(ActionTitan,  lvl) && g.IsTitanReady)  parts.Add(AccessibilityStrings.GaugeTitanReady);
        if (Usable(ActionGaruda, lvl) && g.IsGarudaReady) parts.Add(AccessibilityStrings.GaugeGarudaReady);
        if (g.AetherflowStacks > 0) parts.Add(AccessibilityStrings.GaugeAetherflowReady);

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

#if DEBUG
    private byte _lastRawFlags      = byte.MaxValue;
    private byte _lastRawAttunement = byte.MaxValue;
    private int  _lastRawGlam       = -1;

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
#endif
}
