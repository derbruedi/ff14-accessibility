using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Turns a BNpcName sheet row into the name the game actually displays.
///
/// German sheet names leave the adjective ending as a placeholder the game
/// fills in per case ("wuchernd[a] Efeuranke"); read out raw, the brackets end
/// up in the speech - and a name compared raw never matches the one on the
/// object.
///
/// BNpcName.Pronoun is the gender, so the nominative ending follows from it:
/// 0 masculine "-er", 1 feminine "-e", 2 neuter "-es". Two of the three are
/// confirmed against names the game itself displayed - Pronoun 1
/// "wuchernd[a] Efeuranke" shows as "Wuchernde Efeuranke" (window dump
/// 2026-08-17) and Pronoun 0 "rostig[a] Kobalos" as "Rostiger Kobalos" (log
/// 2026-07-19); the neuter form is plain German strong declension.
///
/// The other two placeholders in the sheet, [p] (1195 names) and [t] (201), are
/// dropped without replacement. UNVERIFIED - no displayed name with one of them
/// has been seen yet. Dropping can only cost an ending, never invent a wrong
/// name, which is why it is the safe fallback.
///
/// Only German gets endings: the placeholders are a property of the client
/// language, and guessing suffixes for a language whose rules have not been
/// measured would be worse than the bare stem.
///
/// Lives in its own class because two features read the same sheet: the hunting
/// log (open monsters of the current rank) and the levequest category (the
/// enemies a running battle leve asks for).
/// </summary>
public static class MonsterNameText
{
    /// <summary>The displayed name of a monster, placeholders resolved.</summary>
    /// <param name="nameRow">Row from the BNpcName sheet.</param>
    /// <param name="language">Client language - only German fills endings in.</param>
    public static string Resolve(BNpcName nameRow, Dalamud.Game.ClientLanguage language)
    {
        var text = nameRow.Singular.ExtractText().Trim();
        if (!text.Contains('[')) return text;

        var ending = language == Dalamud.Game.ClientLanguage.German
            ? nameRow.Pronoun switch { 0 => "er", 1 => "e", 2 => "es", _ => string.Empty }
            : string.Empty;

        return text.Replace("[a]", ending)
                   .Replace("[p]", string.Empty)
                   .Replace("[t]", string.Empty)
                   .Trim();
    }
}
