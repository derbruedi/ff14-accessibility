using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
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
        if (IsCombatLogLine(msg.LogKind)) return;

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
            ? $" an {senderText}"
            : string.Empty;

        // Nachlese-Archiv füllen (BEVOR der Echo-Schutz greift, damit auch
        // eine live nicht erneut gesprochene Toast-Dublette im Verlauf steht).
        // Der Kanal-Prefix entfällt hier - die Kategorie trägt ihn schon.
        var archived = string.IsNullOrWhiteSpace(archiveName)
            ? messageText
            : $"{archiveName}{addressee}: {messageText}";
        _history.Add(MapCategory(msg.LogKind), archived);

        // Many toast notifications (_TextError etc.) the UIReader already spoke
        // are mirrored into the chat log as SystemMessage/ErrorMessage a few
        // seconds later. Skip the echo when the plain message (no prefix) was
        // just spoken (log 2026-07-12: "Du hast einen Auftrag angenommen!" twice).
        if (_tolk.WasRecentlySpoken(messageText, 6)) return;

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
            fullText = $"{prefix}: {messageText}";
        else
            fullText = $"{prefix} von {senderText}: {messageText}";

        var interrupt = msg.LogKind is XivChatType.Say or XivChatType.Shout or XivChatType.Party
                                    or XivChatType.Alliance or XivChatType.TellIncoming
                                    or XivChatType.Yell or XivChatType.CrossParty
                                    or XivChatType.TellOutgoing;

        if (interrupt)
            _tolk.SpeakInterrupt(fullText);
        else
            _tolk.Speak(fullText);
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
        _                         => MessageHistoryService.Category.System
    };

    private static string GetChatPrefix(XivChatType type) => type switch
    {
        XivChatType.Say           => "Sagt",
        XivChatType.Shout         => "Ruft",
        XivChatType.Party         => "Gruppe",
        XivChatType.Alliance      => "Allianz",
        XivChatType.TellIncoming  => "Flüstert",
        XivChatType.FreeCompany   => "FC",
        XivChatType.SystemMessage => "System",
        XivChatType.ErrorMessage  => "Fehler",
        XivChatType.TellOutgoing  => "Flüstert an",
        XivChatType.Yell          => "Brüllt",
        XivChatType.CrossParty    => "Gruppe",
        XivChatType.Echo          => "Echo",
        _                         => "Chat"
    };

    /// <summary>Prefix for the player's own messages ("Du sagst: ...").</summary>
    private static string GetOwnChatPrefix(XivChatType type) => type switch
    {
        XivChatType.Say          => "Du sagst",
        XivChatType.Shout        => "Du rufst",
        XivChatType.Yell         => "Du brüllst",
        XivChatType.Party        => "Du zur Gruppe",
        XivChatType.CrossParty   => "Du zur Gruppe",
        XivChatType.Alliance     => "Du zur Allianz",
        XivChatType.FreeCompany  => "Du zur FC",
        XivChatType.TellOutgoing => "Du flüsterst",
        _                        => "Du"
    };

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
