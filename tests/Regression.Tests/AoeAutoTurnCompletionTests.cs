using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public class AoeAutoTurnCompletionTests
{
    private static readonly DangerZone Circle = new(DangerShape.Circle, Vector3.Zero, 3, 0, 0, 0);

    [Fact]
    public void ConeMarginUsesDistanceToItsEdgeRatherThanAnArcApproximation()
    {
        var cone = new DangerZone(DangerShape.Cone, Vector3.Zero, 10, 0, MathF.PI / 4, 0);
        var angle = cone.HalfAngleRad + 0.70f;
        var point = new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 3;
        Assert.InRange(3 * MathF.Sin(0.70f), 1.9f, 2);
        Assert.True(AoeEscapeGeometry.ContainsWithMargin(cone, point, 2));
        Assert.False(AoeEscapeGeometry.ContainsWithMargin(cone, point, 1.8f));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(0)]
    public void InvalidAttackGeometryCannotAuthorizeAnyPathOrTurn(float range)
    {
        var bad = Circle with { Range = range };
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(0, 0, 8), [bad],
            _ => throw new Exception("Invalid geometry must stop before floor queries"), (_, _) => true));
        Assert.Equal(AoeTurnResult.None, new AoeAutoTurn().Update(1, 133, true, true, false,
            Vector3.Zero, 0, [bad], 0, (_, _, _) => throw new Exception(), _ => throw new Exception()));
    }

    [Fact]
    public void InfiniteFacingIsConservativelyDangerousAndNeverLoops()
        => Assert.True((Circle with { Shape = DangerShape.Cone, Facing = float.PositiveInfinity }).Contains(Vector3.One));

    [Fact]
    public void AShiftedFloorHitCannotPretendToSupportTheEdgeOfALedge()
    {
        Vector3? Floor(Vector3 p) => MathF.Abs(p.X) > 0.1f ? p with { X = 0 } : p;
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(0, 0, 8), [Circle], Floor, (_, _) => true));
    }

    [Fact]
    public void AttackChangeRestartsSearchWithCurrentCandidateIndices()
    {
        var turn = new AoeAutoTurn();
        Vector3? attempted = null;
        Vector3? Block(Vector3 _, Vector3 point, IReadOnlyList<DangerZone> __) { attempted ??= point; return null; }
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [Circle], 0, Block, _ => false);
        attempted = null;
        var larger = Circle with { Range = 7 };
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [larger], 50, Block, _ => false);
        Assert.Equal(new Vector3(0, 0, 10), attempted);
    }

    [Fact]
    public void RevalidationCannotKeepAnOldExitWhenTheReturnedPointMoved()
    {
        var turn = new AoeAutoTurn();
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [Circle], 0, (_, to, _) => to, _ => true);
        Assert.NotNull(turn.GuidanceSpot);
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [Circle], 300, (_, _, _) => Vector3.Zero, _ => true);
        Assert.Null(turn.GuidanceSpot);
    }

    [Fact]
    public void InvalidGeometryDropsPreviouslyAcceptedGuidance()
    {
        var turn = new AoeAutoTurn();
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [Circle], 0, (_, to, _) => to, _ => true);
        turn.Update(1, 133, true, true, false, Vector3.Zero, 0, [Circle with { Range = float.NaN }],
            300, (_, _, _) => throw new Exception(), _ => throw new Exception());
        Assert.Null(turn.GuidanceSpot);
    }

    [Fact]
    public void StopBeforeFirstDangerFrameIsNotLostDuringContextInitialization()
    {
        var f = new Fixture();
        f.Service.Cancel();
        f.Update(0); f.Update(150); f.Update(500);
        Assert.Empty(f.Control.Turned);
        Assert.Empty(f.Spoken);
        Assert.Null(f.Escape.SafeSpot);
    }

    [Fact]
    public void ServiceAnnouncesAndGuidesOnlyAfterTheActualLaterReadback()
    {
        var f = new Fixture();
        f.Update(0);
        Assert.Single(f.Control.Turned);
        Assert.Empty(f.Spoken);
        Assert.Null(f.Escape.SafeSpot);
        f.Update(100);
        Assert.Empty(f.Spoken);
        f.Update(150);
        Assert.Single(f.Spoken);
        Assert.Equal(f.Control.Turned[0], f.Escape.SafeSpot);
        f.Update(400);
        Assert.Single(f.Spoken);
        Assert.Single(f.Control.Turned);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedCameraOrCharacterReadbackNeverTellsThePlayerToMoveForward(bool failCamera)
    {
        var f = new Fixture();
        f.Update(0);
        f.Control.State = failCamera ? f.Control.State with { CameraFacing = 0 }
            : f.Control.State with { CharacterFacing = 0 };
        f.Update(150);
        Assert.Equal(AccessibilityStrings.AoeAutoTurnNotConfirmed, Assert.Single(f.Spoken));
        Assert.Null(f.Escape.SafeSpot);
        f.Update(600);
        Assert.Single(f.Control.Turned);
    }

    [Fact]
    public void StopImmediatelyRemovesConfirmedGuidanceAndDoesNotTurnAgain()
    {
        var f = new Fixture();
        f.Update(0); f.Update(150);
        Assert.NotNull(f.Escape.SafeSpot);
        f.Service.Cancel();
        Assert.Null(f.Escape.SafeSpot);
        f.Update(500);
        Assert.Single(f.Control.Turned);
        Assert.Single(f.Spoken);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void RealSilenceKeyCancelsAoeHelpBeforeModalInputWithoutTakingChatInput(bool reader, bool typing, bool cancels)
    {
        var f = new Fixture();
        f.Update(0); f.Update(150);
        var (plugin, keys) = FollowTargetKeyTests.CreateInput();
        Set(plugin, "_config", f.Config);
        Set(plugin, "_aoeAutoTurn", f.Service);
        Set(plugin, "_warnVoice", RuntimeHelpers.GetUninitializedObject(typeof(WarningVoiceService)));
        Set(plugin, "_textInputActive", typing);
        keys.Down[0x11] = true; keys.Down[0x7A] = true;
        typeof(Plugin).GetMethod("UpdateKeyEdges", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
        var handled = typeof(Plugin).GetMethod("HandleAoeTurnCancelKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, [reader]);
        Assert.Equal(cancels, handled);
        Assert.Equal(cancels, f.Escape.SafeSpot == null);
    }

    [Fact]
    public void RealStopCommandCancelsConfirmedAoeHelp()
    {
        var f = new Fixture();
        f.Update(0); f.Update(150);
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        Set(plugin, "_config", f.Config);
        Set(plugin, "_aoeAutoTurn", f.Service);
        Set(plugin, "_warnVoice", RuntimeHelpers.GetUninitializedObject(typeof(WarningVoiceService)));
        Set(plugin, "_tolk", RuntimeHelpers.GetUninitializedObject(typeof(TolkService)));
        Set(plugin, "_chatVoice", RuntimeHelpers.GetUninitializedObject(typeof(ChatVoiceService)));
        typeof(Plugin).GetMethod("OnCommand", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(plugin, ["/acc", "stop"]);
        Assert.Null(f.Escape.SafeSpot);
        f.Update(500);
        Assert.Single(f.Control.Turned);
    }

    [Fact]
    public void DisableImmediatelyRemovesOwnedGuidanceWithoutReadingTheGame()
    {
        var f = new Fixture();
        f.Update(0); f.Update(150);
        f.Config.AutoTurnAoe = false;
        f.Control.ThrowOnRead = true;
        f.Update(500);
        Assert.Null(f.Escape.SafeSpot);
    }

    [Theory]
    [InlineData("menu")]
    [InlineData("unknown")]
    [InlineData("player")]
    [InlineData("territory")]
    [InlineData("manual")]
    public void InterruptedPendingTurnCannotGiveAStaleForwardInstruction(string interruption)
    {
        var f = new Fixture();
        f.Update(0);
        if (interruption == "unknown") Set(f.Combat, "<AutoTurnUncertain>k__BackingField", true);
        if (interruption == "player") f.PlayerId = 2;
        if (interruption == "territory") f.Territory = 153;
        if (interruption == "manual") f.Control.State = f.Control.State with { ManualInput = true };
        f.Update(150, interruption != "menu");
        Assert.Empty(f.Spoken);
        Assert.Null(f.Escape.SafeSpot);
    }

    [Fact]
    public void NewObstacleBeforeReadbackCancelsTheForwardInstruction()
    {
        var f = new Fixture();
        f.Update(0);
        f.Control.PathBlocked = true;
        f.Update(150);
        Assert.Equal(AccessibilityStrings.AoeAutoTurnNotConfirmed, Assert.Single(f.Spoken));
        Assert.Null(f.Escape.SafeSpot);
    }

    [Fact]
    public void ShortCastLogsThePathRefusalWithoutWaitingForAnExhaustiveSearch()
    {
        var f = new Fixture();
        f.Control.PathBlocked = true;
        f.Update(0); f.Update(50); f.Update(100);
        Assert.Empty(f.Control.Turned);
        Assert.Empty(f.Spoken);
        Assert.Contains("no supported ground hit", Assert.Single(f.Logged));
    }

    [Fact]
    public void SuspendedControlRecordsTheActualUiCauseWithoutTurning()
    {
        var f = new Fixture();
        f.Service.Update(false, "focused addon=Talk");
        Assert.Empty(f.Control.Turned);
        Assert.Contains("focused addon=Talk", Assert.Single(f.Logged));
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    private sealed class Fixture
    {
        internal readonly Configuration Config = new() { AutoTurnAoe = true };
        internal readonly EscapeRouteService Escape = (EscapeRouteService)RuntimeHelpers.GetUninitializedObject(typeof(EscapeRouteService));
        internal readonly CombatService Combat = (CombatService)RuntimeHelpers.GetUninitializedObject(typeof(CombatService));
        internal readonly FakeControl Control = new();
        internal readonly List<string> Spoken = new();
        internal readonly List<string> Logged = new();
        internal readonly AoeAutoTurnService Service;
        internal ulong PlayerId = 1;
        internal uint Territory = 133;
        private long _time;

        internal Fixture()
        {
            var player = AoeAutoTurnIntegrationTests.Proxy.Of<IPlayerCharacter>((m, _) => m.Name switch
            {
                "get_CurrentHp" => 100u, "get_Address" => (nint)1, "get_GameObjectId" => PlayerId,
                "get_Position" => Vector3.Zero, _ => throw new Exception(m.Name),
            });
            var objects = AoeAutoTurnIntegrationTests.Proxy.Of<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? player : throw new Exception(m.Name));
            var client = AoeAutoTurnIntegrationTests.Proxy.Of<IClientState>((m, _) => m.Name switch
            {
                "get_IsLoggedIn" => true, "get_TerritoryType" => Territory, _ => throw new Exception(m.Name),
            });
            var condition = AoeAutoTurnIntegrationTests.Proxy.Of<ICondition>((_, _) => false);
            var log = AoeAutoTurnIntegrationTests.Proxy.Of<IPluginLog>((m, args) =>
            {
                if (m.Name == "Info") Logged.Add((string)args![0]!);
                return null;
            });
            Set(Combat, "_escape", Escape);
            Set(Combat, "_autoTurnZones", new List<DangerZone> { Circle });
            Set(Combat, "<AutoTurnInDanger>k__BackingField", true);
            Service = new(Config, objects, client, condition, Combat, log, null!, null!, Control, () => _time, Spoken.Add);
        }

        internal void Update(long time, bool allowed = true) { _time = time; Service.Update(allowed); }
    }

    private sealed class FakeControl : IAoeTurnControl
    {
        internal AoeControlState State = new(true, false, MathF.PI / 2, MathF.PI / 2);
        internal readonly List<Vector3> Turned = new();
        internal bool ThrowOnRead;
        internal bool PathBlocked;
        public string? LastPathFailure => PathBlocked ? "no supported ground hit" : null;
        public AoeControlState Read(IPlayerCharacter player) => ThrowOnRead ? throw new Exception("Unexpected native read") : State;
        public bool Turn(IPlayerCharacter player, Vector3 point)
        {
            Turned.Add(point);
            var angle = MathF.Atan2(point.X, point.Z);
            State = State with { CameraFacing = angle, CharacterFacing = angle };
            return true;
        }
        public Vector3? CheckPath(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones) => PathBlocked ? null
            : AoeEscapePath.Check(from, to, zones, p => p with { Y = 0 }, (_, _) => true);
    }
}
