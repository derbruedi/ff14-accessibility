using System.Numerics;

namespace FF14Accessibility.Services;

internal sealed record QuestNavigationPlan(Vector3 Position, string Name, bool IsBorder,
    bool HeightIsGuess, InteriorEntrance? Entrance, uint NextMapId)
{
    internal static QuestNavigationPlan? Resolve(QuestDestination quest, uint territory, uint map,
        Func<uint, PlaceDestination?> findHop, Func<uint, InteriorEntrance?> findEntrance,
        Func<uint, uint, bool>? sameMap = null)
    {
        if (quest.TerritoryTypeId == territory && (quest.MapId == 0 || (sameMap?.Invoke(quest.MapId, map) ?? quest.MapId == map)))
            return new(quest.Position, quest.QuestName, false, QuestAreaPoint.IsSearchArea(quest), null, 0);
        var hop = findHop(quest.MapId);
        if (hop == null) return null;
        var entrance = findEntrance(quest.MapId);
        return new(hop.Position, hop.Name, hop.IsZoneTransition && entrance == null,
            entrance == null, entrance, hop.TargetMapId);
    }

    internal static QuestDestination? Refresh(QuestDestination previous,
        IEnumerable<QuestDestination> current, uint territory, uint map, Vector3 player)
    {
        var matches = current.Where(d => previous.QuestId != 0 ? d.QuestId == previous.QuestId
            : previous.NativeMarkerId != 0 ? d.NativeMarkerId == previous.NativeMarkerId
            : d.QuestName == previous.QuestName).ToArray();
        // The same objective wins while it exists; don't jump between parallel
        // objectives just because the player crossed a border or moved closer.
        return matches.FirstOrDefault(d => previous.ObjectiveLevelId != 0 && d.ObjectiveLevelId == previous.ObjectiveLevelId)
            ?? matches.FirstOrDefault(d => d.MapId == previous.MapId && d.TargetBaseId == previous.TargetBaseId
                && d.TargetLevelType == previous.TargetLevelType && Vector3.Distance(d.Position, previous.Position) < 0.1f)
            ?? matches.OrderByDescending(d => d.TerritoryTypeId == territory && (d.MapId == 0 || d.MapId == map))
                .ThenBy(d => d.TerritoryTypeId == territory ? Vector3.Distance(player, d.Position) : float.MaxValue)
                .ThenBy(d => d.ObjectiveLevelId).FirstOrDefault();
    }
}
