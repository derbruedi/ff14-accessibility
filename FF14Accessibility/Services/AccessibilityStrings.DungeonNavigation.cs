namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string NavigationMeshStatus(bool ready, int steps) => L(
        $"Wegenetz {(ready ? "bereit" : "nicht bereit")}. Dungeon-Stationen hier: {steps}.",
        $"Navigation mesh {(ready ? "ready" : "not ready")}. Dungeon stations here: {steps}.",
        $"Навигационная сетка {(ready ? "готова" : "не готова")}. Точек подземелья в этой зоне: {steps}.");
    public static string NavigationMeshRebuilding => L(
        "Das Wegenetz wird neu erstellt. Bitte warten und danach die Navigation erneut starten.",
        "Rebuilding the navigation mesh. Wait, then start navigation again.",
        "Навигационная сетка строится заново. Подожди завершения, затем снова включи движение.");
    public static string NavigationMeshRebuildFailed => L(
        "Das Wegenetz konnte nicht neu erstellt werden. Ist vnavmesh aktiv?",
        "Could not rebuild the navigation mesh. Is vnavmesh enabled?",
        "Не удалось запустить перестроение сетки. Проверь, включён ли vnavmesh.");
}
