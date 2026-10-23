namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string SquadronPhysical => L("Körperlich", "Physical", "Физические навыки");
    public static string SquadronMental => L("Mental", "Mental", "Ментальные навыки");
    public static string SquadronTactical => L("Taktisch", "Tactical", "Тактические навыки");
    public static string SquadronChemistry => L("Kameradschaft", "Chemistry", "Совместимость");
    public static string SquadronTactics => L("Kampftaktik", "Battle tactics", "Боевая тактика");
    public static string SquadronLevel(string value) => L($"Stufe {value}", $"Level {value}", $"Уровень {value}");
    public static string SquadronRequired => L("Benötigte Werte", "Required attributes", "Требуемые характеристики");
    public static string SquadronCurrent => L("Aktuelle Werte", "Current attributes", "Текущие характеристики");
    public static string SquadronCost => L("Kosten", "Expenditures", "Затраты");
    public static string SquadronReward => L("Grundbelohnung", "Basic reward", "Основная награда");
    public static string SquadronExperience => L("Routine", "EXP", "Опыт");
    public static string SquadronLoading => L("Die Angaben werden geladen.", "Details are loading.", "Сведения загружаются.");
    public static string SquadronReadHelp(string key) => L(
        $"{key} liest alle sichtbaren Angaben. Die Spieltasten wählen und bestätigen.",
        $"{key} reads all visible details. Use the game's keys to select and confirm.",
        $"{key} читает все видимые сведения. Выбор и подтверждение выполняются обычными клавишами игры.");
}
