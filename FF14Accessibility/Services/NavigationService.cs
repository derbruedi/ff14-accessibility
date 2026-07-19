using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using LuminaENpcResident = Lumina.Excel.Sheets.ENpcResident;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace FF14Accessibility.Services;

public sealed class NavigationService
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly TolkService _tolk;
    private readonly BeaconService _beacon;
    private readonly CueService _cue;
    private readonly QuestMarkerService _questMarkers;
    private readonly PlacesService _places;
    private readonly RouteService _routes;
    private readonly Configuration _config;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    // Aktuell verfolgtes Ziel
    private IGameObject? _trackedObject;
    private string? _trackedName;

    public NavigationService(
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        TolkService tolk,
        BeaconService beacon,
        CueService cue,
        QuestMarkerService questMarkers,
        PlacesService places,
        RouteService routes,
        Configuration config,
        IDataManager data,
        IPluginLog log)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _tolk = tolk;
        _beacon = beacon;
        _cue = cue;
        _questMarkers = questMarkers;
        _places = places;
        _routes = routes;
        _config = config;
        _data = data;
        _log = log;
    }

    private ulong _lastSeenTargetId;   // hard/soft target id from the previous frame
    private ulong _ownSelectionId;     // CycleObject announced this id itself already

    /// <summary>
    /// Announces the game target whenever it changes: name, kind, distance,
    /// direction. This makes the game's own targeting keys (Tab, F1-F12, F)
    /// usable without sight. Called every frame from Plugin.OnFrameworkUpdate.
    /// Also drives the walk guide (beacon every frame, speech every 2 s).
    /// </summary>
    public void Update(bool announceTargetChanges)
    {
        // Route preview runs independently of the walk guide (async pathfind).
        PollPreviewTask();

        // LocalPlayer.TargetObject does NOT track UI targeting (verified in-game
        // 2026-07-10: Tab-target set, property stayed null) - ITargetManager does.
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _lastSeenTargetId = 0;
            if (_walkGuideActive)
            {
                // Player gone (logout/zone change) - the tracked object is
                // invalid now, so end the guide instead of beeping stale data.
                StopWalkGuide();
                _tolk.SpeakInterrupt("Gehhilfe beendet.");
                _log.Info("[Nav] Gehhilfe: beendet, Spieler nicht mehr verfügbar.");
            }
            return;
        }

        PollMapFlag(player);

        // Announce only when the ACTUAL target changes. V4.24 compared against
        // the id CycleObject WANTED to set - when the game rejected the set
        // (SetHardTarget returns bool, log 2026-07-10 16:39), every N press
        // re-announced the old stuck target.
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        var targetId = target?.GameObjectId ?? 0;
        if (targetId != _lastSeenTargetId)
        {
            _lastSeenTargetId = targetId;
            var isOwnSelection = targetId != 0 && targetId == _ownSelectionId;
            if (isOwnSelection) _ownSelectionId = 0;

            // No mod tone on targeting an enemy: the GAME already plays one
            // (user report 2026-07-18) - a second blip on top of it is noise,
            // not information. The spoken announcement below stays.
            if (target != null && announceTargetChanges && !isOwnSelection)
            {
                var name = target.Name.TextValue;
                if (string.IsNullOrWhiteSpace(name)) name = "Unbenannt";
                var distance = Vector3.Distance(player.Position, target.Position);
                var text = $"Ziel: {NpcPrefix(target)}{name}, {DescribeKind(target.ObjectKind)}, " +
                           $"{FormatDistance(distance)}, {CalculateDirection(player, target.Position)}" +
                           $"{DescribeTargetHp(target)}.";
                _log.Info($"[Nav] Zielwechsel: {text} (id={target.GameObjectId:X}, kind={target.ObjectKind})");
                _tolk.SpeakInterrupt(text);
            }
        }

        if (_walkGuideActive) WalkGuideFrame(player);
    }

    // ── Objekt-Browser: mit einer Taste durch Objekte in der Nähe blättern ──

    private static readonly ObjectKind[] AllBrowseKinds =
    {
        ObjectKind.EventNpc, ObjectKind.BattleNpc, ObjectKind.Pc,
        ObjectKind.EventObj, ObjectKind.Treasure,
        ObjectKind.GatheringPoint, ObjectKind.Aetheryte,
    };

    // Kinds == null marks the marker categories (quest objectives and map
    // waypoints): they browse positions, not ObjectTable game objects.
    private static readonly (string Label, ObjectKind[]? Kinds)[] Categories =
    {
        ("Alles",              AllBrowseKinds),
        ("NPCs",               new[] { ObjectKind.EventNpc }),
        ("Gegner",             new[] { ObjectKind.BattleNpc }),
        ("Spieler",            new[] { ObjectKind.Pc }),
        ("Objekte",            new[] { ObjectKind.EventObj, ObjectKind.Treasure }),
        ("Sammelpunkte",       new[] { ObjectKind.GatheringPoint }),
        // Ätheryten kommen aus den Kartendaten (PlacesService), nicht aus der
        // ObjectTable: die Marker kennen ALLE Ätheryten + Aethernet-Splitter
        // der Zone, die Objektsuche nur die in ~100 m (User-Wunsch 2026-07-13).
        ("Ätheryten",          null),
        ("Quest-Ziele",        null),
        ("Annehmbare Quests",  null),
        ("Wegpunkte",          null),
    };

    private bool IsQuestCategory           => Categories[_categoryIndex].Label == "Quest-Ziele";
    private bool IsUnacceptedQuestCategory => Categories[_categoryIndex].Label == "Annehmbare Quests";
    private bool IsPlacesCategory          => Categories[_categoryIndex].Label == "Wegpunkte";
    private bool IsAetheryteCategory       => Categories[_categoryIndex].Label == "Ätheryten";

    /// <summary>
    /// The quest objective selected via the browser, or null when the browser
    /// is not on the quest category. Plugin.cs routes Numpad 3 here: quest
    /// markers have no game object to target, the auto-walk gets a position.
    /// </summary>
    public QuestDestination? SelectedQuestDestination { get; private set; }

    /// <summary>
    /// The map waypoint selected via the browser (Wegpunkte category), or
    /// null. Position is 2D (Y=0, map data has no height) - Plugin.cs
    /// resolves the walkable height via navmesh before the auto-walk.
    /// </summary>
    public PlaceDestination? SelectedPlaceDestination { get; private set; }

    /// <summary>Search radius for the object browser, in yalms/meters.</summary>
    private const float CycleRange = 100f;

    private int _categoryIndex;
    private int _cycleIndex = -1;

    /// <summary>Switches to the next object category and announces its object count.</summary>
    public void NextCategory() => CycleCategory(+1);

    /// <summary>Switches to the previous object category and announces its object count.</summary>
    public void PreviousCategory() => CycleCategory(-1);

    /// <summary>Steps the category index by <paramref name="direction"/> (wrapping)
    /// and announces the new category with its object count.</summary>
    private void CycleCategory(int direction)
    {
        var n = Categories.Length;
        _categoryIndex = ((_categoryIndex + direction) % n + n) % n;

        // Skip categories that make no sense right now (gathering nodes while
        // playing a fighting class). Bounded by n so a fully unavailable set
        // can never spin forever.
        for (var guard = 0; guard < n && !IsCategoryAvailable(_categoryIndex); guard++)
            _categoryIndex = ((_categoryIndex + (direction >= 0 ? 1 : -1)) % n + n) % n;

        _cycleIndex = -1;
        SelectedQuestDestination = null;
        SelectedPlaceDestination = null;

        if (IsQuestCategory || IsUnacceptedQuestCategory)
        {
            var label = Categories[_categoryIndex].Label;
            var dests = GetQuestDestinations(IsUnacceptedQuestCategory);
            var here = dests.Count(d => d.InCurrentZone);
            var away = dests.Count - here;
            _tolk.SpeakInterrupt(away > 0
                ? $"Kategorie {label}: {here} im Gebiet, {away} in anderen Gebieten."
                : $"Kategorie {label}: {here} im Gebiet.");
            return;
        }

        if (IsPlacesCategory)
        {
            var places = _places.GetPlaces();
            var exits = places.Count(p => p.IsZoneTransition);
            _tolk.SpeakInterrupt(exits > 0
                ? $"Kategorie Wegpunkte: {places.Count} im Gebiet, davon {exits} Übergänge."
                : $"Kategorie Wegpunkte: {places.Count} im Gebiet.");
            return;
        }

        if (IsAetheryteCategory)
        {
            var aetherytes = _places.GetPlaces().Count(IsAetherytePlace);
            _tolk.SpeakInterrupt($"Kategorie Ätheryten: {aetherytes} im Gebiet.");
            return;
        }

        var count = GetCategoryObjects().Count;
        _tolk.SpeakInterrupt($"Kategorie {Categories[_categoryIndex].Label}: {count} in der Nähe.");
    }

    /// <summary>
    /// Selects the next/previous object of the current category (sorted by
    /// distance), sets it as the real game target and announces it.
    /// </summary>
    public void CycleObject(int direction)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        if (IsQuestCategory || IsUnacceptedQuestCategory)
        {
            CycleQuestDestination(direction, player, IsUnacceptedQuestCategory);
            return;
        }

        if (IsPlacesCategory || IsAetheryteCategory)
        {
            CyclePlaceDestination(direction, player, aetherytesOnly: IsAetheryteCategory);
            return;
        }

        var objects = GetCategoryObjects();
        if (objects.Count == 0)
        {
            _tolk.SpeakInterrupt($"Keine {Categories[_categoryIndex].Label} in {CycleRange:F0} Metern.");
            return;
        }

        var count = objects.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var obj = objects[_cycleIndex];

        // Suppress the target-change announcer: we announce with position info here.
        _ownSelectionId = obj.GameObjectId;
        _targetManager.Target = obj;

        // Audit probe: the game may REFUSE the change (SetHardTarget returns
        // bool, Dalamud discards it; rejections seen in log 2026-07-10 16:39
        // - cause unknown). Without this check F would silently turn the
        // player towards the OLD target.
        var actualId = _targetManager.Target?.GameObjectId ?? 0;
        var rejected = actualId != obj.GameObjectId;
        if (rejected)
            _log.Info($"[Nav] Target-Set ABGELEHNT: wollte {obj.GameObjectId:X} " +
                      $"({obj.Name.TextValue}), ist weiterhin {actualId:X}");

        var distance = Vector3.Distance(player.Position, obj.Position);

        // Gathering nodes: the type and required level ARE the useful content
        // ("Erzader, Stufe 20"); their object name is usually empty and the
        // kind ("Sammelpunkt") would just repeat the category.
        var description = obj.ObjectKind == ObjectKind.GatheringPoint
            ? DescribeGatheringPoint(obj)
            : $"{NpcPrefix(obj)}{obj.Name.TextValue}, {DescribeKind(obj.ObjectKind)}";

        // Position goes LAST: the name is what the user is listening for, the
        // counter only tells them how far they have cycled (user wish
        // 2026-07-19). The rejection warning stays at the very end.
        var text = $"{description}, " +
                   $"{FormatDistance(distance)}, " +
                   $"{CalculateDirection(player, obj.Position)}, " +
                   $"{_cycleIndex + 1} von {count}." +
                   (rejected ? " Achtung, nicht anvisiert." : "");
        _log.Info($"[Nav] Auswahl: {text} (id={obj.GameObjectId:X})");
        _tolk.SpeakInterrupt(text);
    }

    // ── Quest-Ziele: durch Marker der angenommenen Quests blättern ──

    private void CycleQuestDestination(int direction, IGameObject player, bool unaccepted)
    {
        var dests = GetQuestDestinations(unaccepted);
        if (dests.Count == 0)
        {
            SelectedQuestDestination = null;
            _tolk.SpeakInterrupt(unaccepted
                ? "Keine annehmbaren Quests in der Nähe."
                : "Keine Quest-Ziele. Erst eine Quest annehmen.");
            return;
        }

        var count = dests.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var dest = dests[_cycleIndex];
        SelectedQuestDestination = dest;

        // Marker tooltip often carries the objective ("Mit X sprechen") -
        // append it when it adds information beyond the quest name.
        var detail = !string.IsNullOrWhiteSpace(dest.Detail) && dest.Detail != dest.QuestName
            ? $" {dest.Detail}."
            : string.Empty;

        // Main Scenario quests are flagged so a blind player can tell the story
        // apart from side quests (a sighted player sees a distinct marker).
        var story = dest.IsMainStory ? "Story: " : string.Empty;

        // The list is level-ordered, so the level has to be audible - otherwise
        // the order is a silent rule the player cannot act on. Omitted when the
        // game gave us no level rather than announcing a made-up "Stufe 0".
        var level = dest.Level > 0 ? $"Stufe {dest.Level}, " : string.Empty;

        // Current objective ("what is still missing", e.g. "Aurelias erlegen 0/3")
        // from the on-screen quest tracker. Only tracked quests have one; the
        // marker tooltip stays as a fallback for the rest.
        var objectives = _questMarkers.GetQuestObjectives();
        var todo = objectives.TryGetValue(dest.QuestName, out var obj) && !string.IsNullOrWhiteSpace(obj)
            ? $", {obj}"
            : string.Empty;

        string text;
        if (dest.InCurrentZone)
        {
            text = $"{level}{story}{dest.QuestName}{todo}, " +
                   $"{FormatDistance(Vector3.Distance(player.Position, dest.Position))}, " +
                   $"{CalculateDirection(player, dest.Position)}.{detail}";
        }
        else
        {
            // Blind players cannot read the world map: name the target zone
            // and the transition that leads there (BFS over the map graph).
            var zone = _places.GetMapName(dest.MapId);
            var hop  = _places.FindFirstHopToMap(dest.MapId, out var hops);
            text = $"{level}{story}{dest.QuestName}{todo}, " +
                   (string.IsNullOrEmpty(zone) ? "in einem anderen Gebiet." : $"im Gebiet {zone}.");
            if (hop != null)
            {
                text += $" Dorthin über {hop.Name}, " +
                        $"{FormatDistance(Distance2D(player.Position, hop.Position))}, " +
                        $"{CalculateDirection(player, hop.Position)}" +
                        (hops > 1 ? $", danach noch {hops - 1} weitere Übergänge." : ".");
                text += " Nummernblock 3 läuft zum Übergang.";
            }
            text += detail;
        }
        // Counter last, after the route hints - see CycleObject.
        text += $" {_cycleIndex + 1} von {count}.";
        _log.Info($"[Quest] Auswahl: {text}");
        _tolk.SpeakInterrupt(text);
    }

    // ── Karten-Markierung: neue Flagge ansagen ──

    // Last flag position seen, so only a NEW or MOVED flag speaks. Null means
    // "no flag in this map" - re-entering the map re-arms the announcement.
    private Vector3? _lastFlagPosition;

    /// <summary>
    /// Announces a newly placed map flag ("Neue Markierung, 120 Meter,
    /// Nordosten"). In a party the flag is the moment everyone is expected to
    /// react, and a blind player cannot see it appear on the map. Compass
    /// bearing on purpose: the flag is a destination to plan around, not a
    /// steering instruction (see route-guidance guide, section 4).
    /// </summary>
    private void PollMapFlag(IGameObject player)
    {
        var flag = _places.GetFlagMarker();
        if (flag == null)
        {
            _lastFlagPosition = null;
            return;
        }

        // A flag re-placed on nearly the same spot is not news. The threshold
        // also absorbs the millimetre rounding SetFlagMapMarker applies.
        if (_lastFlagPosition != null
            && Distance2D(_lastFlagPosition.Value, flag.Position) < 1f) return;

        _lastFlagPosition = flag.Position;

        var distance = Distance2D(player.Position, flag.Position);
        var compass  = RouteService.CompassWord(player.Position, flag.Position);
        _log.Info($"[Nav] Neue Karten-Markierung: pos={flag.Position.X:F1}/{flag.Position.Z:F1} " +
                  $"dist={distance:F1} {compass}");

        if (!_config.AnnounceMapFlag) return;
        _tolk.SpeakInterrupt($"Neue Markierung, {FormatDistance(distance)}, {compass}.");
    }

    // ── Wegpunkte: durch die Karten-Symbole des Gebiets blättern ──

    /// <summary>Horizontal distance (map data has no height).</summary>
    private static float Distance2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private void CyclePlaceDestination(int direction, IGameObject player, bool aetherytesOnly = false)
    {
        var places = _places.GetPlaces()
            .Where(p => !aetherytesOnly || IsAetherytePlace(p))
            .OrderBy(p => Distance2D(player.Position, p.Position))
            .ToList();
        if (places.Count == 0)
        {
            SelectedPlaceDestination = null;
            _tolk.SpeakInterrupt(aetherytesOnly
                ? "Keine Ätheryten in diesem Gebiet gefunden."
                : "Keine Wegpunkte in diesem Gebiet gefunden.");
            return;
        }

        var count = places.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var place = places[_cycleIndex];
        SelectedPlaceDestination = place;

        // Direction uses X/Z only - the placeholder Y does not affect it.
        var text = $"{place.Name}, {place.TypeLabel}, " +
                   $"{FormatDistance(Distance2D(player.Position, place.Position))}, " +
                   $"{CalculateDirection(player, place.Position)}, " +
                   $"{_cycleIndex + 1} von {count}.";
        _log.Info($"[Orte] Auswahl: {text} pos=({place.Position.X:F1}|{place.Position.Z:F1})");
        _tolk.SpeakInterrupt(text);
    }

    /// <summary>
    /// Quest objectives, nearest first. In-zone markers come first, sorted by
    /// straight-line distance. Cross-zone markers follow, sorted by the walking
    /// distance to the transition that leads there (that is what a blind player
    /// actually walks to, so "nearest" stays meaningful across zones).
    /// <paramref name="unaccepted"/> switches to acceptable-quest markers.
    /// </summary>
    private List<QuestDestination> GetQuestDestinations(bool unaccepted)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return new List<QuestDestination>();

        var source = unaccepted
            ? _questMarkers.GetUnacceptedDestinations()
            : _questMarkers.GetDestinations();

        // Level is the primary order within a zone (user request 2026-07-18):
        // it answers "which of these can I actually do next?", which distance
        // never did. Reachability still wins over it - a level-appropriate quest
        // three zones away is not the next thing to walk to. Unknown levels (0)
        // sort last so they never masquerade as level 1.
        var ordered = source
            .OrderByDescending(d => d.InCurrentZone)
            .ThenBy(d => d.Level == 0 ? int.MaxValue : d.Level)
            .ThenBy(d => EffectiveWalkDistance(player.Position, d))
            .ThenBy(d => d.QuestName, StringComparer.Ordinal);

        // Cross-zone markers of the SAME quest all funnel to the same transition,
        // so they announce an identical long routing sentence ("1 von 3 ... 2 von
        // 3", all the same; log 2026-07-12). Collapse them to one entry per
        // (quest, target map) - the nearest survives thanks to the sort. In-zone
        // markers stay separate: each is a distinct, individually reachable spot.
        var result = new List<QuestDestination>();
        var seenAway = new HashSet<(string, uint)>();
        foreach (var d in ordered)
        {
            if (!d.InCurrentZone && !seenAway.Add((d.QuestName, d.MapId))) continue;
            result.Add(d);
        }
        return result;
    }

    /// <summary>
    /// Distance used to rank a quest marker: the straight-line distance for
    /// in-zone markers, or the distance to the first transition on the route
    /// for cross-zone markers (float.MaxValue when no route is found, so
    /// unreachable ones sort last).
    /// </summary>
    private float EffectiveWalkDistance(Vector3 playerPos, QuestDestination dest)
    {
        if (dest.InCurrentZone)
            return Vector3.Distance(playerPos, dest.Position);

        var hop = _places.FindFirstHopToMap(dest.MapId, out _);
        return hop != null ? Distance2D(playerPos, hop.Position) : float.MaxValue;
    }

    private static bool IsAetherytePlace(PlaceDestination p) =>
        p.TypeLabel is "Ätheryt" or "Aethernet";

    /// <summary>
    /// Whether a category is worth offering right now. Only the gathering
    /// category is conditional: as a fighting class it is dead weight in the
    /// rotation (user request 2026-07-19 - "soll sie nur sichtbar sein wenn die
    /// klasse auf minenarbeiter steht").
    ///
    /// It stays available while a gathering class is active EVEN IF nothing is
    /// in range - otherwise a miner could not check "is there anything here?",
    /// and an empty answer is a real answer. Nodes in range also keep it
    /// available regardless of class, so the filter can never hide something
    /// that actually exists.
    /// </summary>
    private bool IsCategoryAvailable(int index)
    {
        var kinds = Categories[index].Kinds;
        if (kinds == null || !kinds.Contains(ObjectKind.GatheringPoint)) return true;
        return IsGatheringClass() || GetObjectsOfKinds(kinds).Count > 0;
    }

    // Last class the check logged, so the probe writes one line per change.
    private uint _lastLoggedClassJob = uint.MaxValue;

    /// <summary>
    /// True while the player is on a gathering class (miner, botanist, fisher).
    ///
    /// ASSUMPTION, marked as such: ClassJob.DohDolJobIndex (sbyte @106,
    /// ilspycmd-verified to EXIST) is >= 0 for the Hand/Land classes and
    /// negative otherwise. The actual VALUES live in game data that cannot be
    /// read offline, so the class name, its abbreviation and both index fields
    /// are logged on every class change - the first in-game test settles it.
    /// If the assumption is wrong, nothing breaks silently: the category also
    /// stays available whenever nodes are in range.
    /// </summary>
    private bool IsGatheringClass()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return false;

        var job = player.ClassJob.ValueNullable;
        if (job == null) return false;

        var isGatherer = job.Value.DohDolJobIndex >= 0 && job.Value.BattleClassIndex < 0;

        if (player.ClassJob.RowId != _lastLoggedClassJob)
        {
            _lastLoggedClassJob = player.ClassJob.RowId;
            _log.Info($"[Gather] Klasse: '{job.Value.Name.ExtractText()}' " +
                      $"({job.Value.Abbreviation.ExtractText()}, RowId={player.ClassJob.RowId}) " +
                      $"DohDolJobIndex={job.Value.DohDolJobIndex} " +
                      $"BattleClassIndex={job.Value.BattleClassIndex} " +
                      $"-> Sammler={isGatherer}");
        }

        return isGatherer;
    }

    /// <summary>Objects of the given kinds within browse range, distance-sorted.</summary>
    private List<IGameObject> GetObjectsOfKinds(ObjectKind[] kinds)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return new List<IGameObject>();

        var isGathering = kinds.Contains(ObjectKind.GatheringPoint);
        return _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && (isGathering || !string.IsNullOrWhiteSpace(o.Name.TextValue))
                        && kinds.Contains(o.ObjectKind)
                        && Vector3.Distance(player.Position, o.Position) <= CycleRange)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .ToList();
    }

    // ── Sammelpunkte (Minenarbeiter / Gärtner) ──────────────────────
    //
    // Data path, all ilspycmd-verified (2026-07-19):
    //   GameObject.DataId -> Sheet "GatheringPoint"
    //   .GatheringPointBase -> "GatheringPointBase"
    //   .GatheringType -> "GatheringType".Name  (localised: Erzader, Steinbruch,
    //                                            Fällpunkt, Erntepunkt, ...)
    //   .GatheringLevel (byte @36)              (required gathering level)
    // The type name is READ, never derived from an id we made up: GatheringType
    // has no class column, so any "0 = miner" table would be our invention.
    private readonly Dictionary<uint, (string Type, int Level)> _gatheringCache = [];

    /// <summary>
    /// Type and required level of a gathering node, or null when the id is not
    /// in the sheet. Cached per DataId - the sheet lookup must not run per frame.
    /// </summary>
    private (string Type, int Level)? GetGatheringInfo(uint dataId)
    {
        if (_gatheringCache.TryGetValue(dataId, out var hit))
            return hit.Type.Length == 0 ? null : hit;

        var sheet = _data.GetExcelSheet<Lumina.Excel.Sheets.GatheringPoint>();
        if (sheet == null || !sheet.TryGetRow(dataId, out var row))
        {
            _gatheringCache[dataId] = (string.Empty, 0);
            return null;
        }

        var baseRow = row.GatheringPointBase.ValueNullable;
        if (baseRow == null)
        {
            _gatheringCache[dataId] = (string.Empty, 0);
            return null;
        }

        var typeName = baseRow.Value.GatheringType.ValueNullable?.Name.ExtractText() ?? string.Empty;
        var level    = baseRow.Value.GatheringLevel;
        var info     = (Type: typeName, Level: (int)level);
        _gatheringCache[dataId] = info;

        _log.Info($"[Gather] DataId={dataId}: Typ='{typeName}' Stufe={level}");
        return typeName.Length == 0 ? null : info;
    }

    /// <summary>
    /// Describes a gathering node for the announcement: "Erzader, Stufe 20".
    /// Falls back to the object's own name, then to a plain "Sammelpunkt" -
    /// never to an invented type.
    /// </summary>
    private string DescribeGatheringPoint(IGameObject obj)
    {
        // BaseId, not DataId: Dalamud renamed it, the old name is deprecated.
        var info = GetGatheringInfo(obj.BaseId);
        var name = obj.Name.TextValue;

        if (info == null)
            return string.IsNullOrWhiteSpace(name) ? "Sammelpunkt" : name;

        return info.Value.Level > 0
            ? $"{info.Value.Type}, Stufe {info.Value.Level}"
            : info.Value.Type;
    }

    private List<IGameObject> GetCategoryObjects()
    {
        var kinds = Categories[_categoryIndex].Kinds;
        return kinds == null ? new List<IGameObject>() : GetObjectsOfKinds(kinds);
    }

    // ── Gehhilfe: manuell laufen, geführt von Beacon + Ansagen ──
    // Seit V4.63 pfadbasiert: Beacon und Richtungsansagen verfolgen den
    // NÄCHSTEN Wegpunkt der vnavmesh-Route (Nav.Pathfind, reine Abfrage ohne
    // Auto-Bewegung), nicht mehr die Luftlinie zum Endziel - um eine Ecke
    // zeigt der Ton auf die Ecke statt in die Wand. Ohne vnavmesh/Route läuft
    // die alte Luftlinien-Führung weiter. Design: docs-de/ideen/
    // ff14-route-guidance-guide.md + docs/manuelle-navigation-konzept.md.

    private bool _walkGuideActive;
    private ulong _walkTargetId;              // 0 = fixed-position destination (marker)
    private string _walkTargetName = string.Empty;
    private Vector3 _walkDestPosition;        // fixed position, or refreshed from the object
    private float _walkArrivalRange = ArrivalDistance;
    private DateTime _lastGuideTick = DateTime.MinValue;

    // Route state. A null route = straight-line guidance (the pre-V4.63 mode).
    private List<Vector3>? _route;
    private int _routeCursor;
    private Vector3 _routeDest;               // destination the active route was computed for
    private Task<List<Vector3>>? _routeTask;  // pending Nav.Pathfind (polled, never awaited)
    private bool _routeTaskIsReroute;
    private DateTime _routeRequestedAt;
    private bool _computeAnnounced;
    private DateTime _lastRerouteAt = DateTime.MinValue;

    /// <summary>Arrival distance for the walk guide, in yalms/meters.</summary>
    private const float ArrivalDistance = 3f;

    /// <summary>A route waypoint counts as reached within this radius: exact
    /// arrival is impossible on foot and funnel corners sit tight against
    /// walls - too small strands the cursor at a corner already turned.</summary>
    private const float WaypointReachRadius = 3f;

    /// <summary>How many waypoints beyond the cursor the skip-ahead checks.</summary>
    private const int SkipAheadLookahead = 3;

    /// <summary>Re-pathfind when the player is this far off the current route
    /// segment (exploring, dodging, knockbacks) or the destination moved.</summary>
    private const float DriftRerouteDistance = 10f;

    private const double RerouteMinIntervalS = 3;

    // Spoken cadence: route mode speaks on EVENTS (waypoint reached) with a
    // slow reassurance repeat between them; the straight-line fallback has no
    // events and keeps the old 2 s rhythm.
    private const double RouteSpeakIntervalS = 5;
    private const double StraightSpeakIntervalS = 2;

    /// <summary>Whether the walk guide is currently running (Plugin.cs decides
    /// between "switch off" and "start towards a marker destination").</summary>
    public bool IsWalkGuideActive => _walkGuideActive;

    /// <summary>
    /// Toggles the walk guide for the current game target: audio beacon
    /// (pitch + pan encode the direction, updated every frame) plus spoken
    /// guidance until arrival. The player walks manually (F turns towards
    /// the guide tone, W/R runs) - no movement is injected.
    /// </summary>
    public void ToggleWalkGuide()
    {
        if (_walkGuideActive)
        {
            StopWalkGuide();
            _tolk.SpeakInterrupt("Gehhilfe aus.");
            _log.Info("[Nav] Gehhilfe: manuell ausgeschaltet.");
            return;
        }

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel. Erst mit N ein Objekt wählen.");
            return;
        }

        StartWalkGuide(target.GameObjectId, target.Name.TextValue, target.Position, ArrivalDistance);
    }

    /// <summary>
    /// Starts the walk guide towards a fixed world position - quest markers
    /// and map waypoints have no game object to target (mirrors the auto-walk;
    /// user request 2026-07-15: reach marker destinations manually too). The
    /// position must already sit on the walkable mesh (Plugin.cs resolves the
    /// height). Callers turn a running guide off before starting a new one.
    /// </summary>
    public void StartWalkGuideToPosition(Vector3 position, string name, float arrivalRange)
        => StartWalkGuide(0, name, position, MathF.Max(ArrivalDistance, arrivalRange));

    private void StartWalkGuide(ulong targetId, string name, Vector3 destination, float arrivalRange)
    {
        _walkGuideActive = true;
        _walkTargetId = targetId;
        _walkTargetName = name;
        _walkDestPosition = destination;
        _walkArrivalRange = arrivalRange;
        // Not MinValue: an immediate first repeat would interrupt the
        // "Gehhilfe an" line and the route preview (V4.42 lesson: competing
        // interrupt-speakers cut each other off).
        _lastGuideTick = DateTime.UtcNow;
        ClearRoute();
        _beacon.Start();
        _tolk.SpeakInterrupt($"Gehhilfe an: {_walkTargetName}.");
        _log.Info($"[Nav] Gehhilfe: gestartet zu {name} (id={targetId:X}, ankunft={arrivalRange:F1})");

        var player = _objectTable.LocalPlayer;
        if (_config.WalkGuideRouteMode && player != null)
            RequestRoute(player.Position, isReroute: false);
        else if (!_config.WalkGuideRouteMode)
            _log.Info("[Nav] Gehhilfe: Routen-Modus per Config aus, Luftlinie.");
    }

    private void StopWalkGuide()
    {
        _walkGuideActive = false;
        _beacon.Stop();
        ClearRoute();
    }

    private void ClearRoute()
    {
        _route = null;
        _routeCursor = 0;
        _routeTask = null;
        _computeAnnounced = false;
    }

    /// <summary>Stops the walk guide without announcement (the auto-walk takes over the beacon).</summary>
    public void StopWalkGuideQuiet()
    {
        if (!_walkGuideActive) return;
        StopWalkGuide();
        _log.Info("[Nav] Gehhilfe: durch Auto-Lauf abgelöst.");
    }

    /// <summary>
    /// Queues a pathfind from <paramref name="from"/> to the current guide
    /// destination. Falls back to straight-line guidance (with one spoken
    /// notice on the initial request) when vnavmesh is unavailable.
    /// </summary>
    private void RequestRoute(Vector3 from, bool isReroute)
    {
        if (_routeTask != null) return; // one pending query at a time

        var task = _routes.RequestPath(from, _walkDestPosition);
        if (task == null)
        {
            if (!isReroute)
            {
                _tolk.Speak("Kein Wegenetz, führe in Luftlinie.");
                _log.Info("[Nav] Gehhilfe: Nav.Pathfind nicht verfügbar, Luftlinien-Modus.");
            }
            return;
        }
        _routeTask = task;
        _routeTaskIsReroute = isReroute;
        _routeRequestedAt = DateTime.UtcNow;
        _lastRerouteAt = DateTime.UtcNow;
    }

    /// <summary>Adopts a finished pathfind: initial routes speak the compass
    /// preview, re-routes stay quiet unless the immediate direction changed.</summary>
    private void PollRouteTask(IGameObject player)
    {
        var task = _routeTask;
        if (task == null) return;
        if (!task.IsCompleted)
        {
            // Explain a noticeably long computation once (fresh zones can
            // still be building mesh tiles).
            if (!_computeAnnounced && (DateTime.UtcNow - _routeRequestedAt).TotalSeconds > 1)
            {
                _computeAnnounced = true;
                _tolk.Speak("Weg wird berechnet.");
            }
            return;
        }
        _routeTask = null;
        _computeAnnounced = false;

        List<Vector3>? waypoints = null;
        if (task.IsCompletedSuccessfully)
            waypoints = task.Result;
        else
            _log.Warning("[Nav] Gehhilfe: Pathfind-Task nicht erfolgreich: " +
                         (task.Exception?.GetBaseException().Message ?? "abgebrochen"));

        if (waypoints == null || waypoints.Count == 0)
        {
            // No route (separate mesh islands, jump-only gaps): keep the old
            // route if this was a re-route, otherwise guide straight-line.
            if (_route == null && !_routeTaskIsReroute)
            {
                _tolk.Speak($"Kein Weg gefunden, führe in Luftlinie.{_places.BuildNoPathHint(_walkDestPosition)}");
                _log.Info("[Nav] Gehhilfe: kein Weg gefunden, Luftlinien-Modus.");
            }
            return;
        }

        var previousDirection = _route != null && _routeCursor < _route.Count
            ? DirectionText(RelativeAngle(player, _route[_routeCursor]))
            : null;

        _route = waypoints;
        _routeCursor = 0;
        _routeDest = _walkDestPosition;
        // The first waypoint is the start position - skip everything already in reach.
        AdvancePastReachedWaypoints(player, announce: false);

        if (!_routeTaskIsReroute)
        {
            _tolk.Speak(_routes.DescribeRoute(_walkTargetName, waypoints));
        }
        else if (_route != null && _routeCursor < _route.Count)
        {
            // Guide rule: after a quiet re-route speak one line ONLY when the
            // immediate direction actually changed.
            var newDirection = DirectionText(RelativeAngle(player, _route[_routeCursor]));
            if (newDirection != previousDirection)
                _tolk.SpeakInterrupt($"Neuer Weg: {newDirection}.");
            _log.Info($"[Nav] Gehhilfe: Route neu berechnet ({waypoints.Count} Wegpunkte).");
        }
    }

    /// <summary>Runs every frame while the walk guide is active.</summary>
    private void WalkGuideFrame(IGameObject player)
    {
        // Destination refresh: objects move (NPCs); marker positions are fixed.
        if (_walkTargetId != 0)
        {
            var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _walkTargetId);
            if (obj == null)
            {
                StopWalkGuide();
                _tolk.SpeakInterrupt("Gehhilfe: Ziel verloren.");
                _log.Info($"[Nav] Gehhilfe: Ziel {_walkTargetId:X} nicht mehr in der ObjectTable.");
                return;
            }
            _walkDestPosition = obj.Position;
        }

        var distance = Vector3.Distance(player.Position, _walkDestPosition);
        if (distance <= _walkArrivalRange)
        {
            StopWalkGuide();
            _cue.PlayArrivalTone();
            _tolk.SpeakInterrupt($"Ziel erreicht: {_walkTargetName}.");
            _log.Info($"[Nav] Gehhilfe: Ziel erreicht, dist={distance:F1}");
            return;
        }

        PollRouteTask(player);

        if (_route != null)
        {
            AdvancePastReachedWaypoints(player, announce: true);
            CheckReroute(player);
        }

        var guidePoint = _route != null && _routeCursor < _route.Count
            ? _route[_routeCursor]
            : _walkDestPosition;
        var guideDist = Vector3.Distance(player.Position, guidePoint);
        var relAngle = RelativeAngle(player, guidePoint);
        // Steering (pitch/pan) follows the waypoint; loudness follows the
        // remaining distance to the DESTINATION (see BeaconService.Update).
        _beacon.Update(relAngle, distance);

        // Reassurance repeat between waypoint events; the beacon carries the
        // direction continuously in the frames between.
        var interval = _route != null ? RouteSpeakIntervalS : StraightSpeakIntervalS;
        if ((DateTime.UtcNow - _lastGuideTick).TotalSeconds < interval) return;
        _lastGuideTick = DateTime.UtcNow;

        _tolk.SpeakInterrupt($"{FormatDistance(guideDist)}, {DirectionText(relAngle)}{VerticalHint(player, guidePoint)}.");
        _log.Info($"[Nav] Gehhilfe: dist={guideDist:F1} zielDist={distance:F1} relAngle={relAngle:F0} " +
                  $"wp={_routeCursor}/{_route?.Count ?? 0} rot={player.Rotation:F2}");
    }

    /// <summary>
    /// Moves the route cursor. A waypoint within <see cref="WaypointReachRadius"/>
    /// counts as reached (cue + fresh spoken leg). Skip-ahead advances SILENTLY
    /// when the player is already within reach of a later waypoint or clearly
    /// closer to the next one than the current corner is - a player who cuts a
    /// corner must not be told to walk backwards. After the last waypoint the
    /// guide homes in on the destination directly.
    /// </summary>
    private void AdvancePastReachedWaypoints(IGameObject player, bool announce)
    {
        var route = _route;
        if (route == null) return;

        var advanced = false;
        var genuineReach = false;

        while (_routeCursor < route.Count)
        {
            if (Vector3.Distance(player.Position, route[_routeCursor]) <= WaypointReachRadius)
            {
                _routeCursor++;
                advanced = true;
                genuineReach = true;
                continue;
            }

            // Skip-ahead (a): within reach of a LATER waypoint - jump past it.
            var skipTo = -1;
            var lookEnd = Math.Min(route.Count, _routeCursor + 1 + SkipAheadLookahead);
            for (var i = _routeCursor + 1; i < lookEnd; i++)
            {
                if (Vector3.Distance(player.Position, route[i]) <= WaypointReachRadius)
                    skipTo = i + 1;
            }
            if (skipTo > 0)
            {
                _routeCursor = skipTo;
                advanced = true;
                continue;
            }

            // Skip-ahead (b): measurably closer to the NEXT waypoint than the
            // current one is - the corner was cut, drop the corner point.
            if (_routeCursor + 1 < route.Count
                && Vector3.Distance(player.Position, route[_routeCursor + 1]) + 1f
                   < Vector3.Distance(route[_routeCursor], route[_routeCursor + 1]))
            {
                _routeCursor++;
                advanced = true;
                continue;
            }

            break;
        }

        if (_routeCursor >= route.Count)
        {
            // All waypoints consumed: home in on the destination directly.
            // The arrival cue/announcement stays with the target-arrival check.
            _route = null;
            _log.Info("[Nav] Gehhilfe: letzter Wegpunkt passiert, Zielanflug.");
            return;
        }

        if (advanced && announce)
        {
            if (genuineReach) _cue.PlayWaypointTone();
            // Fresh leg, spoken once, relative to the current heading - this
            // event line replaces the old fixed 2 s repetition.
            var next = route[_routeCursor];
            var relAngle = RelativeAngle(player, next);
            var dist = Vector3.Distance(player.Position, next);
            _tolk.SpeakInterrupt($"{FormatDistance(dist)}, {DirectionText(relAngle)}{VerticalHint(player, next)}.");
            _lastGuideTick = DateTime.UtcNow;
            _log.Info($"[Nav] Gehhilfe: Wegpunkt {(genuineReach ? "erreicht" : "übersprungen")}, " +
                      $"weiter zu {_routeCursor + 1}/{route.Count}, dist={dist:F1}");
        }
    }

    /// <summary>
    /// Re-pathfinds quietly when the player drifted off the current route
    /// segment (exploring, dodging mobs, knockbacks) or the destination itself
    /// moved (wandering NPCs). The guide follows the player, it does not scold.
    /// </summary>
    private void CheckReroute(IGameObject player)
    {
        var route = _route;
        if (route == null || _routeTask != null) return;
        if ((DateTime.UtcNow - _lastRerouteAt).TotalSeconds < RerouteMinIntervalS) return;

        var destMoved = Vector3.Distance(_walkDestPosition, _routeDest) > DriftRerouteDistance;
        var segStart = route[_routeCursor > 0 ? _routeCursor - 1 : 0];
        var drift = DistanceToSegment2D(player.Position, segStart, route[_routeCursor]);
        if (!destMoved && drift <= DriftRerouteDistance) return;

        _log.Info($"[Nav] Gehhilfe: Re-Routing (drift={drift:F1}, zielBewegt={destMoved}).");
        RequestRoute(player.Position, isReroute: true);
    }

    /// <summary>", aufwärts"/", abwärts" when the guide point sits clearly above
    /// or below the player (stairs, ramps) - plain Y arithmetic on route data.</summary>
    private static string VerticalHint(IGameObject player, Vector3 point)
    {
        var dy = point.Y - player.Position.Y;
        return dy > 1.5f ? ", aufwärts" : dy < -1.5f ? ", abwärts" : string.Empty;
    }

    /// <summary>2D point-to-segment distance on XZ (Y is noisy across slopes).</summary>
    private static float DistanceToSegment2D(Vector3 p, Vector3 a, Vector3 b)
    {
        float px = p.X - a.X, pz = p.Z - a.Z;
        float bx = b.X - a.X, bz = b.Z - a.Z;
        var lenSq = bx * bx + bz * bz;
        var t = lenSq > 0f ? Math.Clamp((px * bx + pz * bz) / lenSq, 0f, 1f) : 0f;
        float dx = px - t * bx, dz = pz - t * bz;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    // ── Routen-Vorschau: Weg ansagen, ohne zu laufen (Strg+Numpad3) ──

    private Task<List<Vector3>>? _previewTask;
    private string _previewName = string.Empty;
    private Vector3 _previewDest;

    /// <summary>
    /// Speaks the turn-by-turn route to a destination without walking:
    /// pathfind, announce, discard. Lets the player build a mental map before
    /// choosing between auto-walk and the manual walk guide.
    /// </summary>
    public void PreviewRoute(Vector3 position, string name)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var task = _routes.RequestPath(player.Position, position);
        if (task == null)
        {
            _tolk.SpeakInterrupt("Kein Wegenetz. Das Plugin vnavmesh fehlt oder lädt noch.");
            return;
        }
        _previewTask = task;
        _previewName = name;
        _previewDest = position;
        _tolk.SpeakInterrupt($"Berechne Weg zu {name}.");
    }

    /// <summary>Route preview to the current game target.</summary>
    public void PreviewRouteToTarget()
    {
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel. Erst mit N ein Objekt wählen.");
            return;
        }
        PreviewRoute(target.Position, target.Name.TextValue);
    }

    /// <summary>Polled every frame from Update (the pathfind runs async).</summary>
    private void PollPreviewTask()
    {
        var task = _previewTask;
        if (task == null || !task.IsCompleted) return;
        _previewTask = null;

        List<Vector3>? waypoints = null;
        if (task.IsCompletedSuccessfully)
            waypoints = task.Result;
        else
            _log.Warning("[Route] Vorschau-Pathfind nicht erfolgreich: " +
                         (task.Exception?.GetBaseException().Message ?? "abgebrochen"));

        if (waypoints == null || waypoints.Count == 0)
        {
            _tolk.SpeakInterrupt($"Kein Weg zu {_previewName} gefunden.{_places.BuildNoPathHint(_previewDest)}");
            return;
        }
        _tolk.SpeakInterrupt(_routes.DescribeRoute(_previewName, waypoints));
    }

    /// <summary>", HP X Prozent" for targets that have hit points, else empty.</summary>
    private static string DescribeTargetHp(IGameObject target)
    {
        if (target is IBattleChara bc && bc.MaxHp > 0)
            return $", HP {(int)(bc.CurrentHp * 100u / bc.MaxHp)} Prozent";
        return string.Empty;
    }

    /// <summary>
    /// Leading description for an NPC, spoken BEFORE the name (user request): its
    /// role/title from the ENpcResident sheet ("Marktverwalter", "Wächter" ...)
    /// and whether it currently offers a quest (the "!" nameplate marker a sighted
    /// player sees, from NamePlateIconId). Returns a trailing ", " so callers can
    /// place it in front of the name; "" for non-NPCs / nothing to add.
    /// </summary>
    private unsafe string NpcPrefix(IGameObject obj)
    {
        if (obj.ObjectKind != ObjectKind.EventNpc && obj.ObjectKind != ObjectKind.BattleNpc)
            return string.Empty;

        var parts = new List<string>();

        if (_data.GetExcelSheet<LuminaENpcResident>().TryGetRow(obj.BaseId, out var npc))
        {
            var title = npc.Title.ExtractText();
            if (!string.IsNullOrWhiteSpace(title)) parts.Add(title);
        }

        if (obj.Address != 0)
        {
            var iconId = ((CSGameObject*)obj.Address)->NamePlateIconId;
            var quest = DescribeQuestMarker(iconId);
            if (!string.IsNullOrEmpty(quest))
            {
                parts.Add(quest);
                _log.Info($"[Nav] NPC {obj.Name.TextValue}: NamePlateIconId={iconId} -> '{quest}'");
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) + ", " : string.Empty;
    }

    /// <summary>
    /// Maps a nameplate icon id to a quest hint. Ranges are the standard FFXIV
    /// quest markers; the exact id is logged so the mapping can be refined from
    /// real data (available "!" vs. active vs. ready-to-turn-in).
    /// </summary>
    private static string DescribeQuestMarker(uint iconId) => iconId switch
    {
        0 => string.Empty,
        >= 71001 and <= 71006 => "Quest verfügbar",
        >= 71021 and <= 71046 => "Quest aktiv",
        >= 71000 and <= 71999 => "Quest",
        _ => string.Empty,
    };

    private static string DescribeKind(ObjectKind kind) => kind switch
    {
        ObjectKind.Pc             => "Spieler",
        ObjectKind.BattleNpc      => "Kampf-NPC",
        ObjectKind.EventNpc       => "NPC",
        ObjectKind.Treasure       => "Schatz",
        ObjectKind.Aetheryte      => "Ätheryt",
        ObjectKind.GatheringPoint => "Sammelpunkt",
        ObjectKind.EventObj       => "Objekt",
        ObjectKind.Companion      => "Begleiter",
        ObjectKind.Retainer       => "Gehilfe",
        ObjectKind.Mount          => "Reittier",
        _                         => kind.ToString()
    };

    // Ziel per Name setzen (NPC oder Spielername)
    public bool SetTarget(string name)
    {
        var obj = _objectTable
            .FirstOrDefault(o => o.Name.TextValue.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (obj == null)
        {
            _tolk.SpeakInterrupt($"Ziel {name} nicht gefunden.");
            return false;
        }

        _trackedObject = obj;
        _trackedName = obj.Name.TextValue;
        _tolk.SpeakInterrupt($"Verfolge {_trackedName}.");
        return true;
    }

    // Aktuell anvisiertes Spielziel übernehmen
    public void SetTargetFromGameTarget()
    {
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel anvisiert.");
            return;
        }

        _trackedObject = target;
        _trackedName = target.Name.TextValue;
        _tolk.SpeakInterrupt($"Verfolge {_trackedName}.");
    }

    public void ClearTarget()
    {
        _trackedObject = null;
        _trackedName = null;
        _tolk.SpeakInterrupt("Zielverfolgung beendet.");
    }

    // Auf Tastendruck: Richtung und Distanz ansagen
    public void AnnounceDirection()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        // Ziel aktualisieren falls es sich bewegt hat
        if (_trackedObject != null)
        {
            _trackedObject = _objectTable.FirstOrDefault(o => o.GameObjectId == _trackedObject.GameObjectId)
                             ?? _trackedObject;
        }

        if (_trackedObject == null)
        {
            _tolk.SpeakInterrupt("Kein Ziel gesetzt. Erst mit N ein Objekt wählen.");
            return;
        }

        var playerPos = player.Position;
        var targetPos = _trackedObject.Position;
        var distance = Vector3.Distance(playerPos, targetPos);

        var direction = CalculateDirection(player, targetPos);
        var distanceText = FormatDistance(distance);

        _tolk.SpeakInterrupt($"{_trackedName}: {distanceText}, {direction}.");
    }

    // Alle nahen NPCs/Spieler auflisten
    public void AnnounceNearbyObjects(float maxDistance = 30f)
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var nearby = _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && !string.IsNullOrWhiteSpace(o.Name.TextValue)
                        && Vector3.Distance(player.Position, o.Position) <= maxDistance)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .Take(5)
            .ToList();

        if (nearby.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Objekte in der Nähe.");
            return;
        }

        var parts = nearby.Select(o =>
        {
            var dist = Vector3.Distance(player.Position, o.Position);
            return $"{o.Name.TextValue} {FormatDistance(dist)}";
        });

        _tolk.SpeakInterrupt("In der Nähe: " + string.Join(", ", parts));
    }

    private string CalculateDirection(IGameObject player, Vector3 targetPos) =>
        DirectionText(RelativeAngle(player, targetPos));

    // Relativer Winkel Spieler-Blickrichtung -> Ziel: 0° = geradeaus,
    // positiv = rechts. Rotations-Konvention VERIFIZIERT aus Live-Log
    // 2026-07-10 15:27 (F-Snap auf Ziel, rot=-1.83 bei Ziel-Peilung
    // atan2(dx,dz)=-105°): Blickvektor = (sin(rot), cos(rot)) in XZ, also
    // rot direkt vergleichbar mit atan2(dx, dz). Vorzeichen (positiv =
    // rechts) vom User per Beacon-Hörtest bestätigt (2026-07-10 Abend).
    // Details: docs/game-api.md.
    internal static double RelativeAngle(IGameObject player, Vector3 targetPos)
    {
        var playerPos = player.Position;
        var dx = targetPos.X - playerPos.X;
        var dz = targetPos.Z - playerPos.Z;

        var relativeAngle = (Math.Atan2(dx, dz) - player.Rotation) * (180.0 / Math.PI);
        if (relativeAngle > 180) relativeAngle -= 360;
        if (relativeAngle < -180) relativeAngle += 360;
        return relativeAngle;
    }

    private static string DirectionText(double relativeAngle) => relativeAngle switch
    {
        < -135 => "hinter links",
        < -45  => "links",
        < -15  => "leicht links",
        <= 15  => "geradeaus",
        <= 45  => "leicht rechts",
        <= 135 => "rechts",
        _      => "hinter rechts"
    };

    private static string FormatDistance(float distance) =>
        distance < 2f   ? "direkt neben dir" :
        distance < 10f  ? $"{distance:F0} Meter" :
        distance < 100f ? $"{distance:F0} Meter" :
                          $"{distance / 1000:F1} Kilometer";
}
