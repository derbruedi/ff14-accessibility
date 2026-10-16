using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

internal sealed record QuestTeleportChoice(uint Id, uint MapId, string Name, int Hops, float Distance);

internal static class QuestTravelHints
{
    internal static DutyEntrance? FindDuty(IDataManager data, DutyEntranceService duties, PlacesService places,
        QuestDestination goal, uint currentMap)
    {
        var ids = data.GetExcelSheet<InstanceContent>().Where(i => i.ContentFinderCondition.ValueNullable?.TerritoryType.RowId == goal.TerritoryTypeId)
            .Select(i => i.RowId).ToHashSet();
        if (ids.Count == 0) return null;
        var distances = places.GetHopDistances();
        return duties.GetAll().Where(d => d.TargetBaseId != 0 && ids.Contains(d.ContentId))
            .OrderBy(d => distances.GetValueOrDefault(places.CanonicalMap(d.MapId), int.MaxValue))
            .ThenBy(d => d.MapId == currentMap ? 0 : 1).ThenBy(d => d.TargetBaseId).FirstOrDefault();
    }

    internal static QuestDestination DutyGoal(QuestDestination quest, DutyEntrance duty, uint currentMap)
        => quest with { QuestName = AccessibilityStrings.QuestDutyRoute(quest.QuestName, duty.Name),
            TerritoryTypeId = (ushort)duty.TerritoryTypeId, MapId = duty.MapId, Position = duty.Position,
            InCurrentZone = duty.MapId == currentMap, TargetBaseId = duty.TargetBaseId, TargetLevelType = 45, Radius = 1 };

    internal static QuestTeleportChoice? FindTeleport(IDataManager data, PlacesService places,
        QuestDestination goal, Func<uint, bool> unlocked)
    {
        var distances = places.GetHopDistancesTo(goal.MapId);
        var choices = new List<QuestTeleportChoice>();
        foreach (var a in data.GetExcelSheet<Aetheryte>())
        {
            if (!a.IsAetheryte || a.Invisible || a.Map.RowId == 0 || !unlocked(a.RowId)
                || !distances.TryGetValue(places.CanonicalMap(a.Map.RowId), out var hops)) continue;
            var name = RussianAuthorText.PlaceName(a.PlaceName.RowId, a.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(name)) continue;
            var distance = hops == 0 && a.Level[0].ValueNullable is { } level
                ? Vector3.Distance(new(level.X, level.Y, level.Z), goal.Position) : float.MaxValue;
            choices.Add(new(a.RowId, a.Map.RowId, name, hops, distance));
        }
        return Choose(choices);
    }

    internal static QuestTeleportChoice? Choose(IEnumerable<QuestTeleportChoice> choices)
        => choices.OrderBy(c => c.Hops).ThenBy(c => c.Distance).ThenBy(c => c.Id).FirstOrDefault();
}
