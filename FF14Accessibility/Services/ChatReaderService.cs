using System;
using System.Collections.Generic;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads the chat log aloud and files every line into the buffer the GAME would
/// show it in.
///
/// MEMBERSHIP IS THE GAME'S, END TO END. This class no
/// longer decides which channels exist, which bucket a line belongs in, or whether a
/// channel is worth keeping. It asks <see cref="GameChatFilters"/> which of the
/// player's own chat TABS would show the line, archives it into each of those, and
/// speaks it once if any of them has speech switched on.
///
/// What that replaced, and why it had to go: <c>MapCategory</c> was a mod-side
/// bucket-per-chat-type table, and <c>ChatChannels.cs</c> was a mod-side
/// catalogue of sixty-odd channels with its own groups, defaults and switches. Both
/// were second sources of truth for something the game already models per tab, so
/// they answered differently from what the player had configured. The
/// evidence that justified the rewrite - NPC dialogue being dropped - justified the bug
/// report, not the catalogue.
///
/// THREE THINGS ARE STILL DECIDED HERE, and none of them is membership:
///  - HOW a line is worded. Emotes and battle-log lines are already complete
///    sentences that name their own actor, so the "&lt;channel&gt; from
///    &lt;sender&gt;:" wrapper would say the actor twice. The game does not hand over
///    a spoken form, so this stays the mod's job.
///  - Whether a line INTERRUPTS the screen reader or queues behind it.
///  - The NPC dialogue de-duplication, because that is about the mod's own two
///    reading paths, not about the game's filters.
/// </summary>
public sealed class ChatReaderService : IDisposable
{
    private readonly IChatGui _chatGui;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly MessageHistoryService _history;
    private readonly IObjectTable _objectTable;
    private readonly IPluginLog _log;

    /// <summary>The game's own tabs and filter rows.</summary>
    private readonly GameChatFilters _filters;

    /// <summary>Wie lange eine schon aus dem Dialogfenster gesprochene NPC-Zeile den
    /// Chat-Nachzuegler unterdrueckt. Unveraendert uebernommen.</summary>
    private const double NpcDialogueEchoSeconds = 120;

    /// <summary>Reused across messages so routing does not allocate per chat line -
    /// a busy fight is several lines a second.</summary>
    private readonly List<int> _showingTabs = new();

    /// <summary>The channels this message belongs to. Same
    /// reason as <see cref="_showingTabs"/>: reused, never reallocated.</summary>
    private readonly List<int> _channels = new();

    /// <summary>Which switch of the game's filter list got this
    /// line into which tab, and under which channel - what the speech switches are read
    /// against. Same reuse.</summary>
    private readonly List<GameChatFilters.ChatRoute> _routes = new();

    /// <summary>Battle-log shapes already logged, so the mod log carries one line per
    /// distinct (kind, source, target) instead of one per hit.</summary>
    private readonly HashSet<int> _loggedShapes = new();

    /// <summary>Whether the "filters unreadable" notice has been given this session.</summary>
    private bool _warnedFallback;

    /// <summary>
    /// How many chat messages have arrived since the plugin
    /// loaded - EVERY event, including the ones that are filtered out or have no text,
    /// because the game stored all of them either way.
    ///
    /// It is what tells the backfill where the game's stored log stops being history and
    /// starts being this session: the newest <c>LiveMessagesSeen</c> records are the ones
    /// the live hook has already archived, so backfilling them would double every line.
    /// See <see cref="ChatBackfill"/>.
    /// </summary>
    public int LiveMessagesSeen { get; private set; }

    /// <summary>
    /// Ob DIESES System gerade das Gesprochene bestreitet. Archiviert wird
    /// immer, gesprochen nur, wenn der Spieler das neue Chatsystem eingeschaltet
    /// hat (Optionsmenue, <see cref="Configuration.UseLegacyChatSystem"/>) -
    /// sonst redet der alte Leser, der daneben laeuft.
    /// </summary>
    private readonly Func<bool> _isActive;

    public ChatReaderService(IChatGui chatGui, TolkService tolk, Configuration config,
        MessageHistoryService history, IObjectTable objectTable, IPluginLog log,
        GameChatFilters filters, Func<bool> isActive)
    {
        _chatGui = chatGui;
        _tolk = tolk;
        _config = config;
        _history = history;
        _objectTable = objectTable;
        _log = log;
        _filters = filters;
        _isActive = isActive;

        _chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        // Counted BEFORE any early return: the game's own log
        // stores a record whether or not this line has text and whether or not a tab
        // shows it, so the backfill's boundary has to count it too.
        LiveMessagesSeen++;

        var senderText = msg.Sender?.TextValue ?? string.Empty;
        var messageText = msg.Message?.TextValue ?? string.Empty;
        if (string.IsNullOrWhiteSpace(messageText)) return;

        // Battle-log lines are no longer diverted to a
        // separate service before they get here. The game's own Battle tab carries
        // the whole combat narrative, split by actor far more finely than the mod's
        // Kampf channel ever was, so they travel the ordinary road now and land in
        // whichever tab the player has them switched on for.
        var battleLog = IsBattleLogLine(msg.LogKind);

        // Which CHANNELS this line belongs to and which of the
        // player's tabs show it - one row match answers both. Anything but Ready means
        // the game's own state could not be read, which is a different thing from "no
        // tab shows it" and must not be treated as a filter decision.
        var state = _filters.Route(msg.LogKind, msg.SourceKind, msg.TargetKind,
                                   _channels, _showingTabs, _routes, out var coverage);

        LogLine(msg, senderText, messageText, battleLog, state);

        // Own message? Decided BEFORE archiving, because the archive has to use
        // the same name the announcement does.
        var ownName = _objectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
        var isOwn = msg.LogKind == XivChatType.TellOutgoing
                    || (!string.IsNullOrEmpty(ownName) && senderText == ownName);

        // For an OUTGOING tell the game puts the RECIPIENT in Sender - archiving
        // that made the player's own line look like it came from the other person
        // (user report 2026-07-19). Own lines are stored under the player's own
        // name; the recipient is kept as an addressee so the entry stays useful
        // when reading back a conversation.
        var archiveName = isOwn ? ownName : senderText;
        var addressee = msg.LogKind == XivChatType.TellOutgoing && !string.IsNullOrWhiteSpace(senderText)
            ? AccessibilityStrings.ChatAddressee(senderText)
            : string.Empty;

        var archived = BuildArchivedText(msg.LogKind, battleLog, archiveName, addressee, senderText, messageText);
        var partner = ExtractTellPartner(msg);

        bool speak;
        if (state != ChatFilterState.Ready)
        {
            // THE DEGRADED PATH. Everything goes to one
            // buffer and everything except the battle log is still read out, because
            // the mod must not decide for the player which channels matter when it
            // cannot see what they chose. The battle log is the one exclusion, and
            // only because it is a known flood that would bury the cast warnings and
            // HP thresholds that keep the player alive.
            //
            // Only a BROKEN state is announced. NotReady is ordinary - the tab names
            // come from the character's config and are empty until the player is in a
            // world, so chat that arrives in that window would otherwise spend a
            // spoken warning on every single login.
            // Die Warnung nur, wenn dieses System auch spricht: im alten
            // Chatsystem ist der Filterzustand ohne Bedeutung, und eine Warnung
            // ueber ein System, das der Spieler gar nicht benutzt, waere Laerm.
            if (state == ChatFilterState.Broken && _isActive()) WarnFallbackOnce();
            _history.Add(_history.EnsureFallbackBuffer(), archived, partner, mirror: false);
            speak = !battleLog && ChatTabSpeech.IsOn(_config, ChatTabSpeech.FallbackIndex, true);
        }
        else
        {
            // In no channel - and WHY decides whether the line
            // lives. Only a switch the player turned off may silence it; a line the game
            // gives no switch for is unfilterable and has to be delivered, or the mod is
            // inventing a filter the player cannot see and cannot turn back on.
            //
            // This was a real, measured loss: at the 2026-08-11 09:46 login, five kind=3
            // Notice lines - the Yo-kai event, "Be Wary of Phishing Attempts via Tell"
            // and its explanation, and both welcome lines - were dropped here, because
            // the LogFilter sheet has no row for chat kind 3. 21 of the 81 kinds Dalamud
            // knows are in that hole, TellIncoming and the GM channels among them.
            if (_channels.Count == 0)
            {
                if (coverage == ChatCoverage.SwitchedOff) return;
                speak = ArchiveUnfilterable(msg.LogKind, archived, partner, battleLog);
            }
            else
            {
                speak = ArchiveRouted(archived, partner);
            }
        }

        if (!speak) return;

        // DAS ANDERE CHATSYSTEM REDET GERADE. Ab hier stehen nur noch
        // Nebenwirkungen ausserhalb dieser Klasse - Sprache und die beiden
        // Echo-Speicher -, und die duerfen dem alten Leser nicht in die Quere
        // kommen. Das Archivieren oben ist da schon passiert, die Puffer dieses
        // Systems bleiben also vollstaendig.
        if (!_isActive()) return;

        // NPC-Rede erreicht den Spieler ZWEIMAL: das Talk/_BattleTalk-Fenster zeigt
        // sie, und der Chatlog wiederholt sie Sekunden spaeter. Das ist die bisherige
        // Entdopplung dieses Plugins, unveraendert - sie liegt ausserhalb dessen, was
        // die Puffer aendern.
        //
        // Sie steht NACH dem Archivieren und INNERHALB des Sprech-Zweigs, und das ist
        // Absicht: eine Zeile, die dieser Weg ohnehin nicht sagen wird, darf den
        // Eintrag des anderen Wegs nicht verbrauchen.
        if (IsNpcDialogue(msg.LogKind) &&
            _tolk.WasSpokenElsewhere(messageText, NpcDialogueEchoSeconds))
        {
            _log.Info($"[Chat] NPC-Dialog schon aus dem Fenster gesprochen, nicht wiederholt: '{messageText}'");
            return;
        }

        // Many toast notifications (_TextError etc.) the UIReader already spoke
        // are mirrored into the chat log as SystemMessage/ErrorMessage a few
        // seconds later. Skip the echo when the plain message (no prefix) was
        // just spoken (log 2026-07-12: "Du hast einen Auftrag angenommen!" twice).
        //
        // NOT for battle-log lines. They have no toast twin,
        // and two identical hits inside six seconds is an ordinary rotation - the old
        // combat log spoke them verbatim without this check for exactly that reason.
        if (!battleLog && _tolk.WasRecentlySpoken(messageText, 6)) return;

        var fullText = BuildSpokenText(msg.LogKind, battleLog, isOwn, addressee, senderText, messageText);

        var interrupt = msg.LogKind is XivChatType.Say or XivChatType.Shout or XivChatType.Party
                                    or XivChatType.Alliance or XivChatType.TellIncoming
                                    or XivChatType.Yell or XivChatType.CrossParty
                                    or XivChatType.TellOutgoing;

        // Battle-log lines are QUEUED, never interrupting: they arrive several per
        // second in a real rotation and SpeakInterrupt WIPES NVDA's queue (see the
        // speech-queue note in UIReaderService.PassiveAddons - 208 announcements died
        // that way in one logged session). The `interrupt` set above already excludes
        // them; this note is here so it stays that way.
        if (interrupt)
            _tolk.SpeakInterrupt(fullText);
        else
            _tolk.Speak(fullText);

        // What went into the dedup history is the PREFIXED line ("System: ..."),
        // but a toast arriving right afterwards carries the bare sentence and
        // asks for exactly that. Without this the same system message was read
        // twice - once from the chat log, once as a toast a millisecond later
        // (user report + log 2026-08-08 23:43:53.396/.397). File the bare
        // wording too, and only when a prefix was actually added: with none,
        // Speak already stored this very string.
        if (!string.Equals(fullText, messageText, StringComparison.Ordinal))
            _tolk.RememberSpokenVariant(messageText);
    }

    /// <summary>
    /// Archives a line that DID match the game's switches, into
    /// its channels and into each showing tab, and says whether it should be spoken.
    ///
    /// Split out of <see cref="OnChatMessage"/> when the unfilterable case gained its own
    /// path (see <see cref="ArchiveUnfilterable"/>), so the two branches read as the two
    /// answers they are rather than as one method with a hole in the middle.
    /// </summary>
    /// <returns>Whether the line should be read out loud.</returns>
    private bool ArchiveRouted(string archived, TellTarget? partner)
    {

        // ARCHIVE PER CHANNEL, SPEAK PER TAB, and the two
        // lists are deliberately independent. Membership is what the line IS - the
        // game's own switch it matched - and it does not change when the player
        // looks at another tab. Delivery is what the player hears right now, which
        // is a property of the tab and stays there: per tab, because
        // per-channel speech switches would rebuild the sixty-odd-entry catalogue
        // this whole rewrite deleted.
        foreach (var key in _channels)
        {
            var channel = _filters.Channel(key);
            if (channel == null) continue;
            _history.Add(_history.EnsureChannelBuffer(channel), archived, partner, mirror: false);
        }

        // AND ONCE MORE INTO EACH SHOWING TAB'S "ALL"
        // BUFFER - the tab as a sighted player sees it, every channel interleaved in
        // arrival order. Driven by the SAME `_showingTabs` list the speech decision
        // below reads, so the buffer holds exactly the lines that tab displays; it
        // is a second ORDERING of one routing answer, never a second answer.
        //
        // Deliberately not derived by merging the channel buffers on demand: a line
        // can reach a channel through a switch that is on in another tab, so the
        // union of a tab's channels is not the same set as the tab's own lines, and
        // merging by timestamp would need a clock the archive does not keep.
        foreach (var index in _showingTabs)
            _history.Add(_history.EnsureTabBuffer(index), archived, partner, mirror: false);

        // SPEAK PER ROUTE. The tab's master decides whether
        // the tab says anything at all; under it sits one switch per channel, and
        // under that one per filter row - the game's own checkbox, which is the only
        // level that separates "damage dealt by you" from "damage you are dealt".
        // So the battle log can announce what is being done TO the player and archive
        // the rest in silence, which is what the user asked for.
        //
        // A ROUTE, not the cross product: the triple says this row, under this
        // channel, is what got the line into this tab. Testing every channel against
        // every showing tab would let a channel silenced here be spoken because
        // another tab shows the same line through a different switch.
        //
        // Each level inherits the one above until the player sets it, which is why
        // the default handed in is `true` - the master is already tested one line up.
        var speak = false;
        foreach (var route in _routes)
        {
            if (!ChatTabSpeech.IsOn(_config, route.Tab, DefaultSpeechFor(route.Tab))) continue;
            if (!ChatTabSpeech.RowIsOn(_config, route.Tab, route.Channel, route.Row, true)) continue;
            speak = true;
            break;
        }

        return speak;
    }

    /// <summary>
    /// Archives a line the game offers no switch for, and says
    /// whether it should be spoken.
    ///
    /// WHERE IT GOES, and the reasoning is not a measurement. A tab's contents are the
    /// lines whose switches it has on; a line with no switch cannot be excluded by any
    /// tab's filter set, so every tab shows it — which is why it goes into each tab's
    /// "all" buffer, the buffer defined as the tab exactly as a sighted player sees it.
    /// It gets no CHANNEL buffer because it has no channel: the game never grouped it -
    /// EXCEPT wenn die Gegenrichtung einen hat, siehe <see cref="SameConversationAs"/>.
    /// If a line ever turns up in a tab a sighted player does not see it in, this
    /// paragraph is the assumption to revisit.
    ///
    /// SPEECH IS ON BY DEFAULT and gated by ONE switch, not by the tab masters. These
    /// lines are the phishing warning, the seasonal-event notice, a GM contacting the
    /// player, an incoming tell — and the player has no game-side way to silence any of
    /// them, so the mod must not silence them by inheriting a battle tab's master. The
    /// single switch under Chat-Register is there so the class is not unsilenceable
    /// either.
    /// </summary>
    /// <returns>Whether the line should be read out loud.</returns>
    private bool ArchiveUnfilterable(XivChatType kind, string archived, TellTarget? partner, bool battleLog)
    {
        foreach (var tab in _filters.Tabs)
            _history.Add(_history.EnsureTabBuffer(tab.Index), archived, partner, mirror: false);

        // DIE GEGENRICHTUNG EINER UNTERHALTUNG. Gemessene Lage (Log 2026-08-13
        // 20:58:42 und 20:59:59): ein eingehendes Fluestern kommt als
        // "kanal=keine" hier an, ein ausgehendes als "kanal=Fluestern". Das Sheet
        // fuehrt fuer Kind 13 keine Zeile, fuer Kind 12 schon - der Puffer
        // "Fluestern" entstand also erst, wenn der Spieler selbst schrieb, und
        // enthielt nie die Antworten (User-Meldung 2026-08-13).
        //
        // Beide Richtungen gehoeren in denselben Puffer: eine Unterhaltung ist
        // eine Unterhaltung, und ein Verlauf, der nur die eigene Haelfte fuehrt,
        // ist als Nachlese wertlos. Der Kanal wird dafuer NICHT erfunden, sondern
        // ueber die Gegenrichtung im Sheet nachgeschlagen
        // (GameChatFilters.ChannelOfKind) - gibt es ihn dort nicht, bleibt es
        // beim bisherigen Verhalten.
        //
        // NUR DAS ARCHIV, NICHT DAS SPRECHEN: die Sprech-Entscheidung faellt
        // weiter unten ueber den Unfiltered-Schalter. Wuerde die Zeile ab hier
        // dem Register-Schalter ihres neuen Kanals folgen, koennte ein
        // ausgeschaltetes Register eingehende Fluester verstummen lassen - und
        // dagegen hat der Spieler im Spiel selbst keinen Schalter.
        if (SameConversationAs(kind) is { } counterpart &&
            _filters.ChannelOfKind(counterpart) is { } channel)
        {
            _history.Add(_history.EnsureChannelBuffer(channel), archived, partner, mirror: false);
            _log.Info($"[Chat] {kind} hat keinen eigenen Schalter - zusaetzlich in den Kanal "
                      + $"'{channel.Name}' von {counterpart} archiviert.");
        }

        // No tab at all - possible for one frame while the tab list is rebuilding. The
        // line still must not vanish, so it goes where the mod's own notices go.
        if (_filters.Tabs.Count == 0)
            _history.Add(MessageHistoryService.SystemKey, archived, partner, mirror: false);

        // THE BATTLE LOG IS THE ONE EXEMPTION, and it is the same one the degraded path
        // makes for the same reason: several lines a second mid-rotation would bury the
        // cast warnings and HP thresholds that keep the player alive. It is a SPEECH
        // decision only - the line is archived above either way. No battle shape is known
        // to land here (all nine battle kinds have rows), so this is a guard against a
        // flood that would otherwise arrive unannounced, not a filter on anything seen.
        if (battleLog) return false;

        return ChatTabSpeech.IsOn(_config, ChatTabSpeech.UnfilteredIndex, true);
    }

    /// <summary>
    /// Die Chat-Art, die dieselbe Unterhaltung von der anderen Seite fuehrt, oder
    /// null. Absichtlich eine winzige, benannte Liste statt einer Regel: es ist
    /// eine Aussage darueber, was fuer den SPIELER dasselbe Gespraech ist, und die
    /// steht nirgends in den Spieldaten.
    ///
    /// Nur die Fluester-Kanaele stehen drin, und nur, weil beide Richtungen
    /// dieselben zwei Personen betreffen. Nicht dabei ist zum Beispiel /sagen und
    /// /rufen: die haben je einen eigenen Schalter, brauchen das hier also gar
    /// nicht.
    /// </summary>
    private static XivChatType? SameConversationAs(XivChatType kind) => kind switch
    {
        XivChatType.TellIncoming => XivChatType.TellOutgoing,
        XivChatType.TellOutgoing => XivChatType.TellIncoming,
        _                        => null,
    };

    /// <summary>
    /// Files a message the GAME had stored before the plugin
    /// loaded, without speaking a word of it.
    ///
    /// THIS IS A SECOND SOURCE FOR ONE PIPELINE, not a second pipeline. Routing, wording
    /// and the buffers are the same code the live path runs - the only differences are
    /// the three this case demands, and each is deliberate:
    ///
    ///  - NOTHING IS SPOKEN. Replaying an hour of chat aloud on a plugin reload would be
    ///    the worst possible reload behaviour, and it is why the speech half of
    ///    <see cref="OnChatMessage"/> is not reachable from here at all rather than
    ///    guarded by a flag that could be got wrong later.
    ///  - Entries are INSERTED, through <see cref="MessageHistoryService.AddOlder"/>, so
    ///    the recovered history lands in front of the few live lines that arrived while
    ///    the game's log module was still starting up.
    ///  - The NPC dialogue de-duplication is not touched. It pairs the two paths that
    ///    speak an NPC's line; a line nobody is going to say must not consume a claim
    ///    the popup reader is waiting for.
    ///
    /// A DEGRADED FILTER STATE MEANS NO BACKFILL. The fallback buffer exists so the mod
    /// keeps working when it cannot read the player's tabs, and filling it with an hour
    /// of history the player never asked to see there would make a bad state worse. The
    /// caller only runs while the filters read Ready.
    ///
    /// The filter state used is the one in force NOW, not the one in force when the line
    /// was printed - the game does not keep filter history, so this is the honest answer
    /// and not a shortcut.
    /// </summary>
    /// <returns>True when the line went into at least one buffer.</returns>
    public bool ArchiveStored(XivChatType kind, XivChatRelationKind source,
                              XivChatRelationKind target, SeString? sender, SeString? message)
    {
        var senderText = sender?.TextValue ?? string.Empty;
        var messageText = message?.TextValue ?? string.Empty;
        if (string.IsNullOrWhiteSpace(messageText)) return false;

        var state = _filters.Route(kind, source, target, _channels, _showingTabs, _routes,
                                   out var coverage);
        if (state != ChatFilterState.Ready) return false;

        // Same rule as the live path, and only this branch may
        // drop: a switch the player turned off. Anything else is delivered.
        if (_channels.Count == 0 && coverage == ChatCoverage.SwitchedOff) return false;

        var ownName = _objectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
        var isOwn = kind == XivChatType.TellOutgoing
                    || (!string.IsNullOrEmpty(ownName) && senderText == ownName);
        var archiveName = isOwn ? ownName : senderText;
        var addressee = kind == XivChatType.TellOutgoing && !string.IsNullOrWhiteSpace(senderText)
            ? AccessibilityStrings.ChatAddressee(senderText)
            : string.Empty;

        var archived = BuildArchivedText(kind, IsBattleLogLine(kind), archiveName, addressee,
                                         senderText, messageText);
        var partner = ExtractTellPartner(kind, sender, verbose: false);

        // A stored line the game gives no switch for has no
        // channel and named no tab, so it is recovered into every tab's "all" buffer -
        // the same placement the live path uses, and for the same reason: a line nothing
        // can filter is a line every tab shows.
        if (_channels.Count == 0)
        {
            foreach (var tab in _filters.Tabs)
                _history.AddOlder(_history.EnsureTabBuffer(tab.Index), archived, partner);
            return _filters.Tabs.Count > 0;
        }

        foreach (var key in _channels)
        {
            var channel = _filters.Channel(key);
            if (channel == null) continue;
            _history.AddOlder(_history.EnsureChannelBuffer(channel), archived, partner);
        }

        foreach (var index in _showingTabs)
            _history.AddOlder(_history.EnsureTabBuffer(index), archived, partner);

        return true;
    }

    // ── Wording ───────────────────────────────────────────────────
    //
    // The game hands over a chat kind, two relation kinds and a text. It does NOT
    // hand over a spoken form, so how a line is worded is genuinely the mod's job -
    // this is the part of the old code that survives the rewrite unchanged in
    // substance.

    /// <summary>
    /// The form written to the history. Same three shapes as
    /// <see cref="BuildSpokenText"/>, minus the channel prefix: the buffer is the tab
    /// the player is reading, so it already says where the line came from.
    /// </summary>
    private static string BuildArchivedText(XivChatType kind, bool battleLog,
        string archiveName, string addressee, string senderText, string messageText)
    {
        // A battle-log line is a complete, localized sentence that already names its
        // actor ("Ifrit hits you for 4,502 damage."). Prepending Sender would say the
        // actor twice, and rebuilding the sentence would mean assuming a battle-log
        // grammar that is nowhere verified - "read, never recompute" applies to text
        // as much as to numbers.
        if (battleLog) return messageText;

        if (IsEmote(kind)) return EmoteText(senderText, messageText);

        return string.IsNullOrWhiteSpace(archiveName)
            ? messageText
            : $"{archiveName}{addressee}: {messageText}";
    }

    /// <summary>The form read out loud.</summary>
    private static string BuildSpokenText(XivChatType kind, bool battleLog, bool isOwn,
        string addressee, string senderText, string messageText)
    {
        if (battleLog) return messageText;

        // Emotes carry their own complete sentence and already name who
        // acted ("You clap for the giant tortoise.", "Y'shtola bows."). Running them
        // through the builder below would produce "You: You clap for..." for the
        // player's own emotes, since Sender equals the player's name and the
        // own-message rewrite would fire.
        if (IsEmote(kind)) return EmoteText(senderText, messageText);

        var prefix = AccessibilityStrings.ChatPrefix(kind);

        // The player's OWN messages are announced as "Du sagst: ..." instead of
        // "Sagt von <eigener Name>: ..." (user request 2026-07-19): without a
        // character echo in the game's input line, this line is the only
        // confirmation that what was typed actually went out, and it has to be
        // instantly distinguishable from someone else talking. For an outgoing
        // tell the recipient follows ("Du flüsterst an X: ...") - never as the
        // speaker, which is what the game's Sender field would have suggested.
        if (isOwn)
            return $"{AccessibilityStrings.OwnChatPrefix(kind)}{addressee}: {messageText}";
        if (string.IsNullOrWhiteSpace(senderText))
            return string.IsNullOrEmpty(prefix) ? messageText : $"{prefix}: {messageText}";
        return AccessibilityStrings.ChatFromLine(prefix, senderText, messageText);
    }

    /// <summary>
    /// Emote wording. StandardEmote is VERIFIED from the log of 2026-08-01:
    /// Sender is the actor and Message is already a complete sentence naming them
    /// ("You clap for the giant tortoise."). CustomEmote produced no line in that
    /// log, so its shape is NOT verified - hence the guard, which only prepends the
    /// actor when the message does not already open with their name. A custom emote
    /// can never arrive anonymous, and it can never be stuttered either.
    /// </summary>
    private static string EmoteText(string sender, string message) =>
        !string.IsNullOrWhiteSpace(sender)
        && !message.StartsWith(sender, StringComparison.CurrentCultureIgnoreCase)
            ? $"{sender} {message}"
            : message;

    /// <summary>
    /// Emote channels. Verified against the Dalamud XivChatType enum
    /// (ilspycmd 2026-08-02): CustomEmote = 28 (<c>/em</c> free text),
    /// StandardEmote = 29 (the built-in gestures).
    /// </summary>
    private static bool IsEmote(XivChatType type) =>
        type is XivChatType.StandardEmote or XivChatType.CustomEmote;

    /// <summary>The two channels that mirror a dialogue
    /// window: 61 echoes <c>Talk</c> when the player advances it, 68 accompanies
    /// <c>_BattleTalk</c>. Every other channel has a single source and is left
    /// alone.</summary>
    private static bool IsNpcDialogue(XivChatType type) =>
        type is XivChatType.NPCDialogue or XivChatType.NPCDialogueAnnouncements;

    // Battle-log base LogKinds: Damage=41, Miss=42, Action=43, Item=44, Healing=45,
    // GainBuff=46, GainDebuff=47, LoseBuff=48, LoseDebuff=49 (Dalamud XivChatType).
    private const int BattleLogMin = 41;
    private const int BattleLogMax = 49;

    /// <summary>
    /// True for battle-log lines.
    ///
    /// THIS IS NO LONGER A MEMBERSHIP DECISION. It used to
    /// divert these lines to a separate service before they reached the chat path at
    /// all; now they travel the ordinary road and the game's own filters decide which
    /// tab shows them. What is left is two things this range genuinely still answers:
    ///  - the line is a complete sentence and must be spoken verbatim, without a
    ///    sender or a channel prefix (see <see cref="BuildSpokenText"/>);
    ///  - it is the highest-volume traffic in the game, so it gets one log line per
    ///    distinct shape rather than one per hit, and it is exempt from the six-second
    ///    toast-echo suppression.
    ///
    /// The mask that used to be here (<c>&amp; 0x7F</c>) is gone: Dalamud splits the
    /// packed LogInfo before the event fires, so <c>LogKind</c> arrives pure
    /// (ilspycmd 2026-08-03, LogInfo).
    /// </summary>
    private static bool IsBattleLogLine(XivChatType type) =>
        (int)type is >= BattleLogMin and <= BattleLogMax;

    /// <summary>
    /// Whether a tab's speech starts switched ON.
    ///
    /// ON for every tab EXCEPT one carrying the battle log,
    /// and that exception is read out of the game's own filter set rather than
    /// guessed from the tab's name. The reason is the one the combat log was built
    /// around: a battle tab is several lines a second mid-rotation, and speaking all
    /// of it buries the enemy-cast warnings and HP thresholds that keep the player
    /// alive. Nothing is LOST by the default - the tab is archived and browsable
    /// either way, and the switch is one row in Umschalt+F9 -&gt; Chat-Register.
    /// </summary>
    private bool DefaultSpeechFor(int tabIndex)
    {
        foreach (var tab in _filters.Tabs)
            if (tab.Index == tabIndex) return !tab.CarriesBattleLog;
        return true;
    }

    /// <summary>
    /// The permanent per-line probe. It reports the SPEECH-relevant decision, which
    /// is now "which tabs show this" - the line that found the NPC-dialogue gap in
    /// the first place, kept for the same reason: an unread channel has to be
    /// identifiable from the log instead of guessed at.
    ///
    /// Battle-log traffic gets one line per distinct (kind, source, target) instead.
    /// It is the only real volume in the chat log, and logging every hit would bury
    /// the one line that matters - while the SHAPES are what any later question about
    /// battle-log wording will need.
    /// </summary>
    private void LogLine(IChatMessage msg, string sender, string text, bool battleLog, ChatFilterState state)
    {
        if (battleLog)
        {
            var shape = ((int)msg.LogKind << 16) | ((int)msg.SourceKind << 8) | (int)msg.TargetKind;
            if (!_loggedShapes.Add(shape)) return;
            _log.Info($"[Chat] Kampflog-Form kind={msg.LogKind} ({(int)msg.LogKind}) "
                      + $"source={msg.SourceKind} target={msg.TargetKind} "
                      + $"register={DescribeTabs(state)} kanal={DescribeChannels(state)} "
                      + $"sender='{sender}' text='{text}'");
            return;
        }

        _log.Info($"[Chat] kind={msg.LogKind} ({(int)msg.LogKind}) sender='{sender}' "
                  + $"register={DescribeTabs(state)} kanal={DescribeChannels(state)} text='{text}'");
    }

    private string DescribeTabs(ChatFilterState state) => state switch
    {
        ChatFilterState.NotReady => "<noch nicht bereit>",
        ChatFilterState.Broken   => "<Filter nicht lesbar>",
        _ => _showingTabs.Count == 0 ? "keins" : string.Join("+", _showingTabs),
    };

    /// <summary>
    /// The channels a line was filed under, by NAME.
    ///
    /// This is the line that will settle the rebuild in game: the old log said which
    /// tabs showed a message, which was never enough to tell "the routing put this in
    /// the wrong buffer" apart from "the player has it switched off there". A name per
    /// channel makes both visible at a glance, and it is the only place the derived
    /// battle-log group names can be checked against what the settings window
    /// calls them.
    /// </summary>
    private string DescribeChannels(ChatFilterState state)
    {
        if (state != ChatFilterState.Ready) return DescribeTabs(state);
        if (_channels.Count == 0) return "keine";

        var names = new List<string>(_channels.Count);
        foreach (var key in _channels)
            names.Add(_filters.Channel(key)?.Name ?? $"?{key}");
        return string.Join("+", names);
    }

    /// <summary>
    /// Says once, out loud, that the game's chat filters could not be read.
    ///
    /// A silent degradation is the one failure mode a blind player cannot detect: the
    /// buffer list would simply look wrong, with no reason given and nothing to
    /// report. This costs one sentence a session and only ever fires in a state that
    /// should not occur.
    /// </summary>
    private void WarnFallbackOnce()
    {
        if (_warnedFallback) return;
        _warnedFallback = true;
        _log.Warning("[Chat] Die Registerkarten des Spiels sind nicht lesbar - der ganze "
                     + "Chat laeuft in einen Puffer, das Kampflog wird nicht gesprochen.");
        _tolk.Speak(AccessibilityStrings.ChatFiltersUnavailable);
    }

    /// <summary>
    /// The other side of a tell (name + home world) from the message's own
    /// PlayerPayload, or null for any other channel. The payload is the game's
    /// own data, so no name parsing and no world guessing is involved.
    /// </summary>
    private TellTarget? ExtractTellPartner(IHandleableChatMessage msg) =>
        ExtractTellPartner(msg.LogKind, msg.Sender, verbose: true);

    /// <summary>
    /// The same, from a bare sender string.
    ///
    /// Split out so the backfill answers tells the same way the
    /// live path does - a recovered whisper has to stay answerable, and a second reading
    /// of the payload list would be a second thing to keep in step.
    /// </summary>
    /// <param name="verbose">Whether to log per line. Off for the backfill: one line per
    /// recovered whisper would bury the pass's own summary in a log the mod rolls on
    /// every rebuild.</param>
    private TellTarget? ExtractTellPartner(XivChatType kind, SeString? sender, bool verbose)
    {
        if (kind is not (XivChatType.TellIncoming or XivChatType.TellOutgoing)) return null;
        if (sender == null) return null;

        foreach (var payload in sender.Payloads)
        {
            if (payload is not PlayerPayload player) continue;
            var world = player.World.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(player.PlayerName) || world.Length == 0) continue;
            if (verbose) _log.Info($"[Chat] Fluester-Partner: '{player.PlayerName}@{world}'");
            return new TellTarget(player.PlayerName, world);
        }

        // No payload: happens for lines the game did not tag (e.g. some system
        // relays). Logged so a missing answer target can be told apart from a bug.
        if (verbose) _log.Info($"[Chat] Fluester ohne Spieler-Payload: sender='{sender.TextValue}'");
        return null;
    }

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
