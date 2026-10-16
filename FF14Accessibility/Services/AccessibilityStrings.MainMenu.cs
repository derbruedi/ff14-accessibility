namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    /// <summary>Only the Russian mode substitutes the native category name.</summary>
    public static string MainMenuCategoryName(uint categoryId, string gameName)
        => !Loc.IsRussian || string.IsNullOrWhiteSpace(gameName) ? gameName : categoryId switch
        {
            1 => "Персонаж",
            2 => "Задания и миссии",
            3 => "Журналы",
            4 => "Путешествия",
            5 => "Группа",
            6 => "Общение",
            7 => "Система",
            _ => gameName,
        };
}
