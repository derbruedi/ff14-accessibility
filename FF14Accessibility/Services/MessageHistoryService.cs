using System;
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// Kategorisierter Nachlese-Speicher (V4.90): pro Kategorie ein Ringpuffer
/// der zuletzt empfangenen Nachrichten. Der Nutzer wechselt die Kategorie
/// (Strg+, / Strg+.) und blättert darin (, / .). Die Live-Ansagen laufen davon
/// unberührt weiter - hier wird nur mitgeschrieben und auf Tastendruck
/// vorgelesen, damit man Verpasstes nachlesen kann.
/// </summary>
/// <summary>
/// The other side of a tell, as the GAME delivered it: player name plus home
/// world. Kept with the archived message so answering never needs the player to
/// type "Name@Welt" - a blind player cannot look that world up anywhere, and
/// guessing it gets the command rejected (user 2026-08-02).
/// </summary>
public sealed record TellTarget(string Name, string World)
{
    /// <summary>The form the game's chat commands expect: "Vorname Nachname@Welt".</summary>
    public string CommandTarget => $"{Name}@{World}";
}

public sealed class MessageHistoryService
{
    public enum Category { Dialogue, Say, Shout, Party, Alliance, Tell, FreeCompany, System, Loot }

    /// <summary>One archived line: the spoken text plus, for tells, who it was
    /// with (null for every other channel).</summary>
    private sealed record Entry(string Text, TellTarget? Partner);

    // Reihenfolge beim Durchschalten (Strg+. vorwärts, Strg+, rückwärts).
    private static readonly Category[] Order =
    {
        Category.Dialogue, Category.Say, Category.Shout, Category.Party,
        Category.Alliance, Category.Tell, Category.FreeCompany, Category.System,
        Category.Loot,
    };

    // Spoken category names are bilingual and live in AccessibilityStrings
    // (ChatCategoryName), so "/acc lang" switches them too.

    // No cap: the history keeps every message of the session (user request
    // 2026-08-02, previously 50 per category). A sighted player can scroll the
    // whole chat log back, so cutting the history short took away reach that
    // the game itself grants. Cost is memory only - the entries are short
    // strings, and a session's worth stays in the low megabytes.
    private readonly Dictionary<Category, List<Entry>> _buffers = new();
    private readonly TolkService _tolk;

    private int _catIndex;      // Index in Order der aktuell gewählten Kategorie
    private int _cursor = -1;   // zuletzt vorgelesene Nachricht, -1 = nicht am Blättern

    public MessageHistoryService(TolkService tolk)
    {
        _tolk = tolk;
        foreach (var c in Order) _buffers[c] = new List<Entry>();
    }

    /// <summary>Adds a message to a category's ring buffer (newest last).</summary>
    /// <param name="partner">For tells, the other side as the game delivered it -
    /// this is what makes answering from the history possible.</param>
    public void Add(Category category, string text, TellTarget? partner = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_buffers.TryGetValue(category, out var buf)) return;

        // Nothing is dropped, so the cursor never has to be pulled along: an
        // entry keeps its index for the whole session.
        buf.Add(new Entry(text, partner));
    }

    /// <summary>The category currently selected for browsing.</summary>
    public Category CurrentCategory => Order[_catIndex];

    /// <summary>
    /// When the user last worked with the history (switched category or read a
    /// message). The chat key uses this: writing into the channel you were just
    /// reading is only meant to happen while that selection is FRESH - a category
    /// picked an hour ago must not silently redirect a message.
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.MinValue;

    /// <summary>Cycles to the next/previous category and announces it with count.</summary>
    public void SwitchCategory(int dir)
    {
        _catIndex = (_catIndex + dir + Order.Length) % Order.Length;
        _cursor   = -1;
        LastActivity = DateTime.UtcNow;
        var cat = Order[_catIndex];
        var n   = _buffers[cat].Count;
        _tolk.SpeakInterrupt(AccessibilityStrings.CategorySummary(AccessibilityStrings.ChatCategoryName(cat), n));
    }

    /// <summary>Reads the previous (older) message; first press reads the newest.</summary>
    public void ReadOlder()
    {
        LastActivity = DateTime.UtcNow;
        var buf = _buffers[Order[_catIndex]];
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1)      _cursor = buf.Count - 1;
        else if (_cursor > 0)   _cursor--;
        else { _tolk.SpeakInterrupt(AccessibilityStrings.HistoryStart); return; }
        Announce(buf);
    }

    /// <summary>Reads the next (newer) message in the current category.</summary>
    public void ReadNewer()
    {
        LastActivity = DateTime.UtcNow;
        var buf = _buffers[Order[_catIndex]];
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1 || _cursor >= buf.Count - 1) { _tolk.SpeakInterrupt(AccessibilityStrings.HistoryEnd); return; }
        _cursor++;
        Announce(buf);
    }

    /// <summary>
    /// The tell partner of the message currently in focus, or null when the
    /// category holds no tells. While not browsing (cursor at -1) this is the
    /// NEWEST tell, which is what "answer them" means right after switching to
    /// the category. Entries without a partner (an older line whose payload the
    /// game did not carry) are skipped rather than reported as "no target".
    /// </summary>
    public TellTarget? CurrentTellPartner
    {
        get
        {
            var buf = _buffers[Order[_catIndex]];
            if (buf.Count == 0) return null;
            var start = _cursor >= 0 && _cursor < buf.Count ? _cursor : buf.Count - 1;
            for (var i = start; i >= 0; i--)
                if (buf[i].Partner != null) return buf[i].Partner;
            return null;
        }
    }

    private void AnnounceEmpty()
        => _tolk.SpeakInterrupt(AccessibilityStrings.CategoryEmpty(AccessibilityStrings.ChatCategoryName(Order[_catIndex])));

    private void Announce(List<Entry> buf)
        => _tolk.SpeakInterrupt($"{buf[_cursor].Text}, {AccessibilityStrings.Counter(_cursor + 1, buf.Count)}");
}
