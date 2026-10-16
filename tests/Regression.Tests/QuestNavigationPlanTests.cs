using System.Numerics;
using FF14Accessibility.Services;

namespace Regression.Tests;

public sealed class QuestNavigationPlanTests
{
    private static QuestDestination Goal(uint quest = 65640, uint level = 5013506, uint map = 548, ushort territory = 129)
        => new("Два клинка в руках", "", new(-153, -129, 265), 1, territory, map, false, QuestKind.Job, 1,
            TargetBaseId: 1009943, TargetLevelType: 8, QuestId: quest, ObjectiveLevelId: level);

    [Fact]
    public void AnotherMapOfTheSameTerritoryRoutesToTheGuildGuard()
    {
        var entrance = new InteriorEntrance(12, 548, 1009944, 8, new(-152, 2.8f, 243));
        var plan = QuestNavigationPlan.Resolve(Goal(), 129, 12,
            id => new("Гильдия", "Entrance", entrance.Position, false, id), _ => entrance);
        Assert.NotNull(plan);
        Assert.Equal(entrance.Position, plan.Position);
        Assert.Same(entrance, plan.Entrance);
        Assert.False(plan.HeightIsGuess);
        Assert.False(plan.IsBorder);
    }

    [Fact]
    public void InsideGuildUsesActualObjectiveHeightWithoutAnotherEntrance()
    {
        var plan = QuestNavigationPlan.Resolve(Goal(), 129, 548,
            _ => throw new Exception("Unnecessary route"), _ => throw new Exception("Unnecessary entrance"));
        Assert.Equal(-129f, plan!.Position.Y);
        Assert.Equal(Goal().QuestName, plan.Name);
        Assert.False(plan.HeightIsGuess);
    }

    [Fact]
    public void PhysicalBorderCarriesTheNudgeFlagButTransportDoesNot()
    {
        var plan = QuestNavigationPlan.Resolve(Goal(), 132, 2,
            _ => new("Граница", "Übergang", new(150, 0, 155), true, 4), _ => null);
        Assert.True(plan!.IsBorder);
        Assert.True(plan.HeightIsGuess);
        Assert.Equal(4u, plan.NextMapId);
    }

    [Fact]
    public void MissingRouteNeverFallsBackToCoordinatesFromAnotherZone()
        => Assert.Null(QuestNavigationPlan.Resolve(Goal(), 132, 2, _ => null, _ => null));

    [Fact]
    public void AlternativeRowOfExactSamePhysicalMapDoesNotRequireAnEntrance()
    {
        var plan = QuestNavigationPlan.Resolve(Goal(map: 94, territory: 212), 212, 80,
            _ => throw new Exception("False cross-zone route"), _ => throw new Exception("False entrance"),
            (left, right) => left is 80 or 94 && right is 80 or 94);
        Assert.Equal(Goal().Position, plan!.Position);
    }

    [Fact]
    public void CrossingZoneRefreshesFlagsAndRetainsTheSameObjective()
    {
        var updated = Goal() with { InCurrentZone = true };
        Assert.Same(updated, QuestNavigationPlan.Refresh(Goal(), [updated], 129, 548, Vector3.Zero));
    }

    [Fact]
    public void DialogueProgressSelectsTheNewObjectiveOfThatQuest()
    {
        var next = Goal(level: 5013509, map: 12) with { TargetBaseId = 1009944, Position = new(-152, 2.8f, 243) };
        Assert.Same(next, QuestNavigationPlan.Refresh(Goal(), [next, Goal(quest: 66043)], 129, 12, Vector3.Zero));
    }

    [Fact]
    public void TranslationChangeCannotChangeTheQuestIdentity()
    {
        var next = Goal() with { QuestName = "Stabbers in Yer Fambles" };
        Assert.Same(next, QuestNavigationPlan.Refresh(Goal(), [next], 129, 548, Vector3.Zero));
    }

    [Fact]
    public void EqualNameOfAnotherQuestCannotReplaceAMissingObjective()
        => Assert.Null(QuestNavigationPlan.Refresh(Goal(), [Goal(quest: 66043)], 132, 2, Vector3.Zero));

    [Fact]
    public void AmbiguousTranslatedNameUsesNativeMarkerIdentityInstead()
    {
        var previous = Goal(quest: 0) with { NativeMarkerId = 10 };
        var wrong = Goal(quest: 0) with { NativeMarkerId = 11 };
        var right = Goal(quest: 0) with { NativeMarkerId = 10, QuestName = "Original name" };
        Assert.Same(right, QuestNavigationPlan.Refresh(previous, [wrong, right], 129, 548, Vector3.Zero));
        Assert.Null(QuestNavigationPlan.Refresh(previous, [wrong], 129, 548, Vector3.Zero));
    }

    [Fact]
    public void LoadingOrCompletionCannotReuseAnOldMarker()
        => Assert.Null(QuestNavigationPlan.Refresh(Goal(), [], 132, 2, Vector3.Zero));

    [Fact]
    public void ParallelObjectivesDoNotSwitchJustBecauseAnotherIsCloser()
    {
        var other = Goal(level: 999) with { Position = Vector3.Zero };
        var same = Goal() with { InCurrentZone = true };
        Assert.Same(same, QuestNavigationPlan.Refresh(Goal(), [other, same], 129, 548, Vector3.Zero));
    }

    [Fact]
    public void TeleportHintChoosesShortestVerifiedContinuationThenActualDistance()
    {
        var close = new QuestTeleportChoice(1, 10, "Ближе", 0, 10);
        var far = new QuestTeleportChoice(2, 10, "Дальше", 0, 100);
        var detour = new QuestTeleportChoice(3, 20, "Обход", 1, 1);
        Assert.Same(close, QuestTravelHints.Choose([detour, far, close]));
        Assert.Null(QuestTravelHints.Choose([]));
    }

    [Fact]
    public void DutyObjectiveFirstUsesTheVerifiedExteriorDoorAndPreservesQuestIdentity()
    {
        var original = Goal(map: 37, territory: 1042);
        var duty = new DutyEntrance("Каменная Стража", 2, "Подземелье", 41, 1, 155, 26,
            new(10, 20, 30), "Коэртас", 2000001);
        var travel = QuestTravelHints.DutyGoal(original, duty, 26);
        Assert.Equal(original.QuestId, travel.QuestId);
        Assert.Equal(original.ObjectiveLevelId, travel.ObjectiveLevelId);
        Assert.Equal(2000001u, travel.TargetBaseId);
        Assert.Equal((byte)45, travel.TargetLevelType);
        Assert.Equal(26u, travel.MapId);
        Assert.Equal(duty.Position, QuestNavigationPlan.Resolve(travel, 155, 26, _ => null, _ => null)!.Position);
    }
}
