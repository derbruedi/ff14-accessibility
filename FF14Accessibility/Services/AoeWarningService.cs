using System;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace FF14Accessibility.Services;

/// <summary>
/// Continuous danger alarm for AoE dodging: a tone that plays for as long as the
/// player is standing inside an active enemy cast's danger zone and goes silent
/// the instant they step out (or the cast ends). Driven every frame by
/// <see cref="CombatService.UpdateEnemyCastWarnings"/> via <see cref="SetActive"/>.
///
/// Deliberately a MONO SUSTAINED tone so it cannot be confused with the walk
/// guide's stereo directional beeps (<see cref="BeaconService"/>): this one HOLDS
/// while the danger holds, whereas every beacon voice strikes and rings out. That
/// difference is what keeps them apart while both sound at once - which they do,
/// because standing in a zone is exactly when the escape beacon steers.
///
/// (The comment here used to describe a fast pulse. That was the very first
/// design; the player asked for a steady tone on 2026-07-26 and the code has
/// produced one ever since - only this text kept saying otherwise.)
///
/// WHICH tone is the player's choice as of 2026-08-21, see <see cref="AoeWarnTone"/>.
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
        if (_provider != null)
        {
            _provider.Active = active;
            // Lautstaerke und Klang bei JEDEM Aufruf nachziehen, nicht nur beim
            // Anlegen des Providers. Vorher wurden sie einmal beim ersten Start
            // gesetzt und danach nie wieder - eine Aenderung im Menue wirkte erst
            // nach einem Neustart des Plugins, also praktisch nie. Der Peil-Ton
            // macht es seit jeher richtig (BeaconService.Update).
            ApplySettings(_provider);
        }

        if (active != _active)
        {
            _active = active;
            _log.Info($"[AoE] Warnton {(active ? "AN" : "aus")}");
        }
    }

    /// <summary>
    /// Spielt einen Klang kurz zum Probehoeren an, ohne dass eine Gefahr besteht.
    ///
    /// WOFUER: die Auswahl im Einstellungsmenue waere sonst wertlos - wer sie
    /// nur im Kampf zu hoeren bekaeme, muesste zum Vergleichen viermal in eine
    /// Flaeche laufen. Die Vorschau laeuft im Provider selbst ab (ein
    /// Sample-Zaehler), damit sie keinen Frame-Takt und keinen Timer braucht.
    ///
    /// Der uebergebene Klang wird dabei WIRKLICH gesetzt: die Menuezeile
    /// schreibt ihn ohnehin in die Konfiguration, und ein Vorhoeren, das etwas
    /// anderes spielt als das, was gilt, waere eine Luege.
    /// </summary>
    /// <returns>Ob die Probe wirklich erklingt. Falsch heisst: keine
    /// Audio-Ausgabe (fehlendes Geraet, oder die Lautstaerke steht auf aus). Der
    /// Aufrufer muss die Wahl dann in Worten bestaetigen, sonst bliebe sie
    /// unquittiert.</returns>
    public bool PlayPreview(AoeWarnTone tone)
    {
        EnsureStarted();
        if (_provider == null) return false;
        if (_config.AoeWarnVolume <= 0f) return false;

        _provider.Tone = tone;
        _provider.Volume = _config.AoeWarnVolume;
        _provider.StartPreview();
        _log.Info($"[AoE] Warnton-Probe: {tone}");
        return true;
    }

    private void ApplySettings(AoeAlarmSampleProvider provider)
    {
        provider.Volume = _config.AoeWarnVolume;
        provider.Tone   = _config.AoeWarnSound;
    }

    private void EnsureStarted()
    {
        if (_output != null) return;

        // try-catch: external audio API - the output device can be missing,
        // disabled or claimed exclusively; the warning must fail silent and never
        // take down the combat loop that drives it.
        try
        {
            _provider = new AoeAlarmSampleProvider
            {
                Volume = _config.AoeWarnVolume,
                Tone   = _config.AoeWarnSound,
            };
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
/// Generates the AoE alarm: a tone that LASTS for as long as it is active (user
/// request 2026-07-26 - a steady tone, not a pulse, so it clearly holds the whole
/// time the player is in the zone). Which tone that is comes from
/// <see cref="AoeWarnTone"/>; even the swelling voice never goes silent between
/// swells, so the "it holds" property survives every choice. A smoothed gain ramps
/// the tone in and out over ~8 ms at on/off so toggling stays click-free; silent
/// while inactive. Read runs on the NAudio playback thread; the volatile fields
/// are written from the framework thread.
/// </summary>
internal sealed class AoeAlarmSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    // Per-sample gain step for an ~8 ms fade between silence and full tone.
    private const float RampStep = 1f / (Rate * 8 / 1000);

    /// <summary>Wie lange eine Probe im Menue klingt. Lang genug, um den Charakter
    /// zu hoeren (die schwellende Stimme braucht zwei volle Wellen), kurz genug,
    /// dass sie der naechsten Menuezeile nicht im Weg steht.</summary>
    private const int PreviewSamples = Rate * 3 / 2;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1);

    public volatile bool Active;
    public volatile float Volume = 0.5f;

    /// <summary>Der gewaehlte Klang. <c>volatile</c> traegt hier, weil der
    /// zugrundeliegende Typ int ist.</summary>
    public volatile AoeWarnTone Tone = AoeWarnTone.Soft;

    // Restliche Samples der Menue-Probe. Vom Framework-Thread gesetzt, vom
    // Audio-Thread heruntergezaehlt. Der Wettlauf zwischen beiden kann die Probe
    // um einen Puffer verlaengern oder verkuerzen - mehr nicht, und dafuer
    // braucht es keine Sperre auf dem Audio-Pfad.
    private volatile int _preview;

    private double _phase;
    private double _tremoloPhase;
    private float _gain; // smoothed 0..1, ramped toward Active to avoid clicks

    /// <summary>Laesst den Ton kurz erklingen, auch wenn keine Gefahr besteht.</summary>
    public void StartPreview() => _preview = PreviewSamples;

    /// <summary>
    /// Grundton plus zwei Oberton-Anteile, auf gleiche Lautheit normiert.
    ///
    /// WARUM NICHT <see cref="ToneSynth.Timbre"/>, obwohl die Formel dieselbe ist:
    /// jene teilt fest durch 1,47, die Amplitudensumme bei VOLLER Helligkeit. Bei
    /// Helligkeit 0 - dem blanken Sinus des bisherigen Klangs - kaeme dadurch nur
    /// noch gut zwei Drittel Pegel heraus, und die Klaenge waeren beim
    /// Durchhoeren vor allem verschieden LAUT statt verschieden. Hier wird
    /// deshalb durch die Summe geteilt, die bei DIESER Helligkeit wirklich
    /// zusammenkommt. Fuer den Peil-Ton ist die feste Teilung richtig, denn dort
    /// laeuft die Helligkeit mit der Huellkurve mit und der Pegelabfall gehoert
    /// zum Ausklingen dazu - deshalb bleibt sie dort unangetastet.
    /// </summary>
    private static float Waveform(double phase, float brightness)
    {
        if (brightness <= 0f) return (float)Math.Sin(phase);

        var second = 0.35f * brightness;
        var third  = 0.12f * brightness * brightness;
        var sum = (float)Math.Sin(phase)
                + second * (float)Math.Sin(phase * 2.0)
                + third  * (float)Math.Sin(phase * 3.0);
        return sum / (1f + second + third);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var voice = AoeWarnTones.VoiceFor(Tone);
        var preview = _preview;

        // Die Probe zieht denselben Weg wie eine echte Warnung, damit sie auch
        // dieselbe Ein- und Ausblendung bekommt und nicht knackt.
        var target = Active || preview > 0 ? 1f : 0f;

        for (var i = 0; i < count; i++)
        {
            if (_gain < target) _gain = Math.Min(target, _gain + RampStep);
            else if (_gain > target) _gain = Math.Max(target, _gain - RampStep);

            var sample = 0f;
            if (_gain > 0f)
            {
                // Continuous phase accumulator: stays click-free across fades.
                _phase += 2.0 * Math.PI * voice.Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

                // Schwellen: der Pegel faellt hoechstens um TremoloDepth, nie auf
                // null. Bei TremoloHz = 0 steht der Faktor fest auf 1.
                var swell = 1f;
                if (voice.TremoloHz > 0f)
                {
                    _tremoloPhase += 2.0 * Math.PI * voice.TremoloHz / Rate;
                    if (_tremoloPhase > 2.0 * Math.PI) _tremoloPhase -= 2.0 * Math.PI;
                    // (sin + 1) / 2 laeuft zwischen 0 und 1; daraus wird ein
                    // Faktor zwischen (1 - Tiefe) und 1.
                    var wave = (float)((Math.Sin(_tremoloPhase) + 1.0) * 0.5);
                    swell = 1f - voice.TremoloDepth + voice.TremoloDepth * wave;
                }

                sample = Waveform(_phase, voice.Brightness) * _gain * Volume * swell;
            }

            buffer[offset + i] = sample;
        }

        if (preview > 0) _preview = Math.Max(0, preview - count);

        return count;
    }
}
