#if DEBUG
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer EINE offene Frage, die ueber ein ganzes Feature entscheidet:
/// ist <c>Transform.Scale</c> einer <c>ExitRange</c> die HALBE oder die VOLLE
/// Ausdehnung ihrer Trigger-Box?
///
/// <para>
/// WARUM DAS ZAEHLT. Gemessener Fall Neu-Gridania nach Tiefer Wald (Log
/// 2026-08-22 10:53): der Lauf endete 18,6 m vor der Grenzenmitte, das Wegenetz
/// reicht nicht weiter. Die Box hat Scale (15,6|3,8|15,0). Ist das die halbe
/// Ausdehnung, reicht sie bis X = 154,5 und der Spieler stand bei X = 152,5 -
/// also nur ZWEI Meter davor, und ein Ziel am Boxrand statt in der Boxmitte
/// wuerde den Uebergang ausloesen, ganz ohne besseres Wegenetz. Ist es die volle
/// Ausdehnung, fehlen zehn Meter und der Weg ueber den Boxrand traegt nicht.
/// Die Frage ist in docs/game-api.md seit 2026-08-09 als offen markiert und
/// offline nicht zu beantworten.
/// </para>
///
/// <para>
/// WIE GEMESSEN WIRD. Beim Zonenwechsel steht die Figur laengst in der neuen
/// Zone - die Position im Moment des Ausloesens ist dann nicht mehr abfragbar.
/// Deshalb laeuft ein Ringpuffer der letzten Sekunden mit, und beim Wechsel wird
/// JEDE gepufferte Position gegen die Boxen der ALTEN Zone geprueft, in beiden
/// Lesarten. Die Zeile, ab der eine Lesart "DRIN" sagt, ist die Antwort: nur die
/// zutreffende Lesart kann kurz vor dem Wechsel greifen.
/// </para>
///
/// <para>
/// Die Boxen kommen aus <c>planmap.lgb</c> ueber Lumina - dieselbe Quelle, mit
/// der die 978 Uebergaenge des Spiels offline vermessen wurden. Bewusst NICHT
/// ueber die Layout-Engine im Speicher: fuer eine Messung braucht es keine
/// Zeigerketten und keine Struct-Offsets, die bei jedem Patch brechen koennen.
/// </para>
///
/// NACH DER MESSUNG LOESCHEN (Sonden-Konvention).
/// </summary>
internal sealed class ZoneExitProbe
{
    /// <summary>How long a tail of positions to keep. A zone change is noticed a
    /// frame or two late, and at running speed that is already metres of travel.</summary>
    private const double HistorySeconds = 4.0;

    /// <summary>One sample per this many seconds - dense enough to see the metre
    /// at which the box triggers, sparse enough to stay readable in the log.</summary>
    private const double SampleIntervalS = 0.1;

    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly IDataManager _data;
    private readonly IPluginLog _log;

    private readonly List<(DateTime At, Vector3 Pos)> _history = new();
    private ushort _territory;
    private DateTime _lastSample = DateTime.MinValue;

    public ZoneExitProbe(IClientState clientState, IObjectTable objectTable, IDataManager data, IPluginLog log)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _data = data;
        _log = log;
        _territory = (ushort)clientState.TerritoryType;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        var current = (ushort)_clientState.TerritoryType;
        if (current != _territory)
        {
            var previous = _territory;
            _territory = current;
            Report(previous, current);
            _history.Clear();
            return;
        }

        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var stamp = DateTime.UtcNow;
        if ((stamp - _lastSample).TotalSeconds < SampleIntervalS) return;
        _lastSample = stamp;

        _history.Add((stamp, player.Position));
        while (_history.Count > 0 && (stamp - _history[0].At).TotalSeconds > HistorySeconds)
            _history.RemoveAt(0);
    }

    private void Report(ushort from, ushort to)
    {
        _log.Info($"[ExitProbe] === Zonenwechsel {from} nach {to}, {_history.Count} gepufferte Positionen ===");
        if (_history.Count == 0)
        {
            _log.Info("[ExitProbe] Kein Verlauf - die Sonde kann nichts sagen (Teleport oder frisch geladen).");
            return;
        }

        if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(from, out var territory))
        {
            _log.Info($"[ExitProbe] Territory {from} steht nicht im Sheet.");
            return;
        }

        var bg = territory.Bg.ExtractText();
        if (string.IsNullOrEmpty(bg) || !bg.Contains("/level/"))
        {
            _log.Info($"[ExitProbe] Territory {from} hat keinen brauchbaren Bg-Pfad (war: {bg}).");
            return;
        }

        var path = "bg/" + bg.Substring(0, bg.LastIndexOf("/level/", StringComparison.Ordinal) + 7) + "planmap.lgb";
        LgbFile? lgb;
        try
        {
            lgb = _data.GetFile<LgbFile>(path);
        }
        catch (Exception ex)
        {
            _log.Info($"[ExitProbe] {path} nicht lesbar: {ex.Message}");
            return;
        }

        if (lgb == null)
        {
            _log.Info($"[ExitProbe] {path} gibt es nicht.");
            return;
        }

        var boxes = new List<ExitBox>();
        foreach (var layer in lgb.Layers)
        {
            foreach (var instance in layer.InstanceObjects)
            {
                if (instance.AssetType != LayerEntryType.ExitRange) continue;
                var exit = (LayerCommon.ExitRangeInstanceObject)instance.Object;
                var transform = instance.Transform;
                boxes.Add(new ExitBox(
                    new Vector3(transform.Translation.X, transform.Translation.Y, transform.Translation.Z),
                    new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z),
                    transform.Rotation.Y,
                    exit.TerritoryType,
                    exit.PlayerRunningDirection));
            }
        }

        _log.Info($"[ExitProbe] {boxes.Count} ExitRange-Boxen in {path}");
        if (boxes.Count == 0) return;

        // Die naechstgelegene Box zur letzten Position - mit grosser Wahrscheinlichkeit
        // die, durch die der Spieler gegangen ist. Die Zielzone wird BEWUSST NICHT als
        // Filter benutzt: passt sie nicht zur tatsaechlichen neuen Zone, ist genau das
        // ein Befund und keine Stoerung.
        var last = _history[_history.Count - 1].Pos;
        var chosen = boxes[0];
        var chosenDistance = float.MaxValue;
        foreach (var box in boxes)
        {
            var distance = Vector2.Distance(new Vector2(box.Centre.X, box.Centre.Z), new Vector2(last.X, last.Z));
            if (distance >= chosenDistance) continue;
            chosenDistance = distance;
            chosen = box;
        }

        _log.Info($"[ExitProbe] naechste Box: Mitte=({chosen.Centre.X:F1}|{chosen.Centre.Y:F1}|{chosen.Centre.Z:F1}) " +
                  $"Scale=({chosen.Scale.X:F1}|{chosen.Scale.Y:F1}|{chosen.Scale.Z:F1}) " +
                  $"BoxDrehung={chosen.Yaw * 180f / MathF.PI:F0} Grad ZielTerritory={chosen.Destination} " +
                  $"(tatsaechlich {to}) Laufrichtung={chosen.RunningDirection * 180f / MathF.PI:F0} Grad " +
                  $"Abstand der letzten Position={chosenDistance:F1} m");

        var newest = _history[_history.Count - 1].At;
        foreach (var (at, position) in _history)
        {
            var local = ToBoxSpace(position, chosen.Centre, chosen.Yaw);
            var insideHalf = MathF.Abs(local.X) <= chosen.Scale.X && MathF.Abs(local.Z) <= chosen.Scale.Z;
            var insideFull = MathF.Abs(local.X) <= chosen.Scale.X / 2f && MathF.Abs(local.Z) <= chosen.Scale.Z / 2f;
            _log.Info($"[ExitProbe]   -{(newest - at).TotalSeconds:F1}s " +
                      $"pos=({position.X:F1}|{position.Y:F1}|{position.Z:F1}) " +
                      $"lokal=({local.X:F1}|{local.Y:F1}|{local.Z:F1})  " +
                      $"HALB(Scale=Halbmass): {(insideHalf ? "DRIN" : "draussen")}  " +
                      $"VOLL(Scale=Vollmass): {(insideFull ? "DRIN" : "draussen")}");
        }

        _log.Info("[ExitProbe] Auswertung: Welche Lesart kurz vor dem Wechsel als EINZIGE 'DRIN' sagt, " +
                  "die stimmt. Sagen beide 'draussen', loest der Uebergang nicht ueber diese Box aus.");
    }

    /// <summary>Position in the box's own coordinates: shift to the centre, then
    /// undo the box's yaw. Only the Y rotation is undone - the trigger boxes of
    /// zone borders stand upright.</summary>
    private static Vector3 ToBoxSpace(Vector3 world, Vector3 centre, float yaw)
    {
        var delta = world - centre;
        var sin = MathF.Sin(-yaw);
        var cos = MathF.Cos(-yaw);
        return new Vector3(delta.X * cos - delta.Z * sin, delta.Y, delta.X * sin + delta.Z * cos);
    }

    private readonly record struct ExitBox(
        Vector3 Centre,
        Vector3 Scale,
        float Yaw,
        uint Destination,
        float RunningDirection);
}
#endif
