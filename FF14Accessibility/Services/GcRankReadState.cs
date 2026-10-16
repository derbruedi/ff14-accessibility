namespace FF14Accessibility.Services;

// Focus is sampled every frame. Log a changed layout/read once, while keeping
// the safe positional label available on every call (including manual reads).
internal sealed class GcRankReadState
{
    private (nint Owner, int Visible, int Expected)? _mismatch;
    private (nint Owner, uint Node, string Text)? _read;

    internal bool ShouldWarn(nint owner, int visible, int expected)
    {
        if (visible == expected) { _mismatch = null; return false; }
        var next = (owner, visible, expected);
        if (_mismatch == next) return false;
        _mismatch = next;
        return true;
    }

    internal bool ShouldLog(nint owner, uint node, string text)
    {
        var next = (owner, node, text);
        if (_read == next) return false;
        _read = next;
        return true;
    }

    internal void Reset() { _mismatch = null; _read = null; }
}
