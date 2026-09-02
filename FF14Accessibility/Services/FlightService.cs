using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace FF14Accessibility.Services;

/// <summary>
/// Everything the auto-walk needs to know about FLYING, and nothing else: is the
/// character airborne, does this territory have air routes at all, and get off the
/// mount when it is time to land.
///
/// <para>
/// WHAT THIS SERVICE DELIBERATELY DOES NOT DO: summon a mount or take off. The
/// player does both themselves (user's call, 2026-09-01), so that anybody who
/// cannot fly yet never notices this code exists. See
/// <c>AutoWalkService.ShouldFly</c>.
/// </para>
///
/// <para>
/// WHY IT IS A SERVICE OF ITS OWN. Flying is not a mode of walking - it is a
/// different search space in vnavmesh (voxel volume instead of walkable surface,
/// see <see cref="NavmeshIpc.MoveCloseTo"/>), and whether that space exists is a
/// question about the TERRITORY, while being airborne is a question about the
/// CHARACTER. Both belong together and neither belongs in the walk logic.
/// </para>
///
/// <para>
/// THE ONE HARD GATE: does a flight volume exist? vnavmesh only builds one when
/// <c>TerritoryType.TerritoryIntendedUse</c> is 1 (normal outdoor), 47 (Diadem) or
/// 49 (island) - <c>NavmeshCustomization.IsFlyingSupported</c>, read from the
/// cloned source at H:\ffxiv_navmesh (2026-09-01). Anywhere else
/// <c>PathfindVolume</c> logs "Nav volume was not built" and hands back an empty
/// list, which would reach the player as a walk that silently never starts.
/// </para>
///
/// <para>
/// The aether-current check below is INFORMATION ONLY - it answers "why can I not
/// fly here" for <c>/acc fly</c> and steers nothing. That matters, because its
/// behaviour in the base game's zones has never been measured: they all share set
/// 19 yet have no currents to collect, the unlock hangs off the main story
/// instead. A character who is airborne has answered the question by being
/// airborne, so the walk logic never needs to ask.
/// </para>
/// </summary>
public sealed class FlightService
{
    /// <summary>
    /// "Absteigen" - GeneralAction row 23 (sheet dump 2026-09-01).
    ///
    /// <para>MEASURED 2026-09-01: this does NOTHING while airborne. The game
    /// reports it as usable (<c>GetActionStatus</c> = 0), accepts it
    /// (<c>UseAction</c> = true) and leaves the character mounted - twelve calls in
    /// a row in the log. Landing in FFXIV means flying DOWN until the character
    /// sets down; only then does this action work. See
    /// <c>AutoWalkService.Descend</c>.</para>
    /// </summary>
    private const uint Dismount = 23;

    /// <summary>Default target id of <c>ActionManager.UseAction</c>, meaning "no
    /// target". The general actions here all act on the player.</summary>
    private const ulong NoTarget = 0xE0000000;

    /// <summary>Don't fire the dismount every frame - the caller asks once per
    /// frame until the mount is gone, and the action has an animation.</summary>
    private static readonly TimeSpan DismountRetryInterval = TimeSpan.FromMilliseconds(500);

    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    private DateTime _lastDismountAttempt = DateTime.MinValue;

    public FlightService(IClientState clientState, ICondition condition,
                         IDataManager data, IPluginLog log)
    {
        _clientState = clientState;
        _condition = condition;
        _data = data;
        _log = log;
    }

    /// <summary>Whether the character is currently airborne on a mount. This is the
    /// one state that makes a flying path steer at all.</summary>
    public bool IsInFlight => _condition[ConditionFlag.InFlight];

    /// <summary>
    /// Whether the character is on a mount of their OWN, airborne or not.
    ///
    /// <para>Deliberately not <c>RidingPillion</c> (the flag formerly called
    /// Mounted2): that is riding along on someone else's mount, where the passenger
    /// steers nothing. Counting it here would have the walk request a flying path
    /// and then wait forever for a take-off only the other player can trigger.</para>
    /// </summary>
    public bool IsMounted => _condition[ConditionFlag.Mounted];

    /// <summary>Whether the summon animation is playing right now.</summary>
    public bool IsMounting => _condition[ConditionFlag.Mounting] || _condition[ConditionFlag.Mounting71];

    /// <summary>Why flying is not available here, or <see cref="FlightBlock.None"/>
    /// when it is. Separate from a bare bool because each reason gets the player a
    /// different sentence - "no flying in cities" and "you have not collected the
    /// aether currents yet" are not the same news.</summary>
    public FlightBlock Blocked()
    {
        var territory = _data.GetExcelSheet<LuminaTerritoryType>()
                             ?.GetRowOrDefault(_clientState.TerritoryType);
        if (territory == null) return FlightBlock.NoVolume;

        var row = territory.Value;

        // Gate 1: does vnavmesh build a flight volume for this territory at all?
        if (row.TerritoryIntendedUse.RowId is not (1 or 47 or 49))
            return FlightBlock.NoVolume;

        // Mounts disabled means flying is off the table regardless of the rest -
        // and it is the same bit the game uses to grey the mount out.
        if (!row.Mount) return FlightBlock.NoMount;

        // Gate 2: has the player unlocked the region's aether currents? A set of 0
        // names no requirement, so nothing to check.
        var set = row.AetherCurrentCompFlgSet.RowId;
        if (set != 0 && !IsAetherCurrentZoneComplete(set))
            return FlightBlock.AetherCurrents;

        return FlightBlock.None;
    }

    /// <summary>
    /// Whether vnavmesh has an air route to compute in this territory - the ONE
    /// hard condition the walk logic checks, and the only one that is about the
    /// map rather than about the player.
    ///
    /// <para>Deliberately NOT the same as <see cref="Blocked"/> returning None:
    /// that one also reports the aether currents, which is information for the
    /// player and must never gate a flight the player is demonstrably already
    /// flying.</para>
    /// </summary>
    public bool HasFlightRoutes
    {
        get
        {
            var territory = _data.GetExcelSheet<LuminaTerritoryType>()
                                 ?.GetRowOrDefault(_clientState.TerritoryType);
            return territory?.TerritoryIntendedUse.RowId is 1 or 47 or 49;
        }
    }

    /// <summary>
    /// Asks the game whether the player has completed the aether currents of the
    /// given set. Reflection-free but through a native function pointer, so the
    /// call is guarded: PlayerState is null before the character is loaded, and a
    /// signature that stops resolving after a patch throws rather than lying.
    /// </summary>
    private unsafe bool IsAetherCurrentZoneComplete(uint compFlgSet)
    {
        try
        {
            var state = PlayerState.Instance();
            if (state == null) return false;
            return state->IsAetherCurrentZoneComplete(compFlgSet);
        }
        catch (Exception ex)
        {
            // Not silent: without this line a patch that moved the signature would
            // look exactly like "the player has not collected the currents".
            _log.Warning(ex, $"[Flug] IsAetherCurrentZoneComplete({compFlgSet}) fehlgeschlagen - " +
                              "Flug wird als gesperrt behandelt.");
            return false;
        }
    }

    /// <summary>
    /// Gets off the mount. In mid-air this drops the character to the ground, which
    /// is the intended landing - the game has no fall damage, and standing on the
    /// ground is the only state in which the player can talk, gather or fight.
    /// Returns false when the game refused, so the caller can say so rather than
    /// claim a landing that did not happen.
    /// </summary>
    public unsafe bool TryDismount()
    {
        if (!IsMounted) return true;

        // Wie beim Aufsitzen gedrosselt: der Aufrufer fragt jeden Frame, bis das
        // Reittier weg ist, und das Absteigen hat eine Animation. Ungedrosselt
        // faenden 60 UseAction-Aufrufe pro Sekunde statt, jeder mit einer eigenen
        // Fehlerzeile des Spiels im Chat.
        var now = DateTime.UtcNow;
        if (now - _lastDismountAttempt < DismountRetryInterval) return false;
        _lastDismountAttempt = now;

        try
        {
            var am = ActionManager.Instance();
            if (am == null) return false;

            var status = am->GetActionStatus(ActionType.GeneralAction, Dismount, NoTarget, false, false);
            if (status != 0)
            {
                _log.Debug($"[Flug] Absteigen nicht moeglich (Status {status}).");
                return false;
            }

            var ok = am->UseAction(ActionType.GeneralAction, Dismount, NoTarget);
            _log.Info($"[Flug] Abgestiegen: {ok}.");
            return ok;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "[Flug] Absteigen fehlgeschlagen.");
            return false;
        }
    }

}

/// <summary>Why the auto-walk cannot fly here. Each value maps to its own spoken
/// sentence - see <c>AccessibilityStrings.FlightBlockedReason</c>.</summary>
public enum FlightBlock
{
    /// <summary>Nothing in the way - flying is available.</summary>
    None,

    /// <summary>vnavmesh builds no flight volume for this territory (cities,
    /// instances, dungeons). There is no air route to compute.</summary>
    NoVolume,

    /// <summary>The zone forbids mounts.</summary>
    NoMount,

    /// <summary>The zone's aether currents are not complete yet.</summary>
    AetherCurrents,
}
