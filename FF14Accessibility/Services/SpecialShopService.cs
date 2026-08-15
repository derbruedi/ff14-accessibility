using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// WAS EIN TAUSCH KOSTET - in der Währung, die das Spiel dafür führt.
///
/// WARUM ES DIESE KLASSE GIBT. Das Tauschfenster zeigt neben jeder Ware eine
/// nackte Zahl (gemessen 2026-08-16: Knotenn id=6 trug "2"), und erst der
/// Bestätigungsdialog nennt die Einheit dazu - "Den folgenden Gegenstand gegen 2
/// Errungenschaftszertifikate tauschen?". Eine Zahl ohne Einheit ist für den
/// Spieler wertlos, und die Einheit zu raten wäre falsch: dieselbe Oberfläche
/// bedient Zertifikate, Marken, Siegel und Münzen.
///
/// DIE QUELLE IST DAS SPIELEIGENE SHEET <c>SpecialShop</c> (Lumina): jede Zeile
/// hat <c>ReceiveItems</c> (was man bekommt) und <c>ItemCosts</c> mit
/// <c>CurrencyCost</c> (wie viel) und <c>ItemCost</c> (womit). Der Zugriff hier
/// geht NICHT über die Shop-Id - welcher Shop gerade offen ist, führt das Spiel
/// in keiner Struktur, die FFXIVClientStructs benennt (es gibt nur AgentShop für
/// AgentId.Shop, den Gil-Laden). Deshalb wird über das PAAR aus Ware und
/// Kostenzahl nachgeschlagen, beides aus dem offenen Fenster gelesen.
///
/// MEHRDEUTIGKEIT WIRD NICHT GERATEN: kommt dasselbe Paar in zwei Shops mit
/// VERSCHIEDENEN Währungen vor, liefert die Abfrage nichts, und der Aufrufer sagt
/// nur die Zahl. Lieber eine unvollständige Ansage als eine falsche Einheit.
/// </summary>
public sealed class SpecialShopService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    // (Ware, Kosten) -> Währung. Ein leerer Name markiert ein mehrdeutiges Paar;
    // die Id kommt mit, weil der Aufrufer damit den eigenen Bestand abfragt.
    private Dictionary<(uint Item, uint Cost), (uint Id, string Name)>? _index;

    public SpecialShopService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>
    /// The currency for "this item at this price": its item id and its name,
    /// already inflected for the given count. (0, "") when the sheets do not
    /// answer it unambiguously.
    /// </summary>
    public (uint Id, string Name) CurrencyFor(uint receiveItemId, uint cost)
    {
        if (receiveItemId == 0 || cost == 0) return (0, string.Empty);
        _index ??= BuildIndex();
        return _index.TryGetValue((receiveItemId, cost), out var currency) ? currency : (0, string.Empty);
    }

    private Dictionary<(uint, uint), (uint, string)> BuildIndex()
    {
        var map = new Dictionary<(uint, uint), (uint Id, string Name)>();
        var ambiguous = 0;

        foreach (var shop in _data.GetExcelSheet<SpecialShop>())
        {
            foreach (var entry in shop.Item)
            {
                // Cost first: an entry without a currency cost (collectability,
                // quest-gated barter) has nothing to announce here.
                var costCount    = 0u;
                var currencyId   = 0u;
                var currencyName = string.Empty;
                foreach (var itemCost in entry.ItemCosts)
                {
                    if (itemCost.CurrencyCost == 0 || itemCost.ItemCost.RowId == 0) continue;
                    if (!itemCost.ItemCost.IsValid) continue;
                    costCount  = itemCost.CurrencyCost;
                    currencyId = itemCost.ItemCost.RowId;
                    // The game inflects this itself, and the sheet carries both
                    // forms - "2 Errungenschaftszertifikate" is the game's own
                    // wording (confirmed in its confirmation prompt, log
                    // 2026-08-16 00:30:10), not a plural rule invented here.
                    var cost = itemCost.ItemCost.Value;
                    currencyName = costCount > 1
                        ? cost.Plural.ExtractText().Trim()
                        : cost.Singular.ExtractText().Trim();
                    if (currencyName.Length == 0) currencyName = cost.Name.ExtractText().Trim();
                    break; // the first real cost is the one the row shows
                }
                if (costCount == 0 || currencyName.Length == 0) continue;

                foreach (var receive in entry.ReceiveItems)
                {
                    var itemId = receive.Item.RowId;
                    if (itemId == 0) continue;

                    var key = (itemId, costCount);
                    if (map.TryGetValue(key, out var known))
                    {
                        // Same pair, different currency -> the pair no longer
                        // identifies one price. Blank it out rather than keep
                        // whichever row happened to come first.
                        if (known.Id != 0 && known.Id != currencyId)
                        {
                            map[key] = (0, string.Empty);
                            ambiguous++;
                        }
                        continue;
                    }
                    map[key] = (currencyId, currencyName);
                }
            }
        }

        _log.Info($"[SpecialShop] Preis-Index gebaut: {map.Count} Paare, {ambiguous} mehrdeutig.");
        return map;
    }
}
