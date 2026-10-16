using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FF14Accessibility.Services;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

// Verification input only. Translations are read from the user's selected HPK,
// never shipped with the plugin. Runtime speech reads actual native game rows.
internal sealed class PackFixture
{
    internal sealed record Cell(string Sheet, uint Id, int Column, ReadOnlyMemory<byte> Original, ReadOnlySeString Display);
    internal readonly List<Cell> Cells = [];
    private readonly Dictionary<(string Sheet, uint Id, int Column), ReadOnlySeString> _shown = new();
    private readonly GameDataReader _data;
    internal int ChangedSources { get; private set; }
    internal int IncompatibleSheets { get; private set; }

    internal PackFixture(string path, GameDataReader data)
    {
        _data = data;
        var bytes = File.ReadAllBytes(path);
        uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        ushort U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at, 2));
        ulong U64(int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at, 8));
        if (!bytes.AsSpan(0, 8).SequenceEqual("AERIAHPK"u8) || U16(8) != 1 || U32(12) != 64) throw new InvalidDataException("Unsupported HPK");
        var length = checked((int)U64(16));
        if (length > bytes.Length || !SHA256.HashData(bytes.AsSpan(0, length - 32)).AsSpan().SequenceEqual(bytes.AsSpan(length - 32, 32)))
            throw new InvalidDataException("Corrupt HPK");
        var sections = new Dictionary<uint, (int At, int Length)>();
        for (var i = 0; i < U32(24); i++)
        {
            var entry = 64 + i * 24;
            sections.Add(U32(entry), (checked((int)U64(entry + 8)), checked((int)U64(entry + 16))));
        }
        int Record(uint kind, uint index, int size)
        {
            var section = sections[kind];
            if (section.Length % size != 0 || index >= section.Length / size) throw new InvalidDataException("Record range");
            return checked(section.At + (int)index * size);
        }
        using var manifest = JsonDocument.Parse(bytes.AsMemory(sections[1].At, sections[1].Length));
        if (manifest.RootElement.GetProperty("language").GetString() != "ru"
            || manifest.RootElement.GetProperty("game").GetProperty("language").GetString() != "en")
            throw new InvalidDataException("Not ru/en");
        for (uint si = 0; si < sections[3].Length / 32; si++)
        {
            var s = Record(3, si, 32);
            var name = Encoding.UTF8.GetString(bytes, checked(sections[2].At + (int)U32(s)), checked((int)U32(s + 4)));
            if (bytes[s + 8] != 0) continue;
            ExcelSheet<RawRow> raw;
            try { raw = data.GetExcelSheet<RawRow>(name: name); }
            catch { IncompatibleSheets++; continue; }
            var stringColumns = Enumerable.Range(0, raw.Columns.Count)
                .Where(i => raw.Columns[i].Type == Lumina.Data.Structs.Excel.ExcelColumnDataType.String).ToArray();
            var layout = new List<int>();
            for (uint ci = 0; ci < U32(s + 16); ci++)
            {
                var c = Record(4, U32(s + 12) + ci, 8);
                var index = checked((int)U32(c));
                if (index >= raw.Columns.Count || raw.Columns[index].Offset != U32(c + 4)) throw new InvalidDataException("Changed layout: " + name);
                layout.Add(index);
            }
            if (!stringColumns.SequenceEqual(layout)) { IncompatibleSheets++; continue; }
            for (uint ri = 0; ri < U32(s + 24); ri++)
            {
                var r = Record(5, U32(s + 20) + ri, 16); var id = U32(r);
                if (!raw.TryGetRow(id, out var row)) continue;
                for (uint ci = 0; ci < U16(r + 6); ci++)
                {
                    var c = Record(6, U32(r + 8) + ci, 24); var ordinal = U16(c);
                    if (ordinal >= layout.Count) throw new InvalidDataException("Column range");
                    var column = layout[ordinal]; var source = row.ReadStringColumn(column).Data;
                    if (HarmoniaQuestNames.Guard(source.Span) != U64(c + 16)) { ChangedSources++; continue; }
                    var translated = new ReadOnlySeString(bytes.AsMemory(checked(sections[7].At + (int)U32(c + 8)), checked((int)U32(c + 4))));
                    Cells.Add(new(name, id, column, source, translated));
                    _shown.Add((name, id, column), translated);
                }
            }
        }
    }

    internal ReadOnlySeString? Read(string sheet, uint id, ReadOnlyMemory<byte> source)
    {
        var raw = _data.GetExcelSheet<RawRow>(name: sheet);
        if (!raw.TryGetRow(id, out var row)) return null;
        var columns = NativeGameDisplayText.MatchingColumns(row, source);
        var values = columns.Select(column => _shown.GetValueOrDefault((sheet, id, column), row.ReadStringColumn(column)));
        return NativeGameDisplayText.Consensus(source.Span, values);
    }
}
