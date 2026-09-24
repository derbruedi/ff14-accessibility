namespace FF14Accessibility.Services;

/// <summary>
/// How the FFXV collab Garuda duty (Territory 834) assists with Warp-strike.
/// Only active inside that duty while the Warp duty action is on the bar —
/// never outside the quest instance.
/// </summary>
public enum NocturneWarpMode : byte
{
    /// <summary>No cue and no auto-warp.</summary>
    Off = 0,

    /// <summary>Sound (+ target set) at the moment Warp should be used; player presses Umschalt+F10.</summary>
    Manual = 1,

    /// <summary>Set target and execute Warp-strike automatically.</summary>
    Auto = 2,
}
