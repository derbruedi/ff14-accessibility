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
    // Dungeonliste: JEDER Eingang zu Dungeon, Pruefung oder Raid im Spiel, nach
    // Stufe sortiert - nicht nur die Tuer in Sichtweite (das ist Duties). Quelle
    // sind die Sheets, deshalb steht hier auch das Ziel drei Zonen weiter, und
    // Numpad3 fuehrt wie bei den Quests ueber die Zonenuebergaenge dorthin. Siehe
    // DutyEntranceService.
    WorldDuties,
    // Dungeon: die Stationen des Wegs durch die Instanz, in Laufreihenfolge.
    // Einzige Kategorie, die NICHT nach Entfernung sortiert - die Reihenfolge ist
    // ihr ganzer Zweck. Quelle sind Pfaddateien im Konfigurationsordner, siehe
    // DungeonRouteService.
    DungeonRoute,
    Aetherytes,
    QuestGoals,
    AcceptableQuests,
    Levequests,
    Waypoints,
    // [Tiefes Gewoelbe] Nur INNERHALB eines Tiefen Gewoelbes angeboten, wo sie den Weltsatz
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
    // Wohin man aus einer Gefahrenflaeche heraus muss - hat Vorrang auf dem
    // Peil-Ton (siehe TryDriveEscapeBeacon).
    private readonly EscapeRouteService _escape;
    private readonly CueService _cue;
    private readonly QuestMarkerService _questMarkers;
    private readonly PlacesService _places;
    private readonly FishingService _fishing;
    private readonly FateService _fates;
    private readonly RouteService _routes;
    private readonly ShopNpcService _shops;
    private readonly HuntingLogService _huntingLog;
    private readonly DutyEntranceService _dutyEntrances;
    private readonly DungeonRouteService _dungeonRoute;
    private readonly LevequestEnemyService _leveEnemies;
    private readonly ObjectNameService _objectNames;
    private readonly ObjectMemoryService _memory;
    private readonly EnemyMarkerService _enemyMarkers;
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
        EscapeRouteService escape,
        CueService cue,
        QuestMarkerService questMarkers,
        PlacesService places,
        FishingService fishing,
        FateService fates,
        RouteService routes,
        ShopNpcService shops,
        HuntingLogService huntingLog,
        DutyEntranceService dutyEntrances,
        DungeonRouteService dungeonRoute,
        LevequestEnemyService leveEnemies,
        ObjectNameService objectNames,
        ObjectMemoryService memory,
        EnemyMarkerService enemyMarkers,
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
        _escape = escape;
        _cue = cue;
        _questMarkers = questMarkers;
        _places = places;
        _fishing = fishing;
        _fates = fates;
        _routes = routes;
        _shops = shops;
        _huntingLog = huntingLog;
        _dutyEntrances = dutyEntrances;
        _dungeonRoute = dungeonRoute;
        _leveEnemies = leveEnemies;
        _objectNames = objectNames;
        _memory = memory;
        _enemyMarkers = enemyMarkers;
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
            // Zonenwechsel oder Logout: es gibt keine Richtung mehr, auf die der
            // Ton zeigen koennte. Ohne das laeuft er mit dem letzten Stand weiter,
            // waehrend der Ladebildschirm liegt. Stop statt Idle - auf einem
            // Ladebildschirm hat auch das Geraet nichts offen zu haben (siehe
            // UpdateTargetBeacon).
            if (_beacon.IsRunning) _beacon.Stop();
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

        // [Tiefes Gewoelbe] Das Betreten oder Verlassen tauscht den gesamten
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
                    || SelectedObjectDestination != null || SelectedHuntTarget != null
                    || SelectedDutyEntrance != null))
            {
                _log.Info($"[Nav] Spiel-Ziel {hardTargetId:X} anvisiert - verwerfe Browser-Markerauswahl, Numpad3 läuft zum Ziel.");
                SelectedQuestDestination = null;
                SelectedPlaceDestination = null;
                SelectedObjectDestination = null;
                SelectedHuntTarget = null;
                SelectedDutyEntrance = null;
                SelectedDungeonStep = null;
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
                // Die Farbe steht VOR dem Namen, wie in Sku: sie ist das, was
                // zwei gleichnamige Gegner ueberhaupt erst auseinanderhaelt, und
                // sie muss deshalb gehoert werden, bevor der Name kommt. Wer
                // keine traegt (kein Gegner, nicht im Kampf), bekommt hier eine
                // leere Zeichenkette und die Ansage klingt wie bisher.
                var text = $"{AccessibilityStrings.TargetPrefix}{_enemyMarkers.SpokenPrefix(target)}" +
                           $"{DescribeObject(target)}, " +
                           $"{FormatDistance(distance)}, {CalculateDirection(player, target.Position)}" +
                           $"{DescribeTargetHp(target)}{DescribeTamed(target)}.";
                _log.Info($"[Nav] Zielwechsel: {text} (id={target.GameObjectId:X}, kind={target.ObjectKind})");
                _tolk.SpeakInterrupt(text);
            }
        }

        // Die Flucht aus einer Gefahrenflaeche hat Vorrang vor allem anderen -
        // vor der Gehhilfe und vor dem anvisierten Ziel. Wer in einer Flaeche
        // steht, hat genau eine Aufgabe, und der Ton, der ihn zu einem Erzbrocken
        // fuehrt, hilft ihm dabei nicht.
        //
        // Vorrang statt zweitem Ton: es gibt nur EIN Peil-Signal, zwei
        // gleichzeitig waeren nicht auseinanderzuhalten. Der Warnton
        // (AoeWarningService) laeuft daneben weiter - der ist ein monaurales
        // Brummen und deshalb bewusst nicht mit dem Peil-Ton zu verwechseln.
        if (TryDriveEscapeBeacon(player))
        {
            BeaconProbe(player, "Flucht");
            return;
        }

        if (_walkGuideActive) WalkGuideFrame(player);
        else UpdateTargetBeacon(player);
        BeaconProbe(player, _walkGuideActive ? "Gehhilfe" : "Ziel");
    }

#if DEBUG
    private string _lastBeaconProbe = string.Empty;
    private DateTime _lastBeaconProbeAt;

    /// <summary>
    /// Schreibt mit, WER den Peil-Ton gerade fuettert und was dabei herauskommt.
    /// Gebaut fuer zwei Meldungen des Users vom 2026-08-23: *"der ton stoppt
    /// nicht wenn man die gehhilfe aus macht"* und *"wenn man sich ausrichtet
    /// passt der ton sich nicht an"*.
    ///
    /// <para>
    /// Die beiden Fragen, die das Log beantworten soll:
    ///   1. STOPPT ER NICHT? Dann zeigt `still=False`, obwohl `quelle=Ziel` und
    ///      `laeuft=False` - also niemand mehr fuettert, aber die Ausgabe steht
    ///      nicht auf stumm. Genau dort sitzt das nicht angekommene `Idle()`.
    ///   2. PASST ER SICH NICHT AN? Dann bleibt `rot` konstant, waehrend sich
    ///      der Spieler dreht - der Beweis, dass die FIGUR stehen bleibt und nur
    ///      die KAMERA sich dreht (`MoveMode` 0). `dirH` daneben zeigt, ob die
    ///      Kamera die Bewegung mitgemacht hat, die die Figur nicht machte.
    /// </para>
    ///
    /// <para>
    /// Entdoppelt: geloggt wird nur, wenn sich die Lage aendert, sonst hoechstens
    /// einmal pro Sekunde. Ein Peil-Ton laeuft ueber Minuten, und ein Eintrag pro
    /// Frame macht das Log unlesbar.
    /// </para>
    /// </summary>
    private void BeaconProbe(IGameObject player, string quelle)
    {
        var dirH = FacingService.CameraFacing();
        var zeile = $"quelle={quelle} still={_beacon.DebugSilent?.ToString() ?? "-"} " +
                    $"eingerastet={_beacon.DebugAligned} offen={_beacon.IsRunning} " +
                    $"schluessel={_beacon.DebugTargetKey:X} gehhilfe={_walkGuideActive} " +
                    $"laeuft={AutoWalk?.IsWalking.ToString() ?? "-"} " +
                    $"rot={player.Rotation:F3} dirH={dirH?.ToString("F3") ?? "-"} " +
                    $"kameraAb={(dirH is { } d ? Normalise180((player.Rotation - d) * (180.0 / Math.PI)) : double.NaN):F1}";

        var now = DateTime.UtcNow;
        if (zeile == _lastBeaconProbe && (now - _lastBeaconProbeAt).TotalSeconds < 1.0) return;
        _lastBeaconProbe = zeile;
        _lastBeaconProbeAt = now;
        _log.Info($"[BeaconProbe] {zeile}");
    }
#else
    private void BeaconProbe(IGameObject player, string quelle) { }
#endif

    /// <summary>
    /// Fuehrt den Peil-Ton auf den sicheren Punkt, solange der Spieler in einer
    /// Gefahrenflaeche steht. True, wenn die Flucht den Ton uebernommen hat -
    /// dann laeuft in diesem Frame nichts anderes mehr auf dem Ton.
    ///
    /// Der Fluchtton haengt bewusst NICHT am Schalter fuer den Ziel-Peil-Ton:
    /// wer die Zielpeilung abschaltet, will keine Erzbrocken angepeilt bekommen -
    /// er hat damit nicht auf die Warnung verzichtet, in welche Richtung er dem
    /// Einschlag entkommt. Sein Schalter ist der der AoE-Warnung, zu der er
    /// gehoert (siehe CombatService).
    ///
    /// Findet die Suche keinen sicheren Punkt, schweigt der Ton, statt
    /// irgendwohin zu zeigen - aber er gibt den Ton auch nicht an die Gehhilfe
    /// zurueck, solange die Gefahr laeuft: eine Wegweisung mitten in einer
    /// Flaeche wuerde als "hier lang ist es sicher" gehoert.
    /// </summary>
    private bool TryDriveEscapeBeacon(IGameObject player)
    {
        if (!_escape.InDanger) return false;

        if (_escape.SafeSpot is not { } spot)
        {
            _beacon.Start();
            _beacon.Idle();
            return true;
        }

        _beacon.Start();
        var distance = Distance2D(player.Position, spot);
        // targetKey konstant: der Fluchtpunkt darf sich verschieben, ohne dass
        // der Ton jedes Mal neu ansetzt. Er wird nur EINMAL neu angesetzt,
        // naemlich wenn die Flucht den Ton uebernimmt - dafuer sorgt der
        // Schluesselwechsel gegenueber dem vorherigen Ziel von selbst.
        _beacon.Update(RelativeAngle(player, spot), distance, BeaconKind.Escape,
                       arrived: false, targetKey: EscapeBeaconKey);
        return true;
    }

    /// <summary>Eigener Schluessel fuer den Fluchtton, damit der Wechsel von und
    /// zu ihm den Ton sauber neu ansetzt (siehe BeaconService.Update).</summary>
    private const ulong EscapeBeaconKey = ulong.MaxValue;

    // ── Peil-Ton auf das anvisierte Ziel ──
    //
    // Wunsch des Users (2026-08-19): der Ton soll je nach Zielart anders klingen,
    // mit der Entfernung leiser werden und verstummen, sobald man richtig
    // ausgerichtet steht (Aufzuege, Plattformen). Die ganze Signallogik sitzt in
    // BeaconService; hier wird nur beantwortet: WORAUF zielt er gerade?
    //
    // ZWEI QUELLEN:
    //   Gehhilfe AN  -> WalkGuideFrame fuettert den Ton. Sie kennt den naechsten
    //                   WEGPUNKT, also die Richtung, in die man wirklich laufen
    //                   muss, und sie kann auch auf eine blosse Position zeigen.
    //   Gehhilfe AUS -> diese Methode, und NUR auf ein anvisiertes Ziel. Eine
    //                   Auswahl im Browser allein loest keinen Ton aus.
    private void UpdateTargetBeacon(IGameObject player)
    {
        if (!_config.TargetBeaconEnabled)
        {
            if (_beacon.IsRunning) _beacon.Stop();
            return;
        }

        // DER TON GEHOERT ZUM LAUFEN, NICHT ZUM ANVISIEREN.
        //
        // Bis v5.89 hing diese Methode an nichts weiter als "es ist etwas
        // anvisiert" - die Vorgabe des Users vom 2026-08-19 ("die toene fuer
        // getrackte sachen sollen auch kommen wenn die gehhilfe aus ist aber dann
        // nur wenn was anvisiert ist"). Im Kampf ist aber IMMER etwas anvisiert,
        // und damit lief der Ton dauerhaft ueber allem. Rueckmeldung aus der
        // Spielerschaft 2026-08-23: *"now its all on every time and you hear it
        // over everything"*. Der User hat die Vorgabe daraufhin gekippt
        // (2026-08-23): der Ton laeuft nur noch, solange wirklich ein Lauf laeuft.
        //
        // Vor 9ec2f24 speiste AUSSCHLIESSLICH die Gehhilfe den Ton (nachgesehen in
        // 9ec2f24^: `if (_walkGuideActive) WalkGuideFrame(player);` ohne
        // else-Zweig). Der Auto-Lauf kommt hier bewusst dazu: auch er ist ein
        // bewusst ausgeloester Vorgang mit Anfang und Ende, also kein
        // Dauerzustand - genau der Unterschied, ueber den sich die Spieler
        // beschwert haben.
        //
        // STOP, NICHT IDLE - und das ist eine Korrektur von heute Vormittag.
        //
        // `Idle()` schaltet nur stumm und LAESST DAS AUDIOGERAET OFFEN. Das war
        // richtig, solange der Ton bei jedem anvisierten Ziel lief: dann kam der
        // naechste Ton im Sekundentakt, und Auf- und Zumachen kostete nur einen
        // hoerbaren Aussetzer. Seit der Ton an einen LAUF gebunden ist, ist das
        // Gegenteil richtig - bis zum naechsten Lauf vergehen Minuten, und ein
        // offener Ausgabestrom schiebt die ganze Zeit Stille durch die Soundkarte.
        //
        // GEMESSEN 2026-08-23: nach dem Ende der Gehhilfe stand die Sonde eine
        // Minute lang auf `still=True offen=True` - stumm, aber offen. Der User
        // meldete waehrenddessen *"er verschwindet nicht beim ausmachen"*. Ein
        // offener Strom rauscht je nach Soundkarte hoerbar; das ist die einzige
        // Erklaerung, die dazu passt, dass die Software stumm ist und trotzdem
        // etwas zu hoeren war.
        //
        // Der Aussetzer beim naechsten Start ist verkraftbar: ein Lauf beginnt
        // ohnehin mit einer gesprochenen Ansage, hinter der das Geraet aufgeht.
        if (AutoWalk is not { IsWalking: true })
        {
            if (_beacon.IsRunning) _beacon.Stop();
            return;
        }

        if (!TryGetBeaconTarget(out var position, out var kind, out var targetKey))
        {
            // Kein Ziel: schweigen, aber das Audiogeraet offen lassen - beim
            // Durchblaettern wechselt das Ziel im Sekundentakt.
            if (_beacon.IsRunning) _beacon.Idle();
            return;
        }

        _beacon.Start();   // folgenlos, wenn schon offen
        // 2D: Hoehe zaehlt weder fuer die Drehung noch fuer "stehe ich drauf",
        // und Markerpositionen fuehren ohnehin keine (Y = 0).
        var distance = Distance2D(player.Position, position);
        _beacon.Update(RelativeAngle(player, position), distance, kind,
                       arrived: distance <= AutoWalkService.StopRange, targetKey: targetKey);
    }

    /// <summary>
    /// Worauf der Peil-Ton zeigt, solange die Gehhilfe AUS ist: auf das
    /// anvisierte Ziel - und nur darauf.
    ///
    /// Vorgabe des Users (2026-08-19): *"die toene fuer getrackte sachen sollen
    /// auch kommen wenn die gehhilfe aus ist aber dann nur wenn was anvisiert
    /// ist"*. Eine blosse Auswahl im Objekt-Browser reicht also NICHT. Der
    /// Unterschied ist hoerbar und gewollt: der Browser blaettert im
    /// Sekundentakt durch die Umgebung, und jeder Schritt wuerde sonst einen
    /// neuen Dauerton starten. Anvisieren ist die bewusste Entscheidung "DAS da".
    ///
    /// FOLGE, die im Blick bleiben muss: was das Spiel nicht anvisieren laesst
    /// (Quest-Requisiten, Kartenmarker, Quest- und Inhalts-Ziele, die gar kein
    /// Objekt sind), bekommt hier keinen Ton. Fuer die ist die Gehhilfe der Weg -
    /// sie fuehrt den Ton auf eine Position, ganz ohne anvisiertes Objekt.
    ///
    /// KEIN SOFT-TARGET: das wandert beim Vorbeilaufen von NPC zu NPC, und der
    /// Ton wuerde die Stimme wechseln, ohne dass der Spieler etwas getan haette.
    /// </summary>
    private bool TryGetBeaconTarget(out Vector3 position, out BeaconKind kind, out ulong targetKey)
    {
        position = default;
        kind = BeaconKind.Object;
        targetKey = 0;

        if (_targetManager.Target is not { } target) return false;

        // Verschwundenes Ziel: der Ton hoert auf (Vorgabe des Users 2026-08-19).
        // Normalerweise raeumt das Spiel selbst auf und setzt das Ziel auf null -
        // IsValid ist das Netz darunter, denn ein Zeiger auf ein entladenes
        // Objekt liefert sonst eine Position, die niemand mehr besetzt.
        if (!target.IsValid()) return false;

        // Erlegter Gegner: fuer den Spieler ist er weg, auch solange die Leiche
        // noch anvisiert bleibt. Die Pruefung gilt NUR fuer Lebewesen - was
        // IsDead an einer Tuer oder Truhe bedeutet, ist nicht gemessen, und eine
        // Tuer, die sich faelschlich fuer tot haelt, waere ein stummer Ton ohne
        // Erklaerung.
        if (target.ObjectKind is ObjectKind.BattleNpc or ObjectKind.Pc && target.IsDead) return false;

        position = target.Position;
        kind = BeaconKindForObject(target);
        // Die Objekt-Id ist die Kennung: wird ein anderes Ziel anvisiert, bricht
        // der Ton fuer das alte ab, statt mitten im Takt die Stimme zu wechseln.
        targetKey = target.GameObjectId;
        return true;
    }

    /// <summary>
    /// Die Stimme, mit der ein Objekt gepeilt wird. Gegner und Verbuendete sind
    /// beide BattleNpc - getrennt wird ueber <see cref="CombatSide"/>, dieselbe
    /// Unterscheidung, die auch die Kategorien Gegner und Verbuendete benutzen;
    /// ein Trupp-Kollege darf nicht wie eine Warnung klingen.
    /// </summary>
    private BeaconKind BeaconKindForObject(IGameObject obj) => obj.ObjectKind switch
    {
        ObjectKind.BattleNpc     => CombatSide.IsAlly(obj) ? BeaconKind.Npc : BeaconKind.Enemy,
        ObjectKind.EventNpc      => BeaconKind.Npc,
        ObjectKind.Pc            => BeaconKind.Npc,
        ObjectKind.GatheringPoint => BeaconKind.Gathering,
        ObjectKind.Aetheryte     => BeaconKind.Aetheryte,
        // Eine Inhalts-Tuer sieht in der Objekttabelle aus wie jedes andere
        // EventObj - erst das Sheet sagt, dass sie in einen Dungeon fuehrt.
        ObjectKind.EventObj      => DungeonSide.Describe(obj, _data, _log) != null
                                        ? BeaconKind.DutyEntrance
                                        : BeaconKind.Object,
        _                        => BeaconKind.Object,
    };

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
        // Dungeonliste: kommt aus DutyEntranceService (Sheets), nicht aus der
        // ObjectTable. Genau das ist der Punkt - die Kategorie "Inhalte" darueber
        // kennt nur geladene Tueren, diese kennt jede Tuer der Welt samt Stufe.
        (NavCategory.WorldDuties,     null),
        // Dungeon: kommt aus einer Pfaddatei (DungeonRouteService), nicht aus der
        // ObjectTable und nicht aus den Sheets. Steht direkt hinter den beiden
        // Tuer-Kategorien, weil es die Fortsetzung derselben Frage ist: erst
        // hinein, dann hindurch.
        (NavCategory.DungeonRoute,    null),
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
    /// [Tiefes Gewoelbe] Der Kategoriensatz INNERHALB eines Gewoelbes. Er ersetzt den Weltsatz
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
        // Verbuendete: dieselben Arten wie im Weltsatz - BattleNpc, solange der Trupp
        // aus Trust/Duty-Support besteht, Pc, sobald es echte Mitspieler sind. Der
        // Filter in GetCategoryObjects bleibt unveraendert: CombatSide.IsAlly sagt nur
        // dann ja, wenn das SPIEL etwas als Begleiter oder Gruppenmitglied fuehrt, und
        // eine Ebene, auf der man allein ist, antwortet damit von selbst mit "0".
        //
        // SIE WIRD AUCH LEER ANGEBOTEN, anders als Schaetze oder Leuchten, weil "ist
        // jemand bei mir?" eine echte Frage ist, deren leere Antwort ebenfalls echt
        // ist. Dafuer war nichts zu tun: IsCategoryAvailable kennt fuer Verbuendete
        // keine Regel und laesst sie deshalb stehen.
        //
        // Nachgetragen 2026-08-21 beim Merge von PR #6. Der hatte sie ausgelassen,
        // solange NavCategory.Allies noch einem anderen offenen PR gehoerte - jener
        // (#3, "Zwei neue Objekt-Kategorien") liegt seit dem 10.08. in main.
        (NavCategory.Allies,       new[] { ObjectKind.BattleNpc, ObjectKind.Pc }),
    };

    /// <summary>
    /// [Tiefes Gewoelbe] Der gerade gueltige Kategoriensatz. Der Wechsel zwischen beiden Saetzen wird in
    /// <see cref="Update"/> erkannt, das jeden Frame laeuft; die Begrenzung hier ist nur
    /// eine Absicherung, damit ein Index aus dem anderen Satz nie ausserhalb liest.
    ///
    /// SEIT 2026-08-26 GEHT DER SATZ DURCH DIE REIHENFOLGE DES SPIELERS: er darf
    /// die Kategorien im Einstellungsmenue sortieren und einzelne ganz
    /// abschalten (siehe <see cref="ListOrder"/> und Configuration).
    ///
    /// GECACHT, und das ist kein vorschnelles Optimieren: diese Property wird von
    /// einem Dutzend anderer Properties gelesen, die ihrerseits in jedem Frame
    /// laufen. Ohne Cache sortierte und filterte die Mod die Liste mehrfach pro
    /// Frame, obwohl sich das Ergebnis nur beim Umsortieren aendert. Der
    /// Konfigurations-Stempel sagt genau das an - siehe Configuration.OrderStamp.
    /// </summary>
    private (NavCategory Cat, ObjectKind[]? Kinds)[] Categories
    {
        get
        {
            var deep = DeepDungeon?.IsActive == true;
            if (_orderedCategories == null || _orderedForDeep != deep || _orderedStamp != _config.OrderStamp)
                RebuildCategoryOrder(deep);

            if (_categoryIndex >= _orderedCategories!.Length) _categoryIndex = 0;
            return _orderedCategories;
        }
    }

    /// <summary>
    /// Baut den sortierten und gefilterten Kategoriensatz neu.
    ///
    /// Setzt den Browser dabei auf die erste Kategorie zurueck. Der Index zeigt
    /// nach einer Umsortierung sonst auf eine ANDERE Kategorie als vorher, ohne
    /// dass irgendetwas das sagt - der Spieler drueckt Bild-ab und bekommt einen
    /// Gegenstand aus einer Kategorie, die er nie gewaehlt hat. Von vorn
    /// anzufangen ist der einzige Zustand, der nach dem Umsortieren noch stimmt.
    /// </summary>
    private void RebuildCategoryOrder(bool deep)
    {
        var set    = deep ? DeepDungeonCategories        : WorldCategories;
        var order  = deep ? _config.DeepCategoryOrder    : _config.ObjectCategoryOrder;
        var hidden = deep ? _config.DeepCategoryHidden   : _config.ObjectCategoryHidden;

        _orderedCategories = ListOrder.Apply(set, CategoryKey, order, hidden).ToArray();
        _orderedForDeep = deep;
        _orderedStamp = _config.OrderStamp;
        _categoryIndex = 0;
        _cycleIndex = -1;

        _log.Info($"[Browser] Kategoriensatz neu geordnet ({(deep ? "Gewölbe" : "Welt")}): " +
                  $"{_orderedCategories.Length} von {set.Length} sichtbar.");
    }

    /// <summary>Der gespeicherte Schluessel einer Kategorie: der Enum-NAME, nie
    /// sein Zahlenwert und nie die gesprochene Beschriftung. Beides aendert sich -
    /// der Zahlenwert, sobald jemand einen Wert einfuegt, die Beschriftung mit
    /// "/acc lang".</summary>
    private static string CategoryKey((NavCategory Cat, ObjectKind[]? Kinds) entry) => entry.Cat.ToString();

    /// <summary>Der gerade gueltige Kategoriensatz fuer das Einstellungsmenue:
    /// sortiert wie der Spieler es festgelegt hat, aber NICHTS ausgeblendet - eine
    /// abgeschaltete Kategorie muss erreichbar bleiben, sonst kann er sie nie
    /// wieder einschalten.</summary>
    internal List<NavCategory> OrderableCategories
    {
        get
        {
            var deep = DeepDungeon?.IsActive == true;
            var set   = deep ? DeepDungeonCategories     : WorldCategories;
            var order = deep ? _config.DeepCategoryOrder : _config.ObjectCategoryOrder;
            return ListOrder.Sort(set, CategoryKey, order).ConvertAll(static e => e.Cat);
        }
    }

    /// <summary>Ob gerade der Gewoelbe-Satz gilt - das Einstellungsmenue sagt es im
    /// Titel, damit der Spieler weiss, welche der beiden Listen er umsortiert.</summary>
    internal bool DeepCategorySetActive => DeepDungeon?.IsActive == true;

    /// <summary>Die gesprochene Beschriftung einer Kategorie, in der aktiven
    /// Sprache. Oeffentlich, weil das Einstellungsmenue dieselben Namen braucht,
    /// die der Browser ansagt - zwei Namensquellen fuer dieselbe Kategorie waeren
    /// genau die Art Abweichung, die niemand bemerkt.</summary>
    internal static string CategoryLabelOf(NavCategory cat) => cat switch
    {
        NavCategory.DeepRooms    => AccessibilityStrings.DeepCategoryRooms,
        NavCategory.DeepTreasure => AccessibilityStrings.DeepCategoryTreasure,
        NavCategory.DeepCairns   => AccessibilityStrings.DeepCategoryCairns,
        _                        => AccessibilityStrings.CategoryLabel(cat),
    };

    /// <summary>
    /// [Tiefes Gewoelbe] Der Gewoelbe-Leser, oder null, solange er nicht gesetzt ist - dann verhaelt sich
    /// diese Datei exakt wie in der offenen Welt. Eine Property statt eines
    /// Konstruktor-Arguments, damit die Signatur unveraendert bleibt.
    /// </summary>
    public DeepDungeonNav? DeepDungeon { get; set; }

    /// <summary>
    /// Der Auto-Lauf, oder null solange er nicht gesetzt ist. Gebraucht wird davon
    /// genau eine Auskunft: laeuft gerade einer? Sie entscheidet, ob der Peil-Ton
    /// ueberhaupt spielen darf (siehe <see cref="UpdateTargetBeacon"/>).
    ///
    /// Eine Property statt eines Konstruktor-Arguments, weil Plugin.cs diesen
    /// Dienst frueher baut als den Auto-Lauf - dieselbe Bauordnung wie bei
    /// <see cref="DeepDungeon"/>. Bleibt sie null, schweigt der Ton ausserhalb der
    /// Gehhilfe; das ist die stillere und damit die sichere Richtung.
    /// </summary>
    public AutoWalkService? AutoWalk { get; set; }

    /// <summary>[Tiefes Gewoelbe] Ob der Gewoelbe-Satz im vorigen Frame galt, damit das Betreten oder
    /// Verlassen eines Gewoelbes den Browser zuruecksetzen kann.</summary>
    private bool _deepCategoriesActive;

    /// <summary>Der sortierte und gefilterte Kategoriensatz, oder null solange er
    /// noch nie gebraucht wurde. Siehe <see cref="Categories"/>.</summary>
    private (NavCategory Cat, ObjectKind[]? Kinds)[]? _orderedCategories;

    /// <summary>Fuer welchen der beiden Saetze <see cref="_orderedCategories"/> gilt.</summary>
    private bool _orderedForDeep;

    /// <summary>Der Konfigurations-Stempel, mit dem <see cref="_orderedCategories"/>
    /// gebaut wurde. Startet auf einem Wert, den kein gespeicherter Stempel treffen
    /// kann, damit der erste Zugriff in jedem Fall neu baut.</summary>
    private int _orderedStamp = int.MinValue;

    /// <summary>The spoken label of the current category, in the active language.</summary>
    private string CurrentCategoryLabel => CategoryLabelOf(Categories[_categoryIndex].Cat);

    /// <summary>[Tiefes Gewoelbe] Ob der Browser gerade auf der Raumliste steht.</summary>
    private bool IsDeepRoomCategory => Categories[_categoryIndex].Cat == NavCategory.DeepRooms;

    private bool IsQuestCategory           => Categories[_categoryIndex].Cat == NavCategory.QuestGoals;
    private bool IsUnacceptedQuestCategory => Categories[_categoryIndex].Cat == NavCategory.AcceptableQuests;
    private bool IsLevequestCategory       => Categories[_categoryIndex].Cat == NavCategory.Levequests;
    private bool IsPlacesCategory          => Categories[_categoryIndex].Cat == NavCategory.Waypoints;
    private bool IsAetheryteCategory       => Categories[_categoryIndex].Cat == NavCategory.Aetherytes;
    private bool IsFishingCategory         => Categories[_categoryIndex].Cat == NavCategory.FishingSpots;
    private bool IsFateCategory            => Categories[_categoryIndex].Cat == NavCategory.Fates;
    private bool IsHuntingCategory         => Categories[_categoryIndex].Cat == NavCategory.HuntingTargets;
    private bool IsWorldDutyCategory       => Categories[_categoryIndex].Cat == NavCategory.WorldDuties;
    private bool IsDungeonRouteCategory    => Categories[_categoryIndex].Cat == NavCategory.DungeonRoute;

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
    /// Der Inhalts-Eingang, den der Browser in der Dungeonliste gewaehlt hat,
    /// oder null. Seine Position ist die feste Stelle der Tuer aus den Sheets -
    /// VOLLE 3D, anders als bei Kartenmarkern; liegt sie in einer anderen Zone,
    /// fuehrt Plugin.cs Numpad3 wie bei einem Quest-Ziel ueber die Zonenuebergaenge.
    /// </summary>
    public DutyEntrance? SelectedDutyEntrance { get; private set; }

    /// <summary>
    /// Die Station des Dungeon-Wegs, die der Browser gewaehlt hat, oder null.
    ///
    /// <para>
    /// Sie hat KEIN Spielobjekt, mit Absicht: eine Station ist ein Ort aus einer
    /// Datei, und der Auto-Lauf soll auch dorthin fuehren, wo gerade nichts
    /// geladen ist - genau wie bei Quest-Zielen und Aetheryten. Plugin.cs nimmt
    /// deshalb die Position, nicht das Ziel.
    /// </para>
    /// </summary>
    public DungeonStep? SelectedDungeonStep { get; private set; }

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
        SelectedDutyEntrance = null;
        SelectedDungeonStep = null;

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
            // Enemies exist only while a leve is actually running - see
            // GetLevequestEnemies; outside that this is 0 and stays unspoken.
            var enemies = _objectTable.LocalPlayer is { } p ? GetLevequestEnemies(p).Count : 0;
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryLevequestCount(givers, goals, enemies));
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

        if (IsDungeonRouteCategory)
        {
            // Die zweite Zahl ist die Station, bei der das naechste Blaettern
            // einsteigt - nicht die Listenlaenge noch einmal. Sie beantwortet
            // "wie weit bin ich", und das ist beim Durchqueren die Frage.
            var steps = _dungeonRoute.GetStepsForCurrentZone();
            if (steps.Count == 0)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.NoDungeonRoute);
                return;
            }

            // Ohne Spielerfigur gibt es kein "wo bin ich" - dann nennt die
            // Kopfansage die erste Station statt zu schweigen.
            var resume = _objectTable.LocalPlayer is { } me ? NextDungeonStepIndex(steps, me) : 0;
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryDungeonCount(steps.Count, steps[resume].Number));
            return;
        }

        if (IsWorldDutyCategory)
        {
            // Zwei Zahlen, weil sie zwei verschiedene Fragen beantworten: wie
            // lang die Liste ist, und wie viel davon der Spieler heute betreten
            // darf. Bleibt die Freischaltfrage unbeantwortet, wird nur gezaehlt.
            var entries  = _dutyEntrances.GetReachableSorted();
            var unlocked = 0;
            var known    = false;
            // Eine Schleife, nicht zwei: die Freischaltfrage geht in eine
            // Spielfunktion, und sie zweimal pro Eintrag zu stellen waere der
            // doppelte Preis fuer dieselbe Antwort.
            foreach (var entry in entries)
            {
                var state = _dutyEntrances.IsUnlocked(entry.ContentId);
                if (state == null) continue;
                known = true;
                if (state == true) unlocked++;
            }
            _tolk.SpeakInterrupt(known
                ? AccessibilityStrings.CategoryWorldDutyCount(entries.Count, unlocked)
                : AccessibilityStrings.CategoryObjectCount(CurrentCategoryLabel, entries.Count));
            return;
        }

        // [Tiefes Gewoelbe] Die Raumliste wird aus dem Content-Director gezaehlt, nicht aus der
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

        if (IsWorldDutyCategory)
        {
            CycleWorldDuty(direction, player);
            return;
        }

        if (IsDungeonRouteCategory)
        {
            CycleDungeonStep(direction, player);
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
        var stats = (rejected ? string.Empty : DescribeTargetHp(obj)) + DescribeTamed(obj);
        // Auch beim Durchblättern die Farbe voranstellen. Ohne das bliebe genau
        // der Weg farblos, auf dem der Spieler seine Gegner ueberhaupt findet:
        // eine Browser-Auswahl visiert selbst an und unterdrueckt deshalb die
        // Zielwechsel-Ansage weiter oben (_ownSelectionId).
        var text = $"{_enemyMarkers.SpokenPrefix(obj)}{description}, " +
                   $"{FormatDistance(distance)}, " +
                   $"{CalculateDirection(player, obj.Position)}" +
                   $"{stats}, " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}." +
                   (rejected ? AccessibilityStrings.NotTargetedSuffix : "");
        _log.Info($"[Nav] Auswahl: {text} (id={obj.GameObjectId:X})");
        _tolk.SpeakInterrupt(text);
    }

    // ── [Tiefes Gewoelbe] Durch die Raeume der aktuellen Ebene blaettern ──

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
                   $"{CalculateDirection(player, dest.Position)}" +
                   $"{GoalCircleHint(dest, player)}.{detail}";
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

    /// <summary>
    /// The live enemies of the RUNNING levequest, nearest first.
    ///
    /// Why they belong in this category at all (user request 2026-08-18): a leve
    /// marker points at the AREA the task happens in, not at the monsters. A
    /// sighted player then sees the enemies standing there; a blind player stood
    /// in the circle with nothing left to steer by. The enemies of the leve are
    /// the actual destination, so they are offered right where the leve is.
    ///
    /// HOW a leve monster is told apart from ordinary wildlife: the object carries
    /// the event id of the leve director that spawned it (GameObject.EventId), and
    /// the wildlife around it carries 0. See LevequestEnemyService.BelongsToLeve
    /// for the two measurements this rests on.
    ///
    /// The dead ends before that are written down so they are not tried again
    /// (logs 2026-08-18):
    ///   (a) Species matching finds nothing reliable. Leve 528 slot 0 names
    ///       BNpcName 1096 "streunender Dodo", but seventeen wild Dodos in the
    ///       same zone carry the slot's BNpcBase 339 as well.
    ///   (b) The director's own EventObjects set is EMPTY on a running leve.
    ///   (c) GetEventHandlersImpl reports one zone-wide handler for every monster,
    ///       leve spawn and wildlife alike - never the director.
    /// The nameplate icon (71244 on both leve spawns that were measured) is NOT
    /// used here: it is the game's "this is a task target" marker in general, so
    /// it says nothing about WHICH task. It is what already carries these monsters
    /// into the quest-enemy category.
    ///
    /// The species list still runs, but only to decide WHOSE fields get dumped
    /// into the log - if the event-id link ever misses, the next decision comes
    /// off a field dump instead of another pick.
    ///
    /// Dead monsters are dropped: a corpse is not a destination.
    /// </summary>
    private List<(IGameObject Obj, LeveEnemySpec? Spec)> GetLevequestEnemies(IGameObject player)
    {
        var leve = _leveEnemies.GetRunningLeve();
        if (leve == null) return new List<(IGameObject, LeveEnemySpec?)>();

        var owned = new HashSet<nint>(leve.EventObjectAddresses);

        var result = new List<(IGameObject Obj, LeveEnemySpec? Spec, float Dist)>();
        var candidates = new List<(string Trace, float Dist)>();
        // Spawns of a leve that is NOT the one we matched: the entry number in
        // the event id differs. Either another player's leve stands next to ours
        // (then ignoring them is right), or our assumption that director and
        // monster share the entry number is wrong (then this line says so).
        var foreignLeve = new List<(string Trace, float Dist)>();

        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
            if (obj is IBattleChara { CurrentHp: 0 }) continue;

            var dist = Vector3.Distance(player.Position, obj.Position);
            var spec = leve.Enemies.FirstOrDefault(e => e.BaseId == obj.BaseId);
            var mine = _leveEnemies.BelongsToLeve(
                           obj.Address, leve.DirectorEventId, out var objEventId, out var anyLeve)
                    || owned.Contains(obj.Address);

            if (mine)
            {
                result.Add((obj, spec, dist));
                continue;
            }

            if (anyLeve)
                foreignLeve.Add(($"'{obj.Name.TextValue}' {dist:F0}m EventId={objEventId} " +
                                 $"(Eintrag {(ushort)objEventId})", dist));

            // Same species, but not bound to our leve: exactly the case that has
            // to be readable in the log if the link ever stops working.
            if (spec != null)
                candidates.Add(($"'{obj.Name.TextValue}' {dist:F0}m " +
                                _leveEnemies.DescribeObjectFields(obj.Address), dist));
        }

        // Only the nearest few, and only when the picture changed - seventeen
        // full field dumps on every keypress would bury the log.
        candidates.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        foreignLeve.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        var todos = _leveEnemies.GetTodoLines(leve.DirectorAddress);
        // Wer von ihnen schon gezaehmt ist, steht mit im Trace. Das ist die
        // Gegenprobe zu der einen Annahme, die DescribeTamed offen laesst: ist
        // die Statusliste auch fuer nicht anvisierte Gegner gefuellt, stehen hier
        // waehrend eines Fang-Freibriefs Ids; bleibt die Zahl bei 0, obwohl das
        // Spiel "ist bereits zahm" sagt, ist sie es nicht.
        var tamed = result.FindAll(e => TameRank(e.Obj) == TameRankTamed)
                          .ConvertAll(e => $"{e.Obj.GameObjectId:X}");
        var agitated = result.FindAll(e => TameRank(e.Obj) == TameRankAgitated)
                             .ConvertAll(e => $"{e.Obj.GameObjectId:X}");

        var trace = $"[Leve] Gegner des laufenden Freibriefs '{leve.LeveName}': {result.Count} gefunden, " +
                    $"davon {tamed.Count} schon gezaehmt" +
                    (tamed.Count > 0 ? $" ({string.Join(", ", tamed)})" : "") +
                    $", {agitated.Count} aufgestachelt" +
                    (agitated.Count > 0 ? $" ({string.Join(", ", agitated)})" : "") + " " +
                    $"(Director-EventId {leve.DirectorEventId}, Eintrag {(ushort)leve.DirectorEventId}, " +
                    $"{leve.EventObjectAddresses.Count} Director-Objekte). " +
                    $"Aufgabenzeilen: {(todos.Count > 0 ? string.Join(" / ", todos) : "keine")}. " +
                    $"Spawns eines FREMDEN Freibriefs: " +
                    (foreignLeve.Count > 0
                        ? string.Join(" | ", foreignLeve.GetRange(0, Math.Min(4, foreignLeve.Count)).ConvertAll(c => c.Trace))
                        : "keine") + ". " +
                    $"Naechste Artgenossen ohne Freibrief-Bindung: " +
                    (candidates.Count > 0
                        ? string.Join(" | ", candidates.GetRange(0, Math.Min(3, candidates.Count)).ConvertAll(c => c.Trace))
                        : "keine");
        if (trace != _lastLeveEnemyTrace)
        {
            _lastLeveEnemyTrace = trace;
            _log.Info(trace);
        }

        // Nach BRAUCHBARKEIT fuer einen Fang, darin unveraendert nach Entfernung:
        // erst die, die gerade zaehlen, dann die aufgestachelten, zuletzt die
        // schon gezaehmten. Ein sehender Spieler ueberspringt die beiden hinteren
        // Gruppen mit einem Blick; ohne diese Reihung muesste ein blinder Spieler
        // sich durch sie hindurchblaettern, um den naechsten zu finden, der noch
        // zaehlt (elf Kobalos fuer vier Faenge im Log 2026-08-21).
        //
        // Sie fallen bewusst NICHT aus der Liste. Beide Zustaende sind
        // voruebergehend - das Spiel kennt sogar eine eigene Meldung dafuer, dass
        // einer endet ("... ist nicht mehr zahm", LogMessage 1809) - und ein
        // Gegner, den das Plugin faelschlich verschwiegen haette, waere ohne
        // Ansage nicht mehr auffindbar. Hinten in der Liste, mit dem Grund
        // dahinter, ist er beides: aus dem Weg und noch da.
        return result
            .OrderBy(e => TameRank(e.Obj))
            .ThenBy(e => e.Dist)
            .Select(e => (e.Obj, e.Spec))
            .ToList();
    }

    // Last leve-enemy diagnosis written, so a held key does not repeat it.
    private string _lastLeveEnemyTrace = string.Empty;

    private void CycleLevequestDestination(int direction, IGameObject player)
    {
        // Enemies of the running leve come FIRST: while a leve runs they are the
        // task, and the giver one is walking away from is not.
        var enemies = GetLevequestEnemies(player);
        var dests = GetLevequestDestinations();
        var count = enemies.Count + dests.Count;
        if (count == 0)
        {
            SelectedQuestDestination = null;
            SelectedObjectDestination = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoLevequests);
            return;
        }

        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        if (_cycleIndex < enemies.Count)
        {
            AnnounceLevequestEnemy(enemies[_cycleIndex], player, count);
            return;
        }

        var dest = dests[_cycleIndex - enemies.Count];
        SelectedQuestDestination = dest;
        // A marker is not an object: drop a leve enemy picked a keypress ago, or
        // Numpad 3 would still steer at the monster.
        SelectedObjectDestination = null;

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
                   $"{CalculateDirection(player, dest.Position)}" +
                   $"{GoalCircleHint(dest, player)}.{detail}";
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

    /// <summary>
    /// Says whether the player stands INSIDE the marker's goal circle, and how
    /// far the edge still is when they do not.
    ///
    /// The distance alone is not usable information: "Gefräßige Puks, 75 Meter"
    /// leaves open whether that is inside or outside, because the circle is 50 m
    /// wide (MarkerInfo.Radius, measured 2026-08-18 - leve goals in La Noscea
    /// carry r=50). A sighted player reads that circle straight off the map. It
    /// also explains what Numpad 3 does: the walk stops at the RIM, so
    /// "angekommen" at 51 m is the rim, not the middle.
    ///
    /// Empty for point markers (radius 0) - there the plain distance already is
    /// the whole truth.
    /// </summary>
    private string GoalCircleHint(QuestDestination dest, IGameObject player)
    {
        if (dest.Radius <= 0f) return string.Empty;

        var dist = Vector3.Distance(player.Position, dest.Position);
        return dist <= dest.Radius
            ? AccessibilityStrings.InsideGoalCircle
            : AccessibilityStrings.ToGoalCircle(FormatDistance(dist - dest.Radius));
    }

    /// <summary>
    /// Announces one enemy of the running leve and makes it the pick.
    ///
    /// Same two steps as the ordinary object browser, and for the same reasons:
    /// the game target so the player can attack it right away, and
    /// SelectedObjectDestination so Numpad 3 still walks there when the game
    /// refuses the target (out of range, line of sight). SelectedQuestDestination
    /// is cleared because the walk key checks it FIRST - left standing, it would
    /// send the player to the leve marker while the announcement named a monster.
    /// </summary>
    private void AnnounceLevequestEnemy(
        (IGameObject Obj, LeveEnemySpec? Spec) entry, IGameObject player, int count)
    {
        var (obj, spec) = entry;

        SelectedQuestDestination = null;

        _ownSelectionId = obj.GameObjectId;
        _targetManager.Target = obj;

        SelectedObjectDestination = new ObjectDestination(
            obj.GameObjectId, _objectNames.Describe(obj), obj.Position);

        var actualId = _targetManager.Target?.GameObjectId ?? 0;
        var rejected = actualId != obj.GameObjectId;
        if (rejected)
            _log.Info($"[Leve] Target-Set ABGELEHNT: wollte {obj.GameObjectId:X} " +
                      $"({obj.Name.TextValue}), ist weiterhin {actualId:X}");

        // Level and HP only when the game accepted the target: only then is the
        // target bar filled, and only then would a sighted player see the values.
        var stats = (rejected ? string.Empty : DescribeTargetHp(obj)) + DescribeTamed(obj);
        var wanted = spec is { Required: > 0 }
            ? AccessibilityStrings.LeveEnemyWanted((int)spec.Required)
            : string.Empty;

        var text = $"{AccessibilityStrings.LeveRolePrefix(QuestMarkerRole.LeveEnemy)}" +
                   $"{_objectNames.Describe(obj)}{wanted}, " +
                   $"{FormatDistance(Vector3.Distance(player.Position, obj.Position))}, " +
                   $"{CalculateDirection(player, obj.Position)}" +
                   $"{stats}, " +
                   $"{AccessibilityStrings.Counter(_cycleIndex + 1, count)}." +
                   (rejected ? AccessibilityStrings.NotTargetedSuffix : "");
        _log.Info($"[Leve] Auswahl: {text} (id={obj.GameObjectId:X})");
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
            // Gleiche Stelle wie im Zweig fuer andere Zonen unten (PR #8): ohne
            // Lebensraum liefert HuntingArea "", und das feste ", " dahinter
            // ergab zwei Kommas hintereinander (", , 30 Meter"). Das Trennzeichen
            // muss mit dem Satzteil verschwinden, den es trennen soll.
            var habitat = AccessibilityStrings.HuntingArea(target.AreaName);
            text += ", " + (habitat.Length > 0 ? habitat + ", " : string.Empty) +
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

    // ── Dungeonliste: jede Tuer der Welt, nach Stufe ──
    //
    // Der Gegenentwurf zur Kategorie "Inhalte": die zeigt, was hier steht, diese
    // zeigt, was es GIBT. Deshalb kommt sie aus den Sheets (DutyEntranceService)
    // und nicht aus der Objekttabelle, und deshalb ist die Reihenfolge die Stufe
    // und nicht die Entfernung - der User will wissen, was als naechstes dran
    // ist, nicht was zufaellig in der Naehe liegt (Wunsch 2026-08-19).
    private void CycleWorldDuty(int direction, IGameObject player)
    {
        var entries = _dutyEntrances.GetReachableSorted();
        if (entries.Count == 0)
        {
            SelectedDutyEntrance = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoWorldDuties);
            return;
        }

        var count = entries.Count;
        _cycleIndex = ((_cycleIndex + direction) % count + count) % count;
        var entry = entries[_cycleIndex];
        SelectedDutyEntrance = entry;

        // Name, Art und Stufe in genau der Form, die die Kategorie "Inhalte"
        // schon spricht - dieselbe Sache darf nicht zweimal anders klingen.
        var text = AccessibilityStrings.DutyEntrance(entry.Name, entry.ContentType, entry.Level, entry.TypeName);

        // Die Sperre steht frueh im Satz: sie entscheidet, ob der Rest den
        // Spieler ueberhaupt interessiert.
        if (_dutyEntrances.IsUnlocked(entry.ContentId) == false)
            text += ", " + AccessibilityStrings.DutyLocked;

        if (entry.TerritoryTypeId == _clientState.TerritoryType)
        {
            text += $", {FormatDistance(Vector3.Distance(player.Position, entry.Position))}, " +
                    $"{CalculateDirection(player, entry.Position)}.";
        }
        else
        {
            // Andere Zone: erst wohin, dann ueber welchen Uebergang - genau wie
            // bei Quest- und Jagdzielen, denn es ist dieselbe Frage.
            var zone = string.IsNullOrEmpty(entry.ZoneName)
                ? AccessibilityStrings.InAnotherArea
                : AccessibilityStrings.InArea(entry.ZoneName);
            text += ", " + zone;

            var hop = _places.FindFirstHopToMap(entry.MapId, out var hops);
            if (hop != null)
            {
                text += AccessibilityStrings.RouteViaHop(
                    hop.Name,
                    FormatDistance(Distance2D(player.Position, hop.Position)),
                    CalculateDirection(player, hop.Position),
                    hops - 1);
                text += AccessibilityStrings.NumpadWalksToTransition;
            }
            else
            {
                // Kein Uebergang dorthin: das ist eine Auskunft und wird gesagt.
                // Zu schweigen hiesse, den Spieler eine Taste druecken zu lassen,
                // die nichts tun kann.
                text += ", " + AccessibilityStrings.DutyNoWalkingRoute;
            }
        }

        text += $" {AccessibilityStrings.Counter(_cycleIndex + 1, count)}.";
        _log.Info($"[Inhalte] Auswahl: {text}");
        _tolk.SpeakInterrupt(text);
    }

    // ── Dungeon: die Stationen des Wegs, in Laufreihenfolge ──
    //
    // DIE EINZIGE KATEGORIE OHNE ENTFERNUNGSSORTIERUNG, und das ist ihr Zweck.
    // Ueberall sonst ist "das Naechste" die beste Antwort, weil der Spieler
    // waehlt, wohin er will. Hier waehlt er es nicht - der Dungeon gibt die
    // Reihenfolge vor, und eine nach Naehe sortierte Liste zerstoert genau die
    // Auskunft, wegen der es die Kategorie gibt (Wunsch 2026-08-29: "so das man
    // sie nach der reie ablaufen kann").
    private void CycleDungeonStep(int direction, IGameObject player)
    {
        var steps = _dungeonRoute.GetStepsForCurrentZone();
        if (steps.Count == 0)
        {
            SelectedDungeonStep = null;
            _tolk.SpeakInterrupt(AccessibilityStrings.NoDungeonRoute);
            return;
        }

        var count = steps.Count;

        // DER EINSTIEG IST DIE NAECHSTE STATION, NICHT DIE ERSTE. Wer auf halber
        // Strecke steht und die Kategorie oeffnet, will dort weitermachen, wo er
        // ist - sonst blaettert er sich jedes Mal durch den halben Dungeon, den
        // er schon hinter sich hat. Ab dem zweiten Druck laeuft die Liste dann
        // stur der Reihe nach, weil nur das eine Reihenfolge ist.
        _cycleIndex = _cycleIndex < 0
            ? NextDungeonStepIndex(steps, player)
            : ((_cycleIndex + direction) % count + count) % count;

        var step = steps[_cycleIndex];
        SelectedDungeonStep = step;

        var text = AccessibilityStrings.DungeonStepEntry(
            step.Number,
            count,
            AccessibilityStrings.DungeonStepKindWord(step.Kind),
            step.Name,
            FormatDistance(Vector3.Distance(player.Position, step.Position)),
            CalculateDirection(player, step.Position));

        // Das Ende wird gesagt, weil es eine Auskunft ist: der Weg hoert hier
        // auf, es kommt nichts mehr. Ein stilles Umbrechen auf Station 1 waere
        // dieselbe Ansage wie ein weiterer Schritt.
        if (step.Number == count) text += " " + AccessibilityStrings.DungeonRouteEnd;

        _log.Info($"[Dungeon] Auswahl: {text}");
        _tolk.SpeakInterrupt(text);
    }

    /// <summary>
    /// Bei welcher Station der Browser einsteigt: der naechstgelegenen.
    ///
    /// <para>
    /// WARUM DIE NAECHSTGELEGENE UND NICHT "DIE ERSTE NOCH NICHT BESUCHTE": das
    /// Plugin sieht den Fortschritt des Dungeons nicht. Es weiss nicht, welcher
    /// Boss gefallen ist. Was es sieht, ist wo der Spieler STEHT, und in einem
    /// Dungeon, der linear durchlaufen wird, ist die naechste Station zugleich
    /// die, an der er gerade arbeitet. Das ist eine Naeherung und sie steht hier,
    /// damit sie beim ersten Fehlgriff nicht gesucht werden muss.
    /// </para>
    ///
    /// <para>
    /// DIE HOEHE ZAEHLT VOLL MIT, anders als bei den Kartenmarkern: Dungeons sind
    /// gestapelt. Zwei Stationen koennen auf der Karte uebereinander liegen und
    /// dreissig Meter Treppe auseinander - eine 2D-Naehe waehlte dort die falsche.
    /// </para>
    /// </summary>
    private static int NextDungeonStepIndex(IReadOnlyList<DungeonStep> steps, IGameObject player)
    {
        var best = 0;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < steps.Count; i++)
        {
            var distance = Vector3.Distance(player.Position, steps[i].Position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }

        return best;
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
        //
        // A running leve also keeps the category available on its own: its
        // enemies are in it, and they must never become unreachable just because
        // the marker list happens to be empty at that moment.
        if (Categories[index].Cat == NavCategory.Levequests)
            return _questMarkers.GetLevequestDestinations().Count > 0
                || _leveEnemies.GetRunningLeve() != null;

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

        // Dungeon nur, wo fuer diese Zone ueberhaupt ein Weg hinterlegt ist. Das
        // ist die grosse Mehrheit der Zonen NICHT - in der offenen Welt waere die
        // Kategorie sonst in jedem Durchblaettern ein Tastendruck Rauschen, und
        // zwar einer, der nie etwas antworten kann (Regel wie bei Angelplaetzen).
        if (Categories[index].Cat == NavCategory.DungeonRoute)
            return _dungeonRoute.GetStepsForCurrentZone().Count > 0;

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

        // [Tiefes Gewoelbe] Eine Truhe nennt ihre FARBE, wo das Spiel eine gibt - im spieleigenen
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

        // [Tiefes Gewoelbe] Die beiden Objekt-Kategorien. Beide ordnen nach den
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

    /// <summary>Wohin der Spieler eigentlich wollte, wenn die Gehhilfe auf einen
    /// Ersatzpunkt daneben umgeleitet hat, weil das Ziel unter einem Vorsprung
    /// steht (<see cref="AutoWalkService.TryStepOutFromUnderCeiling"/>). Null bei
    /// jeder gewoehnlichen Fuehrung.</summary>
    private Vector3? _walkCeilingDestination;

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
    /// <summary>Stimme des Peil-Tons fuer das aktuelle Gehhilfe-Ziel.</summary>
    private BeaconKind _walkBeaconKind = BeaconKind.Object;

    /// <summary>
    /// Kennung des laufenden Gehhilfe-Ziels fuer den Peil-Ton. Eine laufende
    /// Nummer und nicht die Objekt-Id, weil die Gehhilfe auch auf blosse
    /// Positionen zeigt (Marker, Zonenuebergaenge) - und weil zweimal dieselbe
    /// Tuer hintereinander trotzdem zwei getrennte Laeufe sind.
    /// </summary>
    private ulong _walkBeaconKey;

    /// <summary>
    /// Vergibt die Kennungen aus <see cref="_walkBeaconKey"/>. Startet weit
    /// oberhalb jeder Objekt-Id, damit eine Gehhilfe-Kennung nie zufaellig mit
    /// der Id eines anvisierten Ziels zusammenfaellt - sonst bliebe genau der
    /// Uebergang zwischen beiden unbemerkt, den der Neustart abfangen soll.
    /// </summary>
    private static ulong _walkBeaconKeyCounter = 1UL << 60;

    private bool _guideMeshEndAnnounced;

    /// <summary>
    /// Das zuletzt ANGESAGTE Segment-Ende, oder null am Anfang eines Laufs.
    /// Verhindert, dass dieselbe Etappe mehrfach gesprochen wird: seit die
    /// Ansage das Segment-Ende nennt statt des naechsten Rohpunkts, passiert der
    /// Spieler mehrere Wegpunkte, die alle auf DENSELBEN Zielpunkt zeigen -
    /// ohne diesen Merker kaeme bei jedem davon dieselbe Zeile
    /// (Log 13:18: "4 Meter, suedwestlich" und "3 Meter, suedwestlich" in
    /// 150 ms Abstand).
    /// </summary>
    private Vector3? _lastSpokenLeg;

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

        _walkBeaconKind = BeaconKindForObject(target);
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
    {
        // Die Stimme kommt aus der Browser-Auswahl, die diesen Lauf ausgeloest
        // hat: der Aufrufer loest genau diese Auswahl gerade in eine Position
        // auf, also ist sie hier noch gueltig. Fuehrt der Weg in eine andere
        // Zone, ist das Laufziel ein Uebergang - und der klingt dann auch wie
        // einer, nicht wie das Ziel dahinter.
        _walkBeaconKind = SelectedQuestDestination is { } q && q.TerritoryTypeId != _clientState.TerritoryType
                       || SelectedDutyEntrance is { } d && d.TerritoryTypeId != _clientState.TerritoryType
                       || SelectedHuntTarget is { InCurrentZone: false }
            ? BeaconKind.Transition
            : BeaconKindForSelection();
        StartWalkGuide(0, name, position, MathF.Max(ArrivalDistance, arrivalRange));
    }

    /// <summary>Die Stimme der aktuellen Browser-Auswahl, ohne Zonen- oder Positionspruefung.</summary>
    private BeaconKind BeaconKindForSelection()
    {
        if (SelectedQuestDestination != null) return BeaconKind.Quest;
        if (SelectedHuntTarget != null) return BeaconKind.Enemy;
        if (SelectedDutyEntrance != null) return BeaconKind.DutyEntrance;
        if (SelectedPlaceDestination is { } p)
            return p.IsZoneTransition ? BeaconKind.Transition
                 : p.TypeLabel is "Ätheryt" or "Aethernet" ? BeaconKind.Aetheryte
                 : p.IsWaterSpot ? BeaconKind.Gathering
                 : BeaconKind.Object;
        return BeaconKind.Object;
    }

    private void StartWalkGuide(ulong targetId, string name, Vector3 destination, float arrivalRange)
    {
        // Steht das Ziel unter etwas Begehbarem, fuehrt der Peil-Ton sonst aufs
        // Dach darueber - der Ton kennt nur einen Punkt, und die Wegsuche macht aus
        // dem Ziel dasselbe Bruecken-Deck wie beim Auto-Lauf. Dieselbe Frage, damit
        // beide Wege am selben Fleck ankommen.
        var ceilingDetour = 0f;
        _walkCeilingDestination = null;
        if (AutoWalk is { } walk && walk.TryStepOutFromUnderCeiling(destination, out var ground, out var lift))
        {
            ceilingDetour = Vector3.Distance(destination, ground);
            _log.Info($"[Vorsprung] Gehhilfe: {name} ({destination.X:F1}|{destination.Y:F1}|{destination.Z:F1}) liegt " +
                      $"{lift:F1} m unter dem Netz - fuehre stattdessen zu " +
                      $"({ground.X:F1}|{ground.Y:F1}|{ground.Z:F1}), {ceilingDetour:F1} m daneben.");
            _walkCeilingDestination = destination;
            destination = ground;
            // Sonst zoege WalkGuideFrame die Objektposition jeden Frame zurueck.
            targetId = 0;
        }

        _walkGuideActive = true;
        _walkBeaconKey = ++_walkBeaconKeyCounter;
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
        // Eine Zeile, nicht zwei: ein zweiter Interrupt direkt danach schnitte die
        // erste ab (V4.42).
        _tolk.SpeakInterrupt(_walkCeilingDestination != null
            ? AccessibilityStrings.WalkGuideOnBelowLedge(_walkTargetName, ceilingDetour)
            : AccessibilityStrings.WalkGuideOn(_walkTargetName));
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
        // Stop, nicht Idle: mit dem Ende der Gehhilfe endet der Ton, und bis zum
        // naechsten Lauf vergehen Minuten - das Geraet hat solange nichts offen zu
        // haben. Die alte Begruendung ("im naechsten Frame uebernimmt der
        // Ziel-Peilton") gilt nicht mehr, seit der Ton an einen LAUF gebunden ist
        // und nicht mehr an ein anvisiertes Ziel. Ausfuehrlich steht das an der
        // gleichlautenden Stelle in UpdateTargetBeacon.
        //
        // Hier UND dort, obwohl das Gatter es im naechsten Frame ohnehin taete:
        // "aus" muss im selben Moment aus sein, in dem es der Spieler ausschaltet.
        _beacon.Stop();
        ClearRoute();
    }

    private void ClearRoute()
    {
        _route = null;
        _routeCursor = 0;
        // Der naechste Lauf faengt frisch an: sonst schwiege seine erste Etappe,
        // wenn sie zufaellig dorthin zeigt wie die letzte des vorigen.
        _lastSpokenLeg = null;
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

        // Kompass, wie alles Gesprochene seit 2026-08-23. Fuer den Vergleich
        // "hat sich die Richtung geaendert?" ist er sogar der bessere Massstab:
        // die relative Angabe wechselte schon, wenn der Spieler sich nur drehte,
        // ohne dass die Route eine andere geworden waere.
        // Segment-Ende, wie Ton und Ausricht-Taste (siehe CurrentGuidePoint) -
        // sonst meldet ein Re-Routing eine Richtung, die der Ton nicht zeigt.
        var previousDirection = _route != null && _routeCursor < _route.Count
            ? RouteService.CompassAdjective(
                  player.Position,
                  _route[RouteService.SegmentEndIndex(player.Position, _route, _routeCursor)])
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
            var newDirection = RouteService.CompassAdjective(
                player.Position,
                _route[RouteService.SegmentEndIndex(player.Position, _route, _routeCursor)]);
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

            // Umgeleitet, weil das Ziel unter einem Vorsprung steht: der Weg ist zu
            // Ende, das Ziel aber noch ein Stueck weiter. Wie beim Auto-Lauf sagt
            // die Ansage beides, und gedreht wird aufs echte Ziel.
            if (_walkCeilingDestination is { } real)
            {
                var gap = Vector3.Distance(player.Position, real);
                var bearing = RouteService.CompassWord(player.Position, real);
                _tolk.SpeakInterrupt(AccessibilityStrings.ArrivedBelowLedge(_walkTargetName, gap, bearing));
                FacingService.FaceTowards(player, real);
                _log.Info($"[Nav] Gehhilfe: unter dem Vorsprung angekommen, dist={distance:F1}, " +
                          $"Ziel noch {gap:F1} m nach {bearing}.");
                return;
            }

            _tolk.SpeakInterrupt(AccessibilityStrings.TargetReached(_walkTargetName));
            // Same promise as the auto-walk: arriving means standing there AND
            // facing it, so walking forward or interacting just works. On the way
            // the beacon stays in charge of the direction - the player steers.
            FacingService.FaceTowards(player, _walkDestPosition);
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

        // DER PEILPUNKT IST DAS SEGMENT-ENDE, NICHT DER NAECHSTE ROHE WEGPUNKT.
        //
        // Vorher sprang er bei jedem passierten Wegpunkt weiter, und weil der
        // naechste woanders liegt, riss die Ausrichtung jedes Mal auf: einrasten,
        // drei Meter laufen, ausrasten, neu ausrichten. Gemessen 2026-08-23 an
        // einer Route mit 5 Wegpunkten auf 72 m - `rot` blieb dabei unveraendert,
        // es lag also nie am Spieler. Jetzt zeigt der Ton dorthin, wo der Weg
        // wirklich abbiegt (siehe RouteService.SegmentEndIndex); dieselbe Route
        // hat damit 2 Peilpunkte statt 5.
        //
        // DER TON REISST DABEI NICHT AB, und das ist Bedingung des Users:
        // `targetKey` bleibt ueber die ganze Gehhilfe `_walkBeaconKey`. Nur ein
        // WECHSEL dieses Schluessels laesst BeaconService den Takt abbrechen und
        // neu ansetzen - ein wandernder Peilpunkt unter demselben Schluessel
        // veraendert lediglich Winkel und Lautstaerke, fortlaufend und ohne Luecke.
        var guidePoint = _route != null && _routeCursor < _route.Count
            ? _route[RouteService.SegmentEndIndex(player.Position, _route, _routeCursor)]
            : _walkDestPosition;
        var guideDist = Vector3.Distance(player.Position, guidePoint);
        var relAngle = RelativeAngle(player, guidePoint);
        // LAUTSTAERKE NACH DEM PEILPUNKT, NICHT NACH DER RESTSTRECKE - geaendert
        // 2026-08-23 auf Vorschlag des Users ("vielleicht sollten wir die
        // Entfernung ueber die Wegpunkte machen").
        //
        // Vorher stand hier `distance`, also der Weg bis zum ZIEL, mit der
        // Begruendung "Wegpunkte sind immer nah, der Ton waere sonst dauernd
        // laut". Was dabei uebersehen wurde, zeigt das Log vom 15:27:
        //     dist=10,0  zielDist=701,0  relAngle=0
        // Der Peilpunkt lag 10 m entfernt, gerechnet wurde mit 701 m - und bei
        // der Entfernung greift die Untergrenze von 15 % aus
        // BeaconService.Update. Auf einer langen Route ist der Ton damit ueber
        // die gesamte Strecke praktisch stumm, genau dort, wo man ihn am
        // laengsten braucht. Der User meldete es als *"beim Laufen hab ich jetzt
        // keinen Ton, es sei denn das Ziel ist weiter weg"*.
        //
        // Das alte Gegenargument traegt nicht: der Ton ist ohnehin NUR hoerbar,
        // wenn die Ausrichtung nicht stimmt (sonst schweigt er). Laut ist er
        // also genau dann, wenn eine Korrektur faellig ist - das ist kein
        // Uebermass, sondern der Zweck. Wie weit es noch ist, sagt die Sprache.
        //
        // `arrived` bleibt bewusst am ZIEL haengen: es beantwortet "bin ich da",
        // und das entscheidet die Reststrecke, nicht der naechste Knick.
        _beacon.Update(relAngle, guideDist, _walkBeaconKind,
                       arrived: distance <= _walkArrivalRange, targetKey: _walkBeaconKey);

        // Reassurance repeat between waypoint events; the beacon carries the
        // direction continuously in the frames between. After the mesh ended we
        // keep the slower route cadence: the straight-line rhythm exists for
        // short final approaches, and at 469 m remaining it would repeat the
        // same line every 2 s with nothing new to say.
        var interval = _route != null || _guideMeshEndAnnounced ? RouteSpeakIntervalS : StraightSpeakIntervalS;
        if ((DateTime.UtcNow - _lastGuideTick).TotalSeconds < interval) return;
        _lastGuideTick = DateTime.UtcNow;

        // Gesprochen wird die Himmelsrichtung, gefuehrt wird ueber den Ton. Der
        // Ton steht direkt darueber und bekommt weiter relAngle - das ist die
        // Arbeitsteilung seit 2026-08-23: die Sprache sagt, WO der Wegpunkt
        // liegt, der Ton sagt, ob man richtig steht.
        _tolk.SpeakInterrupt($"{FormatDistance(guideDist)}, " +
                             $"{RouteService.CompassAdjective(player.Position, guidePoint)}" +
                             $"{VerticalHint(player, guidePoint)}.");
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

        // Shared with the automatic turn on arrival (AutoWalkService) so both keep
        // writing the same two fields with the same convention.
        FacingService.FaceTowards(player, target);

        _log.Info($"[Face] nachher: rot={((CSGameObject*)player.Address)->Rotation:F3} " +
                  $"dirH={(gameCam != null ? gameCam->DirH : float.NaN):F3}");

        _tolk.SpeakInterrupt(AccessibilityStrings.FaceAligned(FormatDistance(distance)));
    }

    /// <summary>
    /// Where the walk guide is currently steering - das Ende des laufenden
    /// Segments, solange eine Route existiert, sonst das Ziel selbst. Null, wenn
    /// keine Gehhilfe laeuft.
    ///
    /// <para>
    /// ES MUSS DERSELBE PUNKT SEIN, AUF DEN DER TON ZEIGT. Als der Ton am
    /// 2026-08-23 auf das Segment-Ende umgestellt wurde, blieb diese Property auf
    /// dem naechsten ROHEN Wegpunkt stehen - und damit drehte die Ausricht-Taste
    /// (Numpad5) den Spieler in eine andere Richtung, als der Ton anzeigte. Der
    /// Ton wurde nach dem Ausrichten folgerichtig nicht still, weil der Spieler
    /// nach der Drehung tatsaechlich falsch stand. Gemeldet vom User noch am
    /// selben Tag: *"wenn ich mich mit Numpad5 automatisch ausrichte geht der Ton
    /// nicht mit"*.
    /// </para>
    ///
    /// <para>
    /// MERKSATZ: Ton, gesprochene Richtung und Ausricht-Taste beantworten
    /// dieselbe Frage - "wo geht es lang". Sie muessen aus derselben Quelle
    /// kommen; drei Antworten auf eine Frage sind fuer einen blinden Spieler
    /// nicht auseinanderzuhalten.
    /// </para>
    /// </summary>
    private Vector3? CurrentGuidePoint
    {
        get
        {
            if (!_walkGuideActive) return null;
            if (_route == null || _routeCursor >= _route.Count) return _walkDestPosition;

            // Ohne Spieler kein Segment - dann bleibt der Rohpunkt die ehrlichste
            // Auskunft, denn das Segment beginnt bei der Spielerposition.
            var player = _objectTable.LocalPlayer;
            return player == null
                ? _route[_routeCursor]
                : _route[RouteService.SegmentEndIndex(player.Position, _route, _routeCursor)];
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
            // NUR AN ECHTEN ABBIEGUNGEN, nicht an jedem Rohpunkt. Der Ton peilt
            // seit 2026-08-23 das Segment-Ende an (siehe WalkGuideFrame), und die
            // Sprache muss denselben Punkt meinen - sonst nennt sie eine Richtung,
            // die der Ton gar nicht zeigt.
            //
            // Es loest ausserdem dasselbe Uebermass wie beim Ton: im Log vom
            // 13:18 kamen "4 Meter, suedwestlich" und "3 Meter, suedwestlich"
            // innerhalb von 150 ms nacheinander - zweimal dieselbe Richtung, weil
            // zwei Rohpunkte in derselben Richtung lagen.
            var next = route[RouteService.SegmentEndIndex(player.Position, route, _routeCursor)];
            var dist = Vector3.Distance(player.Position, next);
            var sameLeg = _lastSpokenLeg is { } last
                          && Vector3.DistanceSquared(last, next) < 0.01f;
            _lastSpokenLeg = next;

            if (!sameLeg)
            {
                if (genuineReach) _cue.PlayWaypointTone();
                _tolk.SpeakInterrupt($"{FormatDistance(dist)}, " +
                                     $"{RouteService.CompassAdjective(player.Position, next)}" +
                                     $"{VerticalHint(player, next)}.");
                // (FormatDistance/CompassAdjective/VerticalHint are all language-aware.)
            }
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
    /// Der Status, den ein bereits gezaehmter Gegner traegt.
    ///
    /// GEMESSEN, nicht geraten (dalamud.log 2026-08-21, Freibrief "Im Namen des
    /// Fortschritts"): um 09:35:51 weist das Spiel einen Beruhigen-Versuch mit
    /// "Der Pyrit-Kobalos ist bereits zahm." ab; zehn Sekunden spaeter liest die
    /// Fang-Sonde an genau diesem Ziel "Status: 213:'Besänftigung'".
    ///
    /// IM SHEET BESTAETIGT (Lumina, installierte Spieldaten, 2026-08-21):
    /// Zeile 213 heisst DE "Besänftigung" / EN "Pacification" und beschreibt sich
    /// selbst als "Zahm und greift nicht mehr an." / "The target is pacified and
    /// will no longer attack." Sie ist die EINZIGE Zeile des ganzen Sheets mit
    /// dem Symbol 216301 - die drei anderen Zeilen, die englisch ebenfalls
    /// "Pacification" heissen (6, 620, 5188), sind der Spieler-Debuff "Pacem"
    /// ("Waffenfertigkeiten können nicht eingesetzt werden", Symbol 215017) und
    /// koennen deshalb nicht mit dieser verwechselt werden.
    ///
    /// Auf die Id geprueft und nicht auf den Text: der Name steht in der Sprache
    /// des Clients, die Id nicht.
    ///
    /// DASS DAS FUER JEDEN FANG GILT und nicht nur fuer den einen gemessenen
    /// Freibrief, ist im Sheet nachgesehen (Spielerfrage 2026-08-21, "kann man
    /// das verallgemeinern"): das Spiel fuehrt GENAU EINE Zaehm-Mechanik. Im
    /// LogMessage-Sheet stehen ihre Meldungen als geschlossener Block 1805-1809
    /// ("... wurde gezähmt. (/)", "... konnte nicht gezähmt werden und verfällt
    /// in Raserei.", "... ist bereits zahm.", "... ist in Raserei verfallen und
    /// lässt sich nicht beruhigen.", "... ist nicht mehr zahm"), und daneben
    /// liegen die beiden EINZIGEN Anleitungen dazu: 1837 fuer das Emote
    /// "Beruhigen" und 1838 fuer den Schluesselgegenstand. Zwei Wege hinein,
    /// eine Mechanik dahinter - und im Status-Sheet gibt es dazu nur dieses eine
    /// Paar.
    ///
    /// UNBELEGT BLEIBT genau ein Glied: dass auch der Weg ueber den
    /// Schluesselgegenstand (1838) denselben Status setzt. Gemessen ist nur der
    /// Emote-Weg. Da es keinen zweiten "zahm"-Status gibt, waere ein eigener
    /// Zustand fuer jenen Weg allerdings ein Sonderfall ohne Zeile im Sheet.
    /// </summary>
    private const uint TamedStatusId = 213;

    /// <summary>
    /// Der Status eines Gegners, an dem ein Besaenftigen MISSLUNGEN ist.
    ///
    /// Zeile 214 liegt im Sheet direkt neben 213 und gehoert sichtbar zu ihr:
    /// DE "Aufstachelung" / EN "Agitation", "Nach misslungener Besänftigung noch
    /// wilder als zuvor." / "Excited by failed pacification. Attack power and
    /// attack magic potency are enhanced." Symbol 216302 gegen 216301 - das Paar
    /// der Fang-Mechanik.
    ///
    /// WARUM ER MITGESPROCHEN WIRD: fuer den Spieler ist er genauso eine Absage
    /// wie "schon zahm", nur aus dem anderen Grund - das Spiel sagt dazu "ist in
    /// Raserei verfallen und lässt sich nicht beruhigen" (LogMessage 1808). Wer
    /// das nicht weiss, laeuft hin und verbraucht einen Versuch an einem Gegner,
    /// der gerade gar nicht zu fangen ist. Und er schlaegt dabei haerter zu.
    /// </summary>
    private const uint AgitatedStatusId = 214;

    /// <summary>
    /// ", schon gezaehmt", wenn der Gegner den Besaenftigungs-Status traegt.
    ///
    /// WARUM (Spielerwunsch 2026-08-21): ein Fang-Freibrief laesst mehr Gegner
    /// stehen, als er verlangt - "Im Namen des Fortschritts" bot elf Pyrit-Kobalos
    /// fuer vier Faenge. Ein gezaehmter verschwindet nicht, verliert keine HP und
    /// heisst weiter genauso; im Log vom 2026-08-21 ist der Spieler deshalb
    /// dreimal zu einem gelaufen, den er schon hatte, und hat es erst an der
    /// Abweisung "ist bereits zahm" gemerkt. Ein sehender Spieler sieht das
    /// Symbol ueber dem Gegner stehen, bevor er losgeht.
    ///
    /// BEWUSST NICHT hinter dem "Ziel angenommen"-Gatter, hinter dem Stufe und
    /// HP stehen: jene beiden stehen in der ZIEL-LEISTE, die bei einer Ablehnung
    /// leer bleibt. Der Besaenftigungs-Status haengt am Gegner selbst und ist
    /// ueber seinem Kopf zu sehen, ohne ihn anzuvisieren.
    ///
    /// ANNAHME, die erst der Test im Spiel bestaetigen kann: dass die
    /// Statusliste auch fuer einen Gegner gefuellt ist, der NICHT anvisiert ist.
    /// Trifft sie nicht zu, spricht die Ansage schlicht nichts - falsch wird sie
    /// dadurch nicht. Der Log-Trace der Freibrief-Gegner schreibt den Zustand je
    /// Gegner mit, dort ist es abzulesen.
    /// </summary>
    private static string DescribeTamed(IGameObject target) => TameRank(target) switch
    {
        TameRankTamed    => AccessibilityStrings.AlreadyTamed,
        TameRankAgitated => AccessibilityStrings.Agitated,
        _                => string.Empty,
    };

    // Wie brauchbar ein Gegner fuer einen Fang gerade ist - kleiner ist besser.
    // Nicht als Enum, weil der Wert unmittelbar als Sortierschluessel dient.
    private const int TameRankReady    = 0;   // nichts im Weg
    private const int TameRankAgitated = 1;   // gerade nicht, spaeter wieder
    private const int TameRankTamed    = 2;   // erledigt, zaehlt nicht noch einmal

    /// <summary>
    /// Der Fang-Zustand eines Gegners als Rang.
    ///
    /// Die Reihenfolge ist die der BRAUCHBARKEIT, nicht die der Schwere: ein
    /// aufgestachelter Gegner kommt VOR einen gezaehmten, weil seine Absage
    /// voruebergehend ist (der Status laeuft ab) und er danach wieder zaehlt -
    /// waehrend ein gezaehmter bereits gezaehlt HAT.
    ///
    /// Nur BattleChara fuehren eine Statusliste; alles andere ist damit immer
    /// <see cref="TameRankReady"/> und faellt aus Ansage und Sortierung heraus.
    /// </summary>
    private static int TameRank(IGameObject target)
    {
        if (target is not IBattleChara bc) return TameRankReady;

        foreach (var status in bc.StatusList)
        {
            // Zahm schlaegt aufgestachelt: sollte ein Gegner wider Erwarten
            // beides tragen, ist "schon erledigt" die Auskunft, die den Spieler
            // davon abhaelt, es noch einmal zu versuchen.
            if (status.StatusId == TamedStatusId) return TameRankTamed;
        }

        foreach (var status in bc.StatusList)
            if (status.StatusId == AgitatedStatusId) return TameRankAgitated;

        return TameRankReady;
    }

    /// <summary>Ob der Gegner den Besaenftigungs-Status traegt.</summary>
    private static bool IsTamed(IGameObject target) => TameRank(target) == TameRankTamed;

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
    /// Die gesprochene Richtung zu <paramref name="targetPos"/> - seit 2026-08-23
    /// eine HIMMELSRICHTUNG ("östlich"), nicht mehr "links"/"rechts".
    ///
    /// <para>
    /// Entscheidung des Users 2026-08-23, nachdem links und rechts vertauscht
    /// waren. Der Punkt ist nicht der behobene Dreher, sondern die Fehlerklasse:
    /// eine relative Angabe haengt an der Blickrichtung (und bei `MoveMode` 0
    /// zusaetzlich an der Kamera), eine Himmelsrichtung faellt allein aus der
    /// Positionsdifferenz. Sie KANN nicht auf die falsche Seite zeigen.
    /// Arbeitsteilung seither: die Sprache sagt, WO es liegt (absolut), der
    /// Peil-Ton fuehrt die Ausrichtung (relativ, Stereo) - siehe
    /// <see cref="RouteService.CompassAdjective"/>.
    /// </para>
    ///
    /// <para>
    /// <paramref name="caller"/> is filled in by the compiler and only used by
    /// the debug probe, so every direction announcement (object browser, quest
    /// goals, target change, /acc nav) can be traced back to its source in the
    /// log without a game target being required.
    /// </para>
    /// </summary>
    private string CalculateDirection(IGameObject player, Vector3 targetPos,
                                      [CallerMemberName] string caller = "")
    {
        var word = RouteService.CompassAdjective(player.Position, targetPos);
        // Weiter berechnet, aber nur noch fuer die Sonde: sie misst die Seite,
        // an der der PEIL-TON haengt, und der bleibt relativ.
        var angle = RelativeAngle(player, targetPos);
#if DEBUG
        var dx = targetPos.X - player.Position.X;
        var dz = targetPos.Z - player.Position.Z;

        // FIGUR GEGEN KAMERA - die Frage betrifft jetzt nur noch den PEIL-TON.
        //
        // Die gesprochene Richtung (`wort`) ist seit 2026-08-23 eine
        // Himmelsrichtung und haengt an gar keiner Blickrichtung mehr. Der Ton
        // aber schon: seine Stereoseite ist sin(relAngle), und `relAngle` rechnet
        // gegen `player.Rotation`, also gegen die FIGUR - waehrend die Bewegung
        // bei `MoveMode` 0 der KAMERA folgt (gemessen 2026-08-22).
        //
        // Deshalb stehen figurWort und kameraWort weiter nebeneinander: wo sie
        // sich unterscheiden, zieht der Ton auf eine andere Seite, als das Laufen
        // sie hinbringt. `kameraAb` sagt, wie weit beide auseinanderstehen.
        var figurWort = DirectionText(angle);
        var camFacing = FacingService.CameraFacing();
        var camAngle  = camFacing is { } cf
            ? Normalise180((cf - Math.Atan2(dx, dz)) * (180.0 / Math.PI))
            : double.NaN;
        var camWord   = double.IsNaN(camAngle) ? "?" : DirectionText(camAngle);
        var camOffset = camFacing is { } cf2
            ? Normalise180((player.Rotation - cf2) * (180.0 / Math.PI))
            : double.NaN;
        var moveMode  = _gameConfig.UiControl.TryGetUInt("MoveMode", out var mm) ? (int)mm : -1;

        _log.Info($"[NavDirProbe] {caller}: rot={player.Rotation:F3} dx={dx:F2} dz={dz:F2} " +
                  $"gesprochen='{word}' | tonWinkel={angle:F1} figurWort='{figurWort}' " +
                  $"kamera={camFacing?.ToString("F3") ?? "-"} " +
                  $"kameraWinkel={camAngle:F1} kameraWort='{camWord}' kameraAb={camOffset:F1} " +
                  $"moveMode={moveMode} {(figurWort != camWord ? "ABWEICHUNG" : "gleich")}");
#endif
        return word;
    }

    // Relativer Winkel Spieler-Blickrichtung -> Ziel: 0° = geradeaus,
    // positiv = rechts, negativ = links (so liest es RelativeDirection).
    //
    // DAS VORZEICHEN WAR BIS 2026-08-23 VERDREHT - links und rechts kamen
    // vertauscht heraus. Der Fehler traf ALLES, was hier hängt: die
    // Richtungsansagen, die Gehhilfe und den Peil-Ton (dessen Stereoseite ist
    // sin(relAngle)). Genau deshalb meldeten die Spieler beides zusammen als
    // falsch - es war nie ein Ton- und ein Sprachfehler, sondern dieser eine.
    //
    // WARUM ES VERDREHT WAR, hergeleitet aus der Konvention, auf der dieselbe
    // Datei schon beruhte:
    //   - Blickvektor = (sin(rot), cos(rot)) in XZ, verifiziert aus Live-Log
    //     2026-07-10 (Details: docs/game-api.md).
    //   - Norden ist -Z, Osten +X. Steht so in RouteService.SectorOf
    //     (atan2(dx, -dz), 0 = Norden) und speist die Himmelsrichtungsansagen.
    //   - Daraus folgt zwingend: rot = 0 blickt nach +Z, also nach SÜDEN
    //     (HeadingSector(0) = SectorOf(0, 1) = atan2(0, -1) = 180° = Süden).
    // Ein Ziel im Osten (dx > 0) ergab mit `atan2 - rot` ein PLUS, also
    // "rechts". Wer nach Süden blickt, hat Osten aber LINKS. Darum steht die
    // Differenz jetzt andersherum.
    //
    // Der Beacon-Hörtest vom 2026-07-10, auf den sich der alte Kommentar hier
    // berief, hat das Vorzeichen NICHT abgesichert - er kann die Seite nur
    // bestätigt haben, wenn dabei nach Norden geblickt wurde, denn nur dann
    // fallen beide Vorzeichen zusammen. In-game gemessen und gemeldet vom User
    // 2026-08-23: *"wenn ich nach links laufe wird weniger und nach rechts mehr,
    // links und rechts ist vertauscht"*.
    //
    // Gegengerechnet an fünf Lagen (Blick Süd/Ziel Ost -> links, Blick Süd/Ziel
    // West -> rechts, Ziel voraus -> geradeaus, Ziel hinten -> hinten, Blick
    // Ost/Ziel Süd -> rechts).
    internal static double RelativeAngle(IGameObject player, Vector3 targetPos)
    {
        var playerPos = player.Position;
        var dx = targetPos.X - playerPos.X;
        var dz = targetPos.Z - playerPos.Z;

        return Normalise180((player.Rotation - Math.Atan2(dx, dz)) * (180.0 / Math.PI));
    }

    /// <summary>Folds an angle in DEGREES into [-180, 180], so "left" and "right"
    /// stay on the sides they belong to across the seam.</summary>
    private static double Normalise180(double degrees)
    {
        if (degrees > 180) degrees -= 360;
        if (degrees < -180) degrees += 360;
        return degrees;
    }

    private static string DirectionText(double relativeAngle) =>
        AccessibilityStrings.RelativeDirection(relativeAngle);

    private static string FormatDistance(float distance) =>
        AccessibilityStrings.FormatDistance(distance);
}
