using System;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace FF14Accessibility.Services;

/// <summary>
/// Continuous danger alarm for AoE dodging: a pulsing tone that plays for as long
/// as the player is standing inside an active enemy cast's danger zone and goes
/// silent the instant they step out (or the cast ends). Driven every frame by
/// <see cref="CombatService.UpdateAoeWarning"/> via <see cref="SetActive"/>.
///
/// Deliberately a MONO pulsing buzzer so it cannot be confused with the walk
/// guide's stereo directional beep (<see cref="BeaconService"/>): a fast pulse
/// reads as "urgent / get out", whereas the beacon is a slow steering beep.
/// </summary>
public sealed class AoeWarningService : IDisposable
{
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private WaveOutEvent? _output;
    private AoeAlarmSampleProvider? _provider;
    private bool _active;

    public AoeWarningService(Configuration config, IPluginLog log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Turns the alarm on or off. Idempotent: safe to call every frame with the
    /// same value. The audio device is started lazily on the first activation and
    /// then kept open (gated silent), so toggling stays click-free and instant.
    /// </summary>
    public void SetActive(bool active)
    {
        if (active) EnsureStarted();
        if (_provider != null) _provider.Active = active;

        if (active != _active)
        {
            _active = active;
            _log.Info($"[AoE] Warnton {(active ? "AN" : "aus")}");
        }
    }

    private void EnsureStarted()
    {
        if (_output != null) return;

        // try-catch: external audio API - the output device can be missing,
        // disabled or claimed exclusively; the warning must fail silent and never
        // take down the combat loop that drives it.
        try
        {
            _provider = new AoeAlarmSampleProvider { Volume = _config.AoeWarnVolume };
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_provider);
            _output.Play();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[AoE] Warnton: Audio-Ausgabe konnte nicht starten");
            Stop();
        }
    }

    private void Stop()
    {
        try
        {
            _output?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[AoE] Warnton: Fehler beim Stoppen der Audio-Ausgabe");
        }

        _output = null;
        _provider = null;
        _active = false;
    }

    public void Dispose() => Stop();
}

/// <summary>
/// Generates the AoE alarm: a CONTINUOUS tone at a fixed pitch for as long as it is
/// active (user request 2026-07-26 - a steady tone, not a pulse, so it clearly lasts
/// the whole time the player is in the zone). A smoothed gain ramps the tone in and
/// out over ~8 ms at on/off so toggling stays click-free; silent while inactive.
/// Read runs on the NAudio playback thread; the volatile fields are written from the
/// framework thread.
/// </summary>
internal sealed class AoeAlarmSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    // Per-sample gain step for an ~8 ms fade between silence and full tone.
    private const float RampStep = 1f / (Rate * 8 / 1000);

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1);

    public volatile bool Active;
    public volatile float Volume = 0.5f;
    public volatile float Frequency = 660f; // distinct from the beacon's 880 Hz

    private double _phase;
    private float _gain; // smoothed 0..1, ramped toward Active to avoid clicks

    public int Read(float[] buffer, int offset, int count)
    {
        var target = Active ? 1f : 0f;
        for (var i = 0; i < count; i++)
        {
            if (_gain < target) _gain = Math.Min(target, _gain + RampStep);
            else if (_gain > target) _gain = Math.Max(target, _gain - RampStep);

            var sample = 0f;
            if (_gain > 0f)
            {
                // Continuous phase accumulator: stays click-free across fades.
                _phase += 2.0 * Math.PI * Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                sample = (float)Math.Sin(_phase) * _gain * Volume;
            }

            buffer[offset + i] = sample;
        }

        return count;
    }
}
