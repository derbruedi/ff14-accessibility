#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Debug-Sonde fuer die Frage des Users (2026-08-19): *"hm es gibt aufzuege aber
/// ich weiss nicht ob ich richtig stehe"*.
///
/// WARUM GEMESSEN UND NICHT GEBAUT WIRD. Was ein Aufzug technisch ist, steht in
/// keiner Quelle, die sich hier nachschlagen laesst:
/// <list type="bullet">
/// <item>Die Layout-Engine kennt KEINEN Aufzug-Typ. Geprueft wurde die
///   vollstaendige <c>InstanceType</c>-Liste der installierten
///   FFXIVClientStructs.dll - es gibt ExitRange, DoorRange, CollisionBox,
///   ClickableRange, aber nichts, was eine Hebebuehne benennt.</item>
/// <item>Im EObj-Sheet gibt es Objekte mit Aufzug-Namen ("Aufzug zur Bruecke",
///   "Aufzugshebel", "Hexalift"), aber die meisten tragen KEINE Position im
///   Level-Sheet - sie werden zur Laufzeit gesetzt.</item>
/// </list>
/// Ob der Aufzug also ein anvisierbares Objekt ist (dann heisst "richtig stehen"
/// schlicht "in Reichweite"), oder eine reine Plattform, auf der man physisch
/// stehen muss, entscheidet ein Blick ins laufende Spiel - nicht eine Annahme.
///
/// WAS DIE SONDE LIEFERT:
/// <list type="number">
/// <item>Eine Momentaufnahme aller Objekte im Umkreis, mit WAAGERECHTEM und
///   SENKRECHTEM Abstand getrennt. Getrennt, weil genau darin die Antwort steckt:
///   wer auf einer Plattform steht, ist waagerecht fast genau ueber ihrem
///   Mittelpunkt, aber senkrecht ein Stueck darueber.</item>
/// <item>Eine Mitschrift der eigenen Position, zwei Mal pro Sekunde. Faehrt der
///   Aufzug los, waehrend man draufsteht, wandert die eigene Hoehe mit - das ist
///   der Beweis "ich stand richtig", und aus dem Abstand zum Objekt in genau
///   diesen Zeilen laesst sich ableiten, welcher Abstand "drauf" bedeutet.</item>
/// </list>
///
/// Die Layout-Instanzen (Kollisionsboxen) bleiben bewusst aussen vor: sie sind
/// nur ueber VTable-Aufrufe erreichbar, und ein Fehlgriff dort stuerzt das Spiel
/// ab. Sie sind der zweite Schritt, falls diese Messung nicht reicht.
/// </summary>
public sealed class LiftProbe
{
    /// <summary>Wie lange mitgeschrieben wird.</summary>
    private static readonly TimeSpan TrackDuration = TimeSpan.FromSeconds(20);

    /// <summary>Abstand zwischen zwei Zeilen der Mitschrift.</summary>
    private static readonly TimeSpan TrackInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>Umkreis der Momentaufnahme in Metern.</summary>
    private const float SnapshotRange = 25f;

    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    private DateTime _trackUntil;
    private DateTime _nextSample;
    private Vector3 _lastSample;
    private bool _movedAnnounced;

    public LiftProbe(IObjectTable objectTable, ITargetManager targetManager, TolkService tolk, IPluginLog log)
    {
        _objectTable = objectTable;
        _targetManager = targetManager;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>Ob die Mitschrift gerade laeuft (dann ruft Plugin.cs <see cref="Update"/>).</summary>
    public bool IsTracking => DateTime.UtcNow < _trackUntil;

    /// <summary>
    /// Startet Momentaufnahme und Mitschrift. Zweiter Aufruf waehrend einer
    /// laufenden Mitschrift bricht sie ab - man will nicht warten muessen, bis
    /// eine Fehlmessung von selbst endet.
    /// </summary>
    public void Start()
    {
        if (IsTracking)
        {
            _trackUntil = DateTime.MinValue;
            _tolk.SpeakInterrupt("Aufzug-Sonde abgebrochen.");
            _log.Info("[LiftProbe] Mitschrift abgebrochen.");
            return;
        }

        var player = _objectTable.LocalPlayer;
        if (player == null)
        {
            _tolk.SpeakInterrupt("Aufzug-Sonde: kein Spieler.");
            return;
        }

        Snapshot(player.Position);

        _trackUntil = DateTime.UtcNow + TrackDuration;
        _nextSample = DateTime.UtcNow;
        _lastSample = player.Position;
        _movedAnnounced = false;
        _tolk.SpeakInterrupt($"Aufzug-Sonde laeuft, {TrackDuration.TotalSeconds:F0} Sekunden. "
                             + "Jetzt auf den Aufzug stellen und ihn ausloesen.");
    }

    /// <summary>Eine Zeile der Mitschrift, wenn es Zeit dafuer ist. Jeden Frame aufrufbar.</summary>
    public void Update()
    {
        var now = DateTime.UtcNow;
        if (now >= _trackUntil)
        {
            if (_trackUntil != DateTime.MinValue && _trackUntil != default)
            {
                _trackUntil = DateTime.MinValue;
                _tolk.SpeakInterrupt("Aufzug-Sonde fertig.");
                _log.Info("[LiftProbe] === Mitschrift beendet ===");
            }
            return;
        }

        if (now < _nextSample) return;
        _nextSample = now + TrackInterval;

        var player = _objectTable.LocalPlayer;
        if (player == null) return;

        var pos = player.Position;
        var moved = pos - _lastSample;
        _lastSample = pos;

        // Das naechste Objekt mitschreiben: nur so laesst sich hinterher sagen,
        // WELCHER Abstand zu WELCHEM Objekt "ich stehe drauf" bedeutet hat.
        var nearest = _objectTable
            .Where(o => o.GameObjectId != player.GameObjectId)
            .Select(o => (Obj: o, Flat: Flat(pos, o.Position)))
            .OrderBy(x => x.Flat)
            .FirstOrDefault();

        var nearestText = nearest.Obj != null
            ? $"naechstes='{Name(nearest.Obj.Name.TextValue)}' waagerecht={nearest.Flat:F2}m "
              + $"senkrecht={pos.Y - nearest.Obj.Position.Y:+0.00;-0.00}m kind={nearest.Obj.ObjectKind}"
            : "naechstes=-";

        _log.Info($"[LiftProbe] pos={pos.X:F2}/{pos.Y:F2}/{pos.Z:F2} "
                  + $"delta={moved.X:+0.00;-0.00}/{moved.Y:+0.00;-0.00}/{moved.Z:+0.00;-0.00} "
                  + nearestText);

        // Eine senkrechte Bewegung ohne waagerechte ist genau das gesuchte
        // Ereignis: der Aufzug faehrt, und der Spieler faehrt mit. Einmal
        // ansagen, damit er es im Moment des Geschehens hoert und nicht erst im
        // Log - die Sonde soll ihm die Frage schon waehrend der Messung
        // beantworten.
        if (!_movedAnnounced && MathF.Abs(moved.Y) > 0.3f && Flat(moved, Vector3.Zero) < 0.3f)
        {
            _movedAnnounced = true;
            _tolk.SpeakInterrupt($"Du faehrst. Hoehe {(moved.Y > 0 ? "steigt" : "faellt")}.");
            _log.Info($"[LiftProbe] >>> SENKRECHTE FAHRT erkannt: dY={moved.Y:F2} m in einem Schritt.");
        }
    }

    /// <summary>Was steht hier gerade herum - mit waagerechtem und senkrechtem Abstand getrennt.</summary>
    private void Snapshot(Vector3 playerPos)
    {
        var near = _objectTable
            .Where(o => Flat(playerPos, o.Position) <= SnapshotRange)
            .OrderBy(o => Flat(playerPos, o.Position))
            .ToList();

        var targetId = _targetManager.Target?.GameObjectId ?? 0;

        _log.Info($"[LiftProbe] === Momentaufnahme: {near.Count} Objekte in {SnapshotRange:F0} m, "
                  + $"Spieler @ {playerPos.X:F2}/{playerPos.Y:F2}/{playerPos.Z:F2} ===");
        foreach (var o in near)
        {
            _log.Info($"[LiftProbe]   waagerecht={Flat(playerPos, o.Position),6:0.00}m "
                      + $"senkrecht={playerPos.Y - o.Position.Y,7:+0.00;-0.00}m "
                      + $"{o.ObjectKind,-14} DataId={o.BaseId} '{Name(o.Name.TextValue)}' "
                      + $"zielbar={o.IsTargetable} anvisiert={(o.GameObjectId == targetId)} "
                      + $"pos={o.Position.X:F2}/{o.Position.Y:F2}/{o.Position.Z:F2}");
        }
    }

    /// <summary>Waagerechter Abstand - die Hoehe ist genau die Groesse, die hier getrennt gehoert.</summary>
    private static float Flat(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static string Name(string raw) => string.IsNullOrWhiteSpace(raw) ? "<leer>" : raw;
}
#endif
