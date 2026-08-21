using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Eine Gefahrenflaeche als reine Geometrie - ohne Gegner, ohne Sheet-Zeile, ohne
/// Objekttabelle. Genau deshalb gibt es sie: <see cref="Contains"/> laesst sich
/// fuer JEDEN Punkt aufrufen, nicht nur fuer den, auf dem der Spieler steht. Aus
/// der Frage "stehe ich drin?" wird damit die Frage "wo ist der naechste Punkt,
/// der draussen ist?".
///
/// Die Formen sind dieselben wie in <see cref="CombatService"/>: der baut diese
/// Flaechen und prueft seinen eigenen Warnton damit, sodass Warnung und
/// Fluchtrichtung nie auseinanderlaufen koennen.
/// </summary>
public readonly record struct DangerZone(
    DangerShape Shape,
    Vector3     Center,
    float       Range,
    float       Facing,       // Blickrichtung des Werfers (rad), nur Kegel/Linie
    float       HalfAngleRad, // halber Oeffnungswinkel, nur Kegel
    float       HalfWidth)    // halbe Breite, nur Linie
{
    /// <summary>Liegt dieser Punkt in der Flaeche? Reine Mathematik in der
    /// Grundebene - Hoehe zaehlt nicht, wie ueberall sonst auch.</summary>
    public bool Contains(Vector3 p) => ContainsWithMargin(p, 0f);

    /// <summary>
    /// Wie <see cref="Contains"/>, aber die Flaeche waechst um
    /// <paramref name="margin"/> Meter. Fuer die Fluchtsuche: ein Punkt genau auf
    /// der Kante ist rechnerisch draussen und praktisch getroffen - der Spieler
    /// steht dort mit halbem Fuss in der Flaeche, und bis er ankommt, hat sich
    /// der Gegner vielleicht schon ein Stueck weitergedreht.
    /// </summary>
    public bool ContainsWithMargin(Vector3 p, float margin)
    {
        var dx = p.X - Center.X;
        var dz = p.Z - Center.Z;

        switch (Shape)
        {
            case DangerShape.Circle:
            {
                var r = Range + margin;
                return dx * dx + dz * dz <= r * r;
            }

            case DangerShape.Cone:
            {
                var dist2 = dx * dx + dz * dz;
                var r = Range + margin;
                if (dist2 > r * r) return false;
                // Innerhalb des Sicherheitsabstands um die Spitze zaehlt der
                // Kegel als getroffen, egal in welcher Richtung: dort ist er
                // beliebig schmal, und ein Punkt "seitlich neben der Spitze"
                // waere nach einem Meter wieder drin.
                if (dist2 <= margin * margin) return true;
                // Der Sicherheitsabstand wirkt beim Kegel als zusaetzlicher
                // WINKEL: je naeher an der Spitze, desto mehr Grad entsprechen
                // demselben Meter. Genau das ist die Bogenlaenge margin/dist.
                var dist = MathF.Sqrt(dist2);
                var rel  = MathF.Abs(Normalize(MathF.Atan2(dx, dz) - Facing));
                return rel <= HalfAngleRad + margin / dist;
            }

            case DangerShape.Line:
            {
                var fx = MathF.Sin(Facing);
                var fz = MathF.Cos(Facing);
                var along = dx * fx + dz * fz;
                if (along < -margin || along > Range + margin) return false;
                var lateral = MathF.Abs(dx * fz - dz * fx);
                return lateral <= HalfWidth + margin;
            }

            default:
                return false;
        }
    }

    private static float Normalize(float rad)
    {
        while (rad >  MathF.PI) rad -= MathF.Tau;
        while (rad < -MathF.PI) rad += MathF.Tau;
        return rad;
    }
}

/// <summary>Die drei Formen, deren Geometrie belegt ist (siehe AoeShape).</summary>
public enum DangerShape
{
    Circle,
    Cone,
    Line,
}

/// <summary>
/// Sucht den naechsten Punkt, auf dem man SICHER steht - und auf den man auch
/// wirklich laufen kann.
///
/// WARUM: bisher sagte das Plugin nur, DASS man in einer Flaeche steht
/// (AoeWarningService brummt, solange man drinsteht, und verstummt beim
/// Heraustreten). Das ist ein Heiss-Kalt-Signal. Ein sehender Spieler sieht
/// dagegen in einem Blick, WOHIN er ausweichen kann. Spielerwunsch 2026-08-19:
/// *"wir muessen eine bessere moeglichkeit finden damit wir aus flaechenangriffen
/// oder anderen bossmechaniken in sichere bereiche kommen die sehenden sehen wo
/// sie hinlaufen muessen bzw koennen aber wir nicht"*.
///
/// WIE: die Gefahrenflaechen sind reine Geometrie (<see cref="DangerZone"/>),
/// also lassen sie sich fuer beliebige Punkte auswerten. Der Dienst legt einen
/// Faecher von Probepunkten um den Spieler, verwirft alle, die in irgendeiner
/// aktiven Flaeche liegen, und nimmt den naechstgelegenen Rest.
///
/// ZWEI DINGE, DIE DIE SUCHE ERST BRAUCHBAR MACHEN:
/// <list type="bullet">
/// <item><b>Begehbarkeit.</b> Ein Fluchtpunkt, zu dem man nicht laufen kann, ist
///   schlimmer als keiner - man rennt gegen eine Wand, waehrend der Cast
///   ablaeuft. Deshalb wird der Gewinner ueber das Wegenetz geprueft, und zwar
///   mit NearestPointReachable: NearestPoint allein gibt bereitwillig einen Punkt
///   hinter einer Wand zurueck (dieselbe Falle wie beim Auto-Lauf, siehe
///   AutoWalkService).</item>
/// <item><b>Traegheit.</b> Der Zielpunkt wird festgehalten, solange er sicher
///   bleibt. Wuerde jeder Frame neu gesucht, spraenge die Richtung bei jeder
///   Drehung des Gegners, und der Peil-Ton waere unbenutzbar.</item>
/// </list>
///
/// WAS ER NICHT KANN, und das ist bewusst so: Flaechen, die nur als Bodeneffekt
/// existieren (ihre Mitte steht in keinem Feld, das wir lesen), Tuerme,
/// Sammelpunkte, Seile und Rueckstoss. Findet die Suche keinen sicheren Punkt,
/// meldet sie das - sie zeigt nie irgendwohin, nur damit sie etwas zeigt.
/// </summary>
public sealed class EscapeRouteService
{
    /// <summary>Sicherheitsabstand zur Kante. Wer auf der Linie steht, ist
    /// getroffen; dazu dreht sich ein Gegner waehrend des Casts weiter.</summary>
    private const float SafetyMarginMeters = 2.0f;

    /// <summary>Probestrahlen rundum (alle 15 Grad) und Schrittweite darauf. Grob
    /// genug, dass die Suche nichts kostet, und fein genug, dass zwischen zwei
    /// Strahlen keine Luecke passt, durch die man laufen wollte.</summary>
    private const int   Rays        = 24;
    private const float StepMeters  = 1.0f;
    private const float MaxDistance = 25f;

    /// <summary>So oft wird hoechstens neu gesucht. Dazwischen wird der gefundene
    /// Punkt nur noch auf Sicherheit geprueft (billig) und weiter angepeilt.</summary>
    private static readonly TimeSpan Recompute = TimeSpan.FromMilliseconds(250);

    /// <summary>Wie viele Kandidaten hoechstens ueber das Wegenetz geprueft
    /// werden. Jede Pruefung ist ein IPC-Aufruf an vnavmesh - billig, aber nicht
    /// umsonst, und der beste Punkt ist fast immer schon der erste.</summary>
    private const int MaxMeshChecks = 12;

    /// <summary>Obergrenze fuer den rechnerischen Durchgang. Nach so vielen
    /// Treffern ist der naechstgelegene laengst dabei; alles darueber waere nur
    /// noch Arbeit fuer Punkte, die ohnehin zu weit weg sind.</summary>
    private const int MaxCandidates = 60;

    private readonly NavmeshIpc _nav;
    private readonly IPluginLog _log;

    // Rechnerisch sichere Punkte dieses Durchgangs, nah zuerst. Feld statt
    // lokaler Liste: die Suche laeuft viermal je Sekunde, waehrend der Spieler um
    // sein Leben rennt - sie soll dabei nichts anlegen.
    private readonly List<Vector3> _candidates = new();

    private Vector3  _spot;
    private bool     _hasSpot;
    private DateTime _searchedAt  = DateTime.MinValue;
    private bool     _lastWasSafe = true;

    public EscapeRouteService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _nav = new NavmeshIpc(pluginInterface, log);
        _log = log;
    }

    /// <summary>Steht der Spieler gerade in einer Gefahrenflaeche?</summary>
    public bool InDanger { get; private set; }

    /// <summary>
    /// Die Suche ist gelaufen und hat NICHTS gefunden - im Unterschied zu "hat
    /// noch nicht gesucht". Der Aufrufer braucht den Unterschied: die Absage
    /// "kein sicherer Weg" darf erst raus, wenn sie auch stimmt, sonst kommt sie
    /// regelmaessig kurz vor der Richtung, die es dann doch gibt.
    /// </summary>
    public bool SearchExhausted { get; private set; }

    /// <summary>
    /// Der Punkt, auf den der Peil-Ton zeigen soll, oder null. Null heisst
    /// entweder "keine Gefahr" oder "kein sicherer Punkt gefunden" - was von
    /// beidem, unterscheidet der Aufrufer an <see cref="InDanger"/>.
    /// </summary>
    public Vector3? SafeSpot => _hasSpot ? _spot : null;

    /// <summary>
    /// Einmal je Frame aus <see cref="CombatService"/>, mit allen gerade aktiven
    /// Gefahrenflaechen. Leere Liste heisst Entwarnung.
    /// </summary>
    public void Update(Vector3 playerPos, IReadOnlyList<DangerZone> zones)
    {
        if (zones.Count == 0)
        {
            Clear();
            return;
        }

        InDanger = IsInAny(zones, playerPos, 0f);
        if (!InDanger)
        {
            // Draussen und sicher: kein Fluchtziel noetig. Der Peil-Ton faellt
            // damit von selbst an die Gehhilfe zurueck.
            if (!_lastWasSafe) _log.Info("[Flucht] Spieler ist aus der Flaeche heraus.");
            _lastWasSafe    = true;
            _hasSpot        = false;
            SearchExhausted = false;
            return;
        }
        _lastWasSafe = false;

        // Traegheit: solange das gemerkte Ziel sicher bleibt, wird nicht neu
        // gesucht. Sonst springt die Richtung mit jeder Drehung des Gegners.
        if (_hasSpot && !IsInAny(zones, _spot, SafetyMarginMeters)) return;

        var now = DateTime.UtcNow;
        if (now - _searchedAt < Recompute) return;
        _searchedAt = now;

        if (TrySearch(playerPos, zones, out var found))
        {
            _spot           = found;
            _hasSpot        = true;
            SearchExhausted = false;
        }
        else
        {
            // KEIN Ersatzziel. Irgendwohin zu zeigen waere schlimmer als zu
            // schweigen: der Spieler liefe los, bliebe trotzdem in der Flaeche -
            // und haette dabei auch noch die Zeit verbraucht.
            _hasSpot        = false;
            SearchExhausted = true;
            _log.Info("[Flucht] Kein sicherer Punkt in Reichweite gefunden.");
        }
    }

    /// <summary>Alles vergessen - es ist kein Cast mehr aktiv.</summary>
    public void Clear()
    {
        InDanger        = false;
        _hasSpot        = false;
        _lastWasSafe    = true;
        SearchExhausted = false;
    }

    private static bool IsInAny(IReadOnlyList<DangerZone> zones, Vector3 p, float margin)
    {
        for (var i = 0; i < zones.Count; i++)
            if (zones[i].ContainsWithMargin(p, margin)) return true;
        return false;
    }

    /// <summary>
    /// Faecher von Probepunkten, von nah nach fern. "Der naechste" ist hier das
    /// richtige Kriterium, weil die Zeit bis zum Einschlag die knappe Groesse
    /// ist, nicht die Bequemlichkeit des Weges.
    ///
    /// ZWEI DURCHGAENGE, und das mit Absicht: erst wird rein rechnerisch
    /// gesammelt, wer aus allen Flaechen heraus ist (kostet nichts), und danach
    /// werden diese Kandidaten der Reihe nach ueber das Wegenetz geprueft (kostet
    /// je einen IPC-Aufruf). In einem Durchgang waere das Budget fuer die
    /// Netzabfragen schon im ersten Meter aufgebraucht gewesen, und die Suche
    /// haette nie weiter aussen nachgesehen - der Spieler haette "kein sicherer
    /// Weg" gehoert, waehrend zwei Meter weiter einer lag.
    /// </summary>
    private bool TrySearch(Vector3 from, IReadOnlyList<DangerZone> zones, out Vector3 spot)
    {
        spot = default;

        _candidates.Clear();
        for (var step = 1; step * StepMeters <= MaxDistance; step++)
        {
            var radius = step * StepMeters;
            for (var ray = 0; ray < Rays; ray++)
            {
                var angle = ray * MathF.Tau / Rays;
                var cand  = new Vector3(
                    from.X + MathF.Sin(angle) * radius,
                    from.Y,
                    from.Z + MathF.Cos(angle) * radius);
                if (IsInAny(zones, cand, SafetyMarginMeters)) continue;
                _candidates.Add(cand);
                if (_candidates.Count >= MaxCandidates) break;
            }
            if (_candidates.Count >= MaxCandidates) break;
        }
        if (_candidates.Count == 0) return false;

        // Ohne Wegenetz wird der naechste rechnerische Punkt genommen. Das ist
        // schlechter als mit, aber immer noch besser als keine Richtung - und es
        // ist genau der Fall, in dem ein sehender Spieler auch nur nach Augenmass
        // ausweicht.
        if (!_nav.IsReady) { spot = _candidates[0]; return true; }

        var checks = Math.Min(_candidates.Count, MaxMeshChecks);
        for (var i = 0; i < checks; i++)
        {
            // Enger Suchkasten: der Punkt soll ungefaehr DA liegen, nicht
            // irgendwo in der Naehe. NearestPointReachable statt NearestPoint,
            // damit kein Punkt hinter einer Wand gewinnt.
            var onMesh = _nav.NearestPointReachable(_candidates[i], 3f, 5f);
            if (onMesh == null) continue;
            // Das Netz darf den Punkt verschieben - aber nicht zurueck in die
            // Flaeche hinein.
            if (IsInAny(zones, onMesh.Value, SafetyMarginMeters)) continue;
            spot = onMesh.Value;
            return true;
        }

        // Rechnerisch gaebe es Punkte, aber keiner davon liegt auf begehbarem
        // Boden. Dorthin zu zeigen waere eine Wegweisung gegen eine Wand,
        // waehrend der Cast ablaeuft - lieber die ehrliche Absage.
        return false;
    }
}
