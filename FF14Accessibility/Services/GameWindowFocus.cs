using Dalamud.Plugin.Services;
using ClientFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace FF14Accessibility.Services;

/// <summary>
/// Whether the game window currently has focus. One source of truth for muting
/// every mod-owned tone and SAPI channel while the player is in another program.
///
/// Reads <c>Framework.WindowInactive</c> (FieldOffset 6104, verified in
/// docs/game-api.md). Preferred over <c>GetForegroundWindow</c>: the game already
/// tracks this, so there is no second source that could drift.
///
/// When the struct is unavailable the feature stays ENABLED - a missing flag must
/// not silence navigation or combat cues.
/// </summary>
public static class GameWindowFocus
{
    private static bool _active = true;
    private static bool _logged;

    /// <summary>True while the game window is in the foreground (or the flag cannot be read).</summary>
    public static bool IsActive => _active;

    /// <summary>
    /// True for the one frame where focus was just lost. Callers silence continuous
    /// SAPI speech on this edge so Alt-Tab does not leave a monologue running.
    /// </summary>
    public static bool JustBecameInactive { get; private set; }

    /// <summary>
    /// Refresh the cached flag. Call once per frame from Plugin.OnFrameworkUpdate
    /// before any audio service that depends on it.
    /// </summary>
    public static unsafe void Update(IPluginLog? log = null)
    {
        var framework = ClientFramework.Instance();
        var active = framework == null || !framework->WindowInactive;

        JustBecameInactive = _active && !active;

        if (active != _active || !_logged)
        {
            log?.Debug($"[Audio] Spielfenster {(active ? "aktiv" : "im Hintergrund")} - Mod-Toene {(active ? "an" : "aus")}.");
            _logged = true;
        }

        _active = active;
    }
}
