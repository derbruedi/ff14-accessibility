using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaLevel = Lumina.Excel.Sheets.Level;
using LuminaQuest = Lumina.Excel.Sheets.Quest;

namespace FF14Accessibility.Services;

/// <summary>
/// The role a marker plays, so the announcement can tell the player what a
/// destination IS. Regular quests carry no role; levequests split into the
/// giver NPC (Levemete, where a leve is accepted/handed in) and the objective
/// location (where the leve task is done) - the user wants to walk to both.
/// </summary>
public enum QuestMarkerRole
{
    /// <summary>A normal quest objective (no extra spoken role).</summary>
    Quest,
    /// <summary>A levequest giver NPC (Levemete) - where leves are accepted.</summary>
    LeveGiver,
    /// <summary>The objective location of an accepted levequest.</summary>
    LeveObjective,
}

/// <summary>
/// What KIND of quest a destination belongs to, so the announcement can tell a
/// blind player apart what a sighted player reads off the journal section.
/// Taken from the game's own journal taxonomy (Quest -> JournalGenre ->
/// JournalCategory -> JournalSection), never guessed from names.
/// <para>
/// EVERY known kind is spoken, side quests included (user decision 2026-08-06,
/// revised the same day). The first cut left side quests silent to keep
/// announcements short, but silence is ambiguous for a blind player: the user
/// heard nothing and could not tell "this is a side quest" from "the feature is
/// broken" - he asked whether it had shipped at all. A sighted player reads the
/// quest symbol and never has that doubt.
/// </para>
/// <para>
/// <see cref="Unknown"/> is the exception and stays silent ON PURPOSE: it means
/// the sheet lookup found nothing, so any word would be a claim we cannot back.
/// </para>
/// </summary>
public enum QuestKind
{
    /// <summary>Not found in the quest sheet - nothing is spoken.</summary>
    Unknown,
    /// <summary>Main scenario - journal sections 0 (ARR..EW) and 1 (Dawntrail).</summary>
    MainStory,
    /// <summary>Raid/alliance storylines - journal section 2.</summary>
    Chronicle,
    /// <summary>Side quests - journal section 3, the most common kind.</summary>
    SideQuest,
    /// <summary>Beast tribe quests - journal sections 4 and 5.</summary>
    BeastTribe,
    /// <summary>Class and job quests - journal section 6.</summary>
    Job,
    /// <summary>Grand company, seasonal and the like - journal section 7.</summary>
    Other,
}

/// <summary>One quest objective location, read from the game's map markers.</summary>
/// <param name="QuestName">Quest name from the marker label.</param>
/// <param name="Detail">Marker tooltip (may repeat the quest name).</param>
/// <param name="Position">Objective position in world coordinates.</param>
/// <param name="Radius">Objective area radius (0 for point targets).</param>
/// <param name="TerritoryTypeId">Zone the marker belongs to.</param>
/// <param name="MapId">Map the marker belongs to (for cross-zone routing).</param>
/// <param name="InCurrentZone">Whether the marker is in the player's current zone.</param>
/// <param name="Kind">Which journal section the quest belongs to.</param>
/// <param name="Level">Required quest level, 0 when unknown.</param>
/// <param name="Role">Giver NPC vs. objective for levequests; Quest otherwise.</param>
public sealed record QuestDestination(
    string QuestName,
    string Detail,
    Vector3 Position,
    float Radius,
    ushort TerritoryTypeId,
    uint MapId,
    bool InCurrentZone,
    QuestKind Kind,
    int Level,
    QuestMarkerRole Role = QuestMarkerRole.Quest);

/// <summary>
/// Reads the objective markers of ACCEPTED quests from the game's map
/// singleton (Client.Game.UI.Map). Read fresh on every call, never cached.
/// All structs ilspycmd-verified, see docs/game-api.md -> "Quest-Marker".
/// </summary>
public sealed class QuestMarkerService
{
    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    public QuestMarkerService(IClientState clientState, IDataManager data, IPluginLog log)
    {
        _clientState = clientState;
        _data = data;
        _log = log;
    }

    private Dictionary<string, QuestKind>? _questKinds;

    /// <summary>
    /// Quest name -> kind, built once from the Quest sheet by walking the game's
    /// own journal taxonomy (JournalGenre -> JournalCategory -> JournalSection).
    /// Matched against the marker label, because MarkerInfo carries no quest
    /// pointer - only a label and an ObjectiveId.
    /// <para>
    /// Rows WITHOUT a journal genre are skipped, and that is what makes the name
    /// lookup trustworthy: the sheet holds duplicate quest names whose sections
    /// disagree (e.g. "In flagranti" as both section 0 and "Ungültige
    /// Kategorie"). Measured on the 2026-08-06 sheet dump: 44 of 5276 names
    /// conflict, and skipping the genre-less rows brings that to exactly 0.
    /// </para>
    /// </summary>
    private Dictionary<string, QuestKind> QuestKinds()
    {
        if (_questKinds != null) return _questKinds;

        var kinds = new Dictionary<string, QuestKind>();
        foreach (var quest in _data.GetExcelSheet<LuminaQuest>())
        {
            var name = quest.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (quest.JournalGenre.RowId == 0) continue;   // "Ungültige Kategorie" row

            var genre = quest.JournalGenre.ValueNullable;
            var category = genre?.JournalCategory.ValueNullable;
            if (category == null) continue;

            var kind = KindForSection(category.Value.JournalSection.RowId);
            if (kind != QuestKind.Unknown)
                kinds[name] = kind;
        }

        _questKinds = kinds;
        _log.Info($"[Quest] Quest-Arten geladen: {kinds.Count} benannte Quests " +
                  $"({kinds.Values.Count(k => k == QuestKind.MainStory)} Hauptszenario, " +
                  $"{kinds.Values.Count(k => k == QuestKind.SideQuest)} Nebenauftrag, " +
                  $"{kinds.Values.Count(k => k == QuestKind.Job)} Job, " +
                  $"{kinds.Values.Count(k => k == QuestKind.BeastTribe)} Freundesvolk, " +
                  $"{kinds.Values.Count(k => k == QuestKind.Chronicle)} Chronik, " +
                  $"{kinds.Values.Count(k => k == QuestKind.Other)} Sonstiges).");
        return kinds;
    }

    /// <summary>
    /// Maps a JournalSection row to the kind spoken to the player. Section ids
    /// read from the game's own JournalSection sheet (offline dump 2026-08-06):
    /// 0 Hauptszenario (ARR-EW), 1 Hauptszenario (Dawntrail), 2 Chroniken der
    /// neuen Ära, 3 Nebenaufträge, 4/5 Freundesvölker, 6 Klassen und Jobs,
    /// 7 Sonstige, 8 Freibriefe, 9 Inhalte. Sections 8 and 9 hold no quests at
    /// all (measured), so they - like anything unexpected - fall through to
    /// <see cref="QuestKind.Unknown"/> and stay silent rather than being folded
    /// into a neighbouring label that would misname them.
    /// </summary>
    private static QuestKind KindForSection(uint sectionId) => sectionId switch
    {
        0 or 1 => QuestKind.MainStory,
        2      => QuestKind.Chronicle,
        3      => QuestKind.SideQuest,
        4 or 5 => QuestKind.BeastTribe,
        6      => QuestKind.Job,
        7      => QuestKind.Other,
        _      => QuestKind.Unknown,
    };

    private Dictionary<string, int>? _questLevels;

    /// <summary>
    /// Quest name -> required level, built once from the Quest sheet. Used only
    /// as a FALLBACK: the marker carries its own RecommendedLevel, and matching
    /// by name is imprecise (FFXIV reuses quest names, e.g. for repeatables -
    /// the first row wins here). Level = ClassJobLevel[0], the field the journal
    /// shows; both sources are logged per marker so a mismatch is visible.
    /// </summary>
    private Dictionary<string, int> QuestLevels()
    {
        if (_questLevels != null) return _questLevels;

        var levels = new Dictionary<string, int>();
        foreach (var quest in _data.GetExcelSheet<LuminaQuest>())
        {
            var name = quest.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name) || levels.ContainsKey(name)) continue;
            if (quest.ClassJobLevel.Count > 0)
                levels[name] = quest.ClassJobLevel[0];
        }

        _questLevels = levels;
        _log.Info($"[Quest] Quest-Stufen aus dem Sheet geladen: {levels.Count}");
        return levels;
    }

    /// <summary>
    /// All objective locations of ACCEPTED quests (a quest can have several).
    /// Logs every marker as ground-truth probe for the two open runtime
    /// questions: zone field correctness and marker height vs. navmesh.
    /// </summary>
    public unsafe List<QuestDestination> GetDestinations()
    {
        var result = new List<QuestDestination>();
        var map = Map.Instance();
        if (map == null)
        {
            _log.Warning("[Quest] Map.Instance() ist null - keine Quest-Marker lesbar.");
            return result;
        }

        var currentTerritory = _clientState.TerritoryType;
        // QuestMarkers is a fixed 30-slot span; empty slots have a blank label.
        foreach (ref var marker in map->QuestMarkers)
            AddMarkerDestinations(result, marker, currentTerritory, "Quest");

        return result;
    }

    /// <summary>
    /// The objective locations of ACCEPTABLE quests near the player (quests
    /// not yet accepted). Read from Map.UnacceptedQuestMarkers, a linked list
    /// (StdList) - only real entries are present, no empty slots. Lets a blind
    /// player discover what quests can be picked up in the area.
    /// </summary>
    public unsafe List<QuestDestination> GetUnacceptedDestinations()
    {
        var result = new List<QuestDestination>();
        var map = Map.Instance();
        if (map == null)
        {
            _log.Warning("[Quest] Map.Instance() ist null - keine annehmbaren Quests lesbar.");
            return result;
        }

        var currentTerritory = _clientState.TerritoryType;
        // StdList yields each MarkerInfo by value (a read-only copy); its inner
        // pointers still reference live game memory, safe to read here.
        foreach (var marker in map->UnacceptedQuestMarkers)
            AddMarkerDestinations(result, marker, currentTerritory, "OpenQuest");

        return result;
    }

    /// <summary>
    /// All levequest ("Freibrief") destinations: the giver NPCs (Levemete, where
    /// leves are accepted / handed in) AND the objective locations of accepted
    /// leves, so a blind player can walk to both from one category (user request
    /// 2026-07-28).
    ///
    /// Sources, ilspycmd-verified on the Map singleton (2026-07-28):
    ///   Map.GuildLeveAssignmentMarkers (StdList&lt;MarkerInfo&gt;) = giver NPCs,
    ///   Map.LevequestMarkers (Span, 16 slots of MarkerInfo)       = objectives.
    /// Both reuse the SAME MarkerInfo extraction as regular quests, so their raw
    /// label/tooltip/territory/position are logged per marker ([LeveGiver] /
    /// [LeveGoal]) - the first in-game test confirms what the game actually puts
    /// in these fields (runtime content of leve markers was not verifiable offline).
    /// </summary>
    public unsafe List<QuestDestination> GetLevequestDestinations()
    {
        var result = new List<QuestDestination>();
        var map = Map.Instance();
        if (map == null)
        {
            _log.Warning("[Leve] Map.Instance() ist null - keine Freibrief-Marker lesbar.");
            return result;
        }

        var currentTerritory = _clientState.TerritoryType;

        // Giver NPCs (Levemete): a StdList, only real entries, no empty slots.
        foreach (var marker in map->GuildLeveAssignmentMarkers)
            AddMarkerDestinations(result, marker, currentTerritory, "LeveGiver", QuestMarkerRole.LeveGiver);

        // Objectives of accepted leves: a fixed 16-slot span, empty slots blank.
        foreach (ref var marker in map->LevequestMarkers)
            AddMarkerDestinations(result, marker, currentTerritory, "LeveGoal", QuestMarkerRole.LeveObjective);

        return result;
    }

    /// <summary>
    /// Maps quest name -> current objective text ("what is still missing", e.g.
    /// "Aurelias mit Hermetik erlegen 0/3") by reading the on-screen quest tracker
    /// (_ToDoList). The objective text only exists in the running tracker - the
    /// QuestManager exposes only sequence numbers, and the todo strings are not in
    /// a plain Excel sheet. Only TRACKED quests appear here; others return no entry.
    ///
    /// Node-id layout verified from the probe (log 2026-07-12 19:59): quest-name
    /// headers are ids 70001.. (70000 + slot), objectives are ids 20SSNN
    /// (20000 + slot*100 + index), so objectives group under the header of the
    /// same slot. Each mapping is logged once per call for verification.
    /// </summary>
    public unsafe Dictionary<string, string> GetQuestObjectives()
    {
        var map = new Dictionary<string, string>();
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return map;
        var addon = mgr->GetAddonByName("_ToDoList");
        if (addon == null || !addon->IsVisible) return map;

        var nameBySlot = new Dictionary<int, string>();
        var objsBySlot = new Dictionary<int, List<string>>();

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;
            var text = AtkText.Read((AtkTextNode*)node);
            if (string.IsNullOrWhiteSpace(text)) continue;

            var id = node->NodeId;
            if (id is >= 70001 and <= 70099)
            {
                nameBySlot[(int)(id - 70000)] = text;
            }
            else if (id is >= 20000 and <= 20999)
            {
                var slot = (int)((id - 20000) / 100);
                if (!objsBySlot.TryGetValue(slot, out var list))
                    objsBySlot[slot] = list = new List<string>();
                list.Add(text);
            }
        }

        foreach (var (slot, name) in nameBySlot)
        {
            if (!objsBySlot.TryGetValue(slot, out var objs) || objs.Count == 0) continue;
            var joined = string.Join(", ", objs);
            map[name] = joined;
            _log.Info($"[Quest] Objective slot {slot}: '{name}' -> '{joined}'");
        }
        return map;
    }

    /// <summary>
    /// Reads every objective location of a single marker into <paramref name="result"/>.
    /// Shared by the accepted (span) and unaccepted (list) marker sources; the
    /// marker is taken by value (144 bytes) so both callers can pass their loop
    /// variable regardless of ref-ness.
    /// </summary>
    /// <summary>
    /// Data-sheet ids of the objects the CURRENT quest markers point at - both
    /// accepted quests and acceptable ones. The link is the marker's
    /// <c>LevelId</c> (MapMarkerData @0, first parameter of SetData): it names a
    /// row of the Level sheet, and that row carries the object standing at that
    /// spot (<c>Level.Object</c>, uint @20, typed by <c>Level.Type</c> @32:
    /// 8 = ENpcBase, 9 = BNpcBase, 45 = EObj). Those are the same ids the object
    /// browser sees as <c>IGameObject.BaseId</c> - the sheet lookup for NPC titles
    /// in NavigationService already matches ENpcResident on BaseId the same way.
    /// So this is the game's own link between "a quest points here" and the object
    /// in front of the player: no icon table to interpret, no distance guess.
    ///
    /// NOT usable for this: <c>MapMarkerData.DataId</c> @68. It is a ushort - too
    /// narrow for an NPC BaseId in the millions - and <c>SetData</c> never writes
    /// it, so it stayed 0 for every marker (measured 2026-08-02, "0 Ids aus
    /// Markern"). Both facts ilspycmd-verified on MapMarkerData.
    ///
    /// Only rows of the CURRENT zone count - an id from another zone could
    /// otherwise flag a same-model NPC standing right next to the player.
    /// </summary>
    public unsafe HashSet<uint> GetQuestObjectIds()
    {
        var ids = new HashSet<uint>();
        var map = Map.Instance();
        if (map == null)
        {
            _log.Warning("[Quest] Map.Instance() ist null - keine Quest-Objekt-Ids lesbar.");
            return ids;
        }

        var trace = new List<string>();
        var currentTerritory = _clientState.TerritoryType;
        foreach (ref var marker in map->QuestMarkers)
            CollectMarkerObjectIds(ids, trace, marker, currentTerritory);
        foreach (var marker in map->UnacceptedQuestMarkers)
            CollectMarkerObjectIds(ids, trace, marker, currentTerritory);

        // One compact line per call: which marker resolved to which object, and
        // why a location was dropped. Without it an empty category gives no clue
        // whether the markers, the sheet or the zone filter is at fault.
        _log.Info($"[Quest] Objekt-Ids aus Markern ({ids.Count}): " +
                  (trace.Count > 0 ? string.Join(" | ", trace) : "keine Marker-Orte"));
        return ids;
    }

    /// <summary>
    /// Resolves one marker's locations to object ids via the Level sheet and adds
    /// them to <paramref name="ids"/>. <paramref name="trace"/> collects one short
    /// human-readable entry per location for the caller's log line.
    /// </summary>
    private unsafe void CollectMarkerObjectIds(
        HashSet<uint> ids, List<string> trace, MarkerInfo marker, uint currentTerritory)
    {
        var label = marker.Label.ToString();
        if (string.IsNullOrWhiteSpace(label)) return; // empty slot

        var locations = marker.MarkerData.Count;
        if (locations is < 0 or > 100) return; // same corruption guard as above

        for (var i = 0; i < locations; i++)
        {
            var data = marker.MarkerData[i];
            if (data.LevelId == 0)
            {
                trace.Add($"'{label}'[{i + 1}] LevelId=0");
                continue;
            }

            if (!_data.GetExcelSheet<LuminaLevel>().TryGetRow(data.LevelId, out var level))
            {
                trace.Add($"'{label}'[{i + 1}] LevelId={data.LevelId} nicht im Sheet");
                continue;
            }

            var objectId = level.Object.RowId;
            var territory = level.Territory.RowId;
            trace.Add($"'{label}'[{i + 1}] LevelId={data.LevelId}->Obj={objectId} Typ={level.Type} terr={territory}");

            if (objectId == 0) continue;                                   // pure position marker
            if (territory != 0 && territory != currentTerritory) continue; // other zone
            ids.Add(objectId);
        }
    }

    private unsafe void AddMarkerDestinations(
        List<QuestDestination> result, MarkerInfo marker, uint currentTerritory, string tag,
        QuestMarkerRole role = QuestMarkerRole.Quest)
    {
        var questName = marker.Label.ToString();
        if (string.IsNullOrWhiteSpace(questName)) return; // empty slot

        var kind = QuestKinds().GetValueOrDefault(questName, QuestKind.Unknown);
        // The marker's own level beats the name lookup; the sheet only fills in
        // when the game leaves RecommendedLevel at 0 (runtime behaviour unknown,
        // hence both values in the log below).
        var sheetLevel = QuestLevels().GetValueOrDefault(questName, 0);

        var locations = marker.MarkerData.Count;
        if (locations is < 0 or > 100)
        {
            // Foreign memory - a corrupt vector must not take the game down.
            _log.Warning($"[{tag}] Marker '{questName}': unplausible MarkerData.Count={locations}, übersprungen.");
            return;
        }

        for (var i = 0; i < locations; i++)
        {
            var data = marker.MarkerData[i];
            var tooltip = data.TooltipString != null ? data.TooltipString->ToString() : string.Empty;
            var inZone = data.TerritoryTypeId == currentTerritory;
            _log.Info($"[{tag}] Marker '{questName}' [{i + 1}/{locations}]: tt='{tooltip}' " +
                      $"pos=({data.Position.X:F1}|{data.Position.Y:F1}|{data.Position.Z:F1}) " +
                      $"r={data.Radius:F1} terr={data.TerritoryTypeId} (aktuell={currentTerritory}) " +
                      $"map={data.MapId} icon={data.IconId} render={marker.ShouldRender} " +
                      $"lvlMarker={data.RecommendedLevel} lvlSheet={sheetLevel}");
            var level = data.RecommendedLevel > 0 ? data.RecommendedLevel : sheetLevel;
            result.Add(new QuestDestination(questName, tooltip, data.Position,
                data.Radius, data.TerritoryTypeId, data.MapId, inZone, kind, level, role));
        }
    }
}
