// ══ ANSCHLUSSPUNKTE DES TIEFEN GEWOELBES — Landkarte fuer den naechsten Merge ══
//
// Sieben neue Dateien (Services/DeepDungeon*.cs) haengen an sieben vorhandenen. JEDER
// Eingriff in eine vorhandene Datei ist mit `[Tiefes Gewoelbe]` markiert, ein
// `grep -rn "\[Tiefes Gewoelbe\]" FF14Accessibility/` findet sie also alle. Jeder ist
// ADDITIV: es wurde keine bestehende Zeile umgeschrieben, und keine Signatur hat sich
// geaendert.
//
//   NavigationService  drei NavCategory-Werte; WorldCategories (nur UMBENANNT von
//                      Categories, damit die neue Property Categories zwischen dem
//                      Welt- und dem Gewoelbe-Satz waehlen kann); die Property
//                      DeepDungeon; je ein Zweig in Update, AnnounceCategoryCount,
//                      CycleObject, DescribeObject und GetCategoryObjects; die Methode
//                      CycleDeepRoom.
//   UIReaderService    die Properties DeepDungeonPanel und DeepDungeonFloor; eine Zeile
//                      in der Fokus-Kette; ein Listener auf DeepDungeonResult;
//                      TryReadDeepPanelDetail in der Kette von ReadCurrentFocus.
//   AutoWalkService    die Property Navmesh (gibt die vorhandene NavmeshIpc-Instanz
//                      heraus, statt eine zweite zu oeffnen) und ResolveReachablePoint.
//   NavmeshIpc         Nav.Rebuild.
//   Configuration      KeyDeepFloor, AnnounceDeepRoomChange.
//   Plugin             der PluginService ISeStringEvaluator, sechs Felder, der Aufbau
//                      samt Verdrahtung, die Ebenen-Taste, AnnounceDeepFloor.
//   AccessibilityStrings  der Abschnitt "Tiefes Gewoelbe" am Ende.
//
// WORAUF BEIM MERGE ZU ACHTEN IST:
//
//   1. `Categories` in NavigationService ist jetzt eine PROPERTY und nicht mehr das
//      statische Feld. Wer das Feld anfasst, muss WorldCategories anfassen; wer die
//      gerade gueltige Liste braucht, nimmt weiter Categories. Alle vorhandenen
//      Leser (IsQuestCategory und die anderen) sind unveraendert geblieben.
//   2. Alles ist null-vertraeglich verdrahtet. Wird eine der Properties nicht gesetzt,
//      verhaelt sich die Datei exakt wie vorher - es gibt keinen Pfad, der eine
//      Gewoelbe-Klasse voraussetzt.
//   3. Ausserhalb eines Tiefen Gewoelbes ist jeder neue Zweig durch
//      `DeepDungeon?.IsActive != true` bzw. einen null-Director stillgelegt.
//   4. Nav.Rebuild wird ausschliesslich bei einem EBENENWECHSEL innerhalb eines
//      Gewoelbes gerufen - siehe den Kopf von DeepDungeonMesh.cs fuer den Grund und
//      fuer die Aufstellung, was dadurch nicht kaputtgehen kann.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using DirectorRoomFlags = FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentDeepDungeon.RoomFlags;

namespace FF14Accessibility.Services;

/// <summary>
/// DER BROWSER FUER DAS TIEFE GEWOELBE: was der Objekt-Browser anbietet, solange der
/// Spieler in einem ist, und die Raumliste, die die Objekttabelle nicht liefern kann.
///
/// User, 2026-08-12: *"make a deep dungeon specific navigator since this is
/// self-contained, we may only need treasures, passages and enemies instead of
/// attempting to show every possible category the game world uses"* und *"categories
/// should include cairns, treasure, enemies and rooms. traps can go under enemies for
/// ease of tracking."*
///
/// WARUM DER KATEGORIENSATZ SCHRUMPFT UND NICHT WAECHST. Eine Gewoelbe-Ebene enthaelt
/// vier Arten von Dingen und sonst nichts: Gegner (Fallen eingeschlossen - das Spiel
/// fuehrt eine aufgedeckte Falle als BattleNpc, gemessen 2026-08-12 *"Trap, Combat NPC,
/// 5 yalms"*), Truhen, die beiden Leuchten und die Raeume, in denen sie stehen. Sechzehn
/// Weltkategorien darueber sind sechzehn Tastendruecke fuer vier Antworten.
///
/// "ALLES" BLEIBT DIE ERSTE KATEGORIE, mit Absicht, und das ist kein Rundungsfehler.
/// Dieses Repo hat zweimal einen Filter ausgeliefert, dessen Vorgabe Stille war, und
/// musste ihn zweimal zuruecknehmen; die Regel daraus lautet, dass keine Regel etwas
/// verbergen darf, nur weil nichts gepasst hat. Was die Einordnung unten nicht erkennt,
/// ist weiterhin einen Tastendruck entfernt.
///
/// DER WORTSCHATZ IST DER DES SPIELS, NICHT DER DIESER DATEI. Eine Truhe wird am
/// spieleigenen Wort dafuer erkannt (<c>Addon</c> 10113) und eine Leuchte an den
/// spieleigenen Namen der beiden (<c>Addon</c> 10418 "Cairn of Return", 10419 "Cairn of
/// Passage"). Diese Zeilen wurden gegen die <c>EObjName</c>-Zeilen geprueft, ueber die
/// die Objekte tatsaechlich aufloesen, in vier Sprachen (Offline-Auszug 2026-08-12):
/// EN/DE/FR/JA stimmen exakt ueberein, z. B. DE "Totenleuchte"/"Wegleuchte"/
/// "Schatztruhe", JA "再生の石塔"/"転移の石塔"/"宝箱". Das Plugin haelt also drei
/// SHEET-ZEILEN-IDS und ueberhaupt keine Zeichenketten, und einer Umbenennung im Spiel
/// wird gefolgt, statt sie zu ueberschreiben.
/// </summary>
public sealed class DeepDungeonNav
{
    /// <summary><c>Addon</c>-Zeile fuer die spieleigenen Woerter. Nur Ids - der Text wird
    /// immer aus dem Sheet in der Sprache des Clients gelesen.</summary>
    private const uint AddonTreasureCoffer = 10113;
    private const uint AddonCairnOfReturn  = 10418;
    private const uint AddonCairnOfPassage = 10419;

    /// <summary>
    /// UNBESTAETIGT: die beiden <c>EObjName</c>-Zeilen namens "buried coffer"
    /// (DE "vergraben[a] Schatztruhe", JA "埋もれた宝箱"). Sie sind die einzigen Zeilen im
    /// ganzen Sheet, die der Verborgene Hort sein koennten - keine Zeile irgendwo enthaelt
    /// das Wort "hoard" - und <c>LogMessage</c> 2590 nennt das Ziel *"Search for a buried
    /// coffer."* Sie stehen unter Schaetzen und werden BEIM SEHEN protokolliert, ein
    /// Auftreten in einem Tiefen Gewoelbe klaert also, was sie sind. Hier falsch zu liegen
    /// kostet eine zusaetzliche Zeile in einer Liste; sie wegzulassen koennte den Spieler
    /// den Hort kosten.
    /// </summary>
    private static readonly uint[] BuriedCofferDataIds = { 2005312, 2007744 };

    private readonly IDataManager      _data;
    private readonly IPluginLog        _log;
    private readonly ObjectNameService _names;
    private readonly DeepDungeonFloor  _floor;
    private readonly TolkService       _tolk;
    private readonly Configuration     _config;

    /// <summary>Wird nur benutzt, um einen Raumpunkt auf erreichbares Netz zu legen;
    /// null-vertraeglich, das Merkmal faellt ohne ihn auf den Rohpunkt zurueck.</summary>
    public AutoWalkService? Walk { get; set; }
    private AutoWalkService? _walk => Walk;

    /// <summary>Haelt das Netz von vnavmesh mit der Ebene im Gleichschritt;
    /// null-vertraeglich, ohne ihn verhaelt sich das Plugin genau wie zuvor.</summary>
    public DeepDungeonMesh? Mesh { get; set; }

    /// <summary>Nur die Fallen-Sonde liest sie - dem Browser werden seine Objektlisten
    /// weiterhin vom Navigationsdienst gereicht.</summary>
    private readonly Dalamud.Plugin.Services.IObjectTable _objects;

    public DeepDungeonNav(IDataManager data, IPluginLog log, ObjectNameService names,
                          DeepDungeonFloor floor, TolkService tolk, Configuration config,
                          Dalamud.Plugin.Services.IObjectTable objects)
    {
        _data    = data;
        _log     = log;
        _names   = names;
        _floor   = floor;
        _tolk    = tolk;
        _config  = config;
        _objects = objects;
    }

    /// <summary>
    /// Wird einmal je Frame aus dem Navigationsdienst aufgerufen.
    ///
    /// Zwei der drei Dinge hier schreiben nur ins Log - der Ebenen-Schnappschuss und die
    /// Truhen-Belege - und das dritte spricht nur, wenn der Spieler in einen anderen Raum
    /// gelaufen ist und der Schalter dafuer an ist. Nichts hier liest die Objekttabelle
    /// oder kostet je Frame ein Nachschlagen im Sheet.
    /// </summary>
    public void Poll(IGameObject player)
    {
        var playerEntityId = player.EntityId;
        _floor.LogSnapshot(playerEntityId);

        // ZUERST, weil alles andere hier, was in einem Lauf endet, auf einem Netz
        // wertlos ist, das fuer eine andere Ebene gebaut wurde. Im gewoehnlichen Frame
        // ein Ganzzahlvergleich - siehe DeepDungeonMesh.
        Mesh?.Poll();

        // Der Weg zurueck in diesen Raum, gemerkt in dem Moment, in dem der Spieler
        // hereinkommt - der Rueckfall fuer den Fall, dass die Layout-Daten nicht lesbar
        // sind oder nicht bestaetigt wurden.
        var here = _floor.PlayerRoom(playerEntityId);
        if (here >= 0) _floor.RememberEntry(here, player.Position);

        // ...und die Pruefung, die die spieleigenen Raumkoordinaten ueberhaupt erst
        // benutzbar macht: die dem Spieler naechste Rauminstanz muss der Raum sein, in
        // dem er laut Director steht. Siehe DeepDungeonRoomMap.
        _floor.VerifyRoomPositions(playerEntityId, player.Position);

        // Die Truhenpruefung baut eine Liste und eine Signatur-Zeichenkette, sie laeuft
        // deshalb auf einem Zaehler statt in jedem Frame - ein sich aenderndes
        // Truhen-Array heisst, dass eine Truhe geoeffnet wird, und das ist kein Ereignis
        // im Sekundenbruchteil. Ein schlichter Frame-Zaehler und KEINE
        // Zeitstempel-Differenz: dieses Repo hat ein Merkmal, das dauerhaft tot war, weil
        // ein long.MinValue-Platzhalter minus TickCount64 ins Negative umschlug, und ein
        // Zaehler kann so nicht scheitern.
        if (++_pollTick >= ChestCheckEveryFrames)
        {
            _pollTick = 0;
            _floor.LogChestEvidence();
#if DEBUG
            LogBattleNpcs(player);   // die Fallen-Sonde
#endif
        }

        var line = RoomChangeLine(playerEntityId);
        if (line == null || !_config.AnnounceDeepRoomChange) return;

        // Eingereiht, nie unterbrechend: in einen Raum zu laufen passiert oft genug
        // mitten im Kampf, und eine Zauber-Warnung abzuschneiden, um zu sagen, wo der
        // Spieler steht, ist der falsche Tausch (dieselbe Regel gilt fuer jede Ansage,
        // die die spieleigene Uhr antreibt).
        _tolk.Speak(line);
        _log.Info($"[DeepNav] Raumwechsel: {line}");
    }

    private int _pollTick;
    private const int ChestCheckEveryFrames = 30;

#if DEBUG
    /// <summary>
    /// DER UMWEG UM FALLEN HAENGT SEIT DEM 2026-08-12 HIERAN, und er laesst sich ohne
    /// das, was diese Sonde misst, nicht bauen.
    ///
    /// Die Forderung des Users (2026-08-13): *"it is very important that auto walk and the
    /// beacon guide route around them ... since it's part of the object scanner along with
    /// coordinates (logged) I imagine it is [known]."* Die Koordinaten SIND bekannt - eine
    /// aufgedeckte Falle ist ein gewoehnlicher BattleNpc, und der Browser liest ihre
    /// Position bereits ("Trap, Combat NPC, 70 yalms, left"). Drei Dinge sind es nicht,
    /// und jedes davon entscheidet einen Teil des Umwegs:
    ///
    /// <list type="number">
    /// <item><b>Welche Objekte Fallen sind, und zwar nicht ueber ein uebersetztes
    ///   Wort.</b> Der Director hat KEIN Fallen-Array - seine Arrays sind Gruppe,
    ///   Gegenstaende, Truhen, Magizite und Kartendaten (FFXIVClientStructs, gelesen
    ///   2026-08-13) - die Objekttabelle ist also die einzige Quelle, und ein Merkmal muss
    ///   vom Objekt selbst kommen. <c>NameId</c> (die BNpcName-Zeile) und <c>BaseId</c>
    ///   (die BNpcBase-Zeile) werden beide protokolliert; was ueber Fallen hinweg konstant
    ///   und bei jedem echten Gegner abwesend ist, wird der Schluessel, und dann passt das
    ///   Plugin auf eine ZAHL statt auf das Wort "Trap".</item>
    /// <item><b>Wie weit die Gefahr reicht.</b> Ein Umweg braucht einen Radius, und einen
    ///   zu erfinden verbieten die Regeln dieses Repos. <c>HitboxRadius</c> ist die
    ///   spieleigene Angabe zum Objekt und wird fuer jeden Kandidaten protokolliert, damit
    ///   der Wert der Falle mit dem gewoehnlicher Gegner verglichen statt geraten werden
    ///   kann.</item>
    /// <item><b>Ob UNAUFGEDECKTE Fallen ueberhaupt in der Tabelle stehen</b> - die
    ///   urspruengliche Blockade, und eher eine Frage der Gleichstellung als eine
    ///   technische. Stehen sie darin, wuerde sie alle zu meiden dem Spieler stillschweigend
    ///   einen Pomander of Sight in die Hand druecken, den er nicht benutzt hat.
    ///   <c>IsTargetable</c> wird neben jeder protokolliert, weil das am ehesten die beiden
    ///   Zustaende trennt; die Entscheidung des Users gilt so oder so - NUR aufgedeckte
    ///   Fallen meiden.</item>
    /// </list>
    ///
    /// Eine Zeile je AENDERUNG der Menge, auf der Drossel der Truhenpruefung, und sie
    /// schreibt ausschliesslich ins Log - es wird nichts gesprochen und kein Verhalten
    /// haengt daran.
    /// </summary>
    private void LogBattleNpcs(IGameObject player)
    {
        // NUR IM TIEFEN GEWOELBE. Poll() laeuft ueberall, wo der Dienst haengt, nicht
        // erst ab dem Betreten - ohne dieses Tor protokollierte eine FALLEN-Sonde die
        // Dodos von Unter-La Noscea. Gemessen am 2026-08-19: 25.254 der 31.315
        // Log-Zeilen einer Spielsitzung kamen von hier, 85 Prozent des ganzen Logs,
        // und keine einzige davon stand in einem Tiefen Gewoelbe.
        if (!_floor.IsActive) return;

        var npcs = _objects
            .Where(o => o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
            .Where(o => Vector3.Distance(o.Position, player.Position) <= TrapProbeRangeYalms)
            .OrderBy(o => Vector3.Distance(o.Position, player.Position))
            .ToList();

        // Die Signatur wird nach Objekt-Id sortiert gebildet, NICHT in der
        // Ausgabereihenfolge. Die Ausgabe steht nach Entfernung, und zwei Gegner in
        // aehnlichem Abstand tauschen beim Laufen staendig die Plaetze - das allein
        // machte die Signatur neu und trieb die Drossel zweimal pro Sekunde durch,
        // obwohl sich an der MENGE nichts geaendert hatte. Die Sonde fragt nach
        // "welche Objekte, und sind sie anvisierbar", und darauf hat die Reihenfolge
        // keine Antwort.
        var signature = string.Join(",", npcs
            .OrderBy(o => o.GameObjectId)
            .Select(o => $"{o.GameObjectId:X}:{o.IsTargetable}"));
        if (signature == _lastNpcSignature) return;
        _lastNpcSignature = signature;

        if (npcs.Count == 0)
        {
            _log.Info("[DeepTraps] Keine Kampf-NPCs in Reichweite.");
            return;
        }

        _log.Info($"[DeepTraps] {npcs.Count} Kampf-NPCs in Reichweite:");
        foreach (var o in npcs)
            _log.Info($"[DeepTraps]   '{_names.Resolve(o) ?? "?"}' NameId={(o as IBattleNpc)?.NameId.ToString() ?? "-"} "
                      + $"DataId={o.BaseId} id={o.GameObjectId:X} zielbar={o.IsTargetable} "
                      + $"Radius={o.HitboxRadius:F2} Raum={_floor.RoomAt(o.Position)} "
                      + $"dist={Vector3.Distance(o.Position, player.Position):F1} pos={o.Position}");
    }

    private string _lastNpcSignature = string.Empty;

    /// <summary>Wie weit die Sonde schaut. Die Reichweite des Browsers selbst, damit
    /// beide dasselbe unter "in Reichweite" verstehen, wenn das Log neben einer
    /// Browser-Ansage gelesen wird.</summary>
    private const float TrapProbeRangeYalms = 100f;
#endif

    /// <summary>Ob der Kategoriensatz des Tiefen Gewoelbes gelten soll.</summary>
    public bool IsActive => _floor.IsActive;

    // ── Was auf der Ebene steht einordnen, in den Worten des Spiels ──

    private readonly Dictionary<uint, string> _words = new();

    /// <summary>Der spieleigene Text zu einer <c>Addon</c>-Zeile, zwischengespeichert;
    /// "", wenn das Sheet keine solche Zeile hat.</summary>
    private string Word(uint addonRow)
    {
        if (_words.TryGetValue(addonRow, out var cached)) return cached;

        var text = _data.GetExcelSheet<Addon>()?.GetRowOrDefault(addonRow)?.Text.ExtractText().Trim()
                   ?? string.Empty;
        if (text.Length == 0)
            _log.Warning($"[DeepNav] Addon-Zeile {addonRow} ist leer - Kategorie bleibt ungefiltert.");
        _words[addonRow] = text;
        return text;
    }

    /// <summary>
    /// Ob ein Objekt eine Truhe ist.
    ///
    /// <c>ObjectKind.Treasure</c> wird bedingungslos genommen: das ist die spieleigene Art
    /// fuer eine Truhe, und obwohl jede bisher gemessene Gewoelbe-Truhe ein
    /// <c>EventObj</c> war (Log 2026-08-12, vier davon, alle <c>art=EventObj</c>), darf
    /// eine Truhe, die das Spiel unter seiner Truhen-Art fuehrt, nie aus der
    /// Truhen-Kategorie fallen.
    ///
    /// Die Namenspruefung ist ein ENTHAELT und kein Gleichheitstest, damit eine naeher
    /// bestimmte Truhe ("vergrabene Schatztruhe") drinbleibt. Sie laeuft ueber den Namen,
    /// den der Browser selbst aufloest - die <c>EObjName</c>-Zeile ohne die
    /// Deklinationsmarken, also genau die Zeichenkette, die der Spieler hoert.
    /// </summary>
    public bool IsCoffer(IGameObject obj)
    {
        if (obj.ObjectKind == ObjectKind.Treasure) return true;
        if (BuriedCofferDataIds.Contains(obj.BaseId))
        {
            _log.Info($"[DeepNav] 'Vergrabene Truhe' gesehen: DataId={obj.BaseId} id={obj.GameObjectId:X} "
                      + $"pos={obj.Position} - moeglicher Verborgener Hort (unbestaetigt).");
            return true;
        }

        var word = Word(AddonTreasureCoffer);
        if (word.Length == 0) return false;

        var name = _names.Resolve(obj);
        return name != null && name.Contains(word, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ob ein Objekt eine der beiden Leuchten ist, nach den spieleigenen Namen
    /// dafuer.</summary>
    public bool IsCairn(IGameObject obj)
    {
        var name = _names.Resolve(obj);
        if (name == null) return false;

        var ret = Word(AddonCairnOfReturn);
        var pas = Word(AddonCairnOfPassage);
        return (ret.Length > 0 && name.Equals(ret, StringComparison.OrdinalIgnoreCase))
            || (pas.Length > 0 && name.Equals(pas, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Beantwortet die Frage des Users - *"I have not been able to open a bronze chest, I
    /// don't know if one has spawned or if they're being skipped or dropped by the mod"* -
    /// aus dem Log statt aus Ueberlegungen.
    ///
    /// Nichts im Plugin kann eine Truhe NACH FARBE verwerfen, weil nichts im Plugin die
    /// Farbe einer Truhe kennt (siehe <see cref="DeepDungeonFloor.ColourOf"/>). Was eine
    /// verbergen KANN, ist gewoehnlich und farbenblind: sie ist weiter weg als die
    /// 100 Yalm des Browsers, oder das Spiel hat sie nicht geladen. Die Zeile unten
    /// schreibt deshalb beide Haelften nebeneinander - was der DIRECTOR auf der Ebene
    /// auffuehrt, was von nichts Geladenem abhaengt, und was der Browser tatsaechlich
    /// anbieten koennte. Eine Ebene, deren Director vier Truhen auffuehrt, waehrend der
    /// Browser eine sieht, ist eine Frage des Nachladens; eine Ebene, auf der beide
    /// uebereinstimmen und der Spieler trotzdem nie eine bronzene trifft, ist eine Frage
    /// an die Erzeugung des Spiels selbst.
    ///
    /// Eine Zeile je Aenderung, nicht eine je Tastendruck - der Browser fragt bei jedem
    /// Druck.
    /// </summary>
    public void LogCofferPass(IReadOnlyList<IGameObject> candidates, IReadOnlyList<IGameObject> coffers)
    {
        var chests = _floor.Chests();

        // Laeuft vor der Entdopplung unten, damit die Widerlegungspruefung jeden Durchgang
        // sieht und nicht nur die, deren Formulierung sich zufaellig geaendert hat.
        CheckAgainstDirector(coffers, chests);

        var signature = string.Join(",", coffers.Select(o => $"{o.BaseId}:{o.GameObjectId:X}"))
                        + "|" + string.Join(",", chests.Select(c => $"{c.ChestType}@{c.RoomIndex}"));
        if (signature == _lastCofferSignature) return;
        _lastCofferSignature = signature;

        _log.Info($"[DeepNav] Schatzkategorie: {coffers.Count} von {candidates.Count} Objekten in Reichweite. "
                  + $"Direktor fuehrt {chests.Count} Truhen auf dieser Ebene "
                  + $"[{string.Join(" ", chests.Select(c => $"Typ{c.ChestType}@Raum{c.RoomIndex}"))}].");
        foreach (var o in coffers)
            _log.Info($"[DeepNav]   Truhe '{_names.Resolve(o) ?? "?"}' DataId={o.BaseId} "
                      + $"id={o.GameObjectId:X} art={o.ObjectKind} Typ={ChestTypeOf(o)?.ToString() ?? "?"} "
                      + $"Raum={_floor.RoomAt(o.Position)} zielbar={o.IsTargetable} pos={o.Position}");
    }

    private string _lastCofferSignature = string.Empty;

    // ── WELCHE TRUHE WELCHE IST, aus der Daten-Id der Truhe selbst ──
    //
    // Eine fruehere Regel musste zurueckgezogen werden, die eine Truhe aus dem Array des
    // Directors benannte, wenn jeder Eintrag darin denselben Typ hatte - denn das Array
    // haelt nur die ENTDECKTEN und nicht geoeffneten Truhen: bei zwei Truhen in Reichweite
    // und einem Eintrag wurde die zweite auf die Kraft eines Eintrags hin silbern genannt,
    // der gar nicht von ihr handelte. Die damals festgehaltene Lehre war, dass jeder neue
    // Weg beantworten muss, "welcher Eintrag ist DIESES Objekt", statt anzunehmen, die
    // Menge sei einheitlich.
    //
    // DAS OBJEKT BEANTWORTET ES SELBST, und keine Verknuepfung ist noetig. Eine
    // Gewoelbe-Truhe ist nicht ein Ding mit angehaengtem Typ - das Spiel setzt je Seltenheit
    // ein ANDERES OBJEKT, und die lassen sich an der Daten-Id auseinanderhalten, die die
    // Objekttabelle ohnehin traegt:
    //
    //     EObj 2007357  sgbg_w_lvd_009_01a.sgb  ->  ChestType 2
    //     EObj 2007358  sgbg_w_lvd_015_01a.sgb  ->  ChestType 3
    //     ObjectKind.Treasure, DataId 783/784   ->  ChestType 1
    //
    // (Die beiden EObj-Zeilen tragen verschiedene ExportedSG-Modelle - Offline-Auszug
    // 2026-08-12 - es sind also verschiedene Requisiten und nicht eine umgefaerbte. Beide
    // teilen den EObjName "treasure coffer", und genau deshalb wurde jede von ihnen gleich
    // angesagt.)
    //
    // DIE BELEGE SIND ZEHN LUECKENLOSE ZUORDNUNGEN und kein Gegenbeispiel. Jeder Moment in
    // den Logs vom 2026-08-12, in dem der Director GENAU EINE Truhe auffuehrte, waehrend
    // GENAU EIN Truhen-Objekt in Reichweite stand - was eine Zuordnung ohne jede Folgerung
    // ist - stimmte ueberein:
    //
    //     10:53:30 [Typ3] 2007358      11:08:30 [Typ3] 2007358     11:43:36 [Typ1] 783
    //     10:55:34 [Typ2] 2007357      11:18:06 [Typ3] 2007358     11:49:09 [Typ3] 2007358
    //     11:02:08 [Typ3] 2007358      11:19:31 [Typ3] 2007358     11:51:40 [Typ1] 784
    //     11:13:42 [Typ3] 2007358
    //
    // und die gedraengten Momente stimmen als Mengen ebenfalls: 11:45:00 fuehrte drei Typ3
    // und einen Typ2 auf, waehrend drei 2007358 und ein 2007357 in Reichweite standen.
    //
    // ES BLEIBT EINE HYPOTHESE, ALSO PRUEFT SIE SICH SELBST - siehe CheckAgainstDirector.
    // Genau die Bedingung, die jene zehn Datenpunkte erzeugt hat, tritt mehrmals je Ebene
    // wieder auf, und ein einziger Widerspruch legt die ganze Tabelle fuer die Sitzung
    // still, statt einen blinden Spieler eine Farbe hoeren zu lassen, die nicht da ist.

    // Alle drei Typen tragen inzwischen eine Farbe, die das SPIEL nennt, alle vier
    // Daten-Ids verdienen sich also ihre Zeile. 783/784 sind ObjectKind.Treasure statt
    // EObj und wurden zweimal mit ChestType 1 gepaart (11:43:36, 11:51:40); ihre Requisite
    // sind dieselben zwei Modelldateien, die die spaeteren Gewoelbe fuer ihre "bronze
    // coffer" setzen. Siehe DeepDungeonFloor.ColourOf.
    private static readonly System.Collections.Generic.Dictionary<uint, byte> TypeByDataId = new()
    {
        [2007357] = 2,   // sgbg_w_lvd_009_01a -> silbern
        [2007358] = 3,   // sgbg_w_lvd_015_01a -> golden
        [783]     = 1,   // sgbg_w_tbx_001_01a -> die bronzene Requisite
        [784]     = 1,
    };

    /// <summary>
    /// Der Truhentyp des Directors zu einer lebenden Truhe, oder null, wenn diese Truhe
    /// keine ist, die die Tabelle kennt, oder die Tabelle widerlegt wurde.
    /// </summary>
    public byte? ChestTypeOf(IGameObject coffer)
        => _dataIdTableRetired ? null
           : TypeByDataId.TryGetValue(coffer.BaseId, out var t) ? t : null;

    /// <summary>
    /// Das spieleigene Farbwort zu einer lebenden Truhe, oder null, wenn sie nicht benannt
    /// werden kann.
    ///
    /// Zwei unabhaengige Dinge muessen gelten, und fehlt eines, bedeutet das Stille: die
    /// Daten-Id des Objekts muss eine sein, die die Tabelle oben kennt, und dieser
    /// Truhentyp muss eine Farbe sein, die das SPIEL genannt hat (siehe
    /// <see cref="DeepDungeonFloor.ColourOf"/>). Eine Truhe, die an einem von beiden
    /// scheitert, behaelt die spieleigene "Schatztruhe" und sagt nichts weiter.
    /// </summary>
    public string? ColourOf(IGameObject coffer)
        => ChestTypeOf(coffer) is { } type ? _floor.ColourOf(type) : null;

    private bool _dataIdTableRetired;
    private readonly System.Collections.Generic.HashSet<uint> _unknownCoffers = new();

    /// <summary>
    /// Die laufende Widerlegungspruefung fuer die Tabelle oben, ausgefuehrt auf dem
    /// eigenen Durchgang des Browsers ueber die Truhen.
    ///
    /// DIE BEDINGUNG IST DIE, DIE KEINE FOLGERUNG BRAUCHT: ein Eintrag im Array des
    /// Directors, ein geladenes Truhen-Objekt, UND BEIDE IM SELBEN RAUM. Dann IST dieses
    /// Objekt jener Eintrag - etwas anderes kann es nicht sein - der Typ, den die Tabelle
    /// vorhersagt, passt also entweder, oder die Tabelle ist falsch.
    ///
    /// Die Raum-Klausel ist keine doppelte Absicherung, sie ist das, was diese Pruefung
    /// davor bewahrt, genau der Fehler zu sein, den zu fangen es sie gibt. Die uebergebene
    /// Liste ist die des Browsers, also bereits auf das zugeschnitten, was in Reichweite
    /// ist: "eine Truhe" heisst eine IN DER NAEHE, waehrend der Eintrag des Directors von
    /// einer Truhe am anderen Ende der Ebene handeln kann, die ueberhaupt nicht geladen
    /// ist. Diese beiden ueber das Abzaehlen zu paaren ist genau die Ueberlegung, die
    /// zurueckgezogen werden musste, und hier wuerde sie eine richtige Tabelle stilllegen,
    /// statt eine Truhe falsch zu beschriften. Es wird also nichts geschlossen, solange die
    /// Raeume nicht uebereinstimmen - und wo das Layout nicht lesbar ist, gar nichts.
    ///
    /// Ein Widerspruch legt die Tabelle fuer den Rest der Sitzung still. Das ist mit
    /// Absicht haerter als "diese eine ueberspringen": ist die Voraussetzung kaputt, ist
    /// jede Farbe verdaechtig, die das Plugin seit dem Laden genannt hat, und die ehrliche
    /// Antwort ist aufzuhoeren, statt mit einer einmal widerlegten Regel weiterzumachen.
    ///
    /// Eine UNBEKANNTE Daten-Id ist kein Fehlschlag - sie ist die vierte Truhen-Requisite,
    /// der noch niemand begegnet ist. Sie wird mit dem Typ protokolliert, den sie haben
    /// muesste, und genau das braucht eine kuenftige Sitzung, um die Tabelle zu erweitern.
    /// </summary>
    private void CheckAgainstDirector(System.Collections.Generic.IReadOnlyList<IGameObject> coffers,
                                      System.Collections.Generic.IReadOnlyList<DeepDungeonFloor.Chest> chests)
    {
        if (coffers.Count != 1 || chests.Count != 1) return;

        var obj      = coffers[0];
        var expected = chests[0].ChestType;

        // Derselbe Raum, aus dem spieleigenen Layout ermittelt, oder kein Schluss.
        var room = _floor.RoomAt(obj.Position);
        if (room < 0 || room != chests[0].RoomIndex) return;

        if (!TypeByDataId.TryGetValue(obj.BaseId, out var predicted))
        {
            if (_unknownCoffers.Add(obj.BaseId))
                _log.Info($"[DeepChests] Unbekannte Truhen-DataId {obj.BaseId} (art={obj.ObjectKind}) - "
                          + $"der Direktor fuehrt sie als Typ {expected}. Eindeutige Zuordnung, "
                          + "gehoert in die Tabelle von DeepDungeonNav.");
            return;
        }

        if (predicted == expected) return;

        _dataIdTableRetired = true;
        _log.Warning($"[DeepChests] TRUHEN-TABELLE WIDERLEGT: DataId {obj.BaseId} sollte Typ "
                     + $"{predicted} sein, der Direktor sagt Typ {expected} - eine Truhe, ein "
                     + "Eintrag, also eindeutig. Es wird keine Truhenfarbe mehr genannt.");
    }


    // ── Die Raumliste ──

    /// <summary>Eine Zeile der Raumliste: was zu sagen ist, und wohin zu laufen, wenn der
    /// Spieler dort war.</summary>
    /// <param name="Index">Der Raumindex des Directors selbst.</param>
    /// <param name="Text">Die fertige Zeile Sprache.</param>
    /// <param name="Walkable">Die Tuer, durch die der Spieler hereinkam, oder null, wenn
    /// er nie in diesem Raum war.</param>
    public readonly record struct RoomRow(int Index, string Text, System.Numerics.Vector3? Walkable);

    /// <summary>
    /// Die Raeume, die der Spieler aufgedeckt hat, jeder als eine fertige Zeile Sprache.
    ///
    /// Ausserhalb eines Tiefen Gewoelbes leer, und leer - statt falsch - solange der
    /// Ebenen-Leser nicht bestaetigen kann, dass die Raum-Flags zu den Raumindizes
    /// gehoeren, mit denen er sie liest (<see cref="DeepDungeonFloor.FlagsTrustworthy"/>).
    /// </summary>
    public List<RoomRow> RoomRows(uint playerEntityId)
    {
        var rows = new List<RoomRow>();
        if (!_floor.IsActive) return rows;

        foreach (var room in _floor.RevealedRooms(playerEntityId))
        {
            // Zuerst die spieleigene Layout-Position, dann die eigene Tuer des Spielers.
            // Der Layout-Punkt taugt fuer jeden aufgedeckten Raum, auch fuer solche, in
            // denen der Spieler nie war, und fuer solche, die er vor dem Laden des Plugins
            // durchquert hat.
            var walkable = _floor.RoomPosition(room.Index) ?? _floor.EntryPoint(room.Index);
            var text = DescribeRoom(room);
            // Ein aufgedeckter Raum, fuer den das Plugin keinen gespeicherten Punkt hat,
            // kann nicht angelaufen werden, und die Zeile sagt das - sonst wuerde die
            // Lauftaste dort einfach nichts tun. Sie nennt die Grenze des PLUGINS, nicht
            // die Vorgeschichte des Spielers: siehe DeepRoomNoRoute.
            if (walkable == null && !room.IsPlayerRoom)
                text += ", " + AccessibilityStrings.DeepRoomNoRoute;
            // Der Raum, in dem der Spieler steht, ist nie ein Laufziel - eine Route
            // dorthin, wo man schon ist, ist ein Tastendruck ohne sichtbare Wirkung.
            rows.Add(new RoomRow(room.Index, text, room.IsPlayerRoom ? null : Snap(walkable)));
        }

        // ...und danach, wo es noch Ebene zu erkunden gibt. NACH den bekannten Raeumen
        // aufgefuehrt und mit nichts als einem Ziel versehen - siehe
        // DeepDungeonFloor.UnexploredRooms, warum das Gleichstellung ist und kein Bruch
        // davon.
        foreach (var index in _floor.UnexploredRooms(playerEntityId))
            rows.Add(new RoomRow(index, AccessibilityStrings.DeepRoomUnexplored(index),
                                 Snap(_floor.RoomPosition(index))));

        return rows;
    }

    /// <summary>
    /// Legt den Punkt eines Raumes dorthin, wo das Netz tatsaechlich hinfuehrt.
    ///
    /// Der aufgezeichnete Punkt eines Raumes ist der Ursprung seines Moduls in der
    /// Layout-Datei und kann in einer Wand liegen, und so endet ein Lauf dorthin an einer
    /// (User, 2026-08-12). Bleibt unangetastet, wenn vnavmesh nicht da ist - dann wird der
    /// Rohpunkt genau wie zuvor benutzt.
    /// </summary>
    private System.Numerics.Vector3? Snap(System.Numerics.Vector3? point)
        => point is { } p ? _walk?.ResolveReachablePoint(p) ?? p : null;

    /// <summary>
    /// Ein Raum als Satz: welcher Raum, ob der Spieler darin steht, was laut Spiel darin
    /// steht, und wohin er sich oeffnet.
    ///
    /// Die Ausgaenge kommen aus den EIGENEN Flags des Raumes (ConnectionN/S/W/E) und sind
    /// damit Tatsachen ueber diesen Raum und keine Route: das Plugin nennt, was das Spiel
    /// festhaelt, und ueberlaesst den naechsten Zug dem Spieler - die stehende Regel fuer
    /// jede Navigationsansage hier.
    /// </summary>
    public string DescribeRoom(DeepDungeonFloor.Room room)
    {
        var parts = new List<string> { AccessibilityStrings.DeepRoomName(room.Index) };

        if (room.IsPlayerRoom) parts.Add(AccessibilityStrings.DeepRoomYouAreHere);
        if (room.IsStart)      parts.Add(AccessibilityStrings.DeepRoomStart);
        if (room.HasReturnCairn && Word(AddonCairnOfReturn).Length > 0)
            parts.Add(Word(AddonCairnOfReturn));
        if (room.HasPassageCairn && Word(AddonCairnOfPassage).Length > 0)
            parts.Add(Word(AddonCairnOfPassage));
        // Die Truhen DIESES Raumes, wo der Typ belegt ist nach Farbe benannt. Exakt statt
        // gefolgert: der Director gibt den Typ je Raum-Eintrag an, es ist also keine
        // Paarung mit einem lebenden Objekt im Spiel und nichts muss einstimmig sein.
        foreach (var group in _floor.Chests().Where(c => c.RoomIndex == room.Index)
                                    .GroupBy(c => c.ChestType))
            parts.Add(AccessibilityStrings.DeepRoomCoffers(
                group.Count(), _floor.ColourOf(group.Key) ?? Word(AddonTreasureCoffer)));

        var exits = Exits(room.Flags);
        if (exits.Count > 0) parts.Add(AccessibilityStrings.DeepRoomExits(exits));

        return string.Join(", ", parts);
    }

    /// <summary>Die eigenen Verbindungs-Flags des Raumes, als Richtungswoerter.</summary>
    private static List<string> Exits(DirectorRoomFlags flags)
    {
        var exits = new List<string>();
        if (flags.HasFlag(DirectorRoomFlags.ConnectionN)) exits.Add(AccessibilityStrings.DirNorth);
        if (flags.HasFlag(DirectorRoomFlags.ConnectionE)) exits.Add(AccessibilityStrings.DirEast);
        if (flags.HasFlag(DirectorRoomFlags.ConnectionS)) exits.Add(AccessibilityStrings.DirSouth);
        if (flags.HasFlag(DirectorRoomFlags.ConnectionW)) exits.Add(AccessibilityStrings.DirWest);
        return exits;
    }

    // ── "In welchem Raum bin ich jetzt?" ──

    private int _lastSpokenRoom = -1;

    /// <summary>
    /// Die Zeile, die zu sprechen ist, wenn der Spieler in einen anderen Raum laeuft, oder
    /// null, wenn sich nichts geaendert hat.
    ///
    /// Das ist das eine Stueck der Raumdaten, das von selbst kommen muss: ein sehender
    /// Spieler liest seine Position fortlaufend von der Gewoelbe-Karte ab, und eine Liste,
    /// die er abfragen muss, ist nicht dieselbe Information. Sie haengt an einem Schalter,
    /// aus demselben Grund wie jede andere laufende Ansage.
    /// </summary>
    public string? RoomChangeLine(uint playerEntityId)
    {
        if (!_floor.IsActive)
        {
            _lastSpokenRoom = -1;
            return null;
        }

        var here = _floor.PlayerRoom(playerEntityId);
        if (here < 0 || here == _lastSpokenRoom) return null;
        _lastSpokenRoom = here;

        // Die Einzelheiten kommen aus dem eigenen Datensatz des Raumes und nur, wenn der
        // Ebenen-Leser dafuer einsteht. Die NUMMER wird so oder so gesagt: sie ist der
        // spieleigene Index fuer den Raum, in dem der Spieler ist, derselbe, der seine
        // Truhen beschriftet, und damit auch dann nuetzlich, wenn den Flags daneben nicht
        // zu trauen ist.
        var room = _floor.RevealedRooms(playerEntityId).FirstOrDefault(r => r.Index == here);
        return room.Index == here ? DescribeRoom(room) : AccessibilityStrings.DeepRoomName(here);
    }
}
