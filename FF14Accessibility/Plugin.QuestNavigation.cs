using System.Numerics;
using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed partial class Plugin
{
    private bool _resolvedCanTrackObject;
    // Quest routing sits above the preserved movement service. Every key press
    // reads the current objective and resolves only the next local leg.
    private MarkerResolve TryResolveSelectedDestination(out Vector3 position, out string name,
        out float stopRange, out bool heightIsGuess, out bool isZoneTransition, bool readout = false)
    {
        _resolvedNavigationObject = null;
        _resolvedCanTrackObject = false;
        if (!readout && !PrepareDungeonNavigation())
        {
            position = default; name = string.Empty; stopRange = 0;
            heightIsGuess = false; isZoneTransition = false;
            return MarkerResolve.Failed;
        }
        if (_navigation.SelectedDungeonStep is { } station)
        {
            heightIsGuess = false; isZoneTransition = false;
            var kind = AccessibilityStrings.DungeonStepKindWord(station.Kind);
            name = station.Name.Length > 0 ? station.Name
                : kind.Length > 0 ? kind : AccessibilityStrings.DungeonWaypointWord;
            stopRange = station.Kind == DungeonStepKind.Waypoint ? 1f : AutoWalkService.StopRange;
            var live = _navigation.GetSelectedNavigationObject();
            position = live?.Position ?? station.Position;
            if (readout) return MarkerResolve.Resolved;
            var nav = _autoWalk.Navmesh;
            var ready = nav.IsReady;
            Log.Info($"[DungeonNav] territory={ClientState.TerritoryType} map={ClientState.MapId} "
                + $"station={station.Number} data={station.DataId} object={live?.GameObjectId:X} "
                + $"player={ObjectTable.LocalPlayer?.Position} destination={position} meshReady={ready}");
            if (!ready)
            {
                _tolk.SpeakInterrupt(QuestMeshUnavailableMessage() ?? AccessibilityStrings.MeshNotReady);
                return MarkerResolve.Failed;
            }
            _resolvedNavigationObject = live;
            _resolvedCanTrackObject = live != null;
            if (live == null) position = DungeonNavigationPoint.Resolve(position, nav.NearestPoint);
            return MarkerResolve.Resolved;
        }
        if (_navigation.SelectedObjectDestination is { Kind: not Dalamud.Game.ClientState.Objects.Enums.ObjectKind.None } selected)
        {
            position = default; name = selected.Name; stopRange = AutoWalkService.StopRange;
            heightIsGuess = false; isZoneTransition = false;
            var live = SelectionObjectResolver.Exact(ObjectTable, selected);
            if (live == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.SelectedObjectMissing(name));
                return MarkerResolve.Failed;
            }
            _resolvedNavigationObject = live;
            _resolvedCanTrackObject = true;
            // Native movement and the author's ceiling/bridge handling need the
            // actor's real position, just as Num5 does, rather than a projection.
            position = live.Position;
            return MarkerResolve.Resolved;
        }
        if (_navigation.SelectedQuestDestination == null
            && _navigation.SelectedPlaceDestination is { IsZoneTransition: false, TypeLabel: "Ätheryt" or "Aethernet" })
            _resolvedCanTrackObject = true;
        if (_navigation.SelectedQuestDestination == null
            && _navigation.SelectedPlaceDestination is { IsZoneTransition: false, TypeLabel: "Ätheryt" or "Aethernet" }
            && _navigation.GetSelectedNavigationObject() is { } crystal)
        {
            _resolvedNavigationObject = crystal;
            _resolvedCanTrackObject = true;
            position = crystal.Position;
            name = _navigation.SelectedPlaceDestination.Name;
            stopRange = AutoWalkService.StopRange;
            heightIsGuess = false; isZoneTransition = false;
            return MarkerResolve.Resolved;
        }
        if (!readout && (_navigation.SelectedQuestDestination != null
            || _navigation.SelectedPlaceDestination is { IsZoneTransition: true })
            && QuestMeshUnavailableMessage() is { } unavailable)
        {
            _tolk.SpeakInterrupt(unavailable);
            position = default; name = string.Empty; stopRange = 0;
            heightIsGuess = false; isZoneTransition = false;
            return MarkerResolve.Failed;
        }
        if (readout && _navigation.SelectedQuestDestination == null && _navigation.SelectedPlaceDestination != null && CurrentWalkingSelection
            && (_autoWalk.IsActive || _navigation.IsWalkGuideActive || _transitions.IsActive) && _walkingPoint is { } active)
        {
            position = active.Position; name = active.Name; stopRange = active.Stop;
            heightIsGuess = false; isZoneTransition = active.Transition;
            return MarkerResolve.Resolved;
        }
        if (!readout && _navigation.SelectedQuestDestination == null
            && _navigation.SelectedPlaceDestination is { IsZoneTransition: true } place)
        {
            position = default; name = place.Name; stopRange = _config.AutoWalkTransitionStopRange;
            heightIsGuess = true; isZoneTransition = false;
            var hop = _places.FindFirstHopToMap(place.TargetMapId, out _);
            if (hop == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.NavigationPathUnavailable(name));
                return MarkerResolve.Failed;
            }
            name = hop.Name;
            if (_places.FindLocalEntranceOnRoute(place.TargetMapId) is { } entrance)
            {
                _resolvedCanTrackObject = true;
                _resolvedNavigationObject = _navigation.GetSelectedNavigationObject();
                var entrancePoint = _resolvedNavigationObject?.Position ?? entrance.Position;
                var floor = SelectionObjectResolver.Approach(entrancePoint, _autoWalk.ProbeReachable);
                if (floor == null)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointAt(name));
                    return MarkerResolve.Failed;
                }
                position = floor.Value; stopRange = AutoWalkService.StopRange; heightIsGuess = false;
                return MarkerResolve.Resolved;
            }
            var border = ResolveWalkingBorder(hop.TargetMapId, hop.Position);
            if (border.HasBorder)
            {
                if (border.Position is not { } target)
                {
                    _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointNear(name));
                    return MarkerResolve.Failed;
                }
                position = target; isZoneTransition = true;
                return MarkerResolve.Resolved;
            }
            _tolk.SpeakInterrupt(AccessibilityStrings.NavigationPathUnavailable(name));
            return MarkerResolve.Failed;
        }
        if (_navigation.SelectedQuestDestination is not { } selection
            || selection.QuestId == 0 && selection.NativeMarkerId == 0 && !QuestAreaPoint.IsSearchArea(selection))
            return readout
                ? TryResolveDestinationReadout(out position, out name, out stopRange, out heightIsGuess, out isZoneTransition)
                : TryResolveMarkerDestination(out position, out name, out stopRange, out heightIsGuess, out isZoneTransition);
        position = default; name = string.Empty; stopRange = _config.AutoWalkPlaceStopRange;
        heightIsGuess = false; isZoneTransition = false;
        if (!_navigation.RefreshSelectedQuest())
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.QuestGoalUpdating);
            return MarkerResolve.Failed;
        }
        var quest = _navigation.GetSelectedQuestTravelGoal()!;
        var plan = QuestNavigationPlan.Resolve(quest, ClientState.TerritoryType, ClientState.MapId,
            id => _places.FindFirstHopToMap(id, out _), _places.FindLocalEntranceOnRoute, _places.AreSameMap);
        if (plan == null)
        {
            var duty = FindQuestDutyFinder(quest);
            var teleport = FindQuestTeleport(quest);
            _tolk.SpeakInterrupt(duty != null ? AccessibilityStrings.QuestDutyFinderRoute(quest.QuestName, duty)
                : teleport != null
                ? AccessibilityStrings.QuestTeleportRoute(quest.QuestName, teleport.Name, _places.GetMapName(quest.MapId))
                : AccessibilityStrings.QuestTravelUnavailable(quest.QuestName, _places.GetMapName(quest.MapId)));
            return MarkerResolve.Failed;
        }
        name = plan.Name;
        heightIsGuess = plan.HeightIsGuess;
        isZoneTransition = plan.IsBorder;
        stopRange = plan.IsBorder ? _config.AutoWalkTransitionStopRange
            : plan.Entrance != null ? _config.AutoWalkPlaceStopRange
            : quest.Role == QuestMarkerRole.QuestTrigger ? _config.AutoWalkPlaceStopRange
            : MathF.Max(_config.AutoWalkPlaceStopRange, MathF.Min(5f, quest.Radius));
        _resolvedCanTrackObject = plan.Entrance != null || plan.NextMapId == 0 && quest.TargetLevelType is 8 or 9 or 45;
        var liveObject = _resolvedCanTrackObject ? _navigation.GetSelectedNavigationObject() : null;
        _resolvedNavigationObject = liveObject;
        var point = liveObject?.Position ?? plan.Position;
        if (readout && liveObject == null && CurrentWalkingSelection
            && (_autoWalk.IsActive || _navigation.IsWalkGuideActive || _transitions.IsActive) && _walkingPoint is { } activeQuest)
        {
            position = activeQuest.Position; stopRange = activeQuest.Stop;
            heightIsGuess = false; isZoneTransition = activeQuest.Transition;
            return MarkerResolve.Resolved;
        }
        if (liveObject != null) { stopRange = AutoWalkService.StopRange; heightIsGuess = false; }
        if (liveObject != null) { position = point; return MarkerResolve.Resolved; }
        if (plan.HeightIsGuess && plan.NextMapId != 0)
            point = point with { Y = ObjectTable.LocalPlayer?.Position.Y ?? point.Y };
        if (!readout)
        {
            if (plan.IsBorder)
            {
                var border = ResolveWalkingBorder(plan.NextMapId, point);
                if (border.HasBorder)
                {
                    if (border.Position is not { } target)
                    {
                        _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointNear(name));
                        return MarkerResolve.Failed;
                    }
                    position = target;
                    return MarkerResolve.Resolved;
                }
                isZoneTransition = false;
            }
            var actorApproach = plan.Entrance != null || plan.NextMapId == 0 && quest.TargetLevelType is 8 or 9 or 45;
            var floor = actorApproach
                ? SelectionObjectResolver.Approach(point, _autoWalk.ProbeReachable)
                : ResolveQuestGroundPoint(quest, plan, point, ref stopRange);
            if (actorApproach)
                Log.Info($"[QuestRoute] Approach quest={quest.QuestId}, marker={quest.NativeMarkerId}, level={quest.ObjectiveLevelId}, "
                    + $"map={ClientState.MapId}, target={point}, floor={floor?.ToString() ?? "unresolved"}");
            if (floor == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.NoWalkablePointAt(name));
                return MarkerResolve.Failed;
            }
            point = floor.Value;
        }
        position = point;
        return MarkerResolve.Resolved;
    }

    private string? QuestMeshUnavailableMessage()
    {
        var nav = _autoWalk.Navmesh;
        if (nav.IsReady) return null;
        var progress = nav.BuildProgress;
        return nav.LastCallFailed ? AccessibilityStrings.AutoWalkUnavailable
            : progress >= 0 ? AccessibilityStrings.MeshStillLoading(progress * 100)
            : AccessibilityStrings.MeshNotReady;
    }

    private Vector3? ResolveQuestGroundPoint(QuestDestination quest, QuestNavigationPlan plan,
        Vector3 point, ref float stopRange)
    {
        if (plan.NextMapId == 0 && QuestAreaPoint.IsSearchArea(quest))
        {
            stopRange = MathF.Min(stopRange, quest.Radius * 0.25f);
            var area = QuestAreaPoint.Resolve(quest.Position, quest.Radius, stopRange,
                _autoWalk.ProbeReachable, candidate => _places.MatchesKnownMap(candidate,
                    quest.MapId != 0 ? quest.MapId : ClientState.MapId));
            Log.Info($"[QuestRoute] Area quest={quest.QuestId}, level={quest.ObjectiveLevelId}, map={quest.MapId}, "
                + $"centre={quest.Position}, radius={quest.Radius}, stop={stopRange}, floor={area?.ToString() ?? "unresolved"}");
            return area;
        }
        var floor = _autoWalk.ResolveReachablePoint(point) ?? _autoWalk.ResolveFloorPoint(point);
        // Exact actors and triggers retain their floor, even if a nearer
        // polygon is on the ceiling or a different interior level.
        return floor is { } found && TravelLayout.Finite(found)
            && (plan.HeightIsGuess || MathF.Abs(found.Y - point.Y) <= 5f) ? floor : null;
    }

    private BorderResolution ResolveWalkingBorder(uint map, Vector3 fallback)
    {
        var from = ObjectTable.LocalPlayer?.Position ?? fallback;
        return _zoneBorders.Resolve(map, from, _autoWalk.ProbeReachable, _autoWalk.ResolveFloorPoint,
            goal => _bridges.FindCrossing(from, goal, out _, out _) != null);
    }

    private unsafe QuestTeleportChoice? FindQuestTeleport(QuestDestination quest)
    {
        var state = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
        if (state == null || !ClientState.IsLoggedIn) return null;
        try { return QuestTravelHints.FindTeleport(DataManager, _places, quest, id => state->IsAetheryteUnlocked(id)); }
        catch (Exception ex) { Log.Warning($"[QuestRoute] Teleport availability: {ex.Message}"); return null; }
    }

    private unsafe string? FindQuestDutyFinder(QuestDestination quest)
    {
        var state = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.Instance();
        if (state == null || !ClientState.IsLoggedIn) return null;
        var choices = DataManager.GetExcelSheet<Lumina.Excel.Sheets.InstanceContent>()
            .Where(i => i.ContentFinderCondition.ValueNullable?.TerritoryType.RowId == quest.TerritoryTypeId).ToArray();
        if (choices.Length != 1) return null;
        try
        {
            if (!FFXIVClientStructs.FFXIV.Client.Game.UI.UIState.IsInstanceContentUnlocked(choices[0].RowId)) return null;
            return choices[0].ContentFinderCondition.ValueNullable?.Name.ExtractText();
        }
        catch (Exception ex) { Log.Warning($"[QuestRoute] Duty availability: {ex.Message}"); return null; }
    }

    private long _nextQuestRefresh;
    private void PollSelectedQuest()
    {
        if (_navigation.SelectedQuestDestination is not { } previous || previous.QuestId == 0 && previous.NativeMarkerId == 0) return;
        var tick = System.Diagnostics.Stopwatch.GetTimestamp();
        if (tick < _nextQuestRefresh) return;
        _nextQuestRefresh = tick + System.Diagnostics.Stopwatch.Frequency / 2;
        var refreshed = _navigation.RefreshSelectedQuest();
        var current = _navigation.SelectedQuestDestination;
        if (!refreshed || current?.ObjectiveLevelId != previous.ObjectiveLevelId || current?.MapId != previous.MapId
            || current?.Position != previous.Position || current?.TargetBaseId != previous.TargetBaseId
            || current?.TargetLevelType != previous.TargetLevelType)
        {
            _autoWalk.StopQuiet();
            _navigation.StopWalkGuideQuiet();
            _transitions.Stop(silent: true);
        }
    }
}
