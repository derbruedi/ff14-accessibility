namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string ItemActionTranslationUsage => L(
        "Russische Gegenstands- und Faehigkeitsnamen und Beschreibungen: /acc translate off zum Ausschalten, /acc translate on zum Einschalten, /acc translate fuer den Zustand.",
        "Russian item and ability names and descriptions: /acc translate off to disable, /acc translate on to enable, /acc translate to read the setting.",
        "Русские названия и описания предметов и умений: /acc translate off — отключить, /acc translate on — включить, /acc translate — узнать состояние. Можно также: /acc перевод выкл или /acc перевод вкл.");

    public static string ItemActionTranslationState(bool enabled) => enabled
        ? L("Russische Uebersetzung von Gegenstaenden und Faehigkeiten eingeschaltet. Sie gilt bei russischer Ansagesprache.",
            "Russian item and ability translation enabled. It applies when the announcement language is Russian.",
            "Русский перевод предметов и умений включён. Он применяется при русском языке озвучивания.")
        : L("Russische Uebersetzung von Gegenstaenden und Faehigkeiten ausgeschaltet. Namen und Beschreibungen folgen den Spieldaten.",
            "Russian item and ability translation disabled. Names and descriptions follow the game data.",
            "Русский перевод предметов и умений отключён. Названия и описания читаются из данных игры.");
}
