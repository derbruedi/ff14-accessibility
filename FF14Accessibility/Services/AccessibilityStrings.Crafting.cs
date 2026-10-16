using System;
using System.Globalization;
using System.Linq;

namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    public static string CraftingPoints(uint? current, uint? maximum) =>
        current.HasValue && maximum > 0
            ? L($"HP {current} von {maximum}.", $"CP {current} of {maximum}.",
                $"Очки работы {current} из {maximum}.")
            : L("HP unbekannt.", "CP unavailable.", "Очки работы недоступны.");

    public static string CraftingEffects(string effects) => effects.Length > 0
        ? L($"Wirkt: {effects}.", $"Active: {effects}.", $"Действует: {effects}.")
        : L("Keine aktiven Handwerkseffekte.", "No active crafting effects.", "Нет активных ремесленных эффектов.");

    public static string CraftingStep(string step) => step.Length == 0 ? string.Empty
        : L($"Schritt {step}.", $"Step {step}.", $"Шаг {step}.");

    public static string CraftingEffect(string name, string steps) => steps.Length == 0 || steps == "0"
        ? name : L($"{name}, noch {steps} Schritte", $"{name}, {steps} steps remaining",
            $"{name}, осталось шагов {steps}");

    private static string CraftingUnknown => L("unbekannt", "unknown", "неизвестно");

    public static string RecipeCrystal(string name, string needed, string have) =>
        L($"{name}, {CraftingCount(needed)} benötigt, vorhanden {CraftingCount(have)}",
            $"{name}, need {CraftingCount(needed)}, have {CraftingCount(have)}",
            $"{name}, нужно {CraftingCount(needed)}, есть {CraftingCount(have)}");

    // UI counters may use grouping separators or SeString formatting (removed
    // by AtkText.ReadClean). Missing data must never become a stock of zero.
    internal static bool TryCraftingCount(string value, out long count)
    {
        var digits = new string(value.Where(c => !char.IsWhiteSpace(c) && c != ',' && c != '.' && c != '\'').ToArray());
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    private static string CraftingCount(string value) => TryCraftingCount(value, out var count)
        ? count.ToString(CultureInfo.InvariantCulture) : CraftingUnknown;

    private static string MaterialWithStock(string name, string needed, string nq, string hq)
    {
        var total = TryCraftingCount(nq, out var normal) && TryCraftingCount(hq, out var high)
                    && normal <= long.MaxValue - high
            ? (normal + high).ToString(CultureInfo.InvariantCulture) : CraftingUnknown;
        var required = CraftingCount(needed);
        return L($"{name}, {required} benötigt, vorhanden {total}, davon {CraftingCount(nq)} NQ und {CraftingCount(hq)} HQ",
            $"{name}, need {required}, have {total}, including {CraftingCount(nq)} NQ and {CraftingCount(hq)} HQ",
            $"{name}, нужно {required}, есть {total}, из них обычных {CraftingCount(nq)}, высокого качества {CraftingCount(hq)}");
    }
}
