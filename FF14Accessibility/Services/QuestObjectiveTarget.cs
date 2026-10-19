using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

// A map circle can cover several floors. Only an explicit listener of the
// current quest step can replace it with an actor or a walk-in volume.
internal sealed class QuestObjectiveTarget(IDataManager data)
{
    private Dictionary<uint, Level[]>? _actors;

    internal QuestDestination Resolve(QuestDestination area, byte? sequence)
    {
        if (!QuestAreaPoint.IsSearchArea(area) || area.QuestId == 0 || sequence is null or 0 or 255
            || !data.GetExcelSheet<Quest>().TryGetRow(area.QuestId, out var quest)) return area;
        _actors ??= data.GetExcelSheet<Level>().Where(l => l.Type is 8 or 45 && l.Object.RowId != 0)
            .GroupBy(l => l.Object.RowId).ToDictionary(g => g.Key, g => g.ToArray());
        var candidates = new List<QuestDestination>();
        foreach (var listener in quest.QuestListenerParams)
        {
            // Do not infer the meaning of arbitrary script conditions or use
            // actors from earlier/later steps merely because they are nearby.
            if (listener.ActorSpawnSeq != sequence.Value) continue;
            if (listener.Listener == 5000000 && listener.ConditionType == 3
                && data.GetExcelSheet<Level>().TryGetRow(listener.ConditionValue, out var trigger)
                && trigger.Type == 49 && trigger.EventId.RowId == area.QuestId)
                Add(trigger);
            else if (_actors.TryGetValue(listener.Listener, out var levels))
                foreach (var level in levels) Add(level);
        }
        var distinct = candidates.Distinct().ToArray();
        var triggers = distinct.Where(d => d.TargetLevelType == 49).ToArray();
        // Multiple simultaneous targets remain a search area; distance/order
        // cannot decide which objective the player intended.
        return triggers.Length == 1 ? triggers[0]
            : triggers.Length == 0 && distinct.Length == 1 ? distinct[0] : area;

        void Add(Level level)
        {
            var position = new Vector3(level.X, level.Y, level.Z);
            if (level.Territory.RowId != area.TerritoryTypeId || level.Map.RowId != area.MapId
                || level.Type == 49 && (!float.IsFinite(level.Radius) || level.Radius <= 0)
                || !TravelLayout.Finite(position) || MathF.Abs(position.Y - area.Position.Y) > 100
                || Vector2.Distance(new(position.X, position.Z), new(area.Position.X, area.Position.Z)) > area.Radius)
                return;
            candidates.Add(area with { Position = position, Radius = level.Type == 49 ? level.Radius : 1,
                TargetBaseId = level.Type == 49 ? 0 : level.Object.RowId, TargetLevelType = level.Type,
                AreaTargetLevelId = level.RowId });
        }
    }

    internal static Vector3? TriggerPoint(QuestDestination trigger, float stop,
        Func<Vector3, float, float, Vector3?> probe)
    {
        var usable = trigger.Radius - stop - MathF.Min(0.5f, trigger.Radius * 0.1f);
        if (!TravelLayout.Finite(trigger.Position) || !float.IsFinite(stop) || stop < 0
            || !float.IsFinite(usable) || usable <= 0) return null;
        var extent = MathF.Min(2.5f, usable);
        var point = probe(trigger.Position, extent, 2f);
        return point is { } p && TravelLayout.Finite(p) && MathF.Abs(p.Y - trigger.Position.Y) <= 2
            && Vector3.Distance(p, trigger.Position) <= usable ? p : null;
    }
}
