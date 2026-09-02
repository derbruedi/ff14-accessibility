#if DEBUG
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer das Zauberbuch der Blaumagie.
///
/// <para>
/// WAS SIE KLAeRT, UND WARUM ES NICHT AUS DEM QUELLCODE ZU HABEN IST.
/// <c>AddonAOZNotebook</c> benennt seine Felder vollstaendig, aber es sagt
/// nichts darueber, WAS das Spiel zur Laufzeit hineinschreibt. Drei Fragen
/// bleiben deshalb offen, und alle drei entscheiden ueber die Ansage:
/// </para>
/// <list type="number">
/// <item>Traegt eine Kachel auch dann eine <c>ActionId</c>, wenn der Zauber noch
/// nicht erlernt ist? Falls nein, greift der Ersatzweg ueber das Nummernschild -
/// die Sonde zeigt, ob er ueberhaupt gebraucht wird.</item>
/// <item>Was bedeutet das Ankreuzfeld der Kachel? Die Ansage sagt derzeit
/// "ausgewaehlt", wenn es gesetzt ist. Steht es in Wahrheit fuer "erlernt",
/// muss das Wort geaendert werden.</item>
/// <item>Folgt das Detail-Feld rechts der TASTATUR? Es gehoert dem ausgewaehlten
/// Zauber, und im Log vom 2026-09-02 hat es nie jemand mitgelesen. Der Reader
/// vergleicht deshalb dessen Nummer mit der Kachel und schweigt bei
/// Nichtpassen - die Sonde zeigt, wie oft das vorkommt.</item>
/// </list>
///
/// <para>
/// Zusaetzlich gemeldet: der Name, den das Fenster selbst in der Kachel fuehrt,
/// gegen den Namen aus den Sheets. Weichen sie ab, ist die Zuordnung ueber die
/// Aktions-Id falsch - und das faellt hier auf, nicht erst beim Spieler.
/// </para>
///
/// <para>Aufruf: <c>/acc aozprobe</c>, bei geoeffnetem Zauberbuch. Nach
/// Abschluss des Features loeschen (Konvention, siehe die uebrigen Sonden).</para>
/// </summary>
public sealed unsafe class AozNotebookProbe
{
    private readonly AozNotebookService _notebook;
    private readonly AozSpellSourceService _sources;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public AozNotebookProbe(AozNotebookService notebook, AozSpellSourceService sources,
                            TolkService tolk, IPluginLog log)
    {
        _notebook = notebook;
        _sources  = sources;
        _tolk     = tolk;
        _log      = log;
    }

    /// <summary>Schreibt einen vollstaendigen Schnappschuss des Fensters ins Log.</summary>
    public void Dump()
    {
        var addon = _notebook.Addon();
        if (addon == null || !addon->AtkUnitBase.IsVisible)
        {
            _tolk.SpeakInterrupt("Das Zauberbuch der Blaumagie ist nicht offen.");
            return;
        }

        _log.Info("───────── Blaumagie-Sonde ─────────");
        _log.Info($"[AozSonde] Reiter {addon->TabIndex + 1} von {addon->TabCount}.");

        DumpDetailPanel(addon);
        var withId = DumpSpellTiles(addon);
        var filled = DumpActiveSlots(addon);
        var unlockVerdict = DumpUnlockCrosscheck(addon);

        _log.Info("───────────────────────────────────");
        _tolk.SpeakInterrupt(
            $"Blaumagie-Sonde. Reiter {addon->TabIndex + 1} von {addon->TabCount}. " +
            $"{withId} von 16 Kacheln mit Aktions-Id, {filled} von 24 Plaetzen belegt. " +
            $"{unlockVerdict} Rest im Log.");
    }

    /// <summary>
    /// DIE ENTSCHEIDENDE MESSUNG fuer die Browser-Kategorie "Blaumagie".
    ///
    /// <para>
    /// Die Kategorie muss DRAUSSEN wissen, welche Zauber noch fehlen - da ist
    /// das Zauberbuch nicht offen. Der einzige Weg dafuer ist
    /// <c>UIState.IsUnlockLinkUnlocked</c> auf dem <c>UnlockLink</c> der Aktion.
    /// Dass jeder der 124 Zauber einen traegt (Werte 102 bis 461, alle
    /// verschieden, keiner ueber 0x10000), ist offline geprueft - dass die
    /// Abfrage fuer Blaumagie auch das Richtige ANTWORTET, ist es nicht.
    /// </para>
    ///
    /// <para>
    /// Hier steht die Gegenprobe: das Fenster fuehrt im Kopf seinen eigenen
    /// Zaehler ("Erlernt: 1/124"). Stimmt die Zahl der ueber UnlockLink als
    /// erlernt gemeldeten Zauber damit ueberein, traegt der Weg. Weicht sie ab,
    /// ist die Kategorie auf Sand gebaut und muss anders geloest werden - und
    /// das steht dann hier, statt beim Spieler als falsche Liste aufzutauchen.
    /// </para>
    /// </summary>
    /// <returns>Ein kurzer Satz fuers Vorlesen.</returns>
    private string DumpUnlockCrosscheck(AddonAOZNotebook* addon)
    {
        var all     = _sources.GetAll();
        var missing = _sources.GetMissing();
        var known   = all.Count - missing.Count;

        // Der Zaehler des Fensters, so wie er dasteht ("1/124").
        var counterNode = FindTopLevelNode(addon, 17);
        var counter = counterNode != null && counterNode->Type == NodeType.Text
            ? AtkText.ReadClean((AtkTextNode*)counterNode).Trim()
            : string.Empty;

        _log.Info($"[AozSonde] UnlockLink meldet {known} von {all.Count} erlernt " +
                  $"({missing.Count} offen). Fenster-Zaehler: '{counter}'.");

        // Zahl vor dem Schraegstrich herausziehen, um sie direkt zu vergleichen.
        var slash = counter.IndexOf('/');
        var windowKnown = -1;
        if (slash > 0 && int.TryParse(counter[..slash].Trim(), out var parsed)) windowKnown = parsed;

        string verdict;
        if (windowKnown < 0)
        {
            verdict = "Fenster-Zaehler nicht lesbar, Vergleich offen.";
            _log.Info($"[AozSonde] URTEIL: {verdict}");
        }
        else if (windowKnown == known)
        {
            verdict = $"UnlockLink stimmt mit dem Fenster ueberein ({known}).";
            _log.Info($"[AozSonde] URTEIL: TRAEGT. {verdict}");
        }
        else
        {
            verdict = $"ACHTUNG, UnlockLink sagt {known}, das Fenster {windowKnown}.";
            _log.Info($"[AozSonde] URTEIL: TRAEGT NICHT. {verdict} " +
                      $"Die Kategorie Blaumagie zeigt dann eine falsche Liste.");
        }

        // Dazu die Aufschluesselung der Fundorte - sie entscheidet, wie viele
        // Eintraege ueberhaupt ein Laufziel bekommen koennen.
        var world = missing.Count(t => t.Kind == AozSourceKind.World);
        var duty  = missing.Count(t => t.Kind == AozSourceKind.Duty);
        var none  = missing.Count(t => t.Kind == AozSourceKind.None);
        _log.Info($"[AozSonde] Offene Zauber nach Fundort: {world} in der Welt, " +
                  $"{duty} in Instanzen, {none} ohne Ort.");

        // Und die ersten Eintraege, wie die Kategorie sie anbieten wuerde.
        foreach (var t in missing.Take(8))
            _log.Info($"[AozSonde]   Nr.{t.Number,3} {t.SpellName,-24} {t.Kind,-5} " +
                      $"'{t.PlaceName}' Map={t.MapId} Inhalt={t.InstanceContentId} Link={t.UnlockLink}");

        return verdict;
    }

    /// <summary>Das Detail-Feld rechts, Knoten fuer Knoten - mit Sichtbarkeit.</summary>
    private void DumpDetailPanel(AddonAOZNotebook* addon)
    {
        foreach (var (id, was) in new (uint, string)[]
                 {
                     (69, "Nummer"), (70, "Name"), (71, "Werte"), (72, "Beschreibung"),
                     (68, "Nicht-erlernt-Hinweis"), (73, "Fundort-Label"),
                     (75, "Fundort 1"), (78, "Fundort 2"),
                     (17, "Erlernt-Zaehler"), (37, "Kommando-Zaehler"),
                 })
        {
            var node = FindTopLevelNode(addon, id);
            if (node == null)
            {
                _log.Info($"[AozSonde] Feld {was} (Knoten {id}): FEHLT.");
                continue;
            }

            var text = node->Type == NodeType.Text
                ? AtkText.ReadClean((AtkTextNode*)node).Replace("\n", " ⏎ ")
                : $"(kein Text, Typ {(int)node->Type})";
            _log.Info($"[AozSonde] Feld {was} (Knoten {id}): sichtbar={IsEffectivelyVisible(node)} '{text}'");
        }
    }

    /// <summary>Die 16 Kacheln der aktuellen Seite. Rueckgabe: wie viele eine Aktions-Id tragen.</summary>
    private int DumpSpellTiles(AddonAOZNotebook* addon)
    {
        var withId = 0;
        var blocks = addon->SpellbookBlocks;
        for (var i = 0; i < blocks.Length; i++)
        {
            ref var b = ref blocks[i];
            if (b.ActionId != 0) withId++;

            var addonName = b.Name.ToString() ?? string.Empty;
            var sheet     = _notebook.Lookup(b.ActionId);
            var tileText  = b.AtkTextNode == null ? "(kein Knoten)" : AtkText.ReadClean(b.AtkTextNode);

            var box     = b.AtkComponentCheckBox;
            var checkSt = box == null ? "(fehlt)" : box->IsChecked.ToString();

            var icon   = b.AtkComponentIcon;
            var iconId = icon == null ? 0u : icon->IconId;

            // Weicht der Fenstername vom Sheet-Namen ab, ist die Zuordnung falsch.
            var match = sheet is { } s
                ? (string.IsNullOrEmpty(addonName) || addonName == s.Name ? "passt" : "WEICHT AB")
                : "kein Sheet-Treffer";

            _log.Info($"[AozSonde] Kachel {i,2}: ActionId={b.ActionId,6} " +
                      $"Schild='{tileText}' FensterName='{addonName}' " +
                      $"SheetName='{sheet?.Name ?? "-"}' Nr={sheet?.Number ?? 0} Rang={sheet?.Rank ?? 0} " +
                      $"angekreuzt={checkSt} Symbol={iconId} -> {match}");
        }
        _log.Info($"[AozSonde] Kacheln mit Aktions-Id: {withId} von {blocks.Length}.");
        return withId;
    }

    /// <summary>Die 24 aktiven Kommando-Plaetze. Rueckgabe: wie viele belegt sind.</summary>
    private int DumpActiveSlots(AddonAOZNotebook* addon)
    {
        var filled = 0;
        var slots = addon->ActiveActions;
        for (var i = 0; i < slots.Length; i++)
        {
            ref var s = ref slots[i];
            if (s.ActionId != 0) filled++;

            var sheet = _notebook.Lookup(s.ActionId);
            var text  = s.AtkTextNode == null ? "(kein Knoten)" : AtkText.ReadClean(s.AtkTextNode);
            _log.Info($"[AozSonde] Platz {i + 1,2}: ActionId={s.ActionId,6} Schild='{text}' " +
                      $"FensterName='{s.Name.ToString() ?? string.Empty}' SheetName='{sheet?.Name ?? "-"}'");
        }
        _log.Info($"[AozSonde] Belegte Plaetze: {filled} von {slots.Length}.");
        return filled;
    }

    private static AtkResNode* FindTopLevelNode(AddonAOZNotebook* addon, uint nodeId)
    {
        ref var uld = ref addon->AtkUnitBase.UldManager;
        for (var i = 0; i < uld.NodeListCount; i++)
        {
            var n = uld.NodeList[i];
            if (n != null && n->NodeId == nodeId) return n;
        }
        return null;
    }

    private static bool IsEffectivelyVisible(AtkResNode* node)
    {
        var guard = 0;
        for (var n = node; n != null && guard++ < 32; n = n->ParentNode)
        {
            if (((ushort)n->NodeFlags & (ushort)NodeFlags.Visible) == 0) return false;
        }
        return true;
    }
}
#endif
