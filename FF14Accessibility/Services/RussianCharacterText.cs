using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace FF14Accessibility.Services;

/// <summary>Exact translations of authored descriptions, never a general speech filter.</summary>
internal static class RussianCharacterText
{
    private static readonly Lazy<Dictionary<string, string>> Text = new(Load);

    internal static string? Find(string english)
        => Text.Value.GetValueOrDefault(english);

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(RussianCharacterText).Assembly.GetManifestResourceStream(
            "FF14Accessibility.Resources.RussianCharacterText.json.gz");
        if (stream == null) return new(StringComparer.Ordinal);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(gzip)
               ?? new(StringComparer.Ordinal);
    }
}
