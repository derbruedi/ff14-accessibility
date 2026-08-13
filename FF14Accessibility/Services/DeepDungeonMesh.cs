// EIN WEGENETZ JE ZEHN EBENEN - warum der Lauf in Waende geroutet hat.
//
// USER, 2026-08-13: *"the NavMesh needs to be able to account for corridors and not route
// through walls. it happens even if I'm trying to route to distant treasure or cairns, and
// I can't always get unstuck."*
//
// DIE URSACHE STECKT IM CACHE-SCHLUESSEL VON VNAVMESH, und sie ist auf der Platte
// sichtbar, ohne irgendetwas laufen zu lassen. NavmeshManager.GetCacheKey (dekompiliert
// aus dem installierten vnavmesh v1.2.3.10) baut den Schluessel aus vier Dingen:
//
//     {TerritoryType.Bg}__{LayoutFilter.Key:X}__{FestivalLayers}__{ZoneSharedGroups}
//
// Die Ebenenaufteilung eines Tiefen Gewoelbes steckt in KEINEM davon. Der Beweis ist das
// Cache-Verzeichnis: der ganze Palast der Toten hat genau zwei Dateien,
//
//     ffxiv_fst_f1_cnt_f1c1_level_f1c1__1E2AC____0.navmesh   Ebenen 1-10
//     ffxiv_fst_f1_cnt_f1c2_level_f1c2__1E2AE____0.navmesh   Ebenen 11-20
//
// - eine je Zehnerblock, mit einem abschliessenden ZoneSharedGroups-Feld "0", das nie
// variiert. Das Update() von vnavmesh laedt nur neu, wenn sich dieser Schluessel AENDERT,
// eine Ebene tiefer loest das also nie aus: das auf der ersten Ebene eines Blocks gebaute
// Netz wird fuer jede Ebene dieses Blocks benutzt, und die f1c2-Datei, auf der der User am
// 2026-08-13 unterwegs war, stammte vom Vortag.
//
// WAS DAS MIT EINEM LAUF MACHT, aus dem Log des Users um 04:13 und 04:18: der Weg wird
// durch einen Gang gebaut, den es auf der Ebene des Netzes gibt und der auf dieser eine
// Wand ist. Die Figur laeuft hinein und bleibt stehen - die Position stand den ganzen
// Versuch ueber auf (-249.3, 366.5), waehrend Wegpunkt 1 7,7 Yalm entfernt lag - und das
// Plugin sagte, weil es keine Annaeherung sah: "so weit reicht der begehbare Weg". Diese
// Meldung war jedes Mal falsch. Das Netz endete nicht; es beschrieb eine andere Ebene.
//
// DIE LOESUNG IST EIN NEUAUFBAU, KEIN NEULADEN. Nav.Reload ist
// Reload(allowLoadFromCache: true) und wuerde dieselbe veraltete Datei erneut lesen, weil
// der nachgeschlagene Schluessel unveraendert ist. Nur Nav.Rebuild
// (allowLoadFromCache: false) rastert die tatsaechlich geladene Szene neu. Es ist das, was
// der Spieler als `/vnav rebuild` tippen wuerde - der Behelf von Hand, solange das hier
// nicht ausgeliefert ist.
//
// WAS ES NICHT KAPUTTMACHEN KANN, wie es die Navigationsregeln ausdruecklich verlangen:
//   - Es laeuft NUR innerhalb eines Tiefen Gewoelbes (der Director existiert) und NUR,
//     wenn sich die Ebenennummer aendert. Laeufe in der offenen Welt, Quest-Laeufe,
//     Gebietswechsel, Aetheryten-Routen und der Folgemodus erreichen diesen Code nie.
//   - Ein Ebenenwechsel versetzt den Spieler, es kann also kein Lauf unterwegs sein, der
//     verloren geht. Der Neuaufbau wird in dem einen Moment ausgeloest, in dem ohnehin
//     nichts lief.
//   - Waehrend er laeuft, gibt es ueberhaupt kein Netz (Reload ruft ClearState), ein in
//     diesen Sekunden gestarteter Lauf scheitert also so, wie ein Lauf ohne vnavmesh
//     immer scheitert - der vorhandene Nicht-bereit-Pfad, unveraendert.
//   - Nichts zu tun ist hier nicht die sichere Wahl. Das veraltete Netz scheitert nicht
//     hoerbar; es routet einen blinden Spieler in eine Wand und meldet am Ende des Weges
//     Ankunft.
using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Haelt das Netz von vnavmesh mit der Ebene des Tiefen Gewoelbes im Gleichschritt, auf
/// der der Spieler wirklich steht. Nur im Framework-Thread.
/// </summary>
public sealed class DeepDungeonMesh
{
    private readonly DeepDungeonFloor _floor;
    private readonly NavmeshIpc       _nav;
    private readonly IPluginLog       _log;

    public DeepDungeonMesh(DeepDungeonFloor floor, NavmeshIpc nav, IPluginLog log)
    {
        _floor = floor;
        _nav   = nav;
        _log   = log;
    }

    /// <summary>Die Ebene, fuer die der letzte Neuaufbau angestossen wurde.
    /// 0 = nicht in einem Gewoelbe.</summary>
    private int  _builtFor;
    private bool _building;
    private long _startedTick;

    /// <summary>
    /// Wird einmal je Frame aus <see cref="DeepDungeonNav.Poll"/> aufgerufen.
    ///
    /// Im gewoehnlichen Frame billig: ein Director-Lesevorgang fuer die Ebenennummer und
    /// ein Ganzzahlvergleich. Der Neuaufbau selbst wird einmal je Ebene ausgeloest und
    /// danach nur noch beobachtet.
    /// </summary>
    public void Poll()
    {
        var floor = _floor.Floor;

        // Ausserhalb des Gewoelbes: die Ebene vergessen, damit ein erneuter Eintritt (der
        // wieder bei 1 anfaengt) als Wechsel gesehen wird und nicht als "dieselbe Ebene
        // wie im letzten Lauf".
        if (floor == 0)
        {
            if (_builtFor != 0)
                _log.Info("[DeepMesh] Tiefes Gewoelbe verlassen - naechster Eintritt baut das Netz neu.");
            _builtFor = 0;
            _building = false;
            return;
        }

        if (_building) { WatchBuild(floor); return; }
        if (floor == _builtFor) return;

        // vnavmesh fehlt oder startet noch: im naechsten Frame erneut versuchen, statt
        // diese Ebene als erledigt zu vermerken. LastCallFailed trennt die beiden Faelle
        // - siehe NavmeshIpc.
        if (!_nav.Rebuild())
        {
            _log.Info($"[DeepMesh] Ebene {floor}: Neuaufbau des Netzes nicht moeglich "
                      + "(vnavmesh nicht erreichbar) - wird erneut versucht.");
            return;
        }

        _builtFor    = floor;
        _building    = true;
        _startedTick = Stopwatch.GetTimestamp();
        _log.Info($"[DeepMesh] Ebene {floor}: Netz wird neu gebaut. Der Zwischenspeicher von "
                  + "vnavmesh kennt nur EIN Netz je Zehnerblock, und das ist das der Ebene, "
                  + "auf der man den Block betreten hat.");
    }

    /// <summary>
    /// Verfolgt den Aufbau bis zum Ende, einzig damit im Log steht, was eine Ebene
    /// kostet. Nichts wartet darauf und nichts wird angesagt: ob das Netz bereit ist,
    /// erfaehrt der Spieler durch Laufen - genau wie ueberall sonst.
    /// </summary>
    private void WatchBuild(int floor)
    {
        if (!_nav.IsReady) return;
        _building = false;
        var seconds = (double)(Stopwatch.GetTimestamp() - _startedTick) / Stopwatch.Frequency;
        _log.Info($"[DeepMesh] Ebene {floor}: Netz steht nach {seconds:F1} s.");
    }
}
