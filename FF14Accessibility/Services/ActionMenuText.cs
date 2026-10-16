using System.Text.RegularExpressions;

namespace FF14Accessibility.Services;

internal static class ActionMenuText
{
    internal sealed record Entry(string Name, string Description);
    // The recorded Russian UI includes untranslated names followed by "Ур. 35".
    // Strip only an explicit level suffix, never Roman numerals in skill names.
    internal static (string Name, int Level) ParseLabel(string label)
    {
        var match = Regex.Match(label.Trim(), @"^(.*?)\s*(?:[,;]\s*)?(?:Ур\.?|Уровень|Lv\.?|Level|St\.?|Stufe)\s*(\d{1,3})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[2].Value, out var level)
            ? (match.Groups[1].Value.Trim(), level) : (label.Trim(), 0);
    }

    internal static bool Matches(string panel, string slot)
    {
        var a = ParseLabel(panel); var b = ParseLabel(slot);
        return a.Name.Length > 0 && b.Name.Length > 0
            && a.Name.Equals(b.Name, StringComparison.OrdinalIgnoreCase)
            && (a.Level == 0 || b.Level == 0 || a.Level == b.Level);
    }

    internal static Entry Consensus(IEnumerable<Entry> entries, Entry fallback)
    {
        var distinct = entries.Distinct().Take(2).ToArray();
        return distinct.Length == 1 && distinct[0].Name.Length > 0 ? distinct[0] : fallback;
    }
}
