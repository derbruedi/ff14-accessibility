using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Data.Structs.Excel;
using Lumina.Excel;
using Lumina.Text.ReadOnly;
using NativeSheet = FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelSheet;

namespace FF14Accessibility.Services;

/// <summary>Read Harmonia's real game rows, including compatibility exclusions.
/// Bind by source bytes and column definitions, never guessed field offsets.
/// Game pointers and translated strings are not cached.</summary>
internal sealed unsafe class NativeGameDisplayText(IDataManager data, Func<bool> available, IPluginLog? log = null)
{
    private readonly Dictionary<(string Sheet, uint Id, ReadOnlyMemory<byte> Source), int[]> _columns = new();
    private readonly HashSet<string> _reported = [];
    internal ReadOnlySeString? Read(string name, uint id, ReadOnlyMemory<byte> sourceMemory)
    {
        try
        {
            var text = ReadCore(name, id, sourceMemory);
            if (text != null && _reported.Add("read:" + name))
                log?.Debug($"[GameText] Native {name}/{id} precedes the speech catalog.");
            return text;
        }
        catch (Exception ex)
        {
            if (_reported.Add("error:" + name)) log?.Warning($"[GameText] Native {name} unavailable: {ex.Message}; using fallback.");
            return null;
        }
    }

    private ReadOnlySeString? ReadCore(string name, uint id, ReadOnlyMemory<byte> sourceMemory)
    {
        var source = sourceMemory.Span;
        if (!available() || data.Language != ClientLanguage.English || source.IsEmpty) return null;
        var sheet = data.GetExcelSheet<RawRow>(ClientLanguage.English, name);
        if (!sheet.TryGetRow(id, out var original)) return null;
        var key = (name, id, sourceMemory);
        if (!_columns.TryGetValue(key, out var columns))
        {
            columns = MatchingColumns(original, sourceMemory);
            if (_columns.Count >= 8192) _columns.Clear();
            _columns[key] = columns;
        }
        if (columns.Length == 0) return null;
        var textModule = RaptureTextModule.Instance();
        if (textModule == null || textModule->ExcelModuleInterface == null) return null;
        var module = textModule->ExcelModuleInterface->ExdModule;
        if (module == null) return null;
        var nativeSheet = module->GetSheetByName(name);
        if (!LayoutMatches(nativeSheet, original.Columns)) return null;
        var row = module->GetRowBySheetAndRowId(nativeSheet, id);
        if (row == null || row->Data == null || row->Sheet != nativeSheet) return null;
        var values = new List<ReadOnlySeString>();
        foreach (var column in columns)
        {
            var pointer = row->GetColumnString((uint)column);
            if (pointer.Value == null) return null;
            var bytes = pointer.AsSpan();
            if (bytes.Length > 65535) return null;
            values.Add(new ReadOnlySeString(bytes.ToArray()));
        }
        return Consensus(source, values);
    }

    internal static int[] MatchingColumns(RawRow row, ReadOnlyMemory<byte> source)
    {
        var found = new List<int>();
        var exact = new List<int>();
        for (var i = 0; i < row.Columns.Count; i++)
            if (row.Columns[i].Type == ExcelColumnDataType.String
                && row.ReadStringColumn(i).Data.Span.SequenceEqual(source.Span))
            {
                found.Add(i);
                // Typed and raw Lumina rows share their ExcelPage. Distinguish
                // equal text at different offsets using the original memory.
                if (row.ReadStringColumn(i).Data.Equals(source)) exact.Add(i);
            }
        return (exact.Count > 0 ? exact : found).ToArray();
    }

    internal static bool LayoutMatches(NativeSheet* sheet, IReadOnlyList<ExcelColumnDefinition> columns)
    {
        if (sheet == null || sheet->ColumnDefinitions == null || sheet->Version <= 2
            || sheet->Variant != FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelVariant.SingleRow
            || sheet->Language != FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelLanguage.English
            || sheet->ColumnCount != columns.Count) return false;
        for (var i = 0; i < columns.Count; i++)
            if ((ushort)sheet->ColumnDefinitions[i].ColumnType != (ushort)columns[i].Type
                || sheet->ColumnDefinitions[i].Offset != columns[i].Offset) return false;
        return true;
    }

    internal static ReadOnlySeString? Consensus(ReadOnlySpan<byte> source, IEnumerable<ReadOnlySeString> values)
    {
        ReadOnlySeString? found = null;
        foreach (var value in values)
        {
            if (value.IsEmpty || (found is { } previous && !previous.Data.Span.SequenceEqual(value.Data.Span))) return null;
            found = value;
        }
        return found is { } result && !result.Data.Span.SequenceEqual(source) ? result : (ReadOnlySeString?)null;
    }
}
