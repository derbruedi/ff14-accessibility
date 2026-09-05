// The one thing the gear comparison window shows that a blind player cannot get
// anywhere else: WHETHER THE PIECE IN THE BAG IS AN UPGRADE, and by how much in
// every single value.
//
// The game already answers that for a sighted player, and it answers it through
// LAYOUT AND COLOUR: two stat blocks printed side by side, with a difference next
// to each value of the selected one, green when it is up and red when it is down.
// A screen reader sees neither the columns nor the colours.
//
// This service rebuilds that window as a SPOKEN TABLE the player walks with the
// numpad (see BuildTable): the verdict is the title, and every value is one row
// carrying both sides - "Defence, 19, equipped 7, plus 12". The two columns of
// the game's window survive as the two halves of a row, which is the shape a
// screen reader can actually walk; a cursor that moves left and right between
// cells was considered and dropped, because it would have taken Numpad 4 and 6
// away from the back/confirm they mean in every other menu of this mod.
//
// THIS IS READ, NOT RECOMPUTED, and that distinction is the whole design. The
// obvious alternative was to look both items up in the Item sheet and subtract.
// That would have been a second implementation of the game's own arithmetic, and
// it would have been WRONG in the cases that matter: the equipped piece carries
// materia, condition and spiritbond that the sheet row knows nothing about. The
// game publishes its finished numbers in AddonItemDetailCompare's AtkValues, so
// those are what is spoken.
//
// WHAT WAS MEASURED, 2026-08-30 (nine hovers in game, logged via the [ItemCmp]
// probe) AND RE-VERIFIED AGAINST THE STRUCT, 2026-09-04 (the field offsets of
// AtkValuesArray.ComparedItem read out of the FFXIVClientStructs.dll in
// DALAMUD_HOME by reflection; every index below is that struct's own byte offset
// divided by 16):
//   - The value array is 349 entries: four 86-value ComparedItem blocks
//     (SelectedItem, SelectedItemOtherQuality, EquippedItem, LeftRing) then five
//     singles ending in CtrlHeld.
//   - EVERY BLOCK CARRIES ITS OWN VALUES, not just the selected one. That is what
//     makes a two-column table possible at all: PrimaryStatValues (33..35) and
//     BonusStats (40..47) exist on the equipped block too, so the right-hand
//     column is READ rather than looked up somewhere else.
//   - Differences are PRE-SIGNED STRINGS in parentheses: "(+25)", "(-12)". The
//     direction is in the text, so MainStatDeltaColorTimelineId - the colour the
//     game paints - is never needed and is deliberately not read.
//   - A difference of ZERO IS OMITTED ENTIRELY rather than written as "(0)".
//     That is why the verdict says "same as equipped" out loud: silence there
//     would be indistinguishable from having no data at all, which for a blind
//     player is the difference between "keep browsing" and "something is broken".
//   - Differences appear ONLY on the selected item; the equipped block and the
//     left-ring block never carry them. The table therefore states the difference
//     as part of the selected side's row and never expects one on the right.
//   - Armour fills two stat slots, weapons three (Delay / Auto-attack /
//     Physical Damage) and only the damage one gets a difference. So the three
//     slots are iterated and empties skipped; nothing about the shape is assumed.
//   - Rings are the only double comparison: LeftRing holds the OTHER hand's ring
//     and ShowRingSlotToggle flips to 1.
//   - Every string arrives finished and in the game's language ("Item Level 27",
//     "Slot: Body", "Gathering +32"), so it is passed through verbatim.
//
// WHY THE TABLE IS A SNAPSHOT and not re-read on every row. The repo's rule is
// "cache references, never values", and this is the case it does not cover: the
// rows are not an announcement waiting to go stale, they are a VIEW OF ONE MOMENT
// that the player opened deliberately on one item, and nothing re-speaks them
// later. Re-reading per row would be worse besides - the window belongs to the
// item under the cursor, and the menu holds the keyboard precisely so that cursor
// cannot move while the table is open.
using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Turns the game's own gear comparison into a spoken, numpad-navigable table:
/// whether the item under the cursor is better than what is worn, by how much,
/// value by value, and what it would replace.
/// </summary>
public sealed unsafe class ItemCompareService
{
    private readonly IGameGui        _gameGui;
    private readonly TolkService     _tolk;
    private readonly GearInfoService _gearInfo;
    private readonly IPluginLog      _log;

    /// <summary>Addon name, from the AddonAttribute on AddonItemDetailCompare.</summary>
    private const string AddonName = "ItemDetailCompare";

    // Layout of AddonItemDetailCompare.AtkValuesArray. AtkValue is 16 bytes, so a
    // byte offset in that struct divided by 16 is an index here.
    private const int SelectedBlock      = 0;
    private const int EquippedBlock      = 172;
    private const int LeftRingBlock      = 258;
    private const int ExpectedValueCount = 349;

    // Indices INSIDE one 86-value block (AtkValuesArray.ComparedItem).
    private const int RelItemName         = 22;
    private const int RelItemLevel        = 26;
    private const int RelEquippableBy     = 27;
    private const int RelEquipLevel       = 28;
    private const int RelPrimaryStatName  = 30;   // 3 entries
    private const int RelPrimaryStatValue = 33;   // 3 entries
    private const int RelPrimaryStatDelta = 36;   // 3 entries
    private const int PrimaryStatSlots    = 3;
    private const int RelBonusStat        = 40;   // 8 entries
    private const int BonusStatSlots      = 8;
    // Named BlueMateriaSlotCount in FFXIVClientStructs, but it is simply HOW MANY
    // SOCKETS the piece has - see AddMateriaRows for the measurement.
    private const int RelMateriaSlotCount = 10;
    private const int RelMateriaName      = 57;   // 5 entries
    private const int RelMateriaValue     = 62;   // 5 entries
    private const int MateriaSlots        = 5;
    private const int RelSellPrice        = 83;

    // Trailing singles.
    private const int IdxSlotName           = 344;
    private const int IdxShowRingSlotToggle = 346;

    /// <summary>Creates the reader.</summary>
    public ItemCompareService(IGameGui gameGui, TolkService tolk, GearInfoService gearInfo, IPluginLog log)
    {
        _gameGui  = gameGui;
        _tolk     = tolk;
        _gearInfo = gearInfo;
        _log      = log;
    }

    /// <summary>
    /// The comparison as a menu level: the verdict as the title, one row per
    /// value. null when the window is not open or not readable - null rather than
    /// an empty table so the caller decides what silence means.
    /// </summary>
    public MenuLevel? BuildTable()
    {
        var ptr = _gameGui.GetAddonByName(AddonName);
        if (ptr.IsNull) return null;

        var addon = (AtkUnitBase*)(nint)ptr;
        if (!addon->IsVisible) return null;

        var values = addon->AtkValuesSpan;
        if (values.Length < ExpectedValueCount)
        {
            // A patch that reshapes the array must not produce a confident wrong
            // table - every index below would point at something else.
            _log.Warning($"[ItemCmp] AtkValues {values.Length} < {ExpectedValueCount} - Tabelle wird NICHT gebaut.");
            return null;
        }

        var selectedName = ReadString(values, SelectedBlock + RelItemName);
        if (selectedName.Length == 0) return null;

        var equippedName = ReadString(values, EquippedBlock + RelItemName);
        var slotName     = ReadString(values, IdxSlotName);

        var rows = new List<string>();

        // 1. The two items themselves. Row one because it is the header of the two
        //    columns: everything below is "this one" against "that one".
        rows.Add(Row(AccessibilityStrings.CompareRowItem, selectedName, equippedName));

        // 2. The frame numbers. Both sides arrive already labelled by the game
        //    ("Item Level 27", "Lv. 27"), so they are passed through whole.
        AddVerbatimRow(rows, values, RelItemLevel);
        AddVerbatimRow(rows, values, RelEquipLevel);

        // 3. The values the decision actually turns on, with the game's OWN
        //    difference on the selected side.
        var better   = 0;
        var worse    = 0;
        var compared = 0;
        AddPrimaryStatRows(rows, values, ref better, ref worse, ref compared);
        AddBonusRows(rows, values, ref better, ref worse, ref compared);

        // 4. What is socketed. The worn piece routinely carries materia the bag
        //    piece does not, and that is a real part of the difference.
        AddMateriaRows(rows, values);

        // 5. Who may wear it, then the shop price. Last because neither decides
        //    whether one piece beats the other.
        AddClassRows(rows, values);
        AddVerbatimRow(rows, values, RelSellPrice);

        // 6. Rings compare against both hands; every difference above is against
        //    the one SlotName names, so the other is stated rather than implied.
        if (values[IdxShowRingSlotToggle].Type == AtkValueType.Int && values[IdxShowRingSlotToggle].Int != 0)
        {
            var otherRing = ReadString(values, LeftRingBlock + RelItemName);
            if (otherRing.Length > 0) rows.Add(AccessibilityStrings.CompareOtherRing(otherRing));
        }

        var level = new MenuLevel { Title = Verdict(better, worse, compared, selectedName, slotName) };
        foreach (var text in rows)
        {
            // Confirm REPEATS the row instead of doing nothing. There is nothing to
            // activate in a table, and a key that answers with silence is
            // indistinguishable from a key that is broken. StayOpen with no
            // Rebuild on the level leaves the table standing afterwards.
            var line = text;
            level.Entries.Add(new MenuEntry
            {
                Label    = line,
                Activate = () => _tolk.SpeakInterrupt(line),
                StayOpen = true,
            });
        }

        _log.Info($"[ItemCmp] Tabelle: '{level.Title}' mit {level.Entries.Count} Zeilen.");
        return level;
    }

    /// <summary>
    /// The title line: the answer to the question the player opened the window
    /// for, then what is being looked at. SlotName arrives with its own label
    /// ("Slot: Body"), so it is appended as the game wrote it.
    /// </summary>
    private static string Verdict(int better, int worse, int compared, string selectedName, string slotName)
    {
        var what = slotName.Length > 0 ? $"{selectedName}, {slotName}" : selectedName;

        // Nothing numeric to judge - the name and slot still tell the player what
        // they are standing on.
        if (compared == 0) return what;

        string verdict;
        if      (better > 0 && worse == 0) verdict = AccessibilityStrings.CompareBetter(better, compared);
        else if (worse > 0 && better == 0) verdict = AccessibilityStrings.CompareWorse(worse, compared);
        else if (better > 0 && worse > 0)  verdict = AccessibilityStrings.CompareMixed(better, worse, compared);
        else                               verdict = AccessibilityStrings.CompareSame;

        return $"{verdict} {what}";
    }

    /// <summary>
    /// The three primary stat slots, matched between the two blocks BY NAME
    /// rather than by slot index.
    ///
    /// By name, because the index is not an identity: the slots are filled in the
    /// order the game happens to print them, and armour fills two while weapons
    /// fill three. Pairing slot 0 with slot 0 would compare a weapon's Delay
    /// against the worn one's Physical Damage the moment the two items print
    /// their values in a different order - a wrong number, which is worse than a
    /// missing one. A stat only one side has is still listed, with the other side
    /// stated as absent.
    /// </summary>
    private static void AddPrimaryStatRows(List<string> rows, Span<AtkValue> values,
                                           ref int better, ref int worse, ref int compared)
    {
        var equipped = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < PrimaryStatSlots; i++)
        {
            var name = ReadString(values, EquippedBlock + RelPrimaryStatName + i);
            if (name.Length == 0) continue;
            equipped[name] = ReadString(values, EquippedBlock + RelPrimaryStatValue + i);
        }

        var used = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < PrimaryStatSlots; i++)
        {
            var name = ReadString(values, SelectedBlock + RelPrimaryStatName + i);
            if (name.Length == 0) continue;
            compared++;
            used.Add(name);

            var mine   = ReadString(values, SelectedBlock + RelPrimaryStatValue + i);
            var theirs = equipped.TryGetValue(name, out var t) ? t : string.Empty;
            var delta  = ParseDelta(ReadString(values, SelectedBlock + RelPrimaryStatDelta + i));

            if (delta == null)
            {
                rows.Add(Row(name, mine, theirs));
                continue;
            }

            var (up, amount) = delta.Value;
            if (up) better++; else worse++;
            rows.Add(Row(name, mine, theirs) + AccessibilityStrings.CompareDelta(up, amount));
        }

        // A stat the worn piece has and the bag piece does not. It counts as a
        // comparison - losing a value entirely is a change - but the game states
        // no difference for it, so none is invented.
        for (var i = 0; i < PrimaryStatSlots; i++)
        {
            var name = ReadString(values, EquippedBlock + RelPrimaryStatName + i);
            if (name.Length == 0 || used.Contains(name)) continue;
            compared++;
            rows.Add(Row(name, string.Empty, equipped[name]));
        }
    }

    /// <summary>
    /// The bonus rows (Strength, Dexterity, Critical Hit, ...).
    ///
    /// THIS IS THE ONE PLACE THE MOD COMPUTES INSTEAD OF READS, and that is not a
    /// shortcut - the game does not publish this figure anywhere. Measured
    /// 2026-08-30 across 34 dumps: each block lists its OWN bonuses as finished
    /// strings ("Strength +3"), and no difference field exists for them. The
    /// obvious candidate did not survive checking: GreenBonusItems / -Count is 0
    /// and empty in every single sample, so it does not mark improvements. A
    /// sighted player performs this comparison by eye, reading the two lists side
    /// by side; the layout IS the affordance, and a table row is the text
    /// equivalent of that layout.
    ///
    /// What is computed is only arithmetic on numbers the GAME printed - no game
    /// formula is reimplemented, because there is none to reimplement here.
    ///
    /// A bonus missing on one side counts as zero there, so gaining a bonus reads
    /// as its full value and losing one as its full value negated - which is what
    /// the change actually is.
    /// </summary>
    private static void AddBonusRows(List<string> rows, Span<AtkValue> values,
                                     ref int better, ref int worse, ref int compared)
    {
        var selected = ReadBonuses(values, SelectedBlock);
        var equipped = ReadBonuses(values, EquippedBlock);

        // Union of both sides, in the order the game listed them: the selected
        // item's own bonuses first (those are what the player is considering),
        // then anything only the worn piece has.
        var names = new List<string>();
        foreach (var name in selected.Keys) names.Add(name);
        foreach (var name in equipped.Keys)
            if (!selected.ContainsKey(name)) names.Add(name);

        foreach (var name in names)
        {
            var hasMine   = selected.TryGetValue(name, out var now);
            var hasTheirs = equipped.TryGetValue(name, out var was);
            compared++;

            var row = Row(name,
                          hasMine   ? now.ToString() : string.Empty,
                          hasTheirs ? was.ToString() : string.Empty);

            var diff = now - was;
            if (diff > 0)      { better++; row += AccessibilityStrings.CompareDelta(true,  diff.ToString()); }
            else if (diff < 0) { worse++;  row += AccessibilityStrings.CompareDelta(false, (-diff).ToString()); }

            rows.Add(row);
        }
    }

    /// <summary>
    /// The materia row: what is melded into each of the two pieces, and how many
    /// sockets each one has.
    ///
    /// THE SOCKET COUNT IS WHY THIS ROW EXISTS AT ALL. The first version listed
    /// only melded materia and skipped a socket that was empty on both sides -
    /// which meant an item with two empty sockets produced NO ROW, and the player
    /// could not tell that from the reader being broken. That is exactly the
    /// failure this repo forbids, and it is how the bug was reported (user,
    /// 2026-09-04: *"Materia slots don't read or I havent found any gear with them
    /// on, which is unlikely"*). The log settled it: over eight hovers,
    /// MateriaNames and MateriaValues were never populated once, while
    /// MateriaSlotCount reached 2 - sockets, nothing melded.
    ///
    /// MEASURED 2026-09-04, and the field's NAME IS MISLEADING: FFXIVClientStructs
    /// calls index 10 BlueMateriaSlotCount, but it is simply the socket count. It
    /// matched the game's own <c>Item.MateriaSlotCount</c> for all eight items in
    /// the log - Goatskin Leg Guards 2, Ash Cavalry Bow 2, Wrapped Elm Longbow 2,
    /// Foestriker's Boots 0, both rings 0, Hempen Kecks 0, Brand-new Skirt 0 -
    /// across all four blocks, with no mismatch. The sheet was read ONLY to check
    /// what the field means; the number spoken is still the window's own.
    ///
    /// WHAT IS STILL UNMEASURED: the SHAPE of the melded strings. Nothing was
    /// melded in any sample, so the two arrays have never been seen populated.
    /// They are therefore joined and passed through verbatim, never parsed and
    /// never subtracted - splitting a string whose form is a guess is exactly how
    /// a wrong number gets spoken.
    /// </summary>
    private static void AddMateriaRows(List<string> rows, Span<AtkValue> values)
    {
        var mine   = ReadMateria(values, SelectedBlock);
        var theirs = ReadMateria(values, EquippedBlock);

        // Neither piece can even take materia: there is nothing to say, and a row
        // saying so on every ordinary item would be noise.
        if (mine.Sockets == 0 && theirs.Sockets == 0 &&
            mine.Melded.Count == 0 && theirs.Melded.Count == 0) return;

        rows.Add(Row(AccessibilityStrings.CompareRowMateria, Describe(mine), Describe(theirs)));

        static string Describe((int Sockets, List<string> Melded) side)
        {
            if (side.Melded.Count > 0)
                return AccessibilityStrings.CompareMateriaMelded(string.Join(", ", side.Melded), side.Sockets);
            return side.Sockets == 0
                ? AccessibilityStrings.CompareMateriaNoSockets
                : AccessibilityStrings.CompareMateriaEmpty(side.Sockets);
        }
    }

    /// <summary>One block's materia: how many sockets, and whatever is melded
    /// into them as the game wrote it.</summary>
    private static (int Sockets, List<string> Melded) ReadMateria(Span<AtkValue> values, int block)
    {
        var melded = new List<string>();
        for (var i = 0; i < MateriaSlots; i++)
        {
            var name  = ReadString(values, block + RelMateriaName + i);
            var value = ReadString(values, block + RelMateriaValue + i);
            if (name.Length == 0 && value.Length == 0) continue;
            if (name.Length == 0)       melded.Add(value);
            else if (value.Length == 0) melded.Add(name);
            else                        melded.Add($"{name} {value}");
        }
        return (ReadInt(values, block + RelMateriaSlotCount), melded);
    }

    /// <summary>One AtkValue as a whole number, 0 when it holds anything else.</summary>
    private static int ReadInt(Span<AtkValue> values, int index)
    {
        if (index < 0 || index >= values.Length) return 0;
        var v = values[index];
        return v.Type == AtkValueType.Int ? v.Int : 0;
    }

    /// <summary>
    /// Who can wear the two pieces, and - separately - whether the player can.
    ///
    /// TWO ROWS, because they answer two different questions and only one of them
    /// is a comparison. EquippableBy exists on both blocks and is compared like
    /// any other value; but it is the game's ABBREVIATION LIST ("ARC BRD"), which
    /// a screen reader spells out letter by letter, so it is followed by the
    /// resolved full class names for the piece in the bag.
    ///
    /// That second row is one-sided on purpose. The names come from
    /// GearInfoService, which needs an item id, and the only id published here is
    /// AgentItemDetail's - the item under the CURSOR. Looking the worn piece up by
    /// its name to fill the other half would be a guess dressed as a fact.
    /// </summary>
    private void AddClassRows(List<string> rows, Span<AtkValue> values)
    {
        var mine   = ReadString(values, SelectedBlock + RelEquippableBy);
        var theirs = ReadString(values, EquippedBlock + RelEquippableBy);
        if (mine.Length > 0 || theirs.Length > 0)
            rows.Add(Row(AccessibilityStrings.CompareRowClasses, mine, theirs));

        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentItemDetail.Instance();
        if (agent == null || agent->ItemId == 0) return;

        var baseId = Dalamud.Utility.ItemUtil.GetBaseId(agent->ItemId).ItemId;
        var owners = _gearInfo.DescribeOwnClasses(baseId);
        if (owners.Length == 0) return;

        // DescribeOwnClasses returns an appendable fragment (", fuer deine Klassen
        // X"); as a row of its own it needs the joining comma taken off again.
        rows.Add(AccessibilityStrings.CompareRowYourClasses(owners.TrimStart(',', ' ')));
    }

    /// <summary>Adds a row whose two sides the game already wrote out in full
    /// ("Item Level 27"), so nothing is labelled on top. Skipped when neither
    /// side has anything.</summary>
    private static void AddVerbatimRow(List<string> rows, Span<AtkValue> values, int relIndex)
    {
        var mine   = ReadString(values, SelectedBlock + relIndex);
        var theirs = ReadString(values, EquippedBlock + relIndex);
        if (mine.Length == 0 && theirs.Length == 0) return;
        rows.Add(AccessibilityStrings.CompareRowVerbatim(
            mine.Length   > 0 ? mine   : AccessibilityStrings.CompareCellNone,
            theirs.Length > 0 ? theirs : AccessibilityStrings.CompareCellNone));
    }

    /// <summary>One labelled row: the value's name, the bag piece, the worn
    /// piece. An empty side is SAID rather than left out - a row that names only
    /// one column would leave the player guessing which one it was.</summary>
    private static string Row(string name, string mine, string theirs)
        => AccessibilityStrings.CompareRow(
            name,
            mine.Length   > 0 ? mine   : AccessibilityStrings.CompareCellNone,
            theirs.Length > 0 ? theirs : AccessibilityStrings.CompareCellNone);

    /// <summary>
    /// The bonus lines of one block as name to value. The game writes them as
    /// "&lt;Name&gt; +&lt;N&gt;" with the name possibly several words long
    /// ("Direct Hit Rate +1"), so the split is on the LAST '+'. A line in any other
    /// shape is dropped - a misparsed bonus would be a wrong number, which is worse
    /// than a missing one.
    /// </summary>
    private static Dictionary<string, int> ReadBonuses(Span<AtkValue> values, int block)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < BonusStatSlots; i++)
        {
            var line = ReadString(values, block + RelBonusStat + i);
            if (line.Length == 0) continue;

            var cut = line.LastIndexOf('+');
            if (cut <= 0 || cut == line.Length - 1) continue;

            var name = line[..cut].Trim();
            if (name.Length == 0) continue;
            if (!int.TryParse(line[(cut + 1)..].Trim(), out var value)) continue;

            // Same bonus twice in one list is not a shape the game has produced in
            // any sample; if it ever does, the values add rather than one winning.
            map[name] = map.TryGetValue(name, out var existing) ? existing + value : value;
        }
        return map;
    }

    /// <summary>
    /// Splits a difference string as the game writes it - "(+25)", "(-12)" - into
    /// direction and magnitude. null when there is no difference at all, which the
    /// game expresses by leaving the value EMPTY rather than writing zero.
    ///
    /// The magnitude is kept as TEXT and never parsed to a number: the delay of a
    /// weapon is written with decimals, and re-formatting it here would risk
    /// saying something the window does not.
    /// </summary>
    private static (bool Up, string Amount)? ParseDelta(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return null;
        if (s.StartsWith('(') && s.EndsWith(')')) s = s[1..^1].Trim();
        if (s.Length < 2) return null;

        var up = s[0] switch
        {
            '+' => true,
            '-' => false,
            _   => (bool?)null,
        };
        if (up == null) return null;          // unrecognised shape: say nothing rather than guess

        var amount = s[1..].Trim();
        return amount.Length == 0 ? null : (up.Value, amount);
    }

    /// <summary>
    /// One AtkValue as text, "" when it holds nothing readable.
    ///
    /// The pointer is checked before it is walked, for the reason AtkText exists:
    /// an AtkValue string is a bare pointer, and reading an unmapped one is an
    /// access violation that takes the whole game down rather than throwing.
    /// The SeString route also strips the payload markers item names carry.
    /// </summary>
    private static string ReadString(Span<AtkValue> values, int index)
    {
        if (index < 0 || index >= values.Length) return string.Empty;
        var v = values[index];
        if (v.Type is not (AtkValueType.String or AtkValueType.ConstString or AtkValueType.ManagedString))
            return string.Empty;
        if (!v.String.HasValue) return string.Empty;

        var p = v.String.Value;
        if (!AtkText.IsReadable(p)) return string.Empty;
        var se = Dalamud.Memory.MemoryHelper.ReadSeStringNullTerminated((nint)p);
        return TolkService.Sanitize(se.TextValue);
    }
}
