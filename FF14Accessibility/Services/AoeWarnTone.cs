using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Die waehlbaren Klaenge der AoE-Gefahrenwarnung.
///
/// WARUM ZUR AUSWAHL und nicht ein fest gesetzter (Spielerwunsch 2026-08-21,
/// "der ist nervig"): wie ein Ton wirkt, der minutenlang durchhaelt, kann nur der
/// hoeren, der ihn hoert. Statt hier einen zu raten, stehen vier zur Wahl, und
/// die Menuezeile spielt jeden beim Anwaehlen kurz vor - entschieden wird am Ohr,
/// nicht am Namen.
///
/// GEMEINSAM IST ALLEN: sie halten durch, solange die Gefahr besteht. Das war die
/// Entscheidung des Spielers vom 2026-07-26 (ein Dauerton, kein Puls), und sie
/// bleibt unangetastet - auch <see cref="Wave"/> reisst nie ab, es schwillt nur
/// an und ab.
/// </summary>
public enum AoeWarnTone
{
    /// <summary>Der bisherige Klang: reiner Sinus auf 660 Hz, unveraendert
    /// durchgehend. Bleibt waehlbar, damit die Umstellung umkehrbar ist.</summary>
    Bright = 0,

    /// <summary>Weicher Dauerton, eine Oktave tiefer und mit etwas Fuelle statt
    /// des blanken Sinus.</summary>
    Soft = 1,

    /// <summary>Tiefes Brummen. Liegt unter der gesamten Peil-Ton-Leiter und
    /// verdeckt die Sprachausgabe am wenigsten.</summary>
    Deep = 2,

    /// <summary>Ton, der in ruhigem Takt an- und abschwillt, ohne je abzureissen.
    /// Weniger ermuedend als ein starrer Pegel - und trotzdem nicht mit den
    /// Schlaegen des Peil-Tons zu verwechseln, weil er nie Stille dazwischen
    /// hat.</summary>
    Wave = 3,
}

/// <summary>Die Klangwerte einer Warnton-Stimme.</summary>
/// <param name="Frequency">Grundfrequenz in Hertz.</param>
/// <param name="Brightness">0..1 fuer die Obertoene von
/// <see cref="ToneSynth.Timbre"/>; 0 ergibt den blanken Sinus.</param>
/// <param name="TremoloHz">Wie oft der Pegel je Sekunde schwingt; 0 = starr.</param>
/// <param name="TremoloDepth">Wie tief er dabei faellt (0..1). 0,5 heisst: bis auf
/// die Haelfte herunter, nie bis zur Stille.</param>
internal readonly record struct AoeToneVoice(
    float Frequency, float Brightness, float TremoloHz, float TremoloDepth);

/// <summary>Klangwerte und Reihenfolge der Warntoene.</summary>
public static class AoeWarnTones
{
    /// <summary>Alle Klaenge in der Reihenfolge, in der das Menue sie anbietet.</summary>
    public static readonly AoeWarnTone[] All =
        (AoeWarnTone[])Enum.GetValues(typeof(AoeWarnTone));

    /// <summary>
    /// Die Klangwerte einer Stimme.
    ///
    /// DIE FREQUENZEN SIND BEWUSST GEWAEHLT, nicht gegriffen: der Peil-Ton belegt
    /// mit 247, 330, 392, 440, 494, 523, 587, 659 und 784 Hz eine ganze
    /// diatonische Leiter (BeaconService.VoiceFor), und seine Toene werden je nach
    /// Winkel noch bis zu eine halbe Oktave nach unten gebeugt. Der bisherige
    /// Warnton auf 660 Hz sitzt genau auf dem Sammelpunkt-Ton (659 Hz) - er ist
    /// nur deshalb auseinanderzuhalten, weil er durchhaelt und jener schlaegt.
    /// Die neuen Stimmen liegen deshalb ZWISCHEN den Stufen der Leiter
    /// beziehungsweise unter ihr.
    /// </summary>
    internal static AoeToneVoice VoiceFor(AoeWarnTone tone) => tone switch
    {
        // Unveraendert der alte Klang - blanker Sinus, starrer Pegel.
        AoeWarnTone.Bright => new AoeToneVoice(660f, 0f, 0f, 0f),

        // 300 Hz liegt zwischen 247 und 330 der Peil-Leiter. Etwas Oberton
        // nimmt dem Sinus die Schaerfe, ohne ihn zum Instrument zu machen.
        AoeWarnTone.Soft   => new AoeToneVoice(300f, 0.3f, 0f, 0f),

        // 120 Hz liegt unter der tiefsten Peil-Stimme, auch nach deren
        // Winkelbeugung. Sprachverstaendlichkeit haengt vor allem am Bereich
        // darueber, ein Brummen hier unten deckt die Ansage also am wenigsten zu.
        AoeWarnTone.Deep   => new AoeToneVoice(120f, 0.2f, 0f, 0f),

        // Zweimal je Sekunde bis auf die halbe Lautstaerke herunter und wieder
        // hinauf. NIE bis zur Stille: die Luecke waere genau das, was die
        // Peil-Schlaege ausmacht, und zwei Signale mit Pausen im selben Moment
        // sind nicht mehr zu trennen - waehrend man in einer Flaeche steht,
        // laeuft der Fluchtton ja mit.
        AoeWarnTone.Wave   => new AoeToneVoice(300f, 0.3f, 2f, 0.5f),

        _ => new AoeToneVoice(660f, 0f, 0f, 0f),
    };
}
