namespace FF14Accessibility.Services;

/// <summary>Exact original/display aliases retain row identity. Conflicting
/// aliases are removed, including a translated name that collides with an
/// original name on a different row.</summary>
internal static class GameNameIndex
{
    internal static Dictionary<string, uint> Build(IEnumerable<(uint Id, string Original, string? Display)> rows)
    {
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, original, display) in rows)
            foreach (var text in new[] { original, display })
            {
                if (string.IsNullOrWhiteSpace(text)) continue;
                var name = text.Trim();
                if (map.TryGetValue(name, out var previous) && previous != id) conflicts.Add(name);
                else map[name] = id;
            }
        foreach (var name in conflicts) map.Remove(name);
        return map;
    }
}
