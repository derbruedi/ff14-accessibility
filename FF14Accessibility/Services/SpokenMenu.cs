using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>One row of a <see cref="SpokenMenu"/>.</summary>
public sealed class MenuEntry
{
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Stable identity of what this row stands for, for levels the player can
    /// reorder (<see cref="MenuLevel.Reordered"/>). Empty for every other row.
    ///
    /// It exists because the LABEL cannot carry that identity: labels are
    /// translated and can change with "/acc lang" between two sessions, and a
    /// saved order keyed by a translated label would silently stop matching.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// How this row is named when it is mentioned as somebody ELSE'S NEIGHBOUR
    /// while reordering ("between merchants and players"). Falls back to
    /// <see cref="Label"/> when empty.
    ///
    /// It exists because a row's own label may carry its state, and a state
    /// tacked onto a neighbour's name lands on the wrong subject: the sorting
    /// list writes a switched-off category as "fishing spots, off", so the
    /// neighbour sentence would come out as "enemies, now 3 of 21, between
    /// merchants and fishing spots, off" - and that trailing "off" reads as if it
    /// described the row being carried.
    /// </summary>
    public string NeighbourLabel { get; init; } = string.Empty;

    /// <summary>Builds the child level when chosen. null for a leaf.</summary>
    public Func<MenuLevel>? Submenu { get; init; }

    /// <summary>What a leaf does when chosen. null when this is a submenu.</summary>
    public Action? Activate { get; init; }

    /// <summary>
    /// Leaf only: keep the menu open and REBUILD the current level afterwards,
    /// so the row's own label reflects what just changed (used by the
    /// show/hide-hotbars list, where you want to flip several in a row and hear
    /// the new state each time).
    /// </summary>
    public bool StayOpen { get; init; }
}

/// <summary>A titled list of entries, plus the builder that produced it.</summary>
public sealed class MenuLevel
{
    public string Title { get; init; } = string.Empty;
    public List<MenuEntry> Entries { get; init; } = new();

    /// <summary>Rebuilds this level in place after a <see cref="MenuEntry.StayOpen"/> action.</summary>
    public Func<MenuLevel>? Rebuild { get; init; }

    /// <summary>
    /// When set, this level's rows can be PICKED UP AND MOVED, and confirm on a
    /// row means "pick up" rather than "activate" - see <see cref="SpokenMenu"/>.
    /// Called after every single move with the level's rows in their new order,
    /// so the caller stores and saves.
    ///
    /// Called on every step rather than once on drop: a player who alt-tabs away
    /// or whose game crashes mid-move should not lose the sorting they just did,
    /// and there is no cost worth counting to writing a list of a few dozen
    /// strings.
    /// </summary>
    public Action<IReadOnlyList<MenuEntry>>? Reordered { get; init; }

    /// <summary>
    /// Spoken once when the level is ENTERED, between the title and the first
    /// row. For a level whose keys do not work the way every other level's keys
    /// work - which right now means the reorder levels, where confirm picks up
    /// instead of activating.
    ///
    /// Deliberately not spoken when returning from a submenu: an explanation you
    /// have to sit through every time you back out of something stops being an
    /// explanation and becomes an obstacle.
    /// </summary>
    public string Intro { get; init; } = string.Empty;

    internal int Cursor;
}

/// <summary>
/// Edge detection and consumption for the menu's own keys.
///
/// The plugin's own <c>IsJustPressed</c> cannot be used here: its VK table does
/// not contain Numpad0, Numpad-Del or the letters, and while a menu is open we
/// need to CONSUME the keys so the game's cursor does not move underneath us.
/// Dalamud's <c>IKeyState</c> indexer is settable, which is the supported way to
/// take a keystroke away from the game.
/// </summary>
public sealed class MenuInput
{
    private readonly IKeyState _keyState;
    private readonly IPluginLog _log;
    private readonly List<int> _tracked = new();
    private readonly bool[] _was = new bool[256];
    private readonly bool[] _down = new bool[256];
    private readonly bool[] _just = new bool[256];

    public MenuInput(IKeyState keyState, IPluginLog log, IEnumerable<int> vks)
    {
        _keyState = keyState;
        _log = log;

        var skipped = new List<int>();
        foreach (var vk in vks)
        {
            // Reading a VK the game does not index throws - ask first, like the
            // plugin's own UpdateKeyEdges does.
            if (_keyState.IsVirtualKeyValid(vk)) _tracked.Add(vk);
            else skipped.Add(vk);
        }

        if (skipped.Count > 0)
            _log.Info($"[Menü] Nicht verfügbare Tasten übersprungen: {string.Join(", ", skipped.ConvertAll(v => $"0x{v:X2}"))}");
    }

    /// <summary>Samples every tracked key once. Call at the top of the frame.</summary>
    public void Poll()
    {
        foreach (var vk in _tracked)
        {
            var down = _keyState[vk];
            _just[vk] = down && !_was[vk];
            _was[vk] = down;
            _down[vk] = down;
        }
    }

    public bool Just(int vk) => vk is >= 0 and < 256 && _just[vk];

    public bool JustAny(params int[] vks)
    {
        foreach (var vk in vks)
            if (Just(vk)) return true;
        return false;
    }

    /// <summary>The first just-pressed letter or digit, or '\0'. For type-ahead.</summary>
    public char JustChar()
    {
        for (var vk = 'A'; vk <= 'Z'; vk++)
            if (Just(vk)) return vk;
        for (var vk = '0'; vk <= '9'; vk++)
            if (Just(vk)) return vk;
        return '\0';
    }

    /// <summary>
    /// Takes every tracked key that is currently down away from the game. Called
    /// only while a menu is open, so nothing outside the menu changes behaviour.
    /// </summary>
    public void ConsumeAll()
    {
        foreach (var vk in _tracked)
            if (_down[vk]) _keyState[vk] = false;
    }
}

/// <summary>
/// A spoken, keyboard-driven menu: a stack of levels the player walks with the
/// NUMPAD, which is where their hand already sits for every other menu in the
/// game. Replaces the old "press the key N times to cycle to entry N" browsers -
/// overshooting costs one keypress instead of a full lap, and backing out of a
/// level returns the cursor to the entry it came from.
///
/// Both a numpad VK and its navigation-key twin are accepted for every action.
/// With NumLock OFF the keyboard driver delivers Numpad8 as VK_UP and Numpad-Del
/// as VK_DELETE, so binding only one of each would leave the menu dead depending
/// on a state the player cannot see. This is the same class of trap the repo
/// already documents for Umschalt+numpad in Configuration.cs.
/// </summary>
public sealed class SpokenMenu
{
    // Numpad8 / Up
    private static readonly int[] KeysUp = { 0x68, 0x26 };
    // Numpad2 / Down
    private static readonly int[] KeysDown = { 0x62, 0x28 };
    // Numpad4 / Left - one level back
    private static readonly int[] KeysBack = { 0x64, 0x25 };
    // Numpad0 / Numpad6 / Right - confirm or descend. Public because the same
    // keys are what OPENS a menu on a focused skill.
    //
    // Return (0x0D) was removed 2026-08-02: Enter belongs to chat and the game
    // consumes it, so accepting it here meant one press both confirmed a menu
    // entry AND opened the chat box. The three remaining keys cover the same
    // action, and Numpad0 is already the user's confirm.
    public static readonly int[] KeysConfirm = { 0x60, 0x66, 0x27 };
    // Numpad-Del (VK_DECIMAL) / Entf / Escape - close the whole menu
    private static readonly int[] KeysClose = { 0x6E, 0x2E, 0x1B };
    // Home / End
    private static readonly int[] KeysFirst = { 0x24 };
    private static readonly int[] KeysLast = { 0x23 };

    /// <summary>Every VK the menu ever reads, for <see cref="MenuInput"/>.</summary>
    public static IEnumerable<int> AllKeys()
    {
        foreach (var set in new[] { KeysUp, KeysDown, KeysBack, KeysConfirm, KeysClose, KeysFirst, KeysLast })
            foreach (var vk in set) yield return vk;
        for (var c = 'A'; c <= 'Z'; c++) yield return c;
        for (var c = '0'; c <= '9'; c++) yield return c;
    }

    private readonly TolkService _tolk;
    private readonly IPluginLog _log;
    private readonly List<MenuLevel> _stack = new();

    /// <summary>Index of the row the player is currently carrying on the top
    /// level, or -1 when nothing is picked up. See <see cref="MenuLevel.Reordered"/>.</summary>
    private int _grabbed = -1;

    public SpokenMenu(TolkService tolk, IPluginLog log)
    {
        _tolk = tolk;
        _log = log;
    }

    public bool IsOpen => _stack.Count > 0;

    private MenuLevel Current => _stack[^1];

    /// <summary>Opens a fresh menu at <paramref name="level"/>.</summary>
    public void Open(MenuLevel level)
    {
        _stack.Clear();
        _grabbed = -1;
        Push(level, announce: true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        _stack.Clear();
        _grabbed = -1;
        _tolk.SpeakInterrupt(AccessibilityStrings.MenuClosed);
        _log.Info("[Menü] geschlossen.");
    }

    private void Push(MenuLevel level, bool announce)
    {
        _stack.Add(level);
        if (level.Entries.Count == 0)
        {
            _tolk.SpeakInterrupt($"{level.Title}. {AccessibilityStrings.MenuEmpty}");
            _log.Info($"[Menü] '{level.Title}' ist leer.");
            return;
        }
        if (announce)
        {
            _log.Info($"[Menü] '{level.Title}' mit {level.Entries.Count} Einträgen geöffnet.");
            var intro = level.Intro.Length > 0 ? level.Intro + " " : string.Empty;
            _tolk.SpeakInterrupt(
                AccessibilityStrings.MenuOpened(level.Title) + " " + intro +
                AccessibilityStrings.MenuEntry(level.Entries[level.Cursor].Label, level.Cursor + 1, level.Entries.Count));
        }
    }

    /// <summary>
    /// Handles one frame of input. Returns true when a menu is open, which the
    /// caller uses to keep every other mod hotkey quiet.
    /// </summary>
    public bool HandleKeys(MenuInput input)
    {
        if (!IsOpen) return false;

        // Take the keys away from the game FIRST, so even a frame we decide to
        // ignore never leaks a cursor move into the window underneath.
        input.ConsumeAll();

        // WHILE A ROW IS PICKED UP, every key means something else, and no key
        // leaves the level. Handled before anything below, so there is no path
        // out of the menu that strands the player holding a row - back and close
        // put it down instead of closing, and the player presses again to leave.
        // Leaving with a row in hand is a state nobody can see they are in.
        if (_grabbed >= 0) { HandleGrabbedKeys(input); return true; }

        if (input.JustAny(KeysClose)) { Close(); return true; }

        if (input.JustAny(KeysBack))
        {
            if (_stack.Count <= 1) { Close(); return true; }
            _stack.RemoveAt(_stack.Count - 1);
            SpeakCursor(withTitle: true);
            return true;
        }

        var level = Current;
        if (level.Entries.Count == 0)
        {
            // Nothing to move through; only back/close apply, handled above.
            return true;
        }

        if (input.JustAny(KeysUp)) { Move(-1); return true; }
        if (input.JustAny(KeysDown)) { Move(+1); return true; }
        if (input.JustAny(KeysFirst)) { level.Cursor = 0; SpeakCursor(false); return true; }
        if (input.JustAny(KeysLast)) { level.Cursor = level.Entries.Count - 1; SpeakCursor(false); return true; }

        if (input.JustAny(KeysConfirm))
        {
            // On a reorderable level, confirm PICKS UP instead of activating.
            // The rows there stand for a position in a list, not for an action -
            // there is nothing else confirm could sensibly do.
            if (level.Reordered != null) Grab();
            else Activate();
            return true;
        }

        var c = input.JustChar();
        if (c != '\0') TypeAhead(c);

        return true;
    }

    /// <summary>
    /// One frame of input while a row is picked up. Up/down MOVE the row (the
    /// cursor travels with it, because the cursor and the row are the same thing
    /// now), Home/End send it to either end, and everything else puts it down.
    /// </summary>
    private void HandleGrabbedKeys(MenuInput input)
    {
        var level = Current;

        if (input.JustAny(KeysUp))    { MoveGrabbed(_grabbed - 1); return; }
        if (input.JustAny(KeysDown))  { MoveGrabbed(_grabbed + 1); return; }
        if (input.JustAny(KeysFirst)) { MoveGrabbed(0); return; }
        if (input.JustAny(KeysLast))  { MoveGrabbed(level.Entries.Count - 1); return; }

        // Confirm, back and close all mean the same thing here. Three keys for
        // one action is right when the alternative is a player pressing the key
        // they always press to get out and staying stuck.
        if (input.JustAny(KeysConfirm) || input.JustAny(KeysBack) || input.JustAny(KeysClose)) Drop();

        // Type-ahead is deliberately dead while carrying a row: it moves the
        // CURSOR, and the cursor is the row. A stray letter would silently
        // teleport what the player is holding.
    }

    /// <summary>Picks up the row under the cursor.</summary>
    private void Grab()
    {
        var level = Current;
        _grabbed = level.Cursor;
        var entry = level.Entries[_grabbed];
        _log.Info($"[Menü] '{entry.Label}' aufgenommen (Platz {_grabbed + 1} von {level.Entries.Count}).");
        _tolk.SpeakInterrupt(AccessibilityStrings.MenuGrabbed(entry.Label, _grabbed + 1, level.Entries.Count)
                             + Neighbours(_grabbed));
    }

    /// <summary>
    /// Which rows the carried one currently sits between, as a sentence to append
    /// to a move announcement - " zwischen Händler und Spieler.".
    ///
    /// WHY THE NUMBER ALONE WAS NOT ENOUGH (user, 2026-08-26: "es wäre schön wenn
    /// man sieht was aktuell auf dem platz ist wo man es ablegen will"). Sorting
    /// is not done by position, it is done by relation - "enemies should come
    /// right after everything". "Now 3 of 21" answers a question nobody is
    /// asking, and to answer the real one the player had to drop the row, walk
    /// the list to see where they had landed, and pick it up again.
    ///
    /// Both neighbours, not just the one just jumped over, because the two
    /// directions ask different things: moving up, the interesting row is the one
    /// now above; moving down, it is the one now below - the next one that will
    /// be jumped. Naming both answers either without the player having to know
    /// which way they were going.
    ///
    /// The ends say only what is actually there. "1 of 21" already means the
    /// front, so adding "at the front" would be the same fact twice.
    /// </summary>
    private string Neighbours(int at)
    {
        var entries = Current.Entries;
        var before = at > 0 ? NameOf(entries[at - 1]) : string.Empty;
        var after = at < entries.Count - 1 ? NameOf(entries[at + 1]) : string.Empty;
        return AccessibilityStrings.MenuBetween(before, after);
    }

    /// <summary>The row's name for a neighbour sentence - see
    /// <see cref="MenuEntry.NeighbourLabel"/>.</summary>
    private static string NameOf(MenuEntry entry)
        => entry.NeighbourLabel.Length > 0 ? entry.NeighbourLabel : entry.Label;

    /// <summary>
    /// Moves the carried row to <paramref name="to"/>, or says the list ended.
    ///
    /// Deliberately does NOT wrap, unlike cursor movement: wrapping a cursor
    /// costs one keypress to undo, wrapping a row you are carrying flings it to
    /// the far end of a list you cannot see. Hitting the end says so and leaves
    /// the row where it is - Home and End are there for the long jump.
    /// </summary>
    private void MoveGrabbed(int to)
    {
        var level = Current;
        var n = level.Entries.Count;
        if (to < 0 || to >= n || to == _grabbed)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.MenuMoveEnd(
                level.Entries[_grabbed].Label, _grabbed + 1, n));
            return;
        }

        var entry = level.Entries[_grabbed];
        level.Entries.RemoveAt(_grabbed);
        level.Entries.Insert(to, entry);
        _grabbed = to;
        level.Cursor = to;

        // Stored on every step, not on drop - see MenuLevel.Reordered.
        try
        {
            level.Reordered?.Invoke(level.Entries);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[Menü] Reihenfolge in '{level.Title}' konnte nicht gespeichert werden.");
        }

        _tolk.SpeakInterrupt(AccessibilityStrings.MenuMovedTo(entry.Label, to + 1, n) + Neighbours(to));
    }

    /// <summary>Puts the carried row down where it is.</summary>
    private void Drop()
    {
        var level = Current;
        var entry = level.Entries[_grabbed];
        var at = _grabbed;
        _log.Info($"[Menü] '{entry.Label}' abgelegt auf Platz {at + 1}.");
        // Auch beim Ablegen die Nachbarn: das ist die Bestaetigung, wo die Zeile
        // endgueltig liegt, und sie muss die Frage beantworten, die den Spieler
        // ueberhaupt zum Verschieben gebracht hat - nicht die Platznummer.
        var line = AccessibilityStrings.MenuDropped(entry.Label, at + 1) + Neighbours(at);
        _grabbed = -1;
        _tolk.SpeakInterrupt(line);
    }

    private void Move(int direction)
    {
        var level = Current;
        var n = level.Entries.Count;
        level.Cursor = ((level.Cursor + direction) % n + n) % n;
        SpeakCursor(false);
    }

    /// <summary>Jumps to the next entry starting with <paramref name="c"/>,
    /// wrapping. Silent when nothing matches, so a stray keypress is harmless.</summary>
    private void TypeAhead(char c)
    {
        var level = Current;
        var n = level.Entries.Count;
        for (var i = 1; i <= n; i++)
        {
            var idx = (level.Cursor + i) % n;
            var label = level.Entries[idx].Label;
            if (label.Length > 0 && char.ToUpperInvariant(label[0]) == char.ToUpperInvariant(c))
            {
                level.Cursor = idx;
                SpeakCursor(false);
                return;
            }
        }
    }

    private void Activate()
    {
        var level = Current;
        var entry = level.Entries[level.Cursor];

        if (entry.Submenu != null)
        {
            // try-catch: submenu builders read live game state (hotbar module,
            // sheets). A throw here must not take the frame - and therefore the
            // whole plugin - down with it.
            MenuLevel child;
            try
            {
                child = entry.Submenu();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"[Menü] Untermenü '{entry.Label}' konnte nicht gebaut werden.");
                _tolk.SpeakInterrupt(AccessibilityStrings.MenuEmpty);
                return;
            }
            Push(child, announce: true);
            return;
        }

        if (entry.Activate == null) return;

        try
        {
            entry.Activate();
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[Menü] Aktion '{entry.Label}' ist fehlgeschlagen.");
            return;
        }

        if (!entry.StayOpen)
        {
            // The action speaks its own result; closing silently avoids talking
            // over it with "Menü geschlossen".
            _stack.Clear();
            return;
        }

        // Rebuild in place so the row now reports its new state.
        if (level.Rebuild == null) return;
        try
        {
            var fresh = level.Rebuild();
            fresh.Cursor = Math.Min(level.Cursor, Math.Max(0, fresh.Entries.Count - 1));
            _stack[^1] = fresh;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[Menü] '{level.Title}' konnte nicht neu aufgebaut werden.");
        }
    }

    private void SpeakCursor(bool withTitle)
    {
        var level = Current;
        if (level.Entries.Count == 0)
        {
            _tolk.SpeakInterrupt($"{level.Title}. {AccessibilityStrings.MenuEmpty}");
            return;
        }
        var line = AccessibilityStrings.MenuEntry(level.Entries[level.Cursor].Label, level.Cursor + 1, level.Entries.Count);
        _tolk.SpeakInterrupt(withTitle ? $"{level.Title}. {line}" : line);
    }
}
