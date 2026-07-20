using System;
using System.Collections.Concurrent;
using Dalamud.Plugin.Services;
using NAudio.Wave;
using ClientFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace FF14Accessibility.Services;

/// <summary>
/// Non-verbal HP/MP feedback: every time the fill level crosses a 10 % step -
/// downwards (damage, spending mana) or upwards (healing, regeneration) - a
/// short tone plays whose STEREO POSITION is the fill level. Full = hard right,
/// empty = hard left, 50 % = centered (user choice 2026-07-20). HP and MP are
/// told apart by pitch, an octave apart, so both can sound in the same frame
/// without being confused: HP high, MP low.
///
/// Tones only play while the game window has focus (user request 2026-07-20) -
/// nobody wants the bar beeping at them from another application.
///
/// Runs everywhere, not just in combat (user choice): out-of-combat
/// regeneration is exactly when a blind player wants to hear the bar refill.
/// This is deliberately separate from CombatService's spoken HP thresholds -
/// speech interrupts, these tones do not.
///
/// The tone generation is kept behind <see cref="PlayTone"/> because the plan
/// is to swap the sine beeps for real sound samples later; only this service
/// changes then.
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

    // Pitches: an octave apart and clear of the walk beacon's octave steps
    // (880/440/220 Hz) and the route cues (990-1568 Hz).
    // HP is the HIGHER of the two (user choice 2026-07-20, after hearing both).
    private const float HpFrequency = 1046f;  // C6 - health, the high signal
    private const float MpFrequency = 523f;   // C5 - mana, the low signal

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

        TrackVital(player.CurrentHp, player.MaxHp, HpFrequency, ref _hpLevel, "HP");
        TrackVital(player.CurrentMp, player.MaxMp, MpFrequency, ref _mpLevel, "MP");
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
    private void TrackVital(uint current, uint max, float frequency, ref int lastLevel, string label)
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

        // Window in the background: the step is recorded but stays silent. The
        // bookkeeping MUST continue anyway - otherwise everything that happened
        // while tabbed out would be announced in one go on return.
        if (!_windowActive)
        {
            _log.Debug($"[Vitals] {label} {previous * 10}% -> {percent}% - Fenster im Hintergrund, kein Ton.");
            return;
        }

        _log.Debug($"[Vitals] {label} {previous * 10}% -> {percent}% (Stufe {level})");
        PlayTone(frequency, PanFor(percent));
    }

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

    /// <summary>
    /// Queues one tone. Swap point for real sound samples later - the callers
    /// only ever say "this bar, this fill level".
    /// </summary>
    private void PlayTone(float frequency, float pan)
    {
        if (_config.VitalCueVolume <= 0f) return;
        if (!EnsureOutput()) return;
        _provider!.Enqueue(frequency, pan, _config.VitalCueVolume);
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

internal readonly record struct VitalTone(float Frequency, float Pan, float Volume);

/// <summary>
/// Generates the HP/MP tones on the NAudio playback thread: silence until a
/// tone is queued, then a 90 ms sine at the queued pitch and equal-power pan
/// (5 ms ramps against clicks). Queued rather than overwritten so an HP and an
/// MP step in the same frame are both heard, one after the other.
/// </summary>
internal sealed class VitalsSampleProvider : ISampleProvider
{
    private const int Rate        = 44100;
    private const int ToneSamples = Rate * 90 / 1000;   // 90 ms per tone
    private const int GapSamples  = Rate * 40 / 1000;   // 40 ms between queued tones
    private const int RampSamples = Rate * 5 / 1000;    // 5 ms fade in/out
    private const int MaxQueued   = 4;                  // burst guard

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    // Written from the framework thread, drained on the audio thread.
    private readonly ConcurrentQueue<VitalTone> _queue = new();

    private VitalTone _current;
    private int _remaining;   // samples left of the current tone; 0 = idle
    private int _gap;         // samples of silence left before the next tone
    private double _phase;

    public void Enqueue(float frequency, float pan, float volume)
    {
        // Dropping under a burst is intentional: a wall of tones carries no
        // more information than the last few, and must not lag behind reality.
        if (_queue.Count >= MaxQueued) return;
        _queue.Enqueue(new VitalTone(frequency, Math.Clamp(pan, -1f, 1f), Math.Clamp(volume, 0f, 1f)));
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
                var pos = ToneSamples - _remaining;

                // Phase accumulator, as in the beacon: continuous phase keeps
                // the tone click-free.
                _phase += 2.0 * Math.PI * _current.Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

                sample = (float)Math.Sin(_phase) * Envelope(pos) * _current.Volume;

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

    private static float Envelope(int pos)
    {
        if (pos < RampSamples) return pos / (float)RampSamples;
        var remaining = ToneSamples - pos;
        return remaining < RampSamples ? remaining / (float)RampSamples : 1f;
    }
}
