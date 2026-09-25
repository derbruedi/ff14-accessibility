#if DEBUG
using System;
using System.Text;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer die Bestienbaendiger-Jobanzeige (ClassJob 43).
///
/// <para>
/// WARUM. Sehend gibt es TP (gelb, max 250), Vertrauten-TP (blau, max 250),
/// Gemeisterter/Natuerlicher Instinkt (je bis 3), Innerer Kompass. Dalamud hat
/// kein <c>BSTGauge</c>; ClientStructs <c>JobGaugeManager</c> endet vor Job 43;
/// kein <c>JobHudBST*</c>. Collect/Announce ohne gemessene Quelle waere Raten.
/// </para>
///
/// <para>
/// WAS GEMESSEN WIRD (ein Schnappschuss per Befehl):
/// <list type="number">
/// <item><c>JobGaugeManager</c>: ClassJobId + Rohbytes der Gauge-Union.</item>
/// <item><c>ActionManager.BeastmasterPets</c> (3 Slot-Bytes — Hotbar-Pets, nicht
/// die Anzeige).</item>
/// <item>Geladene Addons deren Name JobHud / XBM / BST / Gauge enthaelt, inkl.
/// sichtbarer Textknoten.</item>
/// <item>Spieler-Statuszeilen (Name + Stack + Restzeit) — Instinkt kann als
/// Status liegen, muss aber gemessen werden.</item>
/// </list>
/// </para>
///
/// <para>Aufruf: <c>/acc bstprobe</c> als Bestienbaendiger, idealerweise im Kampf
/// mit gefuellter Anzeige. Nach erfolgreicher Messung und Collect loeschen
/// (Sonden-Konvention).</para>
/// </summary>
public sealed unsafe class BeastmasterGaugeProbe
{
    private const byte JobBeastmaster = 43;
    private const int GaugeUnionBytes = 80;

    private readonly IObjectTable _objects;
    private readonly TolkService  _tolk;
    private readonly IPluginLog   _log;

    public BeastmasterGaugeProbe(IObjectTable objects, TolkService tolk, IPluginLog log)
    {
        _objects = objects;
        _tolk    = tolk;
        _log     = log;
    }

    /// <summary>Schreibt einen Schnappschuss der BST-Anzeige-Kandidaten ins Log.</summary>
    public void Dump()
    {
        var ps = PlayerState.Instance();
        if (ps == null)
        {
            _log.Warning("[BstGaugeProbe] PlayerState nicht verfuegbar.");
            _tolk.SpeakInterrupt("Sonde nicht moeglich.");
            return;
        }

        var job   = ps->CurrentClassJobId;
        var level = ps->CurrentLevel;
        _log.Info("[BstGaugeProbe] ===================================================");
        _log.Info($"[BstGaugeProbe] Job={job} Stufe={level} (BST erwartet={JobBeastmaster})");

        if (job != JobBeastmaster)
        {
            _log.Info("[BstGaugeProbe] Nicht Bestienbaendiger — trotzdem Kandidaten loggen.");
        }

        DumpJobGaugeManager();
        DumpBeastmasterPets();
        DumpNamedJobHudXbm();
        var addonHits = DumpCandidateAddons();
        var statusHits = DumpPlayerStatuses();

        _log.Info("[BstGaugeProbe] ===================================================");
        _tolk.SpeakInterrupt(
            $"Bestienbaendiger-Sonde. Job {job}, Stufe {level}. " +
            $"{addonHits} Addon-Treffer, {statusHits} Statuszeilen. Rest im Log.");
    }

    private void DumpJobGaugeManager()
    {
        var mgr = JobGaugeManager.Instance();
        if (mgr == null)
        {
            _log.Warning("[BstGaugeProbe] JobGaugeManager.Instance null.");
            return;
        }

        _log.Info($"[BstGaugeProbe] JobGaugeManager.ClassJobId={mgr->ClassJobId} " +
                  $"CurrentGauge={(nint)mgr->CurrentGauge:X}");

        // Union beginnt bei Offset 8; ClassJobId liegt bei 88. Rohbytes lesen,
        // damit spaeter Offset-Zuordnung moeglich ist, ohne Struct zu erfinden.
        var basePtr = (byte*)mgr + 8;
        var sb = new StringBuilder(GaugeUnionBytes * 3);
        for (var i = 0; i < GaugeUnionBytes; i++)
        {
            if (i > 0 && i % 16 == 0) sb.Append(" | ");
            else if (i > 0) sb.Append(' ');
            sb.Append(basePtr[i].ToString("X2"));
        }
        _log.Info($"[BstGaugeProbe] GaugeUnion[+8..+{7 + GaugeUnionBytes}]: {sb}");
    }

    private void DumpBeastmasterPets()
    {
        var am = ActionManager.Instance();
        if (am == null)
        {
            _log.Warning("[BstGaugeProbe] ActionManager null.");
            return;
        }

        var pets = am->BeastmasterPets;
        _log.Info($"[BstGaugeProbe] ActionManager.BeastmasterPets=" +
                  $"[{pets[0]}, {pets[1]}, {pets[2]}]");
    }

    /// <summary>
    /// Laufzeit-Log 2026-09-25: JobHudXBM / JobHudXBM0 / JobHudXBM1 existieren,
    /// auch wenn ClientStructs sie noch nicht typisiert. Explizit anfragen.
    /// </summary>
    private void DumpNamedJobHudXbm()
    {
        string[] names = ["JobHudXBM", "JobHudXBM0", "JobHudXBM1"];
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return;

        foreach (var name in names)
        {
            var addon = mgr->GetAddonByName(name);
            if (addon == null)
            {
                _log.Info($"[BstGaugeProbe] {name}: nicht geladen.");
                continue;
            }
            _log.Info($"[BstGaugeProbe] {name}: visible={addon->IsVisible} id={addon->Id}");
            if (addon->IsVisible) DumpVisibleTexts(addon, name);
        }
    }

    private int DumpCandidateAddons()
    {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null)
        {
            _log.Warning("[BstGaugeProbe] RaptureAtkUnitManager null.");
            return 0;
        }

        var hits = 0;
        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 512; i++)
        {
            var a = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (a == null) continue;
            var name = a->NameString;
            if (string.IsNullOrEmpty(name)) continue;
            if (!IsCandidateAddon(name)) continue;

            hits++;
            _log.Info($"[BstGaugeProbe] Addon '{name}' visible={a->IsVisible} " +
                      $"id={a->Id} depth={a->DepthLayer}");
            if (!a->IsVisible) continue;

            DumpVisibleTexts(a, name);
        }

        if (hits == 0)
            _log.Info("[BstGaugeProbe] Kein geladenes Addon mit JobHud/XBM/BST/Gauge im Namen.");
        return hits;
    }

    private static bool IsCandidateAddon(string name)
    {
        return name.Contains("JobHud", StringComparison.OrdinalIgnoreCase)
            || name.Contains("XBM", StringComparison.OrdinalIgnoreCase)
            || name.Contains("BST", StringComparison.OrdinalIgnoreCase)
            || (name.Contains("Gauge", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Gather", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Same walk as UIReaderService's addon text probe: UldManager.NodeList,
    /// then one level into component children. Job gauges often put numbers
    /// only in nested components.
    /// </summary>
    private void DumpVisibleTexts(AtkUnitBase* addon, string addonName)
    {
        var logged = 0;
        for (var i = 0; i < addon->UldManager.NodeListCount && logged < 60; i++)
        {
            var n = addon->UldManager.NodeList[i];
            if (n == null) continue;
            if (n->Type == NodeType.Text && n->IsVisible())
            {
                var t = AtkText.Read((AtkTextNode*)n).Trim();
                if (t.Length > 0)
                {
                    _log.Info($"[BstGaugeProbe]   '{addonName}' id={n->NodeId}: '{t}'");
                    logged++;
                }
                continue;
            }

            if ((int)n->Type < 1000) continue;
            var comp = ((AtkComponentNode*)n)->Component;
            if (comp == null) continue;
            for (var j = 0; j < comp->UldManager.NodeListCount && logged < 60; j++)
            {
                var child = comp->UldManager.NodeList[j];
                if (child == null || child->Type != NodeType.Text || !child->IsVisible())
                    continue;
                var t = AtkText.Read((AtkTextNode*)child).Trim();
                if (t.Length == 0) continue;
                _log.Info($"[BstGaugeProbe]   '{addonName}' id={n->NodeId}/{child->NodeId}: '{t}'");
                logged++;
            }
        }

        if (logged == 0)
            _log.Info($"[BstGaugeProbe]   '{addonName}': keine sichtbaren Textknoten.");
    }

    private int DumpPlayerStatuses()
    {
        if (_objects.LocalPlayer is not IBattleChara player)
        {
            _log.Info("[BstGaugeProbe] Kein LocalPlayer fuer Statusliste.");
            return 0;
        }

        var count = 0;
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == 0) continue;
            count++;
            var name = status.GameData.ValueNullable?.Name.ExtractText().Trim() ?? "?";
            _log.Info($"[BstGaugeProbe] Status id={status.StatusId} " +
                      $"\"{name}\" param={status.Param} remain={status.RemainingTime:F1}s");
        }

        if (count == 0)
            _log.Info("[BstGaugeProbe] Keine Statuszeilen auf dem Spieler.");
        return count;
    }
}
#endif
