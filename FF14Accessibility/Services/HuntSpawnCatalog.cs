using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace FF14Accessibility.Services;

/// <summary>
/// One community spawn for a hunting-log monster.
/// </summary>
/// <param name="MapX">Player-facing map coordinate, the same numbers a wiki shows.</param>
/// <param name="MapY">Player-facing map coordinate.</param>
/// <param name="WorldZ">World Z from the list, kept only to check the map conversion.</param>
public readonly record struct HuntSpawnPoint(float MapX, float MapY, float WorldZ);

/// <summary>
/// Spawn points for hunting-log monsters, read from a local copy of
/// LuminaSupplemental <c>MobSpawn.csv</c>.
///
/// <para>
/// The game client does not place open-world hunt mobs. Field layouts checked
/// on 2026-10-07 (territories 141 and 145) contain no battle NPCs, and
/// <see cref="HuntingLogService.GetSpawnPoints"/> joins Level type 9 to the
/// wrong sheet. This list is the source that carries the coordinates a wiki
/// shows. It is downloaded onto the player's machine and is not part of the
/// repository: the upstream file is GPL-3.
/// </para>
///
/// <para>
/// Columns, from MobSpawnPosition.FromCsv: BNpcBaseId, BNpcNameId,
/// TerritoryTypeId, Position "mapX;mapY;worldZ", Subtype. Map X/Y are the
/// player-facing coordinates. World Y (height) is absent. Subtype other than 0
/// is undocumented, so those rows are dropped. The sentinel
/// <c>21.48;21.48</c> with a negative Z is a placeholder, not a camp.
/// </para>
/// </summary>
public sealed class HuntSpawnCatalog
{
    public const string FolderName = "HuntSpawns";
    public const string FileName = "MobSpawn.csv";

    /// <summary>Pinned file measured 2026-10-08. The hash 138b2e1a… is the
    /// blob of MobSpawn.csv, not a commit, and raw.githubusercontent answers
    /// 404 for it (log 2026-10-08 09:56). This commit contains that same blob,
    /// 894039 bytes. A moving branch would change coordinates without a code
    /// change.</summary>
    public const string SourceUrl =
        "https://raw.githubusercontent.com/Critical-Impact/LuminaSupplemental/2a04329e5fb3ca6f22bc8d1915fe0d1382d59519/src/LuminaSupplemental.Excel/Generated/MobSpawn.csv";

    private static readonly IReadOnlyList<HuntSpawnPoint> None = Array.Empty<HuntSpawnPoint>();
    private static readonly float PlaceholderMap = float.Parse("21.48", CultureInfo.InvariantCulture);

    private Dictionary<(uint NameId, uint TerritoryId), List<HuntSpawnPoint>> _index = new();

    public int PointCount { get; private set; }

    /// <summary>Reads a CSV already on disk. A missing file leaves the catalog
    /// empty and returns false only when the file exists but cannot be used.</summary>
    public bool TryLoad(string path, out int points, out string? error)
    {
        points = 0;
        error = null;
        if (!File.Exists(path))
        {
            Replace(Parse(string.Empty));
            return true;
        }

        try
        {
            var parsed = Parse(File.ReadAllText(path));
            if (parsed.PointCount == 0)
            {
                error = "keine Standorte";
                Replace(Parse(string.Empty));
                return false;
            }

            Replace(parsed);
            points = PointCount;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Replace(Parse(string.Empty));
            return false;
        }
    }

    public static HuntSpawnCatalog Parse(string csv)
    {
        var catalog = new HuntSpawnCatalog();
        var index = new Dictionary<(uint, uint), List<HuntSpawnPoint>>();
        var seen = new HashSet<(uint Name, uint Territory, int X, int Y)>();
        var count = 0;

        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var cols = line.Split(',');
            if (cols.Length < 5) continue;
            if (!uint.TryParse(cols[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nameId))
                continue;
            if (!uint.TryParse(cols[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var territory))
                continue;
            if (!byte.TryParse(cols[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var subtype)
                || subtype != 0)
                continue;

            var pos = cols[3].Split(';');
            if (pos.Length < 3) continue;
            if (!float.TryParse(pos[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mapX)
                || !float.TryParse(pos[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mapY)
                || !float.TryParse(pos[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var worldZ))
                continue;
            if (IsPlaceholder(mapX, mapY, worldZ)) continue;

            var key = (nameId, territory, (int)MathF.Round(mapX * 100f), (int)MathF.Round(mapY * 100f));
            if (!seen.Add(key)) continue;

            if (!index.TryGetValue((nameId, territory), out var list))
                index[(nameId, territory)] = list = new List<HuntSpawnPoint>();
            list.Add(new HuntSpawnPoint(mapX, mapY, worldZ));
            count++;
        }

        catalog._index = index;
        catalog.PointCount = count;
        return catalog;
    }

    /// <summary>Points for this monster in this territory. A dungeon copy of the
    /// same name in another territory is a different key.</summary>
    public IReadOnlyList<HuntSpawnPoint> Find(uint bnpcNameId, uint territoryId)
    {
        if (bnpcNameId == 0 || territoryId == 0) return None;
        return _index.TryGetValue((bnpcNameId, territoryId), out var list) ? list : None;
    }

    /// <summary>
    /// Spawn points win when at least one converts. Otherwise the caller keeps
    /// the area pieces. The area function is not called when a spawn point exists.
    /// </summary>
    public static bool TrySelectParts(
        IReadOnlyList<HuntSpawnPoint> spawns,
        Func<float, float, Vector3?> toWorld,
        Vector3 sortFrom,
        Func<List<AreaPart>> areaParts,
        out List<AreaPart> parts,
        out bool fromSpawns,
        Action<HuntSpawnPoint, Vector3>? note = null)
    {
        var spawned = ToAreaParts(spawns, toWorld, sortFrom, note);
        if (spawned.Count > 0)
        {
            parts = spawned;
            fromSpawns = true;
            return true;
        }

        parts = areaParts();
        fromSpawns = false;
        return parts.Count > 0;
    }

    public static List<AreaPart> ToAreaParts(
        IReadOnlyList<HuntSpawnPoint> spawns,
        Func<float, float, Vector3?> toWorld,
        Vector3 sortFrom,
        Action<HuntSpawnPoint, Vector3>? note = null)
    {
        var parts = new List<AreaPart>(spawns.Count);
        foreach (var spawn in spawns)
        {
            if (toWorld(spawn.MapX, spawn.MapY) is not { } world) continue;
            note?.Invoke(spawn, world);
            parts.Add(new AreaPart(world, string.Empty, 0f));
        }

        parts.Sort((a, b) => Flat(sortFrom, a.Centre).CompareTo(Flat(sortFrom, b.Centre)));
        return parts;
    }

    private static bool IsPlaceholder(float mapX, float mapY, float worldZ) =>
        worldZ < 0f
        && MathF.Abs(mapX - PlaceholderMap) <= 0.001f
        && MathF.Abs(mapY - PlaceholderMap) <= 0.001f;

    private static float Flat(Vector3 from, Vector3 to)
    {
        var dx = from.X - to.X;
        var dz = from.Z - to.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private void Replace(HuntSpawnCatalog parsed)
    {
        _index = parsed._index;
        PointCount = parsed.PointCount;
    }
}
