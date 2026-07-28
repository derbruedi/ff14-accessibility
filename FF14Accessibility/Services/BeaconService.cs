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
            _tolk.SpeakInterrupt(AccessibilityStrings.BeaconUnavailable);
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

        // Pitch: a warm D5 (587 Hz) straight ahead, gliding down about an octave
        // and a half to ~208 Hz when the target is directly behind. The lower,
        // gentler range is far less piercing than the old 220-880 Hz sweep while
        // still giving clear "ahead = high" steering feedback.
        var absAngle = Math.Abs(relAngleDegrees);
        provider.Frequency = (float)(587.0 * Math.Pow(2.0, -absAngle / 120.0));

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
/// Generates the beacon signal: a soft plucked bell-note every 0.5 s with a warm
/// crystalline timbre (<see cref="ToneSynth"/>) and live-adjustable frequency,
/// equal-power pan and volume. The note is struck (short attack) and rings out
/// with an exponential decay, leaving quiet space before the next one - much
/// gentler on the ear than the old flat 150 ms sine beep. Read runs on the NAudio
/// playback thread; the volatile fields are written from the framework thread.
/// </summary>
internal sealed class BeaconSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    private const int BeepPeriodSamples = Rate / 2;           // one pluck every 0.5 s
    private const int AttackSamples = Rate * 4 / 1000;        // 4 ms click-free onset
    private const float DecayTauSamples = Rate * 110 / 1000f; // ~110 ms ring-out (near-silent by ~350 ms)

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    public volatile float Frequency = 587f;
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
            // Each 0.5 s period strikes a fresh pluck; reset the phase at the
            // period start (the previous note has decayed to near-silence, so
            // this is click-free) to keep every note identical.
            if (_posInPeriod == 0) _phase = 0;

            var env = ToneSynth.PluckEnvelope(_posInPeriod, AttackSamples, DecayTauSamples);
            var sample = 0f;
            if (env > 0.0008f)
            {
                _phase += 2.0 * Math.PI * Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                // Brightness follows the envelope so the overtones fade first, the
                // way a real struck bell mellows as it rings out.
                sample = ToneSynth.Timbre(_phase, env) * env * Volume * DistanceFactor;
            }

            var panAngle = (Pan + 1f) * MathF.PI / 4f;
            buffer[offset + 2 * i]     = sample * MathF.Cos(panAngle);
            buffer[offset + 2 * i + 1] = sample * MathF.Sin(panAngle);

            _posInPeriod = (_posInPeriod + 1) % BeepPeriodSamples;
        }

        return frames * 2;
    }
}
