using System;
using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using NAudio.Wave;
using ClientFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace FF14Accessibility.Services;

/// <summary>
/// Non-verbal HP/MP feedback: every time the fill level crosses a 10 % step a
/// short struck bell/pluck note plays, encoding the situation on four separate
/// perceptual axes so nothing blurs together (user choices 2026-07-20 / -07-28):
///  - which bar: HP and MP each have their own voice - HP high and warm/round,
///    MP lower and glassy/overtone-rich (pitch AND timbre, so they can sound in
///    the same frame without being confused).
///  - direction: the pitch glides up for healing/regen, down for damage/spend.
///  - fill level: STEREO POSITION. Full = hard right, empty = hard left, 50 % =
///    centered.
///  - danger: below 25 % HP the note pulses (tremolo) as a critical alarm; mana
///    never pulses, empty mana is not life-threatening.
///
/// Tones only play while the game window has focus (user request 2026-07-20) -
/// nobody wants the bar beeping at them from another application.
///
/// Runs everywhere, not just in combat (user choice): out-of-combat
/// regeneration is exactly when a blind player wants to hear the bar refill.
/// This is deliberately separate from CombatService's spoken HP thresholds -
/// speech interrupts, these tones do not.
///
/// The synthesis lives in <see cref="VitalsSampleProvider"/> and shares the
/// mod's <see cref="ToneSynth"/> voice with the navigation cues.
/// </summary>
public sealed class VitalsService : IDisposable
{
    private readonly IObjectTable  _objectTable;
    private readonly Configuration _config;
    private readonly IPluginLog    _log;

    private WaveOutEvent? _output;
    private VitalsSampleProvider? _provider;
    private bool _audioFailed;   // stop retrying after the device refused once

    // Last announced step, 0..10 (10 = full). -1 = no baseline yet: the next
    // read only sets it, silently. That covers login, zone changes and death
    // screens - none of which should fire a burst of tones.
    private int _hpLevel = -1;
    private int _mpLevel = -1;

    // Set per frame from Framework.WindowInactive: while the game window is in
    // the background the steps keep being TRACKED but no tone is played.
    private bool _windowActive = true;
    private bool _loggedWindowState;   // log the first reading, then only changes

    // Each bar is a distinct "instrument", told apart on TWO perceptual axes at
    // once so they can never be confused (user choice 2026-07-28):
    //  - pitch: HP high, MP low (HP the higher one, user choice 2026-07-20), and
    //    deliberately NOT an exact octave apart - octaves sound alike (octave
    //    equivalence), which is what made the old sine beeps easy to mix up.
    //  - timbre: HP is warm/round (almost pure, few overtones); MP is glassy and
    //    overtone-rich. Brightness feeds ToneSynth.Timbre.
    // Both stay clear of the walk beacon's octaves (880/440/220 Hz) and the route
    // cues (990-1568 Hz).
    private static readonly VitalVoice HpVoice = new(Frequency: 1046f, Brightness: 0.20f, CriticalAlarm: true);
    private static readonly VitalVoice MpVoice = new(Frequency: 494f,  Brightness: 1.00f, CriticalAlarm: false);

    // Below this fill level HP is "critical" and its tone pulses (see PlayTone).
    private const int CriticalPercent = 25;

    public VitalsService(IObjectTable objectTable, Configuration config, IPluginLog log)
    {
        _objectTable = objectTable;
        _config      = config;
        _log         = log;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        if (!_config.AnnounceVitalCues) return;

        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            // Logged out or loading: drop the baseline so coming back does not
            // sound like a full bar's worth of damage.
            _hpLevel = -1;
            _mpLevel = -1;
            return;
        }

        UpdateWindowActive();

        TrackVital(player.CurrentHp, player.MaxHp, HpVoice, ref _hpLevel, "HP");
        TrackVital(player.CurrentMp, player.MaxMp, MpVoice, ref _mpLevel, "MP");
    }

    /// <summary>
    /// Reads the game's own window-focus flag (Framework.WindowInactive,
    /// ilspycmd-verified, FieldOffset 6104). Preferred over asking Windows for
    /// the foreground window: the game already tracks this, so there is no
    /// second source of truth that could drift.
    /// If the struct is not available the tones stay ENABLED - a missing flag
    /// must not silence the feature.
    /// </summary>
    private unsafe void UpdateWindowActive()
    {
        var framework = ClientFramework.Instance();
        var active = framework == null || !framework->WindowInactive;

        if (active != _windowActive || !_loggedWindowState)
        {
            _log.Debug($"[Vitals] Spielfenster {(active ? "aktiv" : "im Hintergrund")} - Toene {(active ? "an" : "aus")}.");
            _loggedWindowState = true;
        }
        _windowActive = active;
    }

    /// <summary>
    /// Tracks one bar and plays a tone when it crosses into a new 10 % step.
    /// A big hit that skips several steps yields ONE tone for the step actually
    /// reached, not a salvo.
    /// </summary>
    private void TrackVital(uint current, uint max, VitalVoice voice, ref int lastLevel, string label)
    {
        // max == 0 means the bar does not exist for this job (no mana) or the
        // data is not ready yet. Either way: nothing to compare against.
        if (max == 0)
        {
            lastLevel = -1;
            return;
        }

        var percent = (int)(current * 100u / max);
        var level   = StepFor(percent, lastLevel);

        if (lastLevel < 0)
        {
            lastLevel = level;   // first read: baseline only, stay silent
            return;
        }

        if (level == lastLevel) return;

        var previous = lastLevel;
        lastLevel = level;   // track even while silent, see below

        // Rising step = healing/regen -> tone glides up; falling = damage/spend
        // -> tone glides down. This is the "which way is it going" axis.
        var direction = level > previous ? +1 : -1;

        // Window in the background: the step is recorded but stays silent. The
        // bookkeeping MUST continue anyway - otherwise everything that happened
        // while tabbed out would be announced in one go on return.
        if (!_windowActive)
        {
            _log.Debug($"[Vitals] {label} {previous * 10}% -> {percent}% - Fenster im Hintergrund, kein Ton.");
            return;
        }

        _log.Debug($"[Vitals] {label} {previous * 10}% -> {percent}% (Stufe {level}, {(direction > 0 ? "auf" : "ab")})");
        PlayTone(voice, PanFor(percent), direction, IsCritical(voice, percent));
    }

    /// <summary>True when this bar should sound the critical alarm at this fill
    /// level - HP only, below <see cref="CriticalPercent"/>. Empty mana is not
    /// life-threatening, so MP never pulses.</summary>
    private static bool IsCritical(VitalVoice voice, int percent) =>
        voice.CriticalAlarm && percent < CriticalPercent;

    /// <summary>
    /// Fill level (percent) to 10 % step 0..10, with 2 points of hysteresis so
    /// a value sitting exactly on a boundary - a regen tick against a damage
    /// tick - cannot rattle back and forth between two steps.
    /// </summary>
    private static int StepFor(int percent, int currentLevel)
    {
        const int Hysteresis = 2;

        var raw = Math.Clamp(percent / 10, 0, 10);
        if (currentLevel < 0 || raw == currentLevel) return raw;

        if (raw > currentLevel)
        {
            // Rising: must clear the new step's boundary by the hysteresis.
            // Step 10 is the exception - 100 % is the ceiling, there is no
            // room above it to clear.
            return raw == 10 || percent >= raw * 10 + Hysteresis ? raw : currentLevel;
        }

        // Falling: must drop below the boundary of the step being left.
        return percent <= currentLevel * 10 - Hysteresis ? raw : currentLevel;
    }

    /// <summary>Fill level to stereo position: 100 % = +1 (right), 50 % = 0, 0 % = -1 (left).</summary>
    private static float PanFor(int percent) => Math.Clamp(percent / 50f - 1f, -1f, 1f);

    // A whole tone (2 semitones) of glide, enough to hear the direction while the
    // note keeps its identity. Up = healing/gain, down = damage/spend.
    private const float GlideUp   = 1.122f;   // 2^( 2/12)
    private const float GlideDown = 0.891f;   // 2^(-2/12)

    /// <summary>
    /// Queues one tone for a bar. <paramref name="direction"/> &gt; 0 glides the
    /// pitch up (healing/gain), &lt; 0 glides it down (damage/spend);
    /// <paramref name="urgent"/> makes it pulse as a critical-HP alarm.
    /// </summary>
    private void PlayTone(VitalVoice voice, float pan, int direction, bool urgent)
    {
        if (_config.VitalCueVolume <= 0f) return;
        if (!EnsureOutput()) return;
        var glide = direction >= 0 ? GlideUp : GlideDown;
        _provider!.Enqueue(voice.Frequency, pan, _config.VitalCueVolume, voice.Brightness, glide, urgent);
    }

    /// <summary>
    /// Plays a single vitals tone on demand for the "/acc soundtest" audition, so
    /// a blind player can judge the sounds without waiting for combat.
    /// <paramref name="health"/> picks the HP or MP voice, <paramref name="direction"/>
    /// the glide (up = heal, down = damage), <paramref name="percent"/> the fill
    /// level (drives the stereo position and, for HP, the critical pulse).
    /// Bypasses the per-frame window/step tracking but honours the volume setting.
    /// </summary>
    public void PlayTestTone(bool health, int direction, int percent)
    {
        var voice = health ? HpVoice : MpVoice;
        PlayTone(voice, PanFor(percent), direction, IsCritical(voice, percent));
    }

    /// <summary>Opens the audio output once and keeps it. Returns false if unavailable.</summary>
    private bool EnsureOutput()
    {
        if (_output != null) return true;
        if (_audioFailed) return false;

        // try-catch: external audio API - the device can be missing, disabled or
        // claimed exclusively. A missing tone must never disrupt gameplay, so we
        // log once, give up and stay silent.
        try
        {
            _provider = new VitalsSampleProvider();
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_provider);
            _output.Play();
            return true;
        }
        catch (Exception ex)
        {
            _audioFailed = true;
            _log.Error(ex, "[Vitals] Audio-Ausgabe konnte nicht starten - HP/MP-Töne deaktiviert.");
            _output?.Dispose();
            _output = null;
            _provider = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _output?.Dispose(); }
        catch (Exception ex) { _log.Error(ex, "[Vitals] Fehler beim Stoppen der Audio-Ausgabe"); }
        _output = null;
        _provider = null;
    }
}

/// <summary>One bar's fixed voice: base pitch, timbre brightness (fed to
/// <see cref="ToneSynth.Timbre"/>), and whether it sounds the critical alarm
/// when it runs low. HP and MP each have their own so they stay distinct.</summary>
internal readonly record struct VitalVoice(float Frequency, float Brightness, bool CriticalAlarm);

internal readonly record struct VitalTone(
    float Frequency, float Pan, float Volume, float Brightness, float GlideFactor, bool Urgent);

/// <summary>
/// Generates the HP/MP tones on the NAudio playback thread: silence until a tone
/// is queued, then a ~150 ms struck bell/pluck note (warm crystalline timbre via
/// <see cref="ToneSynth"/>, exponential ring-out) at the queued pitch and
/// equal-power pan. The pitch glides a whole tone over the note - up for
/// healing/gain, down for damage/spend - and, when <c>Urgent</c> is set, a fast
/// tremolo makes it pulse as the critical-HP alarm. Queued rather than
/// overwritten so an HP and an MP step in the same frame are both heard.
/// </summary>
internal sealed class VitalsSampleProvider : ISampleProvider
{
    private const int   Rate            = 44100;
    private const int   ToneSamples     = Rate * 150 / 1000;   // 150 ms per note
    private const int   GapSamples      = Rate * 35 / 1000;    // 35 ms between queued notes
    private const int   AttackSamples   = Rate * 4 / 1000;     // 4 ms click-free onset
    private const int   ReleaseSamples  = Rate * 8 / 1000;     // 8 ms fade-out against the tail click
    private const float DecayTauSamples = Rate * 60 / 1000f;   // ~60 ms ring
    private const float TremoloHz       = 14f;                 // critical-HP pulse rate
    private const float TremoloDepth    = 0.5f;                // how deep the pulse dips
    private const int   MaxQueued       = 4;                   // burst guard

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    // Written from the framework thread, drained on the audio thread.
    private readonly ConcurrentQueue<VitalTone> _queue = new();

    private VitalTone _current;
    private int _remaining;   // samples left of the current tone; 0 = idle
    private int _gap;         // samples of silence left before the next tone
    private double _phase;

    public void Enqueue(float frequency, float pan, float volume, float brightness, float glideFactor, bool urgent)
    {
        // Dropping under a burst is intentional: a wall of tones carries no
        // more information than the last few, and must not lag behind reality.
        if (_queue.Count >= MaxQueued) return;
        _queue.Enqueue(new VitalTone(
            frequency,
            Math.Clamp(pan, -1f, 1f),
            Math.Clamp(volume, 0f, 1f),
            Math.Clamp(brightness, 0f, 1f),
            glideFactor,
            urgent));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        for (var i = 0; i < frames; i++)
        {
            float sample = 0f;

            if (_remaining == 0)
            {
                if (_gap > 0) _gap--;
                else if (_queue.TryDequeue(out var tone))
                {
                    _current   = tone;
                    _remaining = ToneSamples;
                    _phase     = 0;
                }
            }

            if (_remaining > 0)
            {
                var pos      = ToneSamples - _remaining;
                var progress = pos / (float)ToneSamples;

                // Glide the pitch across the note: linearly interpolate the
                // frequency from the base up/down to base * GlideFactor.
                var freq = _current.Frequency * (1f + (_current.GlideFactor - 1f) * progress);

                // Phase accumulator, as in the beacon: continuous phase keeps
                // the tone click-free even while the frequency slides.
                _phase += 2.0 * Math.PI * freq / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

                // Pluck envelope; brightness fades with it so overtones ring out
                // first (a struck bell mellows as it decays). Extra short release
                // ramp guarantees the tail is click-free.
                var env = ToneSynth.PluckEnvelope(pos, AttackSamples, DecayTauSamples);
                if (_remaining < ReleaseSamples) env *= _remaining / (float)ReleaseSamples;

                sample = ToneSynth.Timbre(_phase, _current.Brightness * env) * env * _current.Volume;

                // Critical-HP alarm: pulse the amplitude so it sounds urgent.
                if (_current.Urgent)
                {
                    var tremolo = 1f - TremoloDepth * 0.5f * (1f - MathF.Cos(2f * MathF.PI * TremoloHz * pos / Rate));
                    sample *= tremolo;
                }

                _remaining--;
                if (_remaining == 0) _gap = GapSamples;
            }

            // Equal-power pan: -1 = full left, +1 = full right.
            var panAngle = (_current.Pan + 1f) * MathF.PI / 4f;
            buffer[offset + 2 * i]     = sample * MathF.Cos(panAngle);
            buffer[offset + 2 * i + 1] = sample * MathF.Sin(panAngle);
        }

        return frames * 2;
    }
}
