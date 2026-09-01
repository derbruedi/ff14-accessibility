using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace FF14Accessibility.Services;

/// <summary>
/// Gives every enemy currently engaged with the party a short spoken NICKNAME -
/// a colour - that stays with it for the whole fight, so a blind player can tell
/// two enemies apart and count how many are on them.
///
/// <para>
/// This is a port of the "Sku raid target" mechanism from the WoW addon Sku
/// (<c>SkuCore/aqCombat.lua</c>, setting <c>autoSetSkuRaidTargetsToInCombatCreatures</c>).
/// Sku hands each mob in combat the next free slot of an eight-entry table and
/// speaks the COLOUR of that slot in front of the mob's name. The colours are
/// carried over unchanged - Weiss, Rot, Blau, Gruen, Lila, Gelb, Orange, Grau,
/// in exactly Sku's assignment order - because the player has used them for
/// years and should not have to relearn them.
/// </para>
///
/// <para>
/// WHY A NICKNAME AT ALL: three "Wüstenwolf" in a pull are three identical
/// announcements. A sighted player tells them apart by where they stand; a blind
/// player cannot. "Rot, Wüstenwolf" and "Blau, Wüstenwolf" are two distinct
/// enemies, and the fact that colours three through eight never got handed out
/// is itself the answer to "how many am I fighting".
/// </para>
///
/// <para>
/// NO GAME MARKER IS PLACED, deliberately. FF14's target signs (Angriff 1-5 and
/// friends) are not reachable through a documented client function - the only
/// marker API in FFXIVClientStructs is <c>MarkingController.PlaceFieldMarker</c>,
/// which places GROUND markers - and in a party the signs belong to the party
/// leader. Overwriting them would take something away from the group to gain
/// nothing: the colour is only ever spoken. Sku works the same way, and for the
/// same reason.
/// </para>
/// </summary>
public sealed class EnemyMarkerService
{
    /// <summary>How many enemies can carry a colour at once. Sku's table size.</summary>
    public const int ColorCount = 8;

    private readonly IObjectTable _objects;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    // Colour index per enemy, keyed by EntityId. NOT by GameObjectId: the game's
    // own enemy list and hate list both speak EntityId, and going through one id
    // space avoids a translation that could silently mismatch.
    private readonly Dictionary<uint, int> _live = new();

    // Colours of enemies that have left the fight (dead, despawned, ran off).
    // They stay BLOCKED until the fight is over. Sku does the same with its
    // SkuRaidTargetRepoDead, and the reason is worth stating: if the colour were
    // handed straight on, the "Rot" that just died would be replaced by a fresh,
    // unrelated "Rot" seconds later, and the player would be tracking a mob that
    // no longer exists. A used colour is retired for the rest of the fight.
    private readonly HashSet<int> _spent = new();

    // Who has the player on their own hate list, i.e. who is actually swinging at
    // them rather than at the tank. Refreshed every sweep.
    private readonly HashSet<uint> _onMe = new();

    // Enemies in the order the game lists them, for the readout.
    private readonly List<uint> _order = new();

    private int _loggedCount = -1;

    public EnemyMarkerService(IObjectTable objects, Configuration config, IPluginLog log)
    {
        _objects = objects;
        _config  = config;
        _log     = log;
    }

    /// <summary>Called every frame from Plugin.OnFrameworkUpdate.</summary>
    public void Update()
    {
        if (!_config.EnemyMarkersEnabled)
        {
            if (_live.Count > 0) Reset();
            return;
        }

        CollectCurrentEnemies();

        // Nobody left: the fight is over, every colour goes back into the pot.
        if (_order.Count == 0)
        {
            if (_live.Count > 0 || _spent.Count > 0) Reset();
            return;
        }

        // Retire the colours of everyone who has left the list since last frame.
        List<uint>? gone = null;
        foreach (var known in _live.Keys)
            if (!_order.Contains(known)) (gone ??= new List<uint>()).Add(known);
        if (gone != null)
            foreach (var id in gone)
            {
                _spent.Add(_live[id]);
                _live.Remove(id);
            }

        // Hand a colour to everyone new, in the order the game lists them.
        foreach (var id in _order)
        {
            if (_live.ContainsKey(id)) continue;
            var free = NextFreeColor();
            if (free < 0) continue;   // more than eight enemies: the rest stay unnamed, as in Sku
            _live[id] = free;
        }

        if (_live.Count != _loggedCount)
        {
            _loggedCount = _live.Count;
            _log.Info($"[Gegnerfarben] {_live.Count} benannt, {_spent.Count} Farben verbraucht, " +
                      $"{_onMe.Count} auf dem Spieler.");
        }
    }

    /// <summary>
    /// The colour of one object as a ready-to-speak prefix ("Rot, "), or an empty
    /// string when the object carries no colour - not an enemy, not in the fight,
    /// or past the eighth. Empty string rather than null so the callers can drop
    /// it straight into their sentence without a branch.
    /// </summary>
    public string SpokenPrefix(IGameObject? obj)
    {
        if (!_config.EnemyMarkersEnabled || obj == null) return string.Empty;
        return _live.TryGetValue(obj.EntityId, out var color)
            ? AccessibilityStrings.EnemyMarkerColor(color) + ", "
            : string.Empty;
    }

    /// <summary>
    /// The whole current field as one sentence: how many enemies, then each one
    /// with its colour, name, health and whether it is on the player. This is the
    /// answer to "who am I fighting", the question the colours only answer one
    /// enemy at a time.
    /// </summary>
    public string DescribeField()
    {
        if (!_config.EnemyMarkersEnabled) return AccessibilityStrings.EnemyMarkersOff;
        CollectCurrentEnemies();
        if (_order.Count == 0) return AccessibilityStrings.NoEnemiesEngaged;

        var parts = new List<string>(_order.Count + 1)
        {
            AccessibilityStrings.EnemyCountIntro(_order.Count),
        };

        foreach (var id in _order)
        {
            var obj  = _objects.SearchByEntityId(id);
            var name = obj?.Name.TextValue;
            if (string.IsNullOrWhiteSpace(name)) name = NameFromHateList(id);
            if (string.IsNullOrWhiteSpace(name)) name = AccessibilityStrings.TargetFallbackName;

            var color = _live.TryGetValue(id, out var c) ? AccessibilityStrings.EnemyMarkerColor(c) : string.Empty;
            var hp    = obj is IBattleChara bc && bc.MaxHp > 0
                ? (int)Math.Round(bc.CurrentHp * 100.0 / bc.MaxHp)
                : -1;

            parts.Add(AccessibilityStrings.EnemyFieldEntry(color, name, hp, _onMe.Contains(id)));
        }

        return string.Join(" ", parts);
    }

    /// <summary>Drops every assignment. Called when the fight ends and on dispose.</summary>
    private void Reset()
    {
        _live.Clear();
        _spent.Clear();
        _onMe.Clear();
        _order.Clear();
        if (_loggedCount != 0) _log.Info("[Gegnerfarben] Kampf vorbei, alle Farben wieder frei.");
        _loggedCount = 0;
    }

    /// <summary>Lowest colour that is neither in use nor already retired, or -1.</summary>
    private int NextFreeColor()
    {
        for (var i = 0; i < ColorCount; i++)
        {
            if (_spent.Contains(i)) continue;
            var taken = false;
            foreach (var used in _live.Values)
                if (used == i) { taken = true; break; }
            if (!taken) return i;
        }
        return -1;
    }

    /// <summary>
    /// Fills <see cref="_order"/> and <see cref="_onMe"/> from the game's own two
    /// lists.
    ///
    /// <para>
    /// PRIMARY SOURCE is the HUD enemy list (<c>EnemyListNumberArray</c>): that is
    /// literally the list of enemies engaged with the PARTY, in the order the game
    /// itself shows them, and it is what a sighted player reads. Reading it
    /// follows the project rule "read, never recompute" - no own guess at who
    /// counts as "in the fight".
    /// </para>
    ///
    /// <para>
    /// SECOND SOURCE is the hate list (<c>UIState.Hater</c>), which holds everyone
    /// who has the PLAYER on their own aggro table. It serves two purposes: it
    /// says which enemies are actually swinging at the player rather than at the
    /// tank, and it keeps the service working if the enemy list array is empty -
    /// the enemy list is a HUD element and a player can switch it off. Falling
    /// back is better than falling silent; silence is indistinguishable from "no
    /// enemies" for the one person who cannot check.
    /// </para>
    /// </summary>
    private unsafe void CollectCurrentEnemies()
    {
        _order.Clear();
        _onMe.Clear();

        var list = EnemyListNumberArray.Instance();
        if (list != null)
        {
            var count = Math.Min(list->EnemyCount, list->Enemies.Length);
            for (var i = 0; i < count; i++)
            {
                var entry = list->Enemies[i];
                if (!entry.ActiveInList) continue;
                var id = (uint)entry.EntityId;
                if (id != 0 && !_order.Contains(id)) _order.Add(id);
            }
        }

        var ui = UIState.Instance();
        if (ui != null)
        {
            var haters = ui->Hater.Haters;
            var count  = Math.Min(ui->Hater.HaterCount, haters.Length);
            for (var i = 0; i < count; i++)
            {
                var id = haters[i].EntityId;
                if (id == 0) continue;
                _onMe.Add(id);
                if (!_order.Contains(id)) _order.Add(id);
            }
        }
    }

    /// <summary>
    /// The name the hate list carries for an enemy. Used when the object table has
    /// no entry - an enemy can be engaged and still be out of the client's object
    /// range, and a nameless line in the readout would look like a bug.
    /// </summary>
    private unsafe string NameFromHateList(uint entityId)
    {
        var ui = UIState.Instance();
        if (ui == null) return string.Empty;

        var haters = ui->Hater.Haters;
        var count  = Math.Min(ui->Hater.HaterCount, haters.Length);
        for (var i = 0; i < count; i++)
            if (haters[i].EntityId == entityId)
                return haters[i].NameString;

        return string.Empty;
    }
}
