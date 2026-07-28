using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Shared synthesis helpers for the mod's generated audio cues. Produces a warm
/// "crystalline" bell/pluck timbre - a fundamental plus two softly decaying upper
/// partials - instead of a bare sine wave. A struck/plucked sound with a natural
/// exponential decay is far less fatiguing across a long session than a flat,
/// repeating beep, and the bell-like ring fits the game's aether/crystal theme.
///
/// All routines are pure and allocation-free so they can run on the NAudio
/// playback thread. Frequencies/pitch mapping stay with the individual providers;
/// only the tone shape lives here so every cue shares one voice.
/// </summary>
internal static class ToneSynth
{
    // Peak sum of the partial amplitudes (1 + 0.35 + 0.12) - used to normalise the
    // summed timbre back into roughly [-1, 1] before the volume/envelope scaling.
    private const float PartialNorm = 1f / 1.47f;

    /// <summary>
    /// Evaluates the bell/pluck timbre for one fundamental <paramref name="phase"/>
    /// (radians). <paramref name="brightness"/> (0..1) fades the two upper partials
    /// in and out; tie it to the note's envelope so the tone mellows as it rings,
    /// the way a struck bell or plucked string loses its overtones first.
    /// </summary>
    public static float Timbre(double phase, float brightness)
    {
        var f1 = (float)Math.Sin(phase);
        var f2 = (float)Math.Sin(phase * 2.0);
        var f3 = (float)Math.Sin(phase * 3.0);
        return (f1 + 0.35f * brightness * f2 + 0.12f * brightness * brightness * f3) * PartialNorm;
    }

    /// <summary>
    /// Plucked-note envelope: a short linear attack (so the onset is click-free),
    /// then an exponential decay that rings out like a struck instrument. All
    /// timing arguments are in samples; <paramref name="decayTauSamples"/> is the
    /// decay time constant (the level reaches ~37% after one tau, ~5% after three).
    /// Returns 0 before the note starts.
    /// </summary>
    public static float PluckEnvelope(int pos, int attackSamples, float decayTauSamples)
    {
        if (pos < 0) return 0f;
        if (pos < attackSamples) return pos / (float)attackSamples;
        return (float)Math.Exp(-(pos - attackSamples) / decayTauSamples);
    }
}
