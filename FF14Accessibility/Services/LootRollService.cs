using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace FF14Accessibility.Services;

/// <summary>
/// Makes the party loot roll ("Bedarf / Gier / Passen") usable without sight.
/// In a dungeon the game pops up the NeedGreed window and starts a timer; a
/// blind player neither sees that it appeared nor what is being rolled for.
/// <para>
/// The announcement does NOT scrape that window. The game keeps the whole roll
/// state in <c>Client.Game.UI.Loot</c> (ilspycmd-verified 2026-08-09): up to 16
/// <c>LootItem</c> entries with ItemId, ItemCount, RollState (what YOU may still
/// do), RollResult (what you already did), RollValue, Time/MaxTime and LootMode.
/// Reading that is independent of whether the window has focus, is scrolled, or
/// is even open - which is exactly why it is used here.
/// </para>
/// </summary>
public sealed class LootRollService
{
    private readonly IDataManager _data;
    private readonly IClientState _clientState;
    private readonly IGameGui _gameGui;
    private readonly Configuration _config;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;

    public LootRollService(IDataManager data, IClientState clientState, IGameGui gameGui,
                           Configuration config, TolkService tolk, IPluginLog log)
    {
        _data = data;
        _clientState = clientState;
        _gameGui = gameGui;
        _config = config;
        _tolk = tolk;
        _log = log;
    }

    /// <summary>The roll window the game pops up, or null when it is not up.</summary>
    private unsafe AtkUnitBase* NeedGreedAddon()
    {
        var handle = _gameGui.GetAddonByName("NeedGreed");
        if (handle.IsNull) return null;
        var addon = (AtkUnitBase*)(nint)handle;
        return addon != null && addon->IsVisible ? addon : null;
    }

    /// <summary>
    /// Hands keyboard focus to the roll window so the player can work it with
    /// the game's own cursor navigation (numpad) and pick Bedarf/Gier/Passen
    /// there - instead of the plugin pressing those buttons for them.
    /// <para>
    /// Uses the window's own <c>AtkUnitBase.Focus()</c> (ilspycmd-verified
    /// 2026-08-09; the same struct also carries SetFocusNode/FocusNode/
    /// CursorTarget). Deliberately NOT automatic on the window opening: a focus
    /// that grabs itself mid-fight would swallow the numpad while the player
    /// still needs to move.
    /// </para>
    /// </summary>
    public unsafe void FocusRollWindow()
    {
        var addon = NeedGreedAddon();
        if (addon == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LootRollNoWindow);
            _log.Info("[Loot] Fokus angefordert, aber kein sichtbares NeedGreed-Fenster.");
            return;
        }

        // Log the focus fields before and after, so the first in-game test shows
        // whether Focus() alone is enough or a start node has to be set too.
        _log.Info($"[Loot] Vor Focus(): FocusNode={(nint)addon->FocusNode:X} " +
                  $"CursorTarget={(nint)addon->CursorTarget:X} " +
                  $"ComponentFocusNode={(nint)addon->ComponentFocusNode:X}");
        addon->Focus();
        _log.Info($"[Loot] Nach Focus(): FocusNode={(nint)addon->FocusNode:X} " +
                  $"CursorTarget={(nint)addon->CursorTarget:X} " +
                  $"ComponentFocusNode={(nint)addon->ComponentFocusNode:X}");

        _tolk.SpeakInterrupt(AccessibilityStrings.LootRollFocused);
    }

    /// <summary>One open roll, flattened for announcing.</summary>
    private readonly record struct Roll(
        int Index, uint ItemId, string Name, int Count,
        RollState State, RollResult Result, byte Value, float Time, float MaxTime);

    // Which entries were already announced, keyed by slot index + item id: the
    // same slot gets reused for the next drop, so the item id has to be part of
    // the identity or a second roll in the same slot would stay silent.
    private readonly HashSet<(int Index, uint ItemId)> _announced = new();

    // Rolls are polled rather than hooked: the state lives in a plain struct, and
    // once a second is far below the roll timer while costing nothing.
    private long _lastCheck;

    /// <summary>
    /// Announces rolls that have just opened. Called every frame, does its work
    /// once a second.
    /// </summary>
    public unsafe void Update()
    {
        if (!_clientState.IsLoggedIn)
        {
            _announced.Clear();
            return;
        }

        var now = Environment.TickCount64;
        if (now - _lastCheck < 1000) return;
        _lastCheck = now;

        var open = CollectOpenRolls();

        // Forget entries that are gone, so a slot can announce again later.
        _announced.RemoveWhere(k => !open.Exists(r => r.Index == k.Index && r.ItemId == k.ItemId));

        if (!_config.AnnounceLootRolls) return;

        foreach (var roll in open)
        {
            // Only entries the player can still act on are worth interrupting
            // for - an already rolled or unavailable line is not news.
            if (roll.State is not (RollState.UpToNeed or RollState.UpToGreed or RollState.UpToPass))
                continue;
            if (!_announced.Add((roll.Index, roll.ItemId))) continue;

            _log.Info($"[Loot] Neue Verlosung: slot={roll.Index} item={roll.ItemId} '{roll.Name}' x{roll.Count} " +
                      $"state={roll.State} result={roll.Result} value={roll.Value} " +
                      $"time={roll.Time:F1} maxTime={roll.MaxTime:F1}");
            _tolk.Speak(AccessibilityStrings.LootRollStarted(roll.Name, roll.Count, DescribeOptions(roll.State)));
        }
    }

    /// <summary>
    /// Reads out every open roll on demand: name, count, what you may still do
    /// and what you already did. Says so when nothing is being rolled for.
    /// </summary>
    public void AnnounceOpenRolls()
    {
        var open = CollectOpenRolls();
        if (open.Count == 0)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.LootRollNone);
            return;
        }

        var parts = new List<string>();
        foreach (var roll in open)
        {
            _log.Info($"[Loot] Offen: slot={roll.Index} '{roll.Name}' x{roll.Count} state={roll.State} " +
                      $"result={roll.Result} value={roll.Value} time={roll.Time:F1} maxTime={roll.MaxTime:F1}");
            parts.Add(AccessibilityStrings.LootRollEntry(
                roll.Name, roll.Count, DescribeOptions(roll.State), DescribeOwnRoll(roll.Result, roll.Value)));
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.LootRollList(open.Count, string.Join(". ", parts)));
    }

    /// <summary>
    /// The currently filled loot slots. An entry counts as filled when it names
    /// an item; the game leaves stale slots behind with ItemId 0.
    /// </summary>
    private unsafe List<Roll> CollectOpenRolls()
    {
        var result = new List<Roll>();
        var loot = Loot.Instance();
        if (loot == null) return result;

        var items = loot->Items;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item.ItemId == 0) continue;

            var name = ResolveItemName(item.ItemId);
            result.Add(new Roll(i, item.ItemId, name, item.ItemCount,
                item.RollState, item.RollResult, item.RollValue, item.Time, item.MaxTime));
        }
        return result;
    }

    /// <summary>
    /// What the player may still do with this entry, straight from RollState.
    /// The enum is cumulative (ilspycmd 2026-08-09): UpToNeed = need, greed or
    /// pass; UpToGreed = greed or pass (need is barred, e.g. wrong job);
    /// UpToPass = pass only.
    /// </summary>
    private static string DescribeOptions(RollState state) => state switch
    {
        RollState.UpToNeed  => AccessibilityStrings.LootOptionsNeedGreedPass,
        RollState.UpToGreed => AccessibilityStrings.LootOptionsGreedPass,
        RollState.UpToPass  => AccessibilityStrings.LootOptionsPassOnly,
        RollState.Rolled    => AccessibilityStrings.LootOptionsDone,
        _                   => AccessibilityStrings.LootOptionsUnavailable,
    };

    /// <summary>What the player already did with this entry, plus the rolled
    /// number when there is one. "" while nothing has been chosen yet.</summary>
    private static string DescribeOwnRoll(RollResult result, byte value) => result switch
    {
        RollResult.Needed  => AccessibilityStrings.LootRolledNeed(value),
        RollResult.Greeded => AccessibilityStrings.LootRolledGreed(value),
        RollResult.Passed  => AccessibilityStrings.LootRolledPass,
        RollResult.Awarded => AccessibilityStrings.LootRolledWon,
        _                  => string.Empty,
    };

    private string ResolveItemName(uint itemId)
    {
        if (_data.GetExcelSheet<LuminaItem>().TryGetRow(itemId, out var row))
        {
            var name = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return AccessibilityStrings.ItemFallback(itemId);
    }
}
