namespace FF14Accessibility.Services;

/// <summary>Read a filled/changed modal body once; empty setup frames are not speech.</summary>
internal sealed class SelectOkAnnouncement
{
    private string _pending = string.Empty, _spoken = string.Empty;
    private long _changedAt;

    internal string Update(string text, bool visible, long now, bool repeat = false)
    {
        if (!visible) { _pending = _spoken = string.Empty; return string.Empty; }
        text = TolkService.Sanitize(text);
        if (text != _pending) { _pending = text; _changedAt = now; }
        if (text.Length == 0 || (!repeat && (text == _spoken || now - _changedAt < 150))) return string.Empty;
        _spoken = text;
        return text;
    }
}
