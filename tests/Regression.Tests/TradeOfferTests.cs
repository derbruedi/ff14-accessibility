using FF14Accessibility;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Regression.Tests;

[Collection("Language")]
public sealed class TradeOfferTests : IDisposable
{
    private readonly LanguageMode _previous = Loc.Mode;

    public TradeOfferTests() => Loc.Mode = LanguageMode.German;

    public void Dispose() => Loc.Mode = _previous;

    [Fact]
    public void OpeningReadsBothSidesAndSkipsEmptySlots()
    {
        var view = View(
            "Daijin Replicant",
            mine: new TradeSide([], 0, TradeState.SelectingTradeGoods),
            theirs: new TradeSide(
            [
                new TradeGoods(0, 1, false),
                new TradeGoods(44, 2, true),
            ], 0, TradeState.SelectingTradeGoods));

        var speech = TradeOfferSpeech.Next(null, view, Name);

        Assert.Equal(
            "Handel mit Daijin Replicant. Daijin Replicant bietet 2 mal Trank Hoch-Qualität, 0 Gil. Du bietest nichts, 0 Gil.",
            speech);
    }

    [Fact]
    public void SameOfferIsSilent()
    {
        var view = View("Daijin Replicant",
            new TradeSide([new TradeGoods(44, 1, false)], 10, TradeState.SelectingTradeGoods),
            new TradeSide([], 0, TradeState.SelectingTradeGoods));

        var first = TradeOfferSpeech.Next(null, view, Name);
        Assert.NotNull(first);
        Assert.Null(TradeOfferSpeech.Next(view, view, Name));
    }

    [Fact]
    public void ChangedGilRepeatsThatSide()
    {
        var before = View("Daijin Replicant",
            new TradeSide([], 0, TradeState.SelectingTradeGoods),
            new TradeSide([new TradeGoods(44, 1, false)], 0, TradeState.SelectingTradeGoods));
        var after = before with
        {
            Theirs = before.Theirs with { Gil = 1500 },
        };

        var speech = TradeOfferSpeech.Next(before, after, Name);

        Assert.Equal("Daijin Replicant bietet jetzt Trank, 1500 Gil.", speech);
    }

    [Fact]
    public void LockAndUnlockNameThePerson()
    {
        var open = View("Daijin Replicant",
            new TradeSide([], 0, TradeState.SelectingTradeGoods),
            new TradeSide([new TradeGoods(44, 1, false)], 0, TradeState.SelectingTradeGoods));
        var locked = open with { Theirs = open.Theirs with { State = TradeState.LockedIn } };
        var mine = locked with { Mine = locked.Mine with { State = TradeState.LockedIn } };
        var back = mine with { Mine = mine.Mine with { State = TradeState.SelectingTradeGoods } };

        Assert.Equal("Daijin Replicant hat bestätigt.", TradeOfferSpeech.Next(open, locked, Name));
        Assert.Equal("Du hast bestätigt.", TradeOfferSpeech.Next(locked, mine, Name));
        Assert.Equal("Du hast die Bestätigung aufgehoben.", TradeOfferSpeech.Next(mine, back, Name));
    }

    [Fact]
    public void OpeningAlreadyLockedSaysSo()
    {
        var view = View("Daijin Replicant",
            new TradeSide([], 0, TradeState.Confirmed),
            new TradeSide([], 20, TradeState.WaitingForConfirmation));

        var speech = TradeOfferSpeech.Next(null, view, Name);

        Assert.Contains("Daijin Replicant wartet auf die Bestätigung.", speech);
        Assert.Contains("Du hast den Handel abgeschlossen.", speech);
    }

    [Fact]
    public void MissingNameUsesTheSheetFallback()
    {
        Loc.Mode = LanguageMode.English;
        var view = View("",
            new TradeSide([new TradeGoods(9, 1, false)], 0, TradeState.SelectingTradeGoods),
            new TradeSide([], 0, TradeState.TradeRequestPending));

        var speech = TradeOfferSpeech.Next(null, view, _ => "");

        Assert.Equal(
            "Trade. The other side offers nothing, 0 Gil. You offer item 9, 0 Gil. The trade request is still pending.",
            speech);
    }

    [Fact]
    public void RussianUsesTheRussianSentences()
    {
        Loc.Mode = LanguageMode.Russian;
        var view = View("Daijin",
            new TradeSide([], 0, TradeState.SelectingTradeGoods),
            new TradeSide([new TradeGoods(44, 3, false)], 5, TradeState.LockedIn));

        var speech = TradeOfferSpeech.Next(null, view, Name);

        Assert.Equal(
            "Обмен с Daijin. Daijin предлагает Trank, 3 штук, 5 гил. Ты предлагаешь ничего, 0 гил. Daijin подтвердил.",
            speech);
    }

    private static TradeView View(string partner, TradeSide mine, TradeSide theirs)
        => new(partner, mine, theirs);

    private static string Name(uint id) => id == 44 ? "Trank" : "";
}
