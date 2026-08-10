using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using FF14Accessibility.Native;

namespace FF14Accessibility.Services;

public sealed class TolkService : IDisposable
{
    private readonly IPluginLog _log;

    public bool IsAvailable => TolkNative.Tolk_IsLoaded();
    public string? DetectedScreenReader { get; private set; }

    public TolkService(IPluginLog log)
    {
        _log = log;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            TolkNative.Tolk_Load();
            if (TolkNative.Tolk_IsLoaded())
            {
                DetectedScreenReader = TolkNative.GetScreenReaderName();
                _log.Info($"[Accessibility] Tolk geladen. Screenreader: {DetectedScreenReader ?? "Keiner erkannt"}, NVDA-Client vorab geladen: {TolkNative.NvdaClientPreloaded}");
                if (DetectedScreenReader == null)
                    _log.Warning("[Accessibility] Kein Screenreader erkannt - alle Ansagen bleiben stumm!");
            }
            else
            {
                _log.Warning("[Accessibility] Tolk konnte nicht geladen werden.");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"[Accessibility] Tolk Initialisierungsfehler: {ex.Message}");
        }
    }

    private string _lastSpoken = string.Empty;
    private long   _lastSpokenTick;

    // Every speech call is logged ([Speak]): silence in the log used to be
    // ambiguous - "nothing was spoken" vs "spoken but not logged" broke the
    // diagnosis twice (V4.21 Tab silence, V4.31 SelectYesno left/right).
    private static string Short(string text) =>
        text.Length > 100 ? text[..100] + "..." : text;

    /// <summary>
    /// Strips game markup before speaking so NVDA reads clean text:
    /// - SeString payloads: FFXIV wraps formatting/icon chunks between START
    ///   (0x02) and END (0x03). Utf8String.ToString() hands the raw bytes
    ///   through, so the reward Gil cell decoded as "H%I&amp;GilIH" and NPC lines
    ///   carried "\x02\x10\x01\x03" line-break chunks (log 2026-07-12). Whole
    ///   payloads are dropped. (A stray 0x03 inside nested payload data can end
    ///   a payload early, but that only ever leaks non-printing control bytes,
    ///   which the C0 filter below removes anyway.)
    /// - Icon-font glyphs in the private-use area (SeIconChar, U+E000-U+F8FF)
    ///   read as garbage ("H(icon) Dalamud Plugins"; log 2026-07-11).
    /// - U+FFFD covers bytes that failed to decode; stray C0 controls (except
    ///   tab/newline/carriage-return) are payload remnants.
    /// Collapses the whitespace the removal leaves behind.
    /// </summary>
    public static string Sanitize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var inPayload = false;
        foreach (var c in text)
        {
            if (c == '\u0002') { inPayload = true; continue; } // SeString payload START
            if (inPayload)
            {
                if (c == '\u0003') inPayload = false;          // SeString payload END
                continue;
            }
            if (c >= 0xE000 && c <= 0xF8FF) continue; // private use area (SeIconChar)
            if (c == 0xFFFD) continue;                 // replacement character
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') continue; // stray C0 control
            sb.Append(c);
        }
        var cleaned = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s{2,}", " ").Trim();
        return cleaned;
    }

    // Short rolling history of what was actually spoken, so a second source can
    // avoid re-announcing the same text. Toast notifications (_TextError etc.)
    // are mirrored into the chat log as SystemMessage a few seconds later - the
    // chat reader consults this to drop the echo (log 2026-07-12: "Du hast einen
    // Auftrag angenommen!" spoken twice).
    private readonly List<(string text, long tick)> _history = new();

    // Kept SEPARATE from _history on purpose: this list holds only wordings that
    // ANOTHER source has already spoken (see RememberSpokenVariant), and it is
    // kept far longer. The distinction matters for NPC dialogue: the chat log
    // repeats a line seconds to minutes after the Talk window showed it
    // (measured 5.5 s, and it grows with how long the player leaves the box up),
    // so the echo window has to be long - but stretching the GENERAL history
    // that far would also swallow a boss shouting the same warning twice, which
    // is exactly what a blind player must not miss.
    private readonly List<(string text, long tick)> _variants = new();

    /// <summary>How long a foreign source's wording stays known (seconds).</summary>
    private const double VariantKeepSeconds = 180;

    private void Remember(string text)
    {
        var now = Stopwatch.GetTimestamp();
        _history.Add((text, now));
        var cutoff = now - 10 * Stopwatch.Frequency; // keep ~10s
        _history.RemoveAll(e => e.tick < cutoff);
    }

    /// <summary>
    /// Files <paramref name="text"/> in the "already said this" history WITHOUT
    /// speaking it, so another source announcing the same wording can recognise
    /// it as an echo.
    ///
    /// WHY THIS IS NEEDED: <see cref="WasRecentlySpoken"/> compares whole
    /// strings, and an announcement that carries a channel prefix is not the
    /// string the other source will ask about. Measured case (log 2026-08-08
    /// 23:43:53.396/.397, user report "Systemmeldungen werden zweimal
    /// vorgelesen"): the chat reader said "System: Sind alle Gruppenmitglieder
    /// kampfunfähig, ..." and one millisecond later the toast handler asked
    /// about the same sentence WITHOUT the prefix, found no match, and read the
    /// whole thing out a second time. The guard only ever worked in the other
    /// direction (toast first, chat second), because there the chat reader
    /// checks the bare text against a bare entry.
    ///
    /// A prefix-tolerant comparison was the alternative and was rejected: "ends
    /// with" would silently swallow short genuine repeats. Letting the source
    /// that ADDED the prefix say what its plain wording was keeps the match
    /// exact.
    /// </summary>
    public void RememberSpokenVariant(string text)
    {
        // Mirrors Speak: with no screen reader attached nothing was spoken, so
        // there is no echo to suppress either.
        if (!IsAvailable || string.IsNullOrEmpty(text)) return;
        text = Sanitize(text);
        if (text.Length == 0) return;
        Remember(text);

        var now = Stopwatch.GetTimestamp();
        _variants.Add((text, now));
        var cutoff = now - (long)(VariantKeepSeconds * Stopwatch.Frequency);
        _variants.RemoveAll(e => e.tick < cutoff);
    }

    /// <summary>
    /// True if ANOTHER source already spoke <paramref name="text"/> within
    /// <paramref name="seconds"/> - i.e. it was filed via
    /// <see cref="RememberSpokenVariant"/>. Unlike <see cref="WasRecentlySpoken"/>
    /// this never matches the caller's own earlier announcements, so a line that
    /// genuinely occurs twice still gets through.
    /// </summary>
    public bool WasSpokenElsewhere(string text, double seconds)
    {
        if (string.IsNullOrEmpty(text)) return false;
        text = Sanitize(text);
        if (text.Length == 0) return false;
        var now = Stopwatch.GetTimestamp();
        var window = (long)(seconds * Stopwatch.Frequency);
        foreach (var (t, tick) in _variants)
            if (t == text && now - tick <= window) return true;
        return false;
    }

    /// <summary>True if <paramref name="text"/> (after sanitizing) was spoken
    /// within the last <paramref name="seconds"/> seconds.</summary>
    public bool WasRecentlySpoken(string text, double seconds)
    {
        if (string.IsNullOrEmpty(text)) return false;
        text = Sanitize(text);
        if (text.Length == 0) return false;
        var now = Stopwatch.GetTimestamp();
        var window = (long)(seconds * Stopwatch.Frequency);
        foreach (var (t, tick) in _history)
            if (t == text && now - tick <= window) return true;
        return false;
    }

    public void Speak(string text)
    {
        if (!IsAvailable || string.IsNullOrEmpty(text)) return;
        text = Sanitize(text);
        if (text.Length == 0) return;
        _log.Info($"[Speak] '{Short(text)}'");
        // Tolk_Output = speech AND braille (user request 2026-07-16: every
        // announcement must also reach the braille display); Tolk_Speak
        // was speech-only.
        TolkNative.Tolk_Output(text, false);
        Remember(text);
    }

    public void SpeakInterrupt(string text)
    {
        if (!IsAvailable || string.IsNullOrEmpty(text)) return;
        text = Sanitize(text);
        if (text.Length == 0) return;

        // Debouncing: Gleichen Text nicht innerhalb von 0,5 Sekunden wiederholen
        var now = Stopwatch.GetTimestamp();
        var elapsed = (double)(now - _lastSpokenTick) / Stopwatch.Frequency;
        if (text == _lastSpoken && elapsed < 0.5)
        {
            _log.Info($"[Speak] DEBOUNCED '{Short(text)}'");
            return;
        }

        // A row is often announced twice by two handlers: the richer one first
        // ("Ja, 2 Einträge" / "Elezen, männlich"), the bare label right after
        // (log 2026-07-18 11:26:00 - "Ja" spoken three more times). When the
        // previous announcement already STARTED with this exact text plus a
        // comma, the new one adds nothing - it would only repeat the word the
        // user just heard. Distinct texts are never affected.
        if (elapsed < 1.0 && _lastSpoken.StartsWith(text + ", ", StringComparison.Ordinal))
        {
            _log.Info($"[Speak] TEIL-DEBOUNCED '{Short(text)}' (steckt in '{Short(_lastSpoken)}')");
            return;
        }

        _lastSpoken = text;
        _lastSpokenTick = now;

        _log.Info($"[Speak] INT '{Short(text)}'");
        TolkNative.Tolk_Output(text, true); // speech + braille, see Speak()
        Remember(text);
    }

    /// <summary>
    /// Shows text on the braille display ONLY - no speech. Used to mirror a text
    /// field the user is typing into (chat input) so a braille reader can read
    /// back what they wrote without any spoken echo (user 2026-07-25). Not routed
    /// through the speech history/dedup - it is a live display update, not an
    /// announcement. An empty line is sent as a single space so Tolk actually
    /// clears the display (it ignores an empty string).
    /// </summary>
    public void Braille(string text)
    {
        if (!IsAvailable) return;
        text = Sanitize(text);
        if (text.Length == 0) text = " ";
        TolkNative.Tolk_Braille(text);
    }

    public void Silence()
    {
        if (!IsAvailable) return;
        TolkNative.Tolk_Silence();
    }

    public void Dispose()
    {
        if (IsAvailable)
        {
            TolkNative.Tolk_Unload();
            _log.Info("[Accessibility] Tolk entladen.");
        }
    }
}
