using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Announces the compass direction the player is FACING whenever they turn to a
/// new heading ("Osten"). A sighted player reads their facing off the minimap
/// arrow; a blind player cannot, so a settled turn speaks the new direction.
/// The rotation-to-compass conversion is <see cref="RouteService.HeadingSector"/>
/// (verified rotation convention).
///
/// Spam control is the whole difficulty here: the character's rotation changes
/// continuously while moving, so a naive "rotation changed" trigger would talk
/// non-stop. Two gates instead:
///   1. SETTLE - the heading is spoken only after the rotation has stopped
///      changing for a short moment, so sweeping through several sectors mid-turn
///      announces just the final one, not every sector crossed.
///   2. SECTOR CHANGE - it speaks only when the settled direction lands in a
///      different 8-sector compass slice than the one last spoken, so holding or
///      re-facing the same direction stays silent.
/// A toggle key (Configuration.KeyToggleHeading) turns the whole thing off when
/// the player wants quiet.
/// </summary>
public sealed class HeadingService
{
    private readonly IObjectTable  _objectTable;
    private readonly TolkService   _tolk;
    private readonly Configuration _config;
    private readonly IPluginLog    _log;

    // Last sector spoken (0..7); -1 = no baseline yet. The first reading only
    // records the baseline silently, so logging in already facing a direction
    // does not fire an announcement.
    private int   _lastSpokenSector = -1;
    private float _lastRotation;
    private bool  _hasBaseline;

    // Rotation counts as "moving" above this per-frame delta (radians). Below it
    // is jitter / a stopped character. ~0.008 rad = ~0.46 deg per frame.
    private const float  TurnDeadzone   = 0.008f;
    // The rotation must stay still this long after a turn before the heading is
    // spoken - long enough to skip intermediate sectors, short enough to feel
    // immediate.
    private const double SettleSeconds  = 0.15;

    private long _settleAtTick;

    public HeadingService(IObjectTable objectTable, TolkService tolk, Configuration config, IPluginLog log)
    {
        _objectTable = objectTable;
        _tolk        = tolk;
        _config      = config;
        _log         = log;
    }

    /// <summary>
    /// Drops the baseline so the next <see cref="Update"/> re-reads the current
    /// facing WITHOUT announcing it. Used by the toggle key: re-enabling the
    /// feature should not immediately echo the direction the player already
    /// hears from the toggle feedback itself.
    /// </summary>
    public void ResetBaseline() => _hasBaseline = false;

    /// <summary>The compass word the player currently faces, or empty when the
    /// player is unavailable. For the toggle key's spoken confirmation.</summary>
    public string CurrentHeadingWord()
    {
        var player = _objectTable.LocalPlayer;
        return player == null
            ? string.Empty
            : RouteService.SectorWord(RouteService.HeadingSector(player.Rotation));
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        if (!_config.AnnounceHeading) return;

        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            // Logged out / loading: drop state so returning does not announce.
            _hasBaseline = false;
            return;
        }

        var rotation = player.Rotation;

        // First frame with a player: remember the facing without speaking it.
        if (!_hasBaseline)
        {
            _lastRotation     = rotation;
            _lastSpokenSector = RouteService.HeadingSector(rotation);
            _hasBaseline      = true;
            return;
        }

        var delta = AngleDelta(rotation, _lastRotation);
        _lastRotation = rotation;

        var now = Stopwatch.GetTimestamp();
        if (delta > TurnDeadzone)
        {
            // Still turning: push the settle deadline out; stay silent for now.
            _settleAtTick = now + (long)(SettleSeconds * Stopwatch.Frequency);
            return;
        }

        // Rotation has been (essentially) still - wait until it has held long
        // enough, then announce only if the resting sector actually changed.
        if (now < _settleAtTick) return;

        var sector = RouteService.HeadingSector(rotation);
        if (sector == _lastSpokenSector) return;

        _lastSpokenSector = sector;
        _log.Info($"[Heading] rot={rotation:F2} -> Sektor {sector} ({RouteService.SectorWord(sector)})");
        _tolk.SpeakInterrupt(RouteService.SectorWord(sector) + ".");
    }

    /// <summary>Smallest absolute angle between two rotations, in radians.</summary>
    private static float AngleDelta(float a, float b)
    {
        var d = MathF.Abs(a - b) % (2f * MathF.PI);
        return d > MathF.PI ? 2f * MathF.PI - d : d;
    }
}
