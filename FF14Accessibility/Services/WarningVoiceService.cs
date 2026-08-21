using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Der ZWEITE Sprachkanal, nur fuer die Kampfwarnungen: eine SAPI-Stimme, die
/// vollstaendig neben dem Screenreader laeuft.
///
/// WARUM ES IHN GIBT (Spielerwunsch 2026-08-21: *"die angriffs warnungen kommen
/// ja ueber nvda aber das kann weggedrueckt werden"*): NVDA hat GENAU EINE
/// Sprachwarteschlange, und das Plugin selbst raeumt sie staendig ab -
/// SpeakInterrupt schneidet ab, was gerade laeuft. Eine Zielwechsel-Ansage, eine
/// Chatzeile oder ein Tastendruck des Spielers loescht damit eine Warnung, die
/// eine halbe Sekunde alt ist. Dazu kommt die Stopptaste des Screenreaders, die
/// nicht zwischen "Geplapper wegdruecken" und "Warnung wegdruecken" unterscheiden
/// kann.
///
/// Ein zweiter Kanal loest das nicht durch Vorrang, sondern durch Trennung: was
/// hier gesprochen wird, steht in keiner Warteschlange, die irgendetwas anderes
/// anfassen kann.
///
/// TOLK KANN DAS NICHT LEISTEN. Die Bibliothek kennt SAPI zwar (Tolk_TrySAPI),
/// benutzt es aber als ERSATZ, wenn kein Screenreader laeuft - nicht daneben.
/// Ueber Tolk gaebe es also weiterhin einen einzigen Kanal, nur mit einer anderen
/// Stimme darin.
///
/// NICHT BLOCKIEREND: <see cref="SpeechSynthesizer.SpeakAsync(string)"/> kehrt
/// nach etwa einer Millisekunde zurueck (gemessen 2026-08-21 auf diesem Rechner).
/// Das ist die Bedingung dafuer, dass der Aufruf im Frame-Update stehen darf;
/// blockierendes Sprechen wuerde das Spiel bei jeder Warnung anhalten.
///
/// WENN SAPI NICHT DA IST, ist dieser Dienst schlicht nicht verfuegbar
/// (<see cref="IsAvailable"/>) und der Aufrufer spricht wieder ueber den
/// Screenreader. Eine Warnung, die stumm bleibt, weil ein Kanal fehlt, waere das
/// schlechtestmoegliche Ergebnis - Stille ist fuer einen blinden Spieler nicht
/// von "keine Gefahr" zu unterscheiden.
/// </summary>
public sealed class WarningVoiceService : IDisposable
{
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private SpeechSynthesizer? _synth;

    // Entprellung wie auf dem Screenreader-Kanal: derselbe Satz nicht zweimal
    // innerhalb einer halben Sekunde. Dieselbe Schwelle wie in
    // TolkService.SpeakInterrupt, damit sich beide Kanaele gleich verhalten.
    private const double DebounceSeconds = 0.5;
    private string _lastSpoken = string.Empty;
    private long   _lastSpokenTick;

    /// <summary>Die Namen aller SAPI-Stimmen, die das System anbietet - in der
    /// Reihenfolge, in der das Einstellungsmenue sie zur Wahl stellt.</summary>
    public IReadOnlyList<string> InstalledVoices { get; private set; } = Array.Empty<string>();

    /// <summary>Ob ueber diesen Kanal ueberhaupt gesprochen werden kann. Ist er
    /// false, faellt der Aufrufer auf den Screenreader zurueck.</summary>
    public bool IsAvailable => _synth != null;

    public WarningVoiceService(Configuration config, IPluginLog log)
    {
        _config = config;
        _log    = log;
        Initialize();
    }

    /// <summary>
    /// Baut den Sprecher auf. Try-catch ist hier die Ausnahme, die die Regel
    /// zulaesst: SAPI ist eine COM-Schnittstelle des Betriebssystems, also ein
    /// externer Aufruf. Geschluckt wird nichts - der Fehler geht ins Log, und
    /// <see cref="IsAvailable"/> bleibt danach false, was der Aufrufer sieht.
    /// </summary>
    private void Initialize()
    {
        try
        {
            var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();

            InstalledVoices = synth.GetInstalledVoices()
                                   .Where(v => v.Enabled)
                                   .Select(v => v.VoiceInfo.Name)
                                   .ToList();

            if (InstalledVoices.Count == 0)
            {
                _log.Warning("[Warnstimme] SAPI meldet keine einzige aktive Stimme - " +
                             "der zweite Kanal bleibt aus, die Warnungen gehen weiter ueber den Screenreader.");
                synth.Dispose();
                return;
            }

            _synth = synth;
            ApplySettings();
            _log.Info($"[Warnstimme] SAPI bereit. Stimmen: {string.Join(", ", InstalledVoices)}. " +
                      $"Gewaehlt: '{_synth.Voice.Name}'.");
        }
        catch (Exception ex)
        {
            // Auch der Konstruktor kann werfen, wenn die Sprachplattform fehlt.
            _synth = null;
            _log.Error($"[Warnstimme] SAPI nicht verfuegbar ({ex.GetType().Name}: {ex.Message}) - " +
                       "die Warnungen gehen weiter ueber den Screenreader.");
        }
    }

    /// <summary>
    /// Zieht Stimme, Tempo und Lautstaerke aus der Konfiguration nach.
    ///
    /// JEDES MAL VOR DEM SPRECHEN, nicht nur beim Anlegen: genau das war der
    /// Fehler des Warntons, der seine Lautstaerke einmal beim Aufbau las und sich
    /// danach nie wieder darum kuemmerte - eine Aenderung im Menue wirkte erst
    /// nach einem Neustart des Plugins.
    /// </summary>
    private void ApplySettings()
    {
        if (_synth == null) return;

        _synth.Volume = Math.Clamp((int)Math.Round(_config.WarningVoiceVolume * 100f), 0, 100);
        _synth.Rate   = Math.Clamp(_config.WarningVoiceRate, -10, 10);

        var wanted = ResolveVoiceName();
        if (wanted != null && !string.Equals(_synth.Voice.Name, wanted, StringComparison.Ordinal))
            _synth.SelectVoice(wanted);
    }

    /// <summary>
    /// Welche Stimme gelten soll: die eingestellte, solange es sie gibt - sonst
    /// die erste, die zur Sprache des Plugins passt.
    ///
    /// DIE SPRACHE DES PLUGINS ENTSCHEIDET, nicht die des Betriebssystems: die
    /// Warnungen kommen aus <see cref="AccessibilityStrings"/> und folgen damit
    /// "/acc lang". Eine deutsche Warnung von einer englischen Stimme gelesen ist
    /// im Ernstfall eine Silbe zu spaet verstanden.
    ///
    /// Findet sich nichts Passendes, wird NICHTS umgestellt (null) - dann bleibt
    /// die Standardstimme des Systems, und die ist immer noch besser als keine.
    /// </summary>
    private string? ResolveVoiceName()
    {
        if (_synth == null) return null;

        var chosen = _config.WarningVoiceName;
        if (!string.IsNullOrEmpty(chosen) && InstalledVoices.Contains(chosen))
            return chosen;

        var want = Loc.IsGerman ? "de" : "en";
        foreach (var voice in _synth.GetInstalledVoices())
        {
            if (!voice.Enabled) continue;
            if (voice.VoiceInfo.Culture.TwoLetterISOLanguageName
                    .Equals(want, StringComparison.OrdinalIgnoreCase))
                return voice.VoiceInfo.Name;
        }

        return null;
    }

    /// <summary>
    /// Spricht eine Warnung. Gibt false zurueck, wenn dieser Kanal sie NICHT
    /// uebernommen hat - dann muss der Aufrufer sie ueber den Screenreader sagen.
    ///
    /// EINE NEUE WARNUNG LOEST DIE ALTE AB (SpeakAsyncCancelAll davor), genau wie
    /// SpeakInterrupt es auf dem Screenreader-Kanal tut. Ohne das wuerden sich
    /// Warnungen in einem Kampf hintereinander stapeln, und die letzte - die
    /// einzige, die noch etwas mit der Lage zu tun hat - kaeme zuletzt.
    /// </summary>
    public bool Speak(string text)
    {
        if (_synth == null || string.IsNullOrWhiteSpace(text)) return false;
        if (!_config.WarningVoiceEnabled) return false;

        // Stumm geschaltet ist nicht dasselbe wie abgeschaltet: bei Lautstaerke 0
        // soll die Warnung wieder ueber den Screenreader kommen statt spurlos zu
        // verschwinden.
        if (_config.WarningVoiceVolume <= 0f) return false;

        // DIESELBE VORBEHANDLUNG WIE AUF DEM SCREENREADER-KANAL. Sie steckte in
        // TolkService.SpeakInterrupt, und wer hier vorbeigeht, verliert sie sonst
        // stillschweigend:
        //
        //   - Sanitize, weil in einer Cast-Warnung der AKTIONSNAME des Spiels
        //     steckt. Der kommt als SeString und kann Nutzlasten und
        //     Symbolzeichen tragen, die eine Stimme als Kauderwelsch vorliest.
        //   - Entprellen, weil zwei Melder dieselbe Gefahr im selben Frame
        //     ansagen koennen.
        //   - Protokollieren, weil Stille im Log sonst zweideutig ist: "nicht
        //     gewarnt" und "gewarnt, nur nicht aufgeschrieben" sehen gleich aus.
        //     Genau daran ist die Diagnose in diesem Projekt schon zweimal
        //     gescheitert.
        text = TolkService.Sanitize(text);
        if (text.Length == 0) return false;

        var now     = Stopwatch.GetTimestamp();
        var elapsed = (double)(now - _lastSpokenTick) / Stopwatch.Frequency;
        if (text == _lastSpoken && elapsed < DebounceSeconds)
        {
            // ABSICHTLICH true: dieser Kanal HAT die Warnung uebernommen und sie
            // nur eben schon gesagt. Mit false wuerde der Aufrufer sie auf den
            // Screenreader zurueckwerfen - und der Spieler hoerte den Satz doch
            // zweimal, diesmal auf zwei Stimmen verteilt.
            _log.Info($"[Warnstimme] ENTPRELLT '{text}'");
            return true;
        }

        try
        {
            ApplySettings();
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
            _lastSpoken     = text;
            _lastSpokenTick = now;
            _log.Info($"[Warnstimme] '{text}' ({_synth.Voice.Name}, Tempo {_synth.Rate}, Lautstaerke {_synth.Volume})");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"[Warnstimme] Sprechen fehlgeschlagen ({ex.GetType().Name}: {ex.Message}) - " +
                       "diese Warnung geht ueber den Screenreader.");
            return false;
        }
    }

    /// <summary>
    /// Spielt eine Stimme im Einstellungsmenue zur Probe vor - dieselbe Regel wie
    /// beim Warnton: entschieden wird am Ohr, nicht am Namen. Gibt false zurueck,
    /// wenn nichts zu hoeren war, damit die Menuezeile dann den Namen ansagt.
    /// </summary>
    public bool PlayPreview(string voiceName)
    {
        if (_synth == null) return false;
        if (_config.WarningVoiceVolume <= 0f) return false;

        try
        {
            _synth.Volume = Math.Clamp((int)Math.Round(_config.WarningVoiceVolume * 100f), 0, 100);
            _synth.Rate   = Math.Clamp(_config.WarningVoiceRate, -10, 10);
            // LEERER NAME HEISST "die, die jetzt gilt" - und das ist genau der
            // Fall, in dem sie sich gerade geaendert haben kann: nach der Wahl
            // von "Automatisch" steht im Sprecher noch die zuvor gesetzte Stimme.
            // ApplySettings loest die Automatik auf, damit die Probe die Stimme
            // vorspielt, die im Kampf auch wirklich spricht.
            if (voiceName.Length > 0)
                _synth.SelectVoice(voiceName);
            else
                ApplySettings();
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(AccessibilityStrings.WarningVoiceSample);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"[Warnstimme] Probe von '{voiceName}' fehlgeschlagen " +
                       $"({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }

    /// <summary>Der Name der Stimme, die gerade wirklich spricht - fuer die
    /// Menuezeile, die den aktuellen Wert nennt.</summary>
    public string CurrentVoiceName
    {
        get
        {
            if (_synth == null) return string.Empty;
            try { return _synth.Voice.Name; }
            catch { return string.Empty; }
        }
    }

    public void Dispose()
    {
        if (_synth == null) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error($"[Warnstimme] Aufraeumen fehlgeschlagen ({ex.GetType().Name}: {ex.Message}).");
        }
        _synth = null;
    }
}
