using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

public class JobProcTests
{
    private readonly JobProcTracker _tracker = new();
    private uint[] Observe(uint[] statuses, uint job = 23, ulong player = 1, uint zone = 133)
    {
        var ready = new List<uint>();
        _tracker.Collect(player, job, zone, statuses, ready);
        return ready.ToArray();
    }

    [Fact]
    public void HawksEyeAnnouncesOnlyWhenItAppearsAndCanProcAgain()
    {
        Assert.Empty(Observe([]));
        Assert.Equal([3861u], Observe([3861]));
        Assert.Empty(Observe([3861]));
        Assert.Empty(Observe([]));
        Assert.Equal([3861u], Observe([3861]));
    }

    [Fact]
    public void ActiveEffectsOnLoginAreSilentButNewEffectsStillAnnounce()
    {
        Assert.Empty(Observe([3861]));
        Assert.Equal([3862u], Observe([3861, 3862]));
    }

    [Fact]
    public void SlotOrderDuplicatesAndRefreshesDoNotRepeatTheEffect()
    {
        Observe([]);
        Assert.Equal([3861u, 3862u], Observe([3862, 0, 3861, 3861]));
        Assert.Empty(Observe([3861, 3862]));
        Assert.Empty(Observe([3862, 3861, 0]));
    }

    [Fact]
    public void UnknownEnemyAndOtherJobStatusesStaySilent()
    {
        Observe([]);
        Assert.Empty(Observe([2, 124, 999999, 1234, 2693, 3142]));
        Assert.Equal([3861u], Observe([2, 124, 1234, 3861]));
    }

    [Theory]
    [InlineData(2ul, 23u, 133u)]
    [InlineData(1ul, 5u, 133u)]
    [InlineData(1ul, 23u, 134u)]
    public void NewPlayerClassOrZoneStartsWithSilentSnapshot(ulong player, uint job, uint zone)
    {
        Observe([]);
        Assert.Empty(Observe([3861], job, player, zone));
        Assert.Empty(Observe([], job, player, zone));
        Assert.Equal([3861u], Observe([3861], job, player, zone));
    }

    [Fact]
    public void ResetForDisabledLoggedOutMissingOrDeadPlayerDropsOldState()
    {
        Observe([]);
        _tracker.Reset();
        Assert.Empty(Observe([3861]));
        Assert.Empty(Observe([]));
        Assert.Equal([3861u], Observe([3861]));
    }

    public static IEnumerable<object[]> Effects() => JobProcCatalog.All.Select(x => new object[] { x.StatusId, (uint)x.JobId });

    [Theory]
    [MemberData(nameof(Effects))]
    public void EverySupportedEffectIsDetectedForItsOwnJob(uint status, uint job)
    {
        Observe([], job);
        Assert.Equal([status], Observe([status], job));
        Assert.Empty(Observe([status], job));
    }

    [Fact]
    public void CatalogHasUniqueIdsAndChecksExactGameNames()
    {
        Assert.Equal(JobProcCatalog.All.Count, JobProcCatalog.All.Select(x => x.StatusId).Distinct().Count());
        foreach (var entry in JobProcCatalog.All)
        {
            Assert.True(entry.MatchesSource(entry.EnglishName));
            Assert.False(entry.MatchesSource(entry.EnglishName + " changed"));
            Assert.Null(JobProcCatalog.Find(entry.StatusId, 8)); // carpenter
            if (entry.BaseClassId != 0) Assert.Same(entry, JobProcCatalog.Find(entry.StatusId, entry.BaseClassId));
        }
        Assert.NotNull(JobProcCatalog.Find(3861, 5)); // archer
        Assert.Null(JobProcCatalog.Find(3895, 40)); // Sun Sign belongs to AST, not SGE
        Assert.NotNull(JobProcCatalog.Find(3895, 33));
        Assert.Null(JobProcCatalog.Find(3852, 34)); // already announced by SAM gauge
    }

    [Fact]
    public void SimultaneousCooldownProcAndGaugeUseOneWarningVoiceCall()
    {
        var batch = new ReadyAnnouncementBatch();
        batch.Add("Sprint ready.");
        batch.Add("Effect: Hawk's Eye.");
        batch.Add("Soul Voice full.");
        var calls = new List<string>();
        batch.Speak(text => { calls.Add(text); return true; }, _ => throw new Exception("Unexpected fallback"));
        Assert.Equal("Sprint ready. Effect: Hawk's Eye. Soul Voice full.", Assert.Single(calls));
        Assert.Equal(0, batch.Count);
        batch.Speak(_ => throw new Exception("Stale speech"), _ => throw new Exception("Stale speech"));
    }

    [Fact]
    public void UnavailableOrDisabledWarningVoiceFallsBackOnceWithEveryMessage()
    {
        var batch = new ReadyAnnouncementBatch();
        batch.Add("Effect: Verfire Ready.");
        batch.Add("Effect: Verstone Ready.");
        batch.Add("Effect: Verfire Ready.");
        var fallback = new List<string>();
        var calls = 0;
        batch.Speak(_ => { calls++; return false; }, fallback.Add);
        Assert.Equal(1, calls);
        Assert.Equal("Effect: Verfire Ready. Effect: Verstone Ready.", Assert.Single(fallback));
    }

    [Fact]
    public void EmptyAndCancelledBatchesNeverSpeak()
    {
        var batch = new ReadyAnnouncementBatch();
        batch.Add(" ");
        batch.Speak(_ => throw new Exception("Empty speech"), _ => throw new Exception("Empty speech"));
        batch.Add("Old effect.");
        batch.Clear();
        batch.Speak(_ => throw new Exception("Cancelled speech"), _ => throw new Exception("Cancelled speech"));
    }
}

[Collection("Language")]
public class JobProcLanguageTests
{
    [Theory]
    [InlineData(LanguageMode.Russian, "Эффект: Орлиный глаз.")]
    [InlineData(LanguageMode.English, "Effect: Орлиный глаз.")]
    [InlineData(LanguageMode.German, "Effekt: Орлиный глаз.")]
    public void EffectAnnouncementUsesTheSelectedLanguage(LanguageMode language, string expected)
    {
        var original = Loc.Mode;
        try { Loc.Mode = language; Assert.Equal(expected, AccessibilityStrings.JobProcReady("Орлиный глаз")); }
        finally { Loc.Mode = original; }
    }
}
