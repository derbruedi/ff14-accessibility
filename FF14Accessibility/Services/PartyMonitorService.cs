using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using NAudio.Wave;
using ClientFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace FF14Accessibility.Services;

/// <summary>
/// The party heal monitor: a running acoustic picture of every group member's
/// health, so a blind healer knows who needs attention without opening anything.
///
/// This is a deliberate port of the "Aq" health2 branch from the WoW addon Sku,
/// which blind players have used for years. The whole idea rests on packing two
/// facts into ONE short sound:
///   - WHICH member: the spoken position number (1-8).
///   - HOW BAD: the PITCH of that spoken number. Full health speaks low and calm,
///     an almost-dead member speaks high and panicky.
/// One 340 ms word therefore answers "who" and "how much" at once, and a healer
/// can follow eight people without a single menu.
///
/// The numbers line up with the game's own targeting keys - F1 is yourself and
/// F2-F8 are the party members (verified keybind dump, docs/game-api.md) - so
/// hearing "five" tells you to press F5. That correspondence is the reason the
/// monitor counts party LIST positions rather than inventing its own order.
///
/// Two independent triggers, both from Sku:
///   - EVENT: a member's health changed. Fires only when the change clears BOTH
///     a percentage threshold and a step threshold, which is what keeps the
///     monitor from chattering on every regen tick.
///   - CONTINUOUS: a slow sweep that keeps repeating anyone already below their
///     role's alarm level, so a dying tank stays audible even while nothing
///     changes.
///
/// The pitch table and its 15 steps are measured from Sku's own audio, see
/// <see cref="NumberVoiceBank"/>.
/// </summary>
public sealed class PartyMonitorService : IDisposable
{
    /// <summary>Health steps, 0 = empty .. 14 = full. Sku's resolution, kept as-is.</summary>
    private const int Steps = NumberVoiceBank.PitchLevels;

    /// <summary>Config role slots: 0 Tank, 1 Healer, 2 DPS, 3 unknown/other.</summary>
    public const int RoleCount = 4;

    // ClassJob.Role as shipped in the game's own sheet, dumped from
    // game/sqpack on 2026-08-21: 0 = crafter/gatherer, 1 = tank (GLA, MRD, PLD,
    // WAR, DRK, GNB), 2 = melee dps, 3 = ranged/magic dps, 4 = healer (CNJ, WHM,
    // SCH, AST, SGE). Not an assumption - read from the sheet.
    private const byte GameRoleTank   = 1;
    private const byte GameRoleHealer = 4;

    private readonly IPartyList     _party;
    private readonly IDataManager   _data;
    private readonly Configuration  _config;
    private readonly IPluginLog     _log;
    private readonly NumberVoiceBank _bank;

    private WaveOutEvent? _output;
    private PartyMonitorSampleProvider? _provider;
    private bool _audioFailed;

    // Health history per member, keyed by ContentId rather than by party slot.
    // Slots shift when somebody leaves, and a shifted slot would make the monitor
    // compare one player's health against another's - announcing damage that
    // never happened. ContentId follows the person.
    private readonly Dictionary<ulong, MemberState> _state = new();
    private readonly List<ulong> _seen = new();

    // Pending calls, drained one per slot. Held HERE and not in the sample
    // provider because Sku re-orders and replaces entries while they wait, which
    // is only possible as long as they have not been handed to the audio thread.
    private readonly List<PendingCall> _queue = new();
    private double _slotCooldown;
    private double _continuousTimer;

    private bool _windowActive = true;

    // Which roster the order was last logged for, so the log gets one line per
    // party rather than one per frame.
    private string _loggedOrderSignature = string.Empty;

    public PartyMonitorService(
        IPartyList party, IDataManager data, Configuration config, IPluginLog log, string assetDir)
    {
        _party  = party;
        _data   = data;
        _config = config;
        _log    = log;
        _bank   = new NumberVoiceBank(
            assetDir,
            msg => _log.Info(msg),
            (msg, ex) => _log.Error(ex, msg));
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update(double deltaSeconds)
    {
        if (!_config.PartyMonitorEnabled)
        {
            if (_state.Count > 0) _state.Clear();
            return;
        }

        if (_bank.HasFailed) return;
        if (!_bank.IsReady)
        {
            _bank.BeginLoad();
            return;
        }

        UpdateWindowActive();

        var party = OrderedParty();
        if (party.Count == 0)
        {
            // Solo: nothing to monitor. Drop the history so re-joining a party
            // does not replay the last fight's damage.
            if (_state.Count > 0) _state.Clear();
            _queue.Clear();
            return;
        }

        ScanParty(party);

        _continuousTimer += deltaSeconds;
        if (_config.PartyMonitorContinuousEnabled &&
            _continuousTimer >= _config.PartyMonitorContinuousInterval)
        {
            _continuousTimer = 0;
            SweepContinuous(party);
        }

        DrainQueue(deltaSeconds);
    }

    /// <summary>
    /// Compares every member against its remembered health and queues the ones
    /// whose change clears both thresholds.
    /// </summary>
    private void ScanParty(List<PartySlot> party)
    {
        _seen.Clear();

        foreach (var (position, member) in party)
        {
            if (member.MaxHP == 0) continue;

            var id      = member.ContentId;
            var percent = (int)(member.CurrentHP * 100u / member.MaxHP);
            var step    = StepFor(percent);
            var role    = RoleFor(member.ClassJob.RowId);
            _seen.Add(id);

            if (!_state.TryGetValue(id, out var prev))
            {
                // First sight of this member: remember only, stay silent. Covers
                // joining, zoning and resurrection.
                _state[id] = new MemberState(percent, step);
                continue;
            }

            var minPercent = Setting(_config.PartyMonitorMinPercentChange, role, 10);
            var minSteps   = Setting(_config.PartyMonitorMinStepChange, role, 1);

            // Sku's double gate: the change must be big enough BOTH in raw
            // percent and in steps. Either alone lets regen ticks through.
            var percentMoved = Math.Abs(prev.Absolute - percent) >= minPercent;
            var stepsMoved   = Math.Abs(prev.Step - step) >= minSteps;
            if (!percentMoved || !stepsMoved) continue;

            _state[id] = new MemberState(percent, step);

            if (_config.PartyMonitorSilentAtFullAndZero && (percent == 0 || percent == 100))
                continue;

            Enqueue(position, step, percent, role, _config.PartyMonitorVolume, ignorePriority: false);
        }

        PruneDeparted();
    }

    /// <summary>
    /// Repeats anyone already below their role's alarm level. Without this a
    /// member who is stable at 20 % would fall silent - the most dangerous kind
    /// of silence for a blind healer.
    /// </summary>
    private void SweepContinuous(List<PartySlot> party)
    {
        foreach (var (position, member) in party)
        {
            if (member.MaxHP == 0) continue;

            var percent = (int)(member.CurrentHP * 100u / member.MaxHP);
            var role    = RoleFor(member.ClassJob.RowId);
            var alarmAt = Setting(_config.PartyMonitorContinuousStartAt, role, 70);

            if (percent > alarmAt) continue;
            if (_config.PartyMonitorSilentAtFullAndZero && (percent == 0 || percent == 100)) continue;

            // ignorePriority: the sweep must not let a tank jump the queue over
            // and over, or a second alarm would never be heard.
            Enqueue(position, StepFor(percent), percent, role, _config.PartyMonitorVolume, ignorePriority: true);
        }
    }

    /// <summary>
    /// The party in the order the HUD shows it, numbered from 1.
    ///
    /// WHY not simply IPartyList's own order: the numbers the monitor calls out
    /// are only useful because they match the targeting keys - F1 is yourself and
    /// F2-F8 the party (verified keybind dump, docs/game-api.md). Those keys
    /// follow the PARTY LIST AS DISPLAYED, and that list is what
    /// AgentHUD.PartyMembers holds. IPartyList exposes the game's party array,
    /// which is not documented to carry the same order.
    ///
    /// So the ORDER comes from the HUD and the DATA (hp, job) from Dalamud's
    /// supported interface, matched by ContentId. If the HUD is not available -
    /// loading, or a layout without a party list - the code falls back to
    /// IPartyList's order rather than going silent.
    /// </summary>
    private unsafe List<PartySlot> OrderedParty()
    {
        var slots = new List<PartySlot>(NumberVoiceBank.MaxPosition);

        var byContentId = new Dictionary<ulong, IPartyMember>(_party.Length);
        for (var i = 0; i < _party.Length; i++)
        {
            var m = _party[i];
            if (m != null) byContentId[m.ContentId] = m;
        }

        if (byContentId.Count == 0) return slots;

        var hud = AgentHUD.Instance();
        if (hud != null && hud->PartyMemberCount > 0)
        {
            var members = hud->PartyMembers;
            var count = Math.Min((int)hud->PartyMemberCount, Math.Min(members.Length, NumberVoiceBank.MaxPosition));

            for (var i = 0; i < count; i++)
            {
                var contentId = members[i].ContentId;
                if (contentId != 0 && byContentId.TryGetValue(contentId, out var member))
                    slots.Add(new PartySlot(i + 1, member));
            }

            if (slots.Count > 0)
            {
                LogOrderMismatch(slots);
                return slots;
            }
        }

        for (var i = 0; i < Math.Min(_party.Length, NumberVoiceBank.MaxPosition); i++)
        {
            var m = _party[i];
            if (m != null) slots.Add(new PartySlot(i + 1, m));
        }

        return slots;
    }

    /// <summary>
    /// Logs once per roster whether the HUD order differs from IPartyList's. This
    /// is the evidence that settles which source is right - a blind player cannot
    /// see the party list to check, so the log has to say it.
    /// </summary>
    private void LogOrderMismatch(List<PartySlot> hudOrder)
    {
        var signature = string.Join(",", hudOrder.Select(s => s.Member.ContentId));
        if (signature == _loggedOrderSignature) return;
        _loggedOrderSignature = signature;

        var differs = false;
        for (var i = 0; i < hudOrder.Count && i < _party.Length; i++)
            if (_party[i]?.ContentId != hudOrder[i].Member.ContentId) { differs = true; break; }

        var names = string.Join(", ", hudOrder.Select(s => $"{s.Position} {s.Member.Name.TextValue}"));
        _log.Info($"[PartyMonitor] Reihenfolge aus dem HUD: {names}" +
                  $" - {(differs ? "WEICHT AB von IPartyList" : "gleich wie IPartyList")}");
    }

    /// <summary>Forgets members who left, so their history cannot resurface later.</summary>
    private void PruneDeparted()
    {
        if (_state.Count == _seen.Count) return;

        var gone = new List<ulong>();
        foreach (var id in _state.Keys)
            if (!_seen.Contains(id)) gone.Add(id);

        foreach (var id in gone) _state.Remove(id);
    }

    /// <summary>
    /// Sku's queue rule, kept intact. A member already waiting is UPDATED rather
    /// than appended - otherwise a burst of damage would queue the same number
    /// five times and the monitor would still be talking about it long after the
    /// fight moved on. The louder of the two volumes wins, and the newest health
    /// replaces the older one.
    /// </summary>
    private void Enqueue(int position, int step, int percent, int role, float volume, bool ignorePriority)
    {
        var priority = !ignorePriority && Setting(_config.PartyMonitorRolePriority, role, false);

        var existing = -1;
        for (var i = 0; i < _queue.Count; i++)
            if (_queue[i].Position == position) { existing = i; break; }

        if (existing >= 0)
        {
            var old = _queue[existing];
            volume = Math.Max(volume, old.Volume);
            _queue.RemoveAt(existing);
        }

        var call = new PendingCall(position, step, volume, percent);

        // A priority role (a tank by default) goes to the FRONT: when several
        // people are hurt at once, the one whose death ends the fight is heard
        // first.
        if (priority) _queue.Insert(0, call);
        else _queue.Add(call);
    }

    /// <summary>Hands one queued call to the audio thread per slot.</summary>
    private void DrainQueue(double deltaSeconds)
    {
        if (_slotCooldown > 0) _slotCooldown -= deltaSeconds;
        if (_queue.Count == 0 || _slotCooldown > 0) return;

        var call = _queue[0];
        _queue.RemoveAt(0);

        var number = _bank.Number(call.Position, call.Step);
        if (number == null) return;

        // The slot follows THIS word's own length, not one fixed value. Spoken
        // numbers differ a lot - measured on the English desktop voice, "one" is
        // 192 ms and "seven" 484 ms - so a fixed slot would either trample the
        // long words or leave dead air after the short ones.
        var slot = number.Length * Math.Max(10, _config.PartyMonitorSlotPercent) / 100.0;
        _slotCooldown = slot / NumberVoiceBank.Rate;

        // The window can be in the background: the queue keeps flowing so it does
        // not pile up, but nothing is played. Same rule as the HP/MP tones.
        if (_windowActive) Speak(call, number);
    }

    /// <summary>Plays one call: the pitched number, plus Sku's extra marker at the extremes.</summary>
    private void Speak(PendingCall call, float[] number)
    {
        if (!EnsureOutput()) return;

        _provider!.Enqueue(number, call.Volume);

        if (_config.PartyMonitorSpeakDeadAtZero && call.Percent == 0 && _bank.Dead != null)
            _provider.Enqueue(_bank.Dead, call.Volume);
        else if (_config.PartyMonitorSpeakFullAtHundred && call.Percent == 100 && _bank.Full != null)
            _provider.Enqueue(_bank.Full, call.Volume);
    }

    /// <summary>
    /// Health percent to Sku's 15 steps. Deliberately the same arithmetic, so a
    /// player who knows the WoW monitor hears the same pitch at the same health.
    /// </summary>
    private static int StepFor(int percent)
    {
        var step = (int)(percent / (100.0 / Steps));
        return Math.Clamp(step, 0, Steps - 1);
    }

    /// <summary>
    /// Maps a job to a config role slot using the game's own ClassJob.Role.
    /// Unknown or non-combat jobs land in the "other" slot rather than being
    /// silently treated as damage dealers.
    /// </summary>
    private int RoleFor(uint jobId)
    {
        if (!_data.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var job)) return 3;

        return job.Role switch
        {
            GameRoleTank   => 0,
            GameRoleHealer => 1,
            2 or 3         => 2,
            _              => 3,
        };
    }

    /// <summary>Reads a per-role setting, falling back when the array is short or unset.</summary>
    private static T Setting<T>(T[]? values, int role, T fallback) =>
        values != null && role >= 0 && role < values.Length ? values[role] : fallback;

    /// <summary>
    /// Reads the game's own window-focus flag, exactly as VitalsService does, so
    /// there is one source of truth for "is the player actually looking at this".
    /// </summary>
    private unsafe void UpdateWindowActive()
    {
        var framework = ClientFramework.Instance();
        _windowActive = framework == null || !framework->WindowInactive;
    }

    /// <summary>
    /// Plays one number at one health level on demand, for the sound-test
    /// audition. Bypasses the queue but honours the volume setting.
    /// </summary>
    public void PlayTestCall(int position, int percent)
    {
        if (!_bank.IsReady || !EnsureOutput()) return;
        var samples = _bank.Number(position, StepFor(percent));
        if (samples != null) _provider!.Enqueue(samples, _config.PartyMonitorVolume);
    }

    /// <summary>True once the spoken numbers are rendered and the monitor can sound.</summary>
    public bool IsVoiceReady => _bank.IsReady;

    /// <summary>
    /// The current party as position/name pairs, for the roster announcement.
    /// Lets the player confirm that "number five" really is the person on F5 -
    /// the one thing about the numbering that only in-game use can settle.
    /// </summary>
    public IReadOnlyList<(int Position, string Name)> Roster()
    {
        var list = new List<(int, string)>();

        foreach (var (position, member) in OrderedParty())
        {
            // The job goes with the name: checking the numbering against the
            // F-keys is far easier when you hear "three, warrior, Name" than a
            // bare name, because you already know who plays what.
            var name = member.Name.TextValue;
            if (_data.GetExcelSheet<ClassJob>().TryGetRow(member.ClassJob.RowId, out var job))
            {
                var jobName = job.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(jobName)) name = $"{jobName} {name}";
            }

            list.Add((position, name));
        }

        return list;
    }

    /// <summary>Opens the audio output once and keeps it. Returns false if unavailable.</summary>
    private bool EnsureOutput()
    {
        if (_output != null) return true;
        if (_audioFailed) return false;

        // try-catch: external audio API - the device can be missing, disabled or
        // claimed exclusively. A silent monitor must not disrupt gameplay.
        try
        {
            _provider = new PartyMonitorSampleProvider();
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_provider);
            _output.Play();
            return true;
        }
        catch (Exception ex)
        {
            _audioFailed = true;
            _log.Error(ex, "[PartyMonitor] Audio-Ausgabe konnte nicht starten - Heilmonitor deaktiviert.");
            _output?.Dispose();
            _output = null;
            _provider = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _output?.Dispose(); }
        catch (Exception ex) { _log.Error(ex, "[PartyMonitor] Fehler beim Stoppen der Audio-Ausgabe"); }
        _output = null;
        _provider = null;
    }

    /// <summary>One member's last announced health, used to gate the next event.</summary>
    private readonly record struct MemberState(int Absolute, int Step);

    /// <summary>A queued call: which position, at which health step and volume.</summary>
    private readonly record struct PendingCall(int Position, int Step, float Volume, int Percent);

    /// <summary>One party member together with the number the monitor calls them by.</summary>
    private readonly record struct PartySlot(int Position, IPartyMember Member);
}

/// <summary>
/// Plays the monitor's pre-rendered words on the NAudio thread. Several words can
/// sound at once: Sku lets its slot be shorter than the word so a healer can scan
/// a whole party quickly, and that only works if an overlapping word mixes in
/// instead of cutting off the previous one.
/// </summary>
internal sealed class PartyMonitorSampleProvider : ISampleProvider
{
    private const int MaxVoices = 4;   // burst guard, as in VitalsSampleProvider

    public WaveFormat WaveFormat { get; } =
        WaveFormat.CreateIeeeFloatWaveFormat(NumberVoiceBank.Rate, 2);

    private readonly ConcurrentQueue<(float[] Samples, float Volume)> _incoming = new();
    private readonly List<Voice> _active = new(MaxVoices);

    /// <summary>Queues one word. Dropped when too many already sound - a wall of
    /// numbers carries no more information than the last few.</summary>
    public void Enqueue(float[] samples, float volume)
    {
        if (_incoming.Count >= MaxVoices) return;
        _incoming.Enqueue((samples, Math.Clamp(volume, 0f, 1f)));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        while (_active.Count < MaxVoices && _incoming.TryDequeue(out var next))
            _active.Add(new Voice(next.Samples, next.Volume));

        var frames = count / 2;
        for (var i = 0; i < frames; i++)
        {
            var sample = 0f;

            for (var v = _active.Count - 1; v >= 0; v--)
            {
                var voice = _active[v];
                sample += voice.Samples[voice.Position] * voice.Volume;
                voice.Position++;
                if (voice.Position >= voice.Samples.Length) _active.RemoveAt(v);
            }

            // Soft clip: four overlapping words can otherwise sum past full scale.
            if (sample > 1f) sample = 1f;
            else if (sample < -1f) sample = -1f;

            buffer[offset + 2 * i]     = sample;
            buffer[offset + 2 * i + 1] = sample;
        }

        return frames * 2;
    }

    /// <summary>One word currently sounding. A class, not a struct, so the read
    /// loop advances the real position instead of a copy.</summary>
    private sealed class Voice
    {
        public Voice(float[] samples, float volume)
        {
            Samples = samples;
            Volume = volume;
        }

        public float[] Samples { get; }
        public float Volume { get; }
        public int Position;
    }
}
