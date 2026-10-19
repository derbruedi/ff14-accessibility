using System.Numerics;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class QuestObjectiveTargetTests
{
    private static QuestDestination Serum => new("Сыворотка от навязчивости", "",
        new(-940.165f, 254.54f, -22.0545f), 5.27859f, 155, 53, true, QuestKind.SideQuest, 40,
        TargetLevelType: 49, QuestId: 66485, ObjectiveLevelId: 4308186, AreaTargetLevelId: 4298945);

    [Fact]
    public void RecordedLowerLedgeCannotBeAnApproachToTheRescueTrigger()
        => Assert.Null(QuestObjectiveTarget.TriggerPoint(Serum, 1.3f,
            (_, _, _) => new(-925.25f, 231.75f, -14)));

    [Fact]
    public void RescueApproachLeavesRoomToStopInsideTheActualTrigger()
    {
        var floor = Serum.Position with { Y = 254.4f };
        Assert.Equal(floor, QuestObjectiveTarget.TriggerPoint(Serum, Serum.Radius / 4, (_, _, _) => floor));
    }

    [Theory]
    [InlineData(-936, 254.54f, -22)] // Stopping short could leave the trigger.
    [InlineData(-940.165f, 231.75f, -22)] // Same XZ, wrong floor.
    [InlineData(float.NaN, 254, -22)]
    public void TriggerRejectsOutsidePointsAndAnotherFloor(float x, float y, float z)
        => Assert.Null(QuestObjectiveTarget.TriggerPoint(Serum, 1.3f, (_, _, _) => new(x, y, z)));

    [Fact]
    public void TriggerCannotFallBackToAnotherTriggerOfTheSameQuest()
    {
        var trigger = Serum with { Role = QuestMarkerRole.QuestTrigger, ObjectiveLevelId = 4298945 };
        var next = trigger with { ObjectiveLevelId = 999, Position = Vector3.Zero };
        Assert.Null(QuestNavigationPlan.Refresh(trigger, [next, Serum], 155, 53, Vector3.Zero));
        Assert.Same(trigger, QuestNavigationPlan.Refresh(trigger, [next, trigger], 155, 53, Vector3.Zero));
    }

    [Fact]
    public void RemainingSearchAreasPreferGroundNearMarkerHeightBeforeAnotherFloor()
    {
        var centre = new Vector3(-935.105f, 244.504f, -21.4385f);
        var lower = new Vector3(-925.25f, 231.75f, -14);
        var upper = new Vector3(-940, 254.4f, -22);
        var result = QuestAreaPoint.Resolve(centre, 20, 1, (p, _, _) =>
            p.X < -938 ? upper : lower, _ => true);
        Assert.Equal(upper, result);
    }
}
