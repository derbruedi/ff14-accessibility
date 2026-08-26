using System;
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// Applies a player-defined order and hide list to a list the mod otherwise
/// ships fixed: the object browser's categories and the chat history's buffers.
///
/// WHY THIS IS SHARED CODE. Three lists needed the same treatment (world
/// categories, deep dungeon categories, chat buffers - twice, once per chat
/// system), and every one of them has the same two traps. Solving them three
/// times separately is how they end up solved differently.
///
/// TRAP ONE: THE LIST GROWS. Every mod update can add a category, and a saved
/// order written before that update does not mention it. A naive
/// "sort by the saved position" drops it - the player installs an update and
/// the new feature is invisible, with nothing to tell them why. So the rule
/// here is the other way round: the saved order decides the position of what it
/// knows, and everything it does not know KEEPS ITS SHIPPED ORDER and follows at
/// the end. Nothing can fall out of a list by being new.
///
/// TRAP TWO: HIDING EVERYTHING. The hide list is the player's, so it can name
/// every entry - and a browser with zero categories is not a configured browser,
/// it is a broken one, and it breaks silently for someone who cannot see that
/// the list is empty. <see cref="Apply"/> therefore treats "nothing left" as a
/// hide list that cannot be honoured and returns the full order instead. The
/// menu refuses to switch off the last visible entry, so this is a backstop
/// against a hand-edited config, not the normal path.
///
/// Keys are STRINGS, never enum values or indices: a saved order has to survive
/// the enum gaining a member in the middle, which is exactly what an index-based
/// order does not.
/// </summary>
public static class ListOrder
{
    /// <summary>
    /// Sorts <paramref name="all"/> by the player's saved order and drops what
    /// the player hid.
    /// </summary>
    /// <param name="all">The list as the mod ships it, in its default order.</param>
    /// <param name="key">The stable string key of an entry, as stored in the config.</param>
    /// <param name="order">Saved order, most recent first save wins. Entries it does
    /// not name keep their shipped order and follow at the end. Empty = untouched.</param>
    /// <param name="hidden">Keys the player switched off. Ignored entirely when
    /// honouring it would leave nothing.</param>
    public static List<T> Apply<T>(IReadOnlyList<T> all, Func<T, string> key,
                                   IReadOnlyList<string>? order, IReadOnlyList<string>? hidden)
    {
        var sorted = Sort(all, key, order);
        if (hidden == null || hidden.Count == 0) return sorted;

        var kept = new List<T>(sorted.Count);
        foreach (var item in sorted)
            if (!Contains(hidden, key(item))) kept.Add(item);

        // See TRAP TWO: an empty browser is indistinguishable from a broken one.
        return kept.Count > 0 ? kept : sorted;
    }

    /// <summary>
    /// Sorts without hiding - what the settings menu lists, because you have to
    /// be able to reach a switched-off entry to switch it back on.
    /// </summary>
    public static List<T> Sort<T>(IReadOnlyList<T> all, Func<T, string> key,
                                  IReadOnlyList<string>? order)
    {
        var sorted = new List<T>(all.Count);
        if (order == null || order.Count == 0)
        {
            sorted.AddRange(all);
            return sorted;
        }

        // Named entries first, in the saved order. A key the config names but the
        // list no longer has (a category removed by an update) is simply skipped -
        // the saved order is allowed to be out of date, it is not allowed to fail.
        var taken = new bool[all.Count];
        foreach (var wanted in order)
        {
            for (var i = 0; i < all.Count; i++)
            {
                if (taken[i] || !string.Equals(key(all[i]), wanted, StringComparison.Ordinal)) continue;
                sorted.Add(all[i]);
                taken[i] = true;
                break;
            }
        }

        // Then everything the saved order never heard of, in shipped order. See TRAP ONE.
        for (var i = 0; i < all.Count; i++)
            if (!taken[i]) sorted.Add(all[i]);

        return sorted;
    }

    /// <summary>Whether the player switched this key off.</summary>
    public static bool IsHidden(IReadOnlyList<string>? hidden, string key)
        => Contains(hidden, key);

    /// <summary>
    /// Records the order of <paramref name="keys"/> into <paramref name="order"/>,
    /// replacing what was there. Called by the settings menu after a move; the
    /// caller saves.
    /// </summary>
    public static void Store(List<string> order, IEnumerable<string> keys)
    {
        order.Clear();
        order.AddRange(keys);
    }

    /// <summary>
    /// Switches <paramref name="key"/> on or off in <paramref name="hidden"/> and
    /// reports the new state. The caller saves.
    /// </summary>
    /// <returns>true when the entry is now VISIBLE.</returns>
    public static bool ToggleHidden(List<string> hidden, string key)
    {
        for (var i = 0; i < hidden.Count; i++)
        {
            if (!string.Equals(hidden[i], key, StringComparison.Ordinal)) continue;
            hidden.RemoveAt(i);
            return true;
        }
        hidden.Add(key);
        return false;
    }

    private static bool Contains(IReadOnlyList<string>? list, string key)
    {
        if (list == null) return false;
        for (var i = 0; i < list.Count; i++)
            if (string.Equals(list[i], key, StringComparison.Ordinal)) return true;
        return false;
    }
}
