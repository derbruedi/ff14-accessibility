using System.Globalization;

namespace FF14Accessibility.Services;

/// <summary>A draft only. Typing and cancelling never write to the game.</summary>
internal sealed class ShopQuantityEdit
{
    internal readonly record struct Target(nint Addon, nint Row, nint Input, string ItemName,
        int Minimum, int Maximum, int Value);

    internal Target Original { get; }
    internal string Text { get; private set; }
    private bool replace = true;

    internal ShopQuantityEdit(Target target)
    {
        Original = target;
        Text = Math.Max(1, target.Value).ToString(CultureInfo.InvariantCulture);
    }

    internal static bool IsUsable(Target target) => target.Addon != 0 && target.Row != 0
        && target.Input != 0 && !string.IsNullOrWhiteSpace(target.ItemName)
        && target.Minimum >= 0 && target.Maximum >= Math.Max(1, target.Minimum);

    // Re-resolve the live row before every write. Reused renderers, another item,
    // a changed limit or a native edit invalidate the entire draft.
    internal bool Matches(Target current) => IsUsable(current) && current == Original;

    internal void Digit(int digit)
    {
        if (digit is < 0 or > 9) return;
        if (replace) Text = string.Empty;
        replace = false;
        // Keep one extra digit so an overlong entry becomes invalid rather
        // than silently accepting a truncated, potentially different amount.
        if (Text.Length < 11) Text += (char)('0' + digit);
    }

    internal void Backspace()
    {
        if (replace) Text = string.Empty;
        else if (Text.Length > 0) Text = Text[..^1];
        replace = false;
    }

    internal void Clear() { Text = string.Empty; replace = false; }

    internal bool TryValue(out int value)
    {
        value = 0;
        return Text.Length <= 10 && int.TryParse(Text, NumberStyles.None,
            CultureInfo.InvariantCulture, out value) && value >= Math.Max(1, Original.Minimum)
            && value <= Original.Maximum;
    }
}
