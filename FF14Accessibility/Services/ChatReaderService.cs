using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

public sealed class ChatReaderService : IDisposable
{
    private readonly IChatGui _chatGui;
    private readonly TolkService _tolk;
    private readonly Configuration _config;
    private readonly MessageHistoryService _history;
    private readonly IObjectTable _objectTable;
    private readonly IPluginLog _log;

    public ChatReaderService(IChatGui chatGui, TolkService tolk, Configuration config,
        MessageHistoryService history, IObjectTable objectTable, IPluginLog log)
    {
        _chatGui = chatGui;
        _tolk = tolk;
        _config = config;
        _history = history;
        _objectTable = objectTable;
        _log = log;

        _chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        // Kampflog-Zeilen werden verworfen (siehe IsCombatLogLine).
        if (IsCombatLogLine(msg.LogKind)) { ProbeCombatLine(msg); return; }

        var senderText = msg.Sender?.TextValue ?? string.Empty;
        var messageText = msg.Message?.TextValue ?? string.Empty;

        // Probe: every non-combat line with its raw LogKind, so an unread
        // channel can be identified from the log instead of guessed at. Kept
        // permanently - combat traffic (the only real volume) is already gone.
        _log.Info($"[Chat] kind={msg.LogKind} ({(int)msg.LogKind}) sender='{senderText}' " +
                  $"gelesen={ShouldRead(msg.LogKind)} text='{messageText}'");

        if (!ShouldRead(msg.LogKind)) return;

        if (string.IsNullOrWhiteSpace(messageText)) return;

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

        // Nachlese-Archiv füllen (BEVOR der Echo-Schutz greift, damit auch
        // eine live nicht erneut gesprochene Toast-Dublette im Verlauf steht).
        // Der Kanal-Prefix entfällt hier - die Kategorie trägt ihn schon.
        var archived = string.IsNullOrWhiteSpace(archiveName)
            ? messageText
            : $"{archiveName}{addressee}: {messageText}";
        // The tell PARTNER travels with the message as a payload, including the
        // home world. Keeping it is what lets the player answer from the history
        // later: typing "Name@Welt" by hand needs a world name a blind player has
        // no way to look up, and a guessed one gets rejected (user 2026-08-02).
        // Both directions put the other side in Sender, so both are usable.
        _history.Add(MapCategory(msg.LogKind), archived, ExtractTellPartner(msg));

        // Many toast notifications (_TextError etc.) the UIReader already spoke
        // are mirrored into the chat log as SystemMessage/ErrorMessage a few
        // seconds later. Skip the echo when the plain message (no prefix) was
        // just spoken (log 2026-07-12: "Du hast einen Auftrag angenommen!" twice).
        if (_tolk.WasRecentlySpoken(messageText, 6)) return;

        // NPC speech reaches the player TWICE: the Talk/_BattleTalk window shows
        // it, and the chat log repeats it seconds later (measured 2026-08-10:
        // 2.5 to 5.5 s, and it stretches with how long the box stays up - every
        // line of the Wheiskaet scene was read out twice). Checked against the
        // foreign-source list rather than the general history on purpose: a boss
        // shouting the same warning twice must still be announced twice.
        if (msg.LogKind is XivChatType.NPCDialogue or XivChatType.NPCDialogueAnnouncements &&
            _tolk.WasSpokenElsewhere(messageText, NpcDialogueEchoSeconds))
        {
            _log.Info($"[Chat] NPC-Dialog schon aus dem Fenster gesprochen, nicht wiederholt: '{messageText}'");
            return;
        }

        var prefix = GetChatPrefix(msg.LogKind);

        // The player's OWN messages are announced as "Du sagst: ..." instead of
        // "Sagt von <eigener Name>: ..." (user request 2026-07-19): without a
        // character echo in the game's input line, this line is the only
        // confirmation that what was typed actually went out, and it has to be
        // instantly distinguishable from someone else talking. For an outgoing
        // tell the recipient follows ("Du flüsterst an X: ...") - never as the
        // speaker, which is what the game's Sender field would have suggested.
        string fullText;
        if (isOwn)
            fullText = $"{GetOwnChatPrefix(msg.LogKind)}{addressee}: {messageText}";
        else if (string.IsNullOrWhiteSpace(senderText))
            fullText = string.IsNullOrEmpty(prefix) ? messageText : $"{prefix}: {messageText}";
        else if (string.IsNullOrEmpty(prefix))
            // Named speaker, no channel word (NPC dialogue): "Y'shtola: ..." -
            // ChatFromLine would produce a dangling " von Y'shtola: ...".
            fullText = $"{senderText}: {messageText}";
        else
            fullText = AccessibilityStrings.ChatFromLine(prefix, senderText, messageText);

        var interrupt = msg.LogKind is XivChatType.Say or XivChatType.Shout or XivChatType.Party
                                    or XivChatType.Alliance or XivChatType.TellIncoming
                                    or XivChatType.Yell or XivChatType.CrossParty
                                    or XivChatType.TellOutgoing;

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

    private bool ShouldRead(XivChatType type) => type switch
    {
        XivChatType.Say              => _config.ReadSayChat,
        XivChatType.Shout            => _config.ReadShoutChat,
        XivChatType.Party            => _config.ReadPartyChat,
        XivChatType.Alliance         => _config.ReadAllianceChat,
        XivChatType.TellIncoming     => _config.ReadTellChat,
        XivChatType.FreeCompany      => _config.ReadFCChat,
        XivChatType.SystemMessage    => _config.ReadSystemMessages,
        XivChatType.ErrorMessage     => true,
        // Gathering (67): loot + status while mining/logging ("Du hast X
        // erhalten", "Du bist fertig ..."). Empty sender, so it is announced
        // without a prefix (the message is already a full sentence).
        XivChatType.Gathering        => _config.ReadGatheringMessages,
        // NPCDialogue (61) / NPCDialogueAnnouncements (68), values verified with
        // ilspycmd on Dalamud.dll (2026-08-10): what bosses and quest NPCs say
        // during a fight. Both were missing entirely and fell through to false,
        // so the lines never reached the player (user report 2026-08-10). The
        // _BattleTalk WINDOW was already handled - this is the chat side of the
        // same speech, and the echo guard below keeps it from being said twice.
        XivChatType.NPCDialogue      => _config.ReadNpcDialogue,
        XivChatType.NPCDialogueAnnouncements => _config.ReadNpcDialogue,
        // LootNotice (62): items/currency picked up ("Du hast ein Lammfilet
        // erhalten.", "Du hast 115 Gil erhalten.") - covers enemy drops and
        // everything else that lands in the bag. Verified from a live [Chat] log
        // (2026-07-25). Empty sender, full sentence -> no prefix.
        XivChatType.LootNotice       => _config.AnnounceLoot,
        // Verified via ilspycmd (Dalamud XivChatType, 2026-07-19): these were
        // missing entirely, so the player's own outgoing tells and everything
        // in /yell, cross-world party and /echo was silently dropped - neither
        // spoken nor archived.
        XivChatType.TellOutgoing     => _config.ReadTellChat,
        XivChatType.Yell             => _config.ReadShoutChat,
        XivChatType.CrossParty       => _config.ReadPartyChat,
        XivChatType.Echo             => true,
        _                            => false
    };

    // Battle-log base LogKinds (low 7 bits of the XivChatType value): Damage=41,
    // Miss=42, Action=43, Item=44, Healing=45, GainBuff=46, ... LoseDebuff=49
    // (Dalamud XivChatType enum). Real messages can arrive as combined values
    // with source/target bits set high, so mask to the base before comparing.
    /// <summary>How long after the dialogue window the chat echo of the same line
    /// is still recognised. Generous because the delay is the player's own
    /// reading pace, not a fixed game timer.</summary>
    private const double NpcDialogueEchoSeconds = 120;

    private const int CombatBaseMin = 41;
    private const int CombatBaseMax = 49;

    /// <summary>
    /// True for battle-log lines. These are dropped: the V4.90 attempt to read
    /// action lines ("Du wirkst X.") did not work in-game (user report
    /// 2026-07-18) and was removed again. The check stays so battle-log traffic
    /// is filtered out here explicitly rather than falling through ShouldRead.
    /// </summary>
    private static bool IsCombatLogLine(XivChatType type)
    {
        var baseKind = (int)type & 0x7F;
        return baseKind is >= CombatBaseMin and <= CombatBaseMax;
    }

    private int _combatProbeCount;

    /// <summary>
    /// Debug-only probe for the ONE question V4.91 left open (STATUS 2026-07-18:
    /// the combat reading was rolled back on request instead of debugged, so
    /// "kommen Typ-41-49-Zeilen ueberhaupt an?" was never answered). Logs every
    /// battle-log line that reaches IChatGui.ChatMessage: the RAW LogKind in hex
    /// kind, sender, text and the message's payload types.
    ///
    /// Second pass (2026-08-04): the first measurement answered the "kommen sie
    /// an?" question with a clear yes (104 lines) but ALSO killed the old
    /// assumption that the high bits of the LogKind carry source/target - raw
    /// equalled the base kind in all 104 lines. The replacement comes from
    /// Dalamud itself: IChatMessage exposes SourceKind/TargetKind of type
    /// XivChatRelationKind, whose values include LocalPlayer, PartyMember,
    /// EngagedEnemy ... (ilspycmd on Dalamud.dll, 2026-08-04). Both are logged
    /// here to check they are actually filled for battle-log lines - that is
    /// what decides whether "betrifft mich" can be told from "Umgebung" without
    /// parsing "Du"/"dich" out of a localized sentence.
    /// Delete once the combat history is verified (Debug-Sonden-Konvention).
    /// </summary>
    [Conditional("DEBUG")]
    private void ProbeCombatLine(IHandleableChatMessage msg)
    {
        _combatProbeCount++;
        var payloads = msg.Message == null
            ? string.Empty
            : string.Join("+", msg.Message.Payloads.Select(p => p.Type.ToString()).Distinct());
        _log.Info($"[CombatProbe] #{_combatProbeCount} raw=0x{(int)msg.LogKind:X4} basis={(int)msg.LogKind & 0x7F} " +
                  $"quelle={msg.SourceKind} ziel={msg.TargetKind} " +
                  $"sender='{msg.Sender?.TextValue ?? string.Empty}' payloads='{payloads}' " +
                  $"text='{msg.Message?.TextValue ?? string.Empty}'");
    }

    /// <summary>
    /// The other side of a tell (name + home world) from the message's own
    /// PlayerPayload, or null for any other channel. The payload is the game's
    /// own data, so no name parsing and no world guessing is involved.
    /// </summary>
    private TellTarget? ExtractTellPartner(IHandleableChatMessage msg)
    {
        if (msg.LogKind is not (XivChatType.TellIncoming or XivChatType.TellOutgoing)) return null;
        if (msg.Sender == null) return null;

        foreach (var payload in msg.Sender.Payloads)
        {
            if (payload is not PlayerPayload player) continue;
            var world = player.World.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(player.PlayerName) || world.Length == 0) continue;
            _log.Info($"[Chat] Fluester-Partner: '{player.PlayerName}@{world}'");
            return new TellTarget(player.PlayerName, world);
        }

        // No payload: happens for lines the game did not tag (e.g. some system
        // relays). Logged so a missing answer target can be told apart from a bug.
        _log.Info($"[Chat] Fluester ohne Spieler-Payload: sender='{msg.Sender.TextValue}'");
        return null;
    }

    private static MessageHistoryService.Category MapCategory(XivChatType type) => type switch
    {
        XivChatType.Say           => MessageHistoryService.Category.Say,
        XivChatType.Shout         => MessageHistoryService.Category.Shout,
        XivChatType.Party         => MessageHistoryService.Category.Party,
        XivChatType.Alliance      => MessageHistoryService.Category.Alliance,
        XivChatType.TellIncoming  => MessageHistoryService.Category.Tell,
        XivChatType.TellOutgoing  => MessageHistoryService.Category.Tell,
        XivChatType.FreeCompany   => MessageHistoryService.Category.FreeCompany,
        XivChatType.Yell          => MessageHistoryService.Category.Shout,
        XivChatType.CrossParty    => MessageHistoryService.Category.Party,
        XivChatType.Echo          => MessageHistoryService.Category.Say,
        // Beute-Kanal: eingesammelte Gegenstaende/Waehrung zum Nachlesen
        // (gemeinsam mit den XP-Gewinnen aus CombatService.TrackXpGain).
        XivChatType.LootNotice    => MessageHistoryService.Category.Loot,
        // Was NPCs say goes into the "Dialoge" channel, where the player already
        // looks for conversation - not into "System".
        XivChatType.NPCDialogue   => MessageHistoryService.Category.Dialogue,
        XivChatType.NPCDialogueAnnouncements => MessageHistoryService.Category.Dialogue,
        _                         => MessageHistoryService.Category.System
    };

    // Spoken channel prefixes are bilingual and live in AccessibilityStrings
    // (ChatPrefix / OwnChatPrefix), so "/acc lang" switches them too.
    private static string GetChatPrefix(XivChatType type) => AccessibilityStrings.ChatPrefix(type);

    /// <summary>Prefix for the player's own messages ("You say: ...").</summary>
    private static string GetOwnChatPrefix(XivChatType type) => AccessibilityStrings.OwnChatPrefix(type);

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
