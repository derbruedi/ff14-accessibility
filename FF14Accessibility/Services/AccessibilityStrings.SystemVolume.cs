namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    internal static string SystemVolumeName(SystemSoundChannel channel) => channel switch
    {
        SystemSoundChannel.Master => "Общая громкость",
        SystemSoundChannel.Music => "Громкость музыки",
        SystemSoundChannel.Effects => "Громкость звуковых эффектов",
        SystemSoundChannel.Voice => "Громкость голосов персонажей",
        SystemSoundChannel.System => "Громкость системных звуков",
        SystemSoundChannel.Ambient => "Громкость окружающих звуков",
        SystemSoundChannel.Performance => "Громкость музыкальных инструментов",
        SystemSoundChannel.Self => "Громкость эффектов своего персонажа",
        SystemSoundChannel.Party => "Громкость эффектов группы",
        _ => "Громкость эффектов других игроков"
    };
    public static string SystemVolumeEditHint => L("Strg+Enter: genauen Prozentwert eingeben.", "Ctrl+Enter: enter an exact percentage.", "Ctrl+Enter — задать точный процент.");
    public static string SystemVolumeEditInstructions => L("Zahl von 0 bis 100 eingeben. Enter übernimmt, Escape bricht ab. Anwenden im Spiel speichert.", "Enter a number from 0 to 100. Enter applies, Escape cancels. Use Apply in the game to save.", "Введи число от 0 до 100. Enter меняет значение, Escape отменяет ввод. Кнопка «Применить» в игре сохраняет настройки.");
    public static string SystemVolumeRange => L("Zahl von 0 bis 100 eingeben.", "Enter a number from 0 to 100.", "Введи число от 0 до 100.");
    public static string SystemVolumeCancelled => L("Eingabe abgebrochen.", "Entry cancelled.", "Ввод отменён.");
    public static string SystemVolumeChanged => L("Regler nicht mehr verfügbar oder geändert. Eingabe abgebrochen.", "The slider is unavailable or changed. Entry cancelled.", "Ползунок недоступен или изменился. Ввод отменён.");
    public static string SystemVolumeNotApplied => L("Wertänderung nicht bestätigt.", "Value change was not confirmed.", "Изменение значения не подтверждено.");
    public static string SystemVolumeDraft(string text) => text.Length > 0 ? text : L("Leer.", "Empty.", "Пусто.");
}
