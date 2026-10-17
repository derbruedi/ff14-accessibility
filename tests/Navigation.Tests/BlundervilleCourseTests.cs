using System.Numerics;
using FF14Accessibility.Services;

namespace Navigation.Tests;

public class BlundervilleCourseTests
{
    [Fact]
    public void ParcoursStartPointsNorthAtTheFirstStretch()
    {
        var step = BlundervilleCourse.Guide(new Vector3(-200f, 6f, -100f));

        Assert.Equal(BlundervillePlace.Parcours, step.Place);
        Assert.Equal(-140f, step.Target.Z);
        Assert.Equal(BlundervilleNote.Wind, step.Note);
        Assert.False(step.AtTarget);
        Assert.Equal(0, RouteService.SectorBetween(new Vector3(-200f, 6f, -100f), step.Target));
    }

    [Fact]
    public void ParcoursGoalCountsAsArrived()
    {
        var step = BlundervilleCourse.Guide(new Vector3(-200f, 14f, -369f));

        Assert.Equal(BlundervillePlace.Parcours, step.Place);
        Assert.True(step.AtTarget);
    }

    [Fact]
    public void RaceAndParcoursStayApart()
    {
        Assert.Equal(BlundervillePlace.Parcours, BlundervilleCourse.Identify(new Vector3(-200f, 0f, -200f)));
        Assert.Equal(BlundervillePlace.Race, BlundervilleCourse.Identify(new Vector3(0f, 0f, -200f)));

        var race = BlundervilleCourse.Guide(new Vector3(0f, 0f, -100f));
        Assert.Equal(BlundervillePlace.Race, race.Place);
        Assert.Equal(-150f, race.Target.Z);
        Assert.Equal(BlundervilleNote.None, race.Note);
    }

    [Fact]
    public void RaceNamesTheRollingBarsBeforeThePendulum()
    {
        var bars = BlundervilleCourse.Guide(new Vector3(0f, 0f, -160f));
        var pendulum = BlundervilleCourse.Guide(new Vector3(0f, 10f, -250f));

        Assert.Equal(BlundervilleNote.RollingBar, bars.Note);
        Assert.Equal(BlundervilleNote.Pendulum, pendulum.Note);
    }

    [Fact]
    public void WhirlCenterIsTheTargetAndTheLobbyIsNotTheWhirl()
    {
        var middle = BlundervilleCourse.Guide(new Vector3(200f, 10f, -125f));
        Assert.Equal(BlundervillePlace.Whirl, middle.Place);
        Assert.True(middle.AtTarget);

        var approach = BlundervilleCourse.Guide(new Vector3(200f, 10f, -100f));
        Assert.Equal(BlundervillePlace.Whirl, approach.Place);
        Assert.False(approach.AtTarget);
        Assert.Equal(BlundervilleCourse.WhirlCenter, approach.Target);

        Assert.Equal(BlundervillePlace.Lobby, BlundervilleCourse.Identify(new Vector3(200f, 10f, 130f)));
        Assert.False(BlundervilleCourse.IsCourse(BlundervillePlace.Lobby));
    }

    [Fact]
    public void PeakStartsUphillAndEndsAtTheCrown()
    {
        var start = BlundervilleCourse.Guide(new Vector3(0f, 5f, 320f));
        Assert.Equal(BlundervillePlace.Peak, start.Place);
        Assert.Equal(290f, start.Target.Z);
        Assert.False(start.AtTarget);

        var crown = BlundervilleCourse.Guide(new Vector3(0f, 43f, 118f));
        Assert.True(crown.AtTarget);
        Assert.Equal(118f, crown.Target.Z);
    }

    [Fact]
    public void SiegeStaysOnTheMiddleUntilAFieldIsCloser()
    {
        var middle = BlundervilleCourse.Guide(BlundervilleCourse.SiegeCenter);
        Assert.Equal(BlundervillePlace.Siege, middle.Place);
        Assert.Equal(BlundervilleNote.Middle, middle.Note);
        Assert.True(middle.AtTarget);

        var towardEast = BlundervilleCourse.Guide(new Vector3(-192f, 7f, 252f));
        Assert.Equal(BlundervilleNote.Middle, towardEast.Note);

        var atEast = BlundervilleCourse.Guide(new Vector3(-166.7f, 7f, 277.1f));
        Assert.Equal(BlundervilleNote.EastField, atEast.Note);
        Assert.False(atEast.AtTarget);
    }

    [Fact]
    public void OpenSpaceBetweenCoursesIsNotACourse()
    {
        Assert.Equal(BlundervillePlace.None, BlundervilleCourse.Identify(new Vector3(0f, 0f, 0f)));
        Assert.False(BlundervilleCourse.IsCourse(BlundervillePlace.None));
    }

    [Fact]
    public void FixedTargetOnTheParcoursLineSitsEightMetresAhead()
    {
        var player = new Vector3(-200f, 6f, -100f);
        Assert.True(BlundervilleCourse.TryFixedTarget(player, out var point));

        Assert.Equal(-200f, point.X);
        Assert.Equal(-108f, point.Z);
        Assert.Equal(0f, DriftDegrees(player, point));
    }

    [Fact]
    public void OneMetreOffTheLineLeavesTheCenter()
    {
        var player = new Vector3(-199f, 6f, -100f);
        Assert.True(BlundervilleCourse.TryFixedTarget(player, out var point));

        Assert.Equal(-200f, point.X);
        Assert.Equal(-108f, point.Z);
        Assert.True(DriftDegrees(player, point) > 6f);
    }

    [Fact]
    public void ADriftOfTenMetresAimsAtTheLineItself()
    {
        var player = new Vector3(-190f, 6f, -100f);
        Assert.True(BlundervilleCourse.TryFixedTarget(player, out var point));

        Assert.Equal(-200f, point.X);
        Assert.True(point.X > -201f);
        Assert.Equal(-108f, point.Z);
    }

    [Fact]
    public void WhirlLockMovesTowardTheCenter()
    {
        Assert.True(BlundervilleCourse.TryFixedTarget(new Vector3(210f, 10f, -125f), out var point));

        Assert.Equal(202f, point.X);
        Assert.Equal(-125f, point.Z);
    }

    [Fact]
    public void LobbyAndOpenSpaceHaveNoFixedTarget()
    {
        Assert.False(BlundervilleCourse.TryFixedTarget(new Vector3(200f, 10f, 130f), out _));
        Assert.False(BlundervilleCourse.TryFixedTarget(new Vector3(0f, 0f, 0f), out _));
    }

    [Fact]
    public void AHazardKeepsTheLockOnTheMeasuredLine()
    {
        var beforeBars = new Vector3(0f, 0f, -100f);
        var inPendulum = new Vector3(0f, 10f, -250f);
        Assert.True(BlundervilleCourse.TryFixedTarget(beforeBars, out var barsLock));
        Assert.True(BlundervilleCourse.TryFixedTarget(inPendulum, out var pendulumLock));

        Assert.Equal(0f, barsLock.X);
        Assert.Equal(-108f, barsLock.Z);
        Assert.Equal(0f, pendulumLock.X);
        Assert.Equal(-258f, pendulumLock.Z);
        Assert.NotEqual(
            BlundervilleCourse.Guide(beforeBars).Target.Z,
            BlundervilleCourse.Guide(inPendulum).Target.Z);
    }

    [Fact]
    public void SiegeLockFollowsTheChosenField()
    {
        Assert.True(BlundervilleCourse.TryFixedTarget(new Vector3(-192f, 7f, 252f), out var middle));
        Assert.Equal(BlundervilleCourse.SiegeCenter, middle);

        Assert.True(BlundervilleCourse.TryFixedTarget(new Vector3(-166.7f, 7f, 277.1f), out var east));
        Assert.Equal(-158.7f, east.X, 3);
        Assert.Equal(277.1f, east.Z, 3);
        Assert.True(east.X < -156.7f);
    }

    private static float DriftDegrees(Vector3 player, Vector3 point)
    {
        var cross = MathF.Abs(point.X - player.X);
        var along = MathF.Abs(point.Z - player.Z);
        return MathF.Atan2(cross, along) * (180f / MathF.PI);
    }
}
