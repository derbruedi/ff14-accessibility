// REVISED. THE MOD SWITCHES THE GAME'S CHAT TAB.
//
// An earlier draft said the opposite - that the mod tracks AddonChatLog.TabIndex and never moves
// it, because "the mod must not switch the game's tab: that would be sending a command
// for convenience, which is the shape that was removed". The user's correction, 2026-08-10:
// *"if a sighted player clicks them, then there's got to be a way to switch between
// tabs. we want to make sure whatever we do with the mod, that it actually makes the
// game switch tabs."*
//
// That is right, and the comparison was wrong. The two cases are opposites:
//
//   THE REMOVED CASE - Enter switched the SEND channel. The game already has that, on
//   bindable keys the player can press themselves (CMD_SAY, CMD_PARTY,
//   CMD_LINKSHELL_1..8, each with an _ALWAYS variant). The mod was adding a second
//   route to a control that was already reachable. Convenience.
//
//   This - switching the displayed TAB. The game has NO key for it. The full InputId
//   command table was dumped and searched for LOG, TAB and CHAT (640 members,
//   cross-checked against the live 679-entry keybind dump): TAB_NEXT/TAB_PREV are the
//   generic UI-cursor tab cycle and are not proven to reach the chat log at all,
//   CHATLOG_VIEWERMODE is unbound and is not tab switching, and there is no
//   LOG_TAB_NEXT or CHATLOG_TAB_* of any kind, and there is no
//   slash command either. A sighted player switches tabs by CLICKING the tab. So the
//   control exists, the game offers it by mouse only, and a blind player has no way to
//   reach it. Making it reachable is feature parity - the first coding principle in
//   CLAUDE.md - not a second mechanism.
//
// WHAT IS CALLED, and why it is the game's own action rather than an imitation of one.
// AddonChatLog.ChangeTab(int) is a member function of the chat log addon, located by
// signature scan in FFXIVClientStructs (E8 ?? ?? ?? ?? 49 8B CD B2, ilspycmd against
// the Dalamud dev FFXIVClientStructs.dll 2026-08-10). It is the game's own tab-change
// path - the same code the tab button runs - so the game does everything it would
// normally do: the filter set changes with it, the visible lines change with it, and
// LogTabFilterN keeps meaning what it meant. Nothing is reimplemented and no state is
// written by hand.
//
// READ-ONLY IS STILL THE DEFAULT EVERYWHERE ELSE. This file contains the mod's only
// chat write, it happens only on a key the player pressed, and it does exactly what
// their click would have done.
using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace FF14Accessibility.Services;

/// <summary>
/// Reads which chat tab the game is showing, and switches it
/// through the game's own <c>ChangeTab</c>.
/// </summary>
public sealed unsafe class ChatTabControl
{
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;

    /// <summary>The last tab index seen by <see cref="Poll"/>, so a change is reported
    /// once instead of every frame. <c>-1</c> = nothing seen yet.</summary>
    private int _reported = -1;

    /// <summary>Creates the control.</summary>
    /// <param name="gameGui">Used to find the chat log addon.</param>
    /// <param name="log">Mod log.</param>
    public ChatTabControl(IGameGui gameGui, IPluginLog log)
    {
        _gameGui = gameGui;
        _log = log;
    }

    /// <summary>
    /// The tab the game is displaying, or <c>-1</c> when the chat log is not readable.
    ///
    /// <c>AddonChatLog.TabIndex</c> is a byte at offset 684, beside <c>TabCount</c> at
    /// 685 (ilspycmd 2026-08-10
    /// 2026-07-17).
    /// </summary>
    public int ActiveTabIndex
    {
        get
        {
            var addon = Addon();
            return addon == null ? -1 : addon->TabIndex;
        }
    }

    /// <summary>How many tabs the game says it has, or <c>0</c>. Only used for the
    /// bounds check in <see cref="SwitchTo"/> - which tabs EXIST for the buffer list is
    /// answered by <see cref="GameChatFilters.Tabs"/>, out of the tab names, because a
    /// count cannot say which indices are the live ones.</summary>
    public int TabCount
    {
        get
        {
            var addon = Addon();
            return addon == null ? 0 : addon->TabCount;
        }
    }

    /// <summary>
    /// Switches the game to a tab, through the game's own <c>ChangeTab</c>.
    /// Returns false when the chat log is not there or the index is out of range,
    /// so the caller can say so rather than fall silent.
    /// </summary>
    public bool SwitchTo(int tabIndex)
    {
        var addon = Addon();
        if (addon == null)
        {
            _log.Warning("[ChatTab] Kein ChatLog-Addon - Registerkarte nicht umschaltbar.");
            return false;
        }

        // The game's own count is the bound. Calling ChangeTab with an index the
        // window does not have is the one way this could take the client down, and
        // the count sits one byte from the index that would be passed.
        if (tabIndex < 0 || tabIndex >= addon->TabCount)
        {
            _log.Warning($"[ChatTab] Registerkarte {tabIndex} gibt es nicht "
                         + $"(TabCount={addon->TabCount}) - nicht umgeschaltet.");
            return false;
        }

        if (addon->TabIndex == tabIndex) return true;

        // try-catch: ChangeTab is resolved by signature scan, and ClientStructs throws
        // a null-address exception when a patch moves the function rather than
        // returning a failure. A missing signature must degrade to "the key did
        // nothing", never take the plugin down mid-session.
        try
        {
            addon->ChangeTab(tabIndex);
        }
        catch (Exception ex)
        {
            _log.Error($"[ChatTab] ChangeTab({tabIndex}) fehlgeschlagen: {ex.Message}");
            return false;
        }

        _log.Info($"[ChatTab] Auf Registerkarte {tabIndex} umgeschaltet "
                  + $"(vorher {_reported}).");
        _reported = tabIndex;
        return true;
    }

    /// <summary>
    /// Logs every change of the game's own tab index.
    ///
    /// This is the cheap probe the status note asked for, and it answers two open
    /// questions at once from a real session rather than from the binding table: does
    /// anything the GAME does move the tab on its own, and do Numpad9 / Numpad7
    /// (TAB_NEXT / TAB_PREV) reach the chat log. Both are visible in the log as a
    /// change nobody's mod key caused.
    ///
    /// Called once per framework update. One byte read and an integer compare.
    /// </summary>
    public void Poll()
    {
        var addon = Addon();
        if (addon == null) return;

        int index = addon->TabIndex;
        if (index == _reported) return;

        _log.Info($"[ChatTab] Registerkarte des Spiels: {_reported} -> {index} "
                  + $"(TabCount={addon->TabCount}).");
        _reported = index;
    }

    /// <summary>
    /// The chat log addon, or null.
    ///
    /// <c>GetAddonByName</c> is used here deliberately, against the dead-twin rule
    /// recorded for <c>ItemDetail</c>: the chat log is a single permanent window that
    /// the game never tears down and rebuilds, so there is no twin to pick wrongly -
    /// and the mod's chat-input reader has read it this way since v4.90 without the
    /// stale-window symptom that rule exists for.
    /// </summary>
    private AddonChatLog* Addon()
    {
        var ptr = _gameGui.GetAddonByName("ChatLog");
        return ptr.IsNull ? null : (AddonChatLog*)(nint)ptr;
    }
}
