using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

public sealed class NavigationService
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    // Aktuell verfolgtes Ziel
    private IGameObject? _trackedObject;
    private string? _trackedName;

    public NavigationService(
        IClientState clientState,
        IObjectTable objectTable,
        TolkService tolk,
        IPluginLog log)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _tolk = tolk;
        _log = log;
    }

    // Ziel per Name setzen (NPC oder Spielername)
    public bool SetTarget(string name)
    {
        var obj = _objectTable
            .FirstOrDefault(o => o.Name.TextValue.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (obj == null)
        {
            _tolk.SpeakInterrupt($"Ziel {name} nicht gefunden.");
            return false;
        }

        _trackedObject = obj;
        _trackedName = obj.Name.TextValue;
        _tolk.SpeakInterrupt($"Verfolge {_trackedName}.");
        return true;
    }

    // Aktuell anvisiertes Spielziel übernehmen
    public void SetTargetFromGameTarget()
    {
        var target = _objectTable.LocalPlayer?.TargetObject;
        if (target == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel anvisiert.");
            return;
        }

        _trackedObject = target;
        _trackedName = target.Name.TextValue;
        _tolk.SpeakInterrupt($"Verfolge {_trackedName}.");
    }

    public void ClearTarget()
    {
        _trackedObject = null;
        _trackedName = null;
        _tolk.SpeakInterrupt("Zielverfolgung beendet.");
    }

    // Auf Tastendruck: Richtung und Distanz ansagen
    public void AnnounceDirection()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        // Ziel aktualisieren falls es sich bewegt hat
        if (_trackedObject != null)
        {
            _trackedObject = _objectTable.FirstOrDefault(o => o.GameObjectId == _trackedObject.GameObjectId)
                             ?? _trackedObject;
        }

        if (_trackedObject == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel gesetzt. Drücke F7 zum Setzen.");
            return;
        }

        var playerPos = player.Position;
        var targetPos = _trackedObject.Position;
        var distance = Vector3.Distance(playerPos, targetPos);

        var direction = CalculateDirection(player, targetPos);
        var distanceText = FormatDistance(distance);

        _tolk.SpeakInterrupt($"{_trackedName}: {distanceText}, {direction}.");
    }

    // Alle nahen NPCs/Spieler auflisten
    public void AnnounceNearbyObjects(float maxDistance = 30f)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var nearby = _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && !string.IsNullOrWhiteSpace(o.Name.TextValue)
                        && Vector3.Distance(player.Position, o.Position) <= maxDistance)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .Take(5)
            .ToList();

        if (nearby.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Objekte in der Nähe.");
            return;
        }

        var parts = nearby.Select(o =>
        {
            var dist = Vector3.Distance(player.Position, o.Position);
            return $"{o.Name.TextValue} {FormatDistance(dist)}";
        });

        _tolk.SpeakInterrupt("In der Nähe: " + string.Join(", ", parts));
    }

    private string CalculateDirection(IGameObject player, Vector3 targetPos)
    {
        var playerPos = player.Position;
        var dx = targetPos.X - playerPos.X;
        var dz = targetPos.Z - playerPos.Z;

        // Winkel zur Nordrichtung (negatives Z = Norden in FF14)
        var angleToTarget = Math.Atan2(dx, -dz) * (180.0 / Math.PI);
        if (angleToTarget < 0) angleToTarget += 360;

        // Spieler-Blickrichtung (Rotation in Radiant)
        var playerFacing = player.Rotation * (180.0 / Math.PI);
        if (playerFacing < 0) playerFacing += 360;

        // Relativer Winkel: 0° = geradeaus, 90° = rechts, -90° = links
        var relativeAngle = angleToTarget - playerFacing;
        if (relativeAngle > 180) relativeAngle -= 360;
        if (relativeAngle < -180) relativeAngle += 360;

        return relativeAngle switch
        {
            < -135 => "hinter links",
            < -45  => "links",
            < -15  => "leicht links",
            <= 15  => "geradeaus",
            <= 45  => "leicht rechts",
            <= 135 => "rechts",
            _      => "hinter rechts"
        };
    }

    private static string FormatDistance(float distance) =>
        distance < 2f   ? "direkt neben dir" :
        distance < 10f  ? $"{distance:F0} Meter" :
        distance < 100f ? $"{distance:F0} Meter" :
                          $"{distance / 1000:F1} Kilometer";
}
