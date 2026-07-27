using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>One fishing hole of the current zone, from the static FishingSpot sheet.</summary>
/// <param name="Name">Localised place name ("Fallgourd Float").</param>
/// <param name="Level">Required fishing level (GatheringLevel).</param>
/// <param name="Position">World X/Z; Y is unknown (map data is 2D) and 0 -
/// resolve via navmesh before walking.</param>
/// <param name="RowId">FishingSpot sheet row id (language-independent key for overrides).</param>
/// <param name="IsExact">True when the position is a hand-verified castable
/// coordinate (a city override), so the walk heads straight there like a map
/// flag - not the wide "nearest bank" snap used for raw water-centre spots.</param>
public sealed record FishingSpotInfo(string Name, int Level, Vector3 Position, uint RowId, bool IsExact = false);

/// <summary>
/// Fishing accessibility. A blind player cannot see where the water is, so the
/// first job is answering "where can I fish?". The game's own FishingSpot sheet
/// is the authoritative, complete catalogue: every rod-fishing hole with its
/// zone, name, required level and map-PIXEL coordinates (0..2048). We read that
/// and convert to world positions with the verified PlacesService pixel formula,
/// then the existing walk guide / navmesh can reach them.
///
/// Verified via ilspycmd + real data read from the game's sqpack (2026-07-25):
/// FishingSpot.TerritoryType@52 (zone), PlaceName@60, X@64/Z@66 (map pixels,
/// all rows fall in 108..1948), Radius@58, GatheringLevel@68. 333 catalogued
/// spots total.
///
/// STILL TO CONFIRM LIVE (one walk): that the converted world position lands on
/// the fishing hole - the last-mile check the compass announcement doubles as.
/// The bite announcement (FishingState) is a separate, later step.
/// </summary>
public sealed class FishingService
{
    private readonly IObjectTable  _objectTable;
    private readonly IClientState  _clientState;
    private readonly IDataManager  _data;
    private readonly PlacesService _places;
    private readonly TolkService   _tolk;
    private readonly IPluginLog    _log;
    private readonly Configuration _config;
    private readonly IDalamudPluginInterface _pluginInterface;

    // Live fishing state (see Update): the handler exists only while fishing, so
    // these reset between sessions and never fire on the very first read.
    private bool         _wasFishing;
    private FishingState _lastState  = FishingState.None;
    private bool         _lastCanFish;

    /// <summary>
    /// Per-spot coordinate overrides for city/harbour fishing holes whose exact
    /// FishingSpot sheet coordinate sits on dry land, tens of yalms from the
    /// castable water (the sheet stores only a coarse district marker there).
    /// The value is the WHOLE-NUMBER map coordinate a player actually fishes
    /// from - guided via MapCoordToWorld instead of the raw sheet pixel.
    ///
    /// Keyed by FishingSpot RowId (language-independent; verified via Lumina
    /// against the game's own sheet, 2026-07-27). Only entries CONFIRMED in-game
    /// belong here - do NOT bulk-fill from the wiki, its values are approximate
    /// and the exact conversion already works for open-world spots.
    /// </summary>
    private static readonly Dictionary<uint, (float MapX, float MapY)> SpotOverrides = new()
    {
        // Limsa Lominsa Lower Decks - user-verified 2026-07-27 (7,12 fishable; the
        // exact 7.7/12.2 lands on the Merchant Strip, ~35 yalms from the water).
        [35] = (7f, 12f),
    };

    public FishingService(
        IObjectTable objectTable,
        IClientState clientState,
        IDataManager data,
        PlacesService places,
        TolkService tolk,
        Configuration config,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _data        = data;
        _places      = places;
        _tolk        = tolk;
        _config      = config;
        _pluginInterface = pluginInterface;
        _log         = log;
    }

    /// <summary>Refuse to capture when no fishing spot is within this many yalms
    /// (the player is not actually standing at one).</summary>
    private const float CaptureMaxDistance = 100f;

    /// <summary>
    /// The castable map coordinate override for a fishing spot, or null when none.
    /// A user capture (config, "/acc fishhere") takes precedence over the
    /// built-in verified table.
    /// </summary>
    private (float MapX, float MapY)? ResolveOverride(uint rowId)
    {
        if (_config.FishingSpotOverrides.TryGetValue(rowId, out var v) && v is { Length: >= 2 })
            return (v[0], v[1]);
        if (SpotOverrides.TryGetValue(rowId, out var s))
            return s;
        return null;
    }

    /// <summary>
    /// All fishing spots of the CURRENT zone, sorted nearest-first from the
    /// player. World positions come from the map-pixel conversion; Y is 0.
    /// Empty when the zone has no fishing spots or no player is loaded.
    /// </summary>
    public List<FishingSpotInfo> GetSpotsInCurrentZone()
    {
        var result = new List<FishingSpotInfo>();
        var player = _objectTable.LocalPlayer;
        if (player == null) return result;

        var territory = (ushort)_clientState.TerritoryType;
        var sheet = _data.GetExcelSheet<FishingSpot>();
        if (sheet == null) return result;

        foreach (var row in sheet.Where(r => r.TerritoryType.RowId == territory))
        {
            if (row.X == 0 && row.Z == 0) continue;
            var name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            // City/harbour spots with a known-good override use the actual
            // castable map coordinate; everyone else the exact sheet conversion.
            // A user capture (config, "/acc fishhere") wins over the built-in table.
            var ov = ResolveOverride(row.RowId);
            var world = ov is { } o
                ? _places.MapCoordToWorld(o.MapX, o.MapY)
                : _places.MapPixelToWorld(row.X, row.Z);
            if (world is not { } pos) continue;

            result.Add(new FishingSpotInfo(name, row.GatheringLevel, pos, row.RowId, ov.HasValue));
        }

        var playerPos = player.Position;
        return result
            .OrderBy(s => PlacesService.Distance2D(playerPos, s.Position))
            .ToList();
    }

    /// <summary>
    /// Speaks the fishing spots of the current zone, nearest first, each with
    /// required level, distance and compass direction - so a blind fisher knows
    /// where they can fish and which way to head. Also logs every spot.
    /// </summary>
    public void AnnounceSpotsInCurrentZone()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var spots = GetSpotsInCurrentZone();
        if (spots.Count == 0)
        {
            _tolk.SpeakInterrupt("Keine Angelplätze in dieser Zone.");
            _log.Info($"[Fish] Gebiet {_clientState.TerritoryType}: keine FishingSpot-Einträge.");
            return;
        }

        var playerPos = player.Position;
        var lines = new List<string>();
        _log.Info($"[Fish] Gebiet {_clientState.TerritoryType}: {spots.Count} Angelplätze");
        foreach (var s in spots)
        {
            var dist    = PlacesService.Distance2D(playerPos, s.Position);
            var compass = CompassDirection(playerPos, s.Position);
            lines.Add($"{s.Name}, Stufe {s.Level}, {dist:F0} Meter {compass}");
            _log.Info($"[Fish]   '{s.Name}' Stufe={s.Level} Welt=({s.Position.X:F1}|{s.Position.Z:F1}) " +
                      $"Dist={dist:F0} m {compass}");
        }

        _tolk.SpeakInterrupt($"{spots.Count} Angelplätze: " + string.Join(". ", lines) + ".");
    }

    /// <summary>
    /// Remembers the player's CURRENT position as the castable coordinate of the
    /// nearest fishing spot in the zone ("/acc fishhere"). For city/harbour spots
    /// whose sheet coordinate sits on dry land, the blind player walks to the
    /// water, stands where casting actually works and presses this - the exact
    /// spot is stored per FishingSpot RowId and, on every future visit, the walk
    /// heads straight there instead of the dry sheet marker. Persisted to config,
    /// so it survives restarts and overrides even the built-in table.
    /// </summary>
    public void CaptureHere()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var spots = GetSpotsInCurrentZone();   // sorted nearest-first
        if (spots.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoFishingSpots);
            return;
        }

        var target = spots[0];
        var dist   = PlacesService.Distance2D(player.Position, target.Position);
        if (dist > CaptureMaxDistance)
        {
            _tolk.SpeakInterrupt($"Kein Angelplatz nah genug. Nächster: {target.Name}, {dist:F0} Meter. " +
                                 "Stell dich an die Angelstelle und drück erneut.");
            return;
        }

        var coord = _places.WorldToMapCoord(player.Position);
        if (coord is not { } c)
        {
            _tolk.SpeakInterrupt("Aktuelle Karte unbekannt, kann die Stelle nicht merken.");
            return;
        }

        _config.FishingSpotOverrides[target.RowId] = new[] { c.X, c.Y };
        _pluginInterface.SavePluginConfig(_config);
        _log.Info($"[Fish] Merke Angelplatz '{target.Name}' (Row {target.RowId}) = Karte {c.X:F1}/{c.Y:F1} " +
                  $"Welt ({player.Position.X:F1}|{player.Position.Z:F1}).");
        _tolk.SpeakInterrupt($"Angelplatz {target.Name} hier gemerkt: Karte {c.X:F1}, {c.Y:F1}.");
    }

    /// <summary>
    /// Watches the live fishing state every frame and speaks the two moments a
    /// blind fisher cannot see for themselves:
    ///   * "Angelbereit" - the game reports the player can cast from where they
    ///     stand and face (FishingEventHandler.CanFish flips true in the ready
    ///     stance). This is the orientation cue: rotate until it speaks, then
    ///     cast. It answers exactly the gap left after walking to a spot - "water
    ///     is here, but which way do I face?" (user 2026-07-27).
    ///   * "Biss" - a fish is on the line, strike now (FishingState -> Bite).
    ///
    /// Reads the game's OWN state, never a reimplementation: the handler is the
    /// authority on whether a cast is possible. Access verified against
    /// FFXIVClientStructs (2026-07-27): EventFramework.GetEventHandlerById(id)
    /// returns the FishingEventHandler; id = EventHandlerContent.Fishing(21) &lt;&lt; 16
    /// | 1 (the singleton entry, mirroring Craft's 0xA0001). State@456, CanFish@464.
    /// The handler exists only while fishing is active, so a null result is the
    /// natural "not fishing" gate - no condition flag needed.
    /// </summary>
    public unsafe void Update()
    {
        var framework = EventFramework.Instance();
        FishingEventHandler* handler = framework == null
            ? null
            : (FishingEventHandler*)framework->GetEventHandlerById(
                  (uint)EventHandlerContent.Fishing << 16 | 1);

        if (handler == null)
        {
            // Not fishing (or just stopped): reset so the next session starts fresh.
            if (_wasFishing)
            {
                _wasFishing  = false;
                _lastState   = FishingState.None;
                _lastCanFish = false;
            }
            return;
        }

        var state   = handler->State;
        var canFish = handler->CanFish;

#if DEBUG
        if (state != _lastState || canFish != _lastCanFish)
            _log.Info($"[Angeln] State={state} CanFish={canFish}");
#endif

        if (!_wasFishing)
        {
            // First frame of a fishing session: prime the baselines so we never
            // announce on the initial read, only on a real change afterwards.
            _wasFishing  = true;
            _lastState   = state;
            _lastCanFish = canFish;
            return;
        }

        // Orientation cue: only meaningful in the standby stance (PoleReady),
        // before the line is out. Fires on the false->true edge as the player
        // turns to face castable water.
        if (canFish && !_lastCanFish && state == FishingState.PoleReady)
            _tolk.SpeakInterrupt(AccessibilityStrings.FishReady);

        // Bite: the one time-critical moment of the cast.
        if (state == FishingState.Bite && _lastState != FishingState.Bite)
            _tolk.SpeakInterrupt(AccessibilityStrings.FishBite);

        _lastState   = state;
        _lastCanFish = canFish;
    }

    /// <summary>
    /// Diagnostic: logs every object within 50 m with its kind, name, data id
    /// and distance. Purpose: find out whether the physical fishing holes (the
    /// ripples you cast at, especially the several piers of a city harbour that
    /// the single FishingSpot sheet row cannot distinguish) show up as game
    /// objects - if they do, they can be browsed like NPCs. Read-only.
    /// Run it standing ON a pier next to a real fishing hole.
    /// </summary>
    public void ProbeNearbyObjects()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NotLoggedIn);
            return;
        }

        var playerPos = player.Position;
        var hits = _objectTable
            .Where(o => o != null)
            .Select(o => (Obj: o, Dist: PlacesService.Distance2D(playerPos, o.Position)))
            .Where(x => x.Dist <= 50f)
            .OrderBy(x => x.Dist)
            .ToList();

        _log.Info($"[FishObj] === {hits.Count} Objekte in 50 m, Spieler=({playerPos.X:F1}|{playerPos.Z:F1}) " +
                  $"Gebiet={_clientState.TerritoryType} ===");
        foreach (var (obj, dist) in hits)
        {
            var name = obj.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = "(kein Name)";
            _log.Info($"[FishObj]   Art={obj.ObjectKind} DataId={obj.BaseId} Dist={dist:F1} m " +
                      $"Name='{name}' Welt=({obj.Position.X:F1}|{obj.Position.Z:F1})");
        }

        _tolk.SpeakInterrupt($"{hits.Count} Objekte in 50 Metern im Log.");
    }

    /// <summary>
    /// Eight-point compass bearing from <paramref name="from"/> to
    /// <paramref name="to"/>. Convention (verified, game-api.md): north = -Z,
    /// east = +X, bearing = atan2(dx, -dz) with 0 deg = north, 90 deg = east.
    /// </summary>
    private static string CompassDirection(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        var deg = MathF.Atan2(dx, -dz) * 180f / MathF.PI;
        if (deg < 0) deg += 360f;

        string[] names =
        {
            "nördlich", "nordöstlich", "östlich", "südöstlich",
            "südlich", "südwestlich", "westlich", "nordwestlich",
        };
        var index = (int)MathF.Round(deg / 45f) % 8;
        return names[index];
    }
}
