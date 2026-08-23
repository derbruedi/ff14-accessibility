using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace FF14Accessibility.Services;

/// <summary>
/// Turning the character towards a point. One place, because two callers need
/// the identical maths and an inconsistency between them would be invisible to
/// a blind player: the manual "face the route" key
/// (<see cref="NavigationService.FaceGuideDirection"/>) and the automatic turn
/// on arrival (<see cref="AutoWalkService"/>).
///
/// <para>
/// TWO fields are written, and the second one is the important one. The game's
/// standard movement mode is CAMERA-RELATIVE (<c>UiControl.MoveMode</c> 0,
/// read from the game's own config and logged as 0 for this player):
/// walking forward goes where the CAMERA looks, not where the character faces.
/// Turning only the character would leave the player facing the door while
/// running past it - exactly the complaint this exists for. So the camera's
/// horizontal direction is turned with it.
/// </para>
///
/// <para>
/// <b>DirH IS OFFSET BY HALF A TURN, MEASURED.</b> <c>Camera.DirH</c> is not the
/// direction the camera LOOKS, it is the direction from the character TO the
/// camera - the camera sits behind the character. Log 2026-08-22, fourteen
/// samples of the manual key, every single one at rest:
/// <c>Rotation - DirH = 3.142</c> (= pi, to three decimals). It held across
/// samples where the player had turned in between, so it is the game's resting
/// coupling and not a coincidence of one pose. Writing <c>DirH = rotation</c>
/// therefore aimed the camera exactly BACKWARDS, and with camera-relative
/// movement "forward" then ran away from the goal - which is precisely what the
/// player reported ("er steht wahrscheinlich immer noch falsch").
/// </para>
///
/// <para>
/// STILL OPEN, and this is why <see cref="Tick"/> exists: whether our write to
/// DirH SURVIVES. In the same log the value was back at <c>rotation - pi</c>
/// seconds later even where the character had not turned, which has two possible
/// causes - the game pulling the camera back behind the character, or our write
/// never reaching the camera the game actually reads. Reading the field back in
/// the same frame cannot tell those apart; <see cref="Tick"/> re-reads it several
/// frames later and logs the answer.
/// </para>
///
/// <para>
/// The angle convention is <c>Atan2(dx, dz)</c>, direction vector
/// <c>(sin, 0, cos)</c> - the same one the heading announcements are built on
/// (verified in-game 2026-07-10) and the same one the game's own zone-exit
/// field <c>PlayerRunningDirection</c> uses (measured offline over 978 exits,
/// see docs/game-api.md).
/// </para>
/// </summary>
internal static class FacingService
{
    /// <summary>Below this the direction is meaningless - standing on the point.</summary>
    private const float MinimumSeparation = 0.01f;

    /// <summary>Seconds after a turn at which the result is re-read and logged.</summary>
    private static readonly double[] VerifyAtS = { 0.2, 1.0 };

    private static IPluginLog? _log;

    private static float    _wantedRotation;
    private static float    _wantedDirH;
    private static DateTime _turnedAt;
    private static int      _verifyStage = -1;

    /// <summary>Gives the service its log. Without it the turn still works, only unmeasured.</summary>
    public static void Configure(IPluginLog log) => _log = log;

    /// <summary>
    /// Turns character and camera towards <paramref name="target"/>. Returns the
    /// angle written, or null when the character is already on the point and no
    /// direction can be derived.
    /// </summary>
    public static unsafe float? FaceTowards(IGameObject player, Vector3 target)
    {
        if (player == null || player.Address == 0) return null;

        var dx = target.X - player.Position.X;
        var dz = target.Z - player.Position.Z;
        if (Math.Abs(dx) < MinimumSeparation && Math.Abs(dz) < MinimumSeparation) return null;

        var rotation = (float)Math.Atan2(dx, dz);
        // Half a turn behind the character - see the DirH note in the class docs.
        var dirH = Normalise(rotation - (float)Math.PI);

        ((CSGameObject*)player.Address)->Rotation = rotation;

        var camera     = CameraManager.Instance();
        var gameCamera = camera != null ? camera->Camera : null;
        if (gameCamera != null) gameCamera->DirH = dirH;

        _wantedRotation = rotation;
        _wantedDirH     = dirH;
        _turnedAt       = DateTime.UtcNow;
        _verifyStage    = 0;

        _log?.Info($"[Drehung] gesetzt: rot={rotation:F3} dirH={dirH:F3} " +
                   $"kameraIndex={(camera != null ? camera->ActiveCameraIndex : -1)} " +
                   $"kameraDa={(gameCamera != null)}");

        return rotation;
    }

    /// <summary>
    /// Re-reads the two fields some frames after a turn and logs what actually
    /// stuck. Called every frame; does nothing unless a turn is pending. This is
    /// the only way to tell "the game took our camera turn back" apart from "our
    /// write never landed" - a read in the same frame just returns our own write.
    /// </summary>
    public static unsafe void Tick(IGameObject? player)
    {
        if (_verifyStage < 0 || _verifyStage >= VerifyAtS.Length) return;
        if ((DateTime.UtcNow - _turnedAt).TotalSeconds < VerifyAtS[_verifyStage]) return;

        var stage = _verifyStage++;
        if (_log == null) return;

        var camera     = CameraManager.Instance();
        var gameCamera = camera != null ? camera->Camera : null;

        var isRotation = player != null && player.Address != 0
            ? ((CSGameObject*)player.Address)->Rotation
            : float.NaN;
        var isDirH = gameCamera != null ? gameCamera->DirH : float.NaN;

        // Both deltas wrapped, so a turn across the +pi/-pi seam does not read as
        // a full circle of error.
        _log.Info($"[Drehung] nach {VerifyAtS[stage]:F1}s: rot={isRotation:F3} (soll {_wantedRotation:F3}, " +
                  $"ab {Normalise(isRotation - _wantedRotation):F3}) " +
                  $"dirH={isDirH:F3} (soll {_wantedDirH:F3}, ab {Normalise(isDirH - _wantedDirH):F3})");
    }

    /// <summary>
    /// The direction the camera LOOKS, in the same convention as
    /// <c>IGameObject.Rotation</c> - or null when there is no camera to ask.
    ///
    /// <para>
    /// This is <c>DirH + pi</c>, not <c>DirH</c>: the raw field points from the
    /// character TO the camera, which sits behind them (measured 2026-08-22,
    /// fourteen samples, see the class docs). Turning it round here means no
    /// caller has to remember the offset - forgetting it once aims things exactly
    /// backwards, which is the mistake this file already made once.
    /// </para>
    ///
    /// <para>
    /// WHY ANYONE WOULD WANT THIS instead of the character rotation: with
    /// <c>UiControl.MoveMode</c> 0 - the game's standard, and what this player has
    /// - walking forward goes where the CAMERA looks. A "left" or "right" measured
    /// against the character is then only correct while the camera happens to sit
    /// squarely behind them.
    /// </para>
    /// </summary>
    public static unsafe float? CameraFacing()
    {
        var camera     = CameraManager.Instance();
        var gameCamera = camera != null ? camera->Camera : null;
        if (gameCamera == null) return null;

        return Normalise(gameCamera->DirH + (float)Math.PI);
    }

    /// <summary>Folds an angle into [-pi, pi] so differences stay comparable.</summary>
    private static float Normalise(float angle)
    {
        const float twoPi = (float)(2 * Math.PI);
        while (angle >  Math.PI) angle -= twoPi;
        while (angle < -Math.PI) angle += twoPi;
        return angle;
    }
}
