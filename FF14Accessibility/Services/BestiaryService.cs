using System.Text.RegularExpressions;
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

    // Sheet entries whose name carries a declension placeholder, as anchored
    // patterns (see BuildPattern). Searched only after an exact lookup failed.
    private List<(Regex Pattern, string Habitat, string SheetName)>? _declined;

    /// <summary>Declension placeholder in German sheet names: "gefräßig[a] Yarzon".</summary>
    private static readonly Regex PlaceholderRx = new(@"\[.\]", RegexOptions.Compiled);

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
        var wanted = monsterName.Trim().ToLowerInvariant();
        if (_habitats.TryGetValue(wanted, out var habitat))
            return habitat;

        // German sheet names store the adjective ending as a placeholder the
        // game fills in per grammatical case, so the UI name never matches
        // literally (log-verified 2026-07-19: sheet 'rostig[a] kobalos' vs. UI
        // 'Rostiger Kobalos'). Match those patterns - but ONLY when exactly one
        // fits. A wrong habitat sends the user to the wrong zone, so ambiguity
        // stays silent rather than guessing.
        var hits = _declined!.Where(d => d.Pattern.IsMatch(wanted)).Take(2).ToList();
        if (hits.Count == 1) return hits[0].Habitat;
        if (hits.Count > 1)
        {
            _log.Info($"[Bestiary] MEHRDEUTIG '{wanted}' - passt auf mehrere Sheet-Namen, keine Ansage.");
            return null;
        }

        ProbeMiss(wanted);
        return null;
    }

    /// <summary>
    /// Turns a sheet name with declension placeholders into an anchored pattern:
    /// "gefräßig[a] yarzon" matches "gefräßiger yarzon" and "gefräßige yarzon",
    /// but never "aas-yarzon" - the stem still has to line up.
    /// Returns null for names without a placeholder (those match exactly).
    /// </summary>
    private static Regex? BuildPattern(string lowerName)
    {
        if (!lowerName.Contains('[')) return null;
        var parts = PlaceholderRx.Split(lowerName).Select(Regex.Escape);
        return new Regex("^" + string.Join(@"\w*", parts) + "$", RegexOptions.Compiled);
    }

    /// <summary>
    /// Diagnostic for names the sheet lookup misses ("Gefraessiger Yarzon").
    /// Logs every sheet entry sharing a significant word with the UI name, so
    /// the LOG shows how the sheet actually spells it instead of us guessing.
    /// The candidate COUNT decides whether a relaxed match could ever be safe:
    /// exactly one candidate is unambiguous, several are not.
    /// </summary>
    private void ProbeMiss(string wanted)
    {
        var words = wanted.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(w => w.Length >= 4)
                          .ToArray();
        if (words.Length == 0) return;

        var hits = _habitats!.Keys
            .Where(k => words.Any(w => k.Contains(w, StringComparison.Ordinal)))
            .Take(6)
            .ToList();

        _log.Info($"[Bestiary] MISS '{wanted}' - {hits.Count} Sheet-Kandidat(en): "
                  + (hits.Count == 0 ? "(keiner)" : string.Join(" | ", hits.Select(h => $"'{h}'"))));
    }

    private Dictionary<string, string> BuildHabitats()
    {
        var map = new Dictionary<string, string>();
        _declined = new List<(Regex, string, string)>();
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

            var key     = name.ToLowerInvariant();
            var habitat = string.Join(", oder ", areas);
            map[key] = habitat;
            if (BuildPattern(key) is { } pattern)
                _declined.Add((pattern, habitat, key));
        }
        _log.Info($"[Bestiary] Lebensraum-Tabelle: {map.Count} Monster aus MonsterNoteTarget, "
                  + $"davon {_declined.Count} mit Deklinations-Platzhalter.");
        return map;
    }
}
