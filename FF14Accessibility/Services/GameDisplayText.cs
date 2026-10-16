using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace FF14Accessibility.Services;

/// <summary>Prefer the actual game text over speech-only catalogs. Translation
/// settings, packs, row identities and action types stay owned by the game.</summary>
internal static class GameDisplayText
{
    internal delegate ReadOnlySeString? Reader(string sheet, uint rowId, ReadOnlyMemory<byte> source);
    private static Reader? _reader;
    private static IDataManager? _data;
    private static Func<bool>? _available;
    private static bool _wasAvailable;
    private static int _revision;
    internal static bool IsAvailable => _reader != null && (_available?.Invoke() ?? true);
    internal static int Revision
    {
        get
        {
            var active = IsAvailable;
            if (active != _wasAvailable) { _wasAvailable = active; _revision++; }
            return _revision;
        }
    }

    internal static void Configure(IDataManager? data, Reader? reader, Func<bool>? available = null)
    {
        _data = data;
        _reader = reader;
        _available = available;
        _wasAvailable = IsAvailable;
        _revision++;
    }

    internal static ReadOnlySeString? Find(string sheet, uint id, ReadOnlySpan<byte> source)
        => Find(sheet, id, source.ToArray().AsMemory());

    internal static ReadOnlySeString? Find(string sheet, uint id, ReadOnlyMemory<byte> source)
    {
        if (_reader == null) return null;
        try { return IsAvailable ? _reader(sheet, id, source) : null; }
        catch { return null; } // Keep the fallback readable when a game row is unavailable.
    }

    internal static ReadOnlySeString? Find<T>(IDataManager data, T row, Func<T, ReadOnlySeString> field)
        where T : struct, IExcelRow<T>
    {
        if (data.Language != ClientLanguage.English) return null;
        return Find(typeof(T).Name, row.RowId, field(row).Data);
    }

    internal static string? Name<T>(uint id, Func<T, ReadOnlySeString> field, string? sheetName = null)
        where T : struct, IExcelRow<T>
    {
        if (_data == null || _data.Language != ClientLanguage.English || _reader == null) return null;
        try
        {
            return _data.GetExcelSheet<T>(name: sheetName).TryGetRow(id, out var row)
                ? Find(sheetName ?? typeof(T).Name, row.RowId, field(row).Data)?.ExtractText() : null;
        }
        catch { return null; }
    }
}
