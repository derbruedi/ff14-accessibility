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
/// A REAL GAME SIGN WINS. If somebody in the party puts one of FF14's own target
/// signs on an enemy - "Angreifen 1", "Binden 2", "Kreis" - that sign is spoken
/// instead of the colour, and the colour goes back into the pot. Sku behaves
/// exactly this way (<c>aqCombatCheckGameRaidTargets</c> deletes its own entry
/// the moment the game has a marker on that unit), and the reason is that the
/// two are not equal: the game sign is what the REST OF THE PARTY sees and talks
/// about, so it is the name everyone shares. A private colour that contradicts
/// the group's "Angreifen 1" would be worse than no nickname at all.
/// </para>
///
/// <para>
/// The sign NAMES come from the game's own Marker sheet, so they arrive in the
/// game's language and match what a sighted player reads on screen - they are
/// not translated here. Same rule as everywhere in the plugin: game content
/// comes from the game.
/// </para>
///
/// <para>
/// NO GAME SIGN IS EVER PLACED, deliberately. FFXIVClientStructs exposes the
/// sign table for READING (<c>MarkingController.Markers</c>) but offers no
/// function to set one - only ground markers (<c>PlaceFieldMarker</c>) - and in
/// a party the signs belong to the party leader anyway. Overwriting them would
/// take something away from the group to gain nothing: the colour is only ever
/// spoken. Sku works the same way, and for the same reason.
/// </para>
/// </summary>
public sealed class EnemyMarkerService
{
    /// <summary>How many enemies can carry a colour at once. Sku's table size.</summary>
    public const int ColorCount = 8;

    /// <summary>
    /// Slots in the game's target-sign table. Seventeen, and the game's Marker
    /// sheet holds seventeen named signs in rows 1 to 17 (row 0 is blank) - read
    /// from the shipped game data on 2026-09-01, see GameSignName.
    /// </summary>
    private const int GameSignSlots = 17;

    private readonly IObjectTable _objects;
    private readonly IDataManager _data;
    private readonly Configuration _config;
    private readonly IPluginLog _log;

    // Colour index per enemy, keyed by EntityId. NOT by GameObjectId: the game's
    // own enemy list and hate list both speak EntityId, and going through one id
    // space avoids a translation that could silently mismatch.
    private readonly Dictionary<uint, int> _live = new();

    // Colours of enemies that have left the fight (dead, despawned, ran off),
    // OLDEST FIRST. They stay blocked while any unused colour is still left. Sku
    // does the same with its SkuRaidTargetRepoDead, and the reason is worth
    // stating: if the colour were handed straight on, the "Rot" that just died
    // would be replaced by a fresh, unrelated "Rot" seconds later, and the player
    // would be tracking a mob that no longer exists.
    //
    // A LIST and not a set, because the order is the point: when the pot runs dry
    // the LONGEST-retired colour is the one that comes back first. See
    // NextFreeColor for why they come back at all.
    private readonly List<int> _spent = new();

    // Who has the player on their own hate list, i.e. who is actually swinging at
    // them rather than at the tank. Refreshed every sweep.
    private readonly HashSet<uint> _onMe = new();

    // Enemies in the order the game lists them, for the readout.
    private readonly List<uint> _order = new();

    // EntityId -> row of the game's Marker sheet (1..17), for every enemy the
    // party has put a real target sign on. Refreshed every sweep, because a sign
    // can be set and cleared by anyone at any time.
    private readonly Dictionary<uint, int> _gameSigns = new();

    // Which signs have already been logged, so the probe writes one line per sign
    // and not one per frame.
    private readonly HashSet<uint> _loggedSigns = new();

    private int _loggedCount = -1;

    public EnemyMarkerService(IObjectTable objects, IDataManager data, Configuration config, IPluginLog log)
    {
        _objects = objects;
        _data    = data;
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

        CollectGameSigns();

        // A real party sign beats our colour. Give the colour straight back to the
        // pot - NOT to the retired pile: it was never worn out by a death, the
        // enemy simply stopped needing it. Sku does the same in
        // aqCombatCheckGameRaidTargets.
        if (_gameSigns.Count > 0)
        {
            List<uint>? signed = null;
            foreach (var id in _live.Keys)
                if (_gameSigns.ContainsKey(id)) (signed ??= new List<uint>()).Add(id);
            if (signed != null)
                foreach (var id in signed)
                    _live.Remove(id);
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

        // Hand a colour to everyone new, in the order the game lists them. This
        // runs EVERY frame, not just at the start of a fight: an enemy that walks
        // into a fight already in progress - an add, a wandering patrol, a second
        // pull that joins the first - is simply an id that is not in _live yet and
        // is named here like any other.
        foreach (var id in _order)
        {
            if (_live.ContainsKey(id)) continue;
            if (_gameSigns.ContainsKey(id)) continue;   // carries a party sign, needs no colour
            var free = NextFreeColor();
            if (free < 0) continue;   // eight enemies alive at once: the ninth stays unnamed, as in Sku
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

        // Ein echtes Zeichen der Gruppe geht vor. Es ist der Name, den ALLE
        // benutzen; unsere Farbe waere daneben eine zweite, private Wahrheit.
        if (_gameSigns.TryGetValue(obj.EntityId, out var sheetRow))
        {
            var sign = GameSignName(sheetRow);
            if (!string.IsNullOrEmpty(sign)) return sign + ", ";
        }

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

            // Dieselbe Rangfolge wie in SpokenPrefix: Spiel-Zeichen vor Farbe.
            var color = _gameSigns.TryGetValue(id, out var sheetRow) ? GameSignName(sheetRow)
                      : _live.TryGetValue(id, out var c)            ? AccessibilityStrings.EnemyMarkerColor(c)
                      : string.Empty;
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
        _gameSigns.Clear();
        _loggedSigns.Clear();
        if (_loggedCount != 0) _log.Info("[Gegnerfarben] Kampf vorbei, alle Farben wieder frei.");
        _loggedCount = 0;
    }

    /// <summary>
    /// The colour for the next enemy, or -1 if every one of the eight is on a
    /// LIVING enemy right now.
    ///
    /// <para>
    /// Two passes, and the second one matters. First the lowest colour that is
    /// neither in use nor retired. If there is none, the longest-retired colour
    /// is taken back into service.
    /// </para>
    ///
    /// <para>
    /// WHY RETIRED COLOURS COME BACK: a fight is not eight enemies and done. Kill
    /// five, and five colours are retired; the adds that walk in afterwards would
    /// find an empty pot and stay NAMELESS. That is the worse outcome by far - a
    /// nameless enemy cannot be told from any other, which is the one thing the
    /// colours exist to fix, and a blind player has no way to notice that a name
    /// is missing rather than merely unspoken. Reusing "Rot" for a new enemy is a
    /// small risk of confusion; leaving that enemy anonymous is a guaranteed one.
    /// Retirement therefore still does its job whenever it can afford to - which
    /// is every normal pull - and gives way when the alternative is silence.
    /// </para>
    ///
    /// <para>
    /// Oldest first, so the colour that comes back is the one whose enemy died
    /// longest ago and is least likely to still be in the player's head. Sku has
    /// no equivalent - its repo simply runs dry - so this is a deliberate
    /// departure, noted as such in the project notes.
    /// </para>
    /// </summary>
    private int NextFreeColor()
    {
        for (var i = 0; i < ColorCount; i++)
        {
            if (_spent.Contains(i)) continue;
            if (!InUse(i)) return i;
        }

        // Pot empty: recycle the longest-retired colour rather than go silent.
        while (_spent.Count > 0)
        {
            var recycled = _spent[0];
            _spent.RemoveAt(0);
            if (InUse(recycled)) continue;   // belt and braces; a retired colour is never live
            _log.Info($"[Gegnerfarben] Alle Farben vergeben - '{AccessibilityStrings.EnemyMarkerColor(recycled)}' " +
                      "wird wiederverwendet, damit der neue Gegner nicht namenlos bleibt.");
            return recycled;
        }

        return -1;
    }

    /// <summary>True while the colour sits on a living enemy.</summary>
    private bool InUse(int color)
    {
        foreach (var used in _live.Values)
            if (used == color) return true;
        return false;
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
    /// Reads the party's real target signs into <see cref="_gameSigns"/>.
    ///
    /// <para>
    /// <c>MarkingController.Markers</c> is a table of 17 slots; each slot holds the
    /// <c>GameObjectId</c> of whatever currently wears that sign, and an unused
    /// slot reads as the game's "no object". GameObjectId carries the EntityId in
    /// its <c>ObjectId</c> field, so the ids line up with the enemy and hate lists
    /// without a detour through the object table.
    /// </para>
    ///
    /// <para>
    /// UNBESTAETIGT, muss einmal im Spiel gegengeprueft werden: dass Slot i dem
    /// Marker-Sheet-Eintrag i+1 entspricht. Dafuer spricht, dass es genau 17
    /// Slots und genau 17 benannte Zeichen gibt (Sheet-Zeile 0 ist leer) und dass
    /// die Sheet-Reihenfolge - Angreifen 1-5, Binden 1-3, Ignorieren 1-2, die
    /// vier Formen, dann Angreifen 6-8 am Ende - genau die eines spaeter
    /// erweiterten Feldes ist. Bewiesen ist es damit NICHT. Deshalb schreibt
    /// jeder erstmals gesehene Slot eine Zeile ins Log: setzt jemand
    /// "Angreifen 1" und im Log steht Slot 0 mit genau diesem Namen, stimmt die
    /// Zuordnung. Steht dort ein anderer Name, ist sie um eins verschoben.
    /// </para>
    /// </summary>
    private unsafe void CollectGameSigns()
    {
        _gameSigns.Clear();

        var marking = MarkingController.Instance();
        if (marking == null) return;

        var markers = marking->Markers;
        var count   = Math.Min(GameSignSlots, markers.Length);
        for (var slot = 0; slot < count; slot++)
        {
            var entityId = markers[slot].ObjectId;
            if (entityId == 0 || entityId == EmptyObjectId) continue;

            var sheetRow = slot + 1;
            _gameSigns[entityId] = sheetRow;

            if (_loggedSigns.Add(entityId))
                _log.Info($"[Gegnerfarben] Spiel-Zeichen erkannt: Slot {slot} -> Sheet-Zeile {sheetRow} " +
                          $"= '{GameSignName(sheetRow)}' auf Gegner {entityId:X}. " +
                          "Stimmt der Name mit dem gesetzten Zeichen ueberein?");
        }

        // Vergessene Ids wieder freigeben, damit das Log bei einem neuen Kampf
        // wieder meldet und nicht ewig waechst.
        if (_gameSigns.Count == 0 && _loggedSigns.Count > 0) _loggedSigns.Clear();
    }

    /// <summary>
    /// The game's own name for a target sign, straight out of the Marker sheet -
    /// "Angreifen 1", "Binden 2", "Kreis". Not translated here on purpose: this is
    /// what the rest of the party sees on screen, so it has to be the game's word,
    /// in the game's language.
    /// </summary>
    private string GameSignName(int sheetRow)
    {
        var sheet = _data.GetExcelSheet<Lumina.Excel.Sheets.Marker>();
        var row   = sheet?.GetRowOrDefault((uint)sheetRow);
        return row?.Name.ExtractText() ?? string.Empty;
    }

    /// <summary>
    /// The value an EMPTY object slot reads as in the game's tables. Not zero -
    /// the game uses 0xE0000000 for "nobody".
    /// </summary>
    private const uint EmptyObjectId = 0xE0000000;

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
