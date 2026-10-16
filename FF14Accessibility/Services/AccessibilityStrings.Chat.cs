using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Die Sprachbausteine für die Chat-Puffer und für das Einstellungsmenü.
///
/// Bewusst eine eigene Datei und deshalb <c>partial</c>: die Hauptdatei ist groß und
/// ändert sich in fast jeder Version, und diese Erweiterung soll sich in einem Stück
/// wieder entfernen lassen. In <c>AccessibilityStrings.cs</c> steht dafür nur ein
/// einziges zusätzliches Wort - <c>partial</c>.
///
/// Gleiches Muster wie dort: eine Sprache pro Zeile, umgeschaltet über
/// <see cref="Loc.IsGerman"/>, damit "/acc lang" auch hier greift.
/// </summary>
public static partial class AccessibilityStrings
{
    // ── Menü-Rahmen (SpokenMenu) ──────────────────────────────────

    /// <summary>Kopfzeile beim Öffnen eines Menüs - der Titel und sonst nichts. Die
    /// Anzahl der Einträge steht nicht dabei: die unmittelbar folgende Zeile trägt
    /// ihre eigene Position ("1 von 15"), siehe <see cref="MenuEntry"/>.</summary>
    public static string MenuOpened(string title) => $"{title}.";

    /// <summary>Eine Menüzeile mit ihrer Position.</summary>
    public static string MenuEntry(string label, int index, int count) =>
        RowWithPosition(label, index, count);

    public static string MenuClosed => L("Menü geschlossen.", "Menu closed.", "Меню закрыто.");

    public static string MenuEmpty => L("Keine Einträge.", "No entries.", "Записей нет.");

    // ── Umsortieren: Zeile aufnehmen, schieben, ablegen ───────────
    //
    // Jede dieser vier Ansagen nennt die Position mit, weil sie das Einzige ist,
    // was den Zustand verrät: eine aufgenommene Zeile sieht man nicht, und ob
    // sie sich beim letzten Druck bewegt hat, hört man sonst nirgends.
    // "aufgenommen" und "abgelegt" sind die Klammern darum - solange sie nicht
    // gefallen ist, ist die Zeile noch in der Hand.

    /// <summary>Beim Aufnehmen einer Zeile: "Gegner aufgenommen, 4 von 21".</summary>
    public static string MenuGrabbed(string label, int index, int count) =>
        L($"{label} aufgenommen, {index} von {count}.", $"{label} picked up, {index} of {count}.", $"{label}: взято, {index} из {count}.");

    /// <summary>Nach jedem Schritt: "Gegner, jetzt 3 von 21".</summary>
    public static string MenuMovedTo(string label, int index, int count) =>
        L($"{label}, jetzt {index} von {count}.", $"{label}, now {index} of {count}.", $"{label}, теперь {index} из {count}.");

    /// <summary>Wenn es in diese Richtung nicht weitergeht. Sagt die Position
    /// erneut, damit ein Druck ins Leere nicht wie ein verschluckter klingt.</summary>
    public static string MenuMoveEnd(string label, int index, int count) =>
        L($"{label} bleibt auf {index} von {count}.", $"{label} stays at {index} of {count}.", $"{label}: дальше некуда, {index} из {count}.");

    /// <summary>Beim Ablegen: "Gegner abgelegt auf Platz 2".</summary>
    public static string MenuDropped(string label, int index) =>
        L($"{label} abgelegt auf Platz {index}.", $"{label} dropped at position {index}.", $"{label}: положено на место {index}.");

    /// <summary>
    /// Wird an jede Verschiebe-Ansage angehängt und sagt, WOZWISCHEN die
    /// aufgenommene Zeile jetzt steht: " zwischen Händler und Spieler."
    ///
    /// Sortiert wird nicht nach Platznummern, sondern nach Nachbarschaft - "die
    /// Gegner sollen gleich hinter Alles kommen". Die Platznummer allein
    /// beantwortet diese Frage nicht, und ohne die Nachbarn musste der Spieler
    /// die Zeile ablegen, die Liste ablaufen und sie wieder aufnehmen, nur um zu
    /// hören, wo er gelandet war (Wunsch vom 2026-08-26).
    ///
    /// AN DEN ENDEN wird nur genannt, was wirklich da ist. Dass Platz 1 der
    /// erste ist, steht schon in "1 von 21" - ein zusätzliches "ganz vorn" wäre
    /// dieselbe Auskunft zum zweiten Mal. Bleibt eine Liste mit nur einer Zeile,
    /// gibt es keine Nachbarn und der Satz entfällt ganz.
    ///
    /// Führendes Leerzeichen, weil er immer angehängt wird.
    /// </summary>
    /// <param name="before">Die Zeile darüber, oder leer am Anfang der Liste.</param>
    /// <param name="after">Die Zeile darunter, oder leer am Ende der Liste.</param>
    public static string MenuBetween(string before, string after)
    {
        var hasBefore = before.Length > 0;
        var hasAfter  = after.Length > 0;

        if (hasBefore && hasAfter)
            return L($" Zwischen {before} und {after}.", $" Between {before} and {after}.", $" Между {before} и {after}.");
        if (hasBefore)
            return L($" Hinter {before}.", $" After {before}.", $" После {before}.");
        if (hasAfter)
            return L($" Vor {after}.", $" Before {after}.", $" Перед {after}.");
        return string.Empty;
    }

    // ── Einstellungsmenü ──────────────────────────────────────────

    public static string OptionsTitle => L("Einstellungen", "Settings", "Настройки");
    public static string OptionsSounds => L("Töne", "Sounds", "Звуки");
    public static string OptionsAnnouncements => L("Ansagen", "Announcements", "Оповещения");

    /// <summary>Eine An/Aus-Zeile: "Kartenmarkierung, an".</summary>
    public static string OptionToggle(string name, bool on) =>
        L($"{name}, {(on ? "an" : "aus")}", $"{name}, {(on ? "on" : "off")}",
          $"{name}, {(on ? "вкл" : "выкл")}");

    /// <summary>Wird im Moment des Umschaltens gesprochen. <c>Rebuild</c> frischt nur
    /// die Beschriftung auf und liest die Zeile bewusst nicht erneut vor - ohne diese
    /// Ansage wäre das Umschalten also stumm.</summary>
    public static string OptionToggled(string name, bool on) =>
        L($"{name} {(on ? "an" : "aus")}.", $"{name} {(on ? "on" : "off")}.",
          $"{name} {(on ? "вкл" : "выкл")}.");

    /// <summary>Eine Lautstärke-Zeile: "Beacon, 35 Prozent" oder "Beacon, aus".</summary>
    public static string OptionVolume(string name, float volume) =>
        volume <= 0f
            ? (L($"{name}, aus", $"{name}, off", $"{name}, выкл"))
            : (L($"{name}, {(int)MathF.Round(volume * 100)} Prozent", $"{name}, {(int)MathF.Round(volume * 100)} percent", $"{name}, {(int)MathF.Round(volume * 100)} процентов"));

    /// <summary>Eine einzelne Stufe im Lautstärke-Untermenü.</summary>
    public static string VolumeStep(float volume) =>
        volume <= 0f
            ? (L("Aus", "Off", "Выкл"))
            : (L($"{(int)MathF.Round(volume * 100)} Prozent", $"{(int)MathF.Round(volume * 100)} percent", $"{(int)MathF.Round(volume * 100)} процентов"));

    public static string VolumeSet(string name, float volume) =>
        volume <= 0f
            ? (L($"{name} aus.", $"{name} off.", $"{name} выкл."))
            : (L($"{name} auf {(int)MathF.Round(volume * 100)} Prozent.", $"{name} at {(int)MathF.Round(volume * 100)} percent.", $"{name}: {(int)MathF.Round(volume * 100)} процентов."));

    // Namen der einzelnen Einstellungen. Jede Zeile hier hat ein Feld in
    // Configuration und einen Dienst dahinter, der es liest - eine Beschriftung ohne
    // Funktion dahinter ist genau der Weg, auf dem eine tote Einstellung eine
    // Überarbeitung überlebt.
    // "beim Laufen" steht bewusst in der Beschriftung: der Schalter hiess bis
    // v5.89 "Peil-Ton auf das Ziel", und genau so hat er sich auch verhalten -
    // er lief bei jedem anvisierten Ziel, im Kampf also ununterbrochen. Seit
    // 2026-08-23 laeuft er nur waehrend eines Laufs, und die Beschriftung muss
    // das sagen, sonst sucht der Spieler den Fehler beim Ton statt beim Lauf.
    /// <summary>Schalter für den Peil-Ton, der während eines Laufs die Richtung hält.</summary>
    public static string OptTargetBeacon => L("Peil-Ton beim Laufen", "Navigation beacon", "Навигационный маяк при беге");

    // Heisst weiterhin so, weil es die LAUTSTAERKE desselben Tons ist.
    public static string OptBeacon => L("Lautstärke Peil-Ton", "Navigation beacon volume", "Громкость навигационного маяка");

    /// <summary>Schalter für die HP- und MP-Töne (jede 10-Prozent-Stufe).</summary>
    public static string OptVitalCues => L("Töne für Leben und Mana", "Health and mana tones", "Звуки жизни и маны");

    /// <summary>Lautstärke der HP- und MP-Töne.</summary>
    public static string OptVitalCueVolume =>
        L("Lautstärke Leben und Mana", "Health and mana tone volume", "Громкость жизни и маны");
    public static string OptRouteCues => L("Wegpunkt- und Ankunftston", "Waypoint and arrival cues", "Звуки путевой точки и прибытия");

    /// <summary>Schalter für Anstoß- und Kantentöne beim freien Laufen.</summary>
    public static string OptMovementCues =>
        L("Anstoß- und Kantentöne", "Bump and ledge tones", "Звуки толчков и краёв");

    /// <summary>Lautstärke der Anstoß- und Kantentöne.</summary>
    public static string OptMovementCueVolume =>
        L("Lautstärke Anstoß und Kante", "Bump and ledge tone volume", "Громкость звуков толчков и краёв");

    // AoE-Warnung: die Lautstärke gab es als Konfigurationswert schon lange, aber
    // in keinem Menü - sie war damit nicht erreichbar. Der Klang kam 2026-08-21
    // auf Wunsch des Spielers dazu.
    public static string OptAoeWarnVolume => L("Lautstärke AoE-Warnung", "AoE warning volume", "Громкость предупреждения об AoE");

    public static string OptAoeWarnTone => L("Klang AoE-Warnung", "AoE warning sound", "Звук предупреждения об AoE");

    /// <summary>Name einer Warnton-Stimme, wie ihn die Auswahl vorliest.
    /// Beschreibend statt technisch: "300 Hertz mit Obertönen" sagt niemandem,
    /// wie etwas klingt.</summary>
    public static string AoeToneName(AoeWarnTone tone) => tone switch
    {
        AoeWarnTone.Bright => L("Hell (bisheriger Klang)", "Bright (previous sound)", "Яркий (прежний звук)"),
        AoeWarnTone.Soft   => L("Weich", "Soft", "Мягкий"),
        AoeWarnTone.Deep   => L("Tiefes Brummen", "Deep hum", "Низкий гул"),
        AoeWarnTone.Wave   => L("An- und abschwellend", "Swelling", "Нарастающий и затухающий"),
        _                  => L("Unbekannt", "Unknown", "Неизвестный"),
    };

    /// <summary>Die Zeile, die das Untermenü öffnet: "Klang AoE-Warnung, Weich".</summary>
    public static string OptionChoice(string name, string value) =>
        L($"{name}, {value}", $"{name}, {value}", $"{name}, {value}");

    /// <summary>Bestätigung nach der Wahl eines Klangs.</summary>
    public static string AoeToneSet(string value) =>
        L($"Warnton {value}.", $"Warning sound {value}.", $"Звук предупреждения: {value}.");

    // ── Warnstimme (zweiter Sprachkanal) ──────────────────────────────────────
    // Sie heißt im Menü nicht "SAPI": für den Spieler zählt, WAS sie tut - dass
    // die Kampfwarnungen an der Sprachausgabe vorbeigehen und dort nicht mehr
    // abgeschnitten werden können.
    public static string OptWarningVoice => L("Eigene Stimme für Kampfwarnungen", "Separate voice for combat warnings", "Отдельный голос для боевых предупреждений");

    public static string OptWarningVoiceVolume => L("Lautstärke Warnstimme", "Warning voice volume", "Громкость голоса предупреждений");

    public static string OptWarningVoiceRate => L("Tempo Warnstimme", "Warning voice speed", "Темп голоса предупреждений");

    public static string OptWarningVoiceName => L("Stimme für Kampfwarnungen", "Voice for combat warnings", "Голос для боевых предупреждений");

    /// <summary>Der Satz, den eine Stimme zur Probe spricht. Eine echte Warnung
    /// und kein "Test eins zwei": beurteilt werden soll, ob man SIE im Kampf
    /// versteht.</summary>
    public static string WarningVoiceSample => L("Kegel von vorne. Nach rechts ausweichen, sieben Meter.", "Cone from the front. Dodge right, seven metres.", "Конус спереди. Уходи вправо, семь метров.");

    /// <summary>Steht in der Stimmenauswahl, wenn keine eigene gewählt ist.</summary>
    public static string WarningVoiceAutomatic => L("Automatisch", "Automatic", "Автоматически");

    /// <summary>Bestätigung nach der Wahl einer Stimme - nur nötig, wenn die
    /// Probe stumm blieb.</summary>
    public static string WarningVoiceSet(string value) =>
        L($"Warnstimme {value}.", $"Warning voice {value}.", $"Голос предупреждений: {value}.");

    /// <summary>Steht statt der Auswahl, wenn das System gar keine Sprachausgabe
    /// anbietet. Eine leere Liste ohne Erklärung wäre von einem Fehler des
    /// Plugins nicht zu unterscheiden.</summary>
    public static string WarningVoiceUnavailable => L("Keine Sprachausgabe des Systems verfügbar. Die Kampfwarnungen kommen über den Screenreader.", "No system speech available. Combat warnings go through the screen reader.", "Системный синтез речи недоступен. Боевые предупреждения идут через скринридер.");

    /// <summary>
    /// Die wählbaren Tempostufen der Warnstimme. SAPI kennt -10 bis 10; die
    /// Enden sind bewusst NICHT dabei - bei -10 dauert eine Warnung länger als
    /// der Zauber, den sie ankündigt, und bei 10 ist der Satz auch für ein
    /// geübtes Ohr nicht mehr sicher zu verstehen. Sieben Stufen reichen, um
    /// die eigene Grenze zu finden, ohne sich durch zwanzig zu hören.
    /// </summary>
    public static readonly int[] VoiceRateSteps = { -4, -2, 0, 2, 4, 6, 8 };

    /// <summary>Name einer Tempostufe. Beschreibend statt technisch: "Tempo 6"
    /// sagt niemandem, wie schnell das ist.</summary>
    public static string VoiceRateName(int rate) => rate switch
    {
        <= -4 => L("Sehr langsam", "Very slow", "Очень медленно"),
        -3 or -2 => L("Langsam", "Slow", "Медленно"),
        -1 or 0 => L("Normal", "Normal", "Обычно"),
        1 or 2 => L("Etwas schneller", "Slightly faster", "Чуть быстрее"),
        3 or 4 => L("Schnell", "Fast", "Быстро"),
        5 or 6 => L("Sehr schnell", "Very fast", "Очень быстро"),
        7 or 8 => L("Am schnellsten", "Fastest", "Быстрее всего"),
        _ => L("Höchstes Tempo", "Maximum speed", "Максимальная скорость"),
    };

    /// <summary>
    /// [Chatstimme] Die Tempostufen der Chatstimme - bis zum Ende der SAPI-Skala.
    ///
    /// ANDERS ALS BEI DER WARNSTIMME ist 10 dabei (Spielerin 2026-09-14: Hedda auf
    /// "Schnell" ist zu langsam). Der Grund, 10 dort wegzulassen, war die
    /// Verständlichkeit im Ernstfall; im Chat entscheidet das geübte Ohr, und das
    /// liest mit Sku schon lange schnelle Stimmen. Unten fehlen die langsamen
    /// Stufen, weil sie im Chat niemand gesucht hat.
    /// </summary>
    public static readonly int[] ChatVoiceRateSteps = { 0, 2, 4, 6, 8, 10 };

    /// <summary>Bestätigung nach der Wahl einer Tempostufe - nur nötig, wenn die
    /// Probe stumm blieb.</summary>
    public static string VoiceRateSet(string value) =>
        L($"Tempo {value}.", $"Speed {value}.", $"Темп: {value}.");

    // ── [Chatstimme] ──────────────────────────────────────────────
    //
    // DIE WÖRTER SIND SKUS (locales/deDE.lua bzw. die englischen Schlüssel):
    // "Stumm", "Text", "TTS Stimme", "TTS Geschwindigkeit", "TTS Lautstärke" -
    // Spielerentscheidung 2026-09-14. EINE AUSNAHME: Skus "Blizzard TTS" heißt
    // hier nur "TTS" (Spielerwunsch 2026-09-14, "das solltest du umbenennen auf
    // einfach nur tts") - in FF14 spricht nicht Blizzard, sondern Windows.
    // Die Zeilenform "Name (Zustand)" ist die aus SkuChat/Options.lua,
    // BuildOutputModeNode.

    /// <summary>Skus "Inactive" / "Stumm".</summary>
    public static string ChatOutputMuted => L("Stumm", "Inactive", "Без звука");

    /// <summary>Skus "Text": über den Screenreader.</summary>
    public static string ChatOutputText => "Text";

    /// <summary>Skus "Blizzard TTS", hier nur "TTS": eine SAPI-Stimme neben dem Screenreader.</summary>
    public static string ChatOutputTts => "TTS";

    /// <summary>Zustand mit Stimme: "TTS: Hedda".</summary>
    public static string ChatOutputTtsWithVoice(string voice) => $"TTS: {voice}";

    /// <summary>Eine Kanalzeile mit ihrem Zustand: "Flüstern (Text)".</summary>
    public static string ChatOutputRow(string name, string state) => $"{name} ({state})";

    /// <summary>Quittung nach der Wahl.</summary>
    public static string ChatOutputSet(string name, string state) => $"{name}: {state}.";

    public static string OptChatVoiceRate => L("TTS Geschwindigkeit", "TTS speed", "Скорость TTS");
    public static string OptChatVoiceVolume => L("TTS Lautstärke", "TTS volume", "Громкость TTS");
    public static string OptChatVoiceName => L("TTS Stimme", "TTS voice", "Голос TTS");

    /// <summary>Statt einer leeren Stimmenliste - eine leere Auswahl ohne Grund
    /// wäre von einem Fehler des Plugins nicht zu unterscheiden.</summary>
    public static string ChatVoiceUnavailable => L("Keine Sprachausgabe des Systems verfügbar. Der Chat wird über den Screenreader vorgelesen.", "No system speech available. Chat is read through the screen reader.", "Системной речи нет. Чат читает экранный диктор.");

    public static string OptSkillReady => L("Fähigkeit bereit und Effekte", "Ability ready and effects", "Готовность умений и эффекты");
    public static string OptSkillReadyVolume => L("Fähigkeit bereit Lautstärke", "Ability ready volume", "Громкость готовности умений");
    public static string OptJobGauge => L("Job-Anzeige wieder verfügbar", "Job gauge back up", "Индикатор профессии снова доступен");
    public static string OptGaugeCueVolume => L("Job-Anzeige Ton Lautstärke", "Job gauge tone volume", "Громкость звука индикатора профессии");
    public static string OptGaugeCuePreview => L("Job-Anzeige Töne nachhören", "Preview job gauge tones", "Прослушать звуки индикатора профессии");
    public static string OptHeading => L("Himmelsrichtung", "Compass heading", "Стороны света");
    public static string OptTargetChanges => L("Zielwechsel", "Target changes", "Смена цели");
    public static string OptTargetHp => L("Ziel-Lebenspunkte", "Target health", "Жизнь цели");
    public static string OptEnemyMarkers => L("Gegnerfarben", "Enemy colours", "Цвета противников");
    public static string OptEnemyCast => L("Zauber-Ansagen", "Skill cast announcements", "Объявления способностей");

    /// <summary>Garuda-Event Warp: Off / Manual / Auto (duty Territory 834 only).</summary>
    public static string OptNocturneWarpMode =>
        L("Teleport beim Garuda-Ereignis", "Garuda event Warp", "Переход на событие Гаруды");

    /// <summary>Spoken value for <see cref="NocturneWarpMode"/>.</summary>
    public static string NocturneWarpModeName(NocturneWarpMode mode) => mode switch
    {
        NocturneWarpMode.Off => L("Aus", "Off", "Выкл"),
        NocturneWarpMode.Manual => L("Manuell mit Ton", "Manual with sound", "Вручную со звуком"),
        NocturneWarpMode.Auto => L("Automatisch", "Automatic", "Автоматически"),
        _ => mode.ToString(),
    };
    public static string OptFineHpDuringLeve => L("Feine Ziel-Lebenspunkte im Freibrief", "Fine target health during levequests", "Точная жизнь цели в лицензиях");
    public static string OptMapFlag => L("Kartenmarkierung", "Map flag", "Метка на карте");
    public static string OptErrorToasts => L("Fehlermeldungen", "Error messages", "Сообщения об ошибках");
    public static string OptInfoToasts => L("Hinweismeldungen", "Notice messages", "Информационные сообщения");

    // ── Namen der Puffer ──────────────────────────────────────────
    //
    // Ein Kanal-Puffer und ein Register-Puffer bekommen ihren Namen vom SPIEL - aus
    // der LogFilter-Zeile beziehungsweise aus dem, was der Spieler selbst als
    // Registernamen eingetippt hat. Übersetzt wird davon nichts. Nur die drei Puffer,
    // die keine Register sind, tragen einen Namen vom Plugin, und jeder sagt, was er
    // ist, nicht was ihn füllt.

    /// <summary>Der Puffer für Dialogfenster. Der eine Puffer, der kein Chat-Register
    /// ist und keines werden kann: der Chat bekommt die Zeile eines NPC erst, wenn der
    /// Spieler weitergeklickt hat, ein aus dem Chat gefüllter Dialogpuffer hinkte dem
    /// Bildschirm also immer einen Schritt hinterher. Gefüllt wird er statt dessen von
    /// den Talk- und _BattleTalk-Lesern.</summary>
    public static string BufferDialogue => L("Dialoge", "Dialogue", "Диалоги");

    /// <summary>Die eigenen Meldungen des Plugins - Toasts, Abmelde-Countdown,
    /// Fensteransagen. Die liefen nie über den Chatlog, also hält sie kein
    /// Register.</summary>
    public static string BufferSystem => L("Meldungen", "Notices", "Сообщения");

    /// <summary>Der einzelne Sammelpuffer, der nur benutzt wird, solange die
    /// Chatfilter des Spiels nicht lesbar sind. Siehe
    /// <see cref="ChatFiltersUnavailable"/>.</summary>
    public static string BufferChat => L("Chat", "Chat", "Чат");

    /// <summary>
    /// Ein ganzes Chat-Register in Ankunftsreihenfolge - das, was ein sehender Spieler
    /// sieht, wenn er auf dieses Register schaut.
    ///
    /// DAS IST DER EINZIGE SELBST VERGEBENE NAME IN DER PUFFERLISTE, und das ist
    /// Absicht. Jeder andere Puffer wird vom Spiel benannt. Für "das ganze Register"
    /// hat das Spiel kein Wort, weil es keine Pufferliste hat - es zeichnet das
    /// Register, und ein Auge überfliegt es. Der Addon-Sheet-Block der
    /// Chat-Einstellungen wurde daraufhin durchgesehen (Zeilen 1205-1290): dort stehen
    /// "Alle auswählen" und "Alle abwählen" für die Voreinstellungsknöpfe, aber nichts,
    /// was eine Ansicht benennt.
    ///
    /// Dass der Name vom Plugin kommt, ist der sichtbare Hinweis darauf, dass auch die
    /// GRUPPIERUNG vom Plugin kommt. Der INHALT nicht: eine Zeile liegt genau dann
    /// hier, wenn die Filterdaten des Spiels sagen, dass dieses Register sie zeigt.
    /// </summary>
    public static string BufferTabAll => L("Alles", "All", "Всё");

    /// <summary>
    /// Derselbe Puffer, aber mit dem Register davor: "Allgemein, alles".
    ///
    /// Beim Nachlesen reicht "Alles", weil das Register der Kontext ist, in dem
    /// man gerade steht. In der Sortierliste des Einstellungsmenüs stehen die
    /// Register aller Reiter untereinander - dort wären es mehrere Zeilen, die
    /// gleich heißen und Verschiedenes bedeuten.
    /// </summary>
    public static string BufferTabAllOf(string tabName) =>
        L($"{tabName}, alles", $"{tabName}, all", $"{tabName}, всё");

    /// <summary>
    /// Wird EINMAL gesagt, wenn der Filterzustand des Spiels nicht gelesen werden kann.
    /// Die Alternative wäre, dass die Pufferliste ohne Angabe eines Grundes falsch
    /// aussieht. Es landet weiterhin alles in einem Puffer und alles außer dem
    /// Kampflog wird weiterhin gesprochen - das Plugin fällt hörbar zurück, es wird
    /// nicht still.
    /// </summary>
    public static string ChatFiltersUnavailable =>
        L("Die Chat-Einstellungen des Spiels sind nicht lesbar. Der Chat läuft in einem Puffer.", "The game's chat settings cannot be read. Chat is going to one buffer.", "Настройки чата в игре не читаются. Чат идёт в один буфер.");

    // ── Register wechseln, und was im neuen Register liegt ────────

    /// <summary>
    /// Wird gesagt, nachdem das Plugin das Chat-Register des Spiels gewechselt hat:
    /// welches Register es jetzt ist, wie viele seiner Puffer etwas enthalten, und der
    /// erste davon mit seiner Anzahl - ein Tastendruck beantwortet also "wo bin ich"
    /// und "was liegt hier" zusammen.
    ///
    /// Gezählt wird, worauf die Blättertaste tatsächlich stehenbleibt, nicht, wie viele
    /// Schalter das Register eingeschaltet hat. Ein Register mit vierzig eingeschalteten
    /// Kanälen, von denen zwei gesprochen haben, ist für den Spieler ein Register mit
    /// zwei Puffern; "vierzig" würde eine Filterliste beschreiben und keinen Verlauf.
    /// </summary>
    public static string ChatTabEntered(string tab, int buffers, string first, int count) =>
        L($"{tab}, {buffers} Puffer. {first}, {count}.", $"{tab}, {buffers} buffers. {first}, {count}.", $"{tab}, буферов: {buffers}. {first}, {count}.");

    /// <summary>Wird gesagt, wenn die Registertaste den Chatlog des Spiels gar nicht
    /// erreicht. Nie Stille: der Spieler hätte sonst keine Möglichkeit, ein fehlendes
    /// Fenster von einer kaputten Taste zu unterscheiden.</summary>
    public static string ChatTabUnavailable =>
        L("Das Chatfenster ist nicht erreichbar.", "The chat window cannot be reached.", "Окно чата недоступно.");

    // ── Einstellungen: eine Sprachschaltung je Chat-Register ──────

    /// <summary>Benannt nach dem, was das Spiel hat, denn genau das sind die Zeilen
    /// darunter: eine je Chat-Register, unter dem Namen des Registers selbst.</summary>
    public static string OptionsChatTabs => L("Chat-Register", "Chat tabs", "Вкладки чата");

    /// <summary>
    /// Die oberste Zeile im Untermenü eines Registers: wird dieses Register vorgelesen.
    ///
    /// JEDE ZEILE IN DIESEM ABSCHNITT SAGT "VORLESEN", und zwar mit Absicht. Die
    /// Schaltung des SPIELS entscheidet, ob eine Zeile überhaupt existiert - aus, heißt
    /// nicht angezeigt, nicht archiviert, nicht gesprochen. Die Schaltung HIER
    /// entscheidet nur, ob eine ohnehin vorhandene Zeile laut vorgelesen wird; aus,
    /// heißt archiviert und blätterbar, aber still. Beide sitzen im Kopf des Spielers
    /// nebeneinander, also muss die Zeile des Plugins das eine benennen, was sie
    /// anfasst. Ein Wort wie "stummschalten" beschreibt einen Zustand, ohne zu sagen,
    /// was verstummt, und das ist genau die Zweideutigkeit, die hier zu vermeiden ist.
    /// </summary>
    public static string OptChatTabMaster =>
        L("Register vorlesen", "Read tab aloud", "Читать вкладку вслух");

    /// <summary>Die Gruppenzeile im Untermenü eines Kanals - die ganze Akteursgruppe
    /// auf einmal, über den Kästchen, in die das Spiel sie aufteilt. Gleiche
    /// Wortregel wie bei <see cref="OptChatTabMaster"/>.</summary>
    public static string OptChatChannelAll =>
        L("Ganze Gruppe vorlesen", "Read whole group aloud", "Читать всю группу вслух");

    /// <summary>
    /// Die eine Schaltung für Zeilen, für die die Filterliste des Spiels gar kein
    /// Kästchen hat - die Anmeldehinweise, die Phishing-Warnung, ein eingehendes
    /// Tell, ein GM.
    ///
    /// Benannt nach dem, was sie abdeckt, und nicht nach einem Kanal, denn ein Kanal
    /// ist es nicht: es ist alles, was das Spiel nicht filterbar gemacht hat. Sie steht
    /// unten in der Registerliste, weil sie zu keinem Register gehört.
    /// </summary>
    public static string OptChatUnfiltered =>
        L("Meldungen ohne Spielfilter vorlesen", "Read lines the game cannot filter", "Читать вслух строки вне фильтров игры");

    /// <summary>Die Zeile, wenn die Register nicht lesbar sind - eine Schaltung für den
    /// einen Sammelpuffer. Ein Abschnitt, der seinen Namen nennt und dann nichts
    /// anbietet, liest sich wie ein Fehler; also sagt er statt dessen, in welchem
    /// Zustand er ist.</summary>
    public static string OptChatFallback =>
        L("Chat vorlesen (Register nicht lesbar)", "Read chat aloud (tabs unreadable)", "Читать чат вслух (вкладки не читаются)");

    // ── Einstellungen: eigene Reihenfolge der Kategorien ──────────
    //
    // ZWEI GETRENNTE UNTERMENÜS für Reihenfolge und An/Aus, obwohl beide
    // dieselbe Liste zeigen. Der Grund ist die Bestätigungstaste: sie kann pro
    // Ebene nur eines von beidem bedeuten, und eine zweite Taste dafür zu
    // erfinden hieße, dem Spieler eine Sondertaste beizubringen, die es sonst
    // in keinem Menü der Mod gibt. So bleibt Numpad0 überall das, was es überall
    // ist - in der einen Ebene nimmt es auf, in der anderen schaltet es um.

    public static string OptionsOrder => L("Reihenfolge", "Order", "Порядок");

    // ── Einstellungen: Wegdateien für die Kategorie "Dungeon" ──────────
    //
    // Der Abschnitt existiert, weil das Fehlen dieser Dateien in v5.94 UNHÖRBAR
    // war: keine Datei, keine Kategorie, keine Meldung. Hier ist es hörbar - der
    // Abschnitt nennt seinen Bestand schon in der Zeile, die zu ihm führt.

    /// <summary>Die Zeile im Hauptmenü, mit dem Bestand als Zahl. Ohne die Zahl
    /// müsste man den Abschnitt öffnen, um die einzige Frage zu beantworten, die
    /// er beantwortet.</summary>
    public static string OptionsDungeonPaths(int files) =>
        L($"Dungeon-Wege, {files} geladen", $"Dungeon routes, {files} loaded", $"Маршруты подземелий, загружено: {files}");

    public static string DungeonPathsTitle =>
        L("Dungeon-Wege", "Dungeon routes", "Маршруты подземелий");

    /// <summary>Die Zeile, die das Laden auslöst.</summary>
    public static string DungeonPathsFetchNow =>
        L("Jetzt herunterladen", "Download now", "Скачать сейчас");

    public static string DungeonPathsAutoName =>
        L("Automatisch herunterladen", "Download automatically", "Скачивать автоматически");

    /// <summary>Quittung beim Start des Ladens. Sie muss sein: der Download
    /// dauert Sekunden, und ein Menü, das auf einen Tastendruck schweigt, ist von
    /// einem, das den Druck verschluckt hat, nicht zu unterscheiden.</summary>
    public static string DungeonPathsFetching =>
        L("Dungeon-Wege werden geladen.", "Downloading dungeon routes.", "Маршруты подземелий загружаются.");

    public static string DungeonPathsFetched(int files) =>
        L($"{files} Dungeon-Wege geladen.", $"{files} dungeon routes loaded.", $"Загружено маршрутов подземелий: {files}.");

    /// <summary>Fehlschlag. Nennt den Ordner NICHT - der Pfad ist als Ansage
    /// unbrauchbar lang; er steht im Log und in der Anleitung.</summary>
    public static string DungeonPathsFailed =>
        L("Dungeon-Wege konnten nicht geladen werden. Sieh ins Log.", "Dungeon routes could not be downloaded. Check the log.", "Маршруты подземелий не загрузились. Смотри лог.");

    /// <summary>Wann zuletzt geladen wurde, oder dass es das noch nie gab.</summary>
    public static string DungeonPathsLast(string date) =>
        string.IsNullOrEmpty(date)
            ? (L("Zuletzt geladen: nie", "Last downloaded: never", "Последняя загрузка: никогда"))
            : (L($"Zuletzt geladen: {date}", $"Last downloaded: {date}", $"Последняя загрузка: {date}"));

    /// <summary>Der Abschnitt für die Objekt-Browser-Kategorien. "Objekte
    /// durchblättern" und nicht "Kategorien", weil der Spieler die Liste über die
    /// Bild-Tasten kennt und nicht über ihren internen Namen.</summary>
    public static string OptionsOrderObjects =>
        L("Reihenfolge beim Objekte-Durchblättern", "Order when browsing objects", "Порядок перелистывания объектов");

    /// <summary>Derselbe Abschnitt für die Nachlese-Kategorien.</summary>
    public static string OptionsOrderChat =>
        L("Reihenfolge der Nachlese-Kategorien", "Order of chat history categories", "Порядок категорий истории чата");

    public static string OptionsShowObjects =>
        L("Objekt-Kategorien ein- und ausschalten", "Switch object categories on and off", "Включать и выключать категории объектов");

    public static string OptionsShowChat =>
        L("Nachlese-Kategorien ein- und ausschalten", "Switch chat history categories on and off", "Включать и выключать категории истории чата");

    /// <summary>
    /// Der Titel der Sortier-Ebene. Sagt den Kategoriensatz mit, weil es zwei
    /// davon gibt und der Spieler sonst nicht wüsste, welchen er gerade
    /// umsortiert: in einem Tiefen Gewölbe gilt ein eigener, kürzerer Satz.
    /// </summary>
    public static string OrderTitle(string set) =>
        L($"Reihenfolge: {set}", $"Order: {set}", $"Порядок: {set}");

    public static string OrderSetWorld => L("Welt", "World", "Мир");
    public static string OrderSetDeepDungeon => L("Tiefes Gewölbe", "Deep dungeon", "Глубокое подземелье");
    public static string OrderSetChat => L("Nachlese", "Chat history", "История чата");

    /// <summary>
    /// Eine Zeile in der Sortier-Ebene. Sagt "aus" mit, wenn die Kategorie
    /// abgeschaltet ist - man soll beim Sortieren sehen, was man da einsortiert,
    /// ohne zwischen zwei Untermenüs hin und her zu wechseln. Eingeschaltete
    /// Zeilen sagen NICHTS dazu: das ist der Normalfall, und ein "an" hinter
    /// zwanzig von einundzwanzig Zeilen ist nur Lärm.
    /// </summary>
    public static string OrderRow(string name, bool visible) =>
        visible ? name : (L($"{name}, aus", $"{name}, off", $"{name}, выкл"));

    /// <summary>
    /// Die Ansage beim Öffnen einer Sortier-Ebene: sie muss die Bedienung
    /// erklären, weil sie die einzige Ebene der Mod ist, in der die
    /// Bestätigungstaste etwas anderes tut als sonst.
    /// </summary>
    public static string OrderHint =>
        L("Bestätigen nimmt eine Zeile auf, hoch und runter verschiebt sie, Bestätigen legt sie wieder ab.", "Confirm picks a row up, up and down move it, confirm puts it down again.", "Подтверждение поднимает строку, вверх и вниз двигают её, подтверждение кладёт её обратно.");

    /// <summary>
    /// Wenn der Spieler die letzte eingeschaltete Kategorie abschalten will.
    ///
    /// Verweigert statt zugelassen: ein Browser ohne jede Kategorie antwortet auf
    /// die Bild-Tasten mit gar nichts, und "gar nichts" ist für einen blinden
    /// Spieler von einer kaputten Mod nicht zu unterscheiden. Die Ansage sagt
    /// deshalb den Grund und nicht nur, dass es nicht ging.
    /// </summary>
    public static string OrderLastOneStays(string name) =>
        L($"{name} bleibt an - es ist die letzte eingeschaltete Kategorie.", $"{name} stays on - it is the last category left switched on.", $"{name} нельзя выключить: это последняя включённая категория.");

    // ── Einstellungen: die Kanäle des GEWOHNTEN Chatsystems ───────

    /// <summary>Der Abschnitt, der im gewohnten Chatsystem an der Stelle steht, an
    /// der im neuen <see cref="OptionsChatTabs"/> steht. "Kanäle" und nicht
    /// "Register", weil das alte System keine Register kennt: seine Einteilung ist
    /// die feste Kategorienliste, die der Spieler auch beim Nachlesen hört.</summary>
    public static string OptionsChatChannels => L("Chat-Kanäle", "Chat channels", "Каналы чата");

    /// <summary>Die Sammel-Rückmeldungen beim Abbauen (XivChatType.Gathering). Sie
    /// haben einen eigenen Schalter, landen in der Nachlese aber unter "System" -
    /// deshalb ist dies der einzige Kanalname dieses Abschnitts, der nicht aus
    /// <see cref="AccessibilityStrings.LegacyChatCategoryName"/> kommen kann.</summary>
    public static string OptChatGathering => L("Sammeln", "Gathering", "Сбор");

    /// <summary>
    /// Hängt sich an die Bestätigung, wenn ein Kanal ABGESCHALTET wird ("Gruppe aus.
    /// Steht weiter zum Nachlesen bereit.").
    ///
    /// Nur beim Abschalten, und nur dort: das ist der Moment, in dem sich die Frage
    /// stellt, ob die Nachrichten jetzt weg sind. Beim Einschalten ist der Satz
    /// überflüssig, und in der Zeilenbeschriftung stünde er bei jedem Durchblättern
    /// im Weg.
    /// </summary>
    public static string ChatChannelStillArchived =>
        L("Steht weiter zum Nachlesen bereit.", "Still available in the history.", "Остаётся в истории.");
}
