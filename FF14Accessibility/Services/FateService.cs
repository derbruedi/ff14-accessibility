using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;

namespace FF14Accessibility.Services;

/// <summary>A single active FATE in the current zone - what the object browser's
/// "FATEs" category lists. Running FATEs can be joined right now; Preparing ones
/// are about to appear.</summary>
public sealed record FateInfo(
    ushort FateId,
    string Name,
    byte Level,
    byte Progress,
    Vector3 Position,
    bool IsPreparing);

/// <summary>
/// Reads the active FATEs of the current zone from the game's FateManager. FATEs
/// never appear in the quest journal - they are world events the game only
/// exposes here and on the map - so a blind player has no other way to discover
/// them. Read fresh on every call, never cached: FATEs start, progress and end
/// continuously.
///
/// All structs ilspycmd-verified (docs/game-api.md -> "FATE"):
///   FateManager.Instance()->Fates = StdVector&lt;Pointer&lt;FateContext&gt;&gt;;
///   FateContext.Name @192, State @940, Progress @951, Level @2035, Location @2128;
///   FateState.Running = 4 (joinable), Preparing = 3 (spawning).
/// </summary>
public sealed class FateService
{
    private readonly IClientState _clientState;

    public FateService(IClientState clientState)
    {
        _clientState = clientState;
    }

    /// <summary>
    /// Every active FATE in the current zone: Running (joinable now) plus
    /// Preparing (about to appear). Ending/Ended/Failed FATEs are gone from the
    /// player's point of view and are skipped, so the list only holds things
    /// worth walking to. Empty when the zone has none or nobody is logged in.
    /// </summary>
    public unsafe List<FateInfo> GetActiveFates()
    {
        var result = new List<FateInfo>();
        if (!_clientState.IsLoggedIn) return result;

        var mgr = FateManager.Instance();
        if (mgr == null) return result;

        for (var i = 0; i < mgr->Fates.Count; i++)
        {
            var fate = mgr->Fates[i].Value;
            if (fate == null) continue;

            var preparing = fate->State == FateState.Preparing;
            if (fate->State != FateState.Running && !preparing) continue;

            var name = fate->Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            result.Add(new FateInfo(
                fate->FateId,
                name,
                fate->Level,
                fate->Progress,
                fate->Location,
                preparing));
        }

        return result;
    }
}
