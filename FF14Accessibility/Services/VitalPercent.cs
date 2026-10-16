namespace FF14Accessibility.Services;

internal static class VitalPercent
{
    // Boss HP can exceed uint.MaxValue / 100. Widen BEFORE multiplication.
    internal static int Floor(uint current, uint maximum) =>
        maximum == 0 ? 0 : (int)Math.Min(100UL, (ulong)current * 100 / maximum);
}
