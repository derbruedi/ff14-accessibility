namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string NavigationPathUnavailable(string name) => L(
        $"{name}: kein vollständiger Weg. Ein Durchgang oder Transport kann erforderlich sein. Prüfe den Zugang im Spiel.",
        $"{name}: no complete path. A passage or transport may be required. Check access in game.",
        $"К «{name}» нет полного пути. Возможно, требуется открыть проход или воспользоваться транспортом. Проверь доступ в игре.");
    public static string LocalTransferRoute(string goal, string actor) => L(
        $"Zu {goal} zuerst über {actor}. Numpad 0 öffnet den Dialog. Bestätige den Durchgang im Spiel, dann Numpad 3.",
        $"To reach {goal}, first use {actor}. Numpad 0 opens the dialog. Confirm passage in game, then press Numpad 3.",
        $"Чтобы добраться к «{goal}», сначала подойди к «{actor}». Num0 открывает разговор. Подтверди проход в игре, затем нажми Num3.");
    public static string WalkingTransitionConfirm(string name) => L(
        $"{name}: zu Fuß durch den Übergang. Numpad 3 führt weiter; hier gibt es keinen Dialog.",
        $"{name}: walk through the transition. Numpad 3 continues; there is no dialog here.",
        $"{name}: нужно пройти через границу зоны. Num3 ведёт к проходу; разговор здесь не требуется.");
    public static string QuestDutyRoute(string quest, string duty) => L(
        $"{quest}: Eingang zu {duty}", $"{quest}: entrance to {duty}", $"{quest}: вход в «{duty}»");
    public static string QuestDutyFinderRoute(string quest, string duty) => L(
        $"{quest}: im freigeschalteten Inhalt {duty}. Öffne die Inhaltssuche und wähle {duty}. Nach dem Betreten führt Numpad 3 zum aktuellen Questziel.",
        $"{quest}: in the unlocked duty {duty}. Open Duty Finder and select {duty}. After entering, Numpad 3 leads to the current quest objective.",
        $"{quest}: цель в открытом подземелье «{duty}». Открой поиск подземелий и выбери «{duty}». После входа Num3 ведёт к текущей цели задания.");
    public static string QuestGoalUpdating => L(
        "Das aktuelle Questziel wird aktualisiert. Warte auf die Karte und drücke Numpad 3 erneut.",
        "The current quest objective is updating. Wait for the map and press Numpad 3 again.",
        "Текущая цель задания обновляется. Дождись появления отметки и снова нажми Num3.");
    public static string QuestTravelUnavailable(string quest, string zone) => L(
        $"{quest}: Ziel {zone}. Kein bestätigter Reiseweg in den Spieldaten. Prüfe Teleport, das Questziel oder den Zugang zum Inhalt.",
        $"{quest}: destination {zone}. No verified travel route in game data. Check Teleport, the current objective, or the duty entrance.",
        $"{quest}: место назначения — {zone}. В данных игры нет подтверждённого маршрута. Проверь телепортацию, текущую задачу в журнале или вход в подземелье.");
    public static string QuestTravelConfirm(string zone) => L(
        $" Numpad 0 öffnet den Dialog. Wähle {zone} und bestätige im Spiel. Danach führt Numpad 3 weiter zum Questziel.",
        $" Numpad 0 opens the dialog. Choose {zone} and confirm in game. Afterwards Numpad 3 continues towards the quest objective.",
        $" Num0 открывает разговор. Выбери «{zone}» и подтверди переход в игре. Затем Num3 ведёт дальше к цели задания.");
    public static string QuestTeleportRoute(string quest, string aetheryte, string zone) => L(
        $"{quest}: nach {zone} über den freigeschalteten Ätheryten {aetheryte}. Öffne Teleport und bestätige {aetheryte} im Spiel. Danach führt Numpad 3 weiter.",
        $"{quest}: travel to {zone} via the attuned aetheryte {aetheryte}. Open Teleport and confirm {aetheryte} in game. Afterwards Numpad 3 continues.",
        $"{quest}: путь в «{zone}» через открытый эфирит «{aetheryte}». Открой телепортацию и подтверди «{aetheryte}» в игре. Затем Num3 ведёт дальше.");
}
