using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

            // Enemy tone: a short blip whenever a hostile becomes the target
            // (Tab, F11 or N), even when the spoken announcement is suppressed
            // for an own selection. Muted during auto-walk via announceTargetChanges.
            if (target != null && announceTargetChanges && target.ObjectKind == ObjectKind.BattleNpc)
                _cue.PlayTargetTone();

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
        ("Ätheryten",          new[] { ObjectKind.Aetheryte }),
        ("Quest-Ziele",        null),
        ("Annehmbare Quests",  null),
        ("Wegpunkte",          null),
    };

    private bool IsQuestCategory           => Categories[_categoryIndex].Label == "Quest-Ziele";
    private bool IsUnacceptedQuestCategory => Categories[_categoryIndex].Label == "Annehmbare Quests";
    private bool IsPlacesCategory          => Categories[_categoryIndex].Label == "Wegpunkte";

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
    public void NextCategory()
    {
        _categoryIndex = (_categoryIndex + 1) % Categories.Length;
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

        if (IsPlacesCategory)
        {
            CyclePlaceDestination(direction, player);
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
        var text = $"{_cycleIndex + 1} von {count}: {NpcPrefix(obj)}{obj.Name.TextValue}, " +
                   $"{DescribeKind(obj.ObjectKind)}, {FormatDistance(distance)}, " +
                   $"{CalculateDirection(player, obj.Position)}." +
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

        string text;
        if (dest.InCurrentZone)
        {
            text = $"{_cycleIndex + 1} von {count}: {story}{dest.QuestName}, " +
                   $"{FormatDistance(Vector3.Distance(player.Position, dest.Position))}, " +
                   $"{CalculateDirection(player, dest.Position)}.{detail}";
        }
        else
        {
            // Blind players cannot read the world map: name the target zone
            // and the transition that leads there (BFS over the map graph).
            var zone = _places.GetMapName(dest.MapId);
            var hop  = _places.FindFirstHopToMap(dest.MapId, out var hops);
            text = $"{_cycleIndex + 1} von {count}: {story}{dest.QuestName}, " +
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
        _log.Info($"[Quest] Auswahl: {text}");
        _tolk.SpeakInterrupt(text);
    }

    // ── Wegpunkte: durch die Karten-Symbole des Gebiets blättern ──

    /// <summary>Horizontal distance (map data has no height).</summary>
    private static float Distance2D(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private void CyclePlaceDestination(int direction, IGameObject player)
    {
        var places = _places.GetPlaces()
            .OrderBy(p => Distance2D(player.Position, p.Position))
            .ToList();
        if (places.Count == 0)
        {
            SelectedPlaceDestination = null;
            _tolk.SpeakInterrupt("Keine Wegpunkte in diesem Gebiet gefunden.");
            return;
        }

        var count = places.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var place = places[_cycleIndex];
        SelectedPlaceDestination = place;

        // Direction uses X/Z only - the placeholder Y does not affect it.
        var text = $"{_cycleIndex + 1} von {count}: {place.Name}, {place.TypeLabel}, " +
                   $"{FormatDistance(Distance2D(player.Position, place.Position))}, " +
                   $"{CalculateDirection(player, place.Position)}.";
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

        var ordered = source
            .OrderByDescending(d => d.InCurrentZone)
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

    private List<IGameObject> GetCategoryObjects()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return new List<IGameObject>();

        var kinds = Categories[_categoryIndex].Kinds;
        if (kinds == null) return new List<IGameObject>();

        return _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && !string.IsNullOrWhiteSpace(o.Name.TextValue)
                        && kinds.Contains(o.ObjectKind)
                        && Vector3.Distance(player.Position, o.Position) <= CycleRange)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .ToList();
    }

    // ── Gehhilfe: beim Laufen regelmäßig Richtung + Distanz zum Ziel ansagen ──

    private bool _walkGuideActive;
    private ulong _walkTargetId;
    private string _walkTargetName = string.Empty;
    private DateTime _lastGuideTick = DateTime.MinValue;

    /// <summary>Arrival distance for the walk guide, in yalms/meters.</summary>
    private const float ArrivalDistance = 3f;

    /// <summary>
    /// Toggles the walk guide for the current game target: audio beacon
    /// (pitch + pan encode the direction, updated every frame) plus spoken
    /// direction and distance every 2 seconds until arrival. The player
    /// walks manually (F turns to the target, W/R runs) - no movement is
    /// injected.
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

        _walkGuideActive = true;
        _walkTargetId = target.GameObjectId;
        _walkTargetName = target.Name.TextValue;
        _lastGuideTick = DateTime.MinValue;
        _beacon.Start();
        _tolk.SpeakInterrupt($"Gehhilfe an: {_walkTargetName}. F dreht dich hin, W läuft.");
    }

    private void StopWalkGuide()
    {
        _walkGuideActive = false;
        _beacon.Stop();
    }

    /// <summary>Stops the walk guide without announcement (the auto-walk takes over the beacon).</summary>
    public void StopWalkGuideQuiet()
    {
        if (!_walkGuideActive) return;
        StopWalkGuide();
        _log.Info("[Nav] Gehhilfe: durch Auto-Lauf abgelöst.");
    }

    /// <summary>Runs every frame while the walk guide is active.</summary>
    private void WalkGuideFrame(IGameObject player)
    {
        var obj = _objectTable.FirstOrDefault(o => o.GameObjectId == _walkTargetId);
        if (obj == null)
        {
            StopWalkGuide();
            _tolk.SpeakInterrupt("Gehhilfe: Ziel verloren.");
            _log.Info($"[Nav] Gehhilfe: Ziel {_walkTargetId:X} nicht mehr in der ObjectTable.");
            return;
        }

        var distance = Vector3.Distance(player.Position, obj.Position);
        if (distance <= ArrivalDistance)
        {
            StopWalkGuide();
            _tolk.SpeakInterrupt($"Ziel erreicht: {_walkTargetName}.");
            _log.Info($"[Nav] Gehhilfe: Ziel erreicht, dist={distance:F1}");
            return;
        }

        var relAngle = RelativeAngle(player, obj.Position);
        _beacon.Update(relAngle, distance);

        // Speech only every 2 seconds - the beacon covers the frames between.
        if ((DateTime.UtcNow - _lastGuideTick).TotalSeconds < 2) return;
        _lastGuideTick = DateTime.UtcNow;

        _tolk.SpeakInterrupt($"{FormatDistance(distance)}, {DirectionText(relAngle)}.");
        // Audit data: zero point is verified (F-snap => relAngle ~0); the
        // left/right SIGN is not. Turning right (D) must move relAngle
        // negative if "positive = right" is correct (target drifts left).
        _log.Info($"[Nav] Gehhilfe: dist={distance:F1} relAngle={relAngle:F0} rot={player.Rotation:F2}");
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
