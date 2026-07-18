using System;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace FF14Accessibility.Services;

/// <summary>
/// Plays short one-shot audio cues (not the continuous walk-guide beacon):
/// waypoint reached and final arrival during the walk guide.
/// The output device is opened lazily on the first cue and kept open; the
/// provider feeds silence between cues.
///
/// There is deliberately NO cue for targeting an enemy: the game plays its own
/// sound for that, and doubling it up added noise instead of information
/// (removed 2026-07-18 on user report).
/// </summary>
public sealed class CueService : IDisposable
{
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private WaveOutEvent? _output;
    private CueSampleProvider? _provider;
    private bool _audioFailed;   // stop retrying after the device refused once

    public CueService(Configuration config, IPluginLog log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Walk-guide waypoint reached: one short steady high note, kept clear of
    /// the beacon range (220-880 Hz) and the rising enemy cue. Centered and
    /// non-positional on purpose - the player is standing on the waypoint.
    /// </summary>
    public void PlayWaypointTone()
    {
        if (_config.RouteCueVolume <= 0f) return;
        if (!EnsureOutput()) return;
        _provider!.Trigger(_config.RouteCueVolume, 1568f, 1568f);
    }

    /// <summary>
    /// Walk-guide final arrival: a falling two-note blip (mirror of the enemy
    /// cue) so "done" sounds distinct from "next waypoint".
    /// </summary>
    public void PlayArrivalTone()
    {
        if (_config.RouteCueVolume <= 0f) return;
        if (!EnsureOutput()) return;
        _provider!.Trigger(_config.RouteCueVolume, 1320f, 990f);
    }

    /// <summary>Opens the audio output once and keeps it. Returns false if unavailable.</summary>
    private bool EnsureOutput()
    {
        if (_output != null) return true;
        if (_audioFailed) return false;

        // try-catch: external audio API - the output device can be missing,
        // disabled or claimed exclusively. A missing cue must never disrupt
        // gameplay, so we log once, give up, and stay silent.
        try
        {
            _provider = new CueSampleProvider();
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_provider);
            _output.Play();
            return true;
        }
        catch (Exception ex)
        {
            _audioFailed = true;
            _log.Error(ex, "[Cue] Audio-Ausgabe konnte nicht starten - Ziel-Ton deaktiviert.");
            _output?.Dispose();
            _output = null;
            _provider = null;
            return false;
        }
    }

    public void Dispose()
    {
        try { _output?.Dispose(); }
        catch (Exception ex) { _log.Error(ex, "[Cue] Fehler beim Stoppen der Audio-Ausgabe"); }
        _output = null;
        _provider = null;
    }
}

/// <summary>
/// Generates one-shot cues on the NAudio playback thread. Outputs silence
/// until <see cref="Trigger"/> queues a blip; a blip is two 70 ms notes whose
/// frequencies the caller picks per cue (rising = enemy targeted, steady high
/// = waypoint reached, falling = arrived). The trigger fields are written
/// from the framework thread and read on the audio thread.
/// </summary>
internal sealed class CueSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    private const int NoteSamples = Rate * 70 / 1000;   // 70 ms per note
    private const int TotalSamples = NoteSamples * 2;    // two notes per cue
    private const int RampSamples = Rate * 4 / 1000;     // 4 ms fade to avoid clicks

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    private volatile float _volume;
    private volatile float _note1 = 990f;
    private volatile float _note2 = 1320f;
    private volatile int _remaining;   // samples left to play; 0 = silent
    private double _phase;

    /// <summary>Queues a single two-note cue at the given volume (0..1) and
    /// note frequencies (keep clear of the beacon's 220-880 Hz range).
    /// Restarts if already playing.</summary>
    public void Trigger(float volume, float note1, float note2)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        _note1 = note1;
        _note2 = note2;
        _phase = 0;
        _remaining = TotalSamples;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        for (var i = 0; i < frames; i++)
        {
            var sample = 0f;
            var remaining = _remaining;
            if (remaining > 0)
            {
                var pos = TotalSamples - remaining;
                var freq = pos < NoteSamples ? _note1 : _note2;
                _phase += 2.0 * Math.PI * freq / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                sample = (float)Math.Sin(_phase) * Envelope(pos) * _volume;
                _remaining = remaining - 1;
            }

            buffer[offset + 2 * i]     = sample;
            buffer[offset + 2 * i + 1] = sample;
        }
        return frames * 2;
    }

    // Fade each note in/out so note boundaries and start/end are click-free.
    private static float Envelope(int pos)
    {
        var inNote = pos % NoteSamples;
        if (inNote < RampSamples) return inNote / (float)RampSamples;
        var remainingInNote = NoteSamples - inNote;
        return remainingInNote < RampSamples ? remainingInNote / (float)RampSamples : 1f;
    }
}
