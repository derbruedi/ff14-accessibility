using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Lumina.Excel.Sheets;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace FF14Accessibility.Services;

/// <summary>One kind of enemy a running battle leve asks the player to kill.</summary>
/// <param name="BaseId">BNpcBase row id - what the SPAWNED object carries as its
/// data id. Measured 2026-08-18: leve 528 slot 0 has BaseID 339, and the monster
/// that appeared 2.9 m from the player was DataId 339.</param>
/// <param name="NameId">BNpcName row id from the same slot. NOT what the spawned
/// object carries: the slot said 1096 "streunender Dodo", the object said 393
/// "Dodo". Kept for the log and for the spoken name.</param>
/// <param name="Name">Monster name from the sheet, for speech and for the log.</param>
/// <param name="Required">How many of them the leve wants (0 = not spoken).</param>
public sealed record LeveEnemySpec(uint BaseId, uint NameId, string Name, uint Required);

/// <summary>The levequest that is currently RUNNING, with what it asks for.</summary>
/// <param name="LeveId">Leve sheet row id.</param>
/// <param name="LeveName">Leve name as shown in the journal.</param>
/// <param name="Objective">The director's own objective line, or empty.</param>
/// <param name="Enemies">Enemy kinds of the leve; empty for non-combat leves.</param>
/// <param name="EventObjectAddresses">The objects the director itself holds
/// (EventHandler.EventObjects). MEASURED EMPTY on a running battle leve
/// (2026-08-18) - kept because it costs nothing and would be exact if a future
/// patch filled it.</param>
/// <param name="DirectorAddress">The director itself, for its to-do lines.</param>
/// <param name="DirectorEventId">The director's event id (EventHandler.Info.EventId).
/// The MONSTERS of this leve carry the very same value in GameObject.EventId -
/// that is the link the whole category rests on, see
/// <see cref="LevequestEnemyService.BelongsToLeve"/>.</param>
public sealed record RunningLeve(
    ushort LeveId,
    string LeveName,
    string Objective,
    IReadOnlyList<LeveEnemySpec> Enemies,
    IReadOnlyList<nint> EventObjectAddresses,
    nint DirectorAddress,
    uint DirectorEventId);

/// <summary>
/// Finds the levequest that is currently RUNNING and the enemies it asks for.
///
/// WHY a running leve and not just an accepted one: the enemies of a battle leve
/// only exist while the leve runs, and their names are ordinary world monster
/// names ("Nussknackerhörnchen"). Listing them off an accepted-but-not-started
/// leve would point the player at random wildlife that killing does not count.
///
/// HOW the running leve is found (offsets ilspycmd-verified, values MEASURED in
/// the live log 2026-08-18):
///   EventFramework.Instance()->DirectorModule.DirectorList
///     -> StdVector&lt;Pointer&lt;Director&gt;&gt;, every active director.
///   Director.Info (EventHandler.Info @32) -> EventHandlerInfo.EventId @0
///     -> EventId.ContentId @2 names the DIRECTOR KIND:
///        BattleLeveDirector 32769, GatheringLeveDirector 32770,
///        CompanyLeveDirector 32775.
///   Director.ContentId @736 is the LEVE ID.
/// Both were read off a running leve: "Content=BattleLeveDirector(32769)
/// Entry=537 DirectorContentId=528 Titel='Lästige Nager'" - and leve 528 in the
/// sheet is indeed "Lästige Nager". EventId.EntryId is NOT the leve id: it read
/// 537 and 542 on two runs of the same leve.
///
/// Only fields of the 1120-byte Director BASE are read, never LeveDirector's own
/// (LeveId @1120) - a director that is not a leve director would be an overread
/// there, and "which subclass is this pointer" cannot be answered from the
/// struct.
///
/// The id is CROSS-CHECKED against QuestManager.LeveQuests (the accepted leves)
/// before anything is read from the sheets. Two independent sources have to name
/// the same leve, so a director kind added by a future patch cannot make the
/// browser announce a leve the player does not hold.
///
/// Enemies come from the SHEETS, not from the director struct: Leve.DataId
/// points at BattleLeve (mercenary leves) or CompanyLeve (grand company leves),
/// both of which carry one BNpcName per objective slot plus the required count.
/// Verified offline against the installed game data (2026-08-18): assignment
/// type 1 "Söldner" resolves to BattleLeve, types 13-15 (the three grand
/// companies) to CompanyLeve, and the four leve data sheets use disjoint row-id
/// ranges, so RowRef.Is&lt;T&gt;() picks the right one.
/// </summary>
public sealed class LevequestEnemyService
{
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    public LevequestEnemyService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log = log;
    }

    /// <summary>
    /// The levequest currently running, or null when none is. Read fresh on
    /// every call - a leve starts, finishes and fails without notice, and a
    /// cached list would send the player after enemies that are already gone.
    /// </summary>
    public unsafe RunningLeve? GetRunningLeve()
    {
        var framework = EventFramework.Instance();
        if (framework == null) return null;

        var quests = QuestManager.Instance();
        if (quests == null) return null;

        // The leves the player holds. A director whose id is not among them is
        // not the player's leve, whatever it claims to be.
        var accepted = new HashSet<ushort>();
        foreach (ref var work in quests->LeveQuests)
            if (work.LeveId != 0) accepted.Add(work.LeveId);

        ref var directors = ref framework->DirectorModule.DirectorList;
        var seen = new List<string>();
        for (var i = 0; i < directors.LongCount; i++)
        {
            var director = directors[i].Value;
            if (director == null) continue;

            var eventId = director->Info.EventId;
            seen.Add($"Content={eventId.ContentId}({(ushort)eventId.ContentId}) Entry={eventId.EntryId} " +
                     $"DirectorContentId={director->ContentId} Titel='{director->Title}'");

            if (!IsLeveDirector(eventId.ContentId)) continue;

            // The leve id lives in Director.ContentId @736, not in the event id.
            var leveId = (ushort)director->ContentId;
            if (!accepted.Contains(leveId))
            {
                _log.Info($"[Leve] Director mit Freibrief-Kennung {leveId} ist keiner der " +
                          $"angenommenen Freibriefe ({string.Join(", ", accepted)}) - ignoriert.");
                continue;
            }

            // The director's OWN object list. If it holds the spawned monsters,
            // nothing has to be inferred from sheets at all - see the caller.
            var eventObjects = new List<nint>();
            foreach (var handle in director->EventObjects)
                if (handle.Value != null) eventObjects.Add((nint)handle.Value);

            var objective = director->Objective.ToString();
            return BuildRunningLeve(leveId, objective, eventObjects, (nint)director, eventId.Id);
        }

        // No leve director found. WHY matters: the whole feature rests on the
        // assumption that a leve director carries EventHandlerContent
        // GuildLeveAssignment, and that assumption cannot be checked without a
        // leve running. Listing every director that IS active turns a failed
        // in-game test into an answer instead of another round of guessing.
        LogDirectorMiss(seen, accepted);
        return null;
    }

    /// <summary>
    /// Row ids of accepted levequests (<c>QuestManager.LeveQuests</c>), including
    /// ones that are currently running, ready for turn-in, or failed.
    /// </summary>
    public unsafe IReadOnlyList<ushort> GetAcceptedLeveIds()
    {
        var ids = new List<ushort>();
        var quests = QuestManager.Instance();
        if (quests == null) return ids;
        foreach (ref var work in quests->LeveQuests)
            if (work.LeveId != 0) ids.Add(work.LeveId);
        return ids;
    }

    /// <summary>
    /// Whether this accepted leve still needs its objective area / enemies —
    /// not ready for turn-in and not failed. Measured via LeveWork.Sequence
    /// (HaselCommon LeveService): 255 = ready for turn-in, 3 = failed.
    /// </summary>
    public unsafe bool IsLeveObjectiveActive(ushort leveId)
    {
        var quests = QuestManager.Instance();
        if (quests == null) return false;
        foreach (ref var work in quests->LeveQuests)
        {
            if (work.LeveId != leveId) continue;
            // Ready for turn-in or failed: no objective walk target.
            if (work.Sequence is 255 or 3) return false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether an accepted leve named <paramref name="leveName"/> still needs
    /// its objective (sheet/map goal). False when ready for turn-in or failed.
    /// </summary>
    public bool IsLeveObjectiveActiveByName(string leveName)
    {
        if (string.IsNullOrWhiteSpace(leveName)) return false;
        var sheet = _data.GetExcelSheet<Leve>();
        foreach (var id in GetAcceptedLeveIds())
        {
            if (!sheet.TryGetRow(id, out var row)) continue;
            var name = row.Name.ExtractText().Trim();
            if (!string.Equals(name, leveName, StringComparison.Ordinal)) continue;
            return IsLeveObjectiveActive(id);
        }
        // Not among accepted leves — map-only label (e.g. board name): keep.
        return true;
    }

    /// <summary>
    /// Whether the player currently holds at least one accepted levequest
    /// (<c>QuestManager.LeveQuests</c>), started or not. Used to keep the
    /// Freibriefe category reachable after a failed run clears map markers
    /// while the leve is still held (log 2026-09-21: Angenommen 661, Markers
    /// leer → Kategorie fehlte).
    /// </summary>
    public unsafe bool HasAcceptedLeve() => GetAcceptedLeveIds().Count > 0;

    /// <summary>
    /// Names of accepted levequests from the Leve sheet, for speech when map
    /// markers are empty (failed/retry state). Empty when none are held.
    /// </summary>
    public unsafe IReadOnlyList<string> GetAcceptedLeveNames()
    {
        var names = new List<string>();
        var sheet = _data.GetExcelSheet<Leve>();
        foreach (var id in GetAcceptedLeveIds())
        {
            if (sheet.TryGetRow(id, out var row))
            {
                var name = row.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                else names.Add(id.ToString());
            }
            else names.Add(id.ToString());
        }
        return names;
    }

    /// <summary>
    /// Giver NPC and objective area for each accepted leve, read from the Leve
    /// sheet (<c>LevelLevemete</c> / <c>LevelStart</c>) when Map markers are
    /// empty. Verified against live markers for leve 661 (2026-09-21): sheet
    /// LevelStart (117.7, -10.8, -467.7) matches <c>Map.LevequestMarkers</c>
    /// exactly. The game clears those markers while a leve runs and after a
    /// fail — without this fallback the Freibriefe category had nothing to
    /// walk to.
    /// <para>
    /// Giver destinations use the <em>NPC</em> name from
    /// <c>LevelLevemete.Object</c> → ENpcResident — never the leve title
    /// (user 2026-09-21: Geber = Abhol-NPC only). Many accepted leves share
    /// one Levemete; that NPC is listed once.
    /// </para>
    /// </summary>
    public List<QuestDestination> GetSheetDestinations(uint currentTerritory)
    {
        var result = new List<QuestDestination>();
        var leveSheet = _data.GetExcelSheet<Leve>();
        var levelSheet = _data.GetExcelSheet<Level>();
        var seenGiverLevels = new HashSet<uint>();

        foreach (var leveId in GetAcceptedLeveIds())
        {
            if (!leveSheet.TryGetRow(leveId, out var leve)) continue;
            var leveName = leve.Name.ExtractText().Trim();
            if (string.IsNullOrWhiteSpace(leveName)) continue;

            var giverLevelId = leve.LevelLevemete.RowId;
            if (giverLevelId != 0 && seenGiverLevels.Add(giverLevelId))
            {
                var npcName = ResolveLevemeteNpcName(giverLevelId, levelSheet);
                if (!string.IsNullOrWhiteSpace(npcName))
                {
                    AddSheetLevelDest(result, giverLevelId, npcName, QuestMarkerRole.LeveGiver,
                        currentTerritory, levelSheet);
                }
            }

            // Objectives only while the task is still open — not after Sequence
            // 255 (turn-in) or 3 (failed). Otherwise finished leves clutter the
            // category (user 2026-09-21).
            if (!IsLeveObjectiveActive(leveId)) continue;

            AddSheetLevelDest(result, leve.LevelStart.RowId, leveName, QuestMarkerRole.LeveObjective,
                currentTerritory, levelSheet);
        }

        return result;
    }

    /// <summary>
    /// Spoken name of the Levemete NPC at a <c>Level</c> row (Type 8 →
    /// ENpcResident). Empty when the row is missing or not an NPC pin.
    /// </summary>
    private string ResolveLevemeteNpcName(uint levelRowId, Lumina.Excel.ExcelSheet<Level> levelSheet)
    {
        if (!levelSheet.TryGetRow(levelRowId, out var level)) return string.Empty;
        // Level.Type 8 = ENpcBase (same as quest markers / ObjectKind map).
        if (level.Type != 8 || level.Object.RowId == 0) return string.Empty;
        if (!_data.GetExcelSheet<ENpcResident>().TryGetRow(level.Object.RowId, out var npc))
            return string.Empty;
        var name = npc.Singular.ExtractText().Trim();
        return name;
    }

    private string _lastSheetDestTrace = string.Empty;

    private void AddSheetLevelDest(
        List<QuestDestination> result,
        uint levelRowId,
        string spokenName,
        QuestMarkerRole role,
        uint currentTerritory,
        Lumina.Excel.ExcelSheet<Level> levelSheet)
    {
        if (levelRowId == 0) return;
        if (!levelSheet.TryGetRow(levelRowId, out var level)) return;

        var terr = (ushort)level.Territory.RowId;
        if (terr == 0) return;

        var targetBase = level.Object.RowId;
        var targetType = level.Type;
        // Area pins (Type 51) have Object 0 — pure position, like map goal markers.
        if (targetBase == 0) targetType = 0;

        result.Add(new QuestDestination(
            spokenName,
            spokenName,
            new Vector3(level.X, level.Y, level.Z),
            level.Radius,
            terr,
            level.Map.RowId,
            terr == currentTerritory,
            QuestKind.Unknown,
            0,
            role,
            targetBase,
            targetType));

        var trace =
            $"[Leve] Sheet-Fallback {role}: '{spokenName}' LevelId={levelRowId} " +
            $"pos=({level.X:F1}|{level.Y:F1}|{level.Z:F1}) r={level.Radius:F1} " +
            $"terr={terr} map={level.Map.RowId} obj={targetBase} type={level.Type}";
        if (trace != _lastSheetDestTrace)
        {
            _lastSheetDestTrace = trace;
            _log.Info(trace);
        }
    }

    // Only one line per changed director set / leve - the caller runs on every
    // browser keypress, and a repeated identical line buries the log.
    private string _lastDirectorTrace = string.Empty;
    private string _lastLeveSummary = string.Empty;

    private void LogDirectorMiss(List<string> directors, HashSet<ushort> accepted)
    {
        var trace = directors.Count > 0 ? string.Join(" | ", directors) : "keine";
        if (trace == _lastDirectorTrace) return;
        _lastDirectorTrace = trace;
        _log.Info($"[Leve] Kein laufender Freibrief erkannt. Angenommen: " +
                  $"{(accepted.Count > 0 ? string.Join(", ", accepted) : "keine")}. " +
                  $"Aktive Directors: {trace}");
    }

    /// <summary>
    /// Whether the GAME itself binds this object to the running leve.
    ///
    /// <c>GameObject.EventId</c> (@244, EventId struct: Id @0 as a whole, EntryId
    /// @0, ContentId @2 - ilspycmd-verified in FFXIVClientStructs) holds the event
    /// that spawned the object, and a leve monster carries the id of ITS leve
    /// director. Measured twice in the live log 2026-08-18:
    ///   "'Streunender Dodo' EventId=2147549737 (Content=BattleLeveDirector/32769,
    ///    Entry=553)" while leve 528 ran, and
    ///   "'Träger-Marienkäfer' EventId=2147549739 (…, Entry=555)" while leve 512 ran.
    /// Every ordinary zone animal standing next to them read EventId=0, including
    /// the wild Dodos of the same species - so this separates leve spawns from
    /// wildlife exactly, which nothing else did.
    ///
    /// The FULL id is compared, not just the director kind: the entry number is
    /// what tells our leve apart from another player's leve running beside us.
    ///
    /// DEAD END, kept so it is not tried a third time: GetEventHandlersImpl
    /// (virtual 30) answers "whose object is this" with the same zone-wide handler
    /// for every monster - measured 25770280390 for the leve's Träger-Marienkäfer
    /// AND for the wild Marienkäfer, never the director address. The director's own
    /// EventObjects set is empty on a running leve.
    /// </summary>
    /// <param name="address">The object, as IGameObject.Address.</param>
    /// <param name="directorEventId">The running leve director's event id.</param>
    /// <param name="objectEventId">The object's own event id, for the log.</param>
    /// <param name="anyLeveSpawn">True when the object was spawned by SOME leve
    /// director - ours or someone else's. Logged when the id does not match, so a
    /// wrong assumption about the entry number shows up as data instead of silence.</param>
    public unsafe bool BelongsToLeve(
        nint address, uint directorEventId, out uint objectEventId, out bool anyLeveSpawn)
    {
        objectEventId = 0;
        anyLeveSpawn = false;
        if (address == 0 || directorEventId == 0) return false;

        var eventId = ((CSGameObject*)address)->EventId;
        objectEventId = eventId.Id;
        anyLeveSpawn = IsLeveDirector(eventId.ContentId);
        return anyLeveSpawn && eventId.Id == directorEventId;
    }

    /// <summary>
    /// The leve's own to-do lines (Director.DirectorTodos @1088), exactly the
    /// text the duty list shows a sighted player. Read as a diagnosis first: it
    /// says whether the leve currently expects enemies at all, which is the one
    /// question the object table cannot answer.
    /// </summary>
    public unsafe List<string> GetTodoLines(nint directorAddress)
    {
        var lines = new List<string>();
        if (directorAddress == 0) return lines;

        var director = (Director*)directorAddress;
        ref var todos = ref director->DirectorTodos;
        for (var i = 0; i < todos.LongCount; i++)
        {
            ref var todo = ref todos[i];
            if (!todo.Enabled) continue;
            lines.Add($"'{todo.Text}' fertig={todo.Complete} Zaehler={todo.CurrentCount}");
        }
        return lines;
    }

    /// <summary>
    /// Every field of an object that could plausibly separate a leve spawn from
    /// ordinary wildlife, in one line for the log. Written because two rounds of
    /// picking a single field have now failed; the next decision is made from a
    /// dump, not from another pick.
    /// </summary>
    public unsafe string DescribeObjectFields(nint address)
    {
        if (address == 0) return "Adresse 0";
        var o = (CSGameObject*)address;
        return $"EventId={o->EventId.Id} (Content={o->EventId.ContentId}/{(ushort)o->EventId.ContentId}, " +
               $"Entry={o->EventId.EntryId}) LayoutId={o->LayoutId} EventState={o->EventState} " +
               $"OwnerId={o->OwnerId:X} FateId={o->FateId} Symbol={o->NamePlateIconId} " +
               $"GimmickId={o->GimmickId}";
    }

    /// <summary>
    /// Whether an event-handler content id names one of the leve directors.
    /// The gathering one is included although it never yields enemies: which
    /// leve kinds have enemies is the SHEET's answer (Leve.DataId), not a
    /// judgement made here, and a leve that turns out to have none simply
    /// produces an empty list.
    /// </summary>
    private static bool IsLeveDirector(EventHandlerContent content)
        => content is EventHandlerContent.BattleLeveDirector
                   or EventHandlerContent.GatheringLeveDirector
                   or EventHandlerContent.CompanyLeveDirector;

    /// <summary>Resolves a running leve id to its name and enemy list.</summary>
    private RunningLeve? BuildRunningLeve(
        ushort leveId, string objective, List<nint> eventObjects, nint directorAddress,
        uint directorEventId)
    {
        if (!_data.GetExcelSheet<Leve>().TryGetRow(leveId, out var leve))
        {
            _log.Info($"[Leve] Freibrief {leveId} laeuft, steht aber nicht im Leve-Sheet.");
            return null;
        }

        var leveName = leve.Name.ExtractText().Trim();
        var enemies = new List<LeveEnemySpec>();

        // Mercenary leves (assignment type 1) and grand company leves (13-15)
        // are the two battle-shaped kinds. Both sheets hold the same pair per
        // slot - which monster, and how many of them - under different names.
        if (leve.DataId.TryGetValue<BattleLeve>(out var battle))
        {
            foreach (var slot in battle.LeveData)
                AddEnemy(enemies, slot.BaseID, slot.BNpcName.RowId, slot.ToDoNumberInvolved);
        }
        else if (leve.DataId.TryGetValue<CompanyLeve>(out var company))
        {
            foreach (var slot in company.CompanyLeveStruct)
                AddEnemy(enemies, slot.BaseID, slot.BNpcName.RowId, 0);
        }

        // Once per change, not once per keypress: a single browser press asks
        // this several times over, and the log is the only channel that can tell
        // a blind user's failed test apart from a wrong assumption.
        var summary = $"[Leve] Laufender Freibrief {leveId} '{leveName}', Ziel '{objective}', " +
                      $"EventId={directorEventId} (Eintrag {(ushort)directorEventId}), " +
                      $"{eventObjects.Count} Director-Objekte, {enemies.Count} Gegnerart(en): " +
                      (enemies.Count > 0
                          ? string.Join(", ", enemies.ConvertAll(e => $"{e.Name} (BaseId={e.BaseId}, NameId={e.NameId}, {e.Required}x)"))
                          : "keine");
        if (summary != _lastLeveSummary)
        {
            _lastLeveSummary = summary;
            _log.Info(summary);
        }

        return new RunningLeve(
            leveId, leveName, objective, enemies, eventObjects, directorAddress, directorEventId);
    }

    /// <summary>
    /// Adds one enemy slot, keyed by its BNpcBase id.
    ///
    /// The slot's BaseID is a multi-target link (EventItem or BNpcBase); only
    /// the BNpcBase case is a monster - a slot pointing at an item is a fetch
    /// objective, not something to walk to.
    ///
    /// A monster can fill SEVERAL slots of the same leve - measured in the sheet
    /// 2026-08-18: leve 530 "Pralle Reben" lists Bienenwolke four times with 2
    /// each, leve 527 lists Wander-Mandragora with 5 and with 0. What the leve
    /// then wants in total is not readable from the slot alone (the slots are
    /// staged over ToDoSequence/NumOfAppearance, which is unmeasured), so the
    /// second slot drops the number to 0 = unspoken. Saying nothing is right;
    /// naming one slot's number as the total would be a claim nothing backs.
    /// </summary>
    private void AddEnemy(List<LeveEnemySpec> enemies, Lumina.Excel.RowRef baseRef, uint nameId, uint required)
    {
        if (!baseRef.Is<BNpcBase>() || baseRef.RowId == 0) return;
        var baseId = baseRef.RowId;

        var known = enemies.FindIndex(e => e.BaseId == baseId);
        if (known >= 0)
        {
            if (enemies[known].Required != 0)
                enemies[known] = enemies[known] with { Required = 0 };
            return;
        }

        // The name is for speech only, so a slot without a usable BNpcName row
        // still counts - the object's own name stands in for it.
        var name = _data.GetExcelSheet<BNpcName>().TryGetRow(nameId, out var nameRow)
            ? MonsterNameText.Resolve(nameRow, _data.Language)
            : string.Empty;

        enemies.Add(new LeveEnemySpec(baseId, nameId, name, required));
    }
}
