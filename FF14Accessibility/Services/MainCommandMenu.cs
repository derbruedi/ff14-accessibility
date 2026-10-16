using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>Names for the seven category buttons in the native Num+ menu.</summary>
public static class MainCommandMenu
{
    public static string ReadCategoryName(IDataManager data, uint categoryId)
    {
        // _MainCommand ButtonClick.Param is a MainCommandCategory row id.
        // MainCommand uses the same small ids for unrelated individual commands:
        // e.g. category 7 is System, whereas command 7 is Gathering Log.
        var row = data.GetExcelSheet<MainCommandCategory>()?.GetRowOrDefault(categoryId);
        if (row is not { } category) return string.Empty;
        var original = category.Name.ExtractText().Trim();
        return original.Length == 0 ? string.Empty
            : AccessibilityStrings.MainMenuCategoryName(categoryId, original);
    }
}
