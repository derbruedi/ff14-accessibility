using System.Numerics;
using FF14Accessibility.Services;

namespace Navigation.Tests;

public class TimedEventSelectionTests
{
    private static TimedEventQuest Quest(
        uint festival, byte begin, byte end, uint questId, uint territory, string name = "Event")
        => new(festival, begin, end, questId, name, territory, 1, "Zone", new Vector3(questId, 0, territory));

    [Fact]
    public void InactiveFestivalIsAbsent()
    {
        var quests = new[] { Quest(2, 0, 1, 10, 133, "Nacht") };
        var picked = TimedEventSelection.Pick(new[] { ((ushort)9, (ushort)1) }, quests);
        Assert.Empty(picked);
    }

    [Fact]
    public void EndedPhaseDropsTheEvent()
    {
        var quests = new[] { Quest(2, 0, 1, 10, 133) };
        Assert.Empty(TimedEventSelection.Pick(new[] { ((ushort)2, (ushort)2) }, quests));
        Assert.Single(TimedEventSelection.Pick(new[] { ((ushort)2, (ushort)1) }, quests));
    }

    [Fact]
    public void NewFestivalAppearsWithOneStartPerTerritory()
    {
        var quests = new[]
        {
            Quest(2, 0, 1, 30, 133, "Spaeter"),
            Quest(2, 0, 1, 10, 133, "Start Gridania"),
            Quest(2, 0, 1, 11, 128, "Start Limsa"),
            Quest(4, 1, 3, 40, 137, "Anderes Event"),
        };

        var picked = TimedEventSelection.Pick(
            new[] { ((ushort)2, (ushort)1), ((ushort)4, (ushort)2) },
            quests);

        Assert.Equal(3, picked.Count);
        Assert.Contains(picked, d => d.EventName == "Start Gridania" && d.TerritoryId == 133);
        Assert.Contains(picked, d => d.EventName == "Start Limsa" && d.TerritoryId == 128);
        Assert.Contains(picked, d => d.EventName == "Anderes Event" && d.TerritoryId == 137);
        Assert.DoesNotContain(picked, d => d.EventName == "Spaeter");
    }

    [Fact]
    public void EmptyFestivalSlotAndDuplicateIdAreIgnored()
    {
        var quests = new[] { Quest(2, 0, 1, 10, 133, "Einmal") };
        var picked = TimedEventSelection.Pick(
            new[] { ((ushort)0, (ushort)1), ((ushort)2, (ushort)0), ((ushort)2, (ushort)0) },
            quests);
        Assert.Single(picked);
    }

    private static TimedEventFate Fate(
        uint festival, byte begin, byte end, uint fateId, uint territory, string name, Vector3 position, string hint = "FATE")
        => new(festival, begin, end, fateId, name, territory, 2, "Feld", position, hint);

    [Fact]
    public void FateScriptSlotIsOnlyTheFateInstruction()
    {
        Assert.True(TimedEventSelection.IsFateScript("FATE0"));
        Assert.True(TimedEventSelection.IsFateScript("FATE12"));
        Assert.False(TimedEventSelection.IsFateScript("FATE"));
        Assert.False(TimedEventSelection.IsFateScript("FATE0A"));
        Assert.False(TimedEventSelection.IsFateScript("LOC_MOTION0"));
        Assert.False(TimedEventSelection.IsFateScript("fate0"));
    }

    [Fact]
    public void ActiveFestivalListsNamedFateSpotsBesideTheStart()
    {
        var quests = new[] { Quest(37, 0, 1, 10, 128, "Start Limsa") };
        var fates = new[]
        {
            Fate(37, 0, 1, 946, 137, "Die Rückkehr des Königs", new Vector3(538, 9, 606)),
            Fate(11, 0, 1, 100, 141, "Anderes Fest", new Vector3(1, 0, 1)),
        };

        var picked = TimedEventSelection.Pick(new[] { ((ushort)37, (ushort)1) }, quests, fates);

        Assert.Equal(2, picked.Count);
        Assert.Contains(picked, d => d.EventName == "Start Limsa" && d.Hint.Length == 0);
        Assert.Contains(picked, d =>
            d.EventName == "Die Rückkehr des Königs" && d.TerritoryId == 137 && d.Hint == "FATE");
        Assert.DoesNotContain(picked, d => d.EventName == "Anderes Fest");
    }

    [Fact]
    public void FateOutsideTheLivePhaseIsAbsent()
    {
        var fates = new[] { Fate(22, 5, 6, 911, 137, "Bugs", new Vector3(10, 0, 10)) };
        Assert.Empty(TimedEventSelection.Pick(new[] { ((ushort)22, (ushort)1) }, Array.Empty<TimedEventQuest>(), fates));
        Assert.Single(TimedEventSelection.Pick(new[] { ((ushort)22, (ushort)5) }, Array.Empty<TimedEventQuest>(), fates));
    }

    [Fact]
    public void TwoFatesAtTheSameSpotCollapseToTheLowerId()
    {
        var fates = new[]
        {
            Fate(32, 0, 1, 940, 148, "Später", new Vector3(127.4f, 59f, 238.4f)),
            Fate(32, 0, 1, 936, 148, "Früher", new Vector3(127.2f, 59f, 238.2f)),
        };

        var picked = TimedEventSelection.Pick(new[] { ((ushort)32, (ushort)0) }, Array.Empty<TimedEventQuest>(), fates);

        var only = Assert.Single(picked);
        Assert.Equal("Früher", only.EventName);
    }
}
