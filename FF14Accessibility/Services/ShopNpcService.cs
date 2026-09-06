using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>What kind of shop an NPC runs; <see cref="None"/> means it is no merchant.</summary>
public enum ShopKind
{
    None,
    /// <summary>Buys and sells for gil - what a player calls "a shop".</summary>
    GilShop,
    /// <summary>Trades for tokens/currencies instead of gil (Allagan pieces,
    /// Grand Company seals, tomestones, ...).</summary>
    Exchange,
}

/// <summary>
/// Tells whether an NPC runs a shop, straight from the game's own link between
/// the NPC and its shop: <c>ENpcBase.ENpcData</c> holds up to 32 references,
/// and Lumina resolves each one against the 25 sheet types the game allows
/// there (ilspycmd-verified 2026-08-03 at Lumina.Excel.Sheets.ENpcBase:
/// ENpcDataCtor lists ChocoboTaxiStand, CollectablesShop, ContentNpc, CraftLeve,
/// CustomTalk, DefaultTalk, DisposalShop, DpsChallengeOfficer, EventPathMove,
/// FccShop, GCShop, GilShop, GuildOrderGuide, GuildOrderOfficer,
/// GuildleveAssignment, InclusionShop, LotteryExchangeShop, PreHandler, Quest,
/// SpecialShop, Story, SwitchTalk, TopicSelect, TripleTriad, Warp).
///
/// An NPC counts as a merchant when a shop sheet is reached by following those
/// links — either directly, or one hop through the menus the game inserts in
/// front of the counter:
/// <list type="bullet">
/// <item><see cref="TopicSelect"/>.<c>Shop</c> → GilShop / SpecialShop / PreHandler
/// (ilspycmd 2026-09-06: TopicSelect.ShopCtor). Battlecraft Armorer / Supplier
/// NPCs use only this path — verified against sqpack EN titles (e.g. Gwalter
/// 1001965, Iron Thunder 1001203): oldDirect=false, Topic→GilShop=true.</item>
/// <item><see cref="PreHandler"/>.<c>Target</c> → GilShop / SpecialShop / …
/// (housing material suppliers and similar).</item>
/// <item><see cref="CustomTalk"/>.<c>SpecialLinks</c> → SpecialShop / CollectablesShop.</item>
/// </list>
/// Nothing is inferred from names, titles or icons.
///
/// The row id used for the lookup is the object's BaseId (its data-sheet id).
/// That link is not new here: <c>NavigationService.NpcPrefix</c> already reads
/// ENpcResident by BaseId to speak an NPC's title, and those titles come out
/// correct in game.
///
/// KNOWN LIMIT, stated rather than hidden: Lumina picks the FIRST sheet whose
/// row id exists (RowRef.GetFirstValidRowOrUntyped). Where a row id is valid in
/// several of those sheets, the type it reports can be the wrong one. The
/// diagnostic log below prints every recognised NPC with its shop kind so a real
/// walk through a market district shows whether the list matches what is there.
/// </summary>
public sealed class ShopNpcService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    // BaseId -> shop kind. The sheets are static per game version, so a miss is
    // worth remembering too (a market district asks the same ids every frame).
    private readonly Dictionary<uint, ShopKind> _cache = new();

    public ShopNpcService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log = log;
    }

    /// <summary>
    /// The shop an NPC runs, or <see cref="ShopKind.None"/>. Gil shops win over
    /// exchanges when an NPC does both: that is the one a player means by "shop",
    /// and the announcement has room for one word.
    /// </summary>
    public ShopKind KindOf(uint baseId)
    {
        if (baseId == 0) return ShopKind.None;
        if (_cache.TryGetValue(baseId, out var cached)) return cached;

        var kind = Resolve(baseId);
        _cache[baseId] = kind;
        return kind;
    }

    private ShopKind Resolve(uint baseId)
    {
        if (!_data.GetExcelSheet<ENpcBase>().TryGetRow(baseId, out var npc))
            return ShopKind.None;

        var exchange = false;
        foreach (var entry in npc.ENpcData)
        {
            if (entry.RowId == 0) continue;

            var direct = KindFromShopRow(entry);
            if (direct == ShopKind.GilShop) return ShopKind.GilShop;
            if (direct == ShopKind.Exchange) exchange = true;

            // TopicSelect: the spoken menu in front of several gil counters
            // (Battlecraft Armorer/Supplier, …). Sheet → Shop[] verified
            // ilspycmd 2026-09-06.
            if (entry.Is<TopicSelect>() && entry.TryGetValue<TopicSelect>(out var topic))
            {
                var via = KindFromTopicSelect(topic);
                if (via == ShopKind.GilShop) return ShopKind.GilShop;
                if (via == ShopKind.Exchange) exchange = true;
                continue;
            }

            // PreHandler: unlock/accept wrapper around a shop target.
            if (entry.Is<PreHandler>() && entry.TryGetValue<PreHandler>(out var pre))
            {
                var via = KindFromPreHandler(pre);
                if (via == ShopKind.GilShop) return ShopKind.GilShop;
                if (via == ShopKind.Exchange) exchange = true;
                continue;
            }

            // CustomTalk may point at an exchange counter via SpecialLinks.
            if (entry.Is<CustomTalk>() && entry.TryGetValue<CustomTalk>(out var talk))
            {
                var via = KindFromCustomTalk(talk);
                if (via == ShopKind.GilShop) return ShopKind.GilShop;
                if (via == ShopKind.Exchange) exchange = true;
            }
        }

        return exchange ? ShopKind.Exchange : ShopKind.None;
    }

    /// <summary>Direct shop sheet on an ENpcData / TopicSelect.Shop / PreHandler.Target row.</summary>
    private static ShopKind KindFromShopRow(RowRef entry)
    {
        if (entry.Is<GilShop>()) return ShopKind.GilShop;

        if (entry.Is<SpecialShop>()
            || entry.Is<CollectablesShop>()
            || entry.Is<GCShop>()
            || entry.Is<FccShop>()
            || entry.Is<InclusionShop>()
            || entry.Is<DisposalShop>()
            || entry.Is<LotteryExchangeShop>())
            return ShopKind.Exchange;

        return ShopKind.None;
    }

    private static ShopKind KindFromTopicSelect(TopicSelect topic)
    {
        var exchange = false;
        foreach (var shop in topic.Shop)
        {
            if (shop.RowId == 0) continue;

            var direct = KindFromShopRow(shop);
            if (direct == ShopKind.GilShop) return ShopKind.GilShop;
            if (direct == ShopKind.Exchange) exchange = true;

            if (shop.Is<PreHandler>() && shop.TryGetValue<PreHandler>(out var pre))
            {
                var via = KindFromPreHandler(pre);
                if (via == ShopKind.GilShop) return ShopKind.GilShop;
                if (via == ShopKind.Exchange) exchange = true;
            }
        }

        return exchange ? ShopKind.Exchange : ShopKind.None;
    }

    private static ShopKind KindFromPreHandler(PreHandler pre) => KindFromShopRow(pre.Target);

    private static ShopKind KindFromCustomTalk(CustomTalk talk)
    {
        var links = talk.SpecialLinks;
        if (links.Is<SpecialShop>() || links.Is<CollectablesShop>())
            return ShopKind.Exchange;
        return ShopKind.None;
    }

    /// <summary>Sheet name of an NPC, for the diagnostic log only - it proves the
    /// BaseId really addressed the NPC the player is looking at.</summary>
    public string SheetName(uint baseId) =>
        _data.GetExcelSheet<ENpcResident>().TryGetRow(baseId, out var npc)
            ? npc.Singular.ExtractText()
            : string.Empty;

    /// <summary>Logs one line per recognised merchant so a walk through a market
    /// district can be checked against what is actually standing there.</summary>
    public void LogMerchants(IEnumerable<(string Name, uint BaseId, ShopKind Kind)> merchants, int total)
    {
        var lines = new List<string>();
        foreach (var (name, baseId, kind) in merchants)
            lines.Add($"'{name}'(Id {baseId}, Sheet '{SheetName(baseId)}')={kind}");

        _log.Info($"[Shop] Haendler: {lines.Count} von {total} NPCs. "
                  + (lines.Count > 0 ? string.Join(", ", lines) : "keiner"));
    }
}
