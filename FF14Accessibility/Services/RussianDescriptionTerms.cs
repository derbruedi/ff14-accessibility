using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FF14Accessibility.Services;

/// <summary>Names inside accepted Russian descriptions, after game macros were evaluated.</summary>
internal static class RussianDescriptionTerms
{
    private static readonly Lazy<Dictionary<char, KeyValuePair<string, string>[]>> Terms = new(Load);

    internal static string Translate(string text, bool russian)
    {
        if (!russian || text.Length == 0) return text;
        var result = new StringBuilder(text.Length);
        for (var position = 0; position < text.Length;)
        {
            var matched = false;
            if ((position == 0 || !IsWord(text[position - 1])) && Terms.Value.TryGetValue(text[position], out var terms))
            {
                foreach (var pair in terms)
                {
                    var end = position + pair.Key.Length;
                    if (end > text.Length || (end < text.Length && IsWord(text[end]))) continue;
                    if (!text.AsSpan(position, pair.Key.Length).SequenceEqual(pair.Key.AsSpan())) continue;
                    result.Append(pair.Value);
                    position = end;
                    matched = true;
                    break;
                }
            }
            if (!matched) result.Append(text[position++]);
        }
        return result.ToString();
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static Dictionary<char, KeyValuePair<string, string>[]> Load()
    {
        using var stream = typeof(RussianDescriptionTerms).Assembly.GetManifestResourceStream(
            "FF14Accessibility.Resources.RussianDescriptionTerms.json.gz");
        if (stream == null) return new();
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        var terms = JsonSerializer.Deserialize<Dictionary<string, string>>(gzip) ?? new();
        return terms.Where(x => x.Key.Length >= 3).GroupBy(x => x.Key[0])
            .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Key.Length).ToArray());
    }
}
