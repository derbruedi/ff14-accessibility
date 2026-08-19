using System;
using Dalamud.Plugin.Services;
using NAudio.Wave;

namespace FF14Accessibility.Services;

/// <summary>
/// Welche Art von Ziel der Peil-Ton gerade fuehrt. Jede Art hat ihre eigene
/// Stimme (Grundton und Schlagmuster), damit man am KLANG hoert, worauf man
/// gerade zielt, ohne es sich vorher ansagen zu lassen.
/// </summary>
public enum BeaconKind
{
    /// <summary>Gegner - tief und zweischlaegig, der einzige Ton, der wie eine Warnung klingt.</summary>
    Enemy,
    /// <summary>NPC, Haendler, Mitspieler, Verbuendeter.</summary>
    Npc,
    /// <summary>Objekt, Truhe, Einrichtung - und der Vorgabeton, wenn nichts Genaueres bekannt ist.</summary>
    Object,
    /// <summary>Sammelpunkt (Erz, Holz) oder Angelplatz.</summary>
    Gathering,
    /// <summary>Zonenuebergang, Ausgang, Aufzug - alles, was einen woanders hinbringt.</summary>
    Transition,
    /// <summary>Aetheryt oder Aethernet-Splitter.</summary>
    Aetheryte,
    /// <summary>Quest-Ziel, Freibrief-Ziel, Jagdgebiet - ein Auftrag, kein Ding.</summary>
    Quest,
    /// <summary>Eingang in einen Dungeon, eine Pruefung oder einen Raid.</summary>
    DutyEntrance,
}

/// <summary>
/// Der Peil-Ton: er sagt, WOHIN man sich drehen muss, und er verstummt, sobald
/// man richtig steht.
///
/// Wunsch des Users (2026-08-19): *"es waere gut wenn wir verschiedene ziele
/// durch toene hoerbar machen sobald sie getrackt sind objekte gegner uebergaenge
/// usw aber fuer jedes einen unterschiedlichen und nicht so nervent er soll auch
/// lautstaerke technisch und richtungsbezogen kommen um so weiter man weg ist um
/// so leiser und die richtung in die man sich drehen muss und wenn man 100%
/// richtig steht soller ausgehen es geht mir dabei auch um aufzuege bzw
/// plattformen ob man richtig steht"*.
///
/// DAS SIGNAL, und warum es so herum gebaut ist:
/// <list type="bullet">
/// <item><b>Stille heisst richtig.</b> Innerhalb von <see cref="AlignedDeg"/>
///   Grad schweigt der Ton. Das ist die Umkehrung des frueheren Verhaltens (frueher
///   war "geradeaus" ein hoher Ton) und genau das, was eine Plattform oder einen
///   Aufzug benutzbar macht: drehen, bis es still ist.</item>
/// <item><b>Stille wird quittiert.</b> Beim Uebergang von falsch auf richtig
///   kommt EIN kurzer Einrast-Ton. Fuer einen blinden Spieler ist Stille sonst
///   nicht von "kaputt" zu unterscheiden - der Einrast-Ton ist der Beweis, dass
///   der Ton absichtlich schweigt.</item>
/// <item><b>Hysterese</b> (<see cref="ReleaseDeg"/>): einmal eingerastet, meldet
///   sich der Ton erst wieder ab <see cref="ReleaseDeg"/> Grad. Ohne das flattert
///   er am Rand der Stille-Zone im Takt der Maus.</item>
/// <item><b>Je genauer, desto ruhiger.</b> Die Schlagfolge geht von 0,4 s (voellig
///   daneben) auf 0,95 s (kurz vor dem Einrasten) zurueck. Man hoert die Naeherung
///   also, bevor die Stille kommt, und der Ton wird zum Ziel hin leiser statt
///   hektischer - das ist der Teil, der ihn ertraeglich macht.</item>
/// <item><b>Lauter je naeher</b> (Wunsch: "um so weiter man weg ist um so
///   leiser"): volle Lautstaerke bis 5 m, dann linear herunter bis auf 15% ab
///   150 m.</item>
/// <item><b>Seite ueber Stereo</b>, Tiefe ueber die Tonhoehe: hinten klingt
///   dunkler, weil ein Stereobild bei genau 180 Grad wieder mittig ist und links
///   von rechts allein nicht zu unterscheiden waere.</item>
/// </list>
///
/// Die Zielart faerbt nur die Stimme (Grundton, ein oder zwei Schlaege) - die
/// Steuerlogik ist fuer jede Art dieselbe.
/// </summary>
public sealed class BeaconService : IDisposable
{
    /// <summary>Bis zu diesem Winkel gilt die Ausrichtung als richtig und der Ton schweigt.</summary>
    private const double AlignedDeg = 6.0;

    /// <summary>Ab diesem Winkel meldet sich der Ton wieder (Hysterese gegen Flattern).</summary>
    private const double ReleaseDeg = 10.0;

    private readonly Configuration _config;
    private readonly TolkService _tolk;
    private readonly CueService _cue;
    private readonly IPluginLog _log;

    private WaveOutEvent? _output;
    private BeaconSampleProvider? _provider;

    /// <summary>Ob die Ausrichtung im letzten Frame schon stimmte - fuer den Einrast-Ton.</summary>
    private bool _aligned;

    /// <summary>Ob im letzten Frame ueberhaupt ein Ziel gefuehrt wurde.</summary>
    private bool _hadTarget;

    /// <summary>
    /// Kennung des Ziels, das im letzten Frame gefuehrt wurde. Aendert sie sich,
    /// faengt der Ton von vorne an - siehe <see cref="Update"/>.
    /// </summary>
    private ulong _lastTargetKey;

    public BeaconService(Configuration config, TolkService tolk, CueService cue, IPluginLog log)
    {
        _config = config;
        _tolk = tolk;
        _cue = cue;
        _log = log;
    }

    /// <summary>Ob die Tonausgabe gerade laeuft (das Geraet also offen ist).</summary>
    public bool IsRunning => _output != null;

    /// <summary>Startet die Tonausgabe. Mehrfacher Aufruf ist folgenlos.</summary>
    public void Start()
    {
        if (_output != null) return;
        // Der Schalter wird HIER geprueft und nicht nur an der Aufrufstelle: sonst
        // haette die Gehhilfe den abgeschalteten Ton beim naechsten Start wieder
        // geholt, und "aus" waere nur solange aus, bis man losgeht.
        if (!_config.TargetBeaconEnabled) return;

        // try-catch: externe Audio-API - das Ausgabegeraet kann fehlen, abgeschaltet
        // oder exklusiv belegt sein. Die Fuehrung muss das ueberleben und
        // weitersprechen (die Sprachausgabe haengt nicht am Ton).
        try
        {
            _provider = new BeaconSampleProvider { Volume = _config.BeaconVolume };
            _output = new WaveOutEvent { DesiredLatency = 100 };
            _output.Init(_provider);
            _output.Play();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Beacon: Audio-Ausgabe konnte nicht starten");
            _tolk.SpeakInterrupt(AccessibilityStrings.BeaconUnavailable);
            Stop();
        }
    }

    /// <summary>Beendet die Tonausgabe. Mehrfacher Aufruf ist folgenlos.</summary>
    public void Stop()
    {
        try
        {
            _output?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Nav] Beacon: Fehler beim Stoppen der Audio-Ausgabe");
        }

        _output = null;
        _provider = null;
        _aligned = false;
        _hadTarget = false;
        _lastTargetKey = 0;
    }

    /// <summary>
    /// Kein Ziel mehr: der Ton schweigt, das Geraet bleibt aber offen. Ein Ziel
    /// wird beim Durchblaettern im Sekundentakt gewechselt, und das Audiogeraet
    /// dabei jedes Mal zu oeffnen und zu schliessen ist teuer und klickt.
    /// </summary>
    public void Idle()
    {
        if (_provider != null) _provider.Silent = true;
        _aligned = false;
        _hadTarget = false;
        _lastTargetKey = 0;
    }

    /// <summary>
    /// Fuettert den Ton mit dem aktuellen Ziel - jeden Frame aufzurufen.
    /// </summary>
    /// <param name="relAngleDegrees">
    /// Winkel zum Ziel, 0 = genau voraus, positiv = rechts (siehe
    /// <see cref="NavigationService.RelativeAngle"/>).
    /// </param>
    /// <param name="distance">
    /// Entfernung zum ZIEL in Metern - sie steuert nur die Lautstaerke. Bei der
    /// Gehhilfe ist das die Reststrecke, nicht der Abstand zum naechsten
    /// Wegpunkt: Wegpunkte sind immer nah, der Ton waere sonst dauernd laut.
    /// </param>
    /// <param name="kind">Zielart - waehlt die Stimme.</param>
    /// <param name="arrived">
    /// Der Spieler steht am Ziel (auf der Plattform, an der Tuer). Dann schweigt
    /// der Ton unabhaengig von der Blickrichtung, denn es gibt nichts mehr
    /// auszurichten.
    /// </param>
    /// <param name="targetKey">
    /// Wer gerade gefuehrt wird - die Objekt-Id des anvisierten Ziels oder die
    /// laufende Nummer des Gehhilfe-Ziels. Aendert sie sich, HOERT DER TON FUER
    /// DAS ALTE ZIEL AUF und faengt fuer das neue von vorne an (Vorgabe des Users
    /// 2026-08-19: *"wenn was anderes getrackt wird bei den objekten und generell
    /// soll der ton fuer das vorher getrackte aufhoeren"*).
    ///
    /// Ohne das schleppt der Ton den Zustand des alten Ziels mit: er laeuft
    /// mitten im Takt weiter, und schlimmer - stand man auf das alte Ziel
    /// eingerastet, bliebe er fuer das neue stumm, solange dieses zufaellig in
    /// der Hysterese-Zone liegt. Der Spieler haette ein neues Ziel und hoerte
    /// die Bestaetigung des alten.
    /// </param>
    public void Update(double relAngleDegrees, float distance, BeaconKind kind = BeaconKind.Object,
                       bool arrived = false, ulong targetKey = 0)
    {
        var provider = _provider;
        if (provider == null) return;

        provider.Volume = _config.BeaconVolume;

        if (targetKey != _lastTargetKey)
        {
            _lastTargetKey = targetKey;
            // Sauberer Schnitt: kein nachwirkendes Einrasten, kein halber Takt
            // aus dem alten Ton. Der Quittungston bleibt beim Wechsel aus - das
            // Anvisieren quittiert das SPIEL bereits mit seinem eigenen Ton, und
            // die Zielansage sagt dazu, was es ist.
            _aligned = false;
            _hadTarget = false;
            provider.Restart = true;
        }

        var voice = VoiceFor(kind);
        var absAngle = Math.Abs(relAngleDegrees);

        // Einrasten mit Hysterese: hinein bei AlignedDeg, hinaus erst bei
        // ReleaseDeg. Ankunft rastet immer ein.
        var alignedNow = arrived || (_aligned ? absAngle <= ReleaseDeg : absAngle <= AlignedDeg);

        if (alignedNow)
        {
            provider.Silent = true;
            // Der eine Ton, der die Stille erklaert. Nur beim UEBERGANG, und nur
            // wenn vorher wirklich ein Ziel gefuehrt wurde - sonst quittiert der
            // erste Frame einer neuen Auswahl eine Ausrichtung, die der Spieler
            // gar nicht vorgenommen hat.
            if (!_aligned && _hadTarget) _cue.PlayAlignedTone();
            _aligned = true;
            _hadTarget = true;
            return;
        }

        _aligned = false;
        _hadTarget = true;
        provider.Silent = false;

        // Grundton der Art, zur Tiefe hin abgesenkt: bei 180 Grad rund eine halbe
        // Oktave tiefer. Die Absenkung ist bewusst klein - sie muss "hinter mir"
        // zeigen, ohne die Stimme unkenntlich zu machen, an der man die Zielart
        // erkennt.
        provider.Frequency = (float)(voice.Frequency * Math.Pow(2.0, -absAngle / 360.0));

        // Seite: sin(0)=mittig, sin(+-90)=ganz rechts/links, sin(+-180)=wieder
        // mittig (hinten steckt in der Tonhoehe, nicht im Stereobild).
        provider.Pan = (float)Math.Sin(relAngleDegrees * Math.PI / 180.0);

        // Naeher = lauter. Voll bis 5 m, linear herunter auf 15% ab 150 m.
        provider.DistanceFactor = Math.Clamp(1f - (distance - 5f) / 145f * 0.85f, 0.15f, 1f);

        // Je genauer gezielt, desto groesser der Abstand zwischen den Schlaegen:
        // 0,4 s bei voelliger Fehlausrichtung, 0,95 s kurz vor dem Einrasten.
        var closeness = (float)Math.Clamp(1.0 - (absAngle - AlignedDeg) / (180.0 - AlignedDeg), 0.0, 1.0);
        provider.PeriodSeconds = 0.4f + 0.55f * closeness;
        provider.PulseCount = voice.Pulses;
    }

    /// <summary>
    /// Stimme je Zielart. Die Grundtoene liegen auf Tonleiterstufen und weit
    /// genug auseinander, um sie auch ohne Gehoerbildung zu trennen; zwei
    /// Schlaege bekommen die Arten, bei denen etwas zu TUN ist (Gegner, Auftrag,
    /// Uebergang, Inhaltstuer), einen Schlag die, die einfach dastehen.
    /// </summary>
    private static (float Frequency, int Pulses) VoiceFor(BeaconKind kind) => kind switch
    {
        BeaconKind.Enemy        => (330f, 2),   // E4, tief und doppelt: das Einzige, was wie eine Warnung klingt
        BeaconKind.DutyEntrance => (392f, 2),   // G4
        BeaconKind.Transition   => (440f, 2),   // A4
        BeaconKind.Npc          => (494f, 1),   // H4
        BeaconKind.Quest        => (523f, 2),   // C5
        BeaconKind.Object       => (587f, 1),   // D5 - der bisherige Gehhilfe-Ton
        BeaconKind.Gathering    => (659f, 1),   // E5
        BeaconKind.Aetheryte    => (784f, 1),   // G5, hell wie der Kristall
        _                       => (587f, 1),
    };

    public void Dispose() => Stop();
}

/// <summary>
/// Erzeugt das Peil-Signal: eine weiche, angeschlagene Glocke
/// (<see cref="ToneSynth"/>) mit lebend einstellbarer Tonhoehe, Stereolage,
/// Lautstaerke, Schlagfolge und ein oder zwei Schlaegen je Takt. Der Anschlag ist
/// kurz und klingt exponentiell aus, sodass zwischen den Schlaegen Ruhe bleibt -
/// deutlich ohrenfreundlicher als ein flacher Dauerpiepser.
///
/// <see cref="Read"/> laeuft auf dem NAudio-Wiedergabefaden; die volatilen Felder
/// werden vom Framework-Faden geschrieben.
/// </summary>
internal sealed class BeaconSampleProvider : ISampleProvider
{
    private const int Rate = 44100;
    private const int AttackSamples = Rate * 4 / 1000;        // 4 ms, damit der Einsatz nicht knackt
    private const float DecayTauSamples = Rate * 110 / 1000f; // ~110 ms Ausklang
    private const int SecondPulseOffset = Rate * 150 / 1000;  // zweiter Schlag 150 ms nach dem ersten

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    public volatile float Frequency = 587f;
    public volatile float Pan;                 // -1 = links, +1 = rechts
    public volatile float Volume = 0.35f;
    public volatile float DistanceFactor = 1f; // 1 = nah/laut, 0.15 = fern/leise
    public volatile float PeriodSeconds = 0.5f;
    public volatile int PulseCount = 1;

    /// <summary>Ausrichtung stimmt (oder kein Ziel): es wird Stille ausgegeben.</summary>
    public volatile bool Silent;

    /// <summary>
    /// Neues Ziel: den laufenden Takt abbrechen und beim naechsten Puffer mit
    /// einem vollen Anschlag neu beginnen. Wird vom Wiedergabefaden selbst
    /// zurueckgesetzt, sobald er den Neustart ausgefuehrt hat.
    /// </summary>
    public volatile bool Restart;

    private double _phase;
    private int _posInPeriod;

    public int Read(float[] buffer, int offset, int count)
    {
        var frames = count / 2;

        if (Silent)
        {
            Array.Clear(buffer, offset, count);
            // Auf den Taktanfang zuruecksetzen, damit der naechste Ton nach der
            // Stille mit einem vollen Anschlag beginnt und nicht mitten im
            // Ausklang.
            _posInPeriod = 0;
            _phase = 0;
            return frames * 2;
        }

        if (Restart)
        {
            Restart = false;
            _posInPeriod = 0;
            _phase = 0;
        }

        var period = Math.Max(1, (int)(PeriodSeconds * Rate));
        var pulses = Math.Clamp(PulseCount, 1, 2);

        for (var i = 0; i < frames; i++)
        {
            if (_posInPeriod >= period) _posInPeriod = 0;

            // Position innerhalb des naechstgelegenen Schlags. Der zweite Schlag
            // ist derselbe Anschlag, nur spaeter im Takt - so bleibt die Stimme
            // gleich und nur das Muster unterscheidet sich.
            var posInPulse = _posInPeriod;
            if (pulses == 2 && _posInPeriod >= SecondPulseOffset)
                posInPulse = _posInPeriod - SecondPulseOffset;

            if (posInPulse == 0) _phase = 0;

            var env = ToneSynth.PluckEnvelope(posInPulse, AttackSamples, DecayTauSamples);
            var sample = 0f;
            if (env > 0.0008f)
            {
                _phase += 2.0 * Math.PI * Frequency / Rate;
                if (_phase > 2.0 * Math.PI) _phase -= 2.0 * Math.PI;
                // Die Obertoene folgen der Huellkurve, so wie eine echte Glocke
                // beim Ausklingen zuerst ihren Glanz verliert.
                sample = ToneSynth.Timbre(_phase, env) * env * Volume * DistanceFactor;
            }

            var panAngle = (Pan + 1f) * MathF.PI / 4f;
            buffer[offset + 2 * i]     = sample * MathF.Cos(panAngle);
            buffer[offset + 2 * i + 1] = sample * MathF.Sin(panAngle);

            _posInPeriod++;
        }

        return frames * 2;
    }
}
