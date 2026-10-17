using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One destination for a timed event the game currently has switched on.</summary>
/// <param name="EventName">Spoken quest name of that event's starting quest in the zone.</param>
/// <param name="ZoneName">Localised territory place name.</param>
/// <param name="TerritoryId">Destination TerritoryType RowId.</param>
/// <param name="MapId">Map used for hop routing.</param>
/// <param name="Position">World position of the quest issuer.</param>
/// <param name="Hint">Short spoken hint after the line. Empty when the name already says where to go.</param>
public sealed record TimedEventDestination(
    string EventName,
    string ZoneName,
    uint TerritoryId,
    uint MapId,
    Vector3 Position,
    string Hint);

/// <summary>
/// One festival quest that can become a walk target. Built from the Quest sheet;
/// whether it is listed depends on the live festival phase.
/// </summary>
/// <param name="FestivalId">Quest.Festival row.</param>
/// <param name="FestivalBegin">Inclusive phase start (Quest.FestivalBegin).</param>
/// <param name="FestivalEnd">Inclusive phase end (Quest.FestivalEnd).</param>
/// <param name="QuestId">Quest row id. The lowest id in a territory is the start.</param>
/// <param name="Name">Spoken quest name.</param>
/// <param name="TerritoryId">Issuer Level territory. Zero is not a place.</param>
/// <param name="MapId">Issuer map, for the hop graph.</param>
/// <param name="ZoneName">Spoken place name.</param>
/// <param name="Position">Issuer world position.</param>
internal readonly record struct TimedEventQuest(
    uint FestivalId,
    byte FestivalBegin,
    byte FestivalEnd,
    uint QuestId,
    string Name,
    uint TerritoryId,
    uint MapId,
    string ZoneName,
    Vector3 Position);

/// <summary>
/// One FATE a festival quest names as something to do. The position is the
/// FATE's own EventRange, not the quest issuer.
/// </summary>
/// <param name="FestivalId">Festival of the quest whose script names this FATE.</param>
/// <param name="FestivalBegin">Inclusive phase start of that quest.</param>
/// <param name="FestivalEnd">Inclusive phase end of that quest.</param>
/// <param name="FateId">Fate sheet row. The lowest id wins when two share a spot.</param>
/// <param name="Name">Spoken FATE name.</param>
/// <param name="TerritoryId">Open-world territory of the EventRange.</param>
/// <param name="MapId">That territory's map, for the hop graph.</param>
/// <param name="ZoneName">Spoken place name.</param>
/// <param name="Position">World position of the EventRange.</param>
/// <param name="Hint">Spoken after the line. Empty in tests that do not care.</param>
internal readonly record struct TimedEventFate(
    uint FestivalId,
    byte FestivalBegin,
    byte FestivalEnd,
    uint FateId,
    string Name,
    uint TerritoryId,
    uint MapId,
    string ZoneName,
    Vector3 Position,
    string Hint);

/// <summary>
/// Picks the walk targets for the festivals the client currently has active.
/// One quest start per festival and territory: the earliest quest whose phase
/// range contains the live phase. Later quests in the same city stay off that
/// part of the list because they start at the same event. FATE spots named by
/// a quest script in the live phase are extra places and stay on the list.
/// </summary>
internal static class TimedEventSelection
{
    /// <summary>True when <paramref name="phase"/> lies in the quest's inclusive range.</summary>
    public static bool PhaseMatches(byte begin, byte end, ushort phase)
        => phase >= begin && phase <= end;

    /// <summary>
    /// A quest script slot that names a FATE row, such as <c>FATE0</c>.
    /// Other slots can carry a number that happens to equal a FATE row
    /// (<c>LOC_MOTION0</c>) and are not places to walk to.
    /// </summary>
    public static bool IsFateScript(string instruction)
    {
        if (instruction.Length <= 4 || !instruction.StartsWith("FATE", StringComparison.Ordinal))
            return false;
        for (var i = 4; i < instruction.Length; i++)
        {
            if (instruction[i] is < '0' or > '9') return false;
        }
        return true;
    }

    /// <summary>
    /// Destinations for <paramref name="active"/> festivals. Festival id 0 and
    /// repeated ids are ignored. Quests and FATEs outside the phase, without a
    /// territory, or without a name are ignored. Two FATEs of one festival at
    /// the same rounded spot become one line, the lower FATE id.
    /// </summary>
    public static List<TimedEventDestination> Pick(
        IReadOnlyList<(ushort Id, ushort Phase)> active,
        IReadOnlyList<TimedEventQuest> quests,
        IReadOnlyList<TimedEventFate>? fates = null)
    {
        var chosen = new List<TimedEventDestination>();
        var seen = new HashSet<ushort>();
        foreach (var (id, phase) in active)
        {
            if (id == 0 || !seen.Add(id)) continue;

            var bestByTerritory = new Dictionary<uint, TimedEventQuest>();
            foreach (var quest in quests)
            {
                if (quest.FestivalId != id) continue;
                if (!PhaseMatches(quest.FestivalBegin, quest.FestivalEnd, phase)) continue;
                if (quest.TerritoryId == 0 || quest.Name.Length == 0) continue;
                if (!bestByTerritory.TryGetValue(quest.TerritoryId, out var current)
                    || quest.QuestId < current.QuestId)
                    bestByTerritory[quest.TerritoryId] = quest;
            }

            foreach (var quest in bestByTerritory.Values.OrderBy(q => q.QuestId))
            {
                chosen.Add(new TimedEventDestination(
                    quest.Name, quest.ZoneName, quest.TerritoryId, quest.MapId, quest.Position,
                    string.Empty));
            }

            if (fates == null) continue;
            var seenSpot = new HashSet<(uint Territory, int X, int Z)>();
            foreach (var fate in fates.Where(f => f.FestivalId == id).OrderBy(f => f.FateId))
            {
                if (!PhaseMatches(fate.FestivalBegin, fate.FestivalEnd, phase)) continue;
                if (fate.TerritoryId == 0 || fate.Name.Length == 0) continue;
                if (fate.Position == Vector3.Zero) continue;
                var spot = (
                    fate.TerritoryId,
                    (int)MathF.Round(fate.Position.X),
                    (int)MathF.Round(fate.Position.Z));
                if (!seenSpot.Add(spot)) continue;
                chosen.Add(new TimedEventDestination(
                    fate.Name, fate.ZoneName, fate.TerritoryId, fate.MapId, fate.Position, fate.Hint));
            }
        }

        return chosen;
    }
}

/// <summary>
/// Browser catalogue for seasonal events that are on right now.
///
/// The client keeps the running festivals in <c>GameMain.ActiveFestivals</c>
/// (id + phase, unused slots are id 0). A festival drops out of that array when
/// the event ends and a new id appears when the next one starts. The Quest sheet
/// links the starting quests to that id (<c>Festival</c>, <c>FestivalBegin</c>,
/// <c>FestivalEnd</c>) and gives the issuer position (<c>IssuerLocation</c>).
/// The Festival sheet itself has no names (offline 2026-10-06: 264 rows, all
/// empty), so the spoken name is the quest name.
///
/// A quest script slot <c>FATE0</c>, <c>FATE1</c>, … names a Fate row that
/// belongs to that festival phase. <c>Fate.Location</c> is the instance id of
/// an EventRange in <c>planevent.lgb</c>. The open-world copy of that layout
/// (TerritoryIntendedUse 0 or 1) is the place to walk to. Measured 2026-10-06:
/// 52 such slots, 44 distinct ranges, all of them in those layouts, read from
/// 67 files in about a tenth of a second.
/// </summary>
public sealed class EventAreaService
{
    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly PlacesService _places;
    private readonly IPluginLog _log;

    private TimedEventCatalogue? _catalogue;
    private (bool Russian, bool German, ClientLanguage Language) _catalogueLanguage;
    private string _lastLogged = string.Empty;

    private readonly record struct TimedEventCatalogue(
        List<TimedEventQuest> Quests,
        List<TimedEventFate> Fates);

    public EventAreaService(
        IClientState clientState,
        IDataManager data,
        PlacesService places,
        IPluginLog log)
    {
        _clientState = clientState;
        _data = data;
        _places = places;
        _log = log;
    }

    /// <summary>
    /// Starting places and named FATE spots of the festivals that are active
    /// this frame, current zone first, then fewest hops. Empty when none are
    /// on or the player is logged out.
    /// </summary>
    public List<(TimedEventDestination Dest, bool InCurrentZone)> GetAreasSorted()
    {
        var active = ReadActiveFestivals();
        var catalogue = EnsureCatalogue();
        var picked = TimedEventSelection.Pick(active, catalogue.Quests, catalogue.Fates);
        var currentTerritory = (uint)_clientState.TerritoryType;
        var hopsByMap = _places.GetHopDistances();

        var result = picked
            .Select(dest => (Dest: dest, InCurrentZone: dest.TerritoryId == currentTerritory))
            .ToList();

        int HopsTo(uint mapId, bool here)
        {
            if (here) return 0;
            if (mapId != 0 && hopsByMap.TryGetValue(mapId, out var hops)) return hops;
            return int.MaxValue / 4;
        }

        result = result
            .OrderByDescending(x => x.InCurrentZone)
            .ThenBy(x => HopsTo(x.Dest.MapId, x.InCurrentZone))
            .ThenBy(x => x.Dest.ZoneName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var signature = string.Join(";", result.Select(x =>
            $"{x.Dest.TerritoryId}:{x.Dest.EventName}:{x.Dest.Position.X:F0}:{x.Dest.Position.Z:F0}"));
        if (signature != _lastLogged)
        {
            _lastLogged = signature;
            var ids = string.Join(",", active.Select(f => $"{f.Id}/{f.Phase}"));
            _log.Info($"[Events] Festivals [{ids}] → {result.Count} Orte.");
        }

        return result;
    }

    /// <summary>How many destinations the timed-event list currently holds.</summary>
    public int Count => GetAreasSorted().Count;

    /// <summary>
    /// Debug dump for the object probe: live festival ids and the places chosen
    /// from them. Reads the same source as the list.
    /// </summary>
    public void DumpActiveEvents()
    {
        var active = ReadActiveFestivals();
        _log.Info(
            $"[Events] === Festivals {active.Count} terr={_clientState.TerritoryType} ===");
        foreach (var (id, phase) in active)
            _log.Info($"[Events] Festival id={id} phase={phase}");

        foreach (var (dest, here) in GetAreasSorted())
        {
            _log.Info(
                $"[Events] '{dest.EventName}' terr={dest.TerritoryId} '{dest.ZoneName}' " +
                $"hier={here} pos=({dest.Position.X:F1}|{dest.Position.Y:F1}|{dest.Position.Z:F1})");
        }
    }

    private unsafe List<(ushort Id, ushort Phase)> ReadActiveFestivals()
    {
        var result = new List<(ushort Id, ushort Phase)>();
        if (!_clientState.IsLoggedIn) return result;

        var main = GameMain.Instance();
        if (main == null) return result;

        var active = main->ActiveFestivals;
        for (var i = 0; i < active.Length; i++)
        {
            var festival = active[i];
            if (festival.Id == 0) continue;
            result.Add((festival.Id, festival.Phase));
        }

        return result;
    }

    private TimedEventCatalogue EnsureCatalogue()
    {
        var language = (Loc.IsRussian, Loc.IsGerman, _data.Language);
        if (_catalogue != null && _catalogueLanguage == language) return _catalogue.Value;
        _catalogueLanguage = language;
        _catalogue = BuildCatalogue();
        return _catalogue.Value;
    }

    private TimedEventCatalogue BuildCatalogue()
    {
        var questsResult = new List<TimedEventQuest>();
        var links = new List<(uint FestivalId, byte Begin, byte End, uint FateId, uint Location, string Name)>();
        var quests = _data.GetExcelSheet<Quest>();
        var territories = _data.GetExcelSheet<TerritoryType>();
        var fates = _data.GetExcelSheet<Fate>();
        if (quests == null || territories == null || fates == null)
            return new TimedEventCatalogue(questsResult, new List<TimedEventFate>());

        foreach (var quest in quests)
        {
            var festivalId = quest.Festival.RowId;
            if (festivalId == 0) continue;
            CollectFateLinks(quest, fates, links);

            var level = quest.IssuerLocation.ValueNullable;
            if (level == null) continue;
            var territoryId = level.Value.Territory.RowId;
            if (territoryId == 0) continue;
            if (level.Value.X == 0f && level.Value.Y == 0f && level.Value.Z == 0f) continue;
            if (!territories.TryGetRow(territoryId, out var territory)) continue;

            var name = SpeakableQuestName(quest);
            if (name.Length == 0) continue;

            var zone = PlaceName(territory);
            if (zone.Length == 0) continue;

            var mapId = level.Value.Map.RowId;
            if (mapId == 0) mapId = territory.Map.RowId;

            questsResult.Add(new TimedEventQuest(
                festivalId,
                quest.FestivalBegin,
                quest.FestivalEnd,
                quest.RowId,
                name,
                territoryId,
                mapId,
                zone,
                new Vector3(level.Value.X, level.Value.Y, level.Value.Z)));
        }

        var fateResult = ResolveFateSpots(links, territories);
        _log.Info($"[Events] Quest-Katalog: {questsResult.Count} Event-Starts, {fateResult.Count} FATE-Orte.");
        return new TimedEventCatalogue(questsResult, fateResult);
    }

    private static void CollectFateLinks(
        Quest quest,
        Lumina.Excel.ExcelSheet<Fate> fates,
        List<(uint FestivalId, byte Begin, byte End, uint FateId, uint Location, string Name)> links)
    {
        for (var i = 0; i < quest.QuestParams.Count; i++)
        {
            var instruction = quest.QuestParams[i].ScriptInstruction.ExtractText();
            if (!TimedEventSelection.IsFateScript(instruction)) continue;
            var fateId = quest.QuestParams[i].ScriptArg;
            if (fateId == 0 || !fates.TryGetRow(fateId, out var fate) || fate.Location == 0) continue;
            var name = SpeakableFateName(fate);
            if (name.Length == 0) continue;
            links.Add((quest.Festival.RowId, quest.FestivalBegin, quest.FestivalEnd, fate.RowId, fate.Location, name));
        }
    }

    /// <summary>
    /// World position of each named FATE. Several territories share one layout
    /// file; the row with the lowest intended use, then the lowest row id, is
    /// the city or field the player can walk. Instance copies stay out.
    /// </summary>
    private List<TimedEventFate> ResolveFateSpots(
        List<(uint FestivalId, byte Begin, byte End, uint FateId, uint Location, string Name)> links,
        Lumina.Excel.ExcelSheet<TerritoryType> territories)
    {
        var result = new List<TimedEventFate>();
        if (links.Count == 0) return result;

        var wanted = new HashSet<uint>(links.Select(link => link.Location));
        var layouts = new Dictionary<string, (uint TerritoryId, uint IntendedUse, uint MapId, string Zone)>(StringComparer.Ordinal);
        foreach (var territory in territories)
        {
            var use = territory.TerritoryIntendedUse.RowId;
            if (use is not (0 or 1)) continue;
            var bg = territory.Bg.ExtractText();
            var cut = bg.LastIndexOf("/level/", StringComparison.Ordinal);
            if (cut < 0) continue;
            var path = "bg/" + bg.Substring(0, cut + 7) + "planevent.lgb";
            var zone = PlaceName(territory);
            if (zone.Length == 0) continue;
            if (!layouts.TryGetValue(path, out var current)
                || use < current.IntendedUse
                || (use == current.IntendedUse && territory.RowId < current.TerritoryId))
            {
                layouts[path] = (territory.RowId, use, territory.Map.RowId, zone);
            }
        }

        var spots = new Dictionary<uint, (uint TerritoryId, uint MapId, string Zone, Vector3 Position)>();
        var watch = Stopwatch.StartNew();
        var files = 0;
        foreach (var (path, place) in layouts)
        {
            if (wanted.Count == 0) break;
            LgbFile? lgb;
            try
            {
                lgb = _data.GetFile<LgbFile>(path);
            }
            catch (Exception ex)
            {
                // External layout file. A missing or unreadable zone must not
                // take the event list down; the quest start is still there.
                _log.Warning($"[Events] {path} nicht lesbar: {ex.Message}");
                continue;
            }
            if (lgb == null) continue;
            files++;
            foreach (var layer in lgb.Layers)
            {
                foreach (var obj in layer.InstanceObjects)
                {
                    if (obj.AssetType != LayerEntryType.EventRange || !wanted.Contains(obj.InstanceId))
                        continue;
                    var raw = obj.Transform.Translation;
                    var pos = new Vector3(raw.X, raw.Y, raw.Z);
                    if (pos == Vector3.Zero) continue;
                    spots[obj.InstanceId] = (place.TerritoryId, place.MapId, place.Zone, pos);
                    wanted.Remove(obj.InstanceId);
                }
            }
        }
        watch.Stop();

        var hint = AccessibilityStrings.TimedEventFateHint;
        foreach (var link in links)
        {
            if (!spots.TryGetValue(link.Location, out var spot)) continue;
            result.Add(new TimedEventFate(
                link.FestivalId, link.Begin, link.End, link.FateId, link.Name,
                spot.TerritoryId, spot.MapId, spot.Zone, spot.Position, hint));
        }

        if (wanted.Count > 0)
            _log.Info($"[Events] FATE-Position fehlt: {string.Join(",", wanted)}.");
        _log.Info($"[Events] FATE-Dateien: {files}, {spots.Count} Orte, {watch.ElapsedMilliseconds} ms.");
        return result;
    }

    private static string PlaceName(TerritoryType territory)
        => RussianAuthorText.PlaceName(territory.PlaceName.RowId,
            territory.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty).Trim();

    /// <summary>Same name order as an event object that points at a quest.</summary>
    private static string SpeakableQuestName(Quest quest)
    {
        if (GameDisplayText.Name<Quest>(quest.RowId, static x => x.Name) is { Length: > 0 } displayed)
            return displayed.Trim();
        if (Loc.IsRussian && RussianQuestNames.QuestName(quest.RowId) is { Length: > 0 } russian)
            return russian.Trim();
        return quest.Name.ExtractText().Trim();
    }

    /// <summary>Displayed FATE name, then the sheet text.</summary>
    private static string SpeakableFateName(Fate fate)
    {
        if (GameDisplayText.Name<Fate>(fate.RowId, static x => x.Name) is { Length: > 0 } displayed)
            return displayed.Trim();
        return fate.Name.ExtractText().Trim();
    }
}
