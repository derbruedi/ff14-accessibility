using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace FF14Accessibility.Services;

internal readonly record struct AoeControlState(bool Available, bool ManualInput, float CameraFacing, float CharacterFacing,
    string? BlockedReason = null)
{
    internal bool Faces(float angle) => Available && float.IsFinite(CameraFacing) && float.IsFinite(CharacterFacing)
        && MathF.Abs(MathF.IEEERemainder(CameraFacing - angle, MathF.Tau)) <= MathF.PI / 18
        && MathF.Abs(MathF.IEEERemainder(CharacterFacing - angle, MathF.Tau)) <= MathF.PI / 18;
}

internal interface IAoeTurnControl
{
    AoeControlState Read(IPlayerCharacter player);
    bool Turn(IPlayerCharacter player, Vector3 point);
    Vector3? CheckPath(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones);
    string? LastPathFailure => null;
}

internal sealed class AoeTurnControl : IAoeTurnControl
{
    public string? LastPathFailure { get; private set; }
    public unsafe AoeControlState Read(IPlayerCharacter player)
    {
        var input = InputManager.Instance();
        var camera = CameraManager.Instance();
        // FaceTowards writes the normal camera. Never announce forward against
        // a different active camera, or pass a nonfinite angle to its normalizer.
        if (input == null || camera == null || camera->ActiveCameraIndex != 0 || camera->Camera == null
            || !float.IsFinite(camera->Camera->DirH) || !float.IsFinite(player.Rotation))
            return new(false, false, 0, 0, "camera or input unavailable");
        var manual = input->HeldMouseButtons != InputManager.MouseButtonHoldState.None
            || input->GetInputStatus(InputCode.CAMERA_LEFT) || input->GetInputStatus(InputCode.CAMERA_RIGHT)
            || input->GetInputStatus(InputCode.MOVE_LEFT) || input->GetInputStatus(InputCode.MOVE_RIGHT)
            || input->GetInputStatus(InputCode.MOVE_BACK)
            || input->GetInputStatus(InputCode.MOVE_STRIFE_L) || input->GetInputStatus(InputCode.MOVE_STRIFE_R);
        var running = InputManager.IsAutoRunning();
        return new(!running, manual,
            MathF.IEEERemainder(camera->Camera->DirH + MathF.PI, MathF.Tau), player.Rotation,
            running ? "native autorun" : manual ? "manual steering" : null);
    }

    public bool Turn(IPlayerCharacter player, Vector3 point) => FacingService.FaceTowards(player, point) != null;
    public Vector3? CheckPath(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones)
    {
        var result = AoeEscapePath.CheckLive(from, to, zones, out var failure);
        LastPathFailure = failure;
        return result;
    }
}
