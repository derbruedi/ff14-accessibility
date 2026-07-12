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

    public ChatReaderService(IChatGui chatGui, TolkService tolk, Configuration config)
    {
        _chatGui = chatGui;
        _tolk = tolk;
        _config = config;

        _chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage msg)
    {
        if (!ShouldRead(msg.LogKind)) return;

        var senderText = msg.Sender?.TextValue ?? string.Empty;
        var messageText = msg.Message?.TextValue ?? string.Empty;

        if (string.IsNullOrWhiteSpace(messageText)) return;

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
