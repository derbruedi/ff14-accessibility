using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Eine noch fehlende Bestie als Wegziel: wohin man zum Fang muss.
/// </summary>
/// <param name="Number">Nummer im Bestienbuch (XBMPet RowId).</param>
/// <param name="Name">Pet-Anzeigename.</param>
/// <param name="ZoneName">Gebietsname (Karten-PlaceName), leer wenn ortlos.</param>
/// <param name="AreaName">Untergebiet / Wegpunkt, leer wenn nur die Zone bekannt.</param>
/// <param name="PlaceNameId">XBMPet-Location PlaceName-Zeile (0 = keiner).</param>
/// <param name="AreaPlaceNameId">Untergebiet-PlaceName (0 = keines).</param>
/// <param name="MapId">Karte des Fundorts, 0 wenn unbekannt oder ortlos.</param>
/// <param name="TerritoryId">Territory der Karte, für AreaRange-Suche.</param>
/// <param name="Position">Kartenmarker des Untergebiets, null wenn keiner.</param>
public sealed record XbmPetTarget(
    byte     Number,
    string   Name,
    string   ZoneName,
    string   AreaName,
    ushort   PlaceNameId,
    uint     AreaPlaceNameId,
    uint     MapId,
    uint     TerritoryId,
    Vector3? Position);

/// <summary>
/// Fehlende Bestien des Bestienbändigers mit Fundort — Gegenstück zu
/// <see cref="AozSpellSourceService"/> für das Bestienbuch.
///
/// <para>
/// Freischaltung über <c>XBMManager.IsPetUnlocked</c>. Fundort aus XBMPet Col7
/// → PlaceName. Das Bestienbuch nennt meist nur die ZONE (offline 2026-09-24:
/// 40 von 50 als Map.PlaceName). Untergebiet kommt aus dem Jagdtagebuch
/// (<c>MonsterNoteTarget.PlaceNameLocation</c>), aber NUR wenn
/// <c>PlaceNameZone</c> dieselbe Zone ist wie der Bestienbuch-Fundort —
/// sonst würde man in die falsche Zone geschickt (z. B. Baumhörnchen:
/// Buch = Tiefer Wald, Jagd-Habitat = Nordwald).
/// </para>
///
/// <para>
/// Ist der Fundort selbst kein Zonenname, sondern ein Wegpunkt (Marker-Subtext),
/// gilt er als Untergebiet auf der Karte, die diesen Marker trägt.
/// </para>
/// </summary>
public sealed unsafe class XbmPetSourceService
{
    private readonly XbmNotebookService _notebook;
    private readonly PlacesService _places;
    private readonly IObjectTable _objectTable;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    private uint? _beastJob;
    private bool _loggedUnlockSummary;
    private bool _loggedHabitatSummary;
    private Dictionary<string, List<uint>>? _bnpcByName;

    public XbmPetSourceService(
        PlacesService places,
        IObjectTable objectTable,
        IDataManager data,
        IPluginLog log)
    {
        _notebook    = new XbmNotebookService(data, log);
        _places      = places;
        _objectTable = objectTable;
        _data        = data;
        _log         = log;
    }

    /// <summary>
    /// Bestienbändiger-ClassJob aus dem Sheet (Abkürzung BST). 0 wenn nicht
    /// eindeutig — dann bleibt die Nav-Kategorie verborgen.
    /// </summary>
    public uint BeastmasterJobId
    {
        get
        {
            if (_beastJob.HasValue) return _beastJob.Value;

            var hits = new List<uint>();
            var sheet = _data.GetExcelSheet<ClassJob>();
            if (sheet != null)
            {
                foreach (var row in sheet)
                {
                    if (row.RowId == 0) continue;
                    var abbr = row.Abbreviation.ExtractText()?.Trim() ?? string.Empty;
                    if (!string.Equals(abbr, "BST", StringComparison.OrdinalIgnoreCase))
                        continue;
                    hits.Add(row.RowId);
                }
            }

            _beastJob = hits.Count == 1 ? hits[0] : 0u;
            _log.Info($"[XbmZiel] Bestienbändiger-Klasse aus den Sheets: {_beastJob} " +
                      $"({hits.Count} Treffer auf Abkürzung BST).");
            return _beastJob.Value;
        }
    }

    /// <summary>
    /// Noch nicht freigeschaltete Bestien in Buchreihenfolge (Nr. 1..50).
    /// Leer, wenn XBMManager fehlt oder die Freischaltliste noch nicht da ist.
    /// </summary>
    public List<XbmPetTarget> GetMissingInBookOrder()
    {
        var mgr = XBMManager.Instance();
        if (mgr == null) return [];

        // Ohne empfangene Liste wäre alles „fehlt“ — das wäre Lüge, nicht Hilfe.
        if (mgr->State != XBMManager.DataState.Received)
            return [];

        var missing = new List<XbmPetTarget>();
        var known = 0;
        var noMap = 0;
        var withArea = 0;

        for (byte n = 1; n <= XbmNotebookService.PetCount; n++)
        {
            if (!_notebook.TryGetPet(n, out var pet)) continue;

            if (mgr->IsPetUnlocked(n))
            {
                known++;
                continue;
            }

            var target = BuildTarget(pet);
            if (pet.PlaceNameId != 0 && target.MapId == 0) noMap++;
            if (target.AreaPlaceNameId != 0) withArea++;
            missing.Add(target);
        }

        if (!_loggedUnlockSummary)
        {
            _loggedUnlockSummary = true;
            _log.Info($"[XbmZiel] Freischaltung: {known} von {XbmNotebookService.PetCount} " +
                      $"(NumUnlockedPets={mgr->NumUnlockedPets}), {missing.Count} offen " +
                      $"({noMap} mit Fundort ohne Karte). GEGENPROBE: dieselbe Zahl im Bestienbuch.");
        }

        if (!_loggedHabitatSummary)
        {
            _loggedHabitatSummary = true;
            _log.Info($"[XbmZiel] Untergebiete: {withArea} von {missing.Count} offenen Bestien " +
                      $"(Jagdtagebuch-Habitat in derselben Zone, oder Fundort = Wegpunkt).");
        }

        return missing;
    }

    private XbmPetTarget BuildTarget(XbmPetInfo pet)
    {
        if (pet.PlaceNameId == 0)
            return new XbmPetTarget(pet.Number, pet.Name, string.Empty, string.Empty,
                                    0, 0, 0, 0, null);

        var placeId = pet.PlaceNameId;
        var asZoneMap = _places.FindMapByPlaceName(placeId);

        if (asZoneMap != 0)
        {
            // Bestienbuch nennt die Zone. Untergebiet nur aus Jagd-Habitat in
            // GENAU dieser Zone — sonst falsche Richtung (gemessen 2026-09-24).
            var zoneName = pet.Habitat;
            var (areaId, areaName) = FindSameZoneHuntArea(pet.Name, placeId);
            Vector3? pos = null;
            if (areaId != 0)
                pos = _places.FindMarkerPosition(asZoneMap, areaId, areaName);

            return new XbmPetTarget(
                pet.Number, pet.Name, zoneName, areaName,
                placeId, areaId, asZoneMap,
                _places.GetTerritoryOfMap(asZoneMap), pos);
        }

        // Fundort ist kein Zonenname → Wegpunkt auf irgendeiner Karte.
        var markerMap = _places.FindMapByMarkerPlaceName(placeId);
        if (markerMap == 0)
            return new XbmPetTarget(pet.Number, pet.Name, pet.Habitat, string.Empty,
                                    placeId, 0, 0, 0, null);

        var zone = _places.GetMapName(markerMap);
        var markerPos = _places.FindMarkerPosition(markerMap, placeId, pet.Habitat);
        return new XbmPetTarget(
            pet.Number, pet.Name, zone, pet.Habitat,
            placeId, placeId, markerMap,
            _places.GetTerritoryOfMap(markerMap), markerPos);
    }

    /// <summary>
    /// Jagdtagebuch-Untergebiet zum Pet-Namen, nur wenn PlaceNameZone dem
    /// Bestienbuch-Fundort entspricht. Erster Treffer mit Location-Zeile gewinnt.
    /// </summary>
    private (uint AreaId, string AreaName) FindSameZoneHuntArea(string petName, uint zonePlaceId)
    {
        var ids = BnpcIdsNamed(petName);
        if (ids.Count == 0) return (0, string.Empty);

        var notes = _data.GetExcelSheet<MonsterNoteTarget>();
        var places = _data.GetExcelSheet<PlaceName>();
        if (notes == null) return (0, string.Empty);

        foreach (var t in notes)
        {
            if (!ids.Contains(t.BNpcName.RowId)) continue;
            for (var i = 0; i < t.PlaceNameZone.Count; i++)
            {
                if (t.PlaceNameZone[i].RowId != zonePlaceId) continue;
                var areaId = i < t.PlaceNameLocation.Count ? t.PlaceNameLocation[i].RowId : 0u;
                if (areaId == 0) continue;
                var areaName = places != null && places.TryGetRow(areaId, out var pn)
                    ? pn.Name.ExtractText()?.Trim() ?? string.Empty
                    : string.Empty;
                return (areaId, areaName);
            }
        }

        return (0, string.Empty);
    }

    private HashSet<uint> BnpcIdsNamed(string petName)
    {
        var result = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(petName)) return result;
        EnsureBnpcIndex();
        if (_bnpcByName!.TryGetValue(petName, out var list))
            foreach (var id in list) result.Add(id);
        return result;
    }

    private void EnsureBnpcIndex()
    {
        if (_bnpcByName != null) return;
        _bnpcByName = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
        var sheet = _data.GetExcelSheet<BNpcName>();
        if (sheet == null) return;
        foreach (var row in sheet)
        {
            var singular = row.Singular.ExtractText()?.Trim() ?? string.Empty;
            if (singular.Length == 0) continue;
            if (!_bnpcByName.TryGetValue(singular, out var list))
                _bnpcByName[singular] = list = [];
            list.Add(row.RowId);
        }
    }

    /// <summary>
    /// Nächstes lebendes Exemplar mit exakt dem Pet-Namen in der Objekttabelle,
    /// oder null. Gleiche Annahme wie beim Jagdtagebuch: der Anzeigename muss
    /// dem BattleNpc-Namen entsprechen — bei Abweichung bleibt nur der Fundort.
    /// </summary>
    public IGameObject? FindNearestLive(string petName)
    {
        if (string.IsNullOrWhiteSpace(petName)) return null;
        var player = _objectTable.LocalPlayer;
        if (player == null) return null;

        IGameObject? nearest = null;
        var nearestDist = float.MaxValue;
        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
                continue;
            if (!string.Equals(obj.Name.TextValue, petName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (obj is IBattleChara { CurrentHp: 0 }) continue;
            var dist = Vector3.Distance(player.Position, obj.Position);
            if (dist < nearestDist) { nearest = obj; nearestDist = dist; }
        }
        return nearest;
    }
}
