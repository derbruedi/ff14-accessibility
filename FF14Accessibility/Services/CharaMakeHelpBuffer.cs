namespace FF14Accessibility.Services;

/// <summary>Help belongs to a visible creation step and the text still displayed there.</summary>
internal sealed class CharaMakeHelpBuffer
{
    private string _owner = string.Empty;
    private string _text = string.Empty;
    private long _queuedAt;
    internal bool HasPending => _text.Length > 0;

    internal void Clear() => (_owner, _text) = (string.Empty, string.Empty);

    internal void Queue(string owner, string text, long now)
    {
        Clear();
        // Class speech comes from the selected class's own Lobby row.
        if (owner.Length == 0 || owner == "_CharaMakeClassSelector" || string.IsNullOrWhiteSpace(text)) return;
        _owner = owner;
        _text = text;
        _queuedAt = now;
    }

    internal string Take(string owner, string visibleText, long now, bool force = false)
    {
        if (_owner != owner || _text != visibleText) { Clear(); return string.Empty; }
        if (!force && now - _queuedAt < 250) return string.Empty;
        var text = _text;
        Clear();
        return text;
    }
}
