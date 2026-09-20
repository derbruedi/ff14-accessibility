namespace FF14Accessibility.Services;

/// <summary>Normal cross hotbars: module bars 10..17, sixteen slots each.
/// Slot names follow the game's /crosshotbar naming (see docs/game-api.md).</summary>
internal static class CrossHotbarLayout
{
    internal const int FirstBar = 10;
    internal const int SetCount = 8;
    internal const int SlotCount = 16;

    internal static bool IsCrossBar(int bar) => bar is >= FirstBar and < FirstBar + SetCount;
    internal static bool IsValidTarget(int bar, int slot) => IsCrossBar(bar) && slot is >= 0 and < SlotCount;

    internal static string SlotCode(int slot)
    {
        if (slot is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return (slot < 8 ? "L" : "R") + (slot % 8 < 4 ? "D" : "A") + "LURD"[slot % 4];
    }

    // Picker choices 0..7 are cross sets; choice 8 retains the keyboard targets.
    internal static int MoveChoice(int choice, int direction) => ((choice + direction) % 9 + 9) % 9;
}
