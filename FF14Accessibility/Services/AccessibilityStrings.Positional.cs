namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    // ── Position relative to the target (front / flank / rear) ────────────────
    // Short on purpose: the positional voice speaks while the player is moving
    // around the enemy, and a SAPI sentence takes far longer than the screen
    // reader (docs/game-api.md, "SAPI timing").

    /// <summary>Spoken word for one positional zone. Left and right are the
    /// ENEMY's sides, the way a flank is named.</summary>
    public static string PositionalZoneName(PositionalZone zone) => zone switch
    {
        PositionalZone.Front      => IsGerman ? "Vorne"         : "Front",
        PositionalZone.FlankLeft  => IsGerman ? "Flanke links"  : "Left flank",
        PositionalZone.FlankRight => IsGerman ? "Flanke rechts" : "Right flank",
        PositionalZone.Rear       => IsGerman ? "Hinten"        : "Rear",
        PositionalZone.Omni       => IsGerman ? "Richtung egal" : "No positionals",
        _                         => IsGerman ? "Unbekannt"     : "Unknown",
    };

    /// <summary>On-demand key pressed without an enemy targeted.</summary>
    public static string PositionalNoEnemy => IsGerman
        ? "Kein Gegner im Ziel."
        : "No enemy targeted.";

    public static string OptPositionalMode => IsGerman
        ? "Lage zum Gegner"
        : "Position to enemy";

    public static string PositionalModeName(PositionalOutput mode) => mode switch
    {
        PositionalOutput.Voice => IsGerman ? "Stimme" : "Voice",
        PositionalOutput.Tone  => IsGerman ? "Ton"    : "Tone",
        _                      => IsGerman ? "Aus"    : "Off",
    };

    /// <summary>Confirmation after choosing the output mode.</summary>
    public static string PositionalModeSet(string value) =>
        IsGerman ? $"Lage zum Gegner: {value}." : $"Position to enemy: {value}.";

    public static string OptPositionalVoiceVolume => IsGerman
        ? "Lautstärke Lage-Stimme"
        : "Position voice volume";

    public static string OptPositionalVoiceRate => IsGerman
        ? "Tempo Lage-Stimme"
        : "Position voice speed";

    public static string OptPositionalVoiceNameDe => IsGerman
        ? "Lage-Stimme Deutsch"
        : "Position voice German";

    public static string OptPositionalVoiceNameEn => IsGerman
        ? "Lage-Stimme Englisch"
        : "Position voice English";

    public static string OptPositionalToneVolume => IsGerman
        ? "Lautstärke Lage-Töne"
        : "Position tone volume";

    public static string OptPositionalTonePreview => IsGerman
        ? "Lage-Töne anhören"
        : "Listen to position tones";

    /// <summary>Preview sentence of the German positional voice. Fixed German,
    /// not localized: it is the sample of the voice that speaks when the plugin
    /// language is German.</summary>
    public const string PositionalVoiceSampleDe = "Flanke links. Hinten.";

    /// <summary>Preview sentence of the English positional voice.</summary>
    public const string PositionalVoiceSampleEn = "Left flank. Rear.";

    /// <summary>Stands in place of the voice list when the system offers no
    /// speech at all.</summary>
    public static string PositionalVoiceUnavailable => IsGerman
        ? "Keine Sprachausgabe des Systems verfügbar. Die Lage kommt über den Screenreader."
        : "No system speech available. Your position goes through the screen reader.";

    /// <summary>Confirmation after choosing a voice - only needed when the
    /// preview stayed silent.</summary>
    public static string PositionalVoiceSet(string value) =>
        IsGerman ? $"Lage-Stimme {value}." : $"Position voice {value}.";
}
