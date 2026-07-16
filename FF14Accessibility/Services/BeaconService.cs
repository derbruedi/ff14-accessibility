using System;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace FF14Accessibility.Services;

/// <summary>
/// Audio beacon for the walk guide: repeating beeps whose pitch and stereo
/// position encode the direction to the target. Facing the target = high
/// and centered; target to one side = tone panned to that side and lower;
/// target behind = lowest pitch. Fed every frame by NavigationService.
/// </summary>
public sealed class BeaconService : IDisposable
{
    private readonly Configuration _config;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    private WaveOutEvent? _output;
    private BeaconSampleProvider? _provider;

    public BeaconService(Configuration config, TolkService tolk, IPluginLog log)
    {
        _config = config;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>Starts the beep loop. Safe to call when already running.</summary>
    public void Start()
    {
        if (_output != null) return;

        // try-catch: external audio API - the output device can be missing,
        // disabled or claimed exclusively; the walk guide must survive that
        // and keep talking (speech ticks are independent of the beacon).
        try
        {
            _provider = new BeaconSampleProvider { Volume = _config.BeaconVolume };
            _output = new WaveOutEvent { DesiredLatency = 100 };
            _output.Init(_provider);
            _output.Play();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Beacon: Audio-Ausgabe konnte nicht starten");
            _tolk.SpeakInterrupt("Ton-Beacon nicht verfügbar.");
            Stop();
        }
    }

    /// <summary>Stops the beep loop. Safe to call when not running.</summary>
    public void Stop()
    {
        try
        {
            _output?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Beacon: Fehler beim Stoppen der Audio-Ausgabe");
        }

        _output = null;
        _provider = null;
    }

    /// <summary>
    /// Feeds the current relative angle to the steering point (degrees, 0 =
    /// straight ahead, positive = right; verified, see
    /// NavigationService.RelativeAngle) and the remaining distance to the
    /// FINAL destination in yalms/meters. The two signals are separate on
    /// purpose (user request 2026-07-16): pitch/pan steer towards the next
    /// waypoint, the volume tracks journey progress - quiet when the goal is
    /// far, swelling towards arrival. Tying the volume to the waypoint made
    /// the tone permanently loud (waypoints are always near).
    /// </summary>
    public void Update(double relAngleDegrees, float distanceToDestination)
    {
        var provider = _provider;
        if (provider == null) return;

        // Pitch: 880 Hz straight ahead, one octave down per 90° off,
        // i.e. 220 Hz when the target is directly behind.
        var absAngle = Math.Abs(relAngleDegrees);
        provider.Frequency = (float)(880.0 * Math.Pow(2.0, -absAngle / 90.0));

        // Pan follows the side the target is on: sin(0°)=0 centered,
        // sin(±90°)=±1 fully left/right, sin(±180°)=0 centered again
        // (behind is encoded by the low pitch, not the pan).
        provider.Pan = (float)Math.Sin(relAngleDegrees * Math.PI / 180.0);

        // Louder when close to the DESTINATION: full volume at 5 m and below,
        // fading linearly to 20% at 200 m and beyond (quest goals sit hundreds
        // of metres out; the 20% floor keeps the tone audible for steering).
        provider.DistanceFactor = Math.Clamp(1f - (distanceToDestination - 5f) / 195f * 0.8f, 0.2f, 1f);
    }

    public void Dispose() => Stop();
}

/// <summary>
/// Generates the beacon signal: a beep every 0.5 s (150 ms long, 5 ms
/// attack/release ramps against clicks) with live-adjustable frequency,
/// equal-power pan and volume. Read runs on the NAudio playback thread;
/// the volatile fields are written from the framework thread.
/// </summary>
internal sealed class BeaconSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    private const int BeepPeriodSamples = Rate / 2;       // one beep every 0.5 s
    private const int BeepLengthSamples = Rate * 150 / 1000; // 150 ms beep
    private const int RampSamples = Rate * 5 / 1000;      // 5 ms fade in/out

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    public volatile float Frequency = 880f;
    public volatile float Pan;                 // -1 = left, +1 = right
    public volatile float Volume = 0.35f;
    public volatile float DistanceFactor = 1f; // 1 = close/loud, 0.2 = far/quiet

    private double _phase;
    private int _posInPeriod;

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;
        for (var i = 0; i < frames; i++)
        {
            var envelope = Envelope(_posInPeriod);
            var sample = 0f;
            if (envelope > 0f)
            {
                // Phase accumulator: frequency changes stay click-free
                // because the phase is continuous.
                _phase += 2.0 * Math.PI * Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                sample = (float)Math.Sin(_phase) * envelope * Volume * DistanceFactor;
            }

            var panAngle = (Pan + 1f) * MathF.PI / 4f;
            buffer[offset + 2 * i]     = sample * MathF.Cos(panAngle);
            buffer[offset + 2 * i + 1] = sample * MathF.Sin(panAngle);

            _posInPeriod = (_posInPeriod + 1) % BeepPeriodSamples;
        }

        return frames * 2;
    }

    private static float Envelope(int posInPeriod)
    {
        if (posInPeriod >= BeepLengthSamples) return 0f;
        if (posInPeriod < RampSamples) return posInPeriod / (float)RampSamples;
        var remaining = BeepLengthSamples - posInPeriod;
        return remaining < RampSamples ? remaining / (float)RampSamples : 1f;
    }
}
