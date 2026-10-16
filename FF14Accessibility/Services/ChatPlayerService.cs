using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Opens the native player context menu (invite, friend, …) for the sender of
/// the chat-history line in focus, and hard-targets them so Numpad3 can walk.
///
/// <para>
/// CLEAN PATH (ClientStructs, verified in FFXIVClientStructs AgentHUD):
/// <c>AgentHUD.OpenContextMenuFromTarget(GameObject*)</c> — the same call the
/// game uses when the HUD opens a menu for a live object. No menu-item text
/// matching; the game builds the entries.
/// </para>
///
/// <para>
/// SEARCH ORDER: (1) <c>ObjectTable</c> PCs in the current zone (exact name,
/// optional home-world match), (2) HUD party list via
/// <c>OpenContextMenuFromPartyAddon</c> when the member is listed there,
/// (3) Friend / Free-Company / Cross-World linkshell info proxies by
/// <c>GetEntryByName</c> — those can report a <em>zone id</em> when the player
/// is not loaded. Without a live <c>GameObject</c> the context menu cannot be
/// opened this way; the mod announces the zone honestly instead of inventing
/// a menu.
/// </para>
/// </summary>
public sealed class ChatPlayerService
{
    private readonly IObjectTable _objects;
    private readonly IDataManager _data;
    private readonly IGameGui _gameGui;
    private readonly NavigationService _navigation;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    /// <summary>Builds the chat-sender act-on service.</summary>
    public ChatPlayerService(
        IObjectTable objects,
        IDataManager data,
        IGameGui gameGui,
        NavigationService navigation,
        TolkService tolk,
        IPluginLog log)
    {
        _objects = objects;
        _data = data;
        _gameGui = gameGui;
        _navigation = navigation;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>
    /// Resolve <paramref name="player"/>, open their context menu when a live
    /// object (or party HUD slot) exists, and set the hard target for Numpad3.
    /// </summary>
    public unsafe void ActOn(TellTarget? player)
    {
        if (player == null || string.IsNullOrWhiteSpace(player.Name))
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatPlayerNone);
            return;
        }

        var live = FindInObjectTable(player);
        if (live != null && live.Address != nint.Zero)
        {
            var hud = AgentHUD.Instance();
            if (hud == null)
            {
                _tolk.SpeakInterrupt(AccessibilityStrings.ChatPlayerNotFound(player.DisplayName));
                return;
            }
            var accepted = _navigation.TargetFromBrowser(live);
            hud->OpenContextMenuFromTarget((GameObject*)live.Address);

            _log.Info($"[ChatPlayer] Kontextmenü + Ziel '{player.DisplayName}' " +
                      $"id={live.GameObjectId:X} anvisiert={accepted}");
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatPlayerMenuOpened(player.DisplayName));
            return;
        }

        if (TryOpenFromPartyHud(player))
            return;

        if (TryAnnounceSocialLocation(player))
            return;

        _log.Info($"[ChatPlayer] Nicht gefunden: '{player.DisplayName}' worldId={player.WorldId}");
        _tolk.SpeakInterrupt(AccessibilityStrings.ChatPlayerNotFound(player.DisplayName));
    }

    private IGameObject? FindInObjectTable(TellTarget player)
    {
        var matches = _objects
            .Where(o => o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                        && string.Equals(o.Name.TextValue, player.Name, StringComparison.Ordinal))
            .ToList();

        if (matches.Count == 0) return null;

        if (player.WorldId != 0)
        {
            // Even a single namesake from another world is a different player.
            matches = matches.Where(o => PlayerInfo.HomeWorldId(o) == player.WorldId).ToList();
        }

        // Same display name twice without a world id: refuse rather than pick wrong.
        if (matches.Count != 1) return null;

        return matches[0];
    }

    private unsafe bool TryOpenFromPartyHud(TellTarget player)
    {
        var hud = AgentHUD.Instance();
        if (hud == null || hud->PartyMemberCount <= 0) return false;

        var addon = _gameGui.GetAddonByName("_PartyList");
        if (addon.IsNull) return false;
        var addonId = ((FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase*)(nint)addon)->Id;

        var members = hud->PartyMembers;
        var count = Math.Min((int)hud->PartyMemberCount, members.Length);
        var matched = -1;
        for (var i = 0; i < count; i++)
        {
            var member = members[i];
            var name = member.Name.ToString();
            if (!string.Equals(name, player.Name, StringComparison.Ordinal)) continue;

            // An unloaded party member has no verifiable world here. The social
            // lookup below can still report their location by name AND world.
            if (player.WorldId != 0)
            {
                if (member.Object == null) continue;
                var candidate = _objects.FirstOrDefault(o => o.Address == (nint)member.Object);
                if (candidate == null || PlayerInfo.HomeWorldId(candidate) != player.WorldId) continue;
            }
            if (matched >= 0) return false;
            matched = i;
        }
        if (matched >= 0)
        {
            var member = members[matched];

            if (member.Object != null)
            {
                var live = _objects.FirstOrDefault(o =>
                    o.Address == (nint)member.Object);
                if (live != null)
                    _navigation.TargetFromBrowser(live);
            }

            hud->OpenContextMenuFromPartyAddon((int)addonId, member.Index);
            _log.Info($"[ChatPlayer] Kontextmenü aus Party-HUD '{player.DisplayName}' " +
                      $"slot={member.Index} addon={addonId}");
            _tolk.SpeakInterrupt(AccessibilityStrings.ChatPlayerMenuOpened(player.DisplayName));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Friend / FC / CWLS lists can name a player who is not in the object
    /// table and report their zone. That is map-spanning <em>search</em>, not a
    /// menu — announced honestly.
    /// </summary>
    private unsafe bool TryAnnounceSocialLocation(TellTarget player)
    {
        if (player.WorldId == 0) return false;

        var info = InfoModule.Instance();
        if (info == null) return false;

        InfoProxyId[] proxies =
        [
            InfoProxyId.FriendList,
            InfoProxyId.PartyMember,
            InfoProxyId.FreeCompanyMember,
            InfoProxyId.LinkshellMember,
            InfoProxyId.CrossWorldLinkshellMember,
        ];

        foreach (var id in proxies)
        {
            var raw = info->GetInfoProxyById(id);
            if (raw == null) continue;

            // FriendList / PartyMember / FC / LS share InfoProxyCommonList layout
            // for GetEntryByName (ClientStructs).
            var list = (InfoProxyCommonList*)raw;
            var entry = list->GetEntryByName(player.Name, player.WorldId);
            if (entry == null) continue;

            var zoneId = entry->Location;
            var zoneName = ZoneName(zoneId);
            _log.Info($"[ChatPlayer] In Sozialliste {id}: '{player.DisplayName}' " +
                      $"Location={zoneId} ({zoneName}) — kein geladenes Objekt.");
            _tolk.SpeakInterrupt(
                AccessibilityStrings.ChatPlayerElsewhere(player.DisplayName, zoneName));
            return true;
        }

        return false;
    }

    private string ZoneName(ushort territoryId)
    {
        if (territoryId == 0) return AccessibilityStrings.ChatPlayerZoneUnknown;
        if (!_data.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var row))
            return AccessibilityStrings.ChatPlayerZoneUnknown;
        var name = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        return string.IsNullOrWhiteSpace(name)
            ? AccessibilityStrings.ChatPlayerZoneUnknown
            : name.Trim();
    }
}
