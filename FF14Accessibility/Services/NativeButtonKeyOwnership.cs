namespace FF14Accessibility.Services;

/// <summary>Keep a handled confirmation key away from the game until key-up,
/// even if the action has already closed its dialog or opened another one.</summary>
internal sealed class NativeButtonKeyOwnership
{
    private readonly HashSet<int> _owned = [];

    internal void Claim(int key) => _owned.Add(key);

    internal bool Suppress(int key, bool down)
    {
        if (!_owned.Contains(key)) return false;
        if (down) return true;
        _owned.Remove(key);
        return false;
    }
}
