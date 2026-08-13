using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FF14Accessibility.Services;

/// <summary>
/// Sets the chat send channel, so a message can go into the channel whose
/// history the player was just reading (user wish 2026-08-02: "wenn wir mit
/// unseren Puffern im ausgewählten Kanal sind, dass Enter den Kanal aufmacht,
/// auf dem wir grad stehen").
///
/// A sighted player sees the channel label above the input line before typing;
/// a blind one only hears it when the line opens. This class does the switching
/// BEFORE the line opens, so that existing announcement ("Chat-Eingabe, Gruppe")
/// is the confirmation - no second, competing announcement is needed.
///
/// Uses the game's own <c>RaptureShellModule.ChangeChatChannel</c> (ilspycmd-
/// verified 2026-08-02) rather than writing the ChatType field: the function
/// also updates the window, which a raw field write would not.
///
/// GEHOERT ZUM ALTEN CHATSYSTEM. PR #5 hat diese Klasse geloescht, mit der
/// Begruendung, dass aus Empfangsfiltern kein Sendekanal folgt - das stimmt fuer
/// die neuen Puffer, nimmt dem Spieler aber die in v5.67 bestaetigte
/// Enter-Antwort. Sie ist deshalb hier wieder da und haengt an der alten
/// Nachlese; aufgerufen wird sie nur, solange das alte System eingeschaltet ist
/// (<see cref="Configuration.UseLegacyChatSystem"/>).
/// </summary>
public sealed class ChatChannelService
{
    /// <summary>
    /// History category to the game's numeric chat channel.
    ///
    /// MEASURED, NOT ASSUMED (probe [ChatTypeProbe], log 2026-08-02 17:22-17:23:
    /// the player switched channels by command while the mod paired
    /// RaptureShellModule.ChatType with the displayed label). Only these three
    /// pairs were observed, so only these three are listed - putting a message
    /// into the wrong channel because of a guessed number is the one mistake a
    /// player cannot take back. Extend this table from probe output only.
    /// </summary>
    private static readonly Dictionary<LegacyChatHistoryService.Category, int> ChannelIds = new()
    {
        [LegacyChatHistoryService.Category.Say]         = 1,
        [LegacyChatHistoryService.Category.Party]       = 2,
        [LegacyChatHistoryService.Category.FreeCompany] = 6,
    };

    /// <summary>
    /// How long a history selection stays "fresh" enough to steer the chat key.
    /// Long enough to read a message and start typing, short enough that a
    /// category chosen much earlier never redirects a message unnoticed.
    /// </summary>
    private static readonly TimeSpan SelectionWindow = TimeSpan.FromSeconds(30);

    private readonly LegacyChatHistoryService _history;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public ChatChannelService(LegacyChatHistoryService history, TolkService tolk, IPluginLog log)
    {
        _history = history;
        _tolk    = tolk;
        _log     = log;
    }

    /// <summary>
    /// Switches the send channel to the history category the player is browsing,
    /// when that selection is still fresh. Does nothing (and stays silent) when
    /// the history has not been touched recently or the category is already the
    /// active channel.
    /// </summary>
    public unsafe void TrySwitchToBrowsedChannel()
    {
        if (DateTime.UtcNow - _history.LastActivity > SelectionWindow) return;

        var category = _history.CurrentCategory;

        // Tells are addressed, not just a channel: the partner comes from the
        // archived message's own payload, so the player never has to type
        // "Name@Welt" - the world name is exactly what they cannot look up.
        if (category == LegacyChatHistoryService.Category.Tell)
        {
            TrySwitchToTellPartner();
            return;
        }

        if (!ChannelIds.TryGetValue(category, out var channelId))
        {
            // Categories that are no send channel at all (System, Beute, Dialoge)
            // stay silent - the player is reading, not writing. The ones that ARE
            // a channel but whose number was never measured say so, because
            // silence there would look like the feature is broken.
            if (IsSendableCategory(category))
                _tolk.SpeakInterrupt(AccessibilityStrings.ChannelNotAvailable(
                    AccessibilityStrings.LegacyChatCategoryName(category)));
            return;
        }

        var ui = UIModule.Instance();
        if (ui == null) return;
        var shell = ui->GetRaptureShellModule();
        if (shell == null) return;
        if (shell->ChatType == channelId) return; // already there

        // An EMPTY string rather than null: the target parameter is only used by
        // the tell channels, but handing the game a null pointer is not worth the
        // risk of a crash mid-session.
        var target = Utf8String.CreateEmpty();
        if (target == null) return;
        try
        {
            var ok = shell->ChangeChatChannel(channelId, 0, target, true);
            _log.Info($"[ChatKanal] Nachlese '{category}' -> ChatType={channelId}, Ergebnis={ok}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[ChatKanal] ChangeChatChannel fehlgeschlagen");
        }
        finally
        {
            target->Dtor(true);
        }
    }

    /// <summary>
    /// The tell channel, measured like the others (probe 2026-08-02 17:47:
    /// ChatType=17 while the window read "... zuflüstern" with TellName and
    /// TellWorld filled in).
    /// </summary>
    private const int TellChannelId = 17;

    /// <summary>
    /// Aims the chat at the tell partner of the message being browsed, through
    /// the same <c>ChangeChatChannel</c> that already works for Say and Party -
    /// its target parameter exists for exactly this, the addressed channels.
    ///
    /// NOT <c>SetContextTellTarget</c>: that was tried first and the game
    /// rejected it every time (log 2026-08-02 18:01-18:52, "gesetzt: False").
    /// It wants the player's account and content ids, and a chat message carries
    /// neither - only name and world, which is what the command form uses too.
    ///
    /// The result is REPORTED, not assumed: the call returns a bool, it is logged,
    /// and a false is spoken rather than swallowed - silence here would look like
    /// the target was set and send the next line to whoever was set before.
    /// </summary>
    private unsafe void TrySwitchToTellPartner()
    {
        var partner = _history.CurrentTellPartner;
        if (partner == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.NoTellPartner);
            return;
        }

        var ui = UIModule.Instance();
        if (ui == null) return;
        var shell = ui->GetRaptureShellModule();
        if (shell == null) return;

        var target = Utf8String.FromString(partner.CommandTarget);
        if (target == null) return;
        try
        {
            var ok = shell->ChangeChatChannel(TellChannelId, 0, target, true);
            _log.Info($"[ChatKanal] Fluester-Ziel '{partner.CommandTarget}' gesetzt: {ok}");
            if (!ok) _tolk.SpeakInterrupt(AccessibilityStrings.TellTargetFailed(partner.CommandTarget));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[ChatKanal] ChangeChatChannel (Fluestern) fehlgeschlagen");
            _tolk.SpeakInterrupt(AccessibilityStrings.TellTargetFailed(partner.CommandTarget));
        }
        finally
        {
            target->Dtor(true);
        }
    }

    /// <summary>Whether a category corresponds to a channel one can write in at
    /// all - as opposed to the read-only collections (system, loot, dialogue).</summary>
    private static bool IsSendableCategory(LegacyChatHistoryService.Category category) => category
        is LegacyChatHistoryService.Category.Say
        or LegacyChatHistoryService.Category.Shout
        or LegacyChatHistoryService.Category.Party
        or LegacyChatHistoryService.Category.Alliance
        or LegacyChatHistoryService.Category.Tell
        or LegacyChatHistoryService.Category.FreeCompany;
}
