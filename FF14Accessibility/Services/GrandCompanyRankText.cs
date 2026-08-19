using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Rank names behind the rank-tier buttons of the seal shop (GrandCompanyExchange).
///
/// WHY THIS EXISTS: the left column of that window holds six RadioButtons that carry
/// no text node at all - only Collision and NineGrid children (dump 2026-08-19,
/// Comp(1016), NodeIds 37-42, three of them visible). The game draws the rank
/// insignia of the ranks a tier covers as separate image nodes NEXT to the button,
/// so a screen reader has nothing to read and the focus reader logged
/// "[Focus] STUMM" on every one of them.
///
/// WHAT THE BUTTONS ARE, from the sheets (offline dump 2026-08-19):
///   GCScripShopCategory  = (GrandCompany, Tier 1-3, SubCategory 1-4). The four
///                          visible tabs on top are the SubCategories, the tiers are
///                          the left column.
///   GrandCompanyRank     = has its own Tier field: ranks 1-4 are tier 1, 5-8 tier 2,
///                          9-11 tier 3.
/// The node tree confirms the mapping without guessing: the three visible buttons
/// carry 4, 4 and 3 insignia images (Res nodes 11/16/21) - exactly the number of
/// ranks in tiers 1, 2 and 3.
///
/// The rank NAMES are per Grand Company and per sex (six sheets), so they are read
/// from the player's own company and character, the same words the game shows in the
/// window header ("Legionär 3. Klasse").
/// </summary>
internal sealed unsafe class GrandCompanyRankText
{
    private readonly IDataManager _data;
    private readonly IPluginLog   _log;

    public GrandCompanyRankText(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>Number of shop tiers the rank sheet knows (3 in every expansion so
    /// far). Read from the sheet rather than hardcoded, so a new rank tier would be
    /// counted instead of silently mislabelling the buttons.</summary>
    public int TierCount()
    {
        var max = 0;
        foreach (var rank in _data.GetExcelSheet<GrandCompanyRank>())
            if (rank.RowId > 0 && rank.Tier > max) max = rank.Tier;
        return max;
    }

    /// <summary>
    /// First and last rank name of a tier, e.g. tier 1 of the Immortal Flames is
    /// "Legionär 3. Klasse" .. "Phönixlegionär". Empty strings when the player has
    /// no Grand Company yet - the caller then announces the position alone rather
    /// than inventing a name.
    /// </summary>
    public (string First, string Last) TierRange(int tier)
    {
        var ranks = new List<(uint RowId, byte Order)>();
        foreach (var rank in _data.GetExcelSheet<GrandCompanyRank>())
            if (rank.RowId > 0 && rank.Tier == tier) ranks.Add((rank.RowId, rank.Order));
        if (ranks.Count == 0) return (string.Empty, string.Empty);

        ranks.Sort((a, b) => a.Order.CompareTo(b.Order));
        return (RankName(ranks[0].RowId), RankName(ranks[^1].RowId));
    }

    /// <summary>
    /// Name of one rank in the player's own Grand Company and sex. PlayerState.Sex
    /// is documented as 0 = Male, 1 = Female (FFXIVClientStructs); GrandCompany is
    /// the RowId of the GrandCompany sheet (1 Maelstrom, 2 Twin Adder, 3 Immortal
    /// Flames), which the log line below makes verifiable in game.
    /// </summary>
    private string RankName(uint rank)
    {
        var state = PlayerState.Instance();
        if (state == null) return string.Empty;

        var company = state->GrandCompany;
        var female  = state->Sex == 1;
        if (company is < 1 or > 3) return string.Empty;

        // One sheet per company and sex; TryGetRow keeps a missing row silent
        // instead of substituting a wrong name.
        if (company == 1 && !female && _data.GetExcelSheet<GCRankLimsaMaleText>()     .TryGetRow(rank, out var lm)) return Read(lm.Singular);
        if (company == 1 &&  female && _data.GetExcelSheet<GCRankLimsaFemaleText>()   .TryGetRow(rank, out var lf)) return Read(lf.Singular);
        if (company == 2 && !female && _data.GetExcelSheet<GCRankGridaniaMaleText>()  .TryGetRow(rank, out var gm)) return Read(gm.Singular);
        if (company == 2 &&  female && _data.GetExcelSheet<GCRankGridaniaFemaleText>().TryGetRow(rank, out var gf)) return Read(gf.Singular);
        if (company == 3 && !female && _data.GetExcelSheet<GCRankUldahMaleText>()     .TryGetRow(rank, out var um)) return Read(um.Singular);
        if (company == 3 &&  female && _data.GetExcelSheet<GCRankUldahFemaleText>()   .TryGetRow(rank, out var uf)) return Read(uf.Singular);
        return string.Empty;
    }

    private static string Read(Lumina.Text.ReadOnly.ReadOnlySeString text)
        => text.ExtractText().Trim();

    /// <summary>Diagnosis line: which company/sex the names were taken from. The
    /// window header shows the player's current rank in the same words, so a wrong
    /// sheet choice is visible in the log next to it instead of only sounding odd.
    /// </summary>
    public void LogSource()
    {
        var state = PlayerState.Instance();
        if (state == null) return;
        _log.Info($"[GC] Rangnamen aus Gesellschaft={state->GrandCompany} Geschlecht={state->Sex} "
                  + $"eigener Rang={state->GetGrandCompanyRank()}");
    }
}
