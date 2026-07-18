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

    public ChatReaderService(IChatGui chatGui, TolkService tolk, Configuration config, MessageHistoryService history)
    {
        _chatGui = chatGui;
        _tolk = tolk;
        _config = config;
        _history = history;

        _chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        // Kampflog-Zeilen werden verworfen (siehe IsCombatLogLine).
        if (IsCombatLogLine(msg.LogKind)) return;

        if (!ShouldRead(msg.LogKind)) return;

        var senderText = msg.Sender?.TextValue ?? string.Empty;
        var messageText = msg.Message?.TextValue ?? string.Empty;

        if (string.IsNullOrWhiteSpace(messageText)) return;

        // Nachlese-Archiv füllen (BEVOR der Echo-Schutz greift, damit auch
        // eine live nicht erneut gesprochene Toast-Dublette im Verlauf steht).
        // Der Kanal-Prefix entfällt hier - die Kategorie trägt ihn schon.
        var archived = string.IsNullOrWhiteSpace(senderText) ? messageText : $"{senderText}: {messageText}";
        _history.Add(MapCategory(msg.LogKind), archived);

        // Many toast notifications (_TextError etc.) the UIReader already spoke
        // are mirrored into the chat log as SystemMessage/ErrorMessage a few
        // seconds later. Skip the echo when the plain message (no prefix) was
        // just spoken (log 2026-07-12: "Du hast einen Auftrag angenommen!" twice).
        if (_tolk.WasRecentlySpoken(messageText, 6)) return;

        var prefix = GetChatPrefix(msg.LogKind);
        var fullText = string.IsNullOrWhiteSpace(senderText)
            ? $"{prefix}: {messageText}"
            : $"{prefix} von {senderText}: {messageText}";

        var interrupt = msg.LogKind is XivChatType.Say or XivChatType.Shout or XivChatType.Party
                                    or XivChatType.Alliance or XivChatType.TellIncoming;

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
        XivChatType.FreeCompany   => MessageHistoryService.Category.FreeCompany,
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
        _                         => "Chat"
    };

    public void Dispose() => _chatGui.ChatMessage -= OnChatMessage;
}
