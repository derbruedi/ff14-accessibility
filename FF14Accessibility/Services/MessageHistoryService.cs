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
public sealed class MessageHistoryService
{
    public enum Category { Dialogue, Say, Shout, Party, Alliance, Tell, FreeCompany, System }

    // Reihenfolge beim Durchschalten (Strg+. vorwärts, Strg+, rückwärts).
    private static readonly Category[] Order =
    {
        Category.Dialogue, Category.Say, Category.Shout, Category.Party,
        Category.Alliance, Category.Tell, Category.FreeCompany, Category.System,
    };

    private static readonly Dictionary<Category, string> Names = new()
    {
        [Category.Dialogue]    = "Dialoge",
        [Category.Say]         = "Sagen",
        [Category.Shout]       = "Rufen",
        [Category.Party]       = "Gruppe",
        [Category.Alliance]    = "Allianz",
        [Category.Tell]        = "Flüstern",
        [Category.FreeCompany] = "Freie Gesellschaft",
        [Category.System]      = "System",
    };

    private const int Max = 50;
    private readonly Dictionary<Category, List<string>> _buffers = new();
    private readonly TolkService _tolk;

    private int _catIndex;      // Index in Order der aktuell gewählten Kategorie
    private int _cursor = -1;   // zuletzt vorgelesene Nachricht, -1 = nicht am Blättern

    public MessageHistoryService(TolkService tolk)
    {
        _tolk = tolk;
        foreach (var c in Order) _buffers[c] = new List<string>();
    }

    /// <summary>Adds a message to a category's ring buffer (newest last).</summary>
    public void Add(Category category, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!_buffers.TryGetValue(category, out var buf)) return;

        buf.Add(text);
        if (buf.Count > Max)
        {
            buf.RemoveAt(0);
            // Cursor der GERADE gewählten Kategorie nachziehen, damit er beim
            // aktiven Blättern nicht auf eine andere Nachricht rutscht.
            if (category == Order[_catIndex] && _cursor >= 0)
                _cursor = Math.Max(-1, _cursor - 1);
        }
    }

    /// <summary>Cycles to the next/previous category and announces it with count.</summary>
    public void SwitchCategory(int dir)
    {
        _catIndex = (_catIndex + dir + Order.Length) % Order.Length;
        _cursor   = -1;
        var cat = Order[_catIndex];
        var n   = _buffers[cat].Count;
        _tolk.SpeakInterrupt(n == 0
            ? $"{Names[cat]}, leer"
            : $"{Names[cat]}, {n} {(n == 1 ? "Nachricht" : "Nachrichten")}");
    }

    /// <summary>Reads the previous (older) message; first press reads the newest.</summary>
    public void ReadOlder()
    {
        var buf = _buffers[Order[_catIndex]];
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1)      _cursor = buf.Count - 1;
        else if (_cursor > 0)   _cursor--;
        else { _tolk.SpeakInterrupt("Anfang des Verlaufs."); return; }
        Announce(buf);
    }

    /// <summary>Reads the next (newer) message in the current category.</summary>
    public void ReadNewer()
    {
        var buf = _buffers[Order[_catIndex]];
        if (buf.Count == 0) { AnnounceEmpty(); return; }

        if (_cursor == -1 || _cursor >= buf.Count - 1) { _tolk.SpeakInterrupt("Ende des Verlaufs."); return; }
        _cursor++;
        Announce(buf);
    }

    private void AnnounceEmpty()
        => _tolk.SpeakInterrupt($"{Names[Order[_catIndex]]}, leer");

    private void Announce(List<string> buf)
        => _tolk.SpeakInterrupt($"{_cursor + 1} von {buf.Count}: {buf[_cursor]}");
}
