using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FF14Accessibility.Services;

namespace FF14Accessibility;

public sealed partial class Plugin
{
    private IGameObject? _resolvedNavigationObject;
    private ObjectDestination? _walkingObject;
    private QuestDestination? _walkingQuestSelection;
    private ObjectDestination? _walkingBrowserSelection;
    private uint _walkingObjectTerritory;
    private uint _walkingObjectMap;
    private bool _awaitingQuestObject;
    private bool _needsObjectTracking;
    private (Vector3 Position, string Name, float Stop, bool Transition)? _walkingPoint;
    private PlaceDestination? _walkingPlaceSelection;

    private bool CurrentWalkingSelection => ClientState.MapId == _walkingObjectMap
        && ClientState.TerritoryType == _walkingObjectTerritory
        && _walkingQuestSelection == _navigation.SelectedQuestDestination
        && _walkingBrowserSelection == _navigation.SelectedObjectDestination
        && _walkingPlaceSelection == _navigation.SelectedPlaceDestination;

    private void RememberWalkingPoint(Vector3 position, string name, float stop, bool transition)
    {
        _walkingPoint = (position, name, stop, transition);
        _walkingObject = null;
        _needsObjectTracking = false;
        _awaitingQuestObject = _resolvedCanTrackObject;
        _walkingQuestSelection = _navigation.SelectedQuestDestination;
        _walkingBrowserSelection = _navigation.SelectedObjectDestination;
        _walkingPlaceSelection = _navigation.SelectedPlaceDestination;
        _walkingObjectTerritory = ClientState.TerritoryType;
        _walkingObjectMap = ClientState.MapId;
    }

    private void StartResolvedWalk(Vector3 position, string name, float stop, bool transition)
    {
        var stopping = _autoWalk.IsActive;
        // Bind the live instance in the first native request. Restarting that
        // request on the next frame discarded the author's selected bridge.
        _autoWalk.ToggleToPosition(position, name, stop, transition,
            _resolvedNavigationObject?.GameObjectId ?? 0);
        if (!stopping && _autoWalk.IsActive) RememberWalkingPoint(position, name, stop, transition);
        if (!stopping && _autoWalk.IsActive && _resolvedNavigationObject is { } live)
        {
            // Use the author's existing object tracking, including its route
            // updates, even when the native hard-target filter rejected this pick.
            RememberWalkingObject(live, name);
        }
    }

    private void StartResolvedGuide(Vector3 position, string name, float stop)
    {
        RememberWalkingPoint(position, name, stop, false);
        if (_resolvedNavigationObject is { } live)
        {
            _navigation.StartWalkGuideToObject(live, name);
            RememberWalkingObject(live, name);
        }
        else _navigation.StartWalkGuideToPosition(position, name, stop);
    }

    private void RememberWalkingObject(IGameObject live, string name)
    {
        _walkingObject = new(live.GameObjectId, name, live.Position, live.ObjectKind, live.BaseId);
        _awaitingQuestObject = false;
        _walkingQuestSelection = _navigation.SelectedQuestDestination;
        _walkingBrowserSelection = _navigation.SelectedObjectDestination;
        _walkingObjectTerritory = ClientState.TerritoryType;
        _walkingObjectMap = ClientState.MapId;
    }

    private void PollWalkingObject()
    {
        if (_awaitingQuestObject)
        {
            if (!CurrentWalkingSelection || ObjectTable.LocalPlayer == null
                || !_autoWalk.IsActive && !_navigation.IsWalkGuideActive) _awaitingQuestObject = false;
            else if (_navigation.GetSelectedNavigationObject() is { } appeared && _walkingPoint is { } point)
            {
                if (_navigation.IsWalkGuideActive) _navigation.RetargetWalkGuideToObject(appeared);
                RememberWalkingObject(appeared, point.Name);
                _needsObjectTracking = _autoWalk.IsActive;
            }
        }
        if (_walkingObject is not { } selected) return;
        if (!_autoWalk.IsActive && !_navigation.IsWalkGuideActive) { _walkingObject = null; return; }
        var changed = !CurrentWalkingSelection;
        var loading = ObjectTable.LocalPlayer == null || ClientState.TerritoryType != _walkingObjectTerritory
            || ClientState.MapId != _walkingObjectMap;
        if (!changed && !loading && SelectionObjectResolver.Exact(ObjectTable, selected) is { } live)
        {
            // Do not submit another SimpleMove query while the first one is
            // pending: Stop does not establish that its async search is over.
            if ((_needsObjectTracking || !_autoWalk.IsTrackingObject) && _autoWalk.CanRetargetObject
                && _autoWalk.IsActive && _autoWalk.Navmesh.IsRunning
                && !_autoWalk.Navmesh.PathfindInProgress)
            {
                _autoWalk.RetargetToObject(live, selected.Name);
                _needsObjectTracking = false;
            }
            return;
        }
        _autoWalk.StopQuiet();
        _navigation.StopWalkGuideQuiet();
        _walkingObject = null;
        _needsObjectTracking = false;
        if (!changed && !loading) _tolk.SpeakInterrupt(AccessibilityStrings.SelectedObjectMissing(selected.Name));
    }
}
