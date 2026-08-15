using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Tells apart several objects that share one name, and remembers which of them
/// the player has already been to.
///
/// THE PROBLEM (user report 2026-08-08, dungeons): a dungeon holds four things
/// called "Truhe". The browser announced all four identically, and its "3 von 8"
/// is no help either - GetObjectsOfKinds re-sorts the list by distance on every
/// key press, so the same chest is "3 von 8" now and "2 von 8" two steps later.
/// Nothing in the announcement stayed with the object, so the player could not
/// tell which one they had already dealt with.
///
/// TWO SEPARATE ANSWERS, deliberately kept apart because they need different
/// things to be true:
///
///  - THE NUMBER identifies one object among its same-named siblings. It has to
///    survive the object unloading while the player is at the far end of the
///    dungeon, so it is stored per name group and re-found by position.
///  - VISITED is about a PLACE, not an object. It needs no name and no group:
///    the player either stood there or did not.
///
/// Merging the two (the first attempt) meant a position lookup that ranged over
/// every name group, which would hand a reloaded chest the entry of the door
/// beside it. Split, each lookup stays inside what it can actually distinguish.
///
/// IDENTITY IS ASKED TWICE, and the order matters:
///  1. <c>GameObjectId</c> while the object stays loaded. That is what the rest
///     of the plugin already relies on to re-find an object between frames
///     (AutoWalkService._targetId, the follow mode, marker resolution).
///  2. POSITION when the id is unknown. The game unloads objects the player
///     walks away from, and whether it hands out the same id on reload is NOT
///     documented anywhere we can check - so it is not assumed. A dungeon chest
///     does not move, so an entry of the same name within
///     <see cref="SamePlaceRange"/> is the same thing coming back.
///
/// HONEST LIMIT of rule 2: an object that both MOVES and gets unloaded (a
/// patrolling NPC leaving and re-entering range) can come back as a new number.
/// Standing scenery - chests, doors, gathering nodes, aetherytes - is what this
/// is for, and there it holds. Moving things are excluded outright (see
/// <see cref="IsLandmark"/>): a respawning enemy is not a place.
/// </summary>
public sealed class ObjectMemoryService
{
    private readonly IObjectTable _objects;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;

    /// <summary>
    /// How close the player has to get before a place counts as visited.
    /// Chosen, not read from the game: the walk guide calls 3 m "arrived"
    /// (NavigationService.ArrivalDistance), and a little room on top of that
    /// catches walking PAST a chest rather than exactly onto it. Deliberately
    /// not stretched further - at 10 m a corridor would mark side rooms as
    /// visited that were only walked past.
    /// </summary>
    private const float VisitRange = 5f;

    /// <summary>
    /// How far apart two readings may be and still mean the same standing
    /// object. Same tolerance the navmesh code uses to match a point back to the
    /// mesh. Two distinct props are never half a metre apart, so this cannot
    /// merge them, but it absorbs float drift across an unload and reload.
    /// </summary>
    private const float SamePlaceRange = 1f;

    // Zone the memory belongs to. Everything is dropped on a zone change: a
    // dungeon run is one visit, and on re-entering the chests are filled again -
    // carrying "schon besucht" over would be a plain lie the second time round.
    private uint _territory;

    // Spoken name -> the objects known under it, in the order they were first
    // announced. The list index+1 IS the spoken number, which is why entries are
    // never removed or re-sorted while the zone lasts.
    private readonly Dictionary<string, List<Landmark>> _byName = new();

    // Fast path for rule 1, across all groups. Kept in step with Landmark.Id.
    private readonly Dictionary<ulong, Landmark> _byId = new();

    // Places the player has stood at. Ids for what is loaded right now,
    // positions so the mark survives the object unloading.
    private readonly HashSet<ulong> _visitedIds = new();
    private readonly List<Vector3> _visitedPlaces = new();

    public ObjectMemoryService(IObjectTable objects, IClientState clientState, IPluginLog log)
    {
        _objects = objects;
        _clientState = clientState;
        _log = log;
    }

    private sealed class Landmark
    {
        public ulong Id;
        public Vector3 Position;
        public int Number;      // 1-based, unique per name and zone
    }

    /// <summary>
    /// Kinds that get a memory. Restricted to things that STAY PUT and that the
    /// player navigates to as a place - the two properties the position fallback
    /// and the visited mark both depend on.
    ///
    /// Left out on purpose: BattleNpc and Pc. Enemies respawn and other players
    /// wander off, so a number would name a different creature minutes later,
    /// and "schon besucht" says nothing useful about either.
    /// </summary>
    /// HousingEventObject joined the browser in 2026-08-15 and belongs here for
    /// exactly the reason this class exists: the wards hold several identically
    /// named pieces (two "Mogry-Briefkasten" and two "Mogul Mog-Briefkasten"
    /// within 60 m in the measured dump), and furniture stays put.
    private static bool IsLandmark(ObjectKind kind) =>
        kind is ObjectKind.EventObj or ObjectKind.Treasure or ObjectKind.GatheringPoint
             or ObjectKind.Aetheryte or ObjectKind.EventNpc or ObjectKind.HousingEventObject;

    /// <summary>
    /// Called every frame from the plugin's framework update. Records every
    /// landmark within <see cref="VisitRange"/> as a place the player has been.
    ///
    /// Only the immediate surroundings are looked at, never the whole table:
    /// this runs per frame, and anything further off has not been visited by
    /// definition. No name is resolved here - a visit is about the spot, so the
    /// loop costs a distance check and a set lookup per nearby object.
    /// </summary>
    public void Update()
    {
        var territory = _clientState.TerritoryType;
        if (territory != _territory)
        {
            if (_byId.Count > 0 || _visitedPlaces.Count > 0)
                _log.Info($"[Ortsgedaechtnis] Zonenwechsel {_territory} -> {territory}: " +
                          $"{_byId.Count} benannte Objekte und {_visitedPlaces.Count} besuchte Orte verworfen.");
            _territory = territory;
            _byName.Clear();
            _byId.Clear();
            _visitedIds.Clear();
            _visitedPlaces.Clear();
        }

        var player = _objects.LocalPlayer;
        if (player == null) return;

        foreach (var obj in _objects)
        {
            if (!IsLandmark(obj.ObjectKind)) continue;
            // Distance FIRST. Adding the id before this check would have marked
            // every landmark in the zone as visited on the first frame, because
            // HashSet.Add tells you the id was new, not that the player was near.
            if (Vector3.Distance(player.Position, obj.Position) > VisitRange) continue;
            if (!_visitedIds.Add(obj.GameObjectId)) continue;   // already recorded this instance

            // Id was new but the SPOT may be known - the same chest reloaded
            // under a fresh id. Recording the position twice would be harmless,
            // but the log line would claim a first visit that is not one.
            if (IsKnownPlace(obj.Position)) continue;

            _visitedPlaces.Add(obj.Position);
            _log.Info($"[Ortsgedaechtnis] Besucht: '{obj.Name.TextValue}' " +
                      $"(id={obj.GameObjectId:X}, art={obj.ObjectKind}, pos={obj.Position}).");
        }
    }

    /// <summary>
    /// The number to speak after an object's name (" 2"), or an empty string
    /// while it is the only thing known by that name.
    ///
    /// Nothing is numbered until a second object of the same name turns up, so a
    /// lone chest stays "Truhe" instead of becoming "Truhe 1". Once the second
    /// one appears both are numbered, and the first keeps the 1 it already had -
    /// the number never moves under the player.
    /// </summary>
    /// <param name="spokenName">
    /// The name as it will be SPOKEN, not the raw one, because that is what the
    /// player needs disambiguated. Objects the game leaves nameless are
    /// announced as "Objekt ohne Namen" and need numbering most of all -
    /// grouping them by their empty raw name would be one bucket per zone, and
    /// grouping a gathering node by its (also empty) raw name would mix ore and
    /// timber into one count.
    /// </param>
    public string NumberSuffix(IGameObject obj, string spokenName)
    {
        if (!IsLandmark(obj.ObjectKind) || string.IsNullOrEmpty(spokenName)) return string.Empty;

        var group = Group(spokenName);
        var entry = Remember(obj, spokenName, group);
        return group.Count > 1 ? $" {entry.Number}" : string.Empty;
    }

    /// <summary>
    /// ", schon besucht" for a place the player has already stood next to, empty
    /// otherwise. Read-only: hearing about an object is not being there.
    /// </summary>
    public string VisitedSuffix(IGameObject obj)
    {
        if (!IsLandmark(obj.ObjectKind)) return string.Empty;

        var visited = _visitedIds.Contains(obj.GameObjectId) || IsKnownPlace(obj.Position);
        return visited ? AccessibilityStrings.AlreadyVisited : string.Empty;
    }

    private bool IsKnownPlace(Vector3 position)
    {
        foreach (var place in _visitedPlaces)
            if (Vector3.Distance(place, position) <= SamePlaceRange) return true;
        return false;
    }

    private List<Landmark> Group(string spokenName)
    {
        if (_byName.TryGetValue(spokenName, out var group)) return group;
        group = new List<Landmark>();
        _byName[spokenName] = group;
        return group;
    }

    /// <summary>Existing entry for an object, or a fresh one with the next number.</summary>
    private Landmark Remember(IGameObject obj, string spokenName, List<Landmark> group)
    {
        var existing = Find(obj, group);
        if (existing != null)
        {
            // The id changes across an unload; the position match found the
            // entry, so re-key it and keep the position current.
            if (existing.Id != obj.GameObjectId)
            {
                _byId.Remove(existing.Id);
                existing.Id = obj.GameObjectId;
                _byId[obj.GameObjectId] = existing;
            }
            existing.Position = obj.Position;
            return existing;
        }

        var entry = new Landmark
        {
            Id = obj.GameObjectId,
            Position = obj.Position,
            Number = group.Count + 1,
        };
        group.Add(entry);
        _byId[obj.GameObjectId] = entry;

        // The moment a name stops being unique is exactly when the announcement
        // starts numbering, so it is worth one line to see it happen.
        if (group.Count == 2)
            _log.Info($"[Ortsgedaechtnis] '{spokenName}' kommt mehrfach vor - ab jetzt nummeriert.");

        return entry;
    }

    /// <summary>
    /// Entry for an object: by id anywhere, else by position WITHIN ITS OWN NAME
    /// GROUP. The group restriction is what keeps the position fallback honest -
    /// searching all groups would match a reloaded chest against the door next
    /// to it, since a metre of tolerance says nothing about what a thing is.
    /// </summary>
    private Landmark? Find(IGameObject obj, List<Landmark> group)
    {
        if (_byId.TryGetValue(obj.GameObjectId, out var byId)) return byId;

        foreach (var entry in group)
            if (Vector3.Distance(entry.Position, obj.Position) <= SamePlaceRange) return entry;

        return null;
    }
}
