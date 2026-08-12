// The ONE chat setting the mod still owns: per chat tab,
// is it read out loud.
//
// Everything else about chat now belongs to the game - which channels exist, which
// tab shows which of them, what a tab is called and in what order they sit are all
// read from the game's own sheet, config and filter block (GameChatFilters.cs).
// What the game cannot answer is whether a screen reader should SAY a line, because
// the game has no screen reader. So the switches that remain do exactly one thing:
// they decide whether a line is SPOKEN.
//
// THEY NEVER TOUCH A GAME SETTING, and the difference matters because the two switches
// sit side by side in a player's head. User, 2026-08-11: *"we do not want this toggling
// the game setting, that turns off both speech and logging. this is literally about
// speech calls. if the setting is on in the mod menu, that announcement is both logged
// and sent to NVDA for vocalization. if it's off, that announcement is logged only. if
// you uncheck the game setting, it removes that filter from the log."* So:
//
//   the GAME's checkbox  -> whether the line EXISTS for this tab at all. Off means it is
//                           not shown, not archived, not spoken. Only the game's own
//                           settings window changes it; the mod reads those bytes and
//                           never writes one.
//   the switches HERE    -> whether a line that already exists is read out loud. Off
//                           means archived and browsable in silence.
//
// Everything in this file writes `Configuration.ChatTabSpeech` or
// `Configuration.ChatTabChannelSpeech` - the mod's own JSON - and nothing else.
//
// THERE ARE NOW THREE LEVELS OF THEM - a master per tab, one
// per channel inside it, and one per filter ROW inside a channel that covers several.
// An earlier draft had said per-channel switches would rebuild the sixty-odd-entry catalogue this
// rewrite deleted; that was about MEMBERSHIP and this is not. The rows come from
// GameChatFilters.ChannelsInTab and RowsInTab, i.e. whatever the player has switched on
// in the game's own window, in the sheet's order, under the game's own names - switch a
// filter off in the game and the row disappears from here. Nothing in this file decides
// whether a line exists, is archived, or which buffer it lands in. The test to re-apply
// if it ever grows: if a switch here starts deciding any of those three, it has become
// the catalogue again and must be reverted.
//
// IT NEVER SILENCES THE BUFFER. A switched-off tab is still archived in full and
// still browsable with the buffer keys - the distinction this rewrite is about, and
// the reason the old "log disabled channels" row is gone: with membership coming
// from the game there is nothing left for it to decide. A line the player has
// filtered out of every tab is not archived either, and that is not the mod
// discarding it - it is the mod showing what the player configured, the same as a
// sighted player's chat log.
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>Reads and writes the per-tab speech switches in the config.</summary>
public static class ChatTabSpeech
{
    /// <summary>
    /// The index the single FALLBACK buffer is stored under - the one used when the
    /// game's filter state cannot be read at all. No real tab can have a negative
    /// index, so it shares the map without a second config field and without a second
    /// mechanism that could disagree with this one.
    /// </summary>
    public const int FallbackIndex = -1;

    /// <summary>
    /// The index the ONE switch for unfilterable lines is stored
    /// under - the lines the game's own filter list has no checkbox for at all (Notice,
    /// Urgent, Debug, TellIncoming, the GM channels: 21 of the 81 chat kinds).
    ///
    /// It exists so the class is not UNSILENCEABLE, and it defaults to ON because the
    /// player has no game-side switch to turn these back on with. Same negative-index
    /// trick as <see cref="FallbackIndex"/>: no real tab can collide with it.
    /// </summary>
    public const int UnfilteredIndex = -2;

    /// <summary>
    /// Whether this tab is read out loud. Missing key = the caller's default, which
    /// is derived from the tab's own filter set rather than typed in - see
    /// <c>ChatReaderService.DefaultSpeechFor</c>.
    /// </summary>
    public static bool IsOn(Configuration config, int tabIndex, bool fallbackDefault) =>
        config.ChatTabSpeech.TryGetValue(tabIndex, out var stored) ? stored : fallbackDefault;

    /// <summary>Sets a tab's speech switch. The caller saves the config.</summary>
    public static void Set(Configuration config, int tabIndex, bool on) =>
        config.ChatTabSpeech[tabIndex] = on;

    /// <summary>
    /// Whether one CHANNEL inside a tab is read out loud.
    ///
    /// <paramref name="tabDefault"/> is what an untouched channel inherits, and every
    /// caller passes the tab's own master: a channel nobody has configured behaves like
    /// the tab it sits in. That is the rule from [[ff14-chat-drops-npc-duty-dialogue]] -
    /// never allowlist with silence as the default - applied to a list whose membership
    /// the game can change under us at any patch.
    /// </summary>
    public static bool ChannelIsOn(Configuration config, int tabIndex, int channelKey, bool tabDefault)
    {
        if (!config.ChatTabChannelSpeech.TryGetValue(tabIndex, out var channels)) return tabDefault;
        return channels.TryGetValue(channelKey, out var stored) ? stored : tabDefault;
    }

    /// <summary>
    /// Whether one filter ROW inside a tab is read out loud -
    /// the finest of the three levels, and the only one that separates a line's
    /// direction.
    ///
    /// The chain is row, then channel, then the tab's master, each level inheriting the
    /// one above until the player sets it. All three levels are the game's own
    /// structure: its tabs, its filter categories, its checkboxes. Nothing here is a
    /// grouping the mod invented.
    ///
    /// For categories 1 and 2 the row id and the channel key are the SAME NUMBER (one
    /// row is one channel), so the first lookup answers both and the chain collapses to
    /// two levels by itself - no special case needed.
    /// </summary>
    public static bool RowIsOn(Configuration config, int tabIndex, int channelKey, int rowId,
                               bool tabDefault)
    {
        if (config.ChatTabChannelSpeech.TryGetValue(tabIndex, out var stored)
            && stored.TryGetValue(rowId, out var row)) return row;
        return ChannelIsOn(config, tabIndex, channelKey, tabDefault);
    }

    /// <summary>
    /// Sets one channel's speech switch inside a tab, and - when
    /// the tab is not being read aloud and a channel is being switched ON - switches the
    /// tab's master on without letting anything else start speaking.
    ///
    /// THE BULK WRITE IS THE POINT, and it exists because of the case the user described
    /// first: the battle tab starts SILENT (<c>CarriesBattleLog</c> - it is archived in
    /// full either way), and the very first thing they want is one channel of it read
    /// aloud. Without this, switching "damage taken" on would leave the tab's master off
    /// and the row would say "on" while nothing spoke - a setting lying about itself,
    /// which is exactly the failure a switch reporting a DERIVED state runs into.
    ///
    /// Switching the master on alone is not enough either: every untouched channel of
    /// that tab inherits the master, so flipping it would make the whole battle log
    /// speak. So the channels that were silent BY INHERITANCE get their own stored
    /// "not spoken" first, and the rule holds that one press changes exactly one thing
    /// the player HEARS. Nothing about what is logged changes at any point - every one
    /// of these channels is archived before speech is even considered.
    ///
    /// <paramref name="channelsInTab"/> is the tab's live channel list, read from the
    /// game's filter bytes by the caller - never a list kept here.
    /// </summary>
    /// <returns>True when the tab's master was switched on as part of this, so the caller
    /// can report it.</returns>
    public static bool SetChannel(Configuration config, int tabIndex, int channelKey, bool on,
                                  bool tabDefault, IReadOnlyList<int> channelsInTab)
    {
        if (!config.ChatTabChannelSpeech.TryGetValue(tabIndex, out var channels))
            config.ChatTabChannelSpeech[tabIndex] = channels = new Dictionary<int, bool>();

        var unmuted = false;
        if (on && !IsOn(config, tabIndex, tabDefault))
        {
            foreach (var other in channelsInTab)
            {
                if (other == channelKey) continue;
                if (!channels.ContainsKey(other)) channels[other] = false;
            }
            Set(config, tabIndex, true);
            unmuted = true;
        }

        channels[channelKey] = on;
        return unmuted;
    }

    /// <summary>
    /// Sets one ROW's speech switch, opening its channel (and
    /// through it its tab) the same way <see cref="SetChannel"/> opens a tab.
    ///
    /// This is the press the user's own example is made of: the battle tab starts silent,
    /// they open "You" and switch "Damage you are dealt." on, and what they HEAR
    /// afterwards is that one line type and nothing else - while the whole battle log
    /// goes on being archived exactly as before. Getting there takes the same
    /// rule applied twice - pin the silent siblings, then open the level above - which is
    /// why the two methods are shaped alike and why this one calls that one rather than
    /// repeating it.
    /// </summary>
    /// <param name="rowsInChannel">The channel's rows that this tab currently shows, read
    /// from the game's filter bytes by the caller.</param>
    /// <returns>True when a level above this row had to be switched on.</returns>
    public static bool SetRow(Configuration config, int tabIndex, int channelKey, int rowId,
                              bool on, bool tabDefault,
                              IReadOnlyList<int> channelsInTab, IReadOnlyList<int> rowsInChannel)
    {
        // One row IS its channel in categories 1 and 2 - there is no level in between to
        // open, and pinning "siblings" would mean pinning the row itself.
        if (rowId == channelKey)
            return SetChannel(config, tabIndex, channelKey, on, tabDefault, channelsInTab);

        if (!config.ChatTabChannelSpeech.TryGetValue(tabIndex, out var stored))
            config.ChatTabChannelSpeech[tabIndex] = stored = new Dictionary<int, bool>();

        // The gate is "is this row silent right now", not "is its channel off" - a
        // channel switched on inside a tab whose master is off is still silent, and
        // testing only the channel would leave this row inaudible while its switch read
        // "on". Both levels above are therefore tested, and SetChannel below opens
        // whichever of them is actually shut.
        var tabOn = IsOn(config, tabIndex, tabDefault);
        var opened = false;
        if (on && !(tabOn && ChannelIsOn(config, tabIndex, channelKey, tabOn)))
        {
            foreach (var other in rowsInChannel)
            {
                if (other == rowId) continue;
                if (!stored.ContainsKey(other)) stored[other] = false;
            }
            SetChannel(config, tabIndex, channelKey, true, tabDefault, channelsInTab);
            opened = true;
        }

        stored[rowId] = on;
        return opened;
    }

    /// <summary>Forgets a channel's stored switch so it inherits its tab again.
    /// Used by nothing yet - here for the same reason <see cref="Clear"/> is.</summary>
    public static bool ClearChannel(Configuration config, int tabIndex, int channelKey) =>
        config.ChatTabChannelSpeech.TryGetValue(tabIndex, out var channels)
        && channels.Remove(channelKey);

    /// <summary>Forgets a stored value so the tab goes back to its derived default.
    /// Used by nothing yet; here so a future "reset" row cannot be tempted to write
    /// a hardcoded true.</summary>
    public static bool Clear(Configuration config, int tabIndex) =>
        config.ChatTabSpeech.Remove(tabIndex);

    /// <summary>Every stored switch, for the log line that reports the chat setup.</summary>
    public static IReadOnlyDictionary<int, bool> Stored(Configuration config) => config.ChatTabSpeech;
}
