#if DEBUG
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer die eine Frage, an der die Bruecken-Etappe haengt: STEHT DIE
/// ABSPERRUNG AM TOR ZUM TIEFEN WALD GERADE, oder ist sie laengst abgeschaltet?
///
/// <para>
/// WARUM DAS ZAEHLT. tools/zone-probe hat 2026-08-22 an der Stelle, an der der
/// Auto-Lauf haengenbleibt, eine <c>CollisionBox</c> aus dem Layer
/// <c>QST_OP_ENPC_001/planner.lgb</c> gefunden: Mitte (155,0|-8,7|157,4),
/// Scale (1,0|5,8|11,0), <c>pushPlayerOut=31</c> - eine unsichtbare Wand, die den
/// Spieler aktiv hinausschiebt und in X von 154 bis 156 genau ueber dem Endpunkt
/// unserer gemessenen Bruecke (155,0|-12,8|161,0) liegt. Das erklaert das
/// Verhalten im Log restlos: die Figur wird geschoben statt gestoppt.
/// </para>
///
/// <para>
/// WAS DIE DATEI NICHT SAGT. <c>QST_*</c>-Layer schaltet das Spiel nach
/// Questfortschritt ein und aus - die .lgb sagt nur, DASS es die Box gibt. Und die
/// Antwort entscheidet ueber zwei voellig verschiedene Wege: ist die Wand aktiv,
/// sperrt das Spiel den Weg mit Absicht und der Mod darf das nicht unterlaufen
/// (dann gehoert dorthin eine ehrliche Ansage statt einer Bruecke). Ist sie
/// abgeschaltet, blockiert etwas anderes und die Brueckenpunkte muessen neu
/// vermessen werden.
/// </para>
///
/// <para>
/// WIE GEMESSEN WIRD. Aus dem LAUFENDEN Layout, nicht aus der Datei: das aktive
/// Layout enthaelt nur, was das Spiel gerade eingeschaltet hat. Gelesen wird
/// dieselbe Kette, die vnavmesh fuer seinen Netzbau benutzt und die dort seit
/// Jahren traegt (<c>SceneDefinition.FillFromLayout</c>): ueber
/// <c>LayoutWorld</c> auf <c>InstancesByType</c>, und je Instanz das Aktiv-Bit
/// <c>Flags3 &amp; 0x10</c> - genau die Pruefung, mit der vnavmesh entscheidet, ob
/// ein Hindernis ins Netz einfliesst. Nur Lesezugriffe, kein Hook.
/// </para>
///
/// <para>
/// NEBENBEI beantwortet die Sonde die zweite Frage des Users vom selben Tag - "kann
/// man ansagen, wogegen man laeuft?". Fuer jedes Hindernis in der Naehe steht hier
/// Position, Ausdehnung und Modellpfad. Sprechbare Namen fuehrt das Spiel dafuer
/// nicht, aber die Kuerzel sind lesbar: <c>f1t0_a0_taru1</c> ist ein Fass
/// (japanisch taru), <c>f1t0_b0_gatdr</c> die Tortuer.
/// </para>
///
/// NACH DER MESSUNG LOESCHEN (Sonden-Konvention).
/// </summary>
internal sealed unsafe class CollisionProbe
{
    /// <summary>Default radius around the character. Wide enough to catch the
    /// barrier at the gate from where the walk gives up (measured 3,8 to 6,2 m),
    /// narrow enough that a town square does not fill the log.</summary>
    private const float DefaultRadius = 12f;

    /// <summary>vnavmesh's own test for "this instance is switched on"
    /// (SceneDefinition.cs:63 and :85). The same bit decides whether a collider
    /// becomes part of the walkable mesh, so it is the bit that matters to us.</summary>
    private const byte ActiveFlag = 0x10;

    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public CollisionProbe(IObjectTable objectTable, IClientState clientState, TolkService tolk, IPluginLog log)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>Lists every collision box and background part near the character,
    /// with its on/off state.</summary>
    public void Dump(float radius = DefaultRadius)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _log.Warning("[CollProbe] Kein Spieler.");
            return;
        }

        var origin = player.Position;
        _log.Info($"[CollProbe] === Territory {_clientState.TerritoryType}, " +
                  $"Spieler ({origin.X:F1}|{origin.Y:F1}|{origin.Z:F1}), Radius {radius:F0} m ===");

        var world = LayoutWorld.Instance();
        if (world == null)
        {
            _log.Warning("[CollProbe] LayoutWorld ist null.");
            return;
        }

        var found = 0;
        found += DumpLayout(world->GlobalLayout, "global", origin, radius);
        found += DumpLayout(world->ActiveLayout, "aktiv", origin, radius);

        _log.Info($"[CollProbe] === Ende, {found} Eintrag/Eintraege ===");
        _tolk.SpeakInterrupt($"Kollisionssonde: {found} Einträge im Log.");
    }

    private int DumpLayout(LayoutManager* layout, string which, Vector3 origin, float radius)
    {
        // Same guard vnavmesh uses before reading a layout: anything else is either
        // half-loaded or mid-festival-swap, and the instance lists are not stable.
        if (layout == null || layout->InitState != 7)
        {
            _log.Info($"[CollProbe] Layout '{which}': nicht bereit (InitState " +
                      $"{(layout == null ? "-" : layout->InitState.ToString())}).");
            return 0;
        }

        DumpFilter(layout, which);

        var count = 0;
        count += DumpCollisionBoxes(layout, which, origin, radius);
        count += DumpBgParts(layout, which, origin, radius);
        return count;
    }

    /// <summary>
    /// WHICH VARIANT OF THE ZONE IS LOADED. Neu-Gridania exists in more than one
    /// version, and that is not a theory: the user's own mesh cache holds FOUR
    /// files for it (measured 2026-08-22 with tools/navmesh-gaps). In three of
    /// them the eastern border to the Deep Forest is unreachable from where the
    /// character stands - in the fourth it is ACCEPTED, and that one has 1643
    /// polygons instead of 1466.
    ///
    /// <para>
    /// The only thing separating them is this key. vnavmesh builds its cache name
    /// as <c>{zone}__{filterKey}__{festivals}__{zoneSharedGroups}</c>
    /// (<c>NavmeshManager.GetCacheKey</c>), and the four differ purely in
    /// filterKey: three say 10942, the walkable one says 125B9. The filter decides
    /// which layers are switched on - so this number says WHICH Gridania the
    /// player is standing in, and whether the gate is open in it.
    /// </para>
    /// </summary>
    private void DumpFilter(LayoutManager* layout, string which)
    {
        _log.Info($"[CollProbe] Layout '{which}': territory={layout->TerritoryTypeId} cfc={layout->CfcId} " +
                  $"layerFilterKey={layout->LayerFilterKey:X}");

        foreach (var (key, filter) in layout->Filters)
        {
            if (filter.Value == null) continue;
            _log.Info($"[CollProbe]   Filter key={filter.Value->Key:X} territory={filter.Value->TerritoryTypeId} " +
                      $"cfc={filter.Value->CfcId}   (Cache-Schluessel-Anteil, vgl. 10942 gesperrt / 125B9 offen)");
        }
    }

    /// <summary>The invisible walls. This is the list the barrier question hangs on.</summary>
    private int DumpCollisionBoxes(LayoutManager* layout, string which, Vector3 origin, float radius)
    {
        var boxes = FindInstances(layout, InstanceType.CollisionBox);
        if (boxes == null)
        {
            _log.Info($"[CollProbe] Layout '{which}': keine CollisionBox-Liste.");
            return 0;
        }

        var hits = new List<(float Distance, string Line)>();
        foreach (var (key, value) in *boxes)
        {
            var box = (CollisionBoxLayoutInstance*)value.Value;
            if (box == null) continue;

            var transform = box->Transform;
            var centre = transform.Translation;
            var distance = Vector3.Distance(origin, centre);
            if (distance > radius) continue;

            // Scale is the HALF extent on trigger boxes - measured in-game on
            // 2026-08-22 with the ZoneExitProbe, twice through the same border in
            // both directions. So this is the range the box actually covers.
            var half = transform.Scale;
            var active = (box->Flags3 & ActiveFlag) != 0;
            var inside = Math.Abs(origin.X - centre.X) <= half.X
                      && Math.Abs(origin.Y - centre.Y) <= half.Y
                      && Math.Abs(origin.Z - centre.Z) <= half.Z;

            hits.Add((distance,
                $"  {distance,5:F1} m  ({centre.X,7:F1}|{centre.Y,7:F1}|{centre.Z,7:F1})  " +
                $"halb=({half.X:F1}|{half.Y:F1}|{half.Z:F1})  " +
                $"X {centre.X - half.X:F1}..{centre.X + half.X:F1}  " +
                $"Z {centre.Z - half.Z:F1}..{centre.Z + half.Z:F1}  " +
                $"AKTIV={active}  typ={box->TriggerBoxLayoutInstance.Type}  " +
                $"flags1={box->Flags1:X2} flags2={box->Flags2:X2} flags3={box->Flags3:X2} " +
                $"flagsActive={box->FlagsActive:X2}  " +
                $"id={key:X16} sub={box->SubId:X}  " +
                (inside ? "  <<< SPIELER STEHT DRIN" : string.Empty)));
        }

        hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        _log.Info($"[CollProbe] Layout '{which}': {hits.Count} CollisionBox(en) im Radius.");
        foreach (var (_, line) in hits) _log.Info(line);
        return hits.Count;
    }

    /// <summary>The solid scenery - barrels, gates, fences. No speakable name
    /// exists for these; the model path is all the game carries.</summary>
    private int DumpBgParts(LayoutManager* layout, string which, Vector3 origin, float radius)
    {
        var parts = FindInstances(layout, InstanceType.BgPart);
        if (parts == null)
        {
            _log.Info($"[CollProbe] Layout '{which}': keine BgPart-Liste.");
            return 0;
        }

        var hits = new List<(float Distance, string Line)>();
        foreach (var (key, value) in *parts)
        {
            var part = (BgPartsLayoutInstance*)value.Value;
            if (part == null) continue;

            var transform = value.Value->GetTransformImpl();
            if (transform == null) continue;

            var centre = transform->Translation;
            var distance = Vector3.Distance(origin, centre);
            if (distance > radius) continue;

            var active = (part->Flags3 & ActiveFlag) != 0;
            // Only parts that carry collision can stop anyone. Everything else is
            // decoration and would only pad the log.
            var hasCollision = part->AnalyticShapeDataCrc != 0 || part->CollisionMeshPathCrc != 0;
            if (!hasCollision) continue;

            hits.Add((distance,
                $"  {distance,5:F1} m  ({centre.X,7:F1}|{centre.Y,7:F1}|{centre.Z,7:F1})  " +
                $"AKTIV={active}  " +
                $"analytic={part->AnalyticShapeDataCrc:X8} pcbCrc={part->CollisionMeshPathCrc:X8}  " +
                $"id={key:X16}"));
        }

        hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        _log.Info($"[CollProbe] Layout '{which}': {hits.Count} BgPart(s) MIT Kollision im Radius.");
        foreach (var (_, line) in hits) _log.Info(line);
        return hits.Count;
    }

    /// <summary>Instances of one type, or null. Mirrors vnavmesh's LayoutUtils.FindPtr.</summary>
    private static FFXIVClientStructs.STD.StdMap<ulong, FFXIVClientStructs.Interop.Pointer<ILayoutInstance>>* FindInstances(
        LayoutManager* layout, InstanceType type)
        => layout->InstancesByType.TryGetValuePointer(type, out var ptr) && ptr != null ? ptr->Value : null;
}
#endif
