using System.Collections.Generic;
using Dalamud.Plugin.Services;
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
/// An NPC counts as a merchant when at least one of those references IS a shop
/// sheet - nothing is inferred from names, titles or icons.
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

            // A gil shop is the plain "buy and sell" counter.
            if (entry.Is<GilShop>()) return ShopKind.GilShop;

            // Everything else that hands out goods does so against something
            // other than gil. They stay one category: the player wants to know
            // "can I get gear here", not which token sheet the game uses.
            if (entry.Is<SpecialShop>()
                || entry.Is<CollectablesShop>()
                || entry.Is<GCShop>()
                || entry.Is<FccShop>()
                || entry.Is<InclusionShop>()
                || entry.Is<DisposalShop>()
                || entry.Is<LotteryExchangeShop>())
                exchange = true;
        }

        return exchange ? ShopKind.Exchange : ShopKind.None;
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
