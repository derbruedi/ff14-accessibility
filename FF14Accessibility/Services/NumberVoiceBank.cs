using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace FF14Accessibility.Services;

/// <summary>
/// The spoken material for the party heal monitor: the position numbers 1-8 at
/// 15 pitch levels, plus the two special calls ("dead", "full").
///
/// These are the ORIGINAL recordings from the WoW addon Sku, which blind players
/// have used for years - the same voice, the same pitch ladder. Nothing is
/// synthesised and nothing is pitch-shifted at runtime, so the words sound
/// exactly as clean as they do in Sku.
///
/// Two earlier approaches were tried and measured against this one, both worse:
///   - Synthesising the numbers with the Windows voice and pitch-shifting them
///     with a phase vocoder. The vocoder smears consonants, which made the
///     numbers hard to tell apart (user report 2026-08-21) - and telling them
///     apart is the entire feature.
///   - Letting the speech engine pitch the words itself via SSML. Measured on
///     Microsoft David Desktop: the fundamental stayed at 81.4 Hz for every
///     value from -50 % to +50 %. The old SAPI5 voices ignore prosody pitch
///     outright, so this route does not exist.
///
/// The pitch ladder is Sku's, verified by measuring its files: 15 steps five
/// percent apart, factor = 1 + percent/100, and the duration stays constant at
/// 341.8 ms. Full health speaks low and calm at 148 Hz, nearly dead speaks high
/// and panicky at 306 Hz. See ki bereich/wissen/sku-wow/aq-tonhoehen-messung.md.
///
/// Provenance and licence of the audio: see THIRD-PARTY-NOTICES.md.
/// </summary>
internal sealed class NumberVoiceBank
{
    /// <summary>Highest party position the monitor can call out (FF14 full party).</summary>
    public const int MaxPosition = 8;

    /// <summary>Number of pitch levels, matching Sku's 15 health steps.</summary>
    public const int PitchLevels = 15;

    /// <summary>Sample rate everything is converted to; matches the other cue providers.</summary>
    public const int Rate = 44100;

    // Sku's step-to-pitch rule: pitch = ((step * 5) - 35) * -1, so step 0 (empty)
    // gives +35 and step 14 (full) gives -35, in increments of 5. The file names
    // in assets/partymonitor carry exactly these numbers.
    private const int PitchStepPercent = 5;
    private const int PitchSpanPercent = 35;

    private readonly string _assetDir;
    private readonly Action<string> _logInfo;
    private readonly Action<string, Exception> _logError;

    // [position 0..7][step 0..14]. Null until loading has finished.
    private float[][][]? _numbers;
    private float[]? _dead;
    private float[]? _full;

    private volatile bool _ready;
    private volatile bool _failed;
    private Task? _load;

    public NumberVoiceBank(string assetDir, Action<string> logInfo, Action<string, Exception> logError)
    {
        _assetDir = assetDir;
        _logInfo  = logInfo;
        _logError = logError;
    }

    /// <summary>True once every clip is loaded and <see cref="Number"/> returns audio.</summary>
    public bool IsReady => _ready;

    /// <summary>True when loading failed; the monitor then stays silent instead of retrying.</summary>
    public bool HasFailed => _failed;

    /// <summary>
    /// Converts Sku's health step (0 = empty .. 14 = full) to its pitch percentage
    /// (+35 .. -35). Positive = higher voice = worse off.
    /// </summary>
    public static int PitchPercentForStep(int step) => ((step * PitchStepPercent) - PitchSpanPercent) * -1;

    /// <summary>
    /// Starts loading the clips on a background thread. 122 MP3 decodes must not
    /// run on the game's frame thread.
    /// </summary>
    public void BeginLoad()
    {
        if (_load != null) return;
        _load = Task.Run(Load);
    }

    /// <summary>
    /// The clip for a party position (1-based) at a health step (0 = empty ..
    /// 14 = full), or null while the bank is still loading.
    /// </summary>
    public float[]? Number(int position, int step)
    {
        var n = _numbers;
        if (!_ready || n == null) return null;
        if (position < 1 || position > MaxPosition) return null;
        return n[position - 1][Math.Clamp(step, 0, PitchLevels - 1)];
    }

    /// <summary>The "dead" call, at natural pitch. Null while loading.</summary>
    public float[]? Dead => _ready ? _dead : null;

    /// <summary>The "full" call, at natural pitch. Null while loading.</summary>
    public float[]? Full => _ready ? _full : null;

    private void Load()
    {
        // try-catch: file IO and the Media Foundation decoder are external. A bank
        // that cannot be loaded must leave the rest of the plugin untouched; the
        // monitor checks HasFailed and stays silent rather than retrying.
        try
        {
            if (!Directory.Exists(_assetDir))
                throw new DirectoryNotFoundException($"Klangdateien nicht gefunden: {_assetDir}");

            var numbers = new float[MaxPosition][][];
            for (var pos = 1; pos <= MaxPosition; pos++)
            {
                var levels = new float[PitchLevels][];
                for (var step = 0; step < PitchLevels; step++)
                    levels[step] = ReadMono(Path.Combine(_assetDir, $"{pos}_{PitchPercentForStep(step)}.mp3"));

                numbers[pos - 1] = levels;
            }

            // Sku plays the dead/full markers at natural pitch - they say WHAT
            // happened, not how much is left, so a pitch would carry no meaning.
            _dead    = ReadMono(Path.Combine(_assetDir, "dead.mp3"));
            _full    = ReadMono(Path.Combine(_assetDir, "full.mp3"));
            _numbers = numbers;
            _ready   = true;

            _logInfo($"[PartyMonitor] Klangbank geladen: {MaxPosition} Nummern x {PitchLevels} Tonhoehen, " +
                     $"{numbers[0][14].Length * 1000 / Rate} ms je Wort.");
        }
        catch (Exception ex)
        {
            _failed = true;
            _logError("[PartyMonitor] Klangbank konnte nicht geladen werden - Heilmonitor bleibt stumm.", ex);
        }
    }

    /// <summary>Decodes one clip to mono float samples at <see cref="Rate"/>.</summary>
    private static float[] ReadMono(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Klangdatei fehlt: {path}", path);

        using var reader = new MediaFoundationReader(path);

        ISampleProvider provider = reader.ToSampleProvider();
        if (provider.WaveFormat.Channels > 1)
            provider = new StereoToMonoSampleProvider(provider);
        if (provider.WaveFormat.SampleRate != Rate)
            provider = new WdlResamplingSampleProvider(provider, Rate);

        var chunk = new float[Rate];
        var all = new List<float>(Rate / 2);
        int read;
        while ((read = provider.Read(chunk, 0, chunk.Length)) > 0)
            for (var i = 0; i < read; i++) all.Add(chunk[i]);

        return Trim(all.ToArray());
    }

    /// <summary>
    /// Strips leading and trailing near-silence, so one call follows the next
    /// without dead air. The threshold is well below Sku's peak level of ~0.76,
    /// so no consonant is clipped off.
    /// </summary>
    private static float[] Trim(float[] x)
    {
        const float Threshold = 0.004f;

        var start = 0;
        while (start < x.Length && MathF.Abs(x[start]) < Threshold) start++;

        var end = x.Length - 1;
        while (end > start && MathF.Abs(x[end]) < Threshold) end--;

        if (start >= end) return x;

        var result = new float[end - start + 1];
        Array.Copy(x, start, result, 0, result.Length);
        return result;
    }
}
