using System.Globalization;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace FF14Accessibility.Services;

/// <summary>One offered stack in a player trade. Gil is not a stack.</summary>
internal readonly record struct TradeGoods(uint ItemId, int Quantity, bool HighQuality);

/// <summary>
/// One side of a player trade. <see cref="Items"/> is slots 0 to 4.
/// Slot 5 of <c>InventoryManager.TradeItemsLocal</c> / <c>TradeItemsRemote</c>
/// is gil and lives in <see cref="Gil"/>.
/// </summary>
internal readonly record struct TradeSide(TradeGoods[] Items, int Gil, TradeState State)
{
    public bool SameGoods(TradeSide other)
        => Gil == other.Gil && Items.AsSpan().SequenceEqual(other.Items);
}

/// <summary>Partner name plus both offers, copied out of the game struct.</summary>
internal readonly record struct TradeView(string Partner, TradeSide Mine, TradeSide Theirs);

/// <summary>Turns two trade snapshots into the sentences a blind player needs.</summary>
internal static class TradeOfferSpeech
{
    public static string? Next(TradeView? previous, TradeView current, Func<uint, string> itemName)
    {
        var you = AccessibilityStrings.TradeYou;
        var them = current.Partner.Length > 0 ? current.Partner : AccessibilityStrings.TradeOther;
        var parts = new List<string>();
        var nameArrived = previous is { } prior
            && prior.Partner.Length == 0
            && current.Partner.Length > 0;

        if (previous is null || nameArrived)
        {
            parts.Add(AccessibilityStrings.TradeOpened(
                current.Partner,
                Describe(current.Theirs, itemName),
                Describe(current.Mine, itemName)));
            AddState(parts, them, default, current.Theirs.State, steady: true, self: false);
            AddState(parts, you, default, current.Mine.State, steady: true, self: true);
        }
        else
        {
            var before = previous.Value;
            if (!before.Theirs.SameGoods(current.Theirs))
                parts.Add(AccessibilityStrings.TradeTheirOffer(current.Partner, Describe(current.Theirs, itemName)));
            AddState(parts, them, before.Theirs.State, current.Theirs.State, steady: false, self: false);

            if (!before.Mine.SameGoods(current.Mine))
                parts.Add(AccessibilityStrings.TradeMyOffer(Describe(current.Mine, itemName)));
            AddState(parts, you, before.Mine.State, current.Mine.State, steady: false, self: true);
        }

        if (parts.Count == 0) return null;
        return string.Join(" ", parts);
    }

    public static string Describe(TradeSide side, Func<uint, string> itemName)
    {
        var parts = new List<string>(side.Items.Length);
        foreach (var item in side.Items)
        {
            if (item.ItemId == 0 || item.Quantity <= 0) continue;
            var name = itemName(item.ItemId).Trim();
            if (name.Length == 0) name = AccessibilityStrings.TradeUnknownItem(item.ItemId);
            if (item.HighQuality) name += AccessibilityStrings.HighQuality;
            var qty = item.Quantity.ToString(CultureInfo.InvariantCulture);
            parts.Add(item.Quantity > 1 ? AccessibilityStrings.ItemQuantity(qty, name) : name);
        }

        var gil = AccessibilityStrings.TradeGil(side.Gil);
        if (parts.Count == 0 && side.Gil == 0) return AccessibilityStrings.TradeNothing;
        if (parts.Count == 0) return gil;
        return string.Join(", ", parts) + ", " + gil;
    }

    private static void AddState(List<string> parts, string who, TradeState before, TradeState after, bool steady, bool self)
    {
        if (!steady && before == after) return;
        string? line = after switch
        {
            TradeState.SelectingTradeGoods when !steady && before is TradeState.LockedIn
                or TradeState.WaitingForConfirmation or TradeState.Confirmed
                => AccessibilityStrings.TradeUnlocked(who, self),
            TradeState.LockedIn when steady || before != TradeState.LockedIn
                => AccessibilityStrings.TradeLocked(who, self),
            TradeState.WaitingForConfirmation when steady || before != TradeState.WaitingForConfirmation
                => AccessibilityStrings.TradeWaiting(who, self),
            TradeState.Confirmed when steady || before != TradeState.Confirmed
                => AccessibilityStrings.TradeFinal(who, self),
            TradeState.TradeRequestPending when steady || before != TradeState.TradeRequestPending
                => AccessibilityStrings.TradeRequestPending,
            TradeState.NotTrading when !steady && before != TradeState.NotTrading && (int)before != 0
                => AccessibilityStrings.TradeEnded,
            _ => null,
        };
        if (line == null || parts.Contains(line)) return;
        parts.Add(line);
    }
}
