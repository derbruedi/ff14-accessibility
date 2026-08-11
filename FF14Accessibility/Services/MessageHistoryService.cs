using System;
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// The other side of a tell, as the GAME delivered it: player name plus home
/// world. Kept with the archived message so answering never needs the player to
/// type "Name@Welt" - a blind player cannot look that world up anywhere, and
/// guessing it gets the command rejected (user 2026-08-02).
/// </summary>
public sealed record TellTarget(string Name, string World)
{
    /// <summary>The form the game's chat commands expect: "Vorname Nachname@Welt".</summary>
    public string CommandTarget => $"{Name}@{World}";
}

/// <summary>
/// Kategorisierter Nachlese-Speicher: pro Puffer ein Verlauf der zuletzt
/// empfangenen Nachrichten. Der Nutzer wechselt den Puffer (Strg+, / Strg+.) und
/// blättert darin (, / .). Die Live-Ansagen laufen davon unberührt weiter - hier
/// wird nur mitgeschrieben und auf Tastendruck vorgelesen.
///
/// ONE BUFFER PER CHANNEL, AND THE ACTIVE TAB DECIDES WHICH
/// OF THEM ARE OFFERED. Buffers are registered at runtime instead of being an enum.
///
/// The old list was a mod-side categorisation: twenty-odd fixed buckets that
/// <c>ChatReaderService.MapCategory</c> filled by chat type. That was a second source
/// of truth for something the game already models, so it answered differently from what
/// the player had configured, and it grew a new bucket every time somebody noticed a
/// channel landing in the wrong place.
///
/// An earlier draft replaced it with one buffer per TAB, and that over-corrected: a tab is a VIEW
/// over many channels, so a single General buffer held say, shout, tell, party, emotes,
/// free company and sixteen linkshells in one stream. This is the correction the user
/// asked for - *"build the buffers per-channel like the old system did, but only for
/// channels that are toggled on in that tab"*. So:
///
///  - A buffer is a CHANNEL, keyed by the game's own filter row (or, for the battle
///    log, by its actor group). <see cref="GameChatFilters"/> derives the whole
///    list from the game's sheets - the mod authors neither the channels nor their
///    names.
///  - The ACTIVE TAB filters the browse list, through <see cref="BufferOffered"/>.
///    Nothing is moved or dropped when the tab changes: a message lives in its channel
///    whatever is on screen, and the tab only decides what the browse key walks past.
///
/// AND EACH TAB ALSO HAS ITS OWN "ALL" BUFFER - the tab
/// interleaved exactly as the game renders it, which is the view the per-channel split
/// took away. It is offered by that one tab, sorts ahead of that tab's channels, and is
/// where <see cref="EnterTab"/> lands. So the shape the player asked for is: the game's
/// tabs, each readable whole, each subdivided by the game's own switches - the tabs and
/// the subdivision are both the game's, and the only thing the mod adds is the ability
/// to read one strand of a tab at a time. See <see cref="EnsureTabBuffer"/>.
///
/// THREE BUFFERS ARE NOT TABS, and each has a reason:
///
///  - <see cref="DialogueKey"/> - THE EXCEPTION, and it is deliberate. Quest text is
///    advanced with confirm, and the chat log only receives an NPC's line once the
///    player has ADVANCED past it. A dialogue buffer fed from chat would therefore
///    always be one step behind what is on screen, so this one is filled by the
///    <c>Talk</c> / <c>TalkSubtitle</c> / <c>_BattleTalk</c> pop-up readers instead
///    and exists regardless of any chat filter or mod setting.
///  - <see cref="SystemKey"/> - the mod's OWN notices: toasts, the logout countdown,
///    window announcements. These never travelled the chat log at all, so no tab can
///    hold them.
///  - <see cref="FallbackKey"/> - only ever used when the game's filter state cannot
///    be read. See <see cref="GameChatFilters"/>; it is registered lazily so it
///    does not appear in a healthy session.
/// </summary>
public sealed class MessageHistoryService
{
    /// <summary>The pop-up dialogue buffer - see the note on the class. Always present.</summary>
    public const string DialogueKey = "dialogue";

    /// <summary>The mod's own notices. Always present.</summary>
    public const string SystemKey = "system";

    /// <summary>Everything, in one buffer, when the game's chat filters are not
    /// readable. Registered on first use only.</summary>
    public const string FallbackKey = "chat";

    /// <summary>The key of the buffer for one chat CHANNEL - see
    /// <see cref="GameChatChannel.Key"/>. Keyed by the game's own filter row (or
    /// battle-log category), never by name: the name is looked up live so a patch that
    /// rewords a channel does not orphan its history.</summary>
    public static string ChannelKey(int key) => ChannelKeyPrefix + key;

    /// <summary>The key of one chat TAB's "all" buffer, keyed
    /// by the game's own tab index - the same index <c>LogTabFilterN</c> and
    /// <c>AddonChatLog.TabIndex</c> use.</summary>
    public static string TabKey(int index) => TabKeyPrefix + index;

    /// <summary>
    /// What marks a buffer key as a TAB's "all" view, for the
    /// same reason <see cref="ChannelKeyPrefix"/> exists: <see cref="BufferOffered"/>
    /// has to tell the three kinds of key apart without parsing them, and a tab buffer
    /// is offered by exactly ONE tab where a channel buffer is offered by every tab
    /// that has one of its switches on.
    /// </summary>
    public const string TabKeyPrefix = "tab:";

    /// <summary>
    /// What marks a buffer key as a CHANNEL, for the tab filter in
    /// <see cref="BufferOffered"/>.
    ///
    /// The colon is not decoration. The first version of this used <c>"ch"</c>, and
    /// <see cref="FallbackKey"/> is <c>"chat"</c> - so the fallback buffer would have
    /// been taken for a channel, matched against no channel, and become unbrowsable in
    /// exactly the degraded state it exists for. A prefix that cannot occur in the
    /// other three keys is what makes the test safe.
    /// </summary>
    public const string ChannelKeyPrefix = "channel:";

    // Sort keys. Dialogue leads because it is what a player reaches for after a
    // cutscene; the TAB's own "all" view comes next, because it is the tab as the game
    // renders it and the channels under it are a subdivision of it; those channels
    // follow in the game's own order (category, then the settings window's
    // DisplayOrder); the mod's own notices come last because they are the least
    // conversational thing in the list.
    private const int SortDialogue = 0;
    private const int SortTabBase = 500;
    private const int SortChannelBase = 1_000;
    private const int SortFallback = 500_000;
    private const int SortSystem   = 1_000_000;

    /// <summary>
    /// Whether a buffer is offered by the tab the game is
    /// currently showing. Set by <c>Plugin</c>; every buffer is offered until it is.
    ///
    /// A PREDICATE rather than a list of keys, and rather than this class knowing what
    /// a tab is: the answer has to be live, because the player can tick a filter in the
    /// game's own settings window between two presses of the browse key, and a list
    /// handed over earlier would be a cached copy of exactly the state this whole
    /// rewrite exists to stop copying.
    ///
    /// The three non-channel buffers are never filtered by it (see the class note) -
    /// they belong to no tab, so no tab can hide them.
    /// </summary>
    public Func<string, bool>? BufferOffered { get; set; }

    /// <summary>One archived line: the spoken text plus, for tells, who it was
    /// with (null for every other channel).</summary>
    private sealed record Entry(string Text, TellTarget? Partner);

    /// <summary>
    /// One browsable buffer. The name is a FUNCTION, for two independent reasons:
    /// the mod's own buffer names are bilingual and must follow "/acc lang" late,
    /// and a chat tab's name belongs to the player, who can rename it while the mod
    /// is running. Resolving it once at registration would freeze both.
    /// </summary>
    private sealed class Buffer(string key, int sort, Func<string> name)
    {
        public string Key { get; } = key;
        public int Sort { get; } = sort;
        public Func<string> NameSource { get; set; } = name;
        public string Name => NameSource();
        public List<Entry> Entries { get; } = new();

        /// <summary>How many entries of this buffer came from
        /// the game's stored log rather than from a live chat event - i.e. where the
        /// next backfilled line has to be INSERTED so the buffer stays in arrival
        /// order. See <see cref="AddOlder"/>.</summary>
        public int Backfilled { get; set; }
    }

    // No cap: the history keeps every message of the session (user request
    // 2026-08-02, previously 50 per category). A sighted player can scroll the
    // whole chat log back, so cutting the history short took away reach that
    // the game itself grants. Cost is memory only - the entries are short
    // strings, and a session's worth stays in the low megabytes.
    private readonly Dictionary<string, Buffer> _byKey = new();
    private readonly List<Buffer> _order = new();
    private readonly TolkService _tolk;

    private string _currentKey = DialogueKey;
    private int _cursor = -1;   // zuletzt vorgelesene Nachricht, -1 = nicht am Blättern

    public MessageHistoryService(TolkService tolk)
    {
        _tolk = tolk;
        Register(DialogueKey, SortDialogue, () => AccessibilityStrings.BufferDialogue);
        Register(SystemKey, SortSystem, () => AccessibilityStrings.BufferSystem);
    }

    /// <summary>
    /// Makes sure a channel has a buffer, and returns its key.
    ///
    /// Called on the message path rather than up front, so a session only ever carries
    /// buffers for channels that actually spoke. Registering all 69 at startup would
    /// cost nothing in memory but would make every list operation walk sixty-odd
    /// certainly-empty entries for the whole session.
    ///
    /// The channel's own <c>NameSource</c> is handed straight through, so the buffer's
    /// name is resolved from the game's sheet at the moment it is spoken - a rename in
    /// a patch, or a language change through "/acc lang", both land immediately.
    /// </summary>
    public string EnsureChannelBuffer(GameChatChannel channel)
    {
        var key = ChannelKey(channel.Key);
        if (!_byKey.ContainsKey(key))
            Register(key, SortChannelBase + channel.Sort, channel.NameSource);
        return key;
    }

    /// <summary>
    /// Makes sure a chat TAB has its "all" buffer, and returns
    /// its key.
    ///
    /// User, 2026-08-11: *"make sure each tab also has an 'all' buffer that basically
    /// displays the whole tab as a sighted player would see it. then the sub-tab buffers
    /// basically act as category sorting for the blind player, a QOL fix not a strict UI
    /// redesign."*
    ///
    /// WHAT THIS IS, AND WHY IT IS NOT A SECOND SOURCE OF TRUTH. The per-channel buffers
    /// of the per-channel split answer "show me only the loot" - a sort the game does not offer and the
    /// whole reason the split exists. What they cost is the one thing a sighted player
    /// has for free: the tab AS A WHOLE, every channel interleaved in the order the
    /// lines arrived, which is how you read a conversation with the fight going on
    /// around it. This buffer is that view, and it decides nothing: a line is in here
    /// exactly when <c>GameChatFilters.Route</c> named this tab as one that shows it.
    /// Channel buffers and this one are two orderings of the same routing answer, never
    /// two answers.
    ///
    /// Registered on the message path like the channels, so a tab that has never spoken
    /// never appears in the browse list.
    /// </summary>
    public string EnsureTabBuffer(int index)
    {
        var key = TabKey(index);
        if (!_byKey.ContainsKey(key))
            Register(key, SortTabBase + index, () => AccessibilityStrings.BufferTabAll);
        return key;
    }

    /// <summary>Registers the single fallback buffer used when the game's filters
    /// cannot be read, and returns its key.</summary>
    public string EnsureFallbackBuffer()
    {
        if (!_byKey.ContainsKey(FallbackKey))
            Register(FallbackKey, SortFallback, () => AccessibilityStrings.BufferChat);
        return FallbackKey;
    }

    private void Register(string key, int sort, Func<string> name)
    {
        var buffer = new Buffer(key, sort, name);
        _byKey[key] = buffer;

        // Kept sorted on insert, so the browse order is stable no matter when a tab
        // shows up - and the player's current buffer is tracked by KEY, so a tab
        // appearing mid-session cannot slide the selection onto a different one.
        var at = _order.FindIndex(b => b.Sort > sort);
        if (at < 0) _order.Add(buffer); else _order.Insert(at, buffer);
    }

    /// <summary>Adds a message to a buffer (newest last). An unknown key is ignored -
    /// every caller registers its buffer first.</summary>
    /// <param name="partner">For tells, the other side as the game delivered it -
    /// this is what makes answering from the history possible.</param>
    public void Add(string key, string text, TellTarget? partner = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_byKey.TryGetValue(key, out var buffer)) return;

        // Nothing is dropped, so the cursor never has to be pulled along: an
        // entry keeps its index for the whole session.
        buffer.Entries.Add(new Entry(text, partner));
    }

    /// <summary>
    /// Adds a message that the GAME had stored before the mod
    /// loaded - see <see cref="ChatBackfill"/>.
    ///
    /// It INSERTS rather than appends, and that is the whole reason this is a separate
    /// method. The backfill cannot run until the game's log module and filter state are
    /// readable, which is some frames after the plugin loads, so a handful of live lines
    /// are usually already in the buffers by then. Appending would put the older stored
    /// lines AFTER the newer live ones and the buffer would read out of order - which,
    /// for a player who can only hear one line at a time, is worse than the missing
    /// history this fixes.
    ///
    /// <see cref="Buffer.Backfilled"/> is the running insertion point per buffer. The
    /// backfill walks the stored log oldest-first, so each buffer ends up as
    /// [stored, oldest to newest][live, oldest to newest].
    /// </summary>
    public void AddOlder(string key, string text, TellTarget? partner = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_byKey.TryGetValue(key, out var buffer)) return;

        buffer.Entries.Insert(buffer.Backfilled, new Entry(text, partner));
        buffer.Backfilled++;
    }

    /// <summary>
    /// Puts the browse cursor back to "not browsing", so the
    /// next read starts at the newest line again.
    ///
    /// Called once when the backfill finishes: <see cref="AddOlder"/> inserts entries
    /// BEFORE existing ones, so any index the player was standing on now points at a
    /// different line. Everything else about the history survives an insert - buffers
    /// are tracked by key, not by position.
    /// </summary>
    public void ResetBrowseCursor() => _cursor = -1;

    /// <summary>The key of the buffer currently selected for browsing.</summary>
    public string CurrentBufferKey => _currentKey;

    /// <summary>
    /// When the user last worked with the history (switched buffer or read a
    /// message).
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// Cycles to the next/previous buffer and announces it with count.
    ///
    /// EMPTY BUFFERS ARE SKIPPED. User: *"only channels
    /// that have a non-empty buffer should be in the buffers list."* Most buffers are
    /// empty in any given session - arrowing through several silent ones to reach the
    /// one with something in it is not a list, it is an obstacle course.
    ///
    /// AND SO ARE CHANNELS THE ACTIVE TAB DOES NOT SHOW.
    /// That is what makes a per-channel history usable: the game's own tab already
    /// answers "which of these sixty-nine channels am I reading right now", and the
    /// browse list is that answer. Switching tabs therefore changes the LIST and never
    /// the CONTENTS - a message stays in its channel whatever is on screen.
    ///
    /// The cycle walks over what is actually there. If NOTHING is there at all it stays
    /// put and says so, rather than spinning through every buffer to land back where it
    /// started.
    ///
    /// AN EMPTY TAB IS NOT THE SAME AS AN EMPTY HISTORY, and
    /// conflating them was a real defect. User: *"if a tab is completely empty and one
    /// attempts to switch buffers within the tab ... the last buffer from a tab that did
    /// have buffers containing logged chats is still selected instead of just announcing
    /// 'tab is empty.'"* The old fall-through summarised <see cref="Current"/> - a buffer
    /// belonging to a DIFFERENT tab, which this tab does not offer - and left
    /// <c>_currentKey</c> pointing at it, so the next page-up read another tab's messages
    /// while the player believed they were in the tab they had switched to. A blind
    /// player has no way to notice that: the lines are real chat, just from elsewhere.
    /// </summary>
    /// <param name="dir">-1 for the previous buffer, +1 for the next.</param>
    /// <param name="tabIndex">The game's active chat tab, or -1 when the chat log
    /// cannot be read. Only used for the empty case - the browse list itself comes from
    /// <see cref="BufferOffered"/> as before.</param>
    /// <param name="tabName">That tab's own name, for the empty announcement.</param>
    public void SwitchCategory(int dir, int tabIndex = -1, string tabName = "")
    {
        LastActivity = DateTime.UtcNow;
        _cursor = -1;

        // Step at least once, then keep stepping over what this tab does not offer.
        // Bounded by the number of buffers, so a completely empty history cannot loop.
        var index = CurrentIndex();
        for (var step = 0; step < _order.Count; step++)
        {
            index = (index + dir + _order.Count) % _order.Count;
            if (!Browsable(_order[index])) continue;
            _currentKey = _order[index].Key;
            Summarise(_order[index]);
            return;
        }

        // This tab offers nothing, but the history elsewhere
        // does. Say the TAB is empty - which is the true answer and the one the player
        // asked for - and move the cursor into this tab's own "all" buffer, so a page-up
        // from here reads nothing rather than another tab's conversation. The buffer is
        // registered empty if it has never spoken; an empty buffer is never browsable,
        // so it cannot appear in anyone's list.
        if (tabIndex >= 0 && tabName.Length > 0 && AnyEntries())
        {
            _currentKey = EnsureTabBuffer(tabIndex);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryEmpty(tabName));
            return;
        }

        // Nothing anywhere. Report the buffer the player is standing in rather
        // than moving them somewhere equally empty - silence here would be
        // indistinguishable from the key not working.
        Summarise(Current());
    }

    /// <summary>Whether ANY buffer holds anything at all,
    /// ignoring the tab filter. This is what tells "this tab has nothing" apart from
    /// "the history is empty" - two different answers, and only the first is this case.</summary>
    private bool AnyEntries()
    {
        foreach (var buffer in _order)
            if (buffer.Entries.Count > 0) return true;
        return false;
    }

    /// <summary>
    /// Whether the browse keys stop on this buffer: it has
    /// something in it, and the tab the game is showing offers it.
    /// </summary>
    private bool Browsable(Buffer buffer)
    {
        if (buffer.Entries.Count == 0) return false;
        return BufferOffered?.Invoke(buffer.Key) ?? true;
    }

    /// <summary>
    /// Puts the browse cursor on the first buffer this tab
    /// offers, and says what the tab holds. Called after the game's tab has changed, so
    /// the player is never left standing in a buffer the new tab does not show.
    ///
    /// Speaks the TAB, not the buffer, when the tab is empty: "Battle, no messages" is
    /// a complete answer, and moving the player somewhere else to have something to say
    /// would hide which tab they are now on.
    ///
    /// IT LANDS ON THE TAB'S "ALL" VIEW, not on whatever sorts
    /// first. That was a real defect in the first version of this, and an invisible one: the
    /// dialogue buffer sorts ahead of everything and belongs to no tab, so as soon as
    /// one NPC had spoken this method put the player in DIALOGUE on every single tab
    /// switch - it announced the tab it had just left them out of. Landing on the tab
    /// as a whole is also what the player asked the "all" buffer for: switch to a tab,
    /// hear the tab.
    /// </summary>
    /// <param name="index">The game's own tab index, for finding that tab's "all"
    /// buffer. Only the buffer key uses it; everything else follows
    /// <see cref="BufferOffered"/> as before.</param>
    public void EnterTab(int index, string tabName)
    {
        LastActivity = DateTime.UtcNow;
        _cursor = -1;

        var found = 0;
        Buffer? first = null;
        foreach (var buffer in _order)
        {
            if (!Browsable(buffer)) continue;
            found++;
            first ??= buffer;
        }

        // The tab's own "all" view wins the landing spot when it has anything in it.
        // Still counted among `found` by the loop above, because it IS one of the
        // buffers the browse key will stop on.
        if (_byKey.TryGetValue(TabKey(index), out var all) && Browsable(all)) first = all;

        if (first == null)
        {
            // THE SAME DEFECT REACHED THROUGH THE OTHER DOOR.
            // The announcement here was already right, but the cursor was not moved -
            // so switching INTO an empty tab also left _currentKey in the tab the player
            // had just left, and the next page-up read that tab's messages. The tab buffer fixed
            // the case where the new tab has something in it; this is the case where it
            // has nothing.
            _currentKey = EnsureTabBuffer(index);
            _tolk.SpeakInterrupt(AccessibilityStrings.CategoryEmpty(tabName));
            return;
        }

        _currentKey = first.Key;
        _tolk.SpeakInterrupt(AccessibilityStrings.ChatTabEntered(tabName, found, first.Name,
                                                               first.Entries.Count));
    }

    /// <summary>Reads the previous (older) message; first press reads the newest.</summary>
    public void ReadOlder()
    {
        LastActivity = DateTime.UtcNow;
        var buf = Current().Entries;
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1)      _cursor = buf.Count - 1;
        else if (_cursor > 0)   _cursor--;
        else { _tolk.SpeakInterrupt(AccessibilityStrings.HistoryStart); return; }
        Announce(buf);
    }

    /// <summary>Reads the next (newer) message in the current buffer.</summary>
    public void ReadNewer()
    {
        LastActivity = DateTime.UtcNow;
        var buf = Current().Entries;
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1 || _cursor >= buf.Count - 1) { _tolk.SpeakInterrupt(AccessibilityStrings.HistoryEnd); return; }
        _cursor++;
        Announce(buf);
    }

    /// <summary>
    /// Jumps to the OLDEST message in the current buffer
    /// (user request: "shift+home and shift+end to jump to the beginning and end
    /// of the buffers that shift+page up and shift+page down browse").
    ///
    /// The history keeps a whole session per buffer and a battle tab alone can run
    /// to thousands of lines, so reaching either end by repeated paging is not a
    /// realistic option. Same buffer, same cursor and the same spoken form as
    /// <see cref="ReadOlder"/> - only the destination differs, so paging on from
    /// here continues exactly where the jump landed.
    /// </summary>
    public void ReadOldest()
    {
        LastActivity = DateTime.UtcNow;
        var buf = Current().Entries;
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        _cursor = 0;
        Announce(buf);
    }

    /// <summary>
    /// Jumps to the NEWEST message in the current buffer.
    /// See <see cref="ReadOldest"/>.
    ///
    /// Note this is deliberately NOT the same as the cursor's idle state: the
    /// first <see cref="ReadOlder"/> press also reads the newest line, but leaves
    /// the cursor able to walk backwards from it. Landing here does the same, so
    /// the two agree about where "the end" is.
    /// </summary>
    public void ReadNewest()
    {
        LastActivity = DateTime.UtcNow;
        var buf = Current().Entries;
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        _cursor = buf.Count - 1;
        Announce(buf);
    }

    /// <summary>
    /// The tell partner of the message currently in focus, or null when the
    /// buffer holds no tells. While not browsing (cursor at -1) this is the
    /// NEWEST tell, which is what "answer them" means right after switching to
    /// the buffer. Entries without a partner (an older line whose payload the
    /// game did not carry) are skipped rather than reported as "no target".
    /// </summary>
    public TellTarget? CurrentTellPartner
    {
        get
        {
            var buf = Current().Entries;
            if (buf.Count == 0) return null;
            var start = _cursor >= 0 && _cursor < buf.Count ? _cursor : buf.Count - 1;
            for (var i = start; i >= 0; i--)
                if (buf[i].Partner != null) return buf[i].Partner;
            return null;
        }
    }

    private Buffer Current() =>
        _byKey.TryGetValue(_currentKey, out var buffer) ? buffer : _order[0];

    private int CurrentIndex()
    {
        var at = _order.FindIndex(b => b.Key == _currentKey);
        return at < 0 ? 0 : at;
    }

    private void Summarise(Buffer buffer) =>
        _tolk.SpeakInterrupt(AccessibilityStrings.CategorySummary(buffer.Name, buffer.Entries.Count));

    private void AnnounceEmpty()
        => _tolk.SpeakInterrupt(AccessibilityStrings.CategoryEmpty(Current().Name));

    // Through WithPosition, so the settings switch reaches the
    // history too - this is the line the user named ("... placed in your armoury
    // chest. 20 of 20"). With positions off the archived text is spoken bare,
    // separator and all; the ends of the buffer are still announced by
    // HistoryStart/HistoryEnd, so paging does not become directionless.
    private void Announce(List<Entry> buf)
        => _tolk.SpeakInterrupt(AccessibilityStrings.RowWithPosition(buf[_cursor].Text, _cursor + 1, buf.Count));
}
