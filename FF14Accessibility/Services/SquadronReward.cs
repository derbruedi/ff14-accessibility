namespace FF14Accessibility.Services;

/// <summary>Only translate rows matching the visible reward icon. Translating
/// the entire Item sheet through a display-text plugin can stall the UI thread.</summary>
internal sealed class SquadronReward
{
    private readonly Dictionary<uint, string> _names = [];
    private (LanguageMode Language, int Revision) _key;

    internal string Resolve<T>(uint wanted, LanguageMode language, int revision,
        IEnumerable<T> items, Func<T, uint> icon, Func<T, string> name)
    {
        if (wanted == 0) return "";
        var key = (language, revision);
        if (key != _key) { _names.Clear(); _key = key; }
        if (_names.TryGetValue(wanted, out var cached)) return cached;
        var result = "";
        foreach (var item in items)
        {
            if (icon(item) != wanted) continue;
            var text = name(item);
            if (!string.IsNullOrWhiteSpace(text)) result = text;
        }
        _names[wanted] = result;
        return result;
    }
}
