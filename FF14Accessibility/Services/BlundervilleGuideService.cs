using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Spoken course guide for the Fall Guys duty (territory 1165). The beacon
/// and Numpad 5 share one near point on the measured line, so the tone sits
/// in the center while the player faces that line and leaves the center when
/// they drift. Speech still names the real next mark. Numpad 5 turns the
/// character and the camera. The plugin does not walk or jump: the courses
/// are timed obstacle runs, and the zone has no terrain navmesh
/// (<c>list.pcb</c> is absent). Side gaps around obstacles are not in the
/// layout, so the lock stays on the measured floor line.
/// </summary>
public sealed class BlundervilleGuideService
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objects;
    private readonly TolkService _tolk;
    private readonly BeaconService _beacon;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    private BlundervillePlace _place = BlundervillePlace.None;
    private BlundervilleNote _note = BlundervilleNote.None;
    private bool _atTargetSpoken;
    private bool _fieldsSpoken;
    private Vector3 _lastPosition;
    private DateTime _nextSpeak = DateTime.MinValue;

    public BlundervilleGuideService(
        IClientState clientState,
        IObjectTable objects,
        TolkService tolk,
        BeaconService beacon,
        Configuration config,
        IPluginLog log)
    {
        _clientState = clientState;
        _objects = objects;
        _tolk = tolk;
        _beacon = beacon;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// True while this guide wants the beacon. Navigation must not close it
    /// on that frame; this service feeds it after <c>NavigationService.Update</c>.
    /// </summary>
    public bool WantsBeacon
    {
        get
        {
            if (!_config.TargetBeaconEnabled) return false;
            if (_clientState.TerritoryType != BlundervilleCourse.TerritoryId) return false;
            var player = _objects.LocalPlayer;
            if (player == null) return false;
            return BlundervilleCourse.IsCourse(BlundervilleCourse.Identify(player.Position));
        }
    }

    /// <summary>
    /// One framework tick. <paramref name="yieldBeacon"/> is set when the walk
    /// guide, auto-walk, or an AoE escape already owns the tone.
    /// </summary>
    public void Update(bool yieldBeacon)
    {
        if (_clientState.TerritoryType != BlundervilleCourse.TerritoryId)
        {
            _place = BlundervillePlace.None;
            return;
        }

        if (yieldBeacon) return;

        var player = _objects.LocalPlayer;
        if (player == null) return;

        var step = BlundervilleCourse.Guide(player.Position);
        if (step.Place == BlundervillePlace.None) return;

        // A new round of the same course teleports the player back to the start.
        // A launch on the course itself stays well under this.
        if (step.Place == _place
            && BlundervilleCourse.IsCourse(step.Place)
            && Vector3.Distance(_lastPosition, player.Position) > 40f)
            _place = BlundervillePlace.None;
        _lastPosition = player.Position;

        var now = DateTime.UtcNow;
        if (step.Place != _place)
        {
            _place = step.Place;
            _note = step.Note;
            _atTargetSpoken = false;
            _fieldsSpoken = false;
            _tolk.SpeakInterrupt(AccessibilityStrings.BlundervilleEntered(step.Place));
            _log.Info($"[Stolperville] {step.Place} bei ({player.Position.X:F1}|{player.Position.Y:F1}|{player.Position.Z:F1}).");
            if (BlundervilleCourse.IsCourse(step.Place))
                LogNearby(player);
            _nextSpeak = now.AddSeconds(4);
        }

        if (!BlundervilleCourse.IsCourse(step.Place)) return;
        if (!BlundervilleCourse.TryFixedTarget(step, player.Position, out var steer)) return;

        var steerX = steer.X - player.Position.X;
        var steerZ = steer.Z - player.Position.Z;
        var steerFlat = MathF.Sqrt(steerX * steerX + steerZ * steerZ);
        // A point under the player's feet has no direction. Center means "you are on it".
        var angle = steerFlat < 0.05f ? 0d : NavigationService.RelativeAngle(player, steer);

        _beacon.Start();
        _beacon.Update(
            angle,
            steerFlat,
            BeaconKind.Quest,
            arrived: step.AtTarget,
            targetKey: step.BeaconKey);

        if (step.AtTarget)
        {
            if (_atTargetSpoken) return;
            _atTargetSpoken = true;
            _tolk.Speak(AccessibilityStrings.BlundervilleReached(step.Place, step.Note));
            if (step.Place == BlundervillePlace.Siege && step.Note == BlundervilleNote.Middle && !_fieldsSpoken)
            {
                _fieldsSpoken = true;
                _tolk.Speak(FieldList(player.Position));
            }
            return;
        }

        // A step off the point should not repeat "Ziel" or "Mitte" immediately.
        if (_atTargetSpoken && step.Distance < 8f) return;
        _atTargetSpoken = false;
        var hazardChanged = IsHazard(step.Note) && step.Note != _note;
        if (!hazardChanged && now < _nextSpeak) return;

        _note = step.Note;
        _nextSpeak = now.AddSeconds(4);
        var compass = RouteService.CompassAdjective(player.Position, step.Target);
        _tolk.Speak(AccessibilityStrings.BlundervilleAhead(
            step.Distance, compass, hazardChanged ? step.Note : BlundervilleNote.None));
    }

    /// <summary>
    /// Turns character and camera onto the course lock. True when this zone's
    /// course owns Numpad 5, including when the player is already on the point.
    /// False outside a course, so the walk guide and the game target keep the key.
    /// Does not start a walk.
    /// </summary>
    public bool Align()
    {
        if (_clientState.TerritoryType != BlundervilleCourse.TerritoryId) return false;
        var player = _objects.LocalPlayer;
        if (player == null) return false;

        var step = BlundervilleCourse.Guide(player.Position);
        if (!BlundervilleCourse.TryFixedTarget(step, player.Position, out var point)) return false;

        if (FacingService.FaceTowards(player, point) == null)
        {
            _log.Info("[Stolperville] Spieler steht auf dem Streckenpunkt.");
            _tolk.SpeakInterrupt(AccessibilityStrings.BlundervilleOnPoint);
            return true;
        }

        _log.Info($"[Stolperville] Fixiert auf ({point.X:F1}|{point.Y:F1}|{point.Z:F1}), " +
                  $"Strecke {step.Distance:F0}m, {step.Note}.");
        _tolk.SpeakInterrupt(AccessibilityStrings.BlundervilleLocked(step.Distance, step.Note));
        return true;
    }

    private void LogNearby(IGameObject player)
    {
        var found = new List<(float Distance, string Line)>();
        foreach (var obj in _objects)
        {
            if (obj == null || obj.GameObjectId == player.GameObjectId) continue;
            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) continue;
            var distance = Vector3.Distance(player.Position, obj.Position);
            if (distance > 25f) continue;
            found.Add((distance, $"{distance:F0}m {name} base={obj.BaseId}"));
        }

        var nearest = found.OrderBy(item => item.Distance).Take(8).Select(item => item.Line);
        _log.Info(found.Count == 0
            ? "[Stolperville] Keine benannten Objekte in 25 Metern."
            : "[Stolperville] In der Nähe: " + string.Join("; ", nearest));
    }

    private static string FieldList(Vector3 player)
    {
        static string One(Vector3 from, Vector3 to)
            => AccessibilityStrings.FormatDistance(Vector3.Distance(from, to))
               + " " + RouteService.CompassAdjective(from, to);

        return AccessibilityStrings.BlundervilleFields(
            One(player, BlundervilleCourse.SiegeEast),
            One(player, BlundervilleCourse.SiegeWest),
            One(player, BlundervilleCourse.SiegeSouth));
    }

    private static bool IsHazard(BlundervilleNote note) => note is
        BlundervilleNote.Wind or BlundervilleNote.Swing or BlundervilleNote.Bridge
        or BlundervilleNote.RollingBar or BlundervilleNote.Pendulum
        or BlundervilleNote.Seesaw or BlundervilleNote.RollingBall;
}
