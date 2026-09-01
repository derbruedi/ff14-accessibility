using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// The stations of a dungeon, in the order they have to be walked.
///
/// <para>
/// WHY THIS EXISTS. Every other browser category answers "what is around me".
/// Inside a dungeon that is the wrong question - the room is full of scenery and
/// none of it says which door continues the run. What a sighted player has and a
/// blind one does not is the ORDER: first that terminal, then the boss, then the
/// gate behind it. Knowing where floor continues would not answer it either -
/// it cannot say which of four open directions is the way onward.
/// </para>
///
/// <para>
/// THE SOURCE IS A PATH FILE ON DISK, never anything bundled. Each file lists
/// the stations of one duty as positions. The plugin reads whatever lies in its
/// own configuration folder and ships nothing itself, which keeps the data a
/// private matter of the machine it sits on - the same separation the third
/// party combat plugins already have. A missing folder is not an error, it means
/// the category simply is not offered.
/// </para>
///
/// <para>
/// WHAT THIS IS NOT: a second pathfinder. Between two stations the auto-walk
/// runs on the ordinary mesh exactly as it does everywhere else. The stations are
/// coarse - a handful per dungeon, not a dense line - so where vnavmesh has a
/// hole it still has a hole. This closes the ORDERING gap, not the mesh gap;
/// recorded trails (<see cref="TrailService"/>) remain the answer to the latter.
/// </para>
/// </summary>
public sealed class DungeonRouteService
{
    /// <summary>Folder below the plugin configuration directory that holds the
    /// path files. Read only, never written by us.</summary>
    private const string FolderName = "DungeonPaths";

    /// <summary>
    /// A path file is named "(TerritoryId) Some Duty Name.json". The territory id
    /// in the leading bracket is the whole reason the folder can be read without
    /// any index: it maps a file to a zone directly.
    /// </summary>
    private static readonly Regex FileNamePattern =
        new(@"^\((?<id>\d+)\)\s*(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Only these steps are PLACES. A path file also carries control steps -
    /// wait, fight what attacks, a comment - and those have no position worth
    /// walking to. Listing them would pad the route with entries that answer
    /// nothing and push the real stations further apart.
    /// </summary>
    private static readonly Dictionary<string, DungeonStepKind> PlaceSteps =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MoveTo"]         = DungeonStepKind.Waypoint,
            ["AutoMoveFor"]    = DungeonStepKind.Waypoint,
            ["Interactable"]   = DungeonStepKind.Interact,
            ["Boss"]           = DungeonStepKind.Boss,
            ["TreasureCoffer"] = DungeonStepKind.Treasure,
            ["Jump"]           = DungeonStepKind.Jump,
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // The files come in two generations that differ only in the case of
        // their keys ("Actions" vs "actions"). One reader has to swallow both,
        // or three quarters of the folder silently reads as empty - which is
        // exactly the kind of failure a blind player cannot see.
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly ObjectNameService _names;
    private readonly IPluginLog _log;

    /// <summary>Territory currently parsed, and its stations. Cached because the
    /// browser asks on every keypress and a duty does not change under us.</summary>
    private uint _cachedTerritory = uint.MaxValue;
    private IReadOnlyList<DungeonStep> _cachedSteps = Array.Empty<DungeonStep>();

    /// <summary>Territory id -> path file, built once from the folder listing.
    /// Null until the first look; an empty map means the folder holds nothing
    /// usable and is a perfectly ordinary state.</summary>
    private Dictionary<uint, string>? _filesByTerritory;

    public DungeonRouteService(
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        ObjectNameService names,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _names = names;
        _log = log;
    }

    /// <summary>Where the path files are expected. Public so the settings menu
    /// and the log can name the folder the player has to fill.</summary>
    public string PathFolder =>
        Path.Combine(_pluginInterface.GetPluginConfigDirectory(), FolderName);

    /// <summary>
    /// How many path files lie in the folder, counted without parsing any of
    /// them.
    ///
    /// It exists for the two questions that must be answerable WITHOUT standing
    /// in a dungeon: whether the folder needs filling at all (see
    /// <see cref="DungeonPathDownloadService"/>) and what the settings menu says
    /// out loud. Both used to be unanswerable, which is how a category nobody
    /// could see stayed unnoticed for a whole release.
    /// </summary>
    public int CountPathFiles()
    {
        try
        {
            return Directory.Exists(PathFolder)
                ? Directory.GetFiles(PathFolder, "*.json").Length
                : 0;
        }
        catch (Exception ex)
        {
            // The folder is on the player's disk: it can be denied, on a
            // disconnected drive, or gone between the check and the listing.
            _log.Error($"[Dungeon] Pfadordner '{PathFolder}' nicht zaehlbar: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// The stations of the duty the player is standing in, in walking order, or
    /// an empty list when no path file covers this zone.
    /// </summary>
    public IReadOnlyList<DungeonStep> GetStepsForCurrentZone()
    {
        var territory = _clientState.TerritoryType;
        if (territory == _cachedTerritory) return _cachedSteps;

        _cachedTerritory = territory;
        _cachedSteps = LoadSteps(territory);
        return _cachedSteps;
    }

    /// <summary>
    /// Throws away the parsed route. Called when the player drops files into the
    /// folder while the game runs - without this the empty first answer would
    /// stick until the next zone change.
    /// </summary>
    public void Reload()
    {
        _filesByTerritory = null;
        _cachedTerritory = uint.MaxValue;
        _cachedSteps = Array.Empty<DungeonStep>();
    }

    private IReadOnlyList<DungeonStep> LoadSteps(uint territory)
    {
        var files = _filesByTerritory ??= ScanFolder();
        if (!files.TryGetValue(territory, out var file)) return Array.Empty<DungeonStep>();

        PathFile? parsed;
        try
        {
            // File and JSON access are both external: the file can be half
            // written, hand edited or from a future format version. This is
            // exactly the try-catch the error rules allow, and it logs.
            parsed = JsonSerializer.Deserialize<PathFile>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Error($"[Dungeon] Pfaddatei '{Path.GetFileName(file)}' nicht lesbar: {ex.Message}");
            return Array.Empty<DungeonStep>();
        }

        var actions = parsed?.Actions;
        if (actions == null || actions.Count == 0)
        {
            _log.Warning($"[Dungeon] Pfaddatei '{Path.GetFileName(file)}' enthält keine Schritte.");
            return Array.Empty<DungeonStep>();
        }

        var steps = new List<DungeonStep>();
        foreach (var action in actions)
        {
            if (action.Name == null || !PlaceSteps.TryGetValue(action.Name, out var kind)) continue;

            var position = new Vector3(action.Position.X, action.Position.Y, action.Position.Z);

            // A place step without a place is a broken row, not a station at the
            // world origin. Walking a player to (0|0|0) is the worst possible
            // reading of a missing value.
            if (position == Vector3.Zero) continue;

            steps.Add(new DungeonStep(steps.Count + 1, kind, position, ResolveName(action, kind)));
        }

        _log.Info($"[Dungeon] '{Path.GetFileName(file)}' gelesen: {steps.Count} Stationen " +
                  $"aus {actions.Count} Schritten (Zone {territory}).");
        return steps;
    }

    /// <summary>
    /// The spoken name of a station.
    ///
    /// <para>
    /// THE DATA ID IS THE PREFERRED SOURCE, not the note that sits beside it. An
    /// interact step carries the object's data id, and that resolves through
    /// <see cref="ObjectNameService"/> into the name the game itself uses - in
    /// the player's language, and identical to what the object browser says about
    /// the very same door. The note in the file is English and written by
    /// whoever recorded the path; it is the fallback, never the first choice.
    /// </para>
    /// </summary>
    private string ResolveName(PathAction action, DungeonStepKind kind)
    {
        if (kind == DungeonStepKind.Interact)
        {
            // The argument is the data id, occasionally followed by a comment
            // ("1004346 (Goblin Pathfinder)"). Only the leading number counts.
            var raw = action.Arguments?.FirstOrDefault() ?? string.Empty;
            var head = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            if (uint.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out var dataId))
            {
                var name = _names.NameForDataId(dataId, ObjectKind.EventObj);
                if (!string.IsNullOrEmpty(name)) return name;
            }

            // No id, or an id the sheet does not name: fall back to whatever text
            // the file offers rather than announcing a nameless station.
            if (!string.IsNullOrWhiteSpace(action.Note)) return action.Note.Trim();
            if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();
        }

        // Boss, treasure and jump steps are named by their KIND, which the
        // browser speaks in the active language. A note, where one exists, adds
        // the detail the kind cannot carry.
        return string.IsNullOrWhiteSpace(action.Note) ? string.Empty : action.Note.Trim();
    }

    /// <summary>
    /// Reads the folder listing once.
    ///
    /// <para>
    /// SOME ZONES HAVE SEVERAL FILES - variants recorded for a different role.
    /// The plain file wins over a variant whose name carries a bracketed prefix:
    /// picking by role would need to know the player's job and would still be a
    /// guess, while the plain path is the one recorded for everyone.
    /// </para>
    /// </summary>
    private Dictionary<uint, string> ScanFolder()
    {
        var map = new Dictionary<uint, string>();
        var folder = PathFolder;

        if (!Directory.Exists(folder))
        {
            _log.Info($"[Dungeon] Kein Pfadordner unter '{folder}' - Kategorie bleibt aus.");
            return map;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.json");
        }
        catch (Exception ex)
        {
            _log.Error($"[Dungeon] Pfadordner '{folder}' nicht lesbar: {ex.Message}");
            return map;
        }

        var variants = 0;
        var plainClaimed = new HashSet<uint>();
        var branched = new List<uint>();

        foreach (var file in files)
        {
            var match = FileNamePattern.Match(Path.GetFileNameWithoutExtension(file));
            if (!match.Success) continue;
            if (!uint.TryParse(match.Groups["id"].Value, NumberStyles.None,
                               CultureInfo.InvariantCulture, out var territory)) continue;

            // A bracketed prefix marks a role variant ("[Tank W2W] ..."). Only
            // let one take the slot if nothing plain has claimed it.
            var isVariant = match.Groups["rest"].Value.StartsWith("「", StringComparison.Ordinal)
                            || match.Groups["rest"].Value.StartsWith("[", StringComparison.Ordinal);

            if (isVariant)
            {
                variants++;
                if (map.ContainsKey(territory)) continue;
                map[territory] = file;
                continue;
            }

            // TWO PLAIN FILES FOR ONE ZONE MEAN THE DUTY BRANCHES - a variant
            // dungeon where the route depends on which exit was taken. We cannot
            // know which branch the player chose, so one of them is used and the
            // rest are dropped. That is a real limitation and it is LOGGED rather
            // than swallowed: a route that quietly describes the wrong branch is
            // the worst outcome here, and the log is what makes it findable.
            if (!plainClaimed.Add(territory)) branched.Add(territory);

            map[territory] = file;
        }

        _log.Info($"[Dungeon] Pfadordner gelesen: {map.Count} Zonen aus {files.Length} Dateien " +
                  $"({variants} Rollen-Varianten).");

        foreach (var territory in branched.Distinct())
            _log.Warning($"[Dungeon] Zone {territory} hat mehrere gleichrangige Wege " +
                         $"(verzweigter Varianten-Dungeon). Es gilt '{Path.GetFileName(map[territory])}' - " +
                         "die anderen Zweige werden nicht angeboten.");

        return map;
    }

    /// <summary>One action row as it stands in the file. Only the fields we use
    /// are declared; the reader ignores the rest.</summary>
    private sealed class PathFile
    {
        public List<PathAction>? Actions { get; set; }
    }

    private sealed class PathAction
    {
        public string? Name { get; set; }
        public PathPosition Position { get; set; }
        public List<string>? Arguments { get; set; }
        public string? Note { get; set; }
    }

    private struct PathPosition
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}

/// <summary>What kind of station a dungeon step is. Decides the spoken wording,
/// so it is language independent exactly like <c>NavCategory</c>.</summary>
public enum DungeonStepKind
{
    /// <summary>A plain point on the way - announced by its number alone.</summary>
    Waypoint,

    /// <summary>Something to interact with: a door, a terminal, a lever.</summary>
    Interact,

    /// <summary>Where a boss is fought.</summary>
    Boss,

    /// <summary>A treasure coffer.</summary>
    Treasure,

    /// <summary>A spot that has to be jumped from.</summary>
    Jump,
}

/// <summary>
/// One station of a dungeon route.
/// </summary>
/// <param name="Number">Position in the route, counted from 1 over the stations
/// only - the player counts stations, not the control steps in between.</param>
/// <param name="Kind">What kind of station this is.</param>
/// <param name="Position">Full 3D world position; dungeons are stacked, so the
/// height matters here more than anywhere else.</param>
/// <param name="Name">Resolved name, or empty when the station is named by its
/// kind alone.</param>
public sealed record DungeonStep(int Number, DungeonStepKind Kind, Vector3 Position, string Name);
