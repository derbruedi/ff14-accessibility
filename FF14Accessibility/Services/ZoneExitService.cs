using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace FF14Accessibility.Services;

/// <summary>One zone transition of the current zone, as the game itself holds it.</summary>
/// <param name="InstanceKey">Layout instance key - identity of this exit.</param>
/// <param name="ExitType">ExitRangeType: 1 = ZoneLine (walk through), 2 = Invisible.</param>
/// <param name="ZoneId">Raw ZoneId field - meaning not yet measured.</param>
/// <param name="TargetTerritoryTypeId">Raw TerritoryType field: the zone behind the line.</param>
/// <param name="TargetZoneName">Resolved place name of <paramref name="TargetTerritoryTypeId"/>.</param>
/// <param name="RunningDirection">Raw PlayerRunningDirection field - see the probe.</param>
/// <param name="Position">Centre of the trigger box (world coordinates).</param>
/// <param name="Scale">Extent of the trigger box - how wide the line is.</param>
/// <param name="Rotation">Orientation of the trigger box.</param>
/// <param name="IsActive">Whether the layout instance is currently active.</param>
public sealed record ZoneExitRange(
    uint InstanceKey,
    int ExitType,
    ushort ZoneId,
    ushort TargetTerritoryTypeId,
    string TargetZoneName,
    int Index,
    uint DestInstanceId,
    uint ReturnInstanceId,
    float RunningDirection,
    Vector3 Position,
    Vector3 Scale,
    Quaternion Rotation,
    bool IsActive);

/// <summary>
/// Reads the REAL zone transitions of the current zone out of the game's layout
/// engine.
///
/// WHY THIS EXISTS. The auto-walk aims zone transitions at their MAP SYMBOL: a
/// row of the MapMarker sheet, stored in map pixels and converted to world
/// coordinates (see <see cref="PlacesService"/>). That symbol is artwork placed
/// on the map, not the border itself - it carries no extent and no direction. So
/// the walk ends up somewhere NEAR the transition and the player stands there
/// without crossing, which is exactly what was reported (2026-08-09: "ich bin
/// jetzt wieder an einem uebergang wo ich nicht ruebergehe weil ich evtl schief
/// stehe").
///
/// The game does hold the real thing. Every transition is an
/// <c>ExitRangeLayoutInstance</c> (InstanceType.ExitRange = 41, ilspycmd-verified
/// against FFXIVClientStructs 2026-08-09) with:
///   Transform (@64, inherited from TriggerBoxLayoutInstance) - centre AND extent
///     of the trigger box, i.e. where the line really runs and how wide it is
///   PlayerRunningDirection (float @148) - a direction belonging to the exit
///   TerritoryType (ushort @134) - the zone behind it
///   ExitType - ZoneLine = 1 (walk through) or Invisible = 2
/// Reached via LayoutWorld.Instance()->ActiveLayout->Layers (StdMap @552) ->
/// LayerManager.Instances (StdMap @40), filtered on Id.Type == ExitRange.
///
/// NOT YET MEASURED, AND THEREFORE NOT USED FOR WALKING: what
/// PlayerRunningDirection means - unit, reference frame, and which of the two
/// ways through the line it points. A wrong guess there would steer the
/// character AWAY from the border, which is worse than today's behaviour. The
/// probe below logs the raw value next to several readings of it plus the
/// player's own bearing, so one look at a real transition settles it.
/// </summary>
public sealed unsafe class ZoneExitService
{
    private readonly IObjectTable _objectTable;
    private readonly IClientState _clientState;
    private readonly IDataManager _data;
    private readonly PlacesService _places;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public ZoneExitService(
        IObjectTable objectTable,
        IClientState clientState,
        IDataManager data,
        PlacesService places,
        TolkService tolk,
        IPluginLog log)
    {
        _objectTable = objectTable;
        _clientState = clientState;
        _data = data;
        _places = places;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>
    /// All exit ranges the layout engine currently holds. Empty when the layout
    /// is not loaded (zoning, cutscene) - never throws.
    /// </summary>
    public List<ZoneExitRange> ReadExitRanges()
    {
        var result = new List<ZoneExitRange>();

        var world = LayoutWorld.Instance();
        if (world == null)
        {
            _log.Warning("[Exit] LayoutWorld nicht verfügbar.");
            return result;
        }

        var layout = world->ActiveLayout;
        if (layout == null)
        {
            _log.Warning("[Exit] Kein ActiveLayout (Zonenwechsel?).");
            return result;
        }

        // StdMap enumerates as StdPair (Item1 = key, Item2 = value); the value
        // is a Pointer<T> wrapper, hence the second .Value.
        foreach (var layerEntry in layout->Layers)
        {
            var layer = layerEntry.Item2.Value;
            if (layer == null) continue;

            foreach (var instEntry in layer->Instances)
            {
                var inst = instEntry.Item2.Value;
                if (inst == null) continue;
                if (inst->Id.Type != InstanceType.ExitRange) continue;

                var exit = (ExitRangeLayoutInstance*)inst;
                var transform = exit->TriggerBoxLayoutInstance.Transform;

                var name = string.Empty;
                if (exit->TerritoryType != 0
                    && _data.GetExcelSheet<LuminaTerritoryType>()
                            .TryGetRow(exit->TerritoryType, out var territory))
                {
                    name = territory.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
                }

                result.Add(new ZoneExitRange(
                    InstanceKey: inst->Id.InstanceKey,
                    ExitType: (int)exit->ExitType,
                    ZoneId: exit->ZoneId,
                    TargetTerritoryTypeId: exit->TerritoryType,
                    TargetZoneName: name,
                    Index: exit->Index,
                    DestInstanceId: exit->DestInstanceId,
                    ReturnInstanceId: exit->ReturnInstanceId,
                    RunningDirection: exit->PlayerRunningDirection,
                    Position: transform.Translation,
                    Scale: transform.Scale,
                    Rotation: transform.Rotation,
                    IsActive: inst->IsActive));
            }
        }

        return result;
    }

    /// <summary>Horizontal distance - heights differ by metres at a border.</summary>
    private static float Distance2D(Vector3 a, Vector3 b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>
    /// How far a map symbol may sit from the border it belongs to. MEASURED
    /// 2026-08-09 (Ul'dah, both crossings): genuine pairs came out at 0,27 to
    /// 6,77 m, while symbols with no border at all were 21, 37 and 91 m from
    /// the nearest one. 15 m separates the two groups with room on both sides.
    /// </summary>
    private const float MaxSymbolGap = 15f;

    /// <summary>
    /// The real border behind a transition map symbol, or null when there is
    /// none. Both conditions must hold: the exit has to lead to the map the
    /// symbol names, AND it has to sit within <see cref="MaxSymbolGap"/> of it.
    ///
    /// The second test is not belt-and-braces. Not every transition symbol has
    /// a border: Ul'dah lists 10 symbols against 7 exit ranges, and the extra
    /// ones ('Die Sanduhr', 'Wachstube der Legion') are doors and instance
    /// entrances - those are entered by TALKING to them, not by walking
    /// through. Without the distance test the nearest unrelated border would be
    /// picked and the walk would go somewhere else entirely.
    /// </summary>
    public ZoneExitRange? FindExitForMap(uint targetMapId, Vector3 symbolPosition)
    {
        if (targetMapId == 0) return null;

        var sheet = _data.GetExcelSheet<LuminaTerritoryType>();
        ZoneExitRange? best = null;
        var bestGap = float.MaxValue;

        foreach (var exit in ReadExitRanges())
        {
            if (!sheet.TryGetRow(exit.TargetTerritoryTypeId, out var territory)) continue;
            if (territory.Map.RowId != targetMapId) continue;

            var gap = Distance2D(exit.Position, symbolPosition);
            if (gap > MaxSymbolGap || gap >= bestGap) continue;

            best = exit;
            bestGap = gap;
        }

        if (best != null)
            _log.Info($"[Uebergang] Karte {targetMapId}: echte Grenze key={best.InstanceKey} " +
                      $"bei ({best.Position.X:F1}|{best.Position.Z:F1}), {bestGap:F1} m vom Symbol, " +
                      $"Laufrichtung {best.RunningDirection * 180f / MathF.PI:F0} Grad.");
        else
            _log.Info($"[Uebergang] Karte {targetMapId}: keine Grenze innerhalb {MaxSymbolGap:F0} m " +
                      "vom Symbol - vermutlich Tuer oder Instanz-Eingang, altes Verhalten.");

        return best;
    }

    /// <summary>
    /// A point <paramref name="metres"/> beyond the border, in the direction the
    /// exit says a player crosses it.
    ///
    /// PlayerRunningDirection is in RADIANS, in the same frame as
    /// GameObject.Rotation - so the direction vector is (sin, 0, cos). MEASURED
    /// 2026-08-09, two ways. Unit: read as radians all ten values come out as
    /// clean 5-degree multiples (15/45/45/75/90/165/195/225/230/270); read as
    /// degrees they are meaningless fractions. Direction: the two halves of a
    /// crossing point exactly 180 degrees apart (key 2377082 at X=-115,58 says
    /// 90 deg = +X, its partner 2379246 at X=-114,31 says 270 deg = -X), i.e.
    /// each side points INTO the other zone; and the player's own crossing ran
    /// along 36 degrees where the border said 45.
    ///
    /// The height is the caller's, not the box centre's: the box spans the air
    /// above the ground (centre Y=8,21 where the floor is at 4,05).
    /// </summary>
    public static Vector3 PointBeyond(ZoneExitRange exit, float groundY, float metres) =>
        new(exit.Position.X + MathF.Sin(exit.RunningDirection) * metres,
            groundY,
            exit.Position.Z + MathF.Cos(exit.RunningDirection) * metres);

#if DEBUG
    /// <summary>
    /// Measures the transitions of the current zone against what the auto-walk
    /// aims at today. Three questions, all answered in the log:
    ///
    /// 1. HOW FAR OFF IS THE MAP SYMBOL? For every transition marker of the
    ///    "Orte" category the nearest exit range and their distance. That
    ///    distance IS the reason the player ends up beside the border.
    /// 2. WHERE IS THE PLAYER RIGHT NOW? Distance and bearing to every exit
    ///    range, so a player standing at one sees whether they are inside the
    ///    box or next to it.
    /// 3. WHAT IS PlayerRunningDirection? Logged raw, as degrees (assuming
    ///    radians), and as radians (assuming degrees), next to the box yaw and
    ///    the player's own rotation. Comparing those at a transition the player
    ///    has just walked through settles the unit and the reference frame.
    /// </summary>
    public void ProbeExitRanges()
    {
        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _log.Info("[ExitProbe] Kein Spieler.");
            return;
        }

        var exits = ReadExitRanges();
        _log.Info($"[ExitProbe] ── Territory {_clientState.TerritoryType} ── {exits.Count} Übergänge, " +
                  $"Spieler=({player.Position.X:F2}|{player.Position.Y:F2}|{player.Position.Z:F2}) " +
                  $"rot={player.Rotation:F3} ({RadToDeg(player.Rotation):F1}°)");

        foreach (var e in exits.OrderBy(e => Distance2D(e.Position, player.Position)))
        {
            var d2 = Distance2D(e.Position, player.Position);
            var dy = e.Position.Y - player.Position.Y;

            // Is the player inside the box? Scale is the extent; without a
            // measured convention (half-extent vs. full size) BOTH readings are
            // logged rather than one of them asserted.
            var dx = MathF.Abs(e.Position.X - player.Position.X);
            var dz = MathF.Abs(e.Position.Z - player.Position.Z);
            var insideHalf = dx <= e.Scale.X && dz <= e.Scale.Z;
            var insideFull = dx <= e.Scale.X / 2f && dz <= e.Scale.Z / 2f;

            _log.Info(
                $"[ExitProbe] key={e.InstanceKey} Typ={e.ExitType}" +
                $"{(e.ExitType == 1 ? "(ZoneLine)" : e.ExitType == 2 ? "(Invisible)" : "")} " +
                $"aktiv={e.IsActive} -> Territory={e.TargetTerritoryTypeId} '{e.TargetZoneName}' " +
                $"ZoneId={e.ZoneId} Index={e.Index} Dest={e.DestInstanceId} Return={e.ReturnInstanceId}");
            _log.Info(
                $"[ExitProbe]   Box Mitte=({e.Position.X:F2}|{e.Position.Y:F2}|{e.Position.Z:F2}) " +
                $"Ausdehnung=({e.Scale.X:F2}|{e.Scale.Y:F2}|{e.Scale.Z:F2}) " +
                $"BoxYaw={YawFromQuaternion(e.Rotation):F3} ({RadToDeg(YawFromQuaternion(e.Rotation)):F1}°)");
            _log.Info(
                $"[ExitProbe]   Spieler: Entfernung={d2:F2} m, Höhe={dy:+0.00;-0.00} m, " +
                $"Peilung={RadToDeg(BearingTo(player.Position, e.Position)):F1}°, " +
                $"drin(Scale=halb)={insideHalf} drin(Scale=voll)={insideFull}");
            _log.Info(
                $"[ExitProbe]   RunningDirection roh={e.RunningDirection:F4} " +
                $"| als Radiant={RadToDeg(e.RunningDirection):F1}° " +
                $"| als Grad={e.RunningDirection:F1}° " +
                $"(Spieler rot={player.Rotation:F3}={RadToDeg(player.Rotation):F1}°)");
        }

        // Frage 1: der Abstand zwischen Kartensymbol und echter Grenze.
        var markers = _places.GetPlaces().Where(p => p.IsZoneTransition).ToList();
        _log.Info($"[ExitProbe] ── Kartensymbole gegen echte Übergänge ── {markers.Count} Symbole");
        foreach (var m in markers)
        {
            if (exits.Count == 0)
            {
                _log.Info($"[ExitProbe]   '{m.Name}' Symbol=({m.Position.X:F2}|{m.Position.Z:F2}) " +
                          "- kein Übergang zum Vergleich.");
                continue;
            }

            var nearest = exits.OrderBy(e => Distance2D(e.Position, m.Position)).First();
            var gap = Distance2D(nearest.Position, m.Position);
            _log.Info(
                $"[ExitProbe]   '{m.Name}' Symbol=({m.Position.X:F2}|{m.Position.Z:F2}) " +
                $"nächste Grenze key={nearest.InstanceKey} '{nearest.TargetZoneName}' " +
                $"=({nearest.Position.X:F2}|{nearest.Position.Z:F2}) ABSTAND={gap:F2} m");
        }

        // Spoken result: the probe has to be usable by the player standing at
        // the transition, not only by whoever reads the log afterwards.
        if (exits.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ExitProbeNone);
            return;
        }

        var closest = exits.OrderBy(e => Distance2D(e.Position, player.Position)).First();
        _tolk.SpeakInterrupt(AccessibilityStrings.ExitProbeResult(
            exits.Count,
            closest.TargetZoneName,
            Distance2D(closest.Position, player.Position)));
    }

    private static float RadToDeg(float rad) => rad * 180f / MathF.PI;

    /// <summary>Bearing from a to b in the same frame the game uses for rotation.</summary>
    private static float BearingTo(Vector3 from, Vector3 to) =>
        MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>Yaw (rotation about the vertical axis) of a layout transform.</summary>
    private static float YawFromQuaternion(Quaternion q) =>
        MathF.Atan2(2f * (q.W * q.Y + q.X * q.Z), 1f - 2f * (q.Y * q.Y + q.Z * q.Z));
#endif
}
