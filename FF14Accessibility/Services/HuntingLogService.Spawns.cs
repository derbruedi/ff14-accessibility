using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

public sealed partial class HuntingLogService
{
    private readonly record struct SpawnRow(uint Territory, byte Type, uint Id, string Name,
                                             float X, float Y, float Z, float Radius);
    private Dictionary<string, List<SpawnRow>>? _spawnsByName;

    public List<Vector3> GetSpawnPoints(string monsterName, uint territory, Vector3 from)
    {
        if (monsterName.Length == 0 || territory == 0) return [];
        if (!GetSpawnIndex().TryGetValue(monsterName, out var rows)) return [];
        return rows.Where(row => row.Territory == territory)
            .Select(row => new Vector3(row.X, row.Y, row.Z))
            .OrderBy(point => Vector3.Distance(from, point)).ToList();
    }

    private Dictionary<string, List<SpawnRow>> GetSpawnIndex()
    {
        if (_spawnsByName != null) return _spawnsByName;
        var index = new Dictionary<string, List<SpawnRow>>(StringComparer.OrdinalIgnoreCase);
        var names = _data.GetExcelSheet<BNpcName>();
        var cache = new Dictionary<uint, string>();
        foreach (var level in _data.GetExcelSheet<Level>())
        {
            if (level.Type != 9 || level.Object.RowId == 0) continue;
            var id = level.Object.RowId;
            if (!cache.TryGetValue(id, out var name))
            {
                name = names.TryGetRow(id, out var nameRow) ? ResolveMonsterName(nameRow) : string.Empty;
                cache[id] = name;
            }
            if (name.Length == 0) continue;
            if (!index.TryGetValue(name, out var rows)) index[name] = rows = [];
            rows.Add(new SpawnRow(level.Territory.RowId, level.Type, id, name,
                                  level.X, level.Y, level.Z, level.Radius));
        }
        _log.Info($"[Jagd] Level-Blatt: {index.Count} Monster-Namen mit Standorten.");
        _spawnsByName = index;
        return index;
    }

    public int DumpSpawnProbe()
    {
        var current = _clientState.TerritoryType;
        var targets = GetOpenTargets();
        targets.AddRange(GetOpenGrandCompanyTargets());
        var index = GetSpawnIndex();
        var output = new StringBuilder();
        output.AppendLine($"FF14 Accessibility - Jagd-Standorte, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        output.AppendLine($"Territorium={current}, Karte={_clientState.MapId}");
        output.AppendLine($"Monster-Namen mit Koordinaten={index.Count}");
        foreach (var target in targets)
        {
            output.AppendLine($"{target.MonsterName}: {target.Killed}/{target.Required}, " +
                              $"{target.ZoneName}, {target.AreaName}, Territorium={target.TerritoryId}");
            if (!index.TryGetValue(target.MonsterName, out var rows)) continue;
            foreach (var row in rows.Where(row => row.Territory == target.TerritoryId).Take(12))
                output.AppendLine($"  Welt={row.X:F1}|{row.Y:F1}|{row.Z:F1}, Radius={row.Radius:F1}");
        }
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FFXIV_Spawn.txt");
            File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            _log.Info("[JagdSonde] Spawn-Sonde geschrieben: " + path);
            return output.ToString().Count(ch => ch == '\n');
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[JagdSonde] Spawn-Sonde nicht schreibbar");
            return 0;
        }
    }
}
