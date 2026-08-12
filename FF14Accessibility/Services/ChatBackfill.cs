// THE CHAT HISTORY SURVIVES A PLUGIN RELOAD, because the
// GAME still has it.
//
// User, 2026-08-10: *"the advantage of obeying the game's chat buffers and reading them
// directly is we should be able to populate the buffers with whatever chat history the
// game has available if the mod gets redeployed. old behavior was chat history was all
// lost if the mod restarted."* Confirmed still missing in game 2026-08-11: five
// Debug builds inside one hour, and every one of them started the buffers empty while
// the game's own chat log still had the lines on screen.
//
// WHAT THE FIRST ATTEMPT AT THIS GOT WRONG, AND THE CORRECTION. It was recorded, from
// *"There is no ClientStructs accessor for reading a stored message back. PrintMessage,
// FormatLogMessage, ShowLogMessage*, SetTabName and GetTabName are all the member
// functions there are, and every one of them WRITES."* That is not true of the
// FFXIVClientStructs this branch builds against (re-checked 2026-08-11, ilspycmd on
// $DALAMUD_HOME/FFXIVClientStructs.dll):
//
//     bool GetLogMessage(int index, out byte[] message)
//     bool GetLogMessageDetail(int index, out byte[] sender, out byte[] message,
//                              out ushort logKind, out EntityRelationKind sourceKind,
//                              out EntityRelationKind targetKind, out int timestamp)
//
// The second one hands back EXACTLY the four things routing needs - LogKind, SourceKind,
// TargetKind and the two strings - which are the same four fields Dalamud splits out of
// the game's own PrintMessage call for a live line. So the probe planned for it is unnecessary:
// there is no byte blob to decode, no record layout to guess at, and nothing to measure.
// The game reads its own stored records for us. LogMessageIndex and LogMessageData are
// left alone entirely; guessing an offset into an unmapped blob was always going to be a
// crash rather than a bug, and now nobody has to.
//
// THE ONE ARITHMETIC THAT IS NOT THE GAME'S. Live messages arrive from the moment the
// plugin loads, and the game stores those same messages in the same log - so the newest
// N records are ones the live hook has already archived. N is ChatReaderService's own
// count of events it has received, and the backfill stops there. This assumes one stored
// record per chat event, which is what the shared origin implies (Dalamud hooks
// PrintMessage, the function that writes the record) but is not separately measured; the
// pass logs both numbers so a disagreement shows up as a duplicated or missing tail
// rather than as silence.
//
// READ-ONLY. GetLogMessageDetail fills two Utf8Strings the caller owns and returns a
// bool. Nothing here writes a game setting, calls a write function, or touches the
// player's log.
using System;
using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace FF14Accessibility.Services;

/// <summary>
/// Replays the chat the game had stored before the mod loaded
/// into the mod's buffers, once, silently.
/// </summary>
public sealed unsafe class ChatBackfill
{
    private readonly ChatReaderService _chat;
    private readonly MessageHistoryService _history;
    private readonly GameChatFilters _filters;
    private readonly IPluginLog _log;

    /// <summary>Whether the one pass has been made (or abandoned). Either way this is
    /// over for the session - a second pass would re-insert what the first one already
    /// recovered, and there is no honest way to tell those apart afterwards.</summary>
    private bool _done;

    /// <summary>Creates the backfill.</summary>
    /// <param name="chat">The chat reader, for its archive path and its live count.</param>
    /// <param name="history">The buffers, for the cursor reset after inserting.</param>
    /// <param name="filters">The game's tabs and filter rows - the pass waits until
    /// these read Ready, because a line routed by an unreadable filter state would land
    /// in the fallback buffer instead of where the player's own tabs put it.</param>
    /// <param name="log">Mod log.</param>
    public ChatBackfill(ChatReaderService chat, MessageHistoryService history,
                        GameChatFilters filters, IPluginLog log)
    {
        _chat = chat;
        _history = history;
        _filters = filters;
        _log = log;
    }

    /// <summary>
    /// Runs the pass as soon as the game's log module and filter state are both readable.
    /// Called once per framework update; two null checks and a throttled state read until
    /// the moment it fires.
    /// </summary>
    public void Update()
    {
        if (_done) return;

        var module = RaptureLogModule.Instance();
        if (module == null) return;

        // NotReady is the ordinary case at login - the tab names come out of the
        // character's own config and are empty until the player is in a world. Waiting
        // costs nothing; routing without them would file everything in one buffer.
        if (!_filters.Available) return;

        _done = true;
        Run(module);
    }

    /// <summary>
    /// One synchronous pass over the stored records, oldest first.
    ///
    /// IN ONE FRAME, DELIBERATELY. Spreading it over several would let live chat arrive
    /// in the middle of it, and a line inserted between two backfilled ones would put the
    /// buffer out of order - which, for someone reading one line at a time, is worse than
    /// the hitch. The elapsed time is logged so the cost is a measured number rather than
    /// an assumption; if it ever turns out to be large, the fix is a smarter insert, not
    /// a cap on how much of the player's own history they get back.
    /// </summary>
    private void Run(RaptureLogModule* module)
    {
        var stored = module->LogMessageCount;
        var live = _chat.LiveMessagesSeen;
        var boundary = stored - live;

        if (boundary <= 0)
        {
            _log.Info($"[ChatBackfill] Nichts nachzutragen: {stored} gespeicherte Zeilen, "
                      + $"{live} davon schon live empfangen.");
            return;
        }

        var watch = Stopwatch.StartNew();
        var read = 0;
        var archived = 0;

        // try-catch: GetLogMessageDetail is resolved by signature scan, and ClientStructs
        // throws a null-address exception when a patch moves the function rather than
        // returning a failure. A missing signature must cost the history, never the
        // session - and it is reported, because a silently empty history is exactly the
        // symptom this whole section exists to remove.
        try
        {
            for (var index = 0; index < boundary; index++)
            {
                if (!module->GetLogMessageDetail(index, out var sender, out var message,
                                                 out var logKind, out var sourceKind,
                                                 out var targetKind, out _))
                {
                    // The game says it has no record there. It knows how many it has, so
                    // this is the end of what is readable rather than a hole to skip.
                    _log.Info($"[ChatBackfill] Eintrag {index} nicht lesbar - Ende der "
                              + $"gespeicherten Zeilen (erwartet waren {boundary}).");
                    break;
                }

                read++;

                // Both enums are byte-valued and member-for-member identical - None,
                // LocalPlayer, PartyMember, AllianceMember, OtherPlayer, EngagedEnemy,
                // UnengagedEnemy, FriendlyNpc, then the four pet kinds (ilspycmd on both
                // assemblies, 2026-08-11). Dalamud's own chat path casts the same way.
                if (_chat.ArchiveStored((XivChatType)logKind,
                                        (XivChatRelationKind)(byte)sourceKind,
                                        (XivChatRelationKind)(byte)targetKind,
                                        Parse(sender), Parse(message)))
                    archived++;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"[ChatBackfill] Abgebrochen nach {read} Zeilen: {ex.Message}");
        }

        watch.Stop();

        // Deliberately not announced out loud. The recovered lines show up as the counts
        // the buffer keys already speak, and a sentence at login about a thing that has
        // simply gone right is noise attached to nothing the player did.
        _log.Info($"[ChatBackfill] {archived} von {read} gespeicherten Zeilen in die Puffer "
                  + $"uebernommen ({watch.ElapsedMilliseconds} ms). Das Spiel hatte {stored}, "
                  + $"{live} davon kamen schon live an.");

        // The inserts moved every entry the player might have been standing on.
        _history.ResetBrowseCursor();
    }

    /// <summary>
    /// One stored string as an SeString.
    ///
    /// The bytes are the game's own SeString bytes - the same encoding Dalamud parses for
    /// a live line - so the payloads survive, which is what keeps a recovered whisper
    /// answerable (its <c>PlayerPayload</c> carries the home world). An empty array is a
    /// line with no sender, which is ordinary for system messages.
    /// </summary>
    private static SeString? Parse(byte[] bytes) =>
        bytes.Length == 0 ? null : SeString.Parse(bytes);
}
