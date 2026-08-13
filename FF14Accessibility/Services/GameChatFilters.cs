// THE GAME'S OWN CHAT SETTINGS, READ RATHER THAN COPIED.
//
// User, 2026-08-10: *"the mod shouldn't receive every message regardless of filter …
// the mod should be able to follow exact game behavior instead of hacking it in … so
// yes, the mod should exactly follow tabs"*, and the general rule this became in
// CLAUDE.md: if the game already tracks a thing, the mod follows and reads it.
//
// WHAT THIS REPLACES. ChatChannels.cs was a mod-side catalogue of sixty-odd
// channels with its own groups, its own defaults and its own switches. It was a second
// source of truth for something the game already models per TAB, so it answered
// differently from what the player had configured and drifted on every patch. The
// evidence that justified the original bug report did not justify the catalogue.
//
// THE FOUR INPUTS, all measured:
//
//  1. The `LogFilter` sheet is the switch list - one row per checkbox in the game's
//     own chat-log settings. Each row names a `LogKind` plus a Caster and a Target
//     BITMASK over EntityRelationKind. The masks are what make the sheet FINER than
//     the chat type: kind 64 is three separate switches (own / party / others'
//     progression), and the split is data, not string matching.
//  2. Dalamud hands the three fields the masks match against straight over -
//     `IChatMessage.LogKind`, `SourceKind`, `TargetKind` - because `ChatGui` reads
//     them out of the game's own `RaptureLogModule.PrintMessage` call.
//  3. `LogFilterConfig + 0x48 + stride*set + rowId` is one byte per (filter set,
//     filter row): 0 off, 1 on, 2 padding. Mapped by measurement 2026-08-10 and
//     verified against 18 states the game's own window reported, plus a blind decode
//     that reconstructed the player's three tabs correctly.
//  4. `LogTabFilterN` says which set tab N uses; `RaptureLogModule.ChatTabs[N].Name`
//     is the tab's own name, and an EMPTY name is how a tab says it does not exist
//     (probe 2026-08-10: tabs 0-2 named General/Battle/Event, tabs 3-4 empty, and
//     `AddonChatLog.TabCount` read 3).
//
// NOTHING HERE IS HARDCODED THAT THE GAME CAN STATE. The 307-byte stride is the
// sheet's id space (highest row id + 1), the slot count is
// `(sizeof(LogFilterConfig) - 0x48) / stride`, and the padding positions are derived
// from the sheet as well - a position is padding when no row has that id, or the row
// there has no name. Square Enix added Cross-world Linkshell 2-8 at ids 300-306 years
// after the rest; deriving the constants is what makes the next such addition
// FOLLOWED instead of mis-read.
//
// READ-ONLY, and it has to stay that way. Nothing in this file writes a game setting
// or calls a game function. The game's settings window is the only route to changing a
// filter - no slash command toggles one - and making that window readable was
// the other half of this work.
using System;
using System.Collections.Generic;
using Dalamud.Game.Config;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>What the mod can currently say about the game's chat filters.
/// "Not ready" and "broken" have to be separate: the first is ordinary and happens
/// every session before the player is in a world, the second is a fault the player
/// must be told about. Treating them alike would either spend a spoken warning on
/// every login or swallow a real one.</summary>
public enum ChatFilterState
{
    /// <summary>The game has not built the log module, the filter config or the tab
    /// names yet. Ordinary, and silent.</summary>
    NotReady,

    /// <summary>The sheet is missing, or the filter block no longer matches the
    /// measured layout. The player is told once, out loud.</summary>
    Broken,

    /// <summary>Tabs and filter state are readable and check out.</summary>
    Ready,
}

/// <summary>
/// WHY a message reached no channel - and the three answers are
/// NOT interchangeable. Two of them mean the mod must still deliver the line.
/// </summary>
public enum ChatCoverage
{
    /// <summary>At least one switch matched and was on. The ordinary case.</summary>
    Routed,

    /// <summary>Switches matched this line and every one of them is OFF in every tab.
    /// This is the player's own decision, made in the game's own settings window, and
    /// it is the ONLY case in which the mod is right to drop a line - a sighted player
    /// would not see it either.</summary>
    SwitchedOff,

    /// <summary>
    /// THE GAME OFFERS NO SWITCH FOR THIS LINE, so the player cannot have switched it
    /// off. Either the <c>LogFilter</c> sheet has no row for the chat kind at all
    /// (measured 2026-08-11: 21 of the 81 kinds Dalamud knows, including Notice,
    /// Urgent, Debug, TellIncoming, CrossParty and the fifteen GM channels), or rows
    /// exist for the kind but none of them accepts this line's caster/target pair.
    ///
    /// A line like this MUST NOT BE DROPPED. It is unfilterable, the game shows it
    /// regardless, and dropping it is the exact failure of
    /// [[ff14-chat-drops-npc-duty-dialogue]] - silence as the default for anything the
    /// switch list does not cover.
    /// </summary>
    Unfilterable,
}

/// <summary>
/// ONE BROWSABLE CHANNEL = ONE BUFFER.
///
/// An earlier draft made a buffer per TAB, and that was the wrong reading of "follow the game".
/// A tab is a VIEW over many channels, so one buffer per tab crams say, shout, tell,
/// party, emotes, free company, eight linkshells and eight cross-world linkshells into
/// a single undifferentiated stream. User, 2026-08-10: *"we should follow tab the game
/// is on and build the buffers per-channel like the old system did, but only for
/// channels that are toggled on in that tab."*
///
/// WHAT A CHANNEL IS, and why this is still not a mod-side catalogue. The
/// <c>LogFilter</c> sheet's 234 switches divide by its own <c>Category</c> column:
/// <list type="bullet">
/// <item>Category 1 - 27 rows of player chat. One channel per row.</item>
/// <item>Category 2 - 31 rows of system and notice traffic. One channel per row.</item>
/// <item>Categories 4-14 - 176 rows of battle log, and they are NOT 176 channels.
///   The sheet groups them eleven ways, one category per ACTOR, with the same 16 line
///   types inside each. One channel per category (user decision 2026-08-10, after
///   being shown both shapes).</item>
/// </list>
///
/// EVERY NAME IS THE GAME'S OWN WORD, and the battle ones took a lookup rather than a
/// guess. A row names itself (<c>LogFilter.Name</c>). A battle CATEGORY does not, so
/// its name comes from the settings window's own label block in the <c>Addon</c>
/// sheet - rows 1227-1232, 1276-1279 and 1283, sitting beside "Log Filters" (1209),
/// the window's three inner tabs "Chat"/"Battle"/"Announcements" (1212-1214) and its
/// three preset buttons (1225/1226/1275). Which label belongs to which category is
/// DERIVED, not paired by name: every row of a battle category carries exactly one
/// single-bit relation in its Caster or Target mask, and that one bit is the actor.
/// Verified offline over all eleven categories 2026-08-10 (the sheets themselves):
/// 4=LocalPlayer, 5=PartyMember, 6=AllianceMember, 7=OtherPlayer, 8=EngagedEnemy,
/// 9=UnengagedEnemy, 10=FriendlyNpc, 11=Pet, 12=PetParty, 13=PetAlliance,
/// 14=PetOther - one bit each, no category ambiguous.
///
/// So nothing here is authored: not the list, not the grouping, not a single name.
/// Add a channel to the game and it appears; rename one in a patch and the mod
/// renames with it; play in German and it is German, because both sheets are.
/// </summary>
/// <param name="Key">Stable identity for the buffer. A <c>LogFilter</c> row id for
/// categories 1 and 2; MINUS the category number for the battle log, which cannot
/// collide because row ids are never negative.</param>
/// <param name="Category">The sheet's own <c>Category</c>, kept so the browse order
/// can follow the sheet instead of an order invented here.</param>
/// <param name="Sort">Category first, then the sheet's <c>DisplayOrder</c> inside it -
/// i.e. the order the game's own settings window lists these switches in.</param>
public sealed record GameChatChannel(int Key, int Category, int Sort)
{
    /// <summary>The channel's name in the game's own words and the game's own
    /// language. A FUNCTION rather than a string: it is read out of a sheet, and
    /// resolving it once at construction would freeze it against a language
    /// change - the same reason <c>MessageHistoryService.Buffer</c> holds one.</summary>
    public required Func<string> NameSource { get; init; }

    /// <summary>See <see cref="NameSource"/>.</summary>
    public string Name => NameSource();

    /// <summary>Whether this channel is one of the eleven battle-log actor groups.
    /// Used for nothing but logging - membership and speech are both decided by the
    /// game's data, never by this flag.</summary>
    public bool IsBattle => Key < 0;
}

/// <summary>One chat tab as the game has it right now.</summary>
/// <param name="Index">The tab's index, 0-based, in the game's own order.</param>
/// <param name="Name">The tab's own name ("General", "Battle", a name the player
/// typed). Never empty - an empty name is how the game says the tab is not there.</param>
/// <param name="SetIndex">Which filter set the tab uses, from <c>LogTabFilterN</c>.</param>
/// <param name="CarriesBattleLog">Whether any battle-log switch is on in this tab's
/// filter set. Read out of the game's own data, not guessed from the tab's name -
/// the player's "Battle" tab is only called that because they named it. It decides
/// one thing: whether the tab's SPEECH starts switched on, because a battle tab is
/// several lines a second mid-rotation.</param>
public sealed record GameChatTab(int Index, string Name, int SetIndex, bool CarriesBattleLog);

/// <summary>
/// Answers one question: which of the game's chat tabs would
/// show this message. Everything it answers with comes out of the game's own sheet,
/// config and filter block - see the file header.
/// </summary>
public sealed unsafe class GameChatFilters
{
    /// <summary>Where the per-set filter bytes start inside <c>LogFilterConfig</c>.
    /// The bytes before it are the <c>UserFileEvent</c> header, whose file name
    /// "LOGFLTR.DAT" is legible at 0x30 and is what anchors this.</summary>
    private const int FilterBlockOffset = 0x48;

    /// <summary>How often the tab list, the per-tab set indices and the integrity
    /// check are re-read once everything is readable. The filter BYTES are not cached
    /// at all (see <see cref="TabsShowing"/>), so this only bounds the two reads that
    /// go through Dalamud's config service and the log module.</summary>
    private const long TabRefreshMs = 1000;

    /// <summary>The same, while the state is NOT ready. Shorter, because the whole
    /// point is to notice the moment the game finishes building its log module -
    /// chat arrives in a burst at login and a full second of "not ready" would send
    /// that burst to the fallback buffer. Still a throttle and not zero: in a broken
    /// state this is a 4 x 307 byte scan, and doing it per chat line would be a real
    /// cost exactly when something is already wrong.</summary>
    private const long RetryRefreshMs = 250;

    /// <summary>
    /// One way a message reaches the player: THIS switch of the
    /// game's own filter list is what got it into THAT tab, and the switch belongs to
    /// THAT channel.
    ///
    /// All three matter, and none of them can be derived from the others afterwards:
    ///
    ///  - Tab crossed with channel is a BIGGER set than the routes that exist. A line
    ///    can match rows in several channels and a tab may have only one of them on, so
    ///    deciding speech off the cross product would let a channel the player silenced
    ///    here be spoken because a different tab shows the same line by another switch.
    ///  - The ROW is the game's finest unit and the only one that separates a line's
    ///    DIRECTION. Measured 2026-08-11 (the sheets themselves): battle category 4 is
    ///    the "You" group, and it holds both "Damage dealt by you." (row 51,
    ///    caster=LocalPlayer) and "Damage you are dealt." (row 58, target=LocalPlayer) -
    ///    the eleven actor groups split by WHOSE line it is, never by which way it went.
    ///    An earlier draft assumed the opposite and was wrong; the user's own example (speak damage
    ///    taken, archive damage dealt) is expressible at the row and nowhere else.
    ///
    /// For the 58 rows of categories 1 and 2 the row IS the channel, so the two keys are
    /// the same number and the distinction costs nothing there.
    /// </summary>
    /// <param name="Tab">The tab's own index, as <see cref="GameChatTab.Index"/>.</param>
    /// <param name="Channel">The channel's key, as <see cref="GameChatChannel.Key"/>.</param>
    /// <param name="Row">The <c>LogFilter</c> row id - one checkbox of the game's own
    /// chat-log settings window.</param>
    public readonly record struct ChatRoute(int Tab, int Channel, int Row);

    /// <summary>One filter row, reduced to what matching needs.</summary>
    /// <param name="ChannelKey">Which browsable channel this row feeds - see
    /// <see cref="GameChatChannel.Key"/>. Computed once at construction so routing a
    /// message never has to look a category up again.</param>
    private readonly record struct FilterRow(int Id, ushort Caster, ushort Target, int ChannelKey);

    /// <summary>
    /// Which <c>Addon</c> row names each battle-log actor.
    /// Indexed by the <c>EntityRelationKind</c> BIT that the category's rows carry.
    ///
    /// This is a mapping between two of the game's own tables, not a name list: every
    /// string still comes out of the Addon sheet, in the player's language. It was read
    /// off the settings window's own label block (see the class note on
    /// <see cref="GameChatChannel"/>) rather than paired by matching words, because
    /// matching "Damage dealt by party members." against "Party Member" is exactly the
    /// string surgery this whole rewrite exists to remove.
    ///
    /// Index 0 is unused - relation kind 0 is <c>None</c> and no category carries it.
    /// </summary>
    private static readonly uint[] ActorNameRowByRelationBit =
    [
        0,      // 0  None            - not an actor
        1227,   // 1  LocalPlayer     - "You"
        1228,   // 2  PartyMember     - "Party Member"
        1229,   // 3  AllianceMember  - "Alliance Member"
        1230,   // 4  OtherPlayer     - "Other PC"
        1231,   // 5  EngagedEnemy    - "Engaged Enemy"
        1232,   // 6  UnengagedEnemy  - "Unengaged Enemy"
        1283,   // 7  FriendlyNpc     - "Friendly NPCs"
        1276,   // 8  Pet             - "Pets/Companions"
        1277,   // 9  PetParty        - "Pets/Companions (Party)"
        1278,   // 10 PetAlliance     - "Pets/Companions (Alliance)"
        1279,   // 11 PetOther        - "Pets/Companions (Other PC)"
    ];

    private readonly IDataManager _data;
    private readonly IGameConfig _gameConfig;
    private readonly IPluginLog _log;

    // ── Derived from the sheet once, at construction ──────────────────────
    private readonly Dictionary<byte, List<FilterRow>> _rowsByKind = new();
    private readonly bool[] _isPadding = [];
    private readonly int _stride;
    private readonly bool _sheetReady;

    /// <summary>Every browsable channel, in the sheet's own order. Built once - the
    /// LIST of switches the game offers is static data; only their on/off state is
    /// live, and that is read per message and never cached.</summary>
    private readonly List<GameChatChannel> _channels = new();

    /// <summary>Channel key -> channel, for the reverse lookup the buffer list needs.</summary>
    private readonly Dictionary<int, GameChatChannel> _channelByKey = new();

    /// <summary>Every filter row that feeds a channel, so "is this channel on in that
    /// tab" is one pass over a short list instead of a scan of all 307 positions.</summary>
    private readonly Dictionary<int, List<int>> _rowsByChannel = new();

    // ── Re-read from the live game, throttled ─────────────────────────────
    private readonly List<GameChatTab> _tabs = new();

    /// <summary>
    /// When the tab list was last re-read, and whether it EVER has been.
    ///
    /// THE FLAG IS NOT REDUNDANT - it is the whole fix. This
    /// field was <c>long.MinValue</c> as a "never read" sentinel, and the throttle below
    /// compares <c>Environment.TickCount64 - _tabsReadAt</c> against an interval. That
    /// subtraction OVERFLOWS: any positive tick count minus <c>long.MinValue</c> exceeds
    /// <c>long.MaxValue</c> and wraps to about -9.2e18, which is less than every
    /// interval - so the throttle returned on its first line, every single call, forever,
    /// and the body of <see cref="RefreshTabs"/> never ran once in a live session. C#
    /// integer arithmetic is unchecked by default, so it wrapped silently rather than
    /// throwing. for the four symptoms this one line produced.
    /// </summary>
    private long _tabsReadAt;
    private bool _tabsEverRead;

    private ChatFilterState _state = ChatFilterState.NotReady;
    private bool _reportedBroken;

    /// <summary>The layout last written to the log, so a tab that is renamed, moved
    /// to another filter set, added or removed is reported again instead of the log
    /// still describing the setup from login.</summary>
    private string _reportedLayout = string.Empty;

    /// <summary>Shapes already reported as shown by no tab, so the log carries one
    /// line each rather than one per message.</summary>
    private readonly HashSet<int> _unshown = new();

    /// <summary>
    /// Builds the sheet-derived tables. The live state is read lazily, because the
    /// log module and the filter config do not exist until the player is in a world.
    /// </summary>
    public GameChatFilters(IDataManager data, IGameConfig gameConfig, IPluginLog log)
    {
        _data = data;
        _gameConfig = gameConfig;
        _log = log;

        var sheet = data.GetExcelSheet<LogFilter>();
        if (sheet == null || sheet.Count == 0)
        {
            // No sheet, no filter list, and nothing may be invented in its place.
            // Available stays false and the caller falls back - loudly.
            _log.Error("[ChatFilter] LogFilter-Blatt nicht lesbar - die Registerkarten "
                       + "des Spiels koennen nicht gefolgt werden.");
            return;
        }

        // The stride is the sheet's ID SPACE, not its row count: 278 rows with ids
        // running 0..306 and gaps in between, and 306 + 1 = 307. Reading it off the
        // highest id is what makes an added channel follow instead of shifting every
        // row after it by one.
        var highest = 0;
        var present = new HashSet<int>();
        foreach (var row in sheet)
        {
            var id = (int)row.RowId;
            present.Add(id);
            if (id > highest) highest = id;
        }
        _stride = highest + 1;

        _isPadding = new bool[_stride];
        var switches = 0;
        for (var id = 0; id < _stride; id++)
        {
            // A position is padding when no row has that id, or the row there has
            // CATEGORY 0. Category is what puts a row in a group of the settings
            // window (1 player chat, 2 system and notices, 4-14 the battle log), so
            // category 0 means "in no group" - not a switch the window offers.
            //
            // MEASURED, and the obvious rule is WRONG. "The row has no name" gives
            // 72 positions; the game uses 73. The odd one out is row 0, which is
            // named "None" and is padding all the same - a name check would have let
            // one real position through, the integrity test below would have failed
            // on every set, and the whole feature would have fallen back for a reason
            // nobody could see. Category 0 gives exactly the 44 rows (row 0 plus the
            // 43 nameless ones) that, with the 29 absent ids, make up the 73.
            // Verified against the player's own saved LOGFLTR.DAT 2026-08-10: with
            // this rule the file decodes at a clean stride with three sets and no
            // stray values; with the name rule nothing decodes at all.
            if (!present.Contains(id) || !sheet.TryGetRow((uint)id, out var row) || row.Category == 0)
            {
                _isPadding[id] = true;
                continue;
            }

            switches++;

            // Which BUFFER this row feeds. Everything about
            // the answer comes out of the row itself - its category and, for the
            // battle log, the single relation bit its masks carry.
            var channelKey = row.Category is 1 or 2 ? id : -row.Category;
            RegisterChannel(channelKey, row.Category, row.DisplayOrder, id);

            var kind = row.LogKind;
            if (!_rowsByKind.TryGetValue(kind, out var list))
                _rowsByKind[kind] = list = new List<FilterRow>();
            list.Add(new FilterRow(id, row.Caster, row.Target, channelKey));
        }

        // The sheet's own order: category first, then DisplayOrder inside it. That is
        // the order the game's own settings window lists these switches in, which is
        // the only order a player has ever been shown them in.
        _channels.Sort(static (a, b) => a.Sort.CompareTo(b.Sort));

        _sheetReady = true;
        _log.Info($"[ChatFilter] LogFilter: {sheet.Count} Zeilen, Id-Raum {_stride}, "
                  + $"{switches} Schalter, {_stride - switches} Fuellstellen, "
                  + $"{_rowsByKind.Count} verschiedene LogKinds, "
                  + $"{_channels.Count} Kanaele.");
    }

    /// <summary>
    /// Files one filter row under its channel, creating the
    /// channel the first time a row asks for it.
    ///
    /// The NAME is deliberately deferred to a lambda rather than resolved here: both
    /// sheets are read in the client's language, and "/acc lang" can move the mod's
    /// own language mid-session. A name captured at construction would be the language
    /// the plugin loaded in, forever.
    /// </summary>
    private void RegisterChannel(int key, byte category, byte displayOrder, int rowId)
    {
        if (!_rowsByChannel.TryGetValue(key, out var rows))
            _rowsByChannel[key] = rows = new List<int>();
        rows.Add(rowId);

        if (_channelByKey.ContainsKey(key)) return;

        Func<string> name;
        int sort;

        if (key >= 0)
        {
            // Categories 1 and 2: the row IS the channel and it names itself.
            var id = (uint)rowId;
            name = () => _data.GetExcelSheet<LogFilter>().TryGetRow(id, out var r)
                ? r.Name.ExtractText().Trim()
                : $"#{id}";
            sort = category * 1000 + displayOrder;
        }
        else
        {
            // The battle log: eleven actor groups, named from the settings window's
            // own label block. The actor BIT is what ties a category to its label,
            // and it is derived from the sheet rather than matched on words.
            var addonRow = ActorNameRow(category);

            if (addonRow == 0)
            {
                // A battle category whose actor could not be derived. NOT given an
                // invented name: the category number is spoken instead, which is ugly
                // and therefore reportable - a plausible English label would hide the
                // fact that the mapping stopped working.
                _log.Warning($"[ChatFilter] Kampflog-Kategorie {category}: kein "
                             + "eindeutiges Akteur-Bit - der Kanal bleibt unbenannt.");
                name = () => $"Kategorie {-key}";
            }
            else
            {
                name = () => _data.GetExcelSheet<Lumina.Excel.Sheets.Addon>()
                                  .TryGetRow(addonRow, out var a)
                    ? a.Text.ExtractText().Trim()
                    : $"Kategorie {-key}";
            }

            // DisplayOrder is per row inside the category and the category collapses
            // to one channel here, so the category alone decides where it sits.
            sort = category * 1000;
        }

        var channel = new GameChatChannel(key, category, sort) { NameSource = name };
        _channelByKey[key] = channel;
        _channels.Add(channel);
    }

    /// <summary>
    /// Which <c>Addon</c> row names a battle-log category,
    /// or 0 when the category does not resolve to exactly one actor.
    ///
    /// Every row of such a category carries the actor in one of its two masks and
    /// <c>0xFFFF</c> ("any") in the other: "Damage dealt by you" is caster=LocalPlayer
    /// target=any, "Damage you are dealt" is caster=any target=LocalPlayer. So the
    /// actor is simply the one single-bit mask the whole category shares. Measured over
    /// all eleven categories 2026-08-10 (the sheets themselves) - each yields exactly
    /// one bit, none is ambiguous.
    ///
    /// Returns 0 rather than picking when a category yields none or several. A wrong
    /// actor here would file a fight's damage under another player's name, which is
    /// worse than an unnamed channel and far harder to notice.
    /// </summary>
    private uint ActorNameRow(byte category)
    {
        var sheet = _data.GetExcelSheet<LogFilter>();
        var found = 0;

        foreach (var row in sheet)
        {
            if (row.Category != category) continue;
            if (!SingleBit(row.Caster, ref found)) return 0;
            if (!SingleBit(row.Target, ref found)) return 0;
        }

        return found > 0 && found < ActorNameRowByRelationBit.Length
            ? ActorNameRowByRelationBit[found]
            : 0u;

        // A mask that names exactly one relation kind either agrees with what the
        // category has already claimed, or makes the category ambiguous. "any" and
        // "none" say nothing about the actor and are skipped.
        static bool SingleBit(ushort mask, ref int found)
        {
            if (mask == 0 || mask == 0xFFFF) return true;
            if ((mask & (mask - 1)) != 0) return true;   // several bits: not an actor
            var bit = System.Numerics.BitOperations.TrailingZeroCount(mask);
            if (found == 0) { found = bit; return true; }
            return found == bit;
        }
    }

    /// <summary>Whether the game's own filter state can be followed right now.
    /// See <see cref="State"/> for why "no" has two meanings.</summary>
    public bool Available => State == ChatFilterState.Ready;

    /// <summary>Whether the filters are readable, not yet built, or broken.</summary>
    public ChatFilterState State
    {
        get { RefreshTabs(); return _state; }
    }

    /// <summary>The tabs the game has right now, in the game's own order. Empty while
    /// <see cref="Available"/> is false.</summary>
    public IReadOnlyList<GameChatTab> Tabs
    {
        get { RefreshTabs(); return _tabs; }
    }

    /// <summary>Every browsable channel the game offers, in the sheet's own order.
    /// The LIST is static data; which of them a tab shows is live and comes from
    /// <see cref="ChannelsInTab"/>.</summary>
    public IReadOnlyList<GameChatChannel> Channels => _channels;

    /// <summary>The channel behind a buffer key, or null if the sheet does not have
    /// one. Null is a real answer for a buffer that outlived a patch.</summary>
    public GameChatChannel? Channel(int key) =>
        _channelByKey.TryGetValue(key, out var channel) ? channel : null;

    /// <summary>
    /// Der Kanal, den die Filterzeilen EINER Chat-Art speisen, oder null, wenn
    /// das Sheet fuer diese Art keine Zeile fuehrt.
    ///
    /// Gebraucht wird das fuer die Gegenrichtung einer Unterhaltung: eingehendes
    /// Fluestern hat keinen eigenen Schalter, ausgehendes schon, und beide
    /// gehoeren in denselben Puffer (siehe
    /// <c>ChatReaderService.SameConversationAs</c>). Der Schluessel wird HIER
    /// nachgeschlagen und nirgends aufgeschrieben - eine feste Zahl im Code
    /// waere genau die Art Annahme, die ein Patch still umlegt.
    ///
    /// Fuehren mehrere Zeilen einer Art auf verschiedene Kanaele, kommt der
    /// erste zurueck; fuer die adressierten Kanaele gibt es je Art nur eine.
    /// </summary>
    public GameChatChannel? ChannelOfKind(XivChatType kind)
    {
        var raw = (int)kind;
        if (raw is < 0 or > byte.MaxValue) return null;
        if (!_rowsByKind.TryGetValue((byte)raw, out var rows) || rows.Count == 0) return null;
        return Channel(rows[0].ChannelKey);
    }

    /// <summary>
    /// Where this message belongs and who would say it.
    ///
    /// Both answers come from the same row match, which is the point: a channel is
    /// "the switch the game would have ticked for this line", and a tab shows the line
    /// exactly when it has one of those switches on. Nothing here decides membership -
    /// it reports what the player's own filter data already says.
    ///
    /// <paramref name="channels"/> receives one key per CHANNEL the message lands in
    /// (see <see cref="GameChatChannel"/>), and <paramref name="tabs"/> one index per
    /// TAB that would display it. A message can land in several of each: a battle line
    /// dealt by a party member to you matches rows in two actor categories, and a tab
    /// per filter set can show both.
    ///
    /// AN EMPTY PAIR WITH <see cref="ChatFilterState.Ready"/> IS A REAL ANSWER, not a
    /// failure: the player has filtered this line out of every tab, so a sighted player
    /// would not see it either. It is logged once per distinct shape all the same,
    /// because a routing gap and a deliberate filter look identical from the outside
    /// and only one of them is a bug.
    ///
    /// The filter bytes are read LIVE here rather than cached. A message matches only a
    /// handful of rows, so this is a couple of dozen byte reads - and it means the mod
    /// can never be a cache-refresh behind a switch the player just flipped in the
    /// game's own settings window.
    /// </summary>
    /// <param name="routes">Receives one entry per (tab,
    /// channel, row) route the line actually travels - see <see cref="ChatRoute"/>. It
    /// is what the speech switches are read against; the two flat lists stay because
    /// archiving wants channels and the log line wants tabs, and deriving either from
    /// the routes at every call site would be the same work done twice.</param>
    /// <param name="coverage">WHY the channel list came back
    /// empty, when it does. The caller MUST consult it: only
    /// <see cref="ChatCoverage.SwitchedOff"/> means the line may be dropped.</param>
    public ChatFilterState Route(XivChatType kind, XivChatRelationKind source,
                                 XivChatRelationKind target,
                                 List<int> channels, List<int> tabs, List<ChatRoute> routes,
                                 out ChatCoverage coverage)
    {
        channels.Clear();
        tabs.Clear();
        routes.Clear();
        coverage = ChatCoverage.Unfilterable;
        var state = State;
        if (state != ChatFilterState.Ready) return state;

        var config = LogFilterConfig.Instance();
        if (config == null) return ChatFilterState.NotReady;

        var raw = (int)kind;
        if (raw is < 0 or > byte.MaxValue)
        {
            // The LogKind Dalamud hands over is the pure 7-bit field: ChatGui reads
            // logInfo.LogKind, .SourceKind and .TargetKind as three separate fields
            // out of the game's own PrintMessage call (ilspycmd, and game-api.md on
            // LogInfo). A value outside a byte would mean that changed, which is a
            // finding in itself and not something to paper over with a mask.
            NoteUnshown(raw, source, target, "LogKind ausserhalb des Bytebereichs");
            return ChatFilterState.Broken;
        }

        if (!_rowsByKind.TryGetValue((byte)raw, out var rows))
        {
            // The sheet has no switch for this chat kind at all,
            // so the player cannot have switched it off. Coverage stays Unfilterable and
            // the caller delivers the line - see ChatCoverage.
            NoteUnshown(raw, source, target, "kein LogFilter-Schalter fuer diese Art");
            return ChatFilterState.Ready;
        }

        var block = (byte*)config + FilterBlockOffset;
        var srcBit = (int)source;
        var tgtBit = (int)target;

        // Whether any row ACCEPTED this line, regardless of its
        // on/off state. It is what tells "the player switched this off" apart from "the
        // game has no switch for this shape", and the two used to be reported as one.
        var matched = false;

        foreach (var row in rows)
        {
            if ((row.Caster >> srcBit & 1) == 0) continue;
            if ((row.Target >> tgtBit & 1) == 0) continue;
            matched = true;

            // The row MATCHES this message. Whether it is switched on decides both
            // answers below - and a row that is off in every tab contributes to
            // neither, which is how a channel the player switched off stays out of
            // the history as well as out of the speech.
            var onSomewhere = false;
            foreach (var tab in _tabs)
            {
                if (block[tab.SetIndex * _stride + row.Id] != 1) continue;
                onSomewhere = true;
                if (!tabs.Contains(tab.Index)) tabs.Add(tab.Index);

                // The route itself: this row is on in this tab,
                // and the row belongs to this channel. Recorded here because this is the
                // one place where all three are known at once - anywhere later the
                // pairing would have to be reconstructed, and could only be guessed.
                var route = new ChatRoute(tab.Index, row.ChannelKey, row.Id);
                if (!routes.Contains(route)) routes.Add(route);
            }

            if (onSomewhere && !channels.Contains(row.ChannelKey)) channels.Add(row.ChannelKey);
        }

        // Tab order, so a caller walking the list walks it the way the game lays the
        // tabs out. Channels keep the order the rows were matched in, which the
        // browse list re-sorts by the sheet anyway.
        tabs.Sort();

        if (channels.Count > 0)
        {
            coverage = ChatCoverage.Routed;
            return ChatFilterState.Ready;
        }

        // Empty, and the reason decides whether the line lives.
        // A row matched and every one of them is off = the player's own choice, and the
        // only case the mod may drop. No row matched this caster/target pair = the game
        // offers no switch for this shape, and dropping it would be inventing a filter
        // the player has no way to see, let alone turn back on.
        coverage = matched ? ChatCoverage.SwitchedOff : ChatCoverage.Unfilterable;
        NoteUnshown(raw, source, target, matched
            ? $"in keiner Registerkarte an [{DescribeRows(raw, source, target)}]"
            : "kein Schalter passt zu diesem Sender/Ziel");
        return ChatFilterState.Ready;
    }

    /// <summary>
    /// The channels a tab has switched ON, in the sheet's
    /// order. This is what a tab IS - a filter over the one message store - so it is
    /// also exactly what the buffer list offers while that tab is the active one.
    ///
    /// Read live, like everything else here: the player can tick a box in the game's
    /// own window at any moment and the very next press of the buffer key must agree
    /// with what they just did.
    /// </summary>
    public ChatFilterState ChannelsInTab(int tabIndex, List<int> into)
    {
        into.Clear();
        var state = State;
        if (state != ChatFilterState.Ready) return state;

        var config = LogFilterConfig.Instance();
        if (config == null) return ChatFilterState.NotReady;

        var set = -1;
        foreach (var tab in _tabs)
            if (tab.Index == tabIndex) { set = tab.SetIndex; break; }

        // A tab index the game does not have. Not an error the player can act on -
        // it happens for one frame while a tab is being created - so the caller gets
        // an empty list and decides what to say about it.
        if (set < 0) return ChatFilterState.NotReady;

        var block = (byte*)config + FilterBlockOffset;
        foreach (var channel in _channels)
        {
            if (!_rowsByChannel.TryGetValue(channel.Key, out var rows)) continue;
            foreach (var rowId in rows)
            {
                if (block[set * _stride + rowId] != 1) continue;
                into.Add(channel.Key);
                break;
            }
        }

        return ChatFilterState.Ready;
    }

    /// <summary>
    /// The ROWS of one channel that a tab has switched on, in
    /// the sheet's own order - i.e. the checkboxes the game's own settings window shows
    /// ticked for that group, and nothing else.
    ///
    /// A channel of category 1 or 2 has exactly one row and it is the channel itself;
    /// a battle-log group has up to sixteen, which is where this matters - the row is
    /// the only place the game distinguishes a line's direction (see
    /// <see cref="ChatRoute"/>).
    ///
    /// Read live, for the same reason <see cref="ChannelsInTab"/> is.
    /// </summary>
    public ChatFilterState RowsInTab(int tabIndex, int channelKey, List<int> into)
    {
        into.Clear();
        var state = State;
        if (state != ChatFilterState.Ready) return state;

        var config = LogFilterConfig.Instance();
        if (config == null) return ChatFilterState.NotReady;

        var set = -1;
        foreach (var tab in _tabs)
            if (tab.Index == tabIndex) { set = tab.SetIndex; break; }
        if (set < 0) return ChatFilterState.NotReady;

        if (!_rowsByChannel.TryGetValue(channelKey, out var rows)) return ChatFilterState.Ready;

        var block = (byte*)config + FilterBlockOffset;
        foreach (var rowId in rows)
            if (block[set * _stride + rowId] == 1) into.Add(rowId);

        return ChatFilterState.Ready;
    }

    /// <summary>
    /// One filter row's own label, in the game's own words and
    /// the player's own language ("Damage you are dealt.").
    ///
    /// Resolved on demand rather than cached, exactly as the channel names are: the
    /// sheet is read in the client's language and "/acc lang" can move the mod's mid
    /// session. Falls back to the row NUMBER, never to an invented sentence - an ugly
    /// row is reportable, a plausible wrong one is not.
    /// </summary>
    public string RowName(int rowId)
    {
        if (rowId < 0) return $"#{rowId}";
        var sheet = _data.GetExcelSheet<LogFilter>();
        return sheet.TryGetRow((uint)rowId, out var row)
            ? row.Name.ExtractText().Trim()
            : $"#{rowId}";
    }

    /// <summary>
    /// Re-reads which tabs exist, which filter set each uses, and whether the block
    /// still looks like the layout that was measured. Throttled - see
    /// <see cref="TabRefreshMs"/>.
    /// </summary>
    private void RefreshTabs()
    {
        // No sheet means no filter list, and that never recovers.
        if (!_sheetReady) { _state = ChatFilterState.Broken; return; }

        var now = Environment.TickCount64;
        var interval = _state == ChatFilterState.Ready ? TabRefreshMs : RetryRefreshMs;
        // The "never read yet" case is a FLAG, not a sentinel
        // value fed through the subtraction - see the field.
        if (_tabsEverRead && now - _tabsReadAt < interval) return;
        _tabsEverRead = true;
        _tabsReadAt = now;

        _tabs.Clear();
        _state = ChatFilterState.NotReady;

        var config = LogFilterConfig.Instance();
        var module = RaptureLogModule.Instance();
        if (config == null || module == null) return;

        // Both from the game's own declarations. If a patch grows the struct or the
        // sheet, the slot count follows instead of the mod reading past the end.
        var slots = (sizeof(LogFilterConfig) - FilterBlockOffset) / _stride;
        if (slots <= 0) { _state = ChatFilterState.Broken; return; }

        for (var index = 0; index < module->ChatTabs.Length; index++)
        {
            var name = TolkService.Sanitize(module->ChatTabs[index].Name.ToString()).Trim();
            // An empty name IS the game saying this tab does not exist - measured
            // against AddonChatLog.TabCount, which read 3 while tabs 3 and 4 were
            // nameless. Not a skipped read and not a default to invent one for.
            if (name.Length == 0) continue;

            if (!TryGetTabFilterOption(index, out var option))
            {
                // A named tab the config has no LogTabFilter option for. That is a
                // real gap - it means the game grew a tab slot this mapping does not
                // cover - so it is reported rather than guessed at.
                _log.Warning($"[ChatFilter] Registerkarte {index} '{name}' hat keine "
                             + "LogTabFilter-Option - sie wird nicht gefolgt.");
                continue;
            }

            uint set;
            try
            {
                if (!_gameConfig.TryGet(option, out set)) continue;
            }
            catch (Exception ex)
            {
                // The config sections fill asynchronously and TryGet throws before
                // the game is ready. Not an error, just "not yet".
                _log.Debug($"[ChatFilter] {option} noch nicht lesbar: {ex.Message}");
                return;
            }

            if (set >= slots)
            {
                _log.Warning($"[ChatFilter] Registerkarte {index} '{name}' zeigt auf "
                             + $"Filtersatz {set}, es gibt nur {slots} - uebersprungen.");
                continue;
            }

            _tabs.Add(new GameChatTab(index, name, (int)set, CarriesBattleLog(config, (int)set)));
        }

        // No named tab yet - the tab names come from the character's own config and
        // are empty until the player is in a world. Ordinary, not a fault.
        if (_tabs.Count == 0) return;

        _state = CheckIntegrity(config) ? ChatFilterState.Ready : ChatFilterState.Broken;
        ReportStateOnce();
    }

    /// <summary>
    /// Verifies the layout before anything is routed by it. Every padding position of
    /// every set a tab uses must read 2 - that is a free self-check on the layout,
    /// and it is the difference between noticing that a patch moved the block and
    /// announcing garbage from it.
    ///
    /// A set whose every byte is zero passes as well. That is an unused set (the
    /// player's fourth tab decoded exactly that way), and zeroes are a legitimate
    /// "nothing is on here", not a moved layout.
    /// </summary>
    private bool CheckIntegrity(LogFilterConfig* config)
    {
        var block = (byte*)config + FilterBlockOffset;
        var ok = true;

        foreach (var set in DistinctSets())
        {
            var bad = 0;
            var nonZero = 0;
            for (var id = 0; id < _stride; id++)
            {
                var value = block[set * _stride + id];
                if (value != 0) nonZero++;
                if (_isPadding[id] && value != 2) bad++;
            }

            if (nonZero == 0) continue;      // an unused set, all zero - fine
            if (bad == 0) continue;

            // Once per ENTRY into the broken state, not once per check: the check
            // re-runs every quarter second while broken, and an error line per chat
            // message would bury the rest of the log at the moment it is needed most.
            if (_reportedBroken) { ok = false; continue; }
            _log.Error($"[ChatFilter] Filtersatz {set}: {bad} von {PaddingCount()} "
                       + "Fuellstellen tragen nicht den Wert 2. Das Layout von "
                       + "LogFilterConfig stimmt nicht mehr - die Registerkarten des "
                       + "Spiels werden NICHT gefolgt.");
            ok = false;
        }

        _reportedBroken = !ok;
        return ok;
    }

    /// <summary>Battle-log LogKinds: Damage(41) through LoseDebuff(49), the Dalamud
    /// XivChatType block. The one range this file knows, and it is not used to route
    /// anything - see <see cref="GameChatTab.CarriesBattleLog"/>.</summary>
    private const int BattleLogMin = 41;
    private const int BattleLogMax = 49;

    /// <summary>Whether any battle-log switch is on in a filter set.</summary>
    private bool CarriesBattleLog(LogFilterConfig* config, int set)
    {
        var block = (byte*)config + FilterBlockOffset;
        for (var kind = BattleLogMin; kind <= BattleLogMax; kind++)
        {
            if (!_rowsByKind.TryGetValue((byte)kind, out var rows)) continue;
            foreach (var row in rows)
                if (block[set * _stride + row.Id] == 1) return true;
        }
        return false;
    }

    private IEnumerable<int> DistinctSets()
    {
        var seen = new HashSet<int>();
        foreach (var tab in _tabs)
            if (seen.Add(tab.SetIndex)) yield return tab.SetIndex;
    }

    private int PaddingCount()
    {
        var count = 0;
        foreach (var padding in _isPadding) if (padding) count++;
        return count;
    }

    /// <summary>
    /// Writes the decoded tab layout to the log when it first resolves, and again
    /// whenever it changes. This is the offline decode reproduced live, and it is the
    /// only way to confirm in game that the mapping still holds.
    ///
    /// DO NOT CHECK IT AGAINST A REMEMBERED SWITCH COUNT.
    /// An earlier version of this comment named one ("the event tab has a single NPC
    /// Dialogue switch"), and the player changed their filters at 07:17 that morning -
    /// the Event tab went from 1 switch on to 14, and NPC Dialogue was not among them.
    /// A count is the player's live setting, not a property of the mapping, so treating
    /// it as an expectation turns an ordinary settings change into a false alarm.
    ///
    /// The mapping is confirmed a different way, and one that cannot go stale: decode the
    /// player's own saved LOGFLTR.DAT with `an offline decode of that file` and check that its
    /// per-set counts equal the ones logged here. Two independent readings of the same
    /// state - different file, different anchor, different stride. They agreed exactly on
    /// 2026-08-11 (28 / 98 / 14).
    ///
    /// The switch COUNT is deliberately not part of the change signature - it moves
    /// every time the player ticks a box in the game's own settings window, and a log
    /// line per tick is noise. Names, indices and set assignments are the layout.
    /// </summary>
    private void ReportStateOnce()
    {
        if (_state != ChatFilterState.Ready) return;

        var layout = string.Join("|", _tabs);
        if (layout == _reportedLayout) return;
        _reportedLayout = layout;

        var config = LogFilterConfig.Instance();
        if (config == null) return;
        var block = (byte*)config + FilterBlockOffset;

        foreach (var tab in _tabs)
        {
            var on = 0;
            for (var id = 0; id < _stride; id++)
                if (block[tab.SetIndex * _stride + id] == 1) on++;
            _log.Info($"[ChatFilter] Registerkarte {tab.Index} '{tab.Name}' -> "
                      + $"Filtersatz {tab.SetIndex}, {on} Schalter an, "
                      + $"Kampflog={(tab.CarriesBattleLog ? "ja" : "nein")}.");
        }
    }

    /// <summary>Which <c>LogTabFilterN</c> option belongs to a tab index. Named
    /// options rather than arithmetic on the enum, because the numeric values are
    /// Dalamud's and nothing promises they stay adjacent.</summary>
    private static bool TryGetTabFilterOption(int index, out UiConfigOption option)
    {
        switch (index)
        {
            case 0: option = UiConfigOption.LogTabFilter0; return true;
            case 1: option = UiConfigOption.LogTabFilter1; return true;
            case 2: option = UiConfigOption.LogTabFilter2; return true;
            case 3: option = UiConfigOption.LogTabFilter3; return true;
            default: option = default; return false;
        }
    }

    /// <summary>
    /// Reports a message shape that no tab shows, once per distinct
    /// (kind, source, target). Most of these are the player's own filter working as
    /// they set it up; the value is that a ROUTING gap looks exactly the same from
    /// outside and would otherwise be silent - which is the one failure mode a blind
    /// player cannot detect. Cheap: one line per shape per session.
    /// </summary>
    /// <summary>
    /// Every switch that accepts this line, by name, with the
    /// byte each filter set currently holds for it.
    ///
    /// This is the line that makes "the player switched it off" CHECKABLE instead of
    /// inferred. The old log said only "in keiner Registerkarte an", which is equally
    /// consistent with a filter the player turned off and with the mod reading the wrong
    /// byte - and those need opposite responses. Now the reading itself is in the log:
    /// row 26 'NPC Dialogue' set0=0 set1=0 set2=1 either agrees with the game's own
    /// window or it does not, and either way the next question is obvious.
    ///
    /// Runs once per distinct message shape, from <see cref="NoteUnshown"/>, so the
    /// re-walk costs nothing on the message path.
    /// </summary>
    private string DescribeRows(int kind, XivChatRelationKind source, XivChatRelationKind target)
    {
        if (!_rowsByKind.TryGetValue((byte)kind, out var rows)) return "keine Zeilen";

        var config = LogFilterConfig.Instance();
        if (config == null) return "Filterblock nicht lesbar";
        var block = (byte*)config + FilterBlockOffset;

        var srcBit = (int)source;
        var tgtBit = (int)target;
        var parts = new List<string>();

        foreach (var row in rows)
        {
            if ((row.Caster >> srcBit & 1) == 0) continue;
            if ((row.Target >> tgtBit & 1) == 0) continue;

            var values = new List<string>();
            foreach (var tab in _tabs)
                values.Add($"Satz{tab.SetIndex}={block[tab.SetIndex * _stride + row.Id]}");

            var name = _data.GetExcelSheet<LogFilter>().TryGetRow((uint)row.Id, out var r)
                ? r.Name.ExtractText().Trim()
                : "?";
            parts.Add($"Zeile {row.Id} '{name}' {string.Join(" ", values)}");
        }

        return parts.Count == 0 ? "keine passende Zeile" : string.Join("; ", parts);
    }

    private void NoteUnshown(int kind, XivChatRelationKind source,
                             XivChatRelationKind target, string why)
    {
        var shape = (kind << 16) | ((int)source << 8) | (int)target;
        if (!_unshown.Add(shape)) return;
        _log.Info($"[ChatFilter] kind={kind} source={source} target={target} "
                  + $"wird von keiner Registerkarte gezeigt ({why}).");
    }
}
