namespace FF14Accessibility.Services;

/// <summary>
/// Decides how an armoury class row may be spoken. The English
/// ClassJobCategory name is either a phrase ("All Classes") or a list of
/// job abbreviations ("GLA MRD PLD"). German abbreviations are a different
/// code (German SCH is rogue, English SCH is scholar), so a list is never
/// read aloud and never translated token by token.
/// </summary>
internal static class ArmouryClassSpeech
{
    internal static bool IsAbbreviationList(string englishName)
    {
        var tokens = englishName.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;
        foreach (var token in tokens)
        {
            if (token.Length is < 2 or > 4) return false;
            foreach (var character in token)
                if (character is < 'A' or > 'Z') return false;
        }
        return true;
    }
}
