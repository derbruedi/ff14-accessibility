using System;
using System.Globalization;

namespace FF14Accessibility;

/// <summary>Language for all screen-reader output of the mod.</summary>
public enum LanguageMode
{
    /// <summary>Follow the Windows UI culture (German Windows -> German, else English).</summary>
    Auto = 0,
    German = 1,
    English = 2,
}

/// <summary>
/// Central language state for every screen-reader announcement the mod makes.
/// Set once at startup from the config and updated by "/acc lang". "Auto"
/// follows the Windows UI culture so users get their OS language with no setup.
///
/// All user-facing strings resolve through <see cref="Services.AccessibilityStrings"/>,
/// which reads <see cref="IsGerman"/> here. Game-provided content (item/NPC
/// names, quest text) is NOT routed through this - it already comes from the
/// game in the player's game language and is spoken verbatim.
/// </summary>
public static class Loc
{
    /// <summary>The active language selection (mirrors Configuration.Language).</summary>
    public static LanguageMode Mode { get; set; } = LanguageMode.Auto;

    /// <summary>True when announcements should be German, resolving Auto against the OS culture.</summary>
    public static bool IsGerman => Mode switch
    {
        LanguageMode.German  => true,
        LanguageMode.English => false,
        _ => string.Equals(
                 CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                 "de",
                 StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>Parses a "/acc lang" argument ("de"/"en"/"auto") to a mode, or null if unknown.</summary>
    public static LanguageMode? ParseArg(string arg) => arg.Trim().ToLowerInvariant() switch
    {
        "de" or "deutsch" or "german" => LanguageMode.German,
        "en" or "english" or "englisch" => LanguageMode.English,
        "auto" => LanguageMode.Auto,
        _ => null,
    };
}
