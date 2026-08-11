using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>One row of a <see cref="SpokenMenu"/>.</summary>
public sealed class MenuEntry
{
    public string Label { get; init; } = string.Empty;

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
        Push(level, announce: true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        _stack.Clear();
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
            _tolk.SpeakInterrupt(
                AccessibilityStrings.MenuOpened(level.Title) + " " +
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

        if (input.JustAny(KeysConfirm)) { Activate(); return true; }

        var c = input.JustChar();
        if (c != '\0') TypeAhead(c);

        return true;
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
