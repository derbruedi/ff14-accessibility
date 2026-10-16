namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string GatherProbeIndexes(int? gather, int? fish) => L(
        $"Sammelindex: {gather}; Fischindex: {fish}", $"Gathering index: {gather}; fishing index: {fish}",
        $"Записей сбора: {gather}; записей рыбалки: {fish}");
    public static string GatherProbeDivision(uint id, string name, int level) => L(
        $"Notizbuchabschnitt {id}: {name}, Freischaltstufe {level}",
        $"Notebook section {id}: {name}, unlock level {level}",
        $"Раздел журнала {id}: {name}, уровень открытия {level}");
    public static string GatherProbeList(uint id, int count) => L(
        $"Sammelliste {id}: {count} Gegenstände", $"Gathering list {id}: {count} items",
        $"Список сбора {id}: предметов {count}");
    public static string ProbeUnavailable => L("Sonde nicht möglich.", "Diagnostic snapshot unavailable.",
        "Сейчас не удалось собрать сведения для проверки.");
    public static string ActionProbeSaved(int count) => L($"Sonde: {count} Aktionen ins Log geschrieben.",
        $"Diagnostics: {count} actions written to the log.", $"В журнал записаны сведения об умениях: {count}.");
    public static string BeastmasterProbeSaved(int job, int level, int windows, int statuses) => L(
        $"Bestienbändiger-Sonde. Job {job}, Stufe {level}. {windows} Fenster, {statuses} Statuszeilen. Rest im Log.",
        $"Beastmaster diagnostics. Job {job}, level {level}. {windows} windows, {statuses} status rows. Details in the log.",
        $"Проверка укротителя зверей. Номер класса {job}, уровень {level}. Найдено окон: {windows}, эффектов: {statuses}. Подробности в журнале.");
    public static string FlightProbeUnknownArea => L("Flugsonde: Gebiet unbekannt.",
        "Flight diagnostics: unknown area.", "Проверка полёта: область не определена.");
    public static string FlightProbeSaved(string place, uint use, uint set, bool complete, FlightBlock blocked) => L(
        $"Flugsonde. Gebiet {place}. Nutzung {use}, Ätherstrom-Satz {set}, komplett {(complete ? "ja" : "nein")}. Urteil {blocked}.",
        $"Flight diagnostics. Area {place}. Usage {use}, aether current set {set}, complete {(complete ? "yes" : "no")}. Result {blocked}.",
        $"Проверка полёта. Область: {place}. Тип области: {use}, набор эфирных потоков: {set}, все найдены: {(complete ? "да" : "нет")}. {FlightBlockedReason(blocked)}");
    public static string FishingProbeSaved(int count) => L($"{count} Objekte in 50 Metern im Log.",
        $"{count} objects within 50 metres written to the log.", $"В журнал записаны объекты в радиусе 50 метров: {count}.");
    public static string ObjectProbeNoPlayer => L("Objekt-Sonde: kein Spieler.",
        "Object diagnostics: no player.", "Проверка объектов: персонаж недоступен.");
    public static string ObjectProbeSaved(int count) => L($"Objekt-Sonde: {count} Objekte im Log.",
        $"Object diagnostics: {count} objects in the log.", $"В журнал записаны сведения об объектах: {count}.");
    public static string MarkerProbeSaved(long events, long minimap, int places) => L(
        $"Marker-Sonde: {events} Event, {minimap} Minimap, {places} Orte im Log.",
        $"Marker diagnostics: {events} event markers, {minimap} minimap markers, {places} places in the log.",
        $"В журнал записаны метки событий: {events}, метки мини-карты: {minimap}, места: {places}.");
    public static string SystemConfigProbeSaved(int count) => L($"ConfigSystem-Diagnose: {count} Elemente.",
        $"System settings diagnostics: {count} elements.", $"В журнал записаны элементы системных настроек: {count}.");
    public static string CollisionProbeSaved(int count) => L($"Kollisionssonde: {count} Einträge im Log.",
        $"Collision diagnostics: {count} entries in the log.", $"В журнал записаны сведения о препятствиях: {count}.");
    public static string LiftProbeCancelled => L("Aufzug-Sonde abgebrochen.", "Lift diagnostics cancelled.", "Проверка подъёмника отменена.");
    public static string LiftProbeNoPlayer => L("Aufzug-Sonde: kein Spieler.", "Lift diagnostics: no player.", "Проверка подъёмника: персонаж недоступен.");
    public static string LiftProbeStarted(double seconds) => L(
        $"Aufzug-Sonde läuft, {seconds:F0} Sekunden. Jetzt auf den Aufzug stellen und ihn auslösen.",
        $"Lift diagnostics running for {seconds:F0} seconds. Stand on the lift and activate it now.",
        $"Проверка подъёмника начата на {seconds:F0} секунд. Встаньте на подъёмник и запустите его.");
    public static string LiftProbeFinished => L("Aufzug-Sonde fertig.", "Lift diagnostics finished.", "Проверка подъёмника завершена.");
    public static string LiftProbeMoving(bool up) => L($"Du fährst. Höhe {(up ? "steigt" : "fällt")}.",
        $"You are moving {(up ? "up" : "down")}.", $"Вы движетесь {(up ? "вверх" : "вниз")}.");
    public static string AozProbeClosed => L("Das Zauberbuch der Blaumagie ist nicht offen.",
        "The blue magic spellbook is not open.", "Книга заклинаний синей магии не открыта.");
    public static string AozProbeCounterUnreadable => L("Fenster-Zähler nicht lesbar, Vergleich offen.",
        "The window counter cannot be read; comparison pending.", "Не удалось прочитать счётчик в окне; сравнение пока невозможно.");
    public static string AozProbeCounterMatches(int known) => L($"UnlockLink stimmt mit dem Fenster überein ({known}).",
        $"Unlock data matches the window ({known}).", $"Число изученных заклинаний совпадает с окном: {known}.");
    public static string AozProbeCounterMismatch(int known, int windowKnown) => L(
        $"ACHTUNG, UnlockLink sagt {known}, das Fenster {windowKnown}.",
        $"Warning: unlock data reports {known}, the window reports {windowKnown}.",
        $"Внимание: по данным игры изучено {known}, а окно показывает {windowKnown}.");
    public static string AozProbeSaved(int tab, int tabs, int actions, int filled, string verdict) => L(
        $"Blaumagie-Sonde. Reiter {tab} von {tabs}. {actions} von 16 Kacheln mit Aktions-Id, {filled} von 24 Plätzen belegt. {verdict} Rest im Log.",
        $"Blue magic diagnostics. Tab {tab} of {tabs}. {actions} of 16 tiles have an action ID, {filled} of 24 slots filled. {verdict} Details in the log.",
        $"Проверка синей магии. Вкладка {tab} из {tabs}. Определены умения в ячейках: {actions} из 16. Заняты места: {filled} из 24. {verdict} Подробности в журнале.");
}
