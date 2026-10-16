using System.Reflection;
using System.Numerics;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Ipc;
using System.Text.Json;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length is < 2 or > 3) throw new ArgumentException("CrossZoneCheck <game/sqpack> <report.json> [coverage.md]");
using var game = new GameData(args[0]);
var data = new GameDataReader(game);
var log = DispatchProxy.Create<IPluginLog, QuietLog>();
var client = DispatchProxy.Create<IClientState, ZoneState>();
var state = (ZoneState)client;
var places = new PlacesService(data, client, log);
var entrances = new InteriorEntranceService(data, log).All;
Loc.Mode = LanguageMode.Russian;
var checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
Check(entrances.Count > 0, "No entrance links were loaded.");
var guard = entrances.Single(e => e.SourceMapId == 2 && e.TargetMapId == 68 && e.BaseId == 1000423);
var level = data.GetExcelSheet<Level>().GetRow(3861082);
Check(guard.Position == new System.Numerics.Vector3(level.X, level.Y, level.Z), "Guard source position lost.");
Check(data.GetExcelSheet<Quest>().GetRow(65983).QuestListenerParams.Any(p => p.Listener == guard.BaseId),
    "Recorded quest no longer links to this guard.");
state.Map = 2; state.Territory = 132;
var first = places.FindFirstHopToMap(68, out var hops);
Check(first != null && first.Position == guard.Position && hops == 1, "Recorded New Gridania -> First Bow route failed.");
Check(first is { IsZoneTransition: false }, "Interaction entrance must not trigger a border push.");
Check(places.GetHopDistances().GetValueOrDefault(68u) == 1, "Route ranking disagrees with the entrance route.");
var outside = places.FindFirstHopToMap(4, out var outsideHops);
Check(outside is { IsZoneTransition: true, TargetMapId: 4 } && outsideHops == 1, "Ordinary Central Shroud exit changed.");
state.Map = 3; state.Territory = 133;
var fromOldGridania = places.FindFirstHopToMap(68, out var oldHops);
Check(fromOldGridania is { IsZoneTransition: true, TargetMapId: 2 } && oldHops == 2, "Multi-zone entrance route failed.");
Check(places.FindLocalEntranceToMap(68) == null, "Foreign source entrance leaked across zones.");
state.Map = 68; state.Territory = 204;
var exit = places.FindFirstHopToMap(2, out var exitHops);
var exitObject = entrances.Single(e => e.SourceMapId == 68 && e.TargetMapId == 2 && e.BaseId == 2001215);
Check(exit != null && exit.Position == exitObject.Position && exitHops == 1, "Native EObj -> Warp exit failed.");
Check(places.FindFirstHopToMap(68, out _) == null, "Same-map target created an unnecessary entrance.");
Check(places.FindFirstHopToMap(0, out _) == null, "Unknown map created a route.");
Check(places.FindFirstHopToMap(uint.MaxValue, out _) == null, "Missing map created a route.");
var checkedSources = 0;
state.Map = 3; state.Territory = 133;
var lotusGuard = entrances.Single(e => e.SourceMapId == 3 && e.TargetMapId == 69 && e.BaseId == 1000460);
var lotus = places.FindFirstHopToMap(69, out var lotusHops);
Check(lotus is { TypeLabel: "Entrance", IsZoneTransition: false } && lotus.Position == lotusGuard.Position && lotusHops == 1,
    "Recorded Old Gridania -> Lotus guard route failed.");
state.Map = 28; state.Territory = 179;
var fromInn = places.FindFirstHopToMap(69, out var innHops);
var innDoor = places.FindLocalEntranceOnRoute(69);
Check(fromInn is { TypeLabel: "Entrance", TargetMapId: 2 } && innHops == 3,
    "Recorded inn -> New Gridania -> Old Gridania -> Lotus route failed.");
Check(innDoor is { BaseId: 2000087, LevelType: 45 } && fromInn?.Position == innDoor.Position,
    "Inn route did not select the exact local exit door.");
Check(places.GetHopDistances().GetValueOrDefault(69u) == innHops, "Inn route/ranking disagreement.");
state.Map = 2; state.Territory = 132;
var airship = places.FindFirstHopToMap(74, out var airshipHops);
var airshipNpc = places.FindLocalEntranceOnRoute(74);
Check(airship is { TypeLabel: "Entrance", TargetMapId: 74, IsZoneTransition: false }
    && airshipNpc is { BaseId: 1000106 } && airshipHops == 1,
    "Recorded Gridanian Envoy route must lead to Lionnellais for passage to Limsa's landing.");
var rogueTrip = places.FindFirstHopToMap(548, out var rogueHops);
Check(rogueTrip?.Position == airship?.Position && rogueHops > 1,
    "Recorded Stabbers in Yer Fambles must start with the airship, not refuse another area.");
state.Map = 74; state.Territory = 128;
var lift = places.FindFirstHopToMap(548, out var liftHops);
Check(lift != null && liftHops < rogueHops && places.FindLocalEntranceOnRoute(548) != null,
    "Arrival must continue through the local lift towards the Rogues' Guild.");
state.Map = 12; state.Territory = 129;
var guild = places.FindLocalEntranceOnRoute(548);
Check(guild is { BaseId: 1009944, TargetMapId: 548 }, "Same-territory guild route must select Lonwoerd.");
state.Map = 548; state.Territory = 129;
var guildExit = places.FindLocalEntranceOnRoute(2);
Check(guildExit is { BaseId: 2004936, TargetMapId: 12 }, "Return travel must leave through the local guild door.");
var landingRange = new TravelLayout(data, log).ForTerritory(128);
Check(landingRange.MapAt(new(-12.6917f, 91.4999f, -7.60297f)) == 74,
    "Airship PopRange's old map11 must be corrected by the actual landing range.");
Check(landingRange.Arrivals.ContainsKey(4158063), "Lift arrival missing from Level must be obtained from layout.");
// Real tall ExitRange from the reported 2026-10-07 failure. Mesh replies below
// are simulated from the logged edge, not a live vnavmesh query.
var travelLayouts = new TravelLayout(data, log);
// New 6.08.112 log: repeatedly "arrived" here without crossing. The old
// mirrored rotation admitted this point, but the game's rotation rejects it.
var middleLimsaBorder = travelLayouts.ForTerritory(134).Borders.Single(b => b.Destination == 129);
var loggedFalseArrival = new Vector3(-62.839893f, 42.263397f, 154.52989f);
Check(!middleLimsaBorder.Contains(loggedFalseArrival), "Recorded false arrival is outside the actual rotated exit.");
var nativeFalseLocal = Vector3.Transform(loggedFalseArrival - middleLimsaBorder.Centre,
    Matrix4x4.CreateRotationY(-middleLimsaBorder.Yaw));
Check(MathF.Abs(nativeFalseLocal.X) > middleLimsaBorder.HalfExtent.X,
    "Recorded false arrival no longer reproduces the independent game-space check.");
var laNosceaBorder = travelLayouts.ForTerritory(134).Borders.Single(b => b.Destination == 135);
var loggedEdge = new Vector3(207, 71.75f, 275);
var loggedPlayer = new Vector3(207.14691f, 71.745f, 274.9847f);
Vector3? LoggedMesh(Vector3 p, float xz, float y) => MathF.Abs(p.X - loggedEdge.X) <= xz
    && MathF.Abs(p.Z - loggedEdge.Z) <= xz && MathF.Abs(p.Y - loggedEdge.Y) <= y ? loggedEdge : null;
var recordedBorder = ZoneBorderService.Resolve([laNosceaBorder], loggedPlayer, LoggedMesh);
Check(recordedBorder.Position is { } goal && laNosceaBorder.Contains(goal)
    && MathF.Abs(goal.Y - loggedEdge.Y) < 0.01f && Vector3.Distance(goal, loggedEdge) <= 5.5f,
    "Reported Middle -> Lower La Noscea border must retain road height and a bounded final approach.");
state.Territory = 134;
state.Map = data.GetExcelSheet<Map>().First(m => m.TerritoryType.RowId == 134).RowId;
var lowerMap = data.GetExcelSheet<Map>().First(m => m.TerritoryType.RowId == 135).RowId;
var selectedBorder = new PlaceDestination("Переход в Нижняя Ла-Носкея", "Переход", new(208, 0, 288), true, lowerMap);
// Exercise the actual shared resolver used by both movement modes and preview.
// Generic snapping returns the logged bad point: it must not be used.
var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
var navigation = (NavigationService)RuntimeHelpers.GetUninitializedObject(typeof(NavigationService));
typeof(NavigationService).GetProperty("SelectedPlaceDestination")!.SetValue(navigation, selectedBorder);
var nav = (NavmeshIpc)RuntimeHelpers.GetUninitializedObject(typeof(NavmeshIpc));
var walk = (AutoWalkService)RuntimeHelpers.GetUninitializedObject(typeof(AutoWalkService));
void Set(object instance, string field, object value) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
Set(nav, "_log", log);
Set(nav, "_isReady", GateProxy.Of<ICallGateSubscriber<bool>>((_, _) => true));
Set(nav, "_nearestPointReachable", GateProxy.Of<ICallGateSubscriber<Vector3, float, float, Vector3?>>((_, a) =>
    (float)a![1]! <= 6 ? LoggedMesh((Vector3)a[0]!, (float)a[1]!, (float)a[2]!) : new Vector3(209.70435f, 91.5f, 278.70197f)));
Set(walk, "_nav", nav); Set(walk, "_log", log);
var player = GateProxy.Of<IPlayerCharacter>((m, _) => m.Name == "get_Position" ? loggedPlayer : throw new NotSupportedException(m.Name));
typeof(Plugin).GetProperty("ObjectTable", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin,
    GateProxy.Of<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? player : throw new NotSupportedException(m.Name)));
Set(plugin, "_navigation", navigation); Set(plugin, "_autoWalk", walk);
Set(plugin, "_places", places);
Set(plugin, "_zoneBorders", new ZoneBorderService(data, client, log));
Set(plugin, "_config", new Configuration());
var resolve = typeof(Plugin).GetMethod("TryResolveSelectedDestination", BindingFlags.Instance | BindingFlags.NonPublic)!;
for (var attempt = 0; attempt < 3; attempt++)
{
    object?[] parameters = [default(Vector3), "", 0f, false, false, false];
    Check(resolve.Invoke(plugin, parameters)!.ToString() == "Resolved", "Shared border resolver refused logged road.");
    Check((Vector3)parameters[0]! == recordedBorder.Position && (bool)parameters[4]!,
        "Border destination was broadly snapped outside the trigger after validation.");
}
var borderVolumes = 0;
var tallBorders = 0;
foreach (var territoryId in data.GetExcelSheet<Map>().Select(m => m.TerritoryType.RowId).Where(id => id != 0).Distinct())
foreach (var border in travelLayouts.ForTerritory(territoryId).Borders)
{
    borderVolumes++;
    if (border.HalfExtent.Y > 10) tallBorders++;
    foreach (var height in new[] { -border.HalfExtent.Y + 0.1f, 0f, border.HalfExtent.Y - 0.1f })
    {
        var from = border.World(new(-border.HalfExtent.X - 10, height, 0));
        Check(ZoneBorderService.Candidates(border, from).All(border.Contains), "Border candidates escaped their real volume.");
        foreach (var point in ZoneBorderService.Candidates(border, from))
        {
            var nativeLocal = Vector3.Transform(point - border.Centre, Matrix4x4.CreateRotationY(-border.Yaw));
            Check(MathF.Abs(nativeLocal.X) <= border.HalfExtent.X + 0.0001f
                && MathF.Abs(nativeLocal.Y) <= border.HalfExtent.Y + 0.0001f
                && MathF.Abs(nativeLocal.Z) <= border.HalfExtent.Z + 0.0001f,
                "Border candidate failed independent game transform.");
        }
    }
}
Console.WriteLine($"Border audit: {borderVolumes} box volumes ({tallBorders} tall); recorded goal {recordedBorder.Position}; shared movement resolver passed. Mesh simulation, no live movement.");
// The reported Type-51 objective is a search circle whose marker height is
// 15.6 metres below the mesh point. Unlike an actor, it has no exact floor.
var areaLevel = data.GetExcelSheet<Level>().GetRow(5058426);
Check(areaLevel.Type == 51 && areaLevel.Object.RowId == 0 && areaLevel.Territory.RowId == 135,
    "Recorded Way of the Culinarian objective changed its marker identity.");
var recordedArea = new QuestDestination("Дилемма изящного вкуса", "",
    // Use the native MarkerData.Radius logged in FFXIV. Level.Radius=303
    // is a different field and must not replace the visible 35-metre circle.
    new(areaLevel.X, areaLevel.Y, areaLevel.Z), 35, 135, 16, true, QuestKind.Job, 5,
    TargetLevelType: areaLevel.Type, QuestId: 65646, ObjectiveLevelId: areaLevel.RowId);
Check(MathF.Abs(recordedArea.Position.X - 205.9f) < 0.1f && MathF.Abs(recordedArea.Position.Y - 32.5f) < 0.1f
    && MathF.Abs(recordedArea.Position.Z - 40.7f) < 0.1f && areaLevel.Map.RowId == 16,
    "Recorded search-circle position no longer matches the log.");
var loggedAreaFloor = new Vector3(205.8f, 48.1f, 40.7f);
state.Map = 16; state.Territory = 135;
Set(plugin, "_places", places);
typeof(Plugin).GetProperty("Log", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, log);
Set(nav, "_nearestPointReachable", GateProxy.Of<ICallGateSubscriber<Vector3, float, float, Vector3?>>((_, _) => loggedAreaFloor));
var areaPlan = QuestNavigationPlan.Resolve(recordedArea, 135, 16, _ => null, _ => null)!;
Check(areaPlan.HeightIsGuess, "A search circle must not claim its height is an actor floor.");
object?[] areaArguments = [recordedArea, areaPlan, areaPlan.Position, 5f];
var sharedAreaFloor = typeof(Plugin).GetMethod("ResolveQuestGroundPoint", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(plugin, areaArguments);
Check(sharedAreaFloor is Vector3 resolvedArea && resolvedArea == loggedAreaFloor,
    "Shared quest resolver refused the logged search-circle ground.");
Check(places.MatchesKnownMap(loggedAreaFloor, 16), "Logged area floor moved to a different physical map.");
Check(!places.MatchesKnownMap(new(-12.6917f, 91.4999f, -7.60297f), 11),
    "Landing floor must not be accepted as upper decks.");
var areaRows = data.GetExcelSheet<Level>().Where(l => l.Type == 51 && l.Object.RowId == 0
    && l.Territory.RowId != 0 && l.Map.RowId != 0 && TravelLayout.Finite(new(l.X, l.Y, l.Z))).ToArray();
foreach (var row in areaRows)
{
    var centre = new Vector3(row.X, row.Y, row.Z);
    Check(QuestAreaPoint.IsSearchArea(recordedArea with { Position = centre, ObjectiveLevelId = row.RowId }),
        "Type-51 area lost its navigation classification.");
    // Uniform simulated native radius at all real positions: live radii are
    // unavailable in this offline sheet scan.
    var projected = QuestAreaPoint.Resolve(centre, 35, 5, (p, _, _) => p with { Y = centre.Y + 15.6f }, _ => true);
    Check(projected is { } pt && Vector2.Distance(new(centre.X, centre.Z), new(pt.X, pt.Z)) + 5 < 35,
        "Generic search area did not keep its stop inside the circle.");
}
Console.WriteLine($"Search-circle audit: {areaRows.Length} Type-51 rows; logged objective65646 ground {sharedAreaFloor}. Mesh simulation, no live movement.");
Console.WriteLine($"Recorded travel: {airship?.Name} (NPC {airshipNpc?.BaseId}); {rogueHops} legs to guild; landing next: {lift?.Name}; guild NPC {guild?.BaseId}.");
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
var checkedMaps = 0;
var service = new InteriorEntranceService(data, log);
foreach (var map in data.GetExcelSheet<Map>())
{
    if (map.RowId == 0 || map.TerritoryType.RowId == 0) continue;
    service.ForMap(map.RowId);
    var canonical = data.GetExcelSheet<Map>().GetRow(places.CanonicalMap(map.RowId));
    Check(map.TerritoryType.RowId == canonical.TerritoryType.RowId && map.Id.ExtractText() == canonical.Id.ExtractText()
        && map.SizeFactor == canonical.SizeFactor && map.OffsetX == canonical.OffsetX && map.OffsetY == canonical.OffsetY,
        "Physical map aliases must never merge different zones or floors.");
    checkedMaps++;
}
var allEntrances = service.All.ToArray();
var allTransfers = data.GetExcelSheet<Map>().Where(m => m.RowId != 0 && m.TerritoryType.RowId != 0)
    .SelectMany(m => service.LocalTransfers(m.RowId)).Distinct().ToArray();
var airportGate = allTransfers.Single(t => t.MapId == 70 && t.BaseId == 1004434 && t.WarpId == 131128);
Check(airportGate.Position != airportGate.Arrival, "Airport gate lost its separate arrival side.");
state.Map = 13; state.Territory = 130;
var uldahUpper = places.FindFirstHopToMap(73, out var upperHops);
Check(uldahUpper is { IsZoneTransition: false, TargetMapId: 73 }
    && places.FindLocalEntranceOnRoute(73) is { BaseId: 1001834 },
    "Ul'dah upper floor must use the exact lift, not a border arriving on map14.");
Check(travelLayouts.WalkingBorders(13).Any(b => b.TargetMap == 14)
    && !travelLayouts.WalkingBorders(13).Any(b => b.TargetMap == 73)
    && travelLayouts.WalkingBorders(70).Count == 0, "Walking exit arrival/source floor mismatch.");
stopwatch.Stop();
Check(allEntrances.Any(e => e.SourceMapId == 28 && e.BaseId == 2000087), "Full layout scan lost the inn door.");
foreach (var group in allEntrances.GroupBy(e => e.SourceMapId))
{
    state.Map = group.Key;
    state.Territory = (ushort)places.GetTerritoryOfMap(group.Key);
    foreach (var entrance in group)
    {
        var route = places.FindFirstHopToMap(entrance.TargetMapId, out var count);
        Check(route != null && count == 1, $"Direct entrance missing: {group.Key} -> {entrance.TargetMapId}");
        if (route is { IsZoneTransition: false })
            Check(group.Any(e => e.TargetMapId == entrance.TargetMapId && e.Position == route.Position), "Entrance source mismatch.");
        Check(places.GetHopDistances().GetValueOrDefault(entrance.TargetMapId) == count, "Full route/ranking disagreement.");
    }
    checkedSources++;
}
// Audit every Quest actor/object constant with a typed, located Level row.
// These include all phases; they are coverage evidence, not live quest runs.
var questActors = data.GetExcelSheet<Quest>().SelectMany(q => q.QuestParams)
    .Where(p => p.ScriptInstruction.ExtractText().StartsWith("ACTOR", StringComparison.Ordinal)
        || p.ScriptInstruction.ExtractText().StartsWith("EOBJECT", StringComparison.Ordinal))
    .Select(p => p.ScriptArg).Where(id => id != 0).ToHashSet();
var targetLevels = data.GetExcelSheet<Level>().Where(l => l.Type is 8 or 45 && questActors.Contains(l.Object.RowId)
    && l.Map.RowId != 0 && l.Territory.RowId != 0).ToArray();
state.Map = 2; state.Territory = 132;
var reachable = places.GetHopDistances();
var targetMaps = targetLevels.Select(l => l.Map.RowId).Distinct().ToArray();
var unreachable = targetMaps.Where(id => !reachable.ContainsKey(places.CanonicalMap(id))).ToArray();
var teleportMaps = data.GetExcelSheet<Aetheryte>().Where(a => a.IsAetheryte && !a.Invisible).Select(a => places.CanonicalMap(a.Map.RowId)).ToHashSet();
var withTeleport = unreachable.Where(id => places.GetHopDistancesTo(id).Keys.Any(teleportMaps.Contains)).ToArray();
var duties = new DutyEntranceService(data, client, places, log);
var withDuty = unreachable.Except(withTeleport).Where(id =>
{
    var target = targetLevels.First(l => l.Map.RowId == id);
    var goal = new QuestDestination("audit", "", new(target.X, target.Y, target.Z), 1,
        (ushort)target.Territory.RowId, id, false, QuestKind.Unknown, 0);
    var duty = QuestTravelHints.FindDuty(data, duties, places, goal, state.Map);
    if (duty == null) return false;
    var travel = QuestTravelHints.DutyGoal(goal, duty, state.Map);
    Check(travel.TargetBaseId != 0 && travel.TargetLevelType == 45
        && data.GetExcelSheet<EObj>().TryGetRow(travel.TargetBaseId, out _), "Duty route lost exact external door.");
    return reachable.ContainsKey(places.CanonicalMap(travel.MapId)) || places.GetHopDistancesTo(travel.MapId).Keys.Any(teleportMaps.Contains);
}).ToArray();
foreach(var id in targetMaps.Where(id => reachable.ContainsKey(places.CanonicalMap(id))))
{
    if (id == 2) continue;
    var firstHop = places.FindFirstHopToMap(id, out var count);
    Check(firstHop != null && count == reachable[places.CanonicalMap(id)], $"Quest actor map {id} route/ranking disagreement.");
}
var withDutyFinder = unreachable.Except(withTeleport).Except(withDuty).Where(id =>
    data.GetExcelSheet<InstanceContent>().Count(i => i.ContentFinderCondition.ValueNullable?.TerritoryType.RowId == places.GetTerritoryOfMap(id)) == 1).ToArray();
var stillUnmapped = unreachable.Except(withTeleport).Except(withDuty).Except(withDutyFinder).Select(id => new { Map = id,
    Territory = places.GetTerritoryOfMap(id), Name = places.GetMapName(id) }).ToArray();
// Preserve every unresolved destination with its actual client-side evidence.
// Warp arrivals are known values, but are not an invented source actor/link.
var unresolvedEvidence = stillUnmapped.Select(m =>
{
    var canonical = places.CanonicalMap(m.Map);
    var arrivals = data.GetExcelSheet<Warp>().Where(w => w.TerritoryType.RowId == m.Territory
        && travelLayouts.ArrivalMap(m.Territory, w.PopRange.RowId) == canonical)
        .Select(w => new { Warp = w.RowId, PopRange = w.PopRange.RowId }).ToArray();
    var incoming = allEntrances.Where(e => e.TargetMapId == canonical).ToArray();
    var instances = data.GetExcelSheet<InstanceContent>()
        .Where(i => i.ContentFinderCondition.ValueNullable?.TerritoryType.RowId == m.Territory)
        .Select(i => i.RowId).ToArray();
    var physical = targetLevels.Where(l => l.Map.RowId == m.Map)
        .Select(l => places.CanonicalMap(travelLayouts.ForTerritory(m.Territory)
            .ResolveMap(new(l.X, l.Y, l.Z), l.Map.RowId))).Distinct().Order().ToArray();
    var reason = m.Territory == 1 ? "Обзорная карта; не физическое место входа"
        : incoming.Length > 0 ? "Есть вход внутри отдельной цепочки; внешняя связь не подтверждена"
        : instances.Length > 1 ? "Несколько записей испытания; текущая доступная запись не установлена"
        : arrivals.Length > 0 ? "Прибытие известно; исходный NPC/объект или сценарная связь не подтверждены"
        : "Клиентская связь входа Warp отсутствует; нужен действующий сценарий игры";
    return new { m.Map, CanonicalMap = canonical, m.Territory, m.Name, PhysicalMaps = physical,
        Arrivals = arrivals, IncomingEntrances = incoming, InstanceContents = instances, Reason = reason };
}).ToArray();
var coverageDoc = new System.Text.StringBuilder("# Неподтверждённые входы навигации\n\n"
    + "Таблица воспроизводится `tools/cross-zone-check` из установленных игровых таблиц и файлов зон. "
    + "Номера Warp/PopRange ниже являются точными значениями прибытия. Они не доказывают исходный вход, "
    + "доступность транспорта, текущий этап задания или работающий путь. Повторные строки одной карты сохранены для сверки; "
    + "это не число сломанных квестов. Плагин не угадывает недостающие связи.\n\n"
    + "В версии 6.08.114 дополнительно восстановлены 82 перемещения NPC/объектами внутри одной физической карты "
    + "и точные исходные/конечные этажи 125 пеших объёмов. Следующие строки ещё требуют живых сведений.\n\n"
    + "| Карта (физическая) | Территория | Название | Warp → PopRange | Что ещё неизвестно |\n"
    + "| --- | --- | --- | --- | --- |\n");
foreach (var item in unresolvedEvidence)
    coverageDoc.AppendLine($"| {item.Map} ({item.CanonicalMap}) | {item.Territory} | {item.Name.Replace("|", "/")} | "
        + $"{(item.Arrivals.Length == 0 ? "не задан" : string.Join(", ", item.Arrivals.Select(a => $"{a.Warp} → {a.PopRange}")))} | {item.Reason} |");
if (args.Length >= 3) File.WriteAllText(args[2], coverageDoc.ToString());
// Check route materialization from EVERY source row, including physical-map
// aliases, rather than only one starting city. A graph edge is not useful if
// the first local action cannot be selected, or the next leg makes no progress.
var routePairs = 0;
var routeFailures = new List<string>();
void AuditRoute(bool ok, string label) { checks++; if (!ok) routeFailures.Add(label); }
var mapRows = data.GetExcelSheet<Map>().Where(m => m.RowId != 0 && m.TerritoryType.RowId != 0).ToArray();
foreach (var source in mapRows)
{
    state.Map = source.RowId; state.Territory = (ushort)source.TerritoryType.RowId;
    var distances = places.GetHopDistances();
    foreach (var destination in distances.Where(p => p.Value > 0))
    {
        routePairs++;
        var hop = places.FindFirstHopToMap(destination.Key, out var count);
        AuditRoute(hop != null && count == destination.Value && TravelLayout.Finite(hop.Position),
            $"{source.RowId}->{destination.Key}: missing local action (canonical={places.CanonicalMap(source.RowId)}, source={places.GetMapName(source.RowId)}, range={source.MapMarkerRange}, distance={destination.Value}, count={count})");
        if (hop == null) continue;
        var remaining = places.GetHopDistancesTo(destination.Key);
        AuditRoute(remaining.TryGetValue(places.CanonicalMap(hop.TargetMapId), out var rest) && rest == count - 1,
            $"{source.RowId}->{destination.Key}: next map {hop.TargetMapId} does not reduce remaining distance");
        var entrance = places.FindLocalEntranceOnRoute(destination.Key);
        AuditRoute(entrance == null || entrance.SourceMapId == places.CanonicalMap(source.RowId)
            && entrance.TargetMapId == places.CanonicalMap(hop.TargetMapId) && entrance.Position == hop.Position && !hop.IsZoneTransition,
            $"{source.RowId}->{destination.Key}: interaction identity differs from first local action");
        var auditGoal = new QuestDestination("route audit", "", Vector3.Zero, 1,
            (ushort)places.GetTerritoryOfMap(destination.Key), destination.Key, false, QuestKind.Unknown, 0);
        var plan = QuestNavigationPlan.Resolve(auditGoal, state.Territory, state.Map, _ => hop, _ => entrance, places.AreSameMap);
        AuditRoute(plan != null && plan.Position == hop.Position && plan.NextMapId == hop.TargetMapId
            && plan.Entrance == entrance && plan.IsBorder == hop.IsZoneTransition,
            $"{source.RowId}->{destination.Key}: quest plan differs from the selected first action");
    }
}
// Keep the real alternate-row failure named separately in the report.
state.Map = 1231; state.Territory = (ushort)places.GetTerritoryOfMap(state.Map);
var alternateMapExit = places.FindFirstHopToMap(784, out var alternateMapHops);
Check(places.AreSameMap(1231, 786) && alternateMapExit != null && alternateMapHops == 1,
    "Zero's Domain alternate row must materialize the exit found on the same physical map.");
Console.WriteLine($"All-map route audit: {mapRows.Length} source rows, {routePairs} reachable pairs, {routeFailures.Count} local-action failures. No live pathfinding.");
var allRoutesPassed = routeFailures.Count == 0;
checks++;
File.WriteAllText(args[1], JsonSerializer.Serialize(new { Passed = allRoutesPassed, Checks = checks,
    Entrances = allEntrances.Length, SourceMaps = checkedSources, MapsScanned = checkedMaps,
    FullScanMs = stopwatch.ElapsedMilliseconds, RecordedQuests = new[] { 65983, 65985, 66043, 65640, 65646 },
    Guard = guard, LotusGuard = lotusGuard, InnDoor = innDoor, AllEntrances = allEntrances,
    LocalTransfers = allTransfers, RecordedAirportGate = airportGate, RecordedUldahUpperLift = uldahUpper,
    BorderVolumes = borderVolumes, TallBorderVolumes = tallBorders,
    RecordedLaNosceaBorder = laNosceaBorder, RecordedLaNosceaDestination = recordedBorder.Position,
    LoggedMeshEdgeSimulated = true, SharedBorderResolverChecked = true,
    RecordedFalseArrival = loggedFalseArrival, RecordedFalseArrivalLocal = nativeFalseLocal,
    MirroredBorderRotationRejected = true,
    SearchCircleLevelRows = areaRows.Length, SearchCircleAuditRadiusSimulated = 35, RecordedSearchCircle = recordedArea,
    RecordedSearchCircleFloor = sharedAreaFloor, SharedAreaResolverChecked = true, LoggedAreaMeshSimulated = true,
    QuestActorLevels = targetLevels.Length, QuestActorMaps = targetMaps.Length,
    ReachableQuestActorMapsFromGridania = targetMaps.Length - unreachable.Length,
    AdditionalMapsWithPossibleTeleport = withTeleport.Length,
    AdditionalMapsViaVerifiedDutyEntrance = withDuty.Length,
    AdditionalMapsWithUniqueDutyFinder = withDutyFinder.Length, DutyFinderActuallyUnlockedChecked = false,
    UnmappedQuestActorMaps = stillUnmapped, TeleportsActuallyUnlockedChecked = false,
    UnmappedEvidence = unresolvedEvidence,
    AllMapRouteSourceRows = mapRows.Length, AllMapReachableRoutePairs = routePairs, AllMapLocalActionFailures = routeFailures,
    AlternateMapExit = alternateMapExit, AllGameNavigationVerified = false,
    NativeFfxivChecked = false }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
if (!allRoutesPassed) throw new Exception($"All-map route failures: {routeFailures.Count}; inspect {args[1]}.");
Console.WriteLine($"PASS: {checks} assertions, {allEntrances.Length} verified entrances in {checkedSources} source maps, {checkedMaps} maps scanned ({stopwatch.ElapsedMilliseconds} ms); five recorded quests and ordinary exits. Live game not tested.");

public class QuietLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.ReturnType == typeof(void)
        ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
}
public class ZoneState : DispatchProxy
{
    public uint Map;
    public ushort Territory;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    { "get_MapId" => Map, "get_TerritoryType" => Territory, _ => throw new NotSupportedException(method.Name) };
}

public class GateProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?> _call = null!;
    public static T Of<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
    {
        var value = Create<T, GateProxy>(); ((GateProxy)(object)value)._call = call; return value;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => _call(method!, args);
}
