using System.Numerics;

namespace FF14Accessibility.Services;

internal enum AoeTurnResult { None, Turned, NoPath }

/// <summary>One turn per valid escape point, never continuous steering. All
/// inputs and callbacks are synchronous snapshots from the framework thread.</summary>
internal sealed class AoeAutoTurn
{
    private ulong _player;
    private uint _territory;
    private Vector3? _spot;
    private float _wantedCamera;
    private long _turnedAt;
    private long _nextSearch;
    private bool _suppressed;
    private bool _reportedNoPath;
    private int _nextCandidate;
    private Vector3 _searchPosition;
    private float _searchCamera;
    private DangerZone[] _searchZones = [];

    internal void Cancel() => _suppressed = true;

    internal void Reset()
    {
        _spot = null;
        _suppressed = false;
        _reportedNoPath = false;
        _nextSearch = 0;
        _nextCandidate = 0;
        _searchZones = [];
    }

    internal AoeTurnResult Update(ulong player, uint territory, bool enabled, bool allowControl,
        bool uncertain, Vector3 position, float? cameraFacing, IReadOnlyList<DangerZone> zones,
        long now, Func<Vector3, Vector3, IReadOnlyList<DangerZone>, Vector3?> checkPath,
        Func<Vector3, bool> turn)
    {
        if (player != _player || territory != _territory || !enabled || player == 0)
        {
            Reset();
            _player = player;
            _territory = territory;
        }
        if (!enabled || player == 0 || !AoeEscapePath.Finite(position)) return AoeTurnResult.None;
        if (zones.Any(z => !AoeEscapeGeometry.IsValid(z)))
        {
            _spot = null;
            _nextCandidate = 0;
            return AoeTurnResult.None;
        }
        if (!zones.Any(z => z.Contains(position)))
        {
            Reset();
            return AoeTurnResult.None;
        }

        // A suspended frame keeps the latch: returning from a menu or Alt-Tab
        // must not repeat a turn the player has already overridden.
        if (!allowControl || uncertain || cameraFacing is not { } camera || !float.IsFinite(camera))
            return AoeTurnResult.None;
        if (_spot is { } old && now - _turnedAt >= 200
            && MathF.Abs(MathF.IEEERemainder(camera - _wantedCamera, MathF.Tau)) > MathF.PI / 12)
            _suppressed = true;
        if (_suppressed) return AoeTurnResult.None;
        // A changing search origin or attack layout invalidates the candidate
        // cursor. Never skip directions using indices from another snapshot.
        if (!_searchZones.SequenceEqual(zones) || Vector3.DistanceSquared(position, _searchPosition) > 0.01f
            || MathF.Abs(MathF.IEEERemainder(camera - _searchCamera, MathF.Tau)) > 0.02f)
        {
            _nextCandidate = 0;
            _searchZones = zones.ToArray();
            _searchPosition = position;
            _searchCamera = camera;
            _nextSearch = 0;
        }
        if (now < _nextSearch) return AoeTurnResult.None;
        _nextSearch = now + 50;
        var checkedPaths = 0;
        if (_spot is { } target)
        {
            if (!zones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, target, 2f))
                && checkPath(position, target, zones) is { } verified && AoeEscapePath.Finite(verified)
                && Vector3.DistanceSquared(verified, target) <= 0.01f
                && !zones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, verified, 2f)))
            {
                _nextSearch = now + 250;
                return AoeTurnResult.None;
            }
            _spot = null;
            _nextCandidate = 0;
            checkedPaths++;
        }

        // Try the closest distances first. A point is accepted only after the
        // entire direct walk has been checked, including every other AoE.
        var candidateIndex = 0;
        for (var distance = 1; distance <= 25; distance++)
        for (var ray = 0; ray < 24; ray++)
        {
            var index = candidateIndex++;
            if (index < _nextCandidate) continue;
            _nextCandidate = candidateIndex;
            var angle = _searchCamera + ray * MathF.Tau / 24;
            var candidate = _searchPosition + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * distance;
            // Avoid accepting a mathematical boundary because a sine/cosine
            // rounding error puts it a few bits outside the margin.
            if (zones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, candidate, 2.05f))) continue;
            var safe = checkPath(position, candidate, zones);
            _nextCandidate = candidateIndex;
            checkedPaths++;
            if (safe is { } point && AoeEscapePath.Finite(point)
                && Vector2.DistanceSquared(new(point.X, point.Z), new(candidate.X, candidate.Z)) <= 0.01f
                && !zones.Any(z => AoeEscapeGeometry.ContainsWithMargin(z, point, 2.05f)) && turn(point))
            {
                _spot = point;
                _wantedCamera = MathF.Atan2(point.X - position.X, point.Z - position.Z);
                _turnedAt = now;
                _reportedNoPath = false;
                _nextCandidate = 0;
                return AoeTurnResult.Turned;
            }
            // Continue in the next frame: do not restart the same directions,
            // and do not announce failure while candidates remain untested.
            if (checkedPaths >= 2) return AoeTurnResult.None;
        }
        _nextCandidate = 0;
        return ReportNoPath();
    }

    internal Vector3? TurnedSpot => _spot;
    internal Vector3? GuidanceSpot => _suppressed ? null : _spot;

    private AoeTurnResult ReportNoPath()
    {
        if (_reportedNoPath) return AoeTurnResult.None;
        _reportedNoPath = true;
        return AoeTurnResult.NoPath;
    }

    internal static string? HandleCommand(string command, Configuration config, Action save)
    {
        var parts = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].ToLowerInvariant() is not ("aoeturn" or "автоповорот")) return null;
        if (parts.Length == 1) return AccessibilityStrings.AoeAutoTurnState(config.AutoTurnAoe);
        if (parts.Length != 2) return AccessibilityStrings.AoeAutoTurnUsage;
        bool? enabled = parts[1].ToLowerInvariant() switch
        {
            "on" or "вкл" => true,
            "off" or "выкл" => false,
            _ => null,
        };
        if (enabled == null) return AccessibilityStrings.AoeAutoTurnUsage;
        config.AutoTurnAoe = enabled.Value;
        save();
        return AccessibilityStrings.AoeAutoTurnState(enabled.Value);
    }
}
