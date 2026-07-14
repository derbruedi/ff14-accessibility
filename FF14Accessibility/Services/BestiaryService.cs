using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Resolves hunting log (bestiary) monsters to their habitat. The game ships
/// this in the MonsterNoteTarget sheet: per monster up to three spawn areas as
/// zone + sub-area PlaceName pairs - the same habitat info the hunting log
/// shows sighted players (sheet structure ilspycmd-verified 2026-07-13:
/// BNpcName ref, PlaceNameZone[3], PlaceNameLocation[3]).
/// </summary>
public sealed class BestiaryService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    // Monster name (lowercase) -> spoken habitat text. Built lazily on first
    // bestiary use; sheet data is static per game version.
    private Dictionary<string, string>? _habitats;

    public BestiaryService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>
    /// Habitat text for a hunting log monster ("Mittleres La Noscea, Sommerfurt")
    /// or null when the name is not a hunting log target. Case-insensitive.
    /// </summary>
    public string? GetHabitat(string monsterName)
    {
        _habitats ??= BuildHabitats();
        return _habitats.TryGetValue(monsterName.Trim().ToLowerInvariant(), out var habitat)
            ? habitat
            : null;
    }

    private Dictionary<string, string> BuildHabitats()
    {
        var map = new Dictionary<string, string>();
        foreach (var row in _data.GetExcelSheet<MonsterNoteTarget>())
        {
            var name = row.BNpcName.ValueNullable?.Singular.ExtractText().Trim() ?? string.Empty;
            if (name.Length == 0) continue;

            // Up to three spawn areas; zone and sub-area are parallel arrays.
            var areas = new List<string>();
            for (var i = 0; i < row.PlaceNameZone.Count; i++)
            {
                var zone = row.PlaceNameZone[i].ValueNullable?.Name.ExtractText().Trim() ?? string.Empty;
                if (zone.Length == 0) continue;
                var loc = i < row.PlaceNameLocation.Count
                    ? row.PlaceNameLocation[i].ValueNullable?.Name.ExtractText().Trim() ?? string.Empty
                    : string.Empty;
                areas.Add(loc.Length > 0 && loc != zone ? $"{zone}, {loc}" : zone);
            }
            if (areas.Count == 0) continue;

            map[name.ToLowerInvariant()] = string.Join(", oder ", areas);
        }
        _log.Info($"[Bestiary] Lebensraum-Tabelle: {map.Count} Monster aus MonsterNoteTarget.");
        return map;
    }
}
