using System.Text;
using System.Text.Json;
using FF14Accessibility;
using FF14Accessibility.Services;

namespace Regression.Tests;

[Collection("Language")]
public sealed class AuthorUpdateTests : IDisposable
{
    private readonly LanguageMode previous = Loc.Mode;
    private readonly JsonDocument sources;

    public AuthorUpdateTests()
    {
        Loc.Mode = LanguageMode.Russian;
        using var stream = typeof(AuthorUpdateTests).Assembly.GetManifestResourceStream(
            "Regression.Tests.Fixtures.author-text-sources.json")!;
        sources = JsonDocument.Parse(stream);
    }

    public void Dispose() { Loc.Mode = previous; sources.Dispose(); }

    private IEnumerable<XbmPetInfo> Pets => sources.RootElement.GetProperty("pets").EnumerateArray()
        .Select(p => new XbmPetInfo(p.GetProperty("number").GetByte(),
            p.GetProperty("name").GetString()!, p.GetProperty("habitat").GetString()!,
            p.GetProperty("description").GetString()!, p.GetProperty("place_id").GetUInt16(),
            p.GetProperty("pet_id").GetUInt32(),
            Convert.FromBase64String(p.GetProperty("name_bytes").GetString()!),
            Convert.FromBase64String(p.GetProperty("description_bytes").GetString()!)));

    private byte[] AddonSource(uint id) => Convert.FromBase64String(sources.RootElement.GetProperty("lottery")
        .EnumerateArray().Single(x => x.GetProperty("id").GetUInt32() == id).GetProperty("en_bytes").GetString()!);

    [Fact]
    public void EveryMeasuredLevemeteHasRussianSpeechWithSafeFallback()
    {
        var givers = sources.RootElement.GetProperty("givers");
        Assert.Equal(68, givers.GetArrayLength());
        foreach (var row in givers.EnumerateArray())
        {
            var id = row.GetProperty("id").GetUInt32();
            var original = row.GetProperty("name").GetString()!;
            var source = Convert.FromBase64String(row.GetProperty("name_bytes").GetString()!);
            var name = RussianAuthorText.Translate("ENpcResidentName", id, source, original);
            Assert.Matches("[А-Яа-яЁё]", name);
            Assert.DoesNotMatch("[A-Za-z]", name);
            Assert.Equal(original, RussianAuthorText.Translate("ENpcResidentName", id, [], original));
            Loc.Mode = LanguageMode.English;
            Assert.Equal(original, RussianAuthorText.Translate("ENpcResidentName", id, source, original));
            Loc.Mode = LanguageMode.Russian;
        }
    }

    [Fact]
    public void AllFiftyMeasuredBeastsHaveGuardedRussianNamesAndCompleteDescriptions()
    {
        var pets = Pets.ToList();
        Assert.Equal(50, pets.Count);
        Assert.Equal(50, pets.Select(p => p.DisplayName).Distinct().Count());
        foreach (var pet in pets)
        {
            Assert.Matches("[А-Яа-яЁё]", pet.DisplayName);
            Assert.DoesNotMatch("[A-Za-z]", pet.DisplayName);
            Assert.Matches("[А-Яа-яЁё]", pet.DisplayDescription);
            Assert.DoesNotMatch("[A-Za-z]", pet.DisplayDescription);
            Assert.True(pet.DisplayDescription.Length > 100);
            Assert.Equal(pet.Name, XbmNotebookService.ResolveGameName(pets, pet.DisplayName));
            Assert.Equal(pet.Name, XbmNotebookService.ResolveGameName(pets, pet.Name));
            var changed = pet with { EnglishName = Encoding.UTF8.GetBytes("Changed name"),
                EnglishDescription = Encoding.UTF8.GetBytes("Changed description") };
            Assert.Equal(pet.Name, changed.DisplayName);
            Assert.Equal(pet.Description, changed.DisplayDescription);
        }
        Assert.Equal("An unknown monster", XbmNotebookService.ResolveGameName(pets, "An unknown monster"));
    }

    [Theory]
    [InlineData(LanguageMode.English)]
    [InlineData(LanguageMode.German)]
    public void CachedTargetsFollowLanguageWithoutChangingTheGameLookupKey(LanguageMode language)
    {
        var pet = Pets.First();
        var target = new XbmPetTarget(pet.Number, pet.Name, "", "", 0, 0, 0, 0, null) { SourcePet = pet };
        Assert.Equal("Ку-сит", target.Name);
        Assert.Equal(pet.Name, target.RawName);
        Loc.Mode = language;
        Assert.Equal(pet.Name, target.Name);
        Assert.Equal(pet.Description, pet.DisplayDescription);
        Loc.Mode = LanguageMode.Russian;
        Assert.Equal("Ку-сит", target.Name);
        Assert.Equal(pet.Name, XbmNotebookService.ResolveGameName(Pets, target.Name));
        Assert.Equal(pet.Name, target.RawName);
    }

    [Theory]
    [InlineData("Mini Cactpot", "Мини-кактпот")]
    [InlineData("MINI-GLÜCKSKAKTOR", "Мини-кактпот")]
    [InlineData("Confirm", "Подтвердить")]
    [InlineData("Bestätigen", "Подтвердить")]
    [InlineData("Purchase", "Купить")]
    [InlineData("Kaufen", "Купить")]
    [InlineData("Quick Pick", "Случайный номер")]
    public void LotteryLabelsTranslateOnlyWithMatchingGameSource(string input, string expected)
    {
        Assert.Equal(expected, RussianLotteryText.Translate(input, AddonSource));
        Assert.Equal(input, RussianLotteryText.Translate(input, _ => Encoding.UTF8.GetBytes("changed")));
        Assert.Equal("A player named Confirm", RussianLotteryText.Translate("A player named Confirm", AddonSource));
        Loc.Mode = LanguageMode.English;
        Assert.Equal(input, RussianLotteryText.Translate(input, AddonSource));
    }

    [Theory]
    [InlineData("Select one slot to uncover.", 1)]
    [InlineData("Select three slots to uncover.", 3)]
    [InlineData("Select 2 slots to uncover.", 2)]
    [InlineData("Decke drei Felder auf.", 3)]
    [InlineData("Decke ein Feld auf.", 1)]
    public void RemainingUncoversKeepTheActualCount(string text, int count)
    {
        Assert.Equal($"Открой клетки: осталось {count}.", RussianLotteryText.Translate(text, AddonSource));
        Assert.Equal(text, RussianLotteryText.Translate(text, _ => []));
        Assert.Equal("Select eleven slots to uncover.",
            RussianLotteryText.Translate("Select eleven slots to uncover.", AddonSource));
    }

    [Fact]
    public void LotteryReadoutsKeepCellCoordinatesDigitsLeadingZeroesAndPayout()
    {
        for (var row = 1; row <= 3; row++)
        for (var col = 1; col <= 3; col++)
        {
            var covered = AccessibilityStrings.LotteryDailyCellCovered(row, col);
            Assert.Contains($"строка {row}, столбец {col}", covered);
            Assert.Contains("закрыта", covered);
            Assert.Contains($"столбец {col}, 9", AccessibilityStrings.LotteryDailyCell(row, col, "9"));
        }
        var buy = AccessibilityStrings.LotteryWeeklyBuy("Купить", "0017");
        Assert.Contains("0017", buy);
        var payout = AccessibilityStrings.LotteryDailyLaneWithPayout(
            AccessibilityStrings.LotteryDailyMajorDiagonal, "1 2 3", 6, "10,000", AccessibilityStrings.RadioSelected);
        Assert.Contains("1 2 3", payout);
        Assert.Contains("сумма 6", payout);
        Assert.Contains("10,000 МГП", payout);
        Assert.DoesNotMatch("[A-Za-z]", payout);
        var unknown = AccessibilityStrings.LotteryDailyLane(AccessibilityStrings.LotteryDailyRow(2),
            "1 ? 3", AccessibilityStrings.RadioNotSelected);
        Assert.Contains("1 ? 3", unknown);
        Assert.DoesNotContain("сумма", unknown);
        Assert.DoesNotContain("МГП", unknown);
    }

    [Fact]
    public void NewEventNameIsSourceGuardedAndOtherLanguagesKeepGameText()
    {
        var source = sources.RootElement.GetProperty("fate");
        var bytes = Convert.FromBase64String(source.GetProperty("en_bytes").GetString()!);
        Assert.Equal("Как по часам", RussianAuthorText.Translate("FateName", 1409, bytes, "Like Clockwork"));
        Assert.Equal("New title", RussianAuthorText.Translate("FateName", 1409, Encoding.UTF8.GetBytes("New title"), "New title"));
        Loc.Mode = LanguageMode.German;
        Assert.Equal("Die mechanischen Krieger", RussianAuthorText.Translate("FateName", 1409, bytes, "Die mechanischen Krieger"));
    }
}
