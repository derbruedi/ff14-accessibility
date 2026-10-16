using System;

namespace FF14Accessibility.Services;

public static partial class AccessibilityStrings
{
    // Speech only: target altitude never changes the author's movement rules.
    public static string TargetHeight(float rise) => MathF.Abs(rise) < 1.5f ? string.Empty : L(
        $" Ziel {MathF.Abs(rise):F0} Meter {(rise > 0 ? "höher" : "tiefer")}.",
        $" Target {MathF.Abs(rise):F0} meters {(rise > 0 ? "above" : "below")}.",
        $" Цель на {MathF.Abs(rise):F0} метров {(rise > 0 ? "выше" : "ниже")}.");
}
