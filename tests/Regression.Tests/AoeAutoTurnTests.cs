using System.Numerics;
using System.Text.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public class AoeAutoTurnTests
{
    private static readonly DangerZone Circle = new(DangerShape.Circle, Vector3.Zero, 3f, 0, 0, 0);
    private readonly AoeAutoTurn _turn = new();
    private readonly List<Vector3> _turned = new();
    private float _camera;

    private AoeTurnResult Update(long time = 0, Vector3? position = null, DangerZone[]? zones = null,
        bool enabled = true, bool allowed = true, bool uncertain = false, ulong player = 1, uint territory = 133,
        Func<Vector3, Vector3, IReadOnlyList<DangerZone>, Vector3?>? path = null, float? camera = null)
        => _turn.Update(player, territory, enabled, allowed, uncertain, position ?? Vector3.Zero,
            camera ?? _camera, zones ?? [Circle], time, path ?? Walk, point =>
            {
                _turned.Add(point);
                _camera = MathF.Atan2(point.X - (position?.X ?? 0), point.Z - (position?.Z ?? 0));
                return true;
            });

    private static Vector3? Walk(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones)
        => AoeEscapePath.Check(from, to, zones, p => p with { Y = 0 }, (_, _) => true);

    [Fact]
    public void TurnsToAnExitWithMarginAndNeverContinuouslySteers()
    {
        Assert.Equal(AoeTurnResult.Turned, Update());
        var point = Assert.Single(_turned);
        Assert.False(Circle.ContainsWithMargin(point, 2));
        Assert.Equal(6f, new Vector2(point.X, point.Z).Length(), 3);
        for (var i = 1; i <= 60; i++) Assert.Equal(AoeTurnResult.None, Update(i * 100));
        Assert.Single(_turned);
        Assert.Equal(point, _turn.GuidanceSpot);
    }

    [Fact]
    public void KeepsSearchingOtherDirectionsAfterBlockedCandidates()
    {
        var attempted = new List<Vector3>();
        Vector3? Path(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones)
        {
            attempted.Add(to);
            return to.X < -1 ? Walk(from, to, zones) : null;
        }
        Assert.Equal(AoeTurnResult.None, Update(path: Path));
        Assert.Equal(2, attempted.Count);
        for (var i = 1; i <= 24 && _turned.Count == 0; i++) Update(i * 50, path: Path);
        Assert.True(Assert.Single(_turned).X < -1);
        Assert.True(attempted.Distinct().Count() > 12);
    }

    [Fact]
    public void ReportsFailureOnceOnlyAfterSearchingAllCandidatesAndCanRecover()
    {
        var reports = 0;
        for (var i = 0; i < 1200; i++)
            if (Update(i * 50, path: (_, _, _) => null) == AoeTurnResult.NoPath) reports++;
        Assert.Equal(1, reports);
        Assert.Empty(_turned);
        Assert.Null(_turn.GuidanceSpot);
        Assert.Equal(AoeTurnResult.Turned, Update(60000));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void DisabledSuspendedOrUncertainFramesNeverInspectOrTurn(bool enabled, bool allowed, bool uncertain)
    {
        Assert.Equal(AoeTurnResult.None, Update(enabled: enabled, allowed: allowed, uncertain: uncertain,
            path: (_, _, _) => throw new Exception("No path inspection should occur.")));
        Assert.Empty(_turned);
    }

    [Fact]
    public void MissingOrInvalidCameraCannotCauseATurn()
    {
        foreach (var camera in new float?[] { null, float.NaN, float.PositiveInfinity })
            Assert.Equal(AoeTurnResult.None, _turn.Update(1, 133, true, true, false, Vector3.Zero,
                camera, [Circle], 0, (_, _, _) => throw new Exception(), _ => throw new Exception()));
    }

    [Fact]
    public void ManualCameraOverrideSuppressesReplacementUntilPlayerLeavesDanger()
    {
        Update();
        _camera = MathF.PI / 2;
        Update(300);
        Assert.Null(_turn.GuidanceSpot);
        Update(500, zones: [Circle, new(DangerShape.Circle, _turned[0], 3, 0, 0, 0)]);
        Assert.Single(_turned);
        Update(600, zones: []);
        Assert.Equal(AoeTurnResult.Turned, Update(700));
        Assert.Equal(2, _turned.Count);
    }

    [Fact]
    public void StopSuppressesCurrentDangerAndDoesNotRepeatOnReturningFromAMenu()
    {
        Update();
        _turn.Cancel();
        Update(300, allowed: false);
        Update(500);
        Assert.Single(_turned);
        Assert.Null(_turn.GuidanceSpot);
        Update(600, zones: []);
        Update(700);
        Assert.Equal(2, _turned.Count);
    }

    [Fact]
    public void RechecksDynamicBlockersWithoutRepeatingATurnForTheSameSafePoint()
    {
        Update();
        var first = _turned[0];
        var inspected = 0;
        Vector3? Path(Vector3 from, Vector3 to, IReadOnlyList<DangerZone> zones)
        {
            inspected++;
            return Vector3.Distance(to, first) < 0.1f ? null : Walk(from, to, zones);
        }
        Assert.Equal(AoeTurnResult.None, Update(300, path: Path));
        Assert.Equal(2, inspected); // old point plus one candidate; bounded work
        Assert.Equal(AoeTurnResult.Turned, Update(350, path: Path));
        Assert.True(inspected >= 2);
        Assert.Equal(2, _turned.Count);
        Assert.NotEqual(first, _turned[1]);
    }

    [Fact]
    public void NewOverlappingAttackInvalidatesTheOldExit()
    {
        Update();
        var old = _turned[0];
        DangerZone[] zones = [Circle, new(DangerShape.Circle, old, 3f, 0, 0, 0)];
        for (var i = 1; i < 40 && _turned.Count < 2; i++) Update(i * 50, zones: zones);
        Assert.Equal(2, _turned.Count);
        Assert.All(zones, zone => Assert.False(zone.ContainsWithMargin(_turned[1], 2)));
    }

    [Theory]
    [InlineData(2ul, 133u)]
    [InlineData(1ul, 153u)]
    public void PlayerOrTerritoryChangeDropsThePreviousTurn(ulong player, uint territory)
    {
        Update();
        _turn.Cancel();
        Update(300, player: player, territory: territory);
        Assert.Equal(2, _turned.Count);
    }

    [Fact]
    public void DeathLogoutAndDisableDropStateBeforeReenabling()
    {
        Update();
        Update(300, player: 0);
        Update(400);
        Update(500, enabled: false);
        Update(600);
        Assert.Equal(3, _turned.Count);
        Update(700, position: new Vector3(20, 0, 0));
        Assert.Null(_turn.GuidanceSpot);
    }

    [Fact]
    public void UnsafeOrInvalidValidatedPointsAreNeverPassedToTheTurn()
    {
        foreach (var point in new[] { Vector3.Zero, new Vector3(float.NaN, 0, 9) })
        {
            _turn.Reset();
            Update(path: (_, _, _) => point);
        }
        Assert.Empty(_turned);
    }

    [Fact]
    public void GuidanceReplacesTheOldEscapeSpotAndKeepsOtherRouteAudioSilentWithoutAnExit()
    {
        var escape = (EscapeRouteService)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EscapeRouteService));
        escape.SetAutoTurnGuidance(true, new Vector3(7, 0, 0));
        Assert.True(escape.InDanger);
        Assert.Equal(new Vector3(7, 0, 0), escape.SafeSpot);
        escape.SetAutoTurnGuidance(true, null);
        Assert.True(escape.InDanger);
        Assert.Null(escape.SafeSpot);
        escape.SetAutoTurnGuidance(false, new Vector3(7, 0, 0));
        Assert.False(escape.InDanger);
        Assert.Null(escape.SafeSpot);
    }

    [Theory]
    [InlineData(2, "general_1b", true, false, 0, true)]
    [InlineData(5, "general", true, false, 0, true)]
    [InlineData(2, "general", false, false, 0, false)]
    [InlineData(2, "general", true, true, 0, false)]
    [InlineData(2, "share", true, false, 0, false)]
    [InlineData(3, "gl_fan090", false, false, 0, true)]
    [InlineData(13, "gl_fan120", false, false, 0, true)]
    [InlineData(3, "gl_fan", false, false, 0, false)]
    [InlineData(3, "gl_fan000", false, false, 0, false)]
    [InlineData(3, "gl_fan999", false, false, 0, false)]
    [InlineData(3, "general", false, false, 0, false)]
    [InlineData(4, "general02", false, false, 2, false)]
    [InlineData(8, "general02", false, false, 2, false)]
    [InlineData(12, "general02", false, false, 2, false)]
    [InlineData(2, "", true, false, 0, false)]
    [InlineData(99, "general", true, false, 0, false)]
    public void OnlyMeasuredGeometryMayTurn(byte type, string omen, bool self, bool follows, float width, bool supported)
        => Assert.Equal(supported, CombatService.ReliableAutoTurnShape(type, omen, self, follows, width));
}

public class AoeEscapePathTests
{
    private static readonly DangerZone Circle = new(DangerShape.Circle, Vector3.Zero, 3, 0, 0, 0);
    private static Vector3? Floor(Vector3 p) => p with { Y = 0 };

    [Fact]
    public void DirectGroundExitIsAcceptedButCrossingAnotherAttackIsRejected()
    {
        var to = new Vector3(8, 0, 0);
        Assert.Equal(to, AoeEscapePath.Check(Vector3.Zero, to, [Circle], Floor, (_, _) => true));
        // Start outside the second attack, but within its safety margin. The
        // old margin-only membership allowed crossing the attack itself.
        var nearby = new DangerZone(DangerShape.Circle, new Vector3(2.2f, 0, 0), 1, 0, 0, 0);
        Assert.False(nearby.Contains(Vector3.Zero));
        Assert.True(nearby.ContainsWithMargin(Vector3.Zero, 2));
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, to, [Circle, nearby], Floor, (_, _) => true));
    }

    [Fact]
    public void CanLeaveTwoZonesInWhichThePlayerActuallyStands()
    {
        var nearby = new DangerZone(DangerShape.Circle, new Vector3(1, 0, 0), 2, 0, 0, 0);
        Assert.NotNull(AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle, nearby], Floor, (_, _) => true));
    }

    [Fact]
    public void SafeEndpointDoesNotPermitAWallOrLowBarrierBetweenFloorSamples()
    {
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle], Floor,
            (a, b) => !(a.X <= 2.2f && b.X >= 2.2f)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingFloorAbruptHeightOrMissingSupportAtTheEdgeRejectsThePath(int failure)
    {
        Vector3? Probe(Vector3 p) => failure switch
        {
            0 => p.X is >= 2 and <= 3 ? null : Floor(p),
            1 => p with { Y = p.X >= 2 ? -3 : 0 },
            _ => MathF.Abs(p.Z) > 0.3f && p.X >= 2 ? null : Floor(p),
        };
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle], Probe, (_, _) => true));
    }

    [Fact]
    public void GentleSlopeIsAllowedWithoutTreatingASuddenStepAsARamp()
    {
        var slope = AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle], p => p with { Y = p.X * 0.2f }, (_, _) => true);
        Assert.NotNull(slope);
        Assert.Equal(1.6f, slope.Value.Y, 3);
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle], p => p with { Y = p.X * 0.9f }, (_, _) => true));
    }

    [Fact]
    public void EndpointInsideASafetyMarginIsRejected()
        => Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(4, 0, 0), [Circle], Floor, (_, _) => true));

    [Fact]
    public void APathCannotEnterAnInitiallyUnrelatedCone()
    {
        var cone = new DangerZone(DangerShape.Cone, new Vector3(0, 0, 4), 10, MathF.PI / 2, MathF.PI / 4, 0);
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(12, 0, 0), [Circle, cone], Floor, (_, _) => true));
    }

    [Fact]
    public void NarrowConeTipBetweenTwoFloorSamplesCannotBeCrossed()
    {
        var from = new Vector3(-0.2f, 0, 0.1f);
        var circle = new DangerZone(DangerShape.Circle, from, 1, 0, 0, 0);
        var cone = new DangerZone(DangerShape.Cone, Vector3.Zero, 10, 0, MathF.PI / 4, 0);
        Assert.False(cone.Contains(from));
        Assert.False(cone.Contains(new Vector3(0.3f, 0, 0.1f)));
        Assert.True(AoeEscapeGeometry.Intersects(cone, from, new(0.3f, 0, 0.1f)));
        Assert.Null(AoeEscapePath.Check(from, new(3.8f, 0, 0.1f), [circle, cone], Floor, (_, _) => true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ContinuousIntersectionCatchesThinZonesAndAllowsAParallelMiss(int shape)
    {
        var zone = shape switch
        {
            0 => new DangerZone(DangerShape.Circle, Vector3.Zero, 0.1f, 0, 0, 0),
            1 => new DangerZone(DangerShape.Cone, Vector3.Zero, 0.2f, 0, MathF.PI / 4, 0),
            _ => new DangerZone(DangerShape.Line, Vector3.Zero, 0.2f, 0, 0, 0.1f),
        };
        Assert.True(AoeEscapeGeometry.Intersects(zone, new(-0.2f, 0, 0.05f), new(0.3f, 0, 0.05f)));
        Assert.False(AoeEscapeGeometry.Intersects(zone, new(-0.2f, 0, -1f), new(0.3f, 0, -1f)));
    }

    [Fact]
    public void ContinuousGeometryDoesNotMissIntersectionsAcrossRotatedTranslatedShapes()
    {
        var random = new Random(73091);
        for (var i = 0; i < 300; i++)
        {
            var facing = (float)(random.NextDouble() * Math.Tau - Math.PI);
            var center = new Vector3(12, 5, -6);
            var from = center + new Vector3((float)(random.NextDouble() * 16 - 8), 0, (float)(random.NextDouble() * 16 - 8));
            var to = center + new Vector3((float)(random.NextDouble() * 16 - 8), 0, (float)(random.NextDouble() * 16 - 8));
            foreach (var shape in new[] { DangerShape.Circle, DangerShape.Cone, DangerShape.Line })
            {
                var zone = new DangerZone(shape, center, 3, facing, MathF.PI / 4, 0.25f);
                var sampledIntersection = false;
                for (var step = 0; step <= 400; step++)
                    if (zone.Contains(Vector3.Lerp(from, to, step / 400f))) { sampledIntersection = true; break; }
                if (sampledIntersection) Assert.True(AoeEscapeGeometry.Intersects(zone, from, to));
            }
        }
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteInputsOrFloorDataNeverYieldAnExit(float invalid)
    {
        Assert.Null(AoeEscapePath.Check(new(invalid, 0, 0), new(8, 0, 0), [Circle], Floor, (_, _) => true));
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(8, 0, invalid), [Circle], Floor, (_, _) => true));
        Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(8, 0, 0), [Circle], p => p with { Y = invalid }, (_, _) => true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01f)]
    [InlineData(30)]
    public void MeaninglessOrOutOfRangeDirectionsAreRejected(float distance)
        => Assert.Null(AoeEscapePath.Check(Vector3.Zero, new(distance, 0, 0), [Circle], Floor, (_, _) => true));
}

public class AoeAutoTurnIntegrationTests
{
    [Theory]
    [InlineData(ConditionFlag.BetweenAreas)]
    [InlineData(ConditionFlag.BetweenAreas51)]
    [InlineData(ConditionFlag.WatchingCutscene)]
    [InlineData(ConditionFlag.WatchingCutscene78)]
    [InlineData(ConditionFlag.OccupiedInEvent)]
    [InlineData(ConditionFlag.OccupiedInQuestEvent)]
    [InlineData(ConditionFlag.OccupiedInCutSceneEvent)]
    [InlineData(ConditionFlag.InFlight)]
    [InlineData(ConditionFlag.Jumping)]
    [InlineData(ConditionFlag.Jumping61)]
    [InlineData(ConditionFlag.Diving)]
    [InlineData(ConditionFlag.Swimming)]
    [InlineData(ConditionFlag.Mounted)]
    public void NativeSceneAndMovementFlagsPreventAllCameraAndInputWrites(ConditionFlag blocked)
    {
        var player = Proxy.Of<IPlayerCharacter>((m, _) => m.Name switch
        {
            "get_CurrentHp" => 100u,
            "get_Address" => (nint)1,
            "get_GameObjectId" => 1ul,
            "get_Position" => Vector3.Zero,
            _ => throw new Exception(m.Name),
        });
        var objects = Proxy.Of<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? player : throw new Exception(m.Name));
        var client = Proxy.Of<IClientState>((m, _) => m.Name switch
        {
            "get_IsLoggedIn" => true,
            "get_TerritoryType" => 133u,
            _ => throw new Exception(m.Name),
        });
        var condition = Proxy.Of<ICondition>((m, args) => m.Name == "get_Item" && (ConditionFlag)args![0]! == blocked);
        var combat = (CombatService)RuntimeHelpers.GetUninitializedObject(typeof(CombatService));
        var escape = (EscapeRouteService)RuntimeHelpers.GetUninitializedObject(typeof(EscapeRouteService));
        Set(combat, "_escape", escape);
        Set(combat, "_autoTurnZones", new List<DangerZone> { new(DangerShape.Circle, Vector3.Zero, 3, 0, 0, 0) });
        Set(combat, "<AutoTurnInDanger>k__BackingField", true);
        var service = new AoeAutoTurnService(new Configuration { AutoTurnAoe = true }, objects, client,
            condition, combat, null!, null!, null!);
        // The native game is absent: reaching any camera/input pointer or voice
        // here would fail. These flags must stop before those calls.
        service.Update(true);
        Assert.True(escape.InDanger);
        Assert.Null(escape.SafeSpot);
    }

    [Fact]
    public void DisabledFeatureDoesNotReadNativeStateOrGameObjects()
    {
        var objects = Proxy.Of<IObjectTable>((_, _) => throw new Exception("Disabled feature must be idle."));
        new AoeAutoTurnService(new Configuration(), objects, null!, null!, null!, null!, null!, null!).Update(true);
    }

    [Fact]
    public void MissingPlayerClearsAreaDataAndGuidanceInsteadOfRetainingOldDanger()
    {
        var objects = Proxy.Of<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? null : throw new Exception(m.Name));
        var log = Proxy.Of<IPluginLog>((_, _) => null);
        var combat = (CombatService)RuntimeHelpers.GetUninitializedObject(typeof(CombatService));
        var escape = (EscapeRouteService)RuntimeHelpers.GetUninitializedObject(typeof(EscapeRouteService));
        escape.SetAutoTurnGuidance(true, new Vector3(7, 0, 0));
        Set(combat, "_objectTable", objects);
        Set(combat, "_escape", escape);
        Set(combat, "_aoeWarn", new AoeWarningService(new Configuration(), log));
        Set(combat, "_autoTurnZones", new List<DangerZone> { new(DangerShape.Circle, Vector3.Zero, 3, 0, 0, 0) });
        Set(combat, "_castsAtMe", new Dictionary<ulong, uint>());
        Set(combat, "_aoeInside", new Dictionary<ulong, uint>());
        Set(combat, "<AutoTurnInDanger>k__BackingField", true);
        Set(combat, "<AutoTurnUncertain>k__BackingField", true);
        combat.UpdateAreaWarnings();
        Assert.Empty(combat.AutoTurnZones);
        Assert.False(combat.AutoTurnInDanger);
        Assert.False(combat.AutoTurnUncertain);
        Assert.False(escape.InDanger);
        Assert.Null(escape.SafeSpot);
    }

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    public class Proxy : DispatchProxy
    {
        private Func<MethodInfo, object?[]?, object?> _call = null!;
        internal static T Of<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
        { var result = Create<T, Proxy>(); ((Proxy)(object)result)._call = call; return result; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!, args);
    }
}

[Collection("Language")]
public class AoeAutoTurnSettingTests : IDisposable
{
    private readonly LanguageMode _language = Loc.Mode;
    public AoeAutoTurnSettingTests() => Loc.Mode = LanguageMode.Russian;
    public void Dispose() => Loc.Mode = _language;

    [Theory]
    [InlineData("aoeturn on", true)]
    [InlineData("  АВТОПОВОРОТ\tВКЛ ", true)]
    [InlineData("aoeturn OFF", false)]
    [InlineData("автоповорот выкл", false)]
    public void CommandsSaveOnlyTheirSettingAndPreserveKeysAndAudioOptions(string command, bool enabled)
    {
        var config = new Configuration { AutoTurnAoe = !enabled, KeyFaceWaypoint = "Alt+F10", AnnounceAoeWarning = false };
        var saved = 0;
        Assert.Contains(enabled ? "включён" : "выключен", AoeAutoTurn.HandleCommand(command, config, () => saved++));
        Assert.Equal(enabled, config.AutoTurnAoe);
        Assert.Equal(1, saved);
        Assert.Equal("Alt+F10", config.KeyFaceWaypoint);
        Assert.False(config.AnnounceAoeWarning);
    }

    [Theory]
    [InlineData("aoeturn")]
    [InlineData("автоповорот")]
    [InlineData("aoeturn maybe")]
    [InlineData("aoeturn on extra")]
    public void StatusAndInvalidArgumentsNeverChangeSettings(string command)
    {
        var config = new Configuration();
        Assert.NotNull(AoeAutoTurn.HandleCommand(command, config, () => throw new Exception()));
        Assert.False(config.AutoTurnAoe);
    }

    [Theory]
    [InlineData("aoeturnoff")]
    [InlineData("автоповоротчик")]
    [InlineData("translate off")]
    [InlineData("")]
    public void OtherCommandsKeepTheirOriginalHandler(string command)
        => Assert.Null(AoeAutoTurn.HandleCommand(command, new(), () => throw new Exception()));

    [Fact]
    public void OlderSettingsDefaultToOffAndChoiceSurvivesReload()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        var config = JsonSerializer.Deserialize<Configuration>("{\"Version\":17,\"KeyFaceWaypoint\":\"Alt+F10\"}", options)!;
        Assert.False(config.AutoTurnAoe);
        AoeAutoTurn.HandleCommand("aoeturn on", config, () => { });
        var loaded = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config, options), options)!;
        Assert.True(loaded.AutoTurnAoe);
        Assert.Equal("Alt+F10", loaded.KeyFaceWaypoint);
    }
}
