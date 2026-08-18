using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using LuminaENpcResident = Lumina.Excel.Sheets.ENpcResident;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
// Aliased, not imported wholesale: that namespace carries its own ObjectKind,
// which would collide with the Dalamud one used throughout this file.
using Treasure = FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;

namespace FF14Accessibility.Services;

/// <summary>
/// Language-independent identity of an object-browser category. Used for all
/// category logic (which browse mode, which object kinds); the spoken label is
/// looked up separately via <see cref="AccessibilityStrings.CategoryLabel"/> so
/// switching announcement language never changes behaviour.
/// </summary>
internal enum NavCategory
{
    All,
    Npcs,
    Merchants,
    Enemies,
    // Verbuendete: alles, was auf der Seite des Spielers kaempft - Trust-Trupp,
    // Duty-Support-NPCs, Gruppe/Allianz, Karfunkel, Fee, Begleitchocobo. Siehe
    // CombatSide.
    Allies,
    Players,
    Objects,
    // Inhalte: Tueren, die in einen Dungeon, eine Pruefung, einen Raid oder eine
    // PvP-Instanz fuehren. Eigene Kategorie und nicht nur ein Wort innerhalb von
    // Objekte, weil eine Tuer ein ZIEL ist - nur diese aufzaehlen zu koennen ist
    // der Punkt. Siehe DungeonSide.
    Duties,
    QuestNpcs,
    QuestObjects,
    QuestEnemies,
    GatheringNodes,
    Fates,
    // Jagdziele: die noch offenen Monster des aktuellen Jagdtagebuch-Rangs.
    // Kommt weder aus der Objekttabelle noch aus der Zone - Quelle sind
    // Jagdtagebuch-Fortschritt und Kartenmarker, siehe HuntingLogService.
    HuntingTargets,
    FishingSpots,
    Aetherytes,
    QuestGoals,
    AcceptableQuests,
    Levequests,
    Waypoints,
    // Nur INNERHALB eines Tiefen Gewoelbes angeboten, wo sie den Weltsatz
    // vollstaendig ersetzen - siehe DeepDungeonCategories. Fallen bekommen keine
    // eigene Kategorie: das Spiel fuehrt eine aufgedeckte Falle als BattleNpc, sie
    // steht also bereits unter Gegner.
    DeepTreasure,
    DeepCairns,
    DeepRooms,
}

/// <summary>A world object picked in the object browser, so walking to it does
/// not depend on the game accepting it as a target.</summary>
/// <param name="ObjectId">GameObjectId, to refresh the position before walking.</param>
/// <param name="Name">Spoken name, for the "walking to X" announcement.</param>
/// <param name="Position">World position at selection time (fallback once the
/// object has left the object table, e.g. after a zone reload).</param>
public sealed record ObjectDestination(ulong ObjectId, string Name, Vector3 Position);

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
    private readonly FishingService _fishing;
    private readonly FateService _fates;
    private readonly RouteService _routes;
    private readonly ShopNpcService _shops;
    private readonly HuntingLogService _huntingLog;
    private readonly ObjectNameService _objectNames;
    private readonly ObjectMemoryService _memory;
    private readonly Configuration _config;
    private readonly IDataManager _data;
    // Only read for the movement mode, which decides whether turning the
    // character or turning the camera is what actually steers manual walking.
    private readonly IGameConfig _gameConfig;
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
        FishingService fishing,
        FateService fates,
        RouteService routes,
        ShopNpcService shops,
        HuntingLogService huntingLog,
        ObjectNameService objectNames,
        ObjectMemoryService memory,
        Configuration config,
        IDataManager data,
        IGameConfig gameConfig,
        IPluginLog log)
    {
        _gameConfig = gameConfig;
        _clientState = clientState;
        _objectTable = objectTable;
        _targetManager = targetManager;
        _tolk = tolk;
        _beacon = beacon;
        _cue = cue;
        _questMarkers = questMarkers;
        _places = places;
        _fishing = fishing;
        _fates = fates;
        _routes = routes;
        _shops = shops;
        _huntingLog = huntingLog;
        _objectNames = objectNames;
        _memory = memory;
        _config = config;
        _data = data;
        _log = log;
    }

    private ulong _lastSeenTargetId;   // hard/soft target id from the previous frame
    private ulong _lastSeenHardTargetId; // hard target only (Tab/F1-F12/F/click), for marker priority
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
                _tolk.SpeakInterrupt(AccessibilityStrings.WalkGuideEnded);
                _log.Info("[Nav] Gehhilfe: beendet, Spieler nicht mehr verfügbar.");
            }
            return;
        }

        PollMapFlag(player);

        // Das Betreten oder Verlassen eines Tiefen Gewoelbes tauscht den gesamten
        // Kategoriensatz, also faengt der Browser von vorne an, statt auf dem Index zu
        // landen, den der andere Satz hinterlassen hat. Hier wird ausserdem der
        // Ebenen-Schnappschuss geholt - ein reiner Lesevorgang, der nur ins Log
        // schreibt (siehe DeepDungeonFloor).
        if (DeepDungeon != null)
        {
            var deepNow = DeepDungeon.IsActive;
            if (deepNow != _deepCategoriesActive)
            {
                _deepCategoriesActive = deepNow;
                _categoryIndex = 0;
                _cycleIndex = -1;
                SelectedQuestDestination  = null;
                SelectedPlaceDestination  = null;
                SelectedObjectDestination = null;
                SelectedHuntTarget        = null;
                _log.Info($"[Nav] Kategoriensatz gewechselt: {(deepNow ? "Tiefes Gewoelbe" : "Welt")}.");
            }
            DeepDungeon.Poll(player);
        }

        // "Most recent choice wins": acquiring a game target with the game's OWN
        // keys (Tab, F1-F12, F, mouse click) drops any object-browser marker
        // still selected from earlier (a quest goal, waypoint, aetheryte or
        // fishing spot). Without this, Numpad3 kept walking to that stale marker
        // instead of the enemy just targeted in combat (user 2026-07-26) - the
        // marker is checked BEFORE the game target in TryResolveMarkerDestination.
        //
        // HARD target only (ITargetManager.Target): SoftTarget churns as the
        // player passes NPCs, and an ambient soft target must never wipe a marker
        // the player just picked. Browser enemy/NPC selections set the hard target
        // too, but flag themselves via _ownSelectionId - those are left alone
        // (their categories carry no marker anyway, so this is only belt-and-braces).
        var hardTargetId = _targetManager.Target?.GameObjectId ?? 0;
        if (hardTargetId != _lastSeenHardTargetId)
        {
            _lastSeenHardTargetId = hardTargetId;
            if (hardTargetId != 0 && hardTargetId != _ownSelectionId
                && (SelectedQuestDestination != null || SelectedPlaceDestination != null
                    || SelectedObjectDestination != null || SelectedHuntTarget != null))
            {
                _log.Info($"[Nav] Spiel-Ziel {hardTargetId:X} anvisiert - verwerfe Browser-Markerauswahl, Numpad3 läuft zum Ziel.");
                SelectedQuestDestination = null;
                SelectedPlaceDestination = null;
                SelectedObjectDestination = null;
                SelectedHuntTarget = null;
            }
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

            // No mod tone on targeting an enemy: the GAME already plays one
            // (user report 2026-07-18) - a second blip on top of it is noise,
            // not information. The spoken announcement below stays.
            if (target != null && announceTargetChanges && !isOwnSelection)
            {
                var distance = Vector3.Distance(player.Position, target.Position);
                var text = $"{AccessibilityStrings.TargetPrefix}{DescribeObject(target)}, " +
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
        // HousingEventObject (kind 12) is the game's own class for the usable
        // furniture in the housing wards. Without it the browser was blind to
        // things standing in plain sight and fully targetable - measured with
        // /acc objprobe in Mist (2026-08-15): "Chocobo-Stall" 12,5 m away,
        // "Mogry-Briefkasten" 11 m, "Dodo-Diarium-Pult" 36 m, all named by the
        // game itself and all zielbar=True. The user went looking for the
        // chocobo stable and the browser did not have it.
        ObjectKind.HousingEventObject,
    };

    // Kinds == null marks the marker categories (quest objectives and map
    // waypoints): they browse positions, not ObjectTable game objects.
    // The category is identified by the language-independent NavCategory key,
    // NOT by its spoken label, so switching announcement language (/acc lang)
    // never breaks the category logic. The label is resolved for speech only,
    // via AccessibilityStrings.CategoryLabel.
    private static readonly (NavCategory Cat, ObjectKind[]? Kinds)[] WorldCategories =
    {
        (NavCategory.All,             AllBrowseKinds),
        (NavCategory.Npcs,            new[] { ObjectKind.EventNpc }),
        // Merchants: the same NPCs, kept to those the game links to a shop sheet
        // (ShopNpcService) - see GetCategoryObjects.
        (NavCategory.Merchants,       new[] { ObjectKind.EventNpc }),
        (NavCategory.Enemies,         new[] { ObjectKind.BattleNpc }),
        // Verbuendete: dieselben Objektarten wie Gegner, plus Pc, in
        // GetCategoryObjects von CombatSide getrennt - ein Dungeon-Trupp ist
        // BattleNpc, solange er aus Trust/Duty-Support besteht, und Pc, sobald es
        // echte Mitspieler sind. Der Browser muss beides unter einer Kategorie
        // finden.
        (NavCategory.Allies,          new[] { ObjectKind.BattleNpc, ObjectKind.Pc }),
        (NavCategory.Players,         new[] { ObjectKind.Pc }),
        // HousingEventObject only here and in "Alles", not in the quest variants:
        // furniture is never what a quest marker points at, and adding it there
        // would only widen a scan that exists to narrow one.
        (NavCategory.Objects,         new[] { ObjectKind.EventObj, ObjectKind.Treasure, ObjectKind.HousingEventObject }),
        // Inhalte: dieselbe Form wie Haendler oben - eine Objektart, eingeengt
        // durch eine Nachschlage-Klasse, die die spieleigenen Sheets liest.
        // Treasure fehlt mit Absicht: eine Inhalts-Tuer ist immer ein EventObj,
        // Schatztruhen wuerden den Scan nur verbreitern.
        (NavCategory.Duties,          new[] { ObjectKind.EventObj }),
        // Quest-only variants of the three categories above (user request
        // 2026-08-02). Same object kinds, but restricted to what the current
        // quest markers point at - see IsQuestOnlyCategory / GetCategoryObjects.
        (NavCategory.QuestNpcs,       new[] { ObjectKind.EventNpc }),
        (NavCategory.QuestObjects,    new[] { ObjectKind.EventObj, ObjectKind.Treasure }),
        (NavCategory.QuestEnemies,    new[] { ObjectKind.BattleNpc }),
        (NavCategory.GatheringNodes,  new[] { ObjectKind.GatheringPoint }),
        // FATEs kommen aus dem FateManager (FateService), nicht aus der ObjectTable:
        // FATEs stehen NIE im Aufgaben-Journal - reine Welt-Ereignisse, die das Spiel
        // nur hier und auf der Karte fuehrt. Position speist den Numpad3-Auto-Lauf.
        (NavCategory.Fates,           null),
        // Jagdziele: was der aktuelle Rang des Jagdtagebuchs noch verlangt, mit
        // dem Gebiet, in dem das Monster lebt. Wie die Quest-Ziele auch dann,
        // wenn es in einer anderen Zone liegt - dort fuehrt Numpad3 zum
        // Uebergang statt ins Leere.
        (NavCategory.HuntingTargets,  null),
        // Angelplätze kommen aus dem FishingSpot-Sheet (FishingService), nicht aus
        // der ObjectTable: das Sheet kennt ALLE Angelplätze der Zone (das Spiel
        // streamt Angel-Löcher als Objekt erst in ~100 m ein, als Suche nach "wo
        // kann ich angeln" nutzlos) - genau wie bei den Ätheryten (User 2026-07-25).
        (NavCategory.FishingSpots,    null),
        // Ätheryten kommen aus den Kartendaten (PlacesService), nicht aus der
        // ObjectTable: die Marker kennen ALLE Ätheryten + Aethernet-Splitter
        // der Zone, die Objektsuche nur die in ~100 m (User-Wunsch 2026-07-13).
        (NavCategory.Aetherytes,      null),
        (NavCategory.QuestGoals,      null),
        (NavCategory.AcceptableQuests,null),
        // Freibriefe: giver NPCs (Levemete) + objectives of accepted leves, both
        // from the Map singleton (QuestMarkerService), not the ObjectTable - the
        // markers know the NPC and the task spot even out of streaming range.
        (NavCategory.Levequests,      null),
        (NavCategory.Waypoints,       null),
    };

    /// <summary>
    /// Der Kategoriensatz INNERHALB eines Tiefen Gewoelbes. Er ersetzt den Weltsatz
    /// vollstaendig, solange der Spieler in einem Gewoelbe ist.
    ///
    /// Eine Ebene enthaelt genau diese Arten von Dingen; sich durch sechzehn
    /// Weltkategorien - Angelplaetze, Freibriefe, Aetheryten - zu blaettern, um sie zu
    /// erreichen, sind fuenfzehn Tastendruecke Rauschen.
    ///
    /// ALLES bleibt drin, mit Absicht: nichts darf unerreichbar werden, nur weil keine
    /// Regel darauf gepasst hat. Was die Einordnung nicht erkennt, ist genau einen
    /// Tastendruck entfernt - wie zuvor.
    /// </summary>
    private static readonly (NavCategory Cat, ObjectKind[]? Kinds)[] DeepDungeonCategories =
    {
        (NavCategory.All,          AllBrowseKinds),
        // Raeume kommen vom Content-Director, nicht aus der Objekttabelle - siehe
        // DeepDungeonFloor. Kinds == null markiert das, wie bei den Markern auch.
        (NavCategory.DeepRooms,    null),
        // Schaetze nehmen ObjectKind.Treasure zusaetzlich zu EventObj: jede bisher
        // gemessene Gewoelbe-Truhe ist ein EventObj, aber eine Truhe, die das Spiel
        // unter seiner eigenen Truhen-Art fuehrt, darf nie aus der Kategorie fallen.
        (NavCategory.DeepTreasure, new[] { ObjectKind.EventObj, ObjectKind.Treasure }),
        (NavCategory.DeepCairns,   new[] { ObjectKind.EventObj }),
        (NavCategory.Enemies,      new[] { ObjectKind.BattleNpc }),
    };

    /// <summary>
    /// Der gerade gueltige Kategoriensatz. Der Wechsel zwischen beiden Saetzen wird in
    /// <see cref="Update"/> erkannt, das jeden Frame laeuft; die Begrenzung hier ist nur
    /// eine Absicherung, damit ein Index aus dem anderen Satz nie ausserhalb liest.
    /// </summary>
    private (NavCategory Cat, ObjectKind[]? Kinds)[] Categories
    {
        get
        {
            var set = DeepDungeon?.IsActive == true ? DeepDungeonCategories : WorldCategories;
            if (_categoryIndex >= set.Length) _categoryIndex = 0;
            return set;
        }
    }

    /// <summary>
    /// Der Gewoelbe-Leser, oder null, solange er nicht gesetzt ist - dann verhaelt sich
    /// diese Datei exakt wie in der offenen Welt. Eine Property statt eines
    /// Konstruktor-Arguments, damit die Signatur unveraendert bleibt.
    /// </summary>
    public DeepDungeonNav? DeepDungeon { get; set; }

    /// <summary>Ob der Gewoelbe-Satz im vorigen Frame galt, damit das Betreten oder
    /// Verlassen eines Gewoelbes den Browser zuruecksetzen kann.</summary>
    private bool _deepCategoriesActive;

    /// <summary>The spoken label of the current category, in the active language.</summary>
    private string CurrentCategoryLabel => Categories[_categoryIndex].Cat switch
    {
        NavCategory.DeepRooms    => AccessibilityStrings.DeepCategoryRooms,
        NavCategory.DeepTreasure => AccessibilityStrings.DeepCategoryTreasure,
        NavCategory.DeepCairns   => AccessibilityStrings.DeepCategoryCairns,
        var cat                  => AccessibilityStrings.CategoryLabel(cat),
    };

    /// <summary>Ob der Browser gerade auf der Raumliste steht.</summary>
    private bool IsDeepRoomCategory => Categories[_categoryIndex].Cat == NavCategory.DeepRooms;

    private bool IsQuestCategory           => Categories[_categoryIndex].Cat == NavCategory.QuestGoals;
    private bool IsUnacceptedQuestCategory => Categories[_categoryIndex].Cat == NavCategory.AcceptableQuests;
    private bool IsLevequestCategory       => Categories[_categoryIndex].Cat == NavCategory.Levequests;
    private bool IsPlacesCategory          => Categories[_categoryIndex].Cat == NavCategory.Waypoints;
    private bool IsAetheryteCategory       => Categories[_categoryIndex].Cat == NavCategory.Aetherytes;
    private bool IsFishingCategory         => Categories[_categoryIndex].Cat == NavCategory.FishingSpots;
    private bool IsFateCategory            => Categories[_categoryIndex].Cat == NavCategory.Fates;
    private bool IsHuntingCategory         => Categories[_categoryIndex].Cat == NavCategory.HuntingTargets;

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

    /// <summary>
    /// The hunting log target selected via the browser (Jagdziele category), or
    /// null. Its position is the AREA the monster lives in, taken from the map
    /// marker - 2D like every other marker, and in another zone it is only the
    /// direction of travel, so Plugin.cs routes it over the zone transitions
    /// exactly like a cross-zone quest goal.
    /// </summary>
    public HuntingTarget? SelectedHuntTarget { get; private set; }

    /// <summary>
    /// The world object selected via the browser (the plain object categories),
    /// or null. Kept SEPARATELY from the game target because the browser also
    /// lists objects the game refuses to target - quest props like the silk
    /// spools of "Eigensinnige Sylphe" are announced fine, but the hard target
    /// does not stick, and the auto-walk (which reads the game target) then had
    /// nothing to walk to (user report 2026-08-02: "Kein Ziel ausgewählt").
    /// Position is the object's own world position, refreshed by Plugin.cs from
    /// the object table at key-press time.
    /// </summary>
    public ObjectDestination? SelectedObjectDestination { get; private set; }

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
        SelectedObjectDestination = null;
        SelectedHuntTarget = null;

        if (IsQuestCategory || IsUnacceptedQuestCategory)
        {
            var label = CurrentCategoryLabel;
            var dests = GetQuestDestinations(IsUnacceptedQuestCategory);
            var here = dests.Count(d => d.InCurrentZone);
            var away = dests.Count - here;
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryQuestCount(label, here, away));
            return;
        }

        if (IsLevequestCategory)
        {
            // Deduplicated list, so the spoken count matches what the player
            // actually cycles through (the raw markers are mostly dupes).
            var leves = GetLevequestDestinations();
            var givers = leves.Count(d => d.Role == QuestMarkerRole.LeveGiver);
            var goals  = leves.Count(d => d.Role == QuestMarkerRole.LeveObjective);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryLevequestCount(givers, goals));
            return;
        }

        if (IsPlacesCategory)
        {
            var places = _places.GetPlaces();
            var exits = places.Count(p => p.IsZoneTransition);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryWaypointCount(places.Count, exits));
            return;
        }

        if (IsAetheryteCategory)
        {
            var aetherytes = _places.GetPlaces().Count(IsAetherytePlace);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryAetheryteCount(aetherytes));
            return;
        }

        if (IsFishingCategory)
        {
            var spots = _fishing.GetSpotsInCurrentZone().Count;
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryFishingCount(spots));
            return;
        }

        if (IsFateCategory)
        {
            var fates = _fates.GetActiveFates();
            var preparing = fates.Count(f => f.IsPreparing);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryFateCount(fates.Count - preparing, preparing));
            return;
        }

        if (IsHuntingCategory)
        {
            var targets = _huntingLog.GetOpenTargets();
            var here = targets.Count(t => t.InCurrentZone);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryHuntingCount(targets.Count, here));
            return;
        }

        // Die Raumliste wird aus dem Content-Director gezaehlt, nicht aus der
        // Objekttabelle - sie antwortet also auch dort, wo nichts geladen ist, und
        // genau dafuer gibt es sie.
        if (IsDeepRoomCategory)
        {
            var rooms = DeepDungeon?.RoomRows(_objectTable.LocalPlayer?.EntityId ?? 0).Count ?? 0;
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryObjectCount(CurrentCategoryLabel, rooms));
            return;
        }

        var count = GetCategoryObjects().Count;
        _tolk.SpeakInterrupt(AccessibilityStrings.CategoryObjectCount(CurrentCategoryLabel, count));
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

        if (IsLevequestCategory)
        {
            CycleLevequestDestination(direction, player);
            return;
        }

        if (IsPlacesCategory || IsAetheryteCategory)
        {
            CyclePlaceDestination(direction, player, aetherytesOnly: IsAetheryteCategory);
            return;
        }

        if (IsFishingCategory)
        {
            CycleFishingDestination(direction, player);
            return;
        }

        if (IsFateCategory)
        {
            CycleFateDestination(direction, player);
            return;
        }

        if (IsHuntingCategory)
        {
            CycleHuntTarget(direction, player);
            return;
        }

        if (IsDeepRoomCategory)
        {
            CycleDeepRoom(direction, player);
            return;
        }

        var objects = GetCategoryObjects();
        if (objects.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoObjectsInRange(CurrentCategoryLabel, CycleRange));
            return;
        }

        var count = objects.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var obj = objects[_cycleIndex];

        // Suppress the target-change announcer: we announce with position info here.
        _ownSelectionId = obj.GameObjectId;
        _targetManager.Target = obj;

        // Remember the pick independently of the game target. Objects the game
        // will not target (quest props) are still listed and announced here, and
        // walking to them must not depend on a target that does not stick.
        //
        // The RESOLVED name is stored, not the raw one: the auto-walk announces
        // this string later, and for a gathering node the raw name is empty -
        // which is why Numpad 3 used to say "Laufe zu Unbenannt" right after the
        // browser had correctly said "Erzader, Stufe 20" (user 2026-08-08).
        SelectedObjectDestination = new ObjectDestination(
            obj.GameObjectId,
            obj.ObjectKind == ObjectKind.GatheringPoint
                ? DescribeGatheringPoint(obj)
                : _objectNames.Describe(obj),
            obj.Position);

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

        LogGatheringNodeState(obj, distance);

        // Gathering nodes: the type and required level ARE the useful content
        // ("Erzader, Stufe 20"); their object name is usually empty and the
        // kind ("Sammelpunkt") would just repeat the category.
        // In the merchant category the SHOP KIND replaces the generic "NPC":
        // the player already knows they are cycling NPCs, what they need is
        // whether this one sells for gil or trades for tokens.
        string description;
        if (IsMerchantCategory)
        {
            // Same shape as DescribeObject: ordinal on the name, visited mark
            // last - a market row holds several NPCs with one name too.
            var label = $"{NpcPrefix(obj)}{_objectNames.Describe(obj)}";
            description = label + _memory.NumberSuffix(obj, label)
                        + $", {AccessibilityStrings.ShopKindWord(_shops.KindOf(obj.BaseId))}"
                        + _memory.VisitedSuffix(obj);
        }
        else if (IsDutyCategory && DungeonSide.Describe(obj, _data, _log) is { } duty)
        {
            // Genau dieselbe Form wie beim Haendler eine Zeile hoeher: in der
            // Kategorie Inhalte ersetzt der INHALT das nichtssagende "Objekt". Er
            // muss es auch, denn jede dieser Tueren heisst im Spiel "Eingang" - ohne
            // den Namen des Inhalts sind 30 Tueren im Gebiet 30 mal dasselbe Wort.
            var label = _objectNames.Describe(obj);
            description = label + _memory.NumberSuffix(obj, label)
                        + $", {AccessibilityStrings.DutyEntrance(duty.Name, duty.ContentType, duty.Level, duty.TypeName)}"
                        + _memory.VisitedSuffix(obj);
        }
        else
        {
            description = DescribeObject(obj);
        }

        // Position goes LAST: the name is what the user is listening for, the
        // counter only tells them how far they have cycled (user wish
        // 2026-07-19). The rejection warning stays at the very end.
        // Stufe und HP NUR wenn das Spiel die Anvisierung angenommen hat: nur dann
        // ist die Ziel-Leiste gefuellt, und nur dann saehe ein sehender Spieler die
        // Werte. Bei einer Ablehnung (ausser Reichweite, Sichtlinie unterbrochen)
        // bleibt es beim bisherigen Hinweis "nicht anvisiert" - ohne Werte, die auf
        // dem Bildschirm nirgends stehen.
        var stats = rejected ? string.Empty : DescribeTargetHp(obj);
        var text = $"{description}, " +
                   $"{FormatDistance(distance)}, " +
                   $"{CalculateDirection(player, obj.Position)}" +
                   $"{stats}, " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}." +
                   (rejected ? AccessibilityStrings.NotTargetedSuffix : "");
        _log.Info($"[Nav] Auswahl: {text} (id={obj.GameObjectId:X})");
        _tolk.SpeakInterrupt(text);
    }

    // ── Tiefes Gewoelbe: durch die Raeume der aktuellen Ebene blaettern ──

    /// <summary>
    /// Blaettert durch die Raeume der aktuellen Ebene.
    ///
    /// Die Zeilen werden bei JEDEM Druck neu gelesen, weil ein Raum, den der Spieler
    /// gerade aufgedeckt hat, in der Liste auftauchen muss, ohne dass er die Kategorie
    /// verlassen und neu betreten muss.
    /// </summary>
    private void CycleDeepRoom(int direction, IGameObject player)
    {
        var rows = DeepDungeon?.RoomRows(player.EntityId)
                   ?? new List<DeepDungeonNav.RoomRow>();
        if (rows.Count == 0)
        {
            SelectedObjectDestination = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.DeepNoRooms);
            return;
        }

        var count = rows.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var row = rows[_cycleIndex];

        // Ein Raum, in dem der Spieler schon war, IST ein Laufziel: die Tuer, durch die
        // er hereingekommen ist. Der Director gibt Raeumen keine Koordinaten, der
        // einzige begehbare Punkt ist also einer, auf dem der Spieler beobachtet wurde.
        //
        // DIE PLATZHALTER-ID IST TRAGEND. Der Objekt-Zweig in Plugin.cs beginnt mit
        // `if ((Target?.GameObjectId ?? 0) == obj.ObjectId) return None;` - bei einer Id
        // von 0 und nichts anvisiertem vergleicht das 0 mit 0, gibt den Lauf an den
        // Ziel-Pfad zurueck, und der Raum wird stillschweigend nie angelaufen.
        // ulong.MaxValue kann weder auf ein echtes Objekt noch auf ein leeres Ziel
        // passen, also laeuft der Positions-Zweig, die Objektsuche geht ins Leere, und
        // der gemerkte Punkt wird benutzt.
        SelectedObjectDestination = row.Walkable is { } point
            ? new ObjectDestination(ulong.MaxValue, AccessibilityStrings.DeepRoomName(row.Index), point)
            : null;

        // Entfernung und Richtung, in derselben Form und Reihenfolge wie in jeder
        // anderen Kategorie - der Raum hat jetzt eine echte Position, es gibt also
        // keinen Grund, ihn anders anzusagen als ein Objekt.
        var where = row.Walkable is { } dest
            ? $", {FormatDistance(Vector3.Distance(player.Position, dest))}"
              + $", {CalculateDirection(player, dest)}"
            : string.Empty;

        var text = $"{row.Text}{where}, {AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Nav] Raum-Auswahl: {text} (Ziel {(row.Walkable?.ToString() ?? "keins")})");
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
                ? AccessibilityStrings.NoAcceptableQuests
                : AccessibilityStrings.NoQuestGoals);
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

        // The kind of quest is flagged so a blind player can tell story, job and
        // beast tribe quests apart from side quests (a sighted player sees a
        // distinct marker). Side quests stay unprefixed - see QuestKind.
        var story = AccessibilityStrings.QuestKindPrefix(dest.Kind);

        // The list is level-ordered, so the level has to be audible - otherwise
        // the order is a silent rule the player cannot act on. Omitted when the
        // game gave us no level rather than announcing a made-up "Stufe 0".
        var level = dest.Level > 0 ? AccessibilityStrings.LevelPrefix(dest.Level) : string.Empty;

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
                   (string.IsNullOrEmpty(zone) ? AccessibilityStrings.InAnotherArea : AccessibilityStrings.InArea(zone));
            if (hop != null)
            {
                text += AccessibilityStrings.RouteViaHop(
                    hop.Name,
                    FormatDistance(Distance2D(player.Position, hop.Position)),
                    CalculateDirection(player, hop.Position),
                    hops - 1);
                text += AccessibilityStrings.NumpadWalksToTransition;
            }
            text += detail;
        }
        // Counter last, after the route hints - see CycleObject.
        text += $" {AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Quest] Auswahl: {text}");
        _tolk.SpeakInterrupt(text);
    }

    // ── FATEs: aktive Welt-Ereignisse der Zone anlaufen ──
    //
    // FATEs are always in the current zone (FateManager only holds this zone's),
    // so a FATE destination is modelled as an in-zone QuestDestination and flows
    // through the SAME downstream path (SelectedQuestDestination -> Numpad3
    // auto-walk, walk guide) unchanged - no separate steering needed.
    private void CycleFateDestination(int direction, IGameObject player)
    {
        var fates = _fates.GetActiveFates()
            .OrderBy(f => Vector3.Distance(player.Position, f.Position))
            .ToList();
        if (fates.Count == 0)
        {
            SelectedQuestDestination = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoFatesInZone);
            return;
        }

        var count = fates.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var fate = fates[_cycleIndex];

        // Reuse the quest destination path: the FATE is in the current zone with a
        // full 3D world position, so it resolves and walks like any in-zone quest
        // goal. Radius 0 -> the default stop range lands the player near the FATE
        // centre, well inside its participation circle.
        SelectedQuestDestination = new QuestDestination(
            QuestName: fate.Name,
            Detail: string.Empty,
            Position: fate.Position,
            Radius: 0f,
            TerritoryTypeId: (ushort)_clientState.TerritoryType,
            MapId: 0,
            InCurrentZone: true,
            // A FATE is not a quest, so no kind is spoken - the FATE announcement
            // below already says what it is.
            Kind: QuestKind.Unknown,
            Level: fate.Level);

        // Name first, then level, then progress (user choice 2026-07-31), then
        // position - the same "content first, counter last" order as the other
        // cyclers.
        var text = $"{AccessibilityStrings.FateEntry(fate.Name, fate.Level, fate.Progress, fate.IsPreparing)}, " +
                   $"{FormatDistance(Vector3.Distance(player.Position, fate.Position))}, " +
                   $"{CalculateDirection(player, fate.Position)}. " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Fate] Auswahl: {text} (id={fate.FateId})");
        _tolk.SpeakInterrupt(text);
    }

    // ── Freibriefe: Geber-NPCs + Ziele angenommener Leves ──
    //
    // Both come from the same Map markers as regular quests, so a leve
    // destination is a QuestDestination and flows into the SAME downstream path
    // (SelectedQuestDestination -> Numpad3 auto-walk, walk guide, cross-zone
    // routing) unchanged. The only leve-specific bit is the spoken ROLE prefix
    // (giver NPC vs. objective) so the player knows which they are walking to.

    /// <summary>Leve destinations, in-zone first, then nearest by walk distance;
    /// within that givers before objectives so "where do I pick one up" is
    /// audible before "where do I go with the one I have".
    ///
    /// The game emits SEVERAL marker entries per leve, all pointing at the SAME
    /// spot (log 2026-07-28: one leve gave 3-4 identical positions), so the raw
    /// list is mostly dupes - a blind player would cycle through the same leve at
    /// the same place again and again. Collapse to one entry per (role, name,
    /// rounded position): identical spots merge, but two different leves that
    /// happen to share a spot stay separate (distinct names), and givers at
    /// different locations stay separate (distinct positions).</summary>
    private List<QuestDestination> GetLevequestDestinations()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return new List<QuestDestination>();

        var ordered = _questMarkers.GetLevequestDestinations()
            .OrderByDescending(d => d.InCurrentZone)
            .ThenBy(d => d.Role == QuestMarkerRole.LeveGiver ? 0 : 1)
            .ThenBy(d => EffectiveWalkDistance(player.Position, d))
            .ThenBy(d => d.QuestName, StringComparer.Ordinal);

        var seen = new HashSet<(QuestMarkerRole, string, int, int)>();
        var result = new List<QuestDestination>();
        foreach (var d in ordered)
        {
            // Round to whole metres so float jitter never splits one real spot.
            var key = (d.Role, d.QuestName, (int)MathF.Round(d.Position.X), (int)MathF.Round(d.Position.Z));
            if (seen.Add(key)) result.Add(d);
        }
        return result;
    }

    private void CycleLevequestDestination(int direction, IGameObject player)
    {
        var dests = GetLevequestDestinations();
        if (dests.Count == 0)
        {
            SelectedQuestDestination = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoLevequests);
            return;
        }

        var count = dests.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var dest = dests[_cycleIndex];
        SelectedQuestDestination = dest;

        // Role tells the player what this destination IS: the Levemete to accept
        // a leve, or the spot to carry an accepted one out (user request).
        var role = AccessibilityStrings.LeveRolePrefix(dest.Role);
        // Marker tooltip often names the actual objective ("Mit X sprechen");
        // append only when it adds something beyond the leve name.
        var detail = !string.IsNullOrWhiteSpace(dest.Detail) && dest.Detail != dest.QuestName
            ? $" {dest.Detail}."
            : string.Empty;
        var level = dest.Level > 0 ? AccessibilityStrings.LevelPrefix(dest.Level) : string.Empty;

        string text;
        if (dest.InCurrentZone)
        {
            text = $"{role}{level}{dest.QuestName}, " +
                   $"{FormatDistance(Vector3.Distance(player.Position, dest.Position))}, " +
                   $"{CalculateDirection(player, dest.Position)}.{detail}";
        }
        else
        {
            // Leve in another zone: name the target zone and the transition that
            // leads there (same BFS route the quest category uses).
            var zone = _places.GetMapName(dest.MapId);
            var hop  = _places.FindFirstHopToMap(dest.MapId, out var hops);
            text = $"{role}{level}{dest.QuestName}, " +
                   (string.IsNullOrEmpty(zone) ? AccessibilityStrings.InAnotherArea : AccessibilityStrings.InArea(zone));
            if (hop != null)
            {
                text += AccessibilityStrings.RouteViaHop(
                    hop.Name,
                    FormatDistance(Distance2D(player.Position, hop.Position)),
                    CalculateDirection(player, hop.Position),
                    hops - 1);
                text += AccessibilityStrings.NumpadWalksToTransition;
            }
            text += detail;
        }
        text += $" {AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Leve] Auswahl: {text}");
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
        _tolk.SpeakInterrupt(AccessibilityStrings.NewFlagMarker(FormatDistance(distance), compass));
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
                ? AccessibilityStrings.NoAetherytesFound
                : AccessibilityStrings.NoWaypointsFound);
            return;
        }

        var count = places.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var place = places[_cycleIndex];
        SelectedPlaceDestination = place;

        // Direction uses X/Z only - the placeholder Y does not affect it.
        // NOTE: place.TypeLabel is still German here - it is coupled to PlacesService
        // comparison logic (IsAetherytePlace) and gets translated with that group.
        // Standing at it? Then take it as the game target too, so it can be used.
        var targeted = TryTargetMarkerObject(place);

        var text = $"{place.Name}, {place.TypeLabel}, " +
                   $"{FormatDistance(Distance2D(player.Position, place.Position))}, " +
                   $"{CalculateDirection(player, place.Position)}, " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}." +
                   (targeted ? " " + AccessibilityStrings.MarkerTargeted : string.Empty);
        _log.Info($"[Orte] Auswahl: {text} pos=({place.Position.X:F1}|{place.Position.Z:F1})");
        _tolk.SpeakInterrupt(text);
    }

    // ── Angelplätze: durch die Angel-Löcher des Gebiets blättern ──
    //
    // Spots come from the static FishingSpot sheet (FishingService), sorted
    // nearest-first. Like the aetheryte/waypoint categories a spot is just a
    // named position, so it flows into the SAME walk guide / auto-walk via
    // SelectedPlaceDestination - no separate steering path needed. The required
    // fishing level is spoken so the player knows if the spot is usable yet.
    private void CycleFishingDestination(int direction, IGameObject player)
    {
        var spots = _fishing.GetSpotsInCurrentZone();
        if (spots.Count == 0)
        {
            SelectedPlaceDestination = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoFishingSpots);
            return;
        }

        var count = spots.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var spot = spots[_cycleIndex];

        // Reuse PlaceDestination so the existing walk guide picks it up unchanged.
        // IsWaterSpot only for raw water-centre spots (snap to nearest bank);
        // hand-verified override coordinates are already the exact castable spot,
        // so they walk straight there like a map flag (spot.IsExact).
        SelectedPlaceDestination = new PlaceDestination(
            spot.Name, AccessibilityStrings.FishingSpotType, spot.Position,
            IsZoneTransition: false, TargetMapId: 0, IsWaterSpot: !spot.IsExact);

        var text = AccessibilityStrings.FishingSpotEntry(spot.Name, spot.Level) + ", " +
                   $"{FormatDistance(Distance2D(player.Position, spot.Position))}, " +
                   $"{CalculateDirection(player, spot.Position)}, " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Fish] Auswahl: {text} pos=({spot.Position.X:F1}|{spot.Position.Z:F1})");
        _tolk.SpeakInterrupt(text);
    }

    // ── Jagdziele: was der aktuelle Rang noch verlangt ──
    //
    // Targets come from the hunting log (HuntingLogService), not the object
    // table: the log knows what is still missing even when the monster is three
    // zones away, and THAT is the question this category answers. Same shape as
    // the quest goals - in-zone first, cross-zone routed over the transitions -
    // because it is the same problem: a named place a blind player cannot see
    // on the map.
    private void CycleHuntTarget(int direction, IGameObject player)
    {
        // A monster the game currently has loaded outranks every marker: the
        // habitat marker is the middle of an area, the live specimen is the
        // thing to walk to and kill (user request 2026-08-17 - "das er direkt
        // zum monster läuft und nicht nur in das gebiet"). Looked up once per
        // entry here so the list can be ordered by it AND the announcement can
        // name the real distance.
        var targets = _huntingLog.GetOpenTargets()
            .Select(t => (Target: t, Live: _huntingLog.FindNearestLive(t.MonsterName)))
            .OrderByDescending(x => x.Live != null)
            .ThenBy(x => x.Live != null ? Distance2D(player.Position, x.Live.Position) : float.MaxValue)
            .ThenByDescending(x => x.Target.InCurrentZone)
            .ThenBy(x => x.Target.Position is { } p ? Distance2D(player.Position, p) : float.MaxValue)
            .ThenBy(x => x.Target.MonsterName, StringComparer.Ordinal)
            .ToList();

        if (targets.Count == 0)
        {
            SelectedHuntTarget = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoHuntingTargets);
            return;
        }

        var count = targets.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var (target, live) = targets[_cycleIndex];
        SelectedHuntTarget = target;

        // Name plus what is still missing - the kill count is the whole point of
        // the entry, and without it the list cannot be prioritised.
        var text = AccessibilityStrings.HuntingTargetEntry(
            target.MonsterName, target.Killed, target.Required);

        if (live != null)
        {
            // Standing right there: the area name would only be noise, and the
            // numbers must be the ones that lead to the monster.
            text += ", " + AccessibilityStrings.HuntingMonsterNearby + ", " +
                    $"{FormatDistance(Distance2D(player.Position, live.Position))}, " +
                    $"{CalculateDirection(player, live.Position)}.";
        }
        else if (target.InCurrentZone && target.Position is { } pos)
        {
            text += ", " + AccessibilityStrings.HuntingArea(target.AreaName) + ", " +
                    $"{FormatDistance(Distance2D(player.Position, pos))}, " +
                    $"{CalculateDirection(player, pos)}.";
        }
        else
        {
            // Another zone: name it, then the transition that leads there. The
            // area alone would be useless - a blind player cannot look up where
            // "Sommerfurt" is.
            var zone = string.IsNullOrEmpty(target.ZoneName)
                ? AccessibilityStrings.InAnotherArea
                : AccessibilityStrings.InArea(target.ZoneName);
            // HuntingArea is empty when the log names no habitat. The separator
            // has to go away with it, otherwise the sentence opens on a gap.
            var habitat = AccessibilityStrings.HuntingArea(target.AreaName);
            text += ", " + (habitat.Length > 0 ? habitat + " " + zone : zone);

            var hop = _places.FindFirstHopToMap(target.MapId, out var hops);
            if (hop != null)
            {
                text += AccessibilityStrings.RouteViaHop(
                    hop.Name,
                    FormatDistance(Distance2D(player.Position, hop.Position)),
                    CalculateDirection(player, hop.Position),
                    hops - 1);
                text += AccessibilityStrings.NumpadWalksToTransition;
            }
        }

        text += $" {AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Jagd] Auswahl: {text}");
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

    /// <summary>How far the real object may sit from its map marker and still
    /// count as the same thing. Map marker coordinates come from map PIXELS, so
    /// they are a few metres coarse; 15 m absorbs that without reaching the next
    /// aethernet shard, which never stands that close to its aetheryte.</summary>
    private const float MarkerObjectMatchRange = 15f;

    /// <summary>
    /// Targets the actual game object behind a map marker, when the game has it
    /// loaded. The marker categories browse map DATA - there is no object in
    /// them - so standing at an aetheryte left the player unable to use it
    /// (user report 2026-08-07: "konnte auch hinlaufen aber er wird nicht
    /// markiert so das ich in nutzen kann"). Interacting needs a target, and the
    /// object is in the table once we are close enough for it to be streamed in.
    ///
    /// Silent when nothing is found: browsing a list of aetherytes across the
    /// whole zone would otherwise comment on every distant one. Returns true
    /// only when the game accepted the target - it can refuse (see CycleObject).
    /// </summary>
    private bool TryTargetMarkerObject(PlaceDestination place)
    {
        if (!IsAetherytePlace(place)) return false;

        var match = _objectTable
            .Where(o => o.ObjectKind == ObjectKind.Aetheryte)
            .Select(o => (Obj: o, Gap: Distance2D(o.Position, place.Position)))
            .Where(x => x.Gap <= MarkerObjectMatchRange)
            .OrderBy(x => x.Gap)
            .FirstOrDefault();
        if (match.Obj == null) return false;

        var accepted = TargetFromBrowser(match.Obj);
        _log.Info($"[Orte] Objekt zum Marker '{place.Name}': id={match.Obj.GameObjectId:X}, " +
                  $"{match.Gap:F1} m vom Marker, anvisiert={accepted}");
        return accepted;
    }

    /// <summary>
    /// Targets an object ON BEHALF of the browser and reports whether the game
    /// accepted it (it can refuse - see CycleObject).
    ///
    /// The id is flagged as our own first: without that the target watcher in
    /// <see cref="Update"/> would read the change as "the player targeted
    /// something else" and drop the very browser selection this targeting came
    /// from.
    /// </summary>
    public bool TargetFromBrowser(IGameObject obj)
    {
        _ownSelectionId = obj.GameObjectId;
        _targetManager.Target = obj;
        return (_targetManager.Target?.GameObjectId ?? 0) == obj.GameObjectId;
    }

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
        // Fishing spots are static per zone: show the category only where the
        // zone actually has any (no clutter in waterless zones), regardless of
        // the active class - a blind player wants to find the water BEFORE
        // switching to fisher, just like aetherytes are always shown.
        if (Categories[index].Cat == NavCategory.FishingSpots)
            return _fishing.GetSpotsInCurrentZone().Count > 0;

        // Freibriefe only where the game actually reports leve markers (a giver
        // NPC nearby or an accepted leve) - no empty "0 Freibriefe" in zones
        // without any. An empty answer inside the category is still a real
        // answer once it IS offered, exactly like the gathering category.
        if (Categories[index].Cat == NavCategory.Levequests)
            return _questMarkers.GetLevequestDestinations().Count > 0;

        // FATEs only where the zone actually has an active/preparing one - no
        // empty "0 FATEs" category in zones without any (same rule as fishing
        // spots and leves).
        if (Categories[index].Cat == NavCategory.Fates)
            return _fates.GetActiveFates().Count > 0;

        // Jagdziele nur, solange der Rang noch etwas verlangt: Klassen ohne
        // Jagdtagebuch (Handwerker, Jobs nach ARR) und ein fertig gejagter Rang
        // liefern beide eine leere Liste, und eine leere Kategorie ist im
        // Durchblättern nur ein Tastendruck Rauschen (Regel wie oben).
        if (Categories[index].Cat == NavCategory.HuntingTargets)
            return _huntingLog.GetOpenTargets().Count > 0;

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

        var inRange = _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && kinds.Contains(o.ObjectKind)
                        && Vector3.Distance(player.Position, o.Position) <= CycleRange)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .ToList();

        TrackGatheringAvailability(inRange, player);

        var listed = inRange.Where(IsWorthBrowsing).ToList();

        // One line per key press, and only when something was actually dropped:
        // it shows in the log how much the filter takes off the list - and would
        // show at once if it ever took too much. Two rules drop things here:
        // nameless-and-untargetable extras, and gathering nodes the game does not
        // currently raise (see IsWorthBrowsing).
        if (listed.Count != inRange.Count)
            _log.Info($"[Nav] Browser: {listed.Count} von {inRange.Count} Objekten " +
                      $"({inRange.Count - listed.Count} nicht nutzbar ausgeblendet).");

        return listed;
    }

    /// <summary>
    /// Whether an object belongs in the browser list at all.
    ///
    /// The rule is about NAMES, and it has to be asked per OBJECT. It used to be
    /// asked per CATEGORY: gathering nodes have no name of their own, so the
    /// check was skipped whenever the category contained them - and the "Alles"
    /// category contains them, which switched the filter off for everything in
    /// it. That is why the log showed bare announcements like ", Objekt, 24
    /// Meter, 7 von 68" (user report + log 2026-08-08 00:40): dozens of scenery
    /// extras were padding the list with entries the player cannot identify.
    ///
    /// What stays:
    ///  - gathering nodes the game lets you TARGET (their type and level ARE the
    ///    description, see DescribeGatheringPoint),
    ///  - anything with a speakable name, the object's own or the sheets' one,
    ///  - nameless things the game lets you TARGET: the game marks those as
    ///    interactive, so hiding them could hide something usable. They are
    ///    announced as "Objekt ohne Namen" rather than as a blank.
    ///
    /// GATHERING NODES ARE NO LONGER EXEMPT FROM THE TARGETABLE TEST (user report
    /// 2026-08-09: "es gibt welche wo ich abbauen kann und wo nicht"). They used
    /// to pass unconditionally, and the browser therefore offered places where
    /// nothing stands. MEASURED that day with [GatherProbe]: of 16 nodes listed
    /// in one spot exactly ONE was targetable (TargetableStatus=123, the
    /// ObjectTargetableFlags.IsTargetable bit set, RenderFlags=None) - and that
    /// was the one the player could work. The other fifteen all read
    /// TargetableStatus=248/120 without that bit and RenderFlags=128, i.e. the
    /// game neither draws nor offers them. DISTANCE IS NOT THE CAUSE: two of the
    /// dead ones were measured from 2 m.
    ///
    /// This is parity, not a filter that removes game information: the game holds
    /// every possible placement of an area as an object but only ever raises a
    /// few. A sighted player sees the ones that are drawn; the browser now lists
    /// the same set. STILL UNMEASURED is whether a LIVE node reads as targetable
    /// from far away - see TrackGatheringAvailability, which logs exactly that.
    ///
    /// What goes: nameless AND untargetable - background extras, scenery, and
    /// invisible trigger spots. In the measured sample (log 2026-08-06, 38
    /// objects nearby) all 25 nameless ones were untargetable, and none of them
    /// had a name in the game's sheets either. A sighted player has no name for
    /// them and cannot interact with them; announcing them is noise, not parity.
    ///
    /// QUEST STATE IS DELIBERATELY NOT A CRITERION (tried and reverted the same
    /// day, 2026-08-08). Hiding props of unaccepted quests emptied whole
    /// categories - "0 von 4 (ausgeblendet: 4 fremde Quest)" where the player
    /// had been using one of them minutes earlier. The user named the reason:
    /// an object does not only exist for its quest, you can act on it
    /// independently. EObj.Data records that an object ALSO appears in a quest,
    /// nothing more. Showing one object too many costs a key press; hiding one
    /// costs the player their objective.
    /// </summary>
    private bool IsWorthBrowsing(IGameObject o)
        => !IsEmptiedTreasure(o)
           && (o.ObjectKind == ObjectKind.GatheringPoint
               ? o.IsTargetable
               : _objectNames.Resolve(o) != null || o.IsTargetable);

    /// <summary>
    /// True once a treasure chest has been dealt with, so the browser can drop
    /// it (user wish 2026-08-09: "objekte die man aufhebt in dungeons sollten
    /// aus der liste verschwinden"). Measured case in the log of 2026-08-09
    /// 00:19:35 - "Schatztruhe 2, Schatz, schon besucht, 2 Meter, 1 von 26",
    /// still occupying a list slot after the player had been there.
    ///
    /// THE GAME KEEPS THIS STATE ITSELF, so nothing is reconstructed here:
    /// <c>Treasure.State</c> (FFXIVClientStructs, field offset 416) runs
    /// Unopened -> Opening -> Opened -> Unk3 -> FadingOut -> FadedOut. Anything
    /// past Unopened means the chest is done with; the object lingers in the
    /// table for a while afterwards purely to play its fade-out.
    ///
    /// State rather than the parallel <c>Flags.Opened</c>: the struct's own
    /// remarks call the two overlapping, and Flags is documented as being set
    /// inconsistently ("sometimes when fading starts, sometimes when fading is
    /// complete"), while the state sequence is ordered and covers Opening as
    /// well - by then the outcome is already decided.
    ///
    /// ONLY the browser list is filtered. Targeting the chest with the game's
    /// own keys still announces it: the game lets you target it, so staying
    /// silent there would hide something the player deliberately selected.
    /// </summary>
    private unsafe bool IsEmptiedTreasure(IGameObject o)
    {
        if (o.ObjectKind != ObjectKind.Treasure || o.Address == nint.Zero) return false;

        var treasure = (Treasure*)o.Address;
        if (treasure->State == Treasure.TreasureState.Unopened) return false;

        // One line per chest that drops out, not one per key press: the state is
        // read on every browse and would otherwise flood the log.
        if (_reportedEmptyTreasures.Add(o.GameObjectId))
            _log.Info($"[Nav] Schatztruhe {o.GameObjectId:X} ist '{treasure->State}' - " +
                      "faellt aus der Browser-Liste.");
        return true;
    }

    // Chests already reported as emptied, so the log line above stays a one-off.
    // Never cleared - this service has no zone-change hook, and adding one for a
    // log-deduplication set would be the wrong trade. It holds one 8-byte id per
    // chest the player empties in a session; the browse behaviour does not read
    // it at all.
    private readonly HashSet<ulong> _reportedEmptyTreasures = new();

    /// <summary>
    /// Debug probe: logs EVERY nearby ObjectTable entry within 60 m - including
    /// the ones the normal browser hides (empty name, untargetable) - with kind,
    /// name, DataId, distance and world position. Used to find out how the game
    /// represents things that are audible but not in the browser list, e.g. the
    /// humming quest-battle circle / duty-entrance portal at Quiverons Pfarrhaus.
    /// Bound to the UI-dump key (Strg+F5) and to /acc objprobe. Announces the
    /// count so the blind user knows the dump ran; the detail goes to the log
    /// ([ObjProbe]).
    ///
    /// Also logs NamePlateIconId and EventId per object. That is the groundwork
    /// for the requested "quest targets only" filter (user 2026-08-02): the
    /// nameplate icon is exactly the marker a SIGHTED player sees floating above
    /// a quest giver, so filtering by it gives the same information rather than a
    /// reconstruction. Which icon number means what is undocumented - this probe
    /// is how it gets measured instead of guessed.
    /// </summary>
    public unsafe void DumpNearbyObjects()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt("Objekt-Sonde: kein Spieler.");
            return;
        }

        var near = _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId
                        && Vector3.Distance(player.Position, o.Position) <= 60f)
            .OrderBy(o => Vector3.Distance(player.Position, o.Position))
            .ToList();

        _log.Info($"[ObjProbe] === {near.Count} Objekte in 60 m, Spieler @ {player.Position} ===");
        foreach (var o in near)
        {
            var name = string.IsNullOrWhiteSpace(o.Name.TextValue) ? "<leer>" : o.Name.TextValue;
            var dist = Vector3.Distance(player.Position, o.Position);
            // Nameplate icon + event id straight from the game struct
            // (GameObject.NamePlateIconId @272, EventId @244 - ilspycmd 2026-08-02).
            var native = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)o.Address;
            var icon   = native != null ? native->NamePlateIconId : 0;
            var evt    = native != null ? native->EventId.Id : 0;
            _log.Info(
                $"[ObjProbe] {dist,6:0.0}m {o.ObjectKind,-14} " +
                $"DataId={o.BaseId} name='{name}' zielbar={o.IsTargetable} " +
                $"icon={icon} event={evt} " +
                $"pos={o.Position} id={o.GameObjectId:X}");
        }

        _tolk.SpeakInterrupt($"Objekt-Sonde: {near.Count} Objekte im Log.");

        DumpHousingPlot();
        DumpMapMarkers();
    }

    /// <summary>
    /// Debug probe for the open question behind "kann man die Eingänge
    /// benennen?" (user 2026-08-15). A housing ward holds several doors that all
    /// share DataId 2002737 and the single word "Eingang" - four of them within
    /// 50 m in the measured dump - so the name identifies nothing. What a
    /// sighted player reads there is the PLOT, and the plot is what has to come
    /// from somewhere.
    ///
    /// What the game offers is asked here rather than assumed. HousingManager
    /// (ilspycmd on the installed FFXIVClientStructs.dll, 2026-08-15) exposes
    /// GetCurrentWard / GetCurrentPlot / GetCurrentDivision / GetCurrentRoom /
    /// GetCurrentHouseId - every one of them phrased as CURRENT, i.e. about
    /// where the PLAYER stands, not about an object one can point at. There is
    /// no "which plot does this door belong to".
    ///
    /// So the probe logs those values continuously while the player walks. Two
    /// outcomes, and they lead to different features:
    ///  - the values change as one crosses onto a plot: then the plot number is
    ///    real, live game state, and the door can be named the moment the player
    ///    is on the plot ("Eingang, Parzelle 23").
    ///  - they stay put outside: then the game genuinely does not track plots
    ///    per position out there, and naming individual doors from a distance
    ///    would have to be reconstructed - which is exactly what this project
    ///    does not do.
    /// Either way the answer is measured before anything is built on it.
    /// </summary>
    private unsafe void DumpHousingPlot()
    {
        var housing = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
        if (housing == null)
        {
            _log.Info("[PlotProbe] HousingManager.Instance() ist null - keine Wohngebiets-Zone.");
            return;
        }

        _log.Info($"[PlotProbe] ward={housing->GetCurrentWard()} plot={housing->GetCurrentPlot()} " +
                  $"division={housing->GetCurrentDivision()} room={housing->GetCurrentRoom()} " +
                  $"hausId={housing->GetCurrentHouseId().Id} " +
                  $"draussen={housing->IsOutside()} drinnen={housing->IsInside()} " +
                  $"art={housing->GetCurrentHousingTerritoryType()}");
    }

    /// <summary>
    /// Debug probe: logs the game's DYNAMIC map markers from
    /// AgentMap.EventMarkers (per FFXIVClientStructs doc: FateManager,
    /// EventFramework and SequentialEvent). Dungeons run on the EventFramework /
    /// Director system, so the current OBJECTIVE marker (the glowing on-screen
    /// waypoint) is expected here - the overworld QuestMarkers only carry the
    /// dungeon ENTRANCE (log 2026-07-23: Sastasha marker sat in map 31, not in
    /// the instance). Logs each marker's position, icon and tooltip plus the
    /// player position so a real dungeon run confirms BOTH the source and the
    /// coordinate system (world vs. map pixels) before anything is built on it.
    /// Runs alongside DumpNearbyObjects on the UI-dump key (Strg+F5).
    /// </summary>
    public unsafe void DumpMapMarkers()
    {
        var agent = AgentMap.Instance();
        if (agent == null)
        {
            _log.Info("[MarkerProbe] AgentMap.Instance() ist null.");
            return;
        }

        var terr   = _clientState.TerritoryType;
        var player = _objectTable.LocalPlayer;
        var ppos   = player != null ? player.Position : default;
        _log.Info($"[MarkerProbe] === terr={terr} Spieler=({ppos.X:F1}|{ppos.Y:F1}|{ppos.Z:F1}) ===");

        long eventCount = 0;
        var markers = agent->EventMarkers;
        for (var i = 0L; i < markers.LongCount; i++)
        {
            var m  = markers[i];
            var tt = m.TooltipString != null ? m.TooltipString->ToString() : string.Empty;
            _log.Info($"[MarkerProbe] Event[{i}] pos=({m.Position.X:F1}|{m.Position.Y:F1}|{m.Position.Z:F1}) " +
                      $"icon={m.IconId} r={m.Radius:F1} tt='{tt}'");
            eventCount++;
        }
        _log.Info($"[MarkerProbe] EventMarkers gesamt: {eventCount}");

        // MiniMapMarkers: the minimap icons. The dungeon OBJECTIVE (glowing
        // waypoint) is expected here. Position is MAP PIXELS (short X/Y), not
        // world coords - convert later like PlacesService does. Subtext often
        // carries the label. DataType/DataKey identify the marker source.
        var mini      = agent->MiniMapMarkers;
        var miniCount = agent->MiniMapMarkerCount;
        _log.Info($"[MarkerProbe] MiniMapMarkers: Count={miniCount} (Span {mini.Length})");
        for (var i = 0; i < mini.Length; i++)
        {
            var m   = mini[i];
            if (m.MapMarker.IconId == 0 && m.DataType == 0) continue; // empty slot
            var sub = m.MapMarker.Subtext.ToString();
            _log.Info($"[MarkerProbe] Mini[{i}] type={m.DataType} key={m.DataKey} " +
                      $"icon={m.MapMarker.IconId} X={m.MapMarker.X} Y={m.MapMarker.Y} sub='{sub}'");
        }

        // Waypoint category (PlacesService, static Lumina MapMarker sheet of the
        // current map). Log each entry with its resolved WORLD position so a
        // dungeon run shows whether these are useful in-instance points or the
        // overworld markers of the underlying map (which would sit outside the
        // dungeon mesh and explain why they cannot be walked to).
        var places = _places.GetPlaces();
        _log.Info($"[MarkerProbe] Wegpunkte (PlacesService): {places.Count}");
        foreach (var p in places)
            _log.Info($"[MarkerProbe] Ort '{p.Name}' ({p.TypeLabel}) " +
                      $"welt=({p.Position.X:F1}|{p.Position.Z:F1})");

        _tolk.SpeakInterrupt($"Marker-Sonde: {eventCount} Event, {miniCount} Minimap, {places.Count} Orte im Log.");
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
            return ObjectNameService.IsSpeakable(name) ? name : AccessibilityStrings.GatheringNodeFallback;

        return AccessibilityStrings.GatheringNodeDesc(info.Value.Type, info.Value.Level);
    }

    // Per node, the last availability the game reported. Only CHANGES are logged,
    // so the probe below stays quiet while the answer stays the same.
    private readonly Dictionary<ulong, bool> _gatherAvailability = [];

    /// <summary>
    /// Logs a line whenever the game changes its mind about whether a gathering
    /// node can be worked - and stays silent while the answer holds.
    ///
    /// This watches the ONE thing the filter in <see cref="IsWorthBrowsing"/> rests
    /// on but that is not yet measured. Measured on 2026-08-09: a live node at 9 m
    /// was targetable while fifteen dead placements were not, two of them read
    /// from 2 m - so distance is not what makes a node untargetable. NOT measured
    /// is the other direction: whether a LIVE node already reads as targetable
    /// from 60-80 m. If it does not, the filter hides usable nodes from the search
    /// instead of only hiding empty ground, and this probe is how that shows: a
    /// node flipping to targetable as the player walks up leaves BOTH distances in
    /// the log, and the flip distance is the answer.
    /// </summary>
    private unsafe void TrackGatheringAvailability(List<IGameObject> inRange, IGameObject player)
    {
#if DEBUG
        foreach (var o in inRange)
        {
            if (o.ObjectKind != ObjectKind.GatheringPoint || o.Address == nint.Zero) continue;

            var usable = o.IsTargetable;
            if (_gatherAvailability.TryGetValue(o.GameObjectId, out var previous) && previous == usable)
                continue;
            _gatherAvailability[o.GameObjectId] = usable;

            var go = (CSGameObject*)o.Address;
            _log.Info($"[GatherProbe] Wechsel: id={o.GameObjectId:X} BaseId={o.BaseId} " +
                      $"nutzbar={usable} TargetableStatus={go->TargetableStatus} " +
                      $"RenderFlags={go->RenderFlags} " +
                      $"Entfernung={Vector3.Distance(player.Position, o.Position):F1}");
        }
#endif
    }

    /// <summary>
    /// Debug probe for the user report of 2026-08-09: "es gibt welche wo ich
    /// abbauen kann und wo nicht" - the browser offers gathering nodes the game
    /// will not let the player work.
    ///
    /// The browser lists EVERY node in range on purpose: <see cref="IsWorthBrowsing"/>
    /// passes ObjectKind.GatheringPoint through unconditionally, without ever
    /// asking whether this placement is currently usable. Which state separates a
    /// workable node from a dead one is NOT answerable from the sheets - it is
    /// runtime state - so every per-object field the game keeps is logged and one
    /// real walk settles it:
    ///  - IsTargetable / TargetableStatus (ObjectTargetableFlags, ilspycmd-verified
    ///    2026-08-09: bit IsTargetable = 2) - the game's own "can you address this".
    ///  - RenderFlags (VisibilityFlags: Model = 2) and Visibility - whether the
    ///    node is drawn at all.
    ///  - EventState - the byte the game sets via GameObject.SetEventState.
    ///  - The raw object name: a live node was called 'Nutzbaum' in the log of
    ///    12:40, while the browser entries carried no name of their own.
    /// No announcement is derived from any of this yet - guessing which field
    /// means "unusable" and hiding nodes on that guess could hide workable ones.
    /// </summary>
    private unsafe void LogGatheringNodeState(IGameObject obj, float distance)
    {
#if DEBUG
        if (obj.ObjectKind != ObjectKind.GatheringPoint || obj.Address == nint.Zero) return;

        var go = (CSGameObject*)obj.Address;
        _log.Info($"[GatherProbe] id={obj.GameObjectId:X} BaseId={obj.BaseId} " +
                  $"Name='{obj.Name.TextValue}' Anvisierbar={obj.IsTargetable} " +
                  $"TargetableStatus={go->TargetableStatus} RenderFlags={go->RenderFlags} " +
                  $"Visibility={go->Visibility} EventState={go->EventState} " +
                  $"Entfernung={distance:F1}");
#endif
    }

    /// <summary>
    /// "&lt;role&gt;, &lt;name&gt;, &lt;kind&gt;" for an object - the identifying part of
    /// every announcement, without distance or direction.
    ///
    /// ONE method for browser and target-change announcement on purpose: the two
    /// used to build this text separately and disagreed about the same object -
    /// the browser said "Erzader, Stufe 20" where the target announcement said
    /// "Unbenannt" (user report 2026-08-08).
    ///
    /// Two cases drop the kind word instead of appending it:
    ///  - gathering nodes, where the type IS the description and ", Sammelpunkt"
    ///    would only repeat it,
    ///  - nameless objects, where the stand-in already names the kind and the
    ///    result would read "Objekt ohne Namen, Objekt".
    /// </summary>
    /// <remarks>
    /// The ordinal goes on the NAME ("Truhe 2, Schatz"), the visited mark at the
    /// very end ("..., Schatz, schon besucht"). Both are asked for the label the
    /// player actually hears, so numbering separates exactly what sounds alike -
    /// two props called "Zielort für Narben im Wald" are one group, while the
    /// same "Zielort" serving another quest is not.
    /// </remarks>
    private string DescribeObject(IGameObject obj)
    {
        if (obj.ObjectKind == ObjectKind.GatheringPoint)
        {
            var node = DescribeGatheringPoint(obj);
            return node + _memory.NumberSuffix(obj, node) + _memory.VisitedSuffix(obj);
        }

        // Eine Gewoelbe-Truhe nennt ihre FARBE, wo das Spiel eine gibt - im spieleigenen
        // Wort dafuer ("Silberne Schatztruhe", Addon 10421). Die Farbe kommt aus der
        // Daten-Id des Objekts selbst und NICHT daraus, es mit dem Truhen-Array des
        // Directors zu paaren: dieses Array enthaelt nur die entdeckten und noch nicht
        // geoeffneten Truhen, "jeder Eintrag ist silbern" hiess also nie "jede Truhe
        // hier ist silbern". Eine Truhe, deren Farbe nicht belegt ist, behaelt die
        // schlichte "Schatztruhe" des Spiels - es geht nichts verloren und es wird
        // nichts behauptet. Siehe DeepDungeonNav.
        var name = (DeepDungeon != null && DeepDungeon.IsActive && DeepDungeon.IsCoffer(obj)
                        ? DeepDungeon.ColourOf(obj)
                        : null)
                   ?? _objectNames.Resolve(obj);
        var label = name == null
            ? $"{NpcPrefix(obj)}{AccessibilityStrings.UnnamedOfKind(obj.ObjectKind)}"
            : $"{NpcPrefix(obj)}{name}{_objectNames.Qualifier(obj)}";

        // The kind word stays omitted for nameless objects - the stand-in names
        // the kind already, and "Objekt ohne Namen 2, Objekt" reads twice.
        var kindWord = name == null ? string.Empty : $", {DescribeKind(obj.ObjectKind)}";

        return label + _memory.NumberSuffix(obj, label) + kindWord + _memory.VisitedSuffix(obj);
    }

    private List<IGameObject> GetCategoryObjects()
    {
        var kinds = Categories[_categoryIndex].Kinds;
        if (kinds == null) return new List<IGameObject>();

        var objects = GetObjectsOfKinds(kinds);

        if (IsMerchantCategory)
        {
            // The link NPC -> shop belongs to the game (ENpcBase.ENpcData); we
            // only ask it. Every hit is logged with the id and the sheet name so
            // a walk through a market district shows whether the list is right.
            var merchants = new List<IGameObject>();
            var trace = new List<(string, uint, ShopKind)>();
            foreach (var o in objects)
            {
                var kind = _shops.KindOf(o.BaseId);
                if (kind == ShopKind.None) continue;
                merchants.Add(o);
                trace.Add((o.Name.TextValue, o.BaseId, kind));
            }
            _shops.LogMerchants(trace, objects.Count);
            return merchants;
        }

        // Freund/Feind. Bis hierher ist "Gegner" schlicht ObjectKind.BattleNpc, und
        // das ist auch der Trust-Trupp, der Karfunkel und das Begleitchocobo (User im
        // Dungeon: *"everything in combat drops into the enemies category"*).
        // CombatSide entscheidet das ausschliesslich an spieleigenen Feldern und nur
        // in EINE Richtung: es nimmt etwas aus Gegner heraus, wenn das Spiel es selbst
        // als Begleiter oder Gruppenmitglied fuehrt. Ein Mob, ueber den das Spiel
        // nichts sagt, bleibt Gegner - ein nicht gepullter Mob kann also nicht aus der
        // Liste fallen.
        var cat = Categories[_categoryIndex].Cat;
        if (cat == NavCategory.Enemies)
            return objects.Where(CombatSide.IsEnemy).ToList();
        if (cat == NavCategory.Allies)
            return objects.Where(CombatSide.IsAlly).ToList();

        // Inhalte: dieselbe Form wie der Haendler-Block oben. Die Verbindung Objekt ->
        // Inhalt gehoert dem Spiel (EObj.Data -> InstanceContentGuide ->
        // ContentFinderCondition); wir fragen sie nur ab. Ein Objekt, dessen Daten sich
        // nicht aufloesen lassen, steht schlicht nicht in dieser Liste und behaelt
        // seinen Platz unter Objekte - es kann also nichts verloren gehen.
        if (cat == NavCategory.Duties)
            return objects.Where(o => DungeonSide.Describe(o, _data, _log) != null).ToList();

        // Die beiden Objekt-Kategorien des Tiefen Gewoelbes. Beide ordnen nach den
        // spieleigenen WORTEN fuer die Dinge ein (Addon 10113 fuer eine Truhe,
        // 10418/10419 fuer die beiden Leuchten), gelesen aus den Sheets in der Sprache
        // des Clients - siehe DeepDungeonNav. Jede hier verworfene Truhe wird mit ihrer
        // Daten-Id protokolliert, denn "ist ueberhaupt je eine bronzene erschienen?"
        // ist eine Frage, die das Log beantworten koennen muss.
        if (cat == NavCategory.DeepTreasure && DeepDungeon != null)
        {
            var coffers = objects.Where(DeepDungeon.IsCoffer).ToList();
            DeepDungeon.LogCofferPass(objects, coffers);
            return coffers;
        }
        if (cat == NavCategory.DeepCairns && DeepDungeon != null)
            return objects.Where(DeepDungeon.IsCairn).ToList();

        if (!IsQuestOnlyCategory) return objects;

        // Quest-only category: two independent links, both owned by the game.
        //  (1) MARKER: what the quest markers point at, resolved over the Level
        //      sheet to the object's data-sheet id (see GetQuestObjectIds).
        //      Covers active objectives, including enemies without a nameplate.
        //  (2) NAMEPLATE: the icon the game draws above the object's head - the
        //      very "!" a sighted player sees. Covers acceptable quests whose
        //      giver has no marker of its own in this zone.
        // Neither is a heuristic; an object counts when at least one says yes.
        var questIds = _questMarkers.GetQuestObjectIds();
        var byMarker = 0;
        var byIcon   = 0;
        var iconTrace = new List<string>();

        var filtered = new List<IGameObject>();
        foreach (var o in objects)
        {
            var fromMarker = questIds.Contains(o.BaseId);
            var icon       = NamePlateIcon(o);
            var fromIcon   = !string.IsNullOrEmpty(AccessibilityStrings.QuestMarkerHint(icon));

            if (icon != 0) iconTrace.Add($"{o.Name.TextValue}={icon}");
            if (fromMarker) byMarker++;
            if (fromIcon) byIcon++;
            if (fromMarker || fromIcon) filtered.Add(o);
        }

        // The icon list is what refines QuestMarkerHint's ranges from real data -
        // only non-zero icons, so this stays short.
        _log.Info($"[Nav] {CurrentCategoryLabel}: {filtered.Count} von {objects.Count} Objekten " +
                  $"(per Marker {byMarker}, per Symbol {byIcon}; {questIds.Count} Ids aus Markern). " +
                  $"Symbole: {(iconTrace.Count > 0 ? string.Join(", ", iconTrace) : "keine")}");
        return filtered;
    }

    /// <summary>
    /// The icon the game draws above an object's head, 0 when it draws none.
    /// This is exactly what a sighted player sees, so it is READ, never derived
    /// from quest state. GameObject.NamePlateIconId @272 (ilspycmd-verified
    /// 2026-08-02).
    /// </summary>
    private unsafe uint NamePlateIcon(IGameObject obj)
        => obj.Address == 0 ? 0u : ((CSGameObject*)obj.Address)->NamePlateIconId;

    /// <summary>Whether the current category shows only quest-related objects.</summary>
    private bool IsQuestOnlyCategory => Categories[_categoryIndex].Cat
        is NavCategory.QuestNpcs or NavCategory.QuestObjects or NavCategory.QuestEnemies;

    /// <summary>Whether the current category shows only shop keepers.</summary>
    private bool IsMerchantCategory => Categories[_categoryIndex].Cat == NavCategory.Merchants;

    /// <summary>Whether the current category shows only duty entrances.</summary>
    private bool IsDutyCategory => Categories[_categoryIndex].Cat == NavCategory.Duties;

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

    // Netzende-Erkennung (siehe Konstanten unten).
    private Vector3 _guideLastPosition;
    private DateTime _guideLastMoveAt;
    private float _guideBestDistance;
    private DateTime _guideLastApproachAt;
    private bool _guideMeshEndAnnounced;

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

    // ── Netzende erkennen (V5.81, aus dem Auto-Lauf übernommen) ──
    // vnavmesh hängt die angeforderte Zielkoordinate JEDEM Ergebnis an, auch
    // einer unerreichbaren (Fakt 3 im Kopf von AutoWalkService). Hängt der
    // Führungspunkt an genau diesem Punkt, ist der begehbare Weg zu Ende, und
    // die Gehhilfe hat den Spieler bisher trotzdem dorthin geschickt
    // (Log 2026-08-10 19:32:36-19:32:56: 30 s "0,5 Kilometer, geradeaus" bei
    // unveränderten 469,5 m).

    /// <summary>Keine Annäherung so lange trotz Bewegung: das Netz endet hier.
    /// Großzügiger als die 2,5 s des Auto-Laufs, weil ein Mensch sich beim
    /// Laufen dreht, ausweicht und tastet - der Auto-Lauf hält stur Kurs.</summary>
    private const double GuideNoApproachS = 5;

    /// <summary>So viel näher zählt als Annäherung (darunter ist es Rauschen).</summary>
    private const float GuideApproachEpsilon = 1f;

    /// <summary>Nur so weit außerhalb der Ankunftsreichweite urteilen: auf den
    /// letzten Metern ist eine Weile ohne Annäherung normal (um ein Hindernis
    /// herum), und dort führt die Luftlinie ohnehin richtig.</summary>
    private const float GuideNoApproachMinDistance = 20f;

    /// <summary>Zählt als echte Bewegung (darunter Zittern an der Geometrie).</summary>
    private const float GuideMovementEpsilon = 0.5f;

    /// <summary>Nur urteilen, solange der Spieler gerade wirklich läuft.
    /// Stillstand beweist nichts: er kann kämpfen oder im Menü sein.</summary>
    private const double GuideMovingWindowS = 1.0;

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
            _tolk.SpeakInterrupt(AccessibilityStrings.WalkGuideOff);
            _log.Info("[Nav] Gehhilfe: manuell ausgeschaltet.");
            return;
        }

        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTargetSelectN);
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
        ResetApproachTracking(_objectTable.LocalPlayer?.Position ?? destination, destination);
        _guideMeshEndAnnounced = false;
        _beacon.Start();
        _tolk.SpeakInterrupt(AccessibilityStrings.WalkGuideOn(_walkTargetName));
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

        // Tolerance = the range the guide itself calls "arrived". Guide targets
        // are map markers and object positions, which routinely sit a little off
        // the walkable surface; without this the query insists on the exact
        // point and returns nothing, and the guide drops to straight-line mode
        // for a destination it could perfectly well have routed to.
        var task = _routes.RequestPath(from, _walkDestPosition, _walkArrivalRange);
        if (task == null)
        {
            if (!isReroute)
            {
                _tolk.Speak(AccessibilityStrings.NoNavmeshStraightLine);
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
                _tolk.Speak(AccessibilityStrings.ComputingRoute);
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
                _tolk.Speak(AccessibilityStrings.NoPathStraightLine(_places.BuildNoPathHint(_walkDestPosition)));
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
        // A fresh route deserves a fresh verdict: judging approach against the
        // old route's best distance would condemn a detour that starts by
        // walking away from the destination.
        ResetApproachTracking(player.Position, _walkDestPosition);

        if (!_routeTaskIsReroute)
        {
            // A "route" that is nothing but the appended destination is not a
            // route at all (see the mesh-end constants above). Announcing it
            // produced "Weg zu Infame Informanten, 466 Meter: 466 Meter nach
            // Süden" for a corridor that does not exist (log 2026-08-10
            // 19:32:31). Stay QUIET rather than claim "no path": on open ground
            // a genuine straight run looks exactly the same from here, and
            // CheckMeshEnd tells the truth as soon as the player actually walks.
            if (RouteIsOnlyAppendedDestination(Vector3.Distance(player.Position, _walkDestPosition)))
                _log.Info("[Nav] Gehhilfe: Route besteht nur aus dem angehängten Ziel - keine Routen-Vorschau.");
            else
                _tolk.Speak(_routes.DescribeRoute(_walkTargetName, waypoints));
        }
        else if (_route != null && _routeCursor < _route.Count)
        {
            // Guide rule: after a quiet re-route speak one line ONLY when the
            // immediate direction actually changed.
            var newDirection = DirectionText(RelativeAngle(player, _route[_routeCursor]));
            if (newDirection != previousDirection)
                _tolk.SpeakInterrupt(AccessibilityStrings.NewRoute(newDirection));
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
                _tolk.SpeakInterrupt(AccessibilityStrings.WalkTargetLost);
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
            _tolk.SpeakInterrupt(AccessibilityStrings.TargetReached(_walkTargetName));
            _log.Info($"[Nav] Gehhilfe: Ziel erreicht, dist={distance:F1}");
            return;
        }

        var now = DateTime.UtcNow;
        TrackApproach(player.Position, distance, now);

        PollRouteTask(player);

        if (_route != null)
        {
            AdvancePastReachedWaypoints(player, announce: true);
            CheckReroute(player);
            CheckMeshEnd(player, distance, now);
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
        // direction continuously in the frames between. After the mesh ended we
        // keep the slower route cadence: the straight-line rhythm exists for
        // short final approaches, and at 469 m remaining it would repeat the
        // same line every 2 s with nothing new to say.
        var interval = _route != null || _guideMeshEndAnnounced ? RouteSpeakIntervalS : StraightSpeakIntervalS;
        if ((DateTime.UtcNow - _lastGuideTick).TotalSeconds < interval) return;
        _lastGuideTick = DateTime.UtcNow;

        _tolk.SpeakInterrupt($"{FormatDistance(guideDist)}, {DirectionText(relAngle)}{VerticalHint(player, guidePoint)}.");
        _log.Info($"[Nav] Gehhilfe: dist={guideDist:F1} zielDist={distance:F1} relAngle={relAngle:F0} " +
                  $"wp={_routeCursor}/{_route?.Count ?? 0} rot={player.Rotation:F2}");
    }

    /// <summary>
    /// Turns the player to where the walk guide is steering, once per press.
    /// Built for MANUAL walking: the guide says "24 Meter, leicht rechts", but
    /// finding that heading by ear costs time at every corner.
    /// <para>
    /// BOTH the character rotation and the camera direction are set, because
    /// which of the two steers walking depends on the movement mode - standard
    /// walks where the CAMERA looks, legacy where the CHARACTER looks - and the
    /// structs do not say which is active. The mode is logged so the first press
    /// settles it and the loser can be dropped afterwards.
    /// </para>
    /// <para>
    /// The character angle is exact: the blick vector is (sin, cos) in XZ, so the
    /// target rotation is atan2(dx, dz) - the same convention
    /// <see cref="RelativeAngle"/> is built on and that was verified in-game
    /// 2026-07-10. The camera field <c>Camera.DirH</c> (ilspycmd 2026-08-12) is
    /// set to the SAME angle, which ASSUMES both use one convention. That
    /// assumption is not verified yet, which is why the log prints the camera's
    /// own DirH next to the character rotation BEFORE anything is written: one
    /// press with the camera behind the player shows whether the two agree.
    /// </para>
    /// </summary>
    public unsafe void FaceGuideDirection()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var guidePoint = CurrentGuidePoint;
        if (guidePoint == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.FaceNoRoute);
            return;
        }

        var target = guidePoint.Value;
        var dx = target.X - player.Position.X;
        var dz = target.Z - player.Position.Z;
        if (Math.Abs(dx) < 0.01f && Math.Abs(dz) < 0.01f)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.FaceAlreadyThere);
            return;
        }

        var targetRotation = (float)Math.Atan2(dx, dz);
        var distance       = Vector3.Distance(player.Position, target);

        // Movement mode: 0 = standard (camera-relative), 1 = legacy - read from
        // the game's own config rather than assumed.
        var moveMode = _gameConfig.UiControl.TryGetUInt("MoveMode", out var mm) ? (int)mm : -1;

        var camera  = CameraManager.Instance();
        var gameCam = camera != null ? camera->Camera : null;
        var dirHBefore = gameCam != null ? gameCam->DirH : float.NaN;

        _log.Info($"[Face] vorher: rot={player.Rotation:F3} dirH={dirHBefore:F3} " +
                  $"ziel={targetRotation:F3} dist={distance:F1} moveMode={moveMode}");

        ((CSGameObject*)player.Address)->Rotation = targetRotation;
        if (gameCam != null) gameCam->DirH = targetRotation;

        _log.Info($"[Face] nachher: rot={((CSGameObject*)player.Address)->Rotation:F3} " +
                  $"dirH={(gameCam != null ? gameCam->DirH : float.NaN):F3}");

        _tolk.SpeakInterrupt(AccessibilityStrings.FaceAligned(FormatDistance(distance)));
    }

    /// <summary>
    /// Where the walk guide is currently steering - the active waypoint while a
    /// route exists, the destination itself otherwise. Null when no guide runs.
    /// </summary>
    private Vector3? CurrentGuidePoint
    {
        get
        {
            if (!_walkGuideActive) return null;
            return _route != null && _routeCursor < _route.Count
                ? _route[_routeCursor]
                : _walkDestPosition;
        }
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
            // (FormatDistance/DirectionText/VerticalHint are all language-aware.)
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

    /// <summary>Starts the movement/approach bookkeeping over (guide start, new route).</summary>
    private void ResetApproachTracking(Vector3 playerPosition, Vector3 destination)
    {
        _guideLastPosition = playerPosition;
        _guideLastMoveAt = DateTime.UtcNow;
        _guideBestDistance = Vector3.Distance(playerPosition, destination);
        _guideLastApproachAt = DateTime.UtcNow;
    }

    /// <summary>Records whether the player is walking at all, and whether the
    /// destination is getting closer. Both are needed to tell "the mesh ends
    /// here" apart from "the player is standing still".</summary>
    private void TrackApproach(Vector3 playerPosition, float distance, DateTime now)
    {
        if (Vector3.Distance(playerPosition, _guideLastPosition) >= GuideMovementEpsilon)
        {
            _guideLastPosition = playerPosition;
            _guideLastMoveAt = now;
        }

        if (distance <= _guideBestDistance - GuideApproachEpsilon)
        {
            _guideBestDistance = distance;
            _guideLastApproachAt = now;
        }
    }

    /// <summary>
    /// True when the only waypoint left is the destination vnavmesh appends to
    /// every path whether it is reachable or not - i.e. the walkable corridor is
    /// used up and what remains is a wish. Restricted to distant destinations:
    /// close by, a one-waypoint route is simply the last hop.
    /// </summary>
    private bool RouteIsOnlyAppendedDestination(float distance)
    {
        var route = _route;
        if (route == null || route.Count == 0) return false;
        if (_routeCursor < route.Count - 1) return false;       // real waypoints still ahead
        if (distance <= _walkArrivalRange + GuideNoApproachMinDistance) return false;
        // Guards against a target that walked off since the pathfind: then the
        // last waypoint is the OLD destination, not the appended current one.
        return Vector3.Distance(route[^1], _walkDestPosition) <= _walkArrivalRange;
    }

    /// <summary>
    /// The mesh ends here: nothing is left but the appended destination, the
    /// player IS walking, and they are getting no closer. The auto-walk stops at
    /// this point because it steers the character; the guide does not steer, so
    /// stopping would only take the guidance away from someone who may well find
    /// their own way down. It says so once and keeps pointing straight-line.
    /// </summary>
    private void CheckMeshEnd(IGameObject player, float distance, DateTime now)
    {
        if (_guideMeshEndAnnounced) return;
        if (!RouteIsOnlyAppendedDestination(distance)) return;
        // Standing still proves nothing - the player may be fighting or reading
        // a menu. Only someone actively walking who still gets no closer does.
        if ((now - _guideLastMoveAt).TotalSeconds > GuideMovingWindowS) return;
        if ((now - _guideLastApproachAt).TotalSeconds <= GuideNoApproachS) return;

        _guideMeshEndAnnounced = true;
        _route = null;   // straight-line guidance from here on
        var direction = RouteService.CompassWord(player.Position, _walkDestPosition);
        _log.Info($"[Nav] Gehhilfe: keine Annäherung seit {GuideNoApproachS:F1} s am angehängten Ziel, " +
                  $"dist={distance:F1} - Netz endet hier, weiter in Luftlinie.");
        _tolk.SpeakInterrupt(AccessibilityStrings.GuideMeshEndsHere(distance, direction));
        _lastGuideTick = now;
    }

    /// <summary>", aufwärts"/", abwärts" when the guide point sits clearly above
    /// or below the player (stairs, ramps) - plain Y arithmetic on route data.</summary>
    private static string VerticalHint(IGameObject player, Vector3 point)
    {
        var dy = point.Y - player.Position.Y;
        return dy > 1.5f ? AccessibilityStrings.VerticalUp : dy < -1.5f ? AccessibilityStrings.VerticalDown : string.Empty;
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

        // Same tolerance as the walk guide, for the same reason: the preview
        // should describe the way there even when the marker itself is a metre
        // off the mesh. It only ever speaks a route - nothing walks on this.
        var task = _routes.RequestPath(player.Position, position, ArrivalDistance);
        if (task == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoNavmeshPlugin);
            return;
        }
        _previewTask = task;
        _previewName = name;
        _previewDest = position;
        _tolk.SpeakInterrupt(AccessibilityStrings.ComputingRouteTo(name));
    }

    /// <summary>Route preview to the current game target.</summary>
    public void PreviewRouteToTarget()
    {
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTargetSelectN);
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NoPathTo(_previewName, _places.BuildNoPathHint(_previewDest)));
            return;
        }
        _tolk.SpeakInterrupt(_routes.DescribeRoute(_previewName, waypoints));
    }

    /// <summary>", Stufe N, HP X Prozent" fuer Ziele mit Lebenspunkten, sonst "".
    /// Wird sowohl beim Anvisieren ueber die Spieltaste als auch beim Blaettern im
    /// Objekt-Browser angehaengt - vorher kannte nur der erste Weg die HP.</summary>
    private static string DescribeTargetHp(IGameObject target)
    {
        if (target is IBattleChara bc && bc.MaxHp > 0)
            return AccessibilityStrings.TargetLevelHpFragment(bc.Level, bc.CurrentHp, bc.MaxHp);
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

        var iconId = NamePlateIcon(obj);
        var quest  = DescribeQuestMarker(iconId);
        if (!string.IsNullOrEmpty(quest))
        {
            parts.Add(quest);
            _log.Info($"[Nav] NPC {obj.Name.TextValue}: NamePlateIconId={iconId} -> '{quest}'");
        }

        return parts.Count > 0 ? string.Join(", ", parts) + ", " : string.Empty;
    }

    /// <summary>
    /// Maps a nameplate icon id to a quest hint. Ranges are the standard FFXIV
    /// quest markers; the exact id is logged so the mapping can be refined from
    /// real data (available "!" vs. active vs. ready-to-turn-in).
    /// </summary>
    private static string DescribeQuestMarker(uint iconId) => AccessibilityStrings.QuestMarkerHint(iconId);

    private static string DescribeKind(ObjectKind kind) => AccessibilityStrings.ObjectKindName(kind);

    // Ziel per Name setzen (NPC oder Spielername)
    public bool SetTarget(string name)
    {
        var obj = _objectTable
            .FirstOrDefault(o => o.Name.TextValue.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (obj == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.TargetNotFound(name));
            return false;
        }

        _trackedObject = obj;
        _trackedName = obj.Name.TextValue;
        _tolk.SpeakInterrupt(AccessibilityStrings.Tracking(_trackedName));
        return true;
    }

    // Aktuell anvisiertes Spielziel übernehmen
    public void SetTargetFromGameTarget()
    {
        var target = _targetManager.Target ?? _targetManager.SoftTarget;
        if (target == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoGameTarget);
            return;
        }

        _trackedObject = target;
        _trackedName = target.Name.TextValue;
        _tolk.SpeakInterrupt(AccessibilityStrings.Tracking(_trackedName));
    }

    public void ClearTarget()
    {
        _trackedObject = null;
        _trackedName = null;
        _tolk.SpeakInterrupt(AccessibilityStrings.TrackingStopped);
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTargetTracked);
            return;
        }

        var playerPos = player.Position;
        var targetPos = _trackedObject.Position;
        var distance = Vector3.Distance(playerPos, targetPos);

        var direction = CalculateDirection(player, targetPos);
        var distanceText = FormatDistance(distance);

        // _trackedName is set in lockstep with _trackedObject (checked non-null above).
        _tolk.SpeakInterrupt(AccessibilityStrings.TargetDirection(_trackedName!, distanceText, direction));
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
            _tolk.SpeakInterrupt(AccessibilityStrings.NoNearbyObjects);
            return;
        }

        var parts = nearby.Select(o =>
        {
            var dist = Vector3.Distance(player.Position, o.Position);
            return $"{o.Name.TextValue} {FormatDistance(dist)}";
        });

        _tolk.SpeakInterrupt(AccessibilityStrings.NearbyList(string.Join(", ", parts)));
    }

    /// <summary>
    /// Direction word from the player's heading to <paramref name="targetPos"/>.
    /// <paramref name="caller"/> is filled in by the compiler and only used by
    /// the debug probe, so every direction announcement (object browser, quest
    /// goals, target change, /acc nav) can be traced back to its source in the
    /// log without a game target being required.
    /// </summary>
    private string CalculateDirection(IGameObject player, Vector3 targetPos,
                                      [CallerMemberName] string caller = "")
    {
        var angle = RelativeAngle(player, targetPos);
        var word = DirectionText(angle);
#if DEBUG
        var dx = targetPos.X - player.Position.X;
        var dz = targetPos.Z - player.Position.Z;
        _log.Info($"[NavDirProbe] {caller}: rot={player.Rotation:F3} dx={dx:F2} dz={dz:F2} " +
                  $"angle={angle:F1} wort='{word}'");
#endif
        return word;
    }

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

    private static string DirectionText(double relativeAngle) =>
        AccessibilityStrings.RelativeDirection(relativeAngle);

    private static string FormatDistance(float distance) =>
        AccessibilityStrings.FormatDistance(distance);
}
