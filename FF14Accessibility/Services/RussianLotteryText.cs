using System.Text.Json;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Only called for Cactpot controls; never rewrites chat or player input.</summary>
internal static class RussianLotteryText
{
    private static readonly Lazy<Dictionary<uint, string[]>> Labels = new(() =>
    {
        using var stream = typeof(RussianLotteryText).Assembly.GetManifestResourceStream(
            "FF14Accessibility.Resources.RussianLotteryLabels.json");
        return stream == null ? [] : JsonSerializer.Deserialize<Dictionary<uint, string[]>>(stream) ?? [];
    });
    private static readonly Regex RevealEnglish = new(
        @"^Select (?<n>\d+|one|two|three|four|five|six|seven|eight|nine|ten) slots? to uncover\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RevealGerman = new(
        @"^Decke (?<n>\d+|ein|eins|eine|einen|zwei|drei|vier|fünf|sechs|sieben|acht|neun|zehn) (?:Feld|Felder) auf\.$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, int> Counts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
        ["ein"] = 1, ["eins"] = 1, ["eine"] = 1, ["einen"] = 1, ["zwei"] = 2,
        ["drei"] = 3, ["vier"] = 4, ["fünf"] = 5, ["sechs"] = 6,
        ["sieben"] = 7, ["acht"] = 8, ["neun"] = 9, ["zehn"] = 10,
    };

    internal static string Translate(IDataManager data, string text)
        => Translate(text, id =>
        {
            try { return data.GetExcelSheet<Addon>(ClientLanguage.English).GetRow(id).Text.Data.ToArray(); }
            catch { return []; }
        });

    internal static string Translate(string text, Func<uint, byte[]> source)
    {
        if (!Loc.IsRussian) return text;
        var cleaned = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        foreach (var (id, labels) in Labels.Value)
            if (labels.Any(label => string.Equals(label, cleaned, StringComparison.OrdinalIgnoreCase)))
                return RussianAuthorText.Translate("AddonText", id, source(id), text);

        var match = RevealEnglish.Match(cleaned);
        if (!match.Success) match = RevealGerman.Match(cleaned);
        if (match.Success)
        {
            var value = match.Groups["n"].Value;
            var count = int.TryParse(value, out var number) ? number : Counts.GetValueOrDefault(value);
            if (count is >= 1 and <= 10 && RussianAuthorText.Translate(
                "AddonText", 9262, source(9262), "") != "")
                return AccessibilityStrings.LotteryDailyRevealCount(count);
        }
        return text;
    }
}
