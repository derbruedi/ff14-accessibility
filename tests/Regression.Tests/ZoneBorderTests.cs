using System.Numerics;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class ZoneBorderTests
{
    [Fact]
    public void LoggedMiddleLaNosceaFalseArrivalIsOutsideTheGameRotatedVolume()
    {
        var border = new TravelBorder(new(-59.0843f, 83.72464f, 161.7435f),
            new(7.356031f, 53.04695f, 7.364943f), -1.1519173f, 129);
        var falseArrival = new Vector3(-62.839893f, 42.263397f, 154.52989f);
        Assert.False(border.Contains(falseArrival));
        Assert.All(ZoneBorderService.Candidates(border, falseArrival), p =>
        {
            var local = Vector3.Transform(p - border.Centre, Matrix4x4.CreateRotationY(-border.Yaw));
            Assert.InRange(MathF.Abs(local.X), 0, border.HalfExtent.X);
            Assert.InRange(MathF.Abs(local.Z), 0, border.HalfExtent.Z);
        });
        var target = ZoneBorderService.Resolve([border], falseArrival, (p, _, _) => p).Position!.Value;
        Assert.True(new TravelMapRange(12, border.Centre, new(0, border.Yaw, 0), border.HalfExtent, 1, 1).Contains(target));
    }

    [Theory]
    [InlineData(0.8845961f)]
    [InlineData(-1.1519173f)]
    [InlineData(1.5707963f)]
    public void BorderAndMapRangeUseTheSameGameRotation(float yaw)
    {
        var border = new TravelBorder(new(10, 5, 30), new(3, 7, 12), yaw, 129);
        var knownLocal = new Vector3(2, 0, 10);
        var world = border.Centre + Vector3.Transform(knownLocal, Matrix4x4.CreateRotationY(yaw));
        Assert.True(Vector3.Distance(knownLocal, border.Local(world)) < 0.00001f);
        Assert.True(border.Contains(world));
        Assert.True(Vector3.Distance(world, border.World(knownLocal)) < 0.00001f);
    }

    // Recorded Middle -> Lower La Noscea ExitRange and the stalled mesh edge.
    private static TravelBorder LaNoscea => new(new(218.7f, 99.8f, 285.5f), new(8.3f, 35.7f, 9f), 0, 135);
    private static Vector3 Edge => new(207, 71.75f, 275);

    [Fact]
    public void TallTriggerUsesRoadHeightAndKeepsDestinationInsideTheRealVolume()
    {
        var calls = new List<(Vector3 Point, float Xz, float Y)>();
        var result = ZoneBorderService.Resolve([LaNoscea], new(207.14691f, 71.745f, 274.9847f), (p, x, y) =>
        {
            calls.Add((p, x, y));
            return MathF.Abs(p.X - Edge.X) <= x && MathF.Abs(p.Z - Edge.Z) <= x
                && MathF.Abs(p.Y - Edge.Y) <= y ? Edge : null;
        });
        Assert.True(result.HasBorder);
        var target = Assert.IsType<Vector3>(result.Position);
        Assert.Equal(Edge.Y, target.Y);
        Assert.True(LaNoscea.Contains(target));
        Assert.InRange(Vector3.Distance(Edge, target), 0, 5.5f);
        Assert.All(calls, c => Assert.InRange(MathF.Abs(c.Point.Y - Edge.Y), 0, 0.01f));
        // Broad generic snapping would move this target back outside the trigger.
        Assert.True(target.X > Edge.X && target.Z > Edge.Z);
    }

    [Fact]
    public void RoadNearPlayerHeightWinsBeforeAnUpperSurfaceNearTheBoxCentre()
    {
        var result = ZoneBorderService.Resolve([LaNoscea], Edge, (p, x, y) =>
            p.Y > 90 ? new(211.4f, 91.5f, 278.7f)
                : x >= 6 ? Edge : null);
        Assert.Equal(Edge.Y, result.Position!.Value.Y);
    }

    [Fact]
    public void DistantPlayerHeightCanResolveFloorAcrossTheTriggersOwnHeight()
    {
        var result = ZoneBorderService.Resolve([LaNoscea], new(180, 10, 240), (p, x, y) =>
            y > 30 && x >= 6 ? Edge : null);
        Assert.NotNull(result.Position);
        Assert.Equal(Edge.Y, result.Position.Value.Y);
    }

    [Fact]
    public void EveryRejectedCandidateReturnsFailureInsteadOfAGuessedCentre()
    {
        var count = 0;
        var result = ZoneBorderService.Resolve([LaNoscea], Edge, (_, _, _) => { count++; return null; });
        Assert.True(result.HasBorder); Assert.Null(result.Position);
        Assert.InRange(count, 1, 36);
        Assert.False(ZoneBorderService.Resolve([], Edge, (_, _, _) => null).HasBorder);
    }

    [Theory]
    [InlineData(200, 71.75f, 275)] // Too far away to nudge.
    [InlineData(211, 150, 279)] // Above trigger.
    [InlineData(211, 60, 279)] // Below trigger.
    [InlineData(float.NaN, 71, 279)]
    [InlineData(211, float.PositiveInfinity, 279)]
    public void InvalidMeshAnswersNeverAuthorizeWalking(float x, float y, float z)
        => Assert.Null(ZoneBorderService.Resolve([LaNoscea], Edge, (_, _, _) => new(x, y, z)).Position);

    [Fact]
    public void WalkableAlternativeBorderIsTriedWhenNearestBorderIsBlocked()
    {
        var far = LaNoscea with { Centre = LaNoscea.Centre + new Vector3(100, 0, 0) };
        var result = ZoneBorderService.Resolve([LaNoscea, far], Edge, (p, _, _) => p.X > 300 ? p : null);
        Assert.NotNull(result.Position); Assert.True(far.Contains(result.Position.Value));
    }

    [Fact]
    public void CandidateSpreadFindsAGateAwayFromTheNearestBlockedPoint()
    {
        var border = new TravelBorder(Vector3.Zero, new(10, 5, 20), 0, 1);
        var result = ZoneBorderService.Resolve([border], new(-15, 0, 0), (p, _, _) => p.Z >= 6 ? p : null);
        Assert.True(result.Position!.Value.Z >= 6); Assert.True(border.Contains(result.Position.Value));
    }

    [Fact]
    public void RotatedAndThinVolumesKeepAllCandidatesInside()
    {
        foreach (var half in new[] { new Vector3(10, 5, 20), new Vector3(0.4f, 0.2f, 0.3f) })
        {
            var border = new TravelBorder(new(20, 7, 30), half, 0.8f, 1);
            Assert.All(ZoneBorderService.Candidates(border, new(-15, -10, 20)), p => Assert.True(border.Contains(p)));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DisconnectedFloorRequiresAnExistingMeasuredCrossing(bool crossing)
    {
        var result = ZoneBorderService.Resolve([LaNoscea], Edge, (_, _, _) => null,
            p => p with { Y = Edge.Y }, _ => crossing);
        Assert.Equal(crossing, result.Position != null);
        if (crossing) Assert.True(LaNoscea.Contains(result.Position!.Value));
    }
}
