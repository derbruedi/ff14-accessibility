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
    public static string SquadronTraining => L("Training", "Training", "Тренировка");
    public static string SquadronTrainingOption(int number) => L($"Training {number}", $"Training {number}", $"Тренировка {number}");
    public static string SquadronTrainingReadHelp(string key) => L(
        $"{key} liest die Vorschau mit aktuellen Werten und Änderungen.",
        $"{key} reads the preview with current values and changes.",
        $"{key} читает предпросмотр: текущие значения и изменения характеристик.");
    public static string SquadronTrainingPreview => L("Trainingsvorschau", "Training preview", "Предпросмотр тренировки");
    public static string SquadronTrainingPreviewDiffers => L(
        "Die Vorschau gehört zur im Fenster gewählten Übung. Die Spieltasten ändern die Auswahl.",
        "The preview belongs to the exercise selected in the window. Use the game's keys to change the selection.",
        "Предпросмотр относится к тренировке, выбранной в окне. Сменить выбор можно обычными клавишами игры.");
    public static string SquadronTrainingNow(string value) => L($"aktuell {value}", $"current {value}", $"сейчас {value}");
    public static string SquadronTrainingIncrease(string value) => L($"steigt um {value}", $"increases by {value}", $"увеличится на {value}");
    public static string SquadronTrainingDecrease(string value) => L($"sinkt um {value}", $"decreases by {value}", $"уменьшится на {value}");
    public static string SquadronTrainingUnchanged => L("unverändert", "unchanged", "без изменений");
    public static string SquadronTrainingChangeUnknown => L("Änderung nicht angezeigt", "change not shown", "изменение не показано");
    public static string SquadronTrainingChange(string value) => L($"angezeigte Änderung {value}", $"displayed change {value}", $"изменение по данным окна: {value}");
    public static string SquadronReadHelp(string key) => L(
        $"{key} liest alle sichtbaren Angaben. Die Spieltasten wählen und bestätigen.",
        $"{key} reads all visible details. Use the game's keys to select and confirm.",
        $"{key} читает все видимые сведения. Выбор и подтверждение выполняются обычными клавишами игры.");
}
