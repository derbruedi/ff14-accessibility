using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Die Schluessel, unter denen <see cref="Configuration.ChatVoice"/> die Stimme
/// eines Chat-Kanals fuehrt. Ein Ort fuer beide Seiten - Menue und Leser -, damit
/// sie nicht auseinanderlaufen koennen.
///
/// TEXT, NICHT ZAHL, weil die beiden Chatsysteme verschiedene Dinge meinen: das
/// gewohnte seine festen Kategorien, das neue die Kanaele des Spiels (deren
/// Schluessel negativ sein koennen und damit mit den Sonderindizes aus
/// <see cref="ChatTabSpeech"/> zusammenstossen wuerden).
/// </summary>
public static class ChatVoiceKeys
{
    /// <summary>Ein Kanal des GEWOHNTEN Chatsystems - eine Zeile aus
    /// OptionsMenu.BuildChatChannels.</summary>
    public static string Legacy(string row) => "alt:" + row;

    /// <summary>Die eigene Zeile "Sammeln" des gewohnten Systems: eigener
    /// Schalter, archiviert unter "System".</summary>
    public const string LegacyGathering = "alt:Gathering";

    /// <summary>Ein Kanal des Spiels im NEUEN Chatsystem.</summary>
    public static string Channel(int channelKey) => "kanal:" + channelKey;

    /// <summary>Zeilen ohne Spielfilter (siehe ChatTabSpeech.UnfilteredIndex).</summary>
    public const string Unfiltered = "kanal:ohnefilter";

    /// <summary>Der Rueckfallpuffer (siehe ChatTabSpeech.FallbackIndex).</summary>
    public const string Fallback = "kanal:rueckfall";

    /// <summary>
    /// Die Zeile des gewohnten Systems, die ueber einen Chat-Typ entscheidet -
    /// woertlich die Zuordnung aus LegacyChatReaderService.ShouldSpeak. null fuer
    /// die Typen OHNE Schalter (Fehlermeldungen, /echo): eine Stimme, die man im
    /// Menue nicht einstellen kann, gibt es fuer sie auch nicht.
    /// </summary>
    public static string? ForLegacyKind(XivChatType type) => type switch
    {
        XivChatType.Say                      => Legacy(nameof(LegacyChatHistoryService.Category.Say)),
        XivChatType.Shout or XivChatType.Yell => Legacy(nameof(LegacyChatHistoryService.Category.Shout)),
        XivChatType.Party or XivChatType.CrossParty => Legacy(nameof(LegacyChatHistoryService.Category.Party)),
        XivChatType.Alliance                 => Legacy(nameof(LegacyChatHistoryService.Category.Alliance)),
        XivChatType.TellIncoming or XivChatType.TellOutgoing => Legacy(nameof(LegacyChatHistoryService.Category.Tell)),
        XivChatType.FreeCompany              => Legacy(nameof(LegacyChatHistoryService.Category.FreeCompany)),
        XivChatType.SystemMessage            => Legacy(nameof(LegacyChatHistoryService.Category.System)),
        XivChatType.Gathering                => LegacyGathering,
        XivChatType.NPCDialogue or XivChatType.NPCDialogueAnnouncements
                                             => Legacy(nameof(LegacyChatHistoryService.Category.Dialogue)),
        XivChatType.LootNotice               => Legacy(nameof(LegacyChatHistoryService.Category.Loot)),
        _                                    => null,
    };

    /// <summary>Die eingestellte Stimme eines Kanals, oder null fuer "Text".</summary>
    public static string? VoiceFor(Configuration config, string? key) =>
        key != null && config.ChatVoice.TryGetValue(key, out var name) && name.Length > 0 ? name : null;
}

/// <summary>
/// Der Sprachkanal fuer den Chat: eine eigene SAPI-Stimme je Chat-Kanal, neben dem
/// Screenreader - nachgebaut nach SkuChat, wo jeder Kanal "Stumm", "Text" oder
/// "Blizzard TTS" mit eigener Stimme sein kann.
///
/// WARUM EIN EIGENER SPRECHER UND NICHT DER DER WARNSTIMME
/// (<see cref="WarningVoiceService"/>): die Warnstimme bricht bei jeder neuen
/// Warnung ab, was gerade laeuft. Laege der Chat auf demselben Sprecher, wuerde
/// eine lange Chatzeile eine Kampfwarnung verzoegern und jede Warnung eine
/// Chatzeile abschneiden. Zwei SpeechSynthesizer sind zwei unabhaengige
/// Warteschlangen.
///
/// DER CHAT WIRD GEREIHT, NICHT ABGEBROCHEN - wie in Sku, das jede Chatzeile mit
/// "wait = true" in seine Warteschlange stellt. Eine neue Zeile schneidet die
/// laufende also nicht ab; die Stopptaste des Plugins (KeySilence) leert die
/// Schlange.
///
/// NIE STUMM STATT GESPROCHEN: fehlt SAPI, ist die gewaehlte Stimme nicht mehr
/// installiert oder steht die Lautstaerke auf null, geht die Zeile ueber den
/// Screenreader. Gewollte Stille ist "Stumm" im Menue und sonst nichts.
/// </summary>
public sealed class ChatVoiceService : IDisposable
{
    private readonly Configuration _config;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;
    // Nur fuer die Frage, welcher Kanal des Spiels ein Dialogfenster spiegelt
    // (SpeakDialogueWindow) - dieselbe Nachschlagetabelle, die der Chat-Router nimmt.
    private readonly GameChatFilters _filters;

    private SpeechSynthesizer? _synth;

    // Entprellung wie in TolkService.SpeakInterrupt: dieselbe Zeile nicht zweimal
    // kurz hintereinander. Gemessen 2026-09-14 11:04:28.013/.014: eine
    // _BattleTalk-Zeile kommt im Chat und im Dialogfenster in DERSELBEN
    // Millisekunde. Auf dem Screenreader-Weg hat dessen Entprellung die zweite
    // geschluckt; seit beide Wege diese Stimme nehmen koennen, muss sie es hier tun.
    private const double DebounceSeconds = 1.0;
    private string _lastSpoken = string.Empty;
    private long _lastSpokenTick;

    /// <summary>Stimmen, deren Fehlen schon protokolliert ist - eine Zeile pro
    /// Stimme statt einer pro Chatnachricht.</summary>
    private readonly HashSet<string> _reportedMissing = new(StringComparer.Ordinal);

    /// <summary>Die Namen aller SAPI-Stimmen, die das System anbietet, in der
    /// Reihenfolge, in der das Menue sie zur Wahl stellt.</summary>
    public IReadOnlyList<string> InstalledVoices { get; private set; } = Array.Empty<string>();

    /// <summary>Ob ueber diesen Kanal gesprochen werden kann.</summary>
    public bool IsAvailable => _synth != null;

    public ChatVoiceService(Configuration config, TolkService tolk, IPluginLog log, GameChatFilters filters)
    {
        _filters = filters;
        _config = config;
        _tolk   = tolk;
        _log    = log;
        Initialize();
    }

    /// <summary>
    /// Baut den Sprecher auf. Try-catch wie in WarningVoiceService: SAPI ist eine
    /// COM-Schnittstelle des Betriebssystems, also ein externer Aufruf. Der Fehler
    /// geht ins Log, <see cref="IsAvailable"/> bleibt false, und jede Chatzeile
    /// laeuft ueber den Screenreader.
    /// </summary>
    private void Initialize()
    {
        try
        {
            var synth = new SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();

            InstalledVoices = synth.GetInstalledVoices()
                                   .Where(v => v.Enabled)
                                   .Select(v => v.VoiceInfo.Name)
                                   .ToList();

            if (InstalledVoices.Count == 0)
            {
                _log.Warning("[Chatstimme] SAPI meldet keine aktive Stimme - der Chat bleibt beim Screenreader.");
                synth.Dispose();
                return;
            }

            _synth = synth;
            _log.Info($"[Chatstimme] SAPI bereit. Stimmen: {string.Join(", ", InstalledVoices)}.");
        }
        catch (Exception ex)
        {
            _synth = null;
            _log.Error($"[Chatstimme] SAPI nicht verfuegbar ({ex.GetType().Name}: {ex.Message}) - " +
                       "der Chat bleibt beim Screenreader.");
        }
    }

    /// <summary>
    /// Sagt eine fertige Chatzeile auf dem Weg, den der Spieler fuer ihren Kanal
    /// gewaehlt hat: mit der eingestellten Stimme, sonst ueber den Screenreader.
    ///
    /// Die Braillezeile bekommt die Zeile in BEIDEN Faellen (Nutzerwunsch
    /// 2026-07-16: jede Ansage auch auf die Braillezeile) - Tolk_Output tut das
    /// auf dem Screenreader-Weg von selbst, auf dem Stimmen-Weg geschieht es hier.
    /// </summary>
    /// <param name="text">Die gesprochene Zeile, mit Kanal und Absender.</param>
    /// <param name="voiceKey">Schluessel aus <see cref="ChatVoiceKeys"/>, oder null
    /// fuer eine Zeile ohne einstellbaren Kanal.</param>
    /// <param name="interrupt">Nur fuer den Screenreader-Weg: ob die Zeile NVDA
    /// unterbricht. Die Stimme reiht immer (siehe Klassenkommentar).</param>
    public void SpeakChatLine(string text, string? voiceKey, bool interrupt) =>
        SpeakChatLine(text, voiceKey, interrupt, talkPage: false);

    private void SpeakChatLine(string text, string? voiceKey, bool interrupt, bool talkPage)
    {
        var voice = ChatVoiceKeys.VoiceFor(_config, voiceKey);
        if (voice != null && Speak(text, voice, talkPage))
        {
            _tolk.Braille(text);
            // In den Echo-Speicher des Screenreader-Wegs, denn die Echo-Pruefungen
            // (Toast-Doppel, NPC-Dialog) fragen dort, und fuer den Spieler ist die
            // Zeile gesagt - egal mit welcher Stimme.
            _tolk.RememberSpoken(text);
            return;
        }

        if (interrupt) _tolk.SpeakInterrupt(text);
        else           _tolk.Speak(text);
    }

    /// <summary>
    /// Sagt eine Zeile aus dem DIALOGFENSTER (Talk, _BattleTalk) mit der Stimme des
    /// Chat-Kanals, der dieses Fenster spiegelt - oder ueber den Screenreader, wenn
    /// der Kanal auf "Text" steht.
    ///
    /// WARUM DAS FENSTER DIE CHATSTIMME NIMMT (Spielerin 2026-09-14: "der chatt wird
    /// jetzt doppelt vorgelesen einmal von nvda und von hedda"): jede NPC-Rede kommt
    /// auf zwei Wegen, als Fenster und als Chatzeile, und das Plugin sagt davon nur
    /// den ERSTEN (die Echo-Pruefung in beiden Chat-Lesern und die Entprellung).
    /// Welcher Weg zuerst ist, entscheidet das Spiel - bei _BattleTalk der Chat, bei
    /// Talk das Fenster. Solange beide Wege verschiedene Stimmen nahmen, hoerte die
    /// Spielerin also mal Hedda, mal NVDA, und wo die Echo-Pruefung nicht griff,
    /// beide zugleich. Mit derselben Stimme auf beiden Wegen ist "Dialoge in Hedda"
    /// eine Einstellung, die auch haelt.
    /// </summary>
    /// <param name="kind">Der Chat-Typ, den das Spiel fuer dieses Fenster in den
    /// Chat schreibt: NPCDialogue (61) fuer Talk, NPCDialogueAnnouncements (68) fuer
    /// _BattleTalk.</param>
    ///
    /// WEITERKLICKEN BRICHT DIE ALTE SEITE AB, aber nur im Talk-Fenster
    /// (Spielerin 2026-09-14: "mach das ruhig, dass man mit weiter den text
    /// abbrechen kann"). Eine neue Talk-Seite gibt es nur, wenn die Spielerin
    /// weitergeklickt hat - so wie NVDA die alte Seite bisher mit SpeakInterrupt
    /// abgeschnitten hat. Abgebrochen wird GENAU die vorige Seite, nicht die ganze
    /// Warteschlange: ein Fluestern, das dahinter wartet, bleibt stehen.
    /// _BattleTalk laeuft von selbst weiter, ohne dass jemand klickt; dort wuerde
    /// Abbrechen Kampfrufe verschlucken, also wird es dort nicht getan.
    public void SpeakDialogueWindow(string text, XivChatType kind)
    {
        string? key;
        if (_config.UseLegacyChatSystem)
            key = ChatVoiceKeys.ForLegacyKind(kind);
        else
            key = _filters.ChannelOfKind(kind) is { } channel ? ChatVoiceKeys.Channel(channel.Key) : null;

        SpeakChatLine(text, key, interrupt: true, talkPage: kind == XivChatType.NPCDialogue);
    }

    /// <summary>Die zuletzt gereihte Talk-Seite, damit die naechste sie abbrechen
    /// kann (siehe <see cref="SpeakDialogueWindow"/>).</summary>
    private Prompt? _talkPrompt;

    /// <summary>Spricht <paramref name="text"/> mit <paramref name="voiceName"/>.
    /// false heisst: nicht uebernommen, der Aufrufer nimmt den Screenreader.</summary>
    private bool Speak(string text, string voiceName, bool talkPage)
    {
        if (_synth == null || string.IsNullOrWhiteSpace(text)) return false;
        if (_config.ChatVoiceVolume <= 0f) return false;
        // Mod SAPI only while the game window is focused. Returning false lets
        // the caller fall back to the screen reader.
        if (!GameWindowFocus.IsActive) return false;

        text = TolkService.Sanitize(text);
        if (text.Length == 0) return false;

        var voice = FindVoice(voiceName);
        if (voice == null)
        {
            if (_reportedMissing.Add(voiceName))
                _log.Warning($"[Chatstimme] Stimme '{voiceName}' ist nicht (mehr) installiert - " +
                             "dieser Kanal geht ueber den Screenreader.");
            return false;
        }

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = (double)(now - _lastSpokenTick) / System.Diagnostics.Stopwatch.Frequency;
        if (text == _lastSpoken && elapsed < DebounceSeconds)
        {
            // true: dieser Kanal HAT die Zeile uebernommen, nur eben schon gesagt.
            // Mit false ginge sie auf den Screenreader - genau die Doppelung, die
            // hier verhindert werden soll.
            _log.Info($"[Chatstimme] ENTPRELLT '{text}'");
            return true;
        }

        try
        {
            ApplyRateAndVolume();
            if (talkPage && _talkPrompt is { IsCompleted: false } previous)
            {
                _synth.SpeakAsyncCancel(previous);
                _log.Info("[Chatstimme] Vorige Dialogseite abgebrochen (weitergeklickt).");
            }
            var prompt = _synth.SpeakAsync(BuildPrompt(voice, text));
            if (talkPage) _talkPrompt = prompt;
            _lastSpoken     = text;
            _lastSpokenTick = now;
            _log.Info($"[Chatstimme] '{text}' ({voice.VoiceInfo.Name}, Tempo {_synth.Rate}, Lautstaerke {_synth.Volume})");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"[Chatstimme] Sprechen fehlgeschlagen ({ex.GetType().Name}: {ex.Message}) - " +
                       "diese Zeile geht ueber den Screenreader.");
            return false;
        }
    }

    /// <summary>
    /// Die Stimme steckt IM PROMPT (PromptBuilder.StartVoice) und nicht im
    /// Sprecher (SelectVoice). Zeilen verschiedener Kanaele stehen gleichzeitig in
    /// der Warteschlange, und jede muss ihre eigene Stimme mitnehmen - ein
    /// SelectVoice fuer die zweite Zeile duerfte nicht die erste treffen, die noch
    /// wartet. StartVoice ist der dokumentierte Weg, innerhalb eines Prompts die
    /// Stimme zu wechseln; die Kultur des Prompts ist die der Stimme, damit SAPI
    /// nicht wegen einer abweichenden Sprachangabe eine andere waehlt.
    /// </summary>
    private static PromptBuilder BuildPrompt(InstalledVoice voice, string text)
    {
        var prompt = new PromptBuilder(voice.VoiceInfo.Culture);
        prompt.StartVoice(voice.VoiceInfo);
        prompt.AppendText(text);
        prompt.EndVoice();
        return prompt;
    }

    /// <summary>Tempo und Lautstaerke gelten fuer alle Chatstimmen und werden vor
    /// jedem Sprechen nachgezogen, damit eine Aenderung im Menue sofort wirkt.</summary>
    private void ApplyRateAndVolume()
    {
        if (_synth == null) return;
        _synth.Volume = Math.Clamp((int)Math.Round(_config.ChatVoiceVolume * 100f), 0, 100);
        _synth.Rate   = Math.Clamp(_config.ChatVoiceRate, -10, 10);
    }

    private InstalledVoice? FindVoice(string name)
    {
        if (_synth == null) return null;
        foreach (var voice in _synth.GetInstalledVoices())
            if (voice.Enabled && string.Equals(voice.VoiceInfo.Name, name, StringComparison.Ordinal))
                return voice;
        return null;
    }

    /// <summary>
    /// Spielt im Menue <paramref name="text"/> mit einer Stimme vor - entschieden
    /// wird am Ohr, nicht am Namen. Bricht eine laufende Probe ab, damit beim
    /// Durchprobieren nicht eine Stimme nach der anderen nachklingt. false, wenn
    /// nichts zu hoeren war; dann sagt die Menuezeile es ueber den Screenreader.
    /// </summary>
    /// <param name="voiceName">Die Stimme, oder null fuer die erste, die im Chat
    /// gerade eingestellt ist (fuer Tempo und Lautstaerke).</param>
    public bool PlayPreview(string? voiceName, string text)
    {
        if (_synth == null || _config.ChatVoiceVolume <= 0f) return false;
        if (!GameWindowFocus.IsActive) return false;

        voiceName ??= _config.ChatVoice.Values.FirstOrDefault(v => v.Length > 0);
        var voice = voiceName != null ? FindVoice(voiceName) : null;
        if (voice == null) return false;

        try
        {
            ApplyRateAndVolume();
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(BuildPrompt(voice, text));
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"[Chatstimme] Probe von '{voiceName}' fehlgeschlagen ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
    }

    /// <summary>Leert die Warteschlange - fuer die Stopptaste, die den
    /// Screenreader zum Schweigen bringt und die Chatstimme sonst weiterreden liesse.</summary>
    public void Silence()
    {
        if (_synth == null) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch (Exception ex)
        {
            _log.Error($"[Chatstimme] Anhalten fehlgeschlagen ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    public void Dispose()
    {
        if (_synth == null) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error($"[Chatstimme] Aufraeumen fehlgeschlagen ({ex.GetType().Name}: {ex.Message}).");
        }
        _synth = null;
    }
}
