using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Ein Eingang in einen Inhalt, mit seinem festen Platz in der Welt.
/// </summary>
/// <param name="Name">Name des Inhalts, wie ihn die Inhaltssuche fuehrt.</param>
/// <param name="ContentType">ContentFinderCondition.ContentType: 2 Dungeon, 4 Pruefung, 5 Raid.</param>
/// <param name="TypeName">Das Wort des SPIELS fuer die Inhaltsart, in Client-Sprache.</param>
/// <param name="Level">Geforderte Stufe, 0 wenn der Inhalt keine nennt.</param>
/// <param name="ContentId">InstanceContent-Zeile - der Schluessel fuer die Freischaltfrage.</param>
/// <param name="TerritoryTypeId">Zone, in der die Tuer steht.</param>
/// <param name="MapId">Karte dieser Zone - der Schluessel fuer die Uebergangs-Route.</param>
/// <param name="Position">Weltposition der Tuer, VOLLE 3D inklusive Hoehe (siehe Klassendoku).</param>
/// <param name="ZoneName">Gesprochener Zonenname.</param>
public sealed record DutyEntrance(
    string Name,
    uint ContentType,
    string TypeName,
    ushort Level,
    uint ContentId,
    uint TerritoryTypeId,
    uint MapId,
    Vector3 Position,
    string ZoneName);

/// <summary>
/// Alle Inhalts-Eingaenge der WELT - Dungeons, Pruefungen und Raids - mit Stufe
/// und fester Weltposition, unabhaengig davon, wo der Spieler gerade steht.
///
/// Wunsch des Users (2026-08-19): *"erstmal brauchen wir eine kategorie wo man zu
/// den dungeons laufen kann die kann man ja durch portale betreten sie sollen in
/// der kategorie nach stufe sortiert sein und man soll map uebergreifend hinlaufen
/// koennen"*.
///
/// UNTERSCHIED ZUR KATEGORIE "INHALTE": die listet die Tueren, die gerade GELADEN
/// sind, also die im Umkreis. Diese Liste kommt aus den Sheets und kennt deshalb
/// auch die Tuer drei Zonen weiter - genau das, was "map uebergreifend hinlaufen"
/// verlangt.
///
/// WOHER DIE POSITION KOMMT, offline gegen das installierte sqpack gemessen
/// (2026-08-19):
/// <list type="bullet">
/// <item><see cref="DungeonSide.All"/> liefert 182 EObj-Zeilen, die in einen
///   benannten Inhalt fuehren; 155 davon sind Dungeon, Pruefung oder Raid.</item>
/// <item>Das <c>Level</c>-Sheet fuehrt zu 152 dieser 155 eine Zeile mit
///   <c>Territory</c>, <c>Map</c> und X/Y/Z. Die Hoehe ist ECHT, nicht wie bei
///   Kartenmarkern geraten - der Weg braucht also keinen Boden-Schaetzer.</item>
/// <item>Die drei ohne Zeile ("Saegerschrei", zwei Eingaenge zu "Verschlungene
///   Schatten 3 - 1") werden weggelassen statt geraten: ein Ziel ohne Ort ist
///   keine Auskunft, sondern eine Irrefuehrung.</item>
/// <item>Der Join ueber <c>Level.Object</c> ist eindeutig. <c>Level.Type</c> 45
///   ist der EObj-Typ und hat einen eigenen Id-Bereich (2000002..2015509), der
///   sich mit keinem anderen Typ ueberschneidet; alle 175 Treffer tragen ihn.</item>
/// </list>
///
/// FREIGESCHALTET ODER NICHT wird das SPIEL gefragt
/// (<c>UIState.IsInstanceContentUnlocked</c>, gegen die installierte
/// FFXIVClientStructs.dll geprueft) und nicht aus Stufe oder Quests abgeleitet.
/// Sagt das Spiel nichts, sagt die Ansage nichts - eine geratene Sperre waere
/// schlimmer als gar keine.
/// </summary>
public sealed class DutyEntranceService
{
    /// <summary>ContentFinderCondition.ContentType: Dungeons.</summary>
    private const uint TypeDungeon = 2;

    /// <summary>ContentFinderCondition.ContentType: Pruefungen.</summary>
    private const uint TypeTrial = 4;

    /// <summary>ContentFinderCondition.ContentType: Raids.</summary>
    private const uint TypeRaid = 5;

    /// <summary>Level.Type des EObj-Verweises - siehe Klassendoku.</summary>
    private const byte LevelTypeEObj = 45;

    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;
    private readonly PlacesService _places;

    /// <summary>Einmal aus den Sheets gebaut - innerhalb einer Spielversion aendert sich nichts.</summary>
    private List<DutyEntrance>? _all;

    /// <summary>
    /// Gesetzt, sobald die Freischaltfrage einmal geworfen hat. Ohne die Bremse
    /// wuerde ein Patch, der die Funktion verschiebt, bei jedem Tastendruck 155
    /// Fehlerzeilen ins Log schreiben.
    /// </summary>
    private bool _unlockCheckBroken;

    public DutyEntranceService(IDataManager data, IClientState clientState, PlacesService places, IPluginLog log)
    {
        _data        = data;
        _clientState = clientState;
        _places      = places;
        _log         = log;
    }

    /// <summary>
    /// Jeder Eingang der Welt in einen Dungeon, eine Pruefung oder einen Raid,
    /// nach Stufe aufsteigend. Mehrere Tueren zu DEMSELBEN Inhalt stehen hier
    /// noch alle drin - <see cref="GetReachableSorted"/> waehlt daraus.
    /// </summary>
    public IReadOnlyList<DutyEntrance> GetAll() => _all ??= Build();

    /// <summary>
    /// Die Liste zum Durchblaettern: pro Inhalt EIN Eingang, nach Stufe sortiert.
    ///
    /// Welcher Eingang, wenn ein Inhalt mehrere hat (7 der 155, z.B.
    /// "Goetterdaemmerung - Ravana" mit einer Tuer im Dravanischen Vorland und
    /// einer in der Opferkammer): der in der aktuellen Zone gewinnt, sonst der
    /// mit den wenigsten Zonenwechseln, sonst der erste. So zeigt die Liste die
    /// Tuer, zu der man tatsaechlich laufen kann, statt der ersten im Sheet.
    /// </summary>
    public List<DutyEntrance> GetReachableSorted()
    {
        var currentMap = _clientState.MapId;
        var hops = _places.GetHopDistances();

        // Nicht erreichbare Karten fehlen in der Distanzkarte. Sie bekommen den
        // groessten Wert, damit ein Inhalt mit zwei Tueren die erreichbare zeigt -
        // fallen aber NICHT aus der Liste: dass ein Raid-Eingang nur ueber eine
        // Instanz zu erreichen ist, ist eine Auskunft und kein Grund zu schweigen.
        int HopsTo(DutyEntrance e) =>
            e.MapId == currentMap ? 0 :
            hops.TryGetValue(e.MapId, out var h) ? h : int.MaxValue;

        return GetAll()
            .GroupBy(e => e.ContentId)
            .Select(g => g.OrderBy(HopsTo).First())
            .OrderBy(e => e.Level)
            .ThenBy(e => e.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Hat der Spieler diesen Inhalt freigeschaltet? <c>null</c> wenn das Spiel
    /// die Frage gerade nicht beantwortet - dann wird darueber geschwiegen.
    /// </summary>
    public unsafe bool? IsUnlocked(uint contentId)
    {
        if (_unlockCheckBroken || contentId == 0) return null;
        // try-catch: Aufruf in eine fremde, mit jedem Patch wandernde Spielfunktion.
        // Er wird nicht verschluckt, sondern einmal protokolliert.
        try
        {
            return UIState.IsInstanceContentUnlocked(contentId);
        }
        catch (Exception ex)
        {
            _unlockCheckBroken = true;
            _log.Error(ex, "[Inhalte] IsInstanceContentUnlocked nicht aufrufbar - "
                           + "die Liste sagt ab jetzt nichts mehr ueber gesperrt oder frei.");
            return null;
        }
    }

    private List<DutyEntrance> Build()
    {
        var result = new List<DutyEntrance>();
        var duties = DungeonSide.All(_data, _log);

        // Level-Zeilen einmal nach der EObj-Zeile aufgeschluesselt, auf die sie
        // zeigen. Nur Type 45 (EObj) und nur die Ids, die wirklich ein Eingang
        // sind - der Rest des 61.000-Zeilen-Sheets geht uns nichts an.
        var positions = new Dictionary<uint, Level>();
        foreach (var level in _data.GetExcelSheet<Level>())
        {
            if (level.Type != LevelTypeEObj) continue;
            var objectRow = level.Object.RowId;
            if (objectRow == 0 || !duties.ContainsKey(objectRow)) continue;
            // Der erste gewinnt: doppelte Zeilen zu derselben Tuer sind dieselbe
            // Stelle (gemessen: identische Koordinaten).
            positions.TryAdd(objectRow, level);
        }

        var missing = 0;
        foreach (var (objectRow, duty) in duties)
        {
            if (duty.ContentType is not (TypeDungeon or TypeTrial or TypeRaid)) continue;

            if (!positions.TryGetValue(objectRow, out var level))
            {
                missing++;
                continue;
            }

            var territory = level.Territory.RowId;
            var mapId     = level.Map.RowId;
            var zoneName  = level.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText()
                            ?? _places.GetMapName(mapId);

            result.Add(new DutyEntrance(
                duty.Name,
                duty.ContentType,
                duty.TypeName,
                duty.Level,
                duty.ContentId,
                territory,
                mapId,
                new Vector3(level.X, level.Y, level.Z),
                zoneName ?? string.Empty));
        }

        _log.Info($"[Inhalte] {result.Count} Eingaenge zu Dungeons, Pruefungen und Raids mit Ort; "
                  + $"{missing} ohne Ortsangabe im Level-Sheet uebergangen.");
        return result;
    }
}
