using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Text.ReadOnly;
using Quest = Lumina.Excel.Sheets.Quest;

namespace FF14Accessibility.Services;

/// <summary>Read-only bridge to installed Harmonia/Prima HPK v1 packs. The format
/// and source guard are documented by Aeria/Harmonia. No translated text is
/// distributed with this plugin, and no Harmonia setting or file is changed.</summary>
internal static class HarmoniaQuestNames
{
    internal readonly record struct Entry(uint RowId, string Name);

    internal static IReadOnlyList<Entry> LoadInstalled(string? directory, IDataManager data, IPluginLog log)
    {
        var result = new List<Entry>();
        if (directory == null || !Directory.Exists(directory)) return result;
        try
        {
            var quests = data.GetExcelSheet<Quest>(ClientLanguage.English);
            // installed.json names the current pack; old HPKs in the same folder
            // must not resurrect translations that Harmonia has replaced.
            foreach (var marker in Directory.EnumerateFiles(directory, "installed.json", SearchOption.AllDirectories).Take(32))
            {
                try
                {
                    if (new FileInfo(marker).Length > 1024 * 1024) throw new InvalidDataException("Oversized installed metadata.");
                    using var json = JsonDocument.Parse(File.ReadAllBytes(marker));
                    var hash = json.RootElement.GetProperty("packHash").GetString();
                    if (hash == null || !hash.StartsWith("sha256:", StringComparison.Ordinal)
                        || hash.Length != 71 || hash[7..].Any(c => !Uri.IsHexDigit(c)))
                        throw new InvalidDataException("Invalid installed pack hash.");
                    var path = Path.Combine(Path.GetDirectoryName(marker)!, hash[7..].ToLowerInvariant() + ".hpk");
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var names = Read(stream, hash[7..], id => quests.TryGetRow(id, out var row) ? row.Name.Data.ToArray() : null);
                    result.AddRange(names);
                    log.Info($"[Quest] Harmonia: {names.Count} source-verified quest names from installed pack.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException)
                { log.Warning($"[Quest] Harmonia names unavailable: {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { log.Warning($"[Quest] Harmonia pack directory unavailable: {ex.Message}"); }
        return result;
    }

    // Bounded seeks read only the Quest name column, rather than loading the
    // entire 125 MB pack into memory. Verify its digest before trusting offsets.
    internal static IReadOnlyList<Entry> Read(Stream stream, string expectedHash, Func<uint, byte[]?> source)
    {
        byte[] Bytes(long offset, int count)
        {
            if (offset < 0 || count < 0 || offset > stream.Length - count) throw new InvalidDataException("HPK range outside file.");
            stream.Position = offset;
            var bytes = new byte[count]; stream.ReadExactly(bytes); return bytes;
        }
        static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at, 4));
        static ushort U16(byte[] b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at, 2));
        static ulong U64(byte[] b, int at) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(at, 8));
        if (!stream.CanSeek || stream.Length is < 64 or > 1L << 30) throw new InvalidDataException("Invalid HPK length.");
        var header = Bytes(0, 64);
        if (!header.AsSpan(0, 8).SequenceEqual("AERIAHPK"u8) || U16(header, 8) != 1 || U32(header, 12) != 64
            || header.AsSpan(28).ContainsAnyExcept((byte)0)) throw new InvalidDataException("Unsupported HPK header.");
        var length = checked((long)U64(header, 16));
        var count = U32(header, 24);
        if (count is < 7 or > 64 || length < 64 + count * 24 + 32 || length > stream.Length)
            throw new InvalidDataException("Invalid HPK body.");
        var storedDigest = Bytes(length - 32, 32);
        if (!Convert.ToHexString(storedDigest).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Installed HPK digest differs from metadata.");
        using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            stream.Position = 0; var buffer = new byte[64 * 1024]; var remaining = length - 32;
            while (remaining > 0) { var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (read == 0) throw new EndOfStreamException(); digest.AppendData(buffer.AsSpan(0, read)); remaining -= read; }
            if (!digest.GetHashAndReset().AsSpan().SequenceEqual(storedDigest)) throw new InvalidDataException("Corrupt HPK content.");
        }
        var sections = new Dictionary<uint, (long Offset, long Length)>();
        long previousEnd = 64 + count * 24;
        for (var i = 0; i < count; i++)
        {
            var entry = Bytes(64 + i * 24, 24); var kind = U32(entry, 0);
            var start = checked((long)U64(entry, 8)); var size = checked((long)U64(entry, 16));
            if (U32(entry, 4) != 0 || start % 8 != 0 || start < previousEnd || size < 0 || start > length - 32 - size
                || !sections.TryAdd(kind, (start, size))) throw new InvalidDataException("Invalid HPK sections.");
            previousEnd = start + size;
        }
        for (uint kind = 1; kind <= 7; kind++) if (!sections.ContainsKey(kind)) throw new InvalidDataException("Missing HPK section.");
        var manifest = sections[1];
        if (manifest.Length > 1024 * 1024) throw new InvalidDataException("Oversized HPK manifest.");
        using var metadata = JsonDocument.Parse(Bytes(manifest.Offset, (int)manifest.Length));
        if (metadata.RootElement.GetProperty("language").GetString() != "ru"
            || metadata.RootElement.GetProperty("game").GetProperty("language").GetString() != "en") return [];
        byte[] Record(uint section, long index, int size)
        {
            var range = sections[section];
            if (range.Length % size != 0 || index < 0 || index >= range.Length / size) throw new InvalidDataException("Invalid HPK record.");
            return Bytes(range.Offset + index * size, size);
        }
        var names = sections[2]; var sheets = sections[3];
        if (sheets.Length % 32 != 0 || sheets.Length / 32 > 65536) throw new InvalidDataException("Invalid HPK sheets.");
        var decoder = new UTF8Encoding(false, true);
        for (long i = 0; i < sheets.Length / 32; i++)
        {
            var sheet = Record(3, i, 32); var nameOffset = U32(sheet, 0); var nameLength = U32(sheet, 4);
            if (nameLength is 0 or > 256 || nameOffset > names.Length - nameLength) throw new InvalidDataException("Invalid HPK sheet name.");
            if (decoder.GetString(Bytes(names.Offset + nameOffset, (int)nameLength)) != "Quest") continue;
            if (sheet[8] != 0 || U32(sheet, 16) == 0 || U32(sheet, 24) > 65536) throw new InvalidDataException("Invalid Quest layout.");
            // Ordinal 0 must actually name string column 0 at offset 0.
            var column = Record(4, U32(sheet, 12), 8);
            if (U32(column, 0) != 0 || U32(column, 4) != 0) throw new InvalidDataException("Quest name column changed.");
            var result = new List<Entry>(); uint previousId = 0;
            for (long r = 0; r < U32(sheet, 24); r++)
            {
                var row = Record(5, U32(sheet, 20) + r, 16); var id = U32(row, 0);
                if (id <= previousId || U16(row, 4) != 0 || U16(row, 6) == 0 || U32(row, 12) != 0)
                    throw new InvalidDataException("Invalid Quest row.");
                previousId = id;
                var cell = Record(6, U32(row, 8), 24);
                if (U16(cell, 0) != 0) continue; // pack may translate only the quest's other fields
                var textLength = U32(cell, 4); var textOffset = U32(cell, 8); var strings = sections[7];
                if (textLength is 0 or > 65535 || textOffset > strings.Length - textLength - 1
                    || U16(cell, 2) != 0 || U32(cell, 12) != 0) throw new InvalidDataException("Invalid Quest name.");
                var original = source(id);
                if (original == null || Guard(original) != U64(cell, 16)) continue;
                var bytes = Bytes(strings.Offset + textOffset, (int)textLength + 1);
                if (bytes[^1] != 0) throw new InvalidDataException("Unterminated Quest name.");
                var text = new ReadOnlySeString(bytes.AsMemory(0, (int)textLength)).ExtractText().Trim();
                if (text.Length > 0) result.Add(new(id, text));
            }
            return result;
        }
        return [];
    }

    internal static ulong Guard(ReadOnlySpan<byte> original)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        digest.AppendData("HARMONIA-HXS-V1-RAW-STRING"u8);
        Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)original.Length);
        digest.AppendData(length); digest.AppendData(original);
        return BinaryPrimitives.ReadUInt64LittleEndian(digest.GetHashAndReset());
    }
}
