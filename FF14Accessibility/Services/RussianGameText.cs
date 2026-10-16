using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace FF14Accessibility.Services;

/// <summary>Row-scoped names; never rewrites chat, player names or search keys.</summary>
internal static class RussianGameText
{
    private static readonly Lazy<RussianDescriptionCatalog?> Names = new(() =>
    {
        try { return RussianDescriptionCatalog.Load("RussianActionNames"); }
        catch { return null; } // A missing resource must not make the interface unreadable.
    });

    internal static string Name<T>(IDataManager data, T row, Func<T, ReadOnlySeString> field)
        where T : struct, IExcelRow<T>
        => Text(data, row, field, "Name").ExtractText();

    internal static ReadOnlySeString Text<T>(IDataManager data, T row, Func<T, ReadOnlySeString> field, string column)
        where T : struct, IExcelRow<T>
    {
        var original = field(row);
        if (GameDisplayText.Find(data, row, field) is { } displayed) return displayed;
        if (!GameTextTranslation.ShouldTranslate(typeof(T).Name) || Names.Value == null) return original;
        try
        {
            if (data.GetExcelSheet<T>(ClientLanguage.English).TryGetRow(row.RowId, out var english))
            {
                var value = Names.Value.Find(typeof(T).Name + column, row.RowId, field(english).Data.Span, true);
                if (value != null) return new ReadOnlySeString(value);
            }
        }
        catch { /* Keep the current game language if a sheet is unavailable. */ }
        return original;
    }
}
