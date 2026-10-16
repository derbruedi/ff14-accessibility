using System;
using System.Globalization;

namespace FF14Accessibility;

/// <summary>Language for all screen-reader output of the mod.</summary>
public enum LanguageMode
{
    /// <summary>Follow the Windows UI culture (German Windows -> German, Russian Windows -> Russian, else English).</summary>
    Auto = 0,
    German = 1,
    English = 2,

    /// <summary>Russian. Strings without a Russian text stay English.</summary>
    Russian = 3,
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

    /// <summary>Saved separately from the language of plugin announcements.</summary>
    public static bool TranslateItemsAndActions { get; set; } = true;

    /// <summary>Current translation state for item/ability speech and its caches.</summary>
    public static bool IsRussianItemActionText => IsRussian && TranslateItemsAndActions;

    /// <summary>Two-letter code of the Windows UI language, lower case.</summary>
    private static string OsLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

    /// <summary>True when announcements should be German, resolving Auto against the OS culture.</summary>
    public static bool IsGerman => Mode switch
    {
        LanguageMode.German  => true,
        LanguageMode.English or LanguageMode.Russian => false,
        _ => OsLanguage == "de",
    };

    /// <summary>True when announcements should be Russian. Auto picks Russian on
    /// a Russian Windows, exactly as it picks German on a German one; an
    /// explicit German or English choice keeps Russian off.</summary>
    public static bool IsRussian => Mode switch
    {
        LanguageMode.Russian => true,
        LanguageMode.German or LanguageMode.English => false,
        _ => OsLanguage == "ru",
    };

    /// <summary>Parses a "/acc lang" argument ("de"/"en"/"ru"/"auto") to a mode, or null if unknown.</summary>
    public static LanguageMode? ParseArg(string arg) => arg.Trim().ToLowerInvariant() switch
    {
        "de" or "deutsch" or "german" => LanguageMode.German,
        "en" or "english" or "englisch" => LanguageMode.English,
        "ru" or "russian" or "russisch" or "рус" or "русский" => LanguageMode.Russian,
        "auto" => LanguageMode.Auto,
        _ => null,
    };
}
