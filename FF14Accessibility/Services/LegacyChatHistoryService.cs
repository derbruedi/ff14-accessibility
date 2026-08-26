using System;
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// Die Nachlese, wie sie bis v5.83 gearbeitet hat: pro KATEGORIE ein Puffer,
/// gewechselt mit Alt+Bild-auf/-ab, geblaettert mit Umschalt+Bild-auf/-ab.
///
/// Diese Klasse ist eine wortgleiche Uebernahme des alten
/// <c>MessageHistoryService</c> von main (nur umbenannt), damit der Spieler
/// zwischen dem gewohnten und dem neuen Chatsystem aus PR #5 umschalten kann,
/// ohne dass eines von beiden nachgebaut werden muss - siehe
/// <see cref="Configuration.UseLegacyChatSystem"/>. Sie wird NICHT
/// weiterentwickelt; wer hier etwas aendert, aendert das, womit der Spieler
/// vergleichen will.
///
/// Sie laeuft immer mit, auch wenn das neue System eingeschaltet ist: gefuellt
/// wird sie in jedem Fall (<see cref="LegacyChatReaderService"/>), gesprochen
/// und auf die Tasten gelegt wird nur die eingeschaltete Seite. Dadurch hat das
/// jeweils andere System beim Umschalten keine Luecke.
///
/// <c>TellTarget</c> steht bewusst nicht hier, sondern weiterhin in
/// <see cref="MessageHistoryService"/> - beide Systeme benutzen denselben
/// Datensatz, und zwei gleichnamige Typen waeren nur eine Fehlerquelle.
/// </summary>
public sealed class LegacyChatHistoryService
{
    public enum Category { Dialogue, Say, Shout, Party, Alliance, Tell, FreeCompany, System, Loot }

    /// <summary>One archived line: the spoken text plus, for tells, who it was
    /// with (null for every other channel).</summary>
    private sealed record Entry(string Text, TellTarget? Partner);

    // Reihenfolge beim Durchschalten (vorwärts/rückwärts), wie das Plugin sie
    // ausliefert. Der Spieler darf sie im Einstellungsmenü umsortieren und
    // einzelne Kategorien ganz abschalten - siehe Order.
    private static readonly Category[] DefaultOrder =
    {
        Category.Dialogue, Category.Say, Category.Shout, Category.Party,
        Category.Alliance, Category.Tell, Category.FreeCompany, Category.System,
        Category.Loot,
    };

    /// <summary>
    /// Die Reihenfolge, in der Alt+Bild-auf/-ab durchschaltet: die des Spielers,
    /// sonst die ausgelieferte.
    ///
    /// ABGESCHALTETE KATEGORIEN FEHLEN NUR HIER. Ihr Puffer bleibt bestehen und
    /// wird weiter gefüllt (siehe <see cref="_buffers"/>) - wer eine Kategorie
    /// wieder einschaltet, findet den Verlauf der ganzen Sitzung vor und nicht
    /// eine Lücke ab dem Moment, in dem er sie abgeschaltet hatte.
    /// </summary>
    private Category[] Order
    {
        get
        {
            if (_order == null || _orderStamp != _config.OrderStamp)
            {
                _order = ListOrder
                    .Apply(DefaultOrder, static c => c.ToString(),
                           _config.LegacyChatCategoryOrder, _config.LegacyChatCategoryHidden)
                    .ToArray();
                _orderStamp = _config.OrderStamp;
                // Der Index zeigte auf eine Position der ALTEN Liste; nach dem
                // Umsortieren steht dort eine andere Kategorie. Von vorn.
                _catIndex = 0;
                _cursor = -1;
            }
            return _order;
        }
    }

    /// <summary>Die sortierte Liste ALLER Kategorien für das Einstellungsmenü -
    /// abgeschaltete eingeschlossen, sonst wären sie unerreichbar.</summary>
    public List<Category> OrderableCategories =>
        ListOrder.Sort(DefaultOrder, static c => c.ToString(), _config.LegacyChatCategoryOrder);

    // Spoken category names are bilingual and live in AccessibilityStrings
    // (LegacyChatCategoryName), so "/acc lang" switches them too.

    // No cap: the history keeps every message of the session (user request
    // 2026-08-02, previously 50 per category). A sighted player can scroll the
    // whole chat log back, so cutting the history short took away reach that
    // the game itself grants. Cost is memory only - the entries are short
    // strings, and a session's worth stays in the low megabytes.
    // Über ALLE Kategorien angelegt, nicht nur über die eingeschalteten: eine
    // abgeschaltete Kategorie wird weiter mitgeschrieben, sie wird nur nicht
    // angeboten. Siehe Order.
    private readonly Dictionary<Category, List<Entry>> _buffers = new();
    private readonly TolkService _tolk;
    private readonly Configuration _config;

    private int _catIndex;      // Index in Order der aktuell gewählten Kategorie
    private int _cursor = -1;   // zuletzt vorgelesene Nachricht, -1 = nicht am Blättern

    /// <summary>Die vom Spieler gesetzte Reihenfolge, oder null solange sie noch
    /// nie gebraucht wurde. Siehe <see cref="Order"/>.</summary>
    private Category[]? _order;

    /// <summary>Startet auf einem Wert, den kein gespeicherter Stempel treffen
    /// kann, damit der erste Zugriff in jedem Fall neu baut.</summary>
    private int _orderStamp = int.MinValue;

    public LegacyChatHistoryService(TolkService tolk, Configuration config)
    {
        _tolk = tolk;
        _config = config;
        foreach (var c in DefaultOrder) _buffers[c] = new List<Entry>();
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
        // ZUERST holen, dann rechnen. Order kann den Index zurücksetzen (nach
        // einer Umsortierung), und in "_catIndex + dir + Order.Length" wäre
        // _catIndex bereits gelesen, bevor Order das tut - der Reset ginge
        // verloren und der Sprung landete auf einer Kategorie, die der Spieler
        // nicht gewählt hat.
        var order = Order;
        _catIndex = (_catIndex + dir + order.Length) % order.Length;
        _cursor   = -1;
        LastActivity = DateTime.UtcNow;
        var cat = order[_catIndex];
        var n   = _buffers[cat].Count;
        _tolk.SpeakInterrupt(AccessibilityStrings.CategorySummary(AccessibilityStrings.LegacyChatCategoryName(cat), n));
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
        => _tolk.SpeakInterrupt(AccessibilityStrings.CategoryEmpty(AccessibilityStrings.LegacyChatCategoryName(Order[_catIndex])));

    private void Announce(List<Entry> buf)
        => _tolk.SpeakInterrupt($"{buf[_cursor].Text}, {AccessibilityStrings.Counter(_cursor + 1, buf.Count)}");
}
