using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaQuest = Lumina.Excel.Sheets.Quest;

namespace FF14Accessibility.Services;

/// <summary>One quest objective location, read from the game's map markers.</summary>
/// <param name="QuestName">Quest name from the marker label.</param>
/// <param name="Detail">Marker tooltip (may repeat the quest name).</param>
/// <param name="Position">Objective position in world coordinates.</param>
/// <param name="Radius">Objective area radius (0 for point targets).</param>
/// <param name="TerritoryTypeId">Zone the marker belongs to.</param>
/// <param name="MapId">Map the marker belongs to (for cross-zone routing).</param>
/// <param name="InCurrentZone">Whether the marker is in the player's current zone.</param>
/// <param name="IsMainStory">Whether the quest belongs to the Main Scenario.</param>
public sealed record QuestDestination(
    string QuestName,
    string Detail,
    Vector3 Position,
    float Radius,
    ushort TerritoryTypeId,
    uint MapId,
    bool InCurrentZone,
    bool IsMainStory);

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

    private HashSet<string>? _mainStoryNames;

    /// <summary>
    /// Names of all Main Scenario quests, built once from the Quest sheet.
    /// A quest is MSQ when its JournalGenre -> JournalCategory -> JournalSection
    /// is section 0 ("Hauptszenario"). Matched against the marker label to flag
    /// story quests; the count is logged for verification.
    /// </summary>
    private HashSet<string> MainStoryNames()
    {
        if (_mainStoryNames != null) return _mainStoryNames;

        var set = new HashSet<string>();
        foreach (var quest in _data.GetExcelSheet<LuminaQuest>())
        {
            var name = quest.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var genre = quest.JournalGenre.ValueNullable;
            var category = genre?.JournalCategory.ValueNullable;
            if (category is { JournalSection.RowId: 0 })
                set.Add(name);
        }

        _mainStoryNames = set;
        _log.Info($"[Quest] Hauptszenario-Namen geladen: {set.Count}");
        return set;
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
            var text = ((AtkTextNode*)node)->NodeText.ToString();
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
    private unsafe void AddMarkerDestinations(
        List<QuestDestination> result, MarkerInfo marker, uint currentTerritory, string tag)
    {
        var questName = marker.Label.ToString();
        if (string.IsNullOrWhiteSpace(questName)) return; // empty slot

        var isMainStory = MainStoryNames().Contains(questName);

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
                      $"map={data.MapId} icon={data.IconId} render={marker.ShouldRender}");
            result.Add(new QuestDestination(questName, tooltip, data.Position,
                data.Radius, data.TerritoryTypeId, data.MapId, inZone, isMainStory));
        }
    }
}
