namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string OptAoeAutoTurn => L("Automatisch aus Flaechen ausrichten", "Automatically face the AoE exit", "Автоповорот к выходу из AoE");
    public static string AoeAutoTurnState(bool enabled) => enabled
        ? L("Automatisches Ausrichten an. Bei Gefahr vorwaerts laufen; du steuerst die Bewegung.", "Automatic AoE facing on. When warned, move forward; you control movement.", "Автоповорот включён. При подсказке беги вперёд; движением управляешь ты.")
        : L("Automatisches Ausrichten aus.", "Automatic AoE facing off.", "Автоповорот выключен.");
    public static string AoeAutoTurnUsage => L("/acc aoeturn on oder off; /acc aoeturn zeigt den Zustand.", "/acc aoeturn on or off; /acc aoeturn reads the setting.", "/acc aoeturn on — включить, off — выключить; /acc aoeturn — узнать состояние. Можно также: /acc автоповорот вкл или выкл.");
    public static string AoeAutoTurnForward(string distance) => L($"Zum Ausgang ausgerichtet. Vorwaerts, {distance}.", $"Facing the exit. Forward, {distance}.", $"Повёрнут к выходу. Вперёд, {distance}.");
    public static string AoeAutoTurnNoPath => L("Kein gerader Ausgang bestaetigt. Selbst ausweichen.", "No straight exit confirmed. Dodge manually.", "Прямой выход не подтверждён. Уклоняйся вручную.");
    public static string AoeAutoTurnNotConfirmed => L("Ausrichtung nicht bestaetigt. Selbst ausweichen.", "Facing was not confirmed. Dodge manually.", "Поворот не подтверждён. Уклоняйся вручную.");
}
