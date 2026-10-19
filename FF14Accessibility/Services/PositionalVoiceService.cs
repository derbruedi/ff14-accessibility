using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// SAPI voice that speaks only the player's position relative to the target
/// (front / flank / rear). Built after <see cref="WarningVoiceService"/>, but with
/// its OWN synthesizer: the user asked for a different, male voice (2026-10-14),
/// and a shared synthesizer would let a position call cancel a cast warning or
/// the other way round.
///
/// The voice is chosen per plugin language (<see cref="Loc.IsGerman"/>, i.e.
/// "/acc lang"), because the text it speaks follows that language: a configured
/// name for German and one for English, else automatically the first male voice
/// of that language, else the first voice of that language at all.
///
/// Returns false from <see cref="Speak"/> whenever it did not take the text, so
/// the caller can fall back to the screen reader - a silent channel must never
/// look like "nothing to report".
/// </summary>
public sealed class PositionalVoiceService : IDisposable
{
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private SpeechSynthesizer? _synth;

    // Same debounce as the warning voice and TolkService.SpeakInterrupt.
    private const double DebounceSeconds = 0.5;
    private string _lastSpoken = string.Empty;
    private long _lastSpokenTick;

    /// <summary>Names of all enabled SAPI voices that SAPI also lets us select
    /// (see <see cref="CanSelect"/>), in the order the options menu
    /// offers them.</summary>
    public IReadOnlyList<string> InstalledVoices { get; private set; } = Array.Empty<string>();

    /// <summary>Whether this channel can speak at all.</summary>
    public bool IsAvailable => _synth != null;

    /// <summary>Creates the synthesizer; see <see cref="IsAvailable"/>.</summary>
    public PositionalVoiceService(Configuration config, IPluginLog log)
    {
        _config = config;
        _log = log;
        Initialize();
    }

    /// <summary>
    /// try-catch: SAPI is an operating-system COM interface (external call).
    /// Nothing is swallowed - the error is logged and IsAvailable stays false,
    /// which the caller sees and answers with the screen reader.
    /// </summary>
    private void Initialize()
    {
        try
        {
            var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();

            InstalledVoices = synth.GetInstalledVoices()
                                   .Where(v => v.Enabled && CanSelect(synth, v.VoiceInfo.Name))
                                   .Select(v => v.VoiceInfo.Name)
                                   .ToList();

            if (InstalledVoices.Count == 0)
            {
                _log.Warning("[Lagestimme] SAPI reports no enabled voice - positions go through the screen reader.");
                synth.Dispose();
                return;
            }

            _synth = synth;
            _log.Info($"[Lagestimme] SAPI ready. German: '{ResolveVoiceName(german: true) ?? "-"}', " +
                      $"English: '{ResolveVoiceName(german: false) ?? "-"}'.");
        }
        catch (Exception ex)
        {
            _synth = null;
            _log.Error($"[Lagestimme] SAPI not available ({ex.GetType().Name}: {ex.Message}) - " +
                       "positions go through the screen reader.");
        }
    }

    /// <summary>
    /// Whether SAPI actually lets this voice be selected. GetInstalledVoices can
    /// list a voice as enabled that SelectVoice then refuses: on the user's PC
    /// "Nvda Sapi" (the NVDA bridge) fails with "No matching voice is installed
    /// or the voice was disabled", reproduced outside the game (2026-10-19).
    /// Why it refuses is not known. A voice SAPI refuses would make every
    /// utterance fail, so it is neither offered nor chosen automatically.
    /// try-catch: SelectVoice is the external COM call that gives the verdict;
    /// a refusal is logged, not swallowed.
    /// </summary>
    private bool CanSelect(SpeechSynthesizer synth, string voiceName)
    {
        try
        {
            synth.SelectVoice(voiceName);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"[Lagestimme] Voice '{voiceName}' is listed but cannot be selected " +
                         $"({ex.GetType().Name}: {ex.Message}) - not offered.");
            return false;
        }
    }

    /// <summary>
    /// The voice for one language: the configured one while it is installed,
    /// else the first usable male voice of that language, else the first
    /// usable voice of that language. Null = nothing fits; the synthesizer
    /// then keeps whatever voice it has.
    /// </summary>
    public string? ResolveVoiceName(bool german)
    {
        if (_synth == null) return null;

        var chosen = german ? _config.PositionalVoiceNameDe : _config.PositionalVoiceNameEn;
        if (!string.IsNullOrEmpty(chosen) && InstalledVoices.Contains(chosen))
            return chosen;

        var want = german ? "de" : "en";
        var ofLanguage = _synth.GetInstalledVoices()
            .Where(v => InstalledVoices.Contains(v.VoiceInfo.Name)
                     && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals(want, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var male = ofLanguage.FirstOrDefault(v => v.VoiceInfo.Gender == VoiceGender.Male);
        return (male ?? ofLanguage.FirstOrDefault())?.VoiceInfo.Name;
    }

    /// <summary>Applies volume, rate and voice from the config before every
    /// utterance, so menu changes take effect at once.</summary>
    private void ApplySettings(bool german)
    {
        if (_synth == null) return;

        _synth.Volume = Math.Clamp((int)Math.Round(_config.PositionalVoiceVolume * 100f), 0, 100);
        _synth.Rate = Math.Clamp(_config.PositionalVoiceRate, -10, 10);

        var wanted = ResolveVoiceName(german);
        if (wanted != null && !string.Equals(_synth.Voice.Name, wanted, StringComparison.Ordinal))
            _synth.SelectVoice(wanted);
    }

    /// <summary>
    /// Speaks one position. A new position cancels the one still being spoken:
    /// only the latest is still true. Returns false when this channel did not
    /// take the text (unavailable, volume 0, game window not focused, error).
    /// </summary>
    public bool Speak(string text)
    {
        if (_synth == null || string.IsNullOrWhiteSpace(text)) return false;
        if (!GameWindowFocus.IsActive) return false;
        if (_config.PositionalVoiceVolume <= 0f) return false;

        text = TolkService.Sanitize(text);
        if (text.Length == 0) return false;

        var now = Stopwatch.GetTimestamp();
        var elapsed = (double)(now - _lastSpokenTick) / Stopwatch.Frequency;
        if (text == _lastSpoken && elapsed < DebounceSeconds)
        {
            // True on purpose: the text WAS taken, just said a moment ago.
            _log.Info($"[Lagestimme] DEBOUNCED '{text}'");
            return true;
        }

        try
        {
            ApplySettings(Loc.IsGerman);
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
            _lastSpoken = text;
            _lastSpokenTick = now;
            _log.Info($"[Lagestimme] '{text}' ({_synth.Voice.Name}, rate {_synth.Rate}, volume {_synth.Volume})");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"[Lagestimme] Speaking failed ({ex.GetType().Name}: {ex.Message}) - " +
                       "this position goes through the screen reader.");
            return false;
        }
    }

    /// <summary>
    /// Plays the sample sentence of one language's voice in the options menu.
    /// Empty <paramref name="voiceName"/> = the voice that is in force for that
    /// language now (automatic). Returns false when nothing could be heard, so
    /// the menu speaks the choice instead.
    /// </summary>
    public bool PlayPreview(bool german, string voiceName)
    {
        if (_synth == null) return false;
        if (!GameWindowFocus.IsActive) return false;
        if (_config.PositionalVoiceVolume <= 0f) return false;

        try
        {
            if (voiceName.Length > 0)
            {
                _synth.Volume = Math.Clamp((int)Math.Round(_config.PositionalVoiceVolume * 100f), 0, 100);
                _synth.Rate = Math.Clamp(_config.PositionalVoiceRate, -10, 10);
                _synth.SelectVoice(voiceName);
            }
            else
            {
                ApplySettings(german);
            }

            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(german ? AccessibilityStrings.PositionalVoiceSampleDe
                                     : AccessibilityStrings.PositionalVoiceSampleEn);
            return true;
        }
        catch (Exception ex)
        {
            var tried = voiceName.Length > 0 ? voiceName : $"automatic: {ResolveVoiceName(german) ?? "-"}";
            _log.Error($"[Lagestimme] Preview of '{tried}' failed ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }

    /// <summary>Stops the current utterance (Alt-Tab), so SAPI does not keep
    /// talking over another application.</summary>
    public void Silence()
    {
        if (_synth == null) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch (Exception ex)
        {
            _log.Error($"[Lagestimme] Stopping failed ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>Stops speaking and releases the synthesizer.</summary>
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
            _log.Error($"[Lagestimme] Cleanup failed ({ex.GetType().Name}: {ex.Message}).");
        }
        _synth = null;
    }
}
