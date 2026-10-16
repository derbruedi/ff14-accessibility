using System.Numerics;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class NavigationPathCheckTests
{
    [Fact]
    public void AppendedDestinationDoesNotHideTheRecordedNineMetreShortfall()
    {
        var goal = new Vector3(-12.619263f, 82.99987f, 4.562378f);
        Assert.False(NavigationPathCheck.Reaches([new(-43, 84, 0), new(-21.8f, 83.2f, 5.5f), goal], goal, 2.5f));
        Assert.False(NavigationPathCheck.Reaches([goal], goal, 2.5f));
        Assert.True(NavigationPathCheck.Reaches([Vector3.Zero, goal, goal], goal, 2.5f));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-1f)]
    public void InvalidPathRangesAreRejected(float range)
        => Assert.False(NavigationPathCheck.Reaches([Vector3.Zero, Vector3.Zero], Vector3.Zero, range));

    [Fact]
    public void ASearchCircleTriesOtherInteriorGroundWithoutLeavingItsNativeRadius()
    {
        var centre = new Vector3(205.8f, 32.5f, 40.7f);
        var points = QuestAreaPoint.ReachablePoints(centre, 35, 5, (p, _, _) => p with { Y = 48.1f }, _ => true).ToArray();
        var check = new NavigationPathCheck(Vector3.Zero, points[0], 5,
            points.Select(p => new NavigationPathCheck.Choice(p)), (_, to, _) => Task.FromResult<List<Vector3>>(
                [Vector3.Zero, to == points[0] ? Vector3.Zero : to, to]));
        Run(check);
        Assert.NotEqual(points[0], check.Result!.Position);
        Assert.All(points, p => Assert.True(Vector2.Distance(new(centre.X, centre.Z), new(p.X, p.Z)) + 5 < 35));
    }

    [Fact]
    public void SynchronousIpcFailureDoesNotStartMovementOrEscapeTheSearch()
    {
        var check = new NavigationPathCheck(Vector3.Zero, Vector3.One, 2.5f, [new(Vector3.One)],
            (_, _, _) => throw new InvalidOperationException("IPC unloaded"));
        Run(check); Assert.Null(check.Result); Assert.Equal(1, check.Requests);
    }

    [Fact]
    public void BorderSearchTriesAnotherOpeningAfterAnIncompletePath()
    {
        var first = new Vector3(10, 0, 0); var second = new Vector3(20, 0, 0);
        var check = new NavigationPathCheck(Vector3.Zero, first, 5.2f, [new(first), new(second)],
            (_, to, _) => Task.FromResult<List<Vector3>>([Vector3.Zero, to == first ? Vector3.Zero : to, to]));
        Run(check);
        Assert.Equal(second, check.Result!.Position); Assert.Equal(2, check.Requests);
    }

    [Fact]
    public void LocalGateRequiresAPathToTheActorAndAPathFromItsArrivalToTheGoal()
    {
        var gate = new LocalTransfer(70, 1004434, 8, new(-27.17f, 83.2f, 2.3f), new(-16, 83, 5), 131128);
        var goal = new Vector3(-12.62f, 83, 4.56f); var calls = new List<(Vector3 From, Vector3 To)>();
        var check = new NavigationPathCheck(new(-43, 84, 0), goal, 2.5f, [new(goal), new(gate.Position, gate)],
            (from, to, _) => { calls.Add((from, to)); return Task.FromResult<List<Vector3>>(
                [from, calls.Count == 1 ? new(-21.8f, 83.2f, 5.5f) : to, to]); });
        Run(check);
        Assert.Equal(gate, check.Result!.Transfer); Assert.Equal(3, check.Requests);
        Assert.Equal((gate.Arrival, goal), calls[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATransferWithEitherDisconnectedLegIsNotSuggested(bool firstLeg)
    {
        var gate = new LocalTransfer(1, 10, 8, new(10, 0, 0), new(20, 0, 0), 100);
        var goal = new Vector3(30, 0, 0);
        var check = new NavigationPathCheck(Vector3.Zero, goal, 2.5f, [new(gate.Position, gate)],
            (from, to, _) => Task.FromResult<List<Vector3>>([from, (from == Vector3.Zero) == firstLeg ? from : to, to]));
        Run(check); Assert.Null(check.Result);
    }

    [Fact]
    public void PendingQueryIsNeverReadEarlyAndCancellationDoesNotReviveIt()
    {
        var task = new TaskCompletionSource<List<Vector3>>();
        var check = new NavigationPathCheck(Vector3.Zero, Vector3.One, 2.5f, [new(Vector3.One)], (_, _, _) => task.Task);
        check.Poll(); check.Poll(); Assert.False(check.Completed); Assert.Equal(1, check.Requests);
        check.Cancel(); task.SetResult([Vector3.Zero, Vector3.One, Vector3.One]); check.Poll();
        Assert.True(check.Completed); Assert.Null(check.Result); Assert.Equal(1, check.Requests);
    }

    [Fact]
    public void MissingOrFailedMeshIsAnExplicitFailureAndRequestsAreBounded()
    {
        var check = new NavigationPathCheck(Vector3.Zero, Vector3.One, 2.5f,
            Enumerable.Repeat(new NavigationPathCheck.Choice(Vector3.One), 1000), (_, _, _) => null);
        Run(check); Assert.Null(check.Result); Assert.Equal(64, check.Requests);
        var fault = new NavigationPathCheck(Vector3.Zero, Vector3.One, 2.5f, [new(Vector3.One)],
            (_, _, _) => Task.FromException<List<Vector3>>(new InvalidOperationException("mesh unloaded")));
        Run(fault); Assert.Null(fault.Result);
    }

    [Fact]
    public void InvalidWaypointsAreRejectedEvenIfTheEndpointMatches()
        => Assert.False(NavigationPathCheck.Reaches([new(float.NaN, 0, 0), Vector3.One, Vector3.One], Vector3.One, 2.5f));

    [Fact]
    public void TransferArrivalRequiresTheFarSideAndCannotCancelAtTheActor()
    {
        var gate = new LocalTransfer(70, 1004434, 8, new(-27.1759f, 83.2f, 2.30538f), new(-17.5367f, 83, 3.71234f), 131128);
        Assert.False(gate.HasArrived(gate.Position));
        Assert.False(gate.HasArrived(gate.Position + new Vector3(2.5f, 0, 0)));
        Assert.True(gate.HasArrived(gate.Arrival));
        Assert.False(gate.HasArrived(new(float.NaN, 83, 3.7f)));
    }

    private static void Run(NavigationPathCheck check)
    {
        for (var i = 0; i < 130 && !check.Completed; i++) check.Poll();
        Assert.True(check.Completed);
    }
}
