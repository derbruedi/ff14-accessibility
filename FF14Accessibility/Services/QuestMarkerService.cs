using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaContentFinderCondition = Lumina.Excel.Sheets.ContentFinderCondition;
using LuminaEmote = Lumina.Excel.Sheets.Emote;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
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
    /// <summary>A live enemy of the RUNNING levequest. Unlike the two above this
    /// is a real world object, not a map marker - see LevequestEnemyService.</summary>
    LeveEnemy,
    /// <summary>An invisible walk-in volume (<c>Level.Type</c> 49 EventRange)
    /// tied to an accepted quest. Map markers (Type 51) only name a search
    /// circle; enemies/progress often start only after entering one of these
    /// smaller ranges (measured 2026-09-09/10, MSQ â€žDie Gabe der
    /// Unsterblichkeitâ€œ). Listed under Quest objects â€” not live GameObjects.</summary>
    QuestTrigger,
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
/// <param name="TargetBaseId">BaseId of the object this marker points at, from
/// the game's own marker -> Level sheet link (see
/// <see cref="QuestMarkerService.GetQuestObjectIds"/>); 0 for pure position
/// markers (areas, "go to X"). This is what lets the plugin aim at the right
/// NPC or prop instead of guessing by distance.</param>
/// <param name="TargetLevelType">The Level sheet's Type for that object
/// (8 = ENpcBase, 9 = BNpcBase, 45 = EObj), 0 when unknown. The BaseId alone
/// would be ambiguous - ENpcBase and BNpcBase are separate sheets with
/// overlapping row ids.</param>
/// <param name="Unlock">Was die Quest laut Quest-Blatt freischaltet, als Teilsatz
/// ("schaltet das Dungeon 'X' frei") - leer, wenn das Blatt nichts hergibt.
/// Gebraucht bei den NOCH NICHT angenommenen Quests.</param>
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
    QuestMarkerRole Role = QuestMarkerRole.Quest,
    uint TargetBaseId = 0,
    byte TargetLevelType = 0,
    // Was die Quest laut Quest-Blatt freischaltet, als Teilsatz ("schaltet das
    // Dungeon 'X' frei") - leer, wenn das Blatt nichts hergibt. Gebraucht wird
    // das bei den NOCH NICHT angenommenen Quests: dort ist "welche davon geben
    // mir etwas?" die eigentliche Frage, und die Markierung allein beantwortet
    // sie nicht (der Marker traegt Name, Ort und eine Id, aber keinen
    // Quest-Zeiger).
    string Unlock = "",
    uint QuestId = 0,
    uint ObjectiveLevelId = 0,
    uint NativeMarkerId = 0);

/// <summary>
/// Reads the objective markers of ACCEPTED quests from the game's map
/// singleton (Client.Game.UI.Map). Read fresh on every call, never cached.
/// All structs ilspycmd-verified, see docs/game-api.md -> "Quest-Marker".
/// </summary>
public sealed class QuestMarkerService
{
    /// <summary>Lumina <c>Level.Type</c> / ClientStructs <c>InstanceType.EventRange</c>.
    /// Sheet dump: Type 49 rows carry <c>Object</c> 5000000 and an <c>EventId</c>
    /// pointing at the Quest row â€” the walk-in volumes inside a map goal circle.</summary>
    private const byte LevelTypeEventRange = 49;

    /// <summary>Extra metres beyond a map marker's Radius when deciding whether
    /// an EventRange belongs to the current objective pin. Marker centres and
    /// range centres do not coincide (Gabe: ~15â€“18 m offset inside r=35).</summary>
    private const float EventRangeMarkerSlack = 15f;

    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;
    private readonly string? _harmoniaPacks;
    private bool _questNameRussian;
    private Dalamud.Game.ClientLanguage _questNameLanguage;
    private int _questNameDisplay;
    private readonly TravelMapIdentity _mapIdentity;

    public QuestMarkerService(IClientState clientState, IDataManager data, IPluginLog log, string? harmoniaPacks = null)
    {
        _clientState = clientState;
        _data = data;
        _log = log;
        _harmoniaPacks = harmoniaPacks;
        _mapIdentity = new(data);
    }

    private Dictionary<uint, List<EventRangeRow>>? _eventRangesByQuestId;

    /// <summary>One Level Type-49 row, cached once from the sheet.</summary>
    private readonly record struct EventRangeRow(
        uint LevelId, Vector3 Position, float Radius, ushort Territory, uint MapId);

    /// <summary>
    /// Resolve the marker's rendered name to quest rows first. The game's
    /// English sheet name does not match a Russian translation's map label.
    /// If a shared name belongs to different journal sections, do not guess.
    /// </summary>
    private QuestKind KindForLabel(string label)
    {
        var result = QuestKind.Unknown;
        foreach (var quest in QuestRows(label))
        {
            var genre = quest.JournalGenre.ValueNullable;
            var category = genre?.JournalCategory.ValueNullable;
            if (category == null) continue;
            var kind = KindForSection(category.Value.JournalSection.RowId);
            if (kind == QuestKind.Unknown) continue;
            if (result != QuestKind.Unknown && result != kind) return QuestKind.Unknown;
            result = kind;
        }
        return result;
    }

    /// <summary>
    /// Maps a JournalSection row to the kind spoken to the player. Section ids
    /// read from the game's own JournalSection sheet (offline dump 2026-08-06):
    /// 0 Hauptszenario (ARR-EW), 1 Hauptszenario (Dawntrail), 2 Chroniken der
    /// neuen Ã„ra, 3 NebenauftrÃ¤ge, 4/5 FreundesvÃ¶lker, 6 Klassen und Jobs,
    /// 7 Sonstige, 8 Freibriefe, 9 Inhalte. Sections 8 and 9 hold no quests at
    /// all (measured), so they - like anything unexpected - fall through to
    /// <see cref="QuestKind.Unknown"/> and stay silent rather than being folded
    /// into a neighbouring label that would misname them.
    /// </summary>
    internal static QuestKind KindForSection(uint sectionId) => sectionId switch
    {
        0 or 1 => QuestKind.MainStory,
        2      => QuestKind.Chronicle,
        3      => QuestKind.SideQuest,
        4 or 5 => QuestKind.BeastTribe,
        6      => QuestKind.Job,
        7      => QuestKind.Other,
        _      => QuestKind.Unknown,
    };

    /// <summary>
    /// Required level from the matching quest rows. Used only when the marker
    /// has no RecommendedLevel. Reused names with different levels stay unknown.
    /// </summary>
    private int LevelForLabel(string label)
    {
        var result = 0;
        foreach (var quest in QuestRows(label))
        {
            if (quest.ClassJobLevel.Count == 0) continue;
            var level = quest.ClassJobLevel[0];
            if (level <= 0) continue;
            if (result != 0 && result != level) return 0;
            result = level;
        }
        return result;
    }

    /// <summary>
    /// All Level Type-49 (EventRange) rows keyed by Quest RowId (<c>EventId</c>).
    /// Built once â€” the sheet is ~60k rows; browsing must not rescan it.
    /// </summary>
    private Dictionary<uint, List<EventRangeRow>> EventRangesByQuestId()
    {
        if (_eventRangesByQuestId != null) return _eventRangesByQuestId;

        var byQuest = new Dictionary<uint, List<EventRangeRow>>();
        var total = 0;
        foreach (var level in _data.GetExcelSheet<LuminaLevel>())
        {
            if (level.Type != LevelTypeEventRange) continue;
            var questId = level.EventId.RowId;
            if (questId == 0) continue;

            var row = new EventRangeRow(
                level.RowId,
                new Vector3(level.X, level.Y, level.Z),
                level.Radius,
                (ushort)level.Territory.RowId,
                level.Map.RowId);

            if (!byQuest.TryGetValue(questId, out var list))
                byQuest[questId] = list = new List<EventRangeRow>();
            list.Add(row);
            total++;
        }

        _eventRangesByQuestId = byQuest;
        _log.Info($"[Quest] EventRange-Index: {total} AuslÃ¶ser fÃ¼r {byQuest.Count} Quests.");
        return byQuest;
    }

    /// <summary>
    /// Walk-in EventRange volumes for ACCEPTED quests in the current territory.
    ///
    /// Map markers (Type 51) only give a search circle; spawn/progress often
    /// needs entering a Type-49 volume linked by <c>Level.EventId</c> = Quest
    /// RowId (sheet + zone-probe 2026-09-10). Ranges are kept when they sit
    /// near an in-zone marker of that quest (marker Radius + slack), so later
    /// steps of the same quest do not flood the list. Listed under the Quest
    /// objects browser â€” these are not ObjectTable entries.
    /// </summary>
    public unsafe List<QuestDestination> GetEventRangeDestinations()
    {
        var result = new List<QuestDestination>();
        var map = Map.Instance();
        if (map == null) return result;

        var currentTerritory = _clientState.TerritoryType;
        var rangesByQuest = EventRangesByQuestId();
        var seenLevel = new HashSet<uint>();
        var trace = new List<string>();

        foreach (ref var marker in map->QuestMarkers)
        {
            var questName = marker.Label.ToString();
            if (string.IsNullOrWhiteSpace(questName)) continue;
            var quests = QuestRows(questName);
            if (quests.Count == 0) continue;

            var circles = new List<(Vector3 Centre, float Radius)>();
            var locations = marker.MarkerData.Count;
            if (locations is < 0 or > 100) continue;
            for (var i = 0; i < locations; i++)
            {
                var data = marker.MarkerData[i];
                if (data.TerritoryTypeId != currentTerritory) continue;
                circles.Add((data.Position, data.Radius));
            }
            if (circles.Count == 0) continue;

            var kind = KindForLabel(questName);
            var sheetLevel = LevelForLabel(questName);
            var markerLevel = 0;
            for (var i = 0; i < locations; i++)
            {
                var data = marker.MarkerData[i];
                if (data.TerritoryTypeId != currentTerritory) continue;
                if (data.RecommendedLevel > 0) { markerLevel = data.RecommendedLevel; break; }
            }
            var level = markerLevel > 0 ? markerLevel : sheetLevel;

            foreach (var quest in quests)
            {
                if (!rangesByQuest.TryGetValue(quest.RowId, out var ranges)) continue;
                foreach (var range in ranges)
                {
                    if (range.Territory != currentTerritory) continue;

                    var near = false;
                    foreach (var (centre, radius) in circles)
                    {
                        var limit = MathF.Max(radius, 1f) + EventRangeMarkerSlack;
                        var dx = range.Position.X - centre.X;
                        var dz = range.Position.Z - centre.Z;
                        if (MathF.Sqrt(dx * dx + dz * dz) <= limit)
                        {
                            near = true;
                            break;
                        }
                    }
                    if (!near || !seenLevel.Add(range.LevelId)) continue;

                    result.Add(new QuestDestination(
                        questName,
                        string.Empty,
                        range.Position,
                        range.Radius,
                        range.Territory,
                        range.MapId,
                        InCurrentZone: true,
                        kind,
                        level,
                        QuestMarkerRole.QuestTrigger));

                    trace.Add($"'{questName}' QuestId={quest.RowId} LevelId={range.LevelId} " +
                              $"pos=({range.Position.X:F0}|{range.Position.Z:F0}) r={range.Radius:F1}");
                }
            }
        }

        _log.Info($"[Quest] EventRange-AuslÃ¶ser in Zone ({result.Count}): " +
                  (trace.Count > 0 ? string.Join(" | ", trace) : "keine"));
        return result;
    }

    /// <summary>
    /// All objective locations of ACCEPTED quests (a quest can have several).
    /// Logs every marker as ground-truth probe for the two open runtime
    /// questions: zone field correctness and marker height vs. navmesh.
    /// </summary>
    public unsafe List<QuestDestination> GetDestinations(bool log = true)
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
            AddMarkerDestinations(result, marker, currentTerritory, "Quest", log: log);

        return result;
    }

    /// <summary>
    /// The objective locations of ACCEPTABLE quests near the player (quests
    /// not yet accepted). Read from Map.UnacceptedQuestMarkers, a linked list
    /// (StdList) - only real entries are present, no empty slots. Lets a blind
    /// player discover what quests can be picked up in the area.
    /// </summary>
    public unsafe List<QuestDestination> GetUnacceptedDestinations(bool log = true)
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
            AddMarkerDestinations(result, marker, currentTerritory, "OpenQuest", log: log);

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
    public unsafe Dictionary<string, string> GetQuestObjectives(bool log = true)
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
            if (log) _log.Info($"[Quest] Objective slot {slot}: '{name}' -> '{joined}'");
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

    private Dictionary<string, List<LuminaQuest>>? _questsByName;
    private Dictionary<uint, string>? _dutyNames;
    private bool _unlockFieldsChecked;

    /// <summary>
    /// Name des Inhalts hinter einer InstanceContent-Id. Der Weg ist der der
    /// Inhaltssuche: die Zeile <c>InstanceContent</c> selbst traegt KEINEN Namen
    /// (gemessen 2026-09-15 - der Uebersetzungsversuch brach mit CS1061 ab),
    /// benannt wird sie von <c>ContentFinderCondition.Content</c>. Erster Treffer
    /// gewinnt: mehrere Zeilen teilen sich einen Inhalt.
    /// </summary>
    private string DutyName(uint contentId)
    {
        if (_dutyNames == null)
        {
            var map = new Dictionary<uint, string>();
            foreach (var row in _data.GetExcelSheet<LuminaContentFinderCondition>())
            {
                var id = row.Content.RowId;
                if (id == 0) continue;
                var name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!map.ContainsKey(id)) map[id] = name;
            }
            _dutyNames = map;
            _log.Info($"[Quest] Inhalts-Namen fuer Freischaltungen geladen: {map.Count}");
        }

        return _dutyNames.GetValueOrDefault(contentId, string.Empty);
    }

    /// <summary>
    /// Was eine Quest laut Quest-Blatt freischaltet, als Teilsatz - leer, wenn
    /// das Blatt nichts hergibt.
    ///
    /// Anlass: bei den NOCH NICHT angenommenen Quests ist die Frage "welche davon
    /// geben mir etwas?" die eigentliche. Die Markierung allein beantwortet sie
    /// nicht: der Marker traegt Name, Ort und eine Id, aber keinen Quest-Zeiger.
    ///
    /// Quelle sind die Felder, deren NAME die Sache ausspricht -
    /// InstanceContentUnlock, ActionReward, GeneralActionReward, EmoteReward,
    /// ClassJobUnlock, SystemReward, OtherReward. Gemessen am Blatt der
    /// Installation (Dump 2026-09-15): 406 von 5373 Zeilen tragen mindestens
    /// eines. Der Name des Freigeschalteten kommt aus dem jeweiligen Blatt;
    /// laesst er sich nicht lesen, faellt nur der Name weg, nie der Satz.
    /// </summary>
    private string UnlockHint(string label)
    {
        string? result = null;
        foreach (var quest in QuestRows(label))
        {
            var hint = UnlockHint(quest);
            if (result != null && result != hint) return string.Empty;
            result = hint;
        }
        return result ?? string.Empty;
    }

    private string UnlockHint(LuminaQuest quest)
    {
        CheckUnlockFields();

        var dungeon = FieldId(quest, "InstanceContentUnlock");
        if (dungeon != 0)
            return AccessibilityStrings.QuestUnlocksDungeon(DutyName(dungeon));

        // Die Blattnamen dieser vier Belohnungsarten kommen hier englisch, im
        // Spiel stehen sie uebersetzt. Ueber die Zeilen-Id wird der russische
        // Name geholt; die uebrigen Tabellen dieses Dienstes bleiben bewusst
        // beim Blattnamen, weil sie ueber den Namen suchen (Label -> Zeile) -
        // eine uebersetzte Tabelle dort wuerde die Suche brechen.
        var emote = FieldId(quest, "EmoteReward");
        if (emote != 0)
        {
            var name = _data.GetExcelSheet<LuminaEmote>().TryGetRow(emote, out var row)
                ? RussianGameText.Name(_data, row, x => x.Name)
                : string.Empty;
            return AccessibilityStrings.QuestUnlocksEmote(name);
        }

        var action = FieldId(quest, "ActionReward");
        if (action != 0)
        {
            var name = _data.GetExcelSheet<LuminaAction>().TryGetRow(action, out var row)
                ? RussianGameText.Name(_data, row, x => x.Name)
                : string.Empty;
            return AccessibilityStrings.QuestUnlocksAction(name);
        }

        var general = FieldId(quest, "GeneralActionReward");
        if (general != 0)
        {
            var name = _data.GetExcelSheet<LuminaGeneralAction>().TryGetRow(general, out var row)
                ? RussianGameText.Name(_data, row, x => x.Name)
                : string.Empty;
            return AccessibilityStrings.QuestUnlocksAction(name);
        }

        var classJob = FieldId(quest, "ClassJobUnlock");
        if (classJob != 0)
        {
            var name = _data.GetExcelSheet<LuminaClassJob>().TryGetRow(classJob, out var row)
                ? RussianGameText.Name(_data, row, x => x.Name)
                : string.Empty;
            return AccessibilityStrings.QuestUnlocksClassJob(name);
        }

        if (FieldId(quest, "SystemReward") != 0 || FieldId(quest, "OtherReward") != 0)
            return AccessibilityStrings.QuestUnlocksSomething;

        return string.Empty;
    }

    /// <summary>Quest rows by either the game's sheet name or the Russian name
    /// rendered by the unofficial translation. Russian names come from our
    /// generated table and source-verified installed Harmonia packs. Keep all rows for ambiguous names so
    /// callers can refuse conflicting kinds, levels and unlock claims.</summary>
    private List<LuminaQuest> QuestRows(string label)
    {
        if (_questsByName == null || _questNameRussian != Loc.IsRussian || _questNameLanguage != _data.Language
            || _questNameDisplay != GameDisplayText.Revision)
        {
            var map = new Dictionary<string, List<LuminaQuest>>(System.StringComparer.OrdinalIgnoreCase);
            var legacy = new Dictionary<string, List<LuminaQuest>>(System.StringComparer.OrdinalIgnoreCase);
            var shownLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            static void AddName(Dictionary<string, List<LuminaQuest>> map, string name, LuminaQuest quest)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                name = name.Trim();
                if (!map.TryGetValue(name, out var list)) map[name] = list = new List<LuminaQuest>();
                if (!list.Any(row => row.RowId == quest.RowId)) list.Add(quest);
            }

            foreach (var quest in _data.GetExcelSheet<LuminaQuest>())
            {
                var displayed = GameDisplayText.Find(_data, quest, x => x.Name);
                var shown = (displayed ?? quest.Name).ExtractText().Trim();
                if (!string.IsNullOrEmpty(shown)) shownLabels.Add(shown);
                if (quest.JournalGenre.RowId == 0) continue; // "Ungueltige Kategorie"
                AddName(map, shown, quest);
                AddName(legacy, quest.Name.ExtractText(), quest);
                if (Loc.IsRussian && RussianQuestNames.QuestName(quest.RowId) is { } russian)
                    AddName(legacy, russian, quest);
            }
            if (Loc.IsRussian)
                foreach (var entry in HarmoniaQuestNames.LoadInstalled(_harmoniaPacks, _data, _log))
                    if (_data.GetExcelSheet<LuminaQuest>().TryGetRow(entry.RowId, out var quest) && quest.JournalGenre.RowId != 0)
                        AddName(map, entry.Name, quest);
            // A label currently shown by the game takes precedence over an
            // older catalog alias belonging to a different quest. Keep every
            // real duplicate, so conflicting current categories stay unknown.
            foreach (var (name, rows) in legacy)
                if (!map.ContainsKey(name) && !shownLabels.Contains(name)) map[name] = rows;
            _questsByName = map;
            _questNameRussian = Loc.IsRussian;
            _questNameLanguage = _data.Language;
            _questNameDisplay = GameDisplayText.Revision;
            _log.Info($"[Quest] Quest-Zeilen nach Blatt- und russischen Namen: {map.Count}");
        }

        return _questsByName.GetValueOrDefault(label.Trim(), new List<LuminaQuest>());
    }

    /// <summary>
    /// Meldet EINMAL je Sitzung, ob es die Freischalt-Felder in dieser
    /// Lumina-Fassung ueberhaupt gibt. Ohne diese Zeile waere ein fehlendes Feld
    /// von einem leeren nicht zu unterscheiden - und "schaltet nichts frei"
    /// waere eine Behauptung ohne Deckung.
    /// </summary>
    private void CheckUnlockFields()
    {
        if (_unlockFieldsChecked) return;
        _unlockFieldsChecked = true;

        var missing = UnlockFields
            .Where(f => typeof(LuminaQuest).GetProperty(f) == null)
            .ToList();
        _log.Info(missing.Count == 0
            ? $"[Quest] Freischalt-Felder vorhanden: {string.Join(", ", UnlockFields)}"
            : $"[Quest] Freischalt-Felder FEHLEN in dieser Lumina-Fassung: {string.Join(", ", missing)}");
    }

    /// <summary>Felder, deren Name die Sache ausspricht. Reihenfolge = Vorrang
    /// bei der Ansage: das Konkrete vor dem Allgemeinen.</summary>
    private static readonly string[] UnlockFields =
    {
        "InstanceContentUnlock", "EmoteReward", "ActionReward", "GeneralActionReward",
        "ClassJobUnlock", "SystemReward", "OtherReward",
    };

    /// <summary>
    /// Rohwert eines Blattfeldes. Zahlen direkt, Zeilenreferenzen ueber RowId,
    /// FELDER (Lumina-Collection) ueber das erste Element ungleich Null:
    /// SystemReward der Chocobo-Quest 66236 ist [0|17] - ein Feld, das als "0"
    /// gelesen als "traegt nichts" durchginge.
    ///
    /// Ueber Reflection, weil diese Lumina-Fassung die Feldtypen hier nicht
    /// ausschreibt; ein Feld, das es nicht gibt, liefert 0 und wird von
    /// <see cref="CheckUnlockFields"/> als fehlend gemeldet.
    /// </summary>
    private static uint FieldId(LuminaQuest quest, string field)
    {
        object? value;
        try // external call: Lumina row property
        {
            value = typeof(LuminaQuest).GetProperty(field)?.GetValue(quest);
        }
        catch (System.Exception)
        {
            return 0;
        }

        if (value is System.Collections.IEnumerable items and not string)
        {
            foreach (var item in items)
            {
                var element = ScalarId(item);
                if (element != 0) return element;
            }
            return 0;
        }
        return ScalarId(value);
    }

    private static uint ScalarId(object? value)
    {
        switch (value)
        {
            case uint u: return u;
            case ushort us: return us;
            case byte b: return b;
            case null: return 0;
        }

        var rowId = value.GetType().GetProperty("RowId");
        return rowId?.GetValue(value) switch
        {
            uint r => r,
            ushort r => r,
            byte r => r,
            _ => 0,
        };
    }

    private unsafe void AddMarkerDestinations(
        List<QuestDestination> result, MarkerInfo marker, uint currentTerritory, string tag,
        QuestMarkerRole role = QuestMarkerRole.Quest, bool log = true)
    {
        var questName = marker.Label.ToString();
        if (string.IsNullOrWhiteSpace(questName)) return; // empty slot

        var kind = KindForLabel(questName);
        // The marker's own level beats the name lookup; the sheet only fills in
        // when the game leaves RecommendedLevel at 0 (runtime behaviour unknown,
        // hence both values in the log below).
        var sheetLevel = LevelForLabel(questName);

        // Was die Quest freischaltet - nur fuer normale Quest-Marker (nicht Leve).
        // Bei angenommenen Quests wird der Teilsatz in der Ansage weggelassen.
        var unlock = role == QuestMarkerRole.Quest ? UnlockHint(questName) : string.Empty;

        var locations = marker.MarkerData.Count;
        if (locations is < 0 or > 100)
        {
            // Foreign memory - a corrupt vector must not take the game down.
            _log.Warning($"[{tag}] Marker '{questName}': unplausible MarkerData.Count={locations}, Ã¼bersprungen.");
            return;
        }

        for (var i = 0; i < locations; i++)
        {
            var data = marker.MarkerData[i];
            var tooltip = data.TooltipString != null ? data.TooltipString->ToString() : string.Empty;
            var inZone = data.TerritoryTypeId == currentTerritory && (data.MapId == 0
                || _mapIdentity.Canonical(data.MapId) == _mapIdentity.Canonical(_clientState.MapId));
            if (log) _log.Info($"[{tag}] Marker '{questName}' [{i + 1}/{locations}]: tt='{tooltip}' unlock='{unlock}' " +
                      $"pos=({data.Position.X:F1}|{data.Position.Y:F1}|{data.Position.Z:F1}) " +
                      $"r={data.Radius:F1} terr={data.TerritoryTypeId} (aktuell={currentTerritory}) " +
                      $"map={data.MapId} icon={data.IconId} render={marker.ShouldRender} " +
                      $"kind={kind} questIds={string.Join(",", QuestRows(questName).Select(q => q.RowId))} " +
                      $"lvlMarker={data.RecommendedLevel} lvlSheet={sheetLevel}");
            var level = data.RecommendedLevel > 0 ? data.RecommendedLevel : sheetLevel;

            // The object behind THIS location, over the game's own link (the
            // same one GetQuestObjectIds uses): LevelId -> Level sheet row ->
            // Level.Object, typed by Level.Type. Only rows of the marker's zone
            // count; a foreign-zone identity is resolved only after arrival. 0 means "pure position
            // marker" - an area or a "go to X", where there is nothing to aim
            // at and the plugin must not guess one.
            var targetBaseId = 0u;
            byte targetType = 0;
            if (data.LevelId != 0
                && _data.GetExcelSheet<LuminaLevel>().TryGetRow(data.LevelId, out var levelRow))
            {
                var territory = levelRow.Territory.RowId;
                if (territory == 0 || territory == data.TerritoryTypeId)
                {
                    // Position-only Type-51 circles have Object=0, but their
                    // type still distinguishes an area from an exact actor.
                    targetBaseId = levelRow.Object.RowId;
                    targetType   = levelRow.Type;
                }
            }

            result.Add(new QuestDestination(questName, tooltip, data.Position,
                data.Radius, data.TerritoryTypeId, data.MapId, inZone, kind, level, role,
                targetBaseId, targetType, unlock,
                role == QuestMarkerRole.Quest && QuestRows(questName) is { Count: 1 } rows ? rows[0].RowId : 0, data.LevelId,
                role == QuestMarkerRole.Quest ? marker.ObjectiveId : 0));
        }
    }
}
