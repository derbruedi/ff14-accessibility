using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// DAS FENSTER "CHARAKTERINFO" DES TIEFEN GEWOELBES
/// (<c>DeepDungeonStatus</c>), das seinen Titel ansagte und dann verstummte.
///
/// WARUM ES VERSTUMMTE, aus dem Auszug des Fensters selbst (2026-08-12, 97 Knoten):
/// fast jeder Abschnitt besteht aus SYMBOLEN OHNE TEXT. Zwei Zeilen tragen eigenen Text
/// ("Aetherpool Armor", "Aetherpool Arm"); die 16 Gegenstands-Plaetze, die 3
/// Magizit-Plaetze und die 16 Wirkungs-Plaetze sind <c>Comp(1007)</c>- /
/// <c>Comp(1008)</c>-Knoten, deren einziges Kind ein Bild ist. Im Baum steht nichts zu
/// lesen.
///
/// ALSO WIRD DAS FENSTER AUS DEN QUELLEN GELESEN, AUS DENEN ES GEZEICHNET WIRD: der
/// Content-Director fuer den Inhalt jedes Platzes (<c>Items[i].Count / IsUsable /
/// IsActive</c>, <c>Magicite</c>, <c>WeaponLevel</c>, <c>ArmorLevel</c>,
/// <c>HoardCount</c>, <c>Floor</c>) und die spieleigenen Sheets fuer jeden Namen und
/// jeden Tooltip, aufgeloest ueber <c>DeepDungeon[id].PomanderSlot[i]</c> - die Tabelle
/// je Gewoelbe, in der derselbe Platz in jedem der vier Tiefen Gewoelbe ein ANDERER
/// Pomander ist.
///
/// <c>AgentDeepDungeonStatus.Data</c> wird NICHT gelesen, und das ist eine
/// Absturzbehebung und keine Geschmacksfrage - siehe den Block ueber
/// <see cref="Pomander"/>.
///
/// DIE ZWEI STEHENDEN AUFLAGEN SIND EINGEBAUT, NICHT ANGESCHRAUBT:
/// <list type="bullet">
/// <item><b>Nur das aktive Fenster.</b> Alles hier haengt am Fensternamen, den der
///   Aufrufer aus der Wurzel des FOKUSSIERTEN KNOTENS aufloest. <c>GetAddonByName</c>
///   wird nie benutzt - das gibt den ersten Treffer zurueck, und der kann ein geladener,
///   aber toter Zwilling sein; es gibt inzwischen vier Tiefe Gewoelbe, und jedes oeffnet
///   seine eigene Charakterinfo.</item>
/// <item><b>Der Titel beim Oeffnen, und sonst nichts.</b> Hier laeuft beim Oeffnen
///   nichts. Der Platz-Leser antwortet auf einen FOKUSWECHSEL, die volle Ausgabe auf
///   einen TASTENDRUCK - das Fenster oeffnet also weiterhin mit seinem Namen und
///   Stille.</item>
/// </list>
/// </summary>
public sealed class DeepDungeonPanel
{
    /// <summary>Das Fenster, fuer das diese Klasse spricht.</summary>
    public const string AddonName = "DeepDungeonStatus";

    /// <summary>
    /// ULD-Komponenten-Ids der Platzgruppen des Fensters, direkt aus dessen eigenem
    /// Auszug (2026-08-12): 1006 = die beiden Ausruestungszeilen, 1007 = ein Gegenstands-
    /// oder Magizit-Platz, 1008 = ein Wirkungs-Platz, 1009 = eine sakramentale Gabe.
    ///
    /// Ein Komponentenknoten traegt seine ULD-Id als ROHEN <c>Type</c> (Werte >= 1000;
    /// <c>NodeType.Component</c> = 10000 ist nur das, was <c>GetNodeType()</c>
    /// zurueckgibt). Zu welchem ABSCHNITT eine 1007 gehoert, ist nicht fest verdrahtet:
    /// das entscheidet die GROESSE der Gruppe, denn Items hat 16 Plaetze und Magicite 3.
    /// </summary>
    private const int CompItemOrMagicite = 1007;
    private const int CompItemEffect     = 1008;

    private readonly IDataManager     _data;
    private readonly IPluginLog       _log;
    private readonly DeepDungeonState _state;
    private readonly IObjectTable     _objectTable;
    private readonly DeepDungeonText  _text;

    public DeepDungeonPanel(IDataManager data, IPluginLog log, DeepDungeonState state,
                            IObjectTable objectTable, DeepDungeonText text)
    {
        _data        = data;
        _log         = log;
        _state       = state;
        _objectTable = objectTable;
        _text        = text;
    }

    // ── Was in einem Platz steckt: der DIRECTOR plus die spieleigenen Sheets, nie der Agent ──
    //
    // DIESE KLASSE FASST AgentDeepDungeonStatus.Data BEWUSST NICHT AN, UND DAS IST EINE
    // ABSTURZBEHEBUNG, keine Stilfrage. Sie hat die Platznamen frueher aus den
    // `Utf8String Name`-Feldern dieser Struktur gelesen, und das hat das Spiel in dem
    // Moment hart abstuerzen lassen, in dem das Fenster im Palast der Toten geoeffnet
    // wurde (User 2026-08-12; das Plugin-Log bricht um 10:05:17.256 ohne verwaltete
    // Ausnahme ab, und das ist die Signatur einer Zugriffsverletzung und nicht die eines
    // .NET-Fehlers).
    //
    // DER MECHANISMUS, aus FFXIVClientStructs gelesen statt geraten:
    //
    //     public readonly int Length => Math.Max(0, (int)(BufUsed - 1));
    //     public ReadOnlySpan<byte> AsSpan() => new((byte*)StringPtr, Length);
    //     public override string ToString() => AsSpan().IsEmpty ? "" : Encoding.UTF8.GetString(AsSpan());
    //
    // `AsSpan` prueft NICHTS. Bei einem Platz, den der Agent nicht befuellt hat, ist
    // StringPtr null und BufUsed das, was gerade in diesem Speicher stand - ToString
    // liest also eine beliebige Laenge ab Adresse null. Ein try-catch rettet das nicht:
    // eine Zugriffsverletzung im nativen Speicher ist keine fangbare .NET-Ausnahme, der
    // Prozess stirbt einfach.
    //
    // DIESES REPO WUSSTE DAS BEREITS. <see cref="AtkText"/> wurde am 2026-07-20 nach
    // demselben Absturz geschrieben (viermal in 25 Minuten), traegt denselben
    // dekompilierten Beleg und ist der eine abgesicherte Weg, hier einen Utf8String zu
    // lesen. Der Fehler war, ihn zu umgehen. Alles in dieser Datei, was je Spieltext
    // liest, geht ueber AtkText; alles andere kommt aus Excel, das ueberhaupt nicht
    // fehlschlagen kann.
    //
    // Jede Zeichenkette unten kommt also aus Excel und jede Zahl aus dem Content-Director
    // - genau der zuvor belegte Weg. Nichts hier dereferenziert einen Zeiger, den das
    // Spiel vielleicht nicht gefuellt hat.

    /// <summary>Was das Spiel in einen Platz des Fensters legt.</summary>
    /// <param name="Name">Der spieleigene Name, oder "", wenn der Platz ungenutzt ist.</param>
    /// <param name="Description">Der spieleigene Tooltip, oder "".</param>
    /// <param name="Count">Wie viele der Spieler hat - 0 ist eine echte Antwort, nicht "keiner".</param>
    /// <param name="Usable">Ob das Spiel die Benutzung gerade erlaubt.</param>
    /// <param name="Active">Ob die Wirkung laeuft.</param>
    public readonly record struct Slot(string Name, string Description, int Count, bool Usable, bool Active);

    /// <summary>
    /// Der Pomander, zu dem ein Platz gehoert, mit dem, was der Director darueber sagt.
    ///
    /// Der Platz loest ueber <c>DeepDungeon[id].PomanderSlot[index]</c> auf - die
    /// spieleigene Tabelle je Gewoelbe - weil DERSELBE Platz in jedem der vier Tiefen
    /// Gewoelbe ein anderer Pomander ist. Ein Platz, den die Tabelle auf Zeile 0 laesst,
    /// ist einer, den das Gewoelbe nicht benutzt, und das wird gesagt statt geschwiegen.
    /// </summary>
    private unsafe Slot? Pomander(int index)
    {
        var dd = _state.GetDirector();
        if (dd == null) return null;

        var items = dd->Items;
        if (index < 0 || index >= items.Length) return null;

        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dd->DeepDungeonId);
        if (dungeon is not { } d || index >= d.PomanderSlot.Count)
            return new Slot(string.Empty, string.Empty, items[index].Count,
                            items[index].IsUsable, items[index].IsActive);

        var row = d.PomanderSlot[index].ValueNullable;
        var name = row is { } r && r.RowId != 0 ? r.Name.ExtractText().Trim() : string.Empty;
        var tip  = row is { } r2 && r2.RowId != 0 ? _text.Read(r2.Tooltip) : string.Empty;

        return new Slot(name, tip, items[index].Count, items[index].IsUsable, items[index].IsActive);
    }

    /// <summary>
    /// Das Magizit, zu dem ein Platz gehoert. Gleiche Form und gleiche Quelle wie bei den
    /// Pomandern: <c>DeepDungeon[id].MagiciteSlot[index]</c>, aufgeloest gegen
    /// <c>DeepDungeonMagicStone</c>. Nur der Himmelsberg und Eureka Orthos fuellen diese
    /// Tabelle, eine leere Zeile ist hier also normal und kein Fehlgriff.
    /// </summary>
    private unsafe Slot? Magicite(int index)
    {
        var dd = _state.GetDirector();
        if (dd == null) return null;

        var stones = dd->Magicite;
        var count  = index >= 0 && index < stones.Length ? stones[index] : (byte)0;

        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dd->DeepDungeonId);
        if (dungeon is not { } d || index < 0 || index >= d.MagiciteSlot.Count)
            return new Slot(string.Empty, string.Empty, count, false, false);

        var rowId = d.MagiciteSlot[index].RowId;
        var stone = rowId == 0 ? null : _data.GetExcelSheet<DeepDungeonMagicStone>()?.GetRowOrDefault(rowId);
        return stone is { } s
            ? new Slot(s.Name.ExtractText().Trim(), _text.Read(s.Tooltip), count, false, false)
            : new Slot(string.Empty, string.Empty, count, false, false);
    }

    // ── Fokus: einen Platz benennen, auf dem der Cursor gelandet ist ──

    /// <summary>
    /// Benennt den nur aus Symbolen bestehenden Platz, auf dem der Fokus steht, oder gibt
    /// <paramref name="text"/> unveraendert zurueck.
    ///
    /// Wird aus dem Fokus-Leser als LETZTER Schritt aufgerufen, beansprucht also immer nur
    /// einen Platz, der sonst stumm waere. Diese Reihenfolge ist wichtig: das Spiel bindet
    /// an manche dieser Bedienelemente einen Tooltip, und wo es das tut, gewinnen die
    /// Worte des Spiels und das hier laeuft nie.
    ///
    /// Der Platz-INDEX ist die Stellung des Knotens unter seinen Geschwistern derselben
    /// Komponenten-Id, nach aufsteigender Knoten-Id. Die Knoten-Ids des Fensters laufen je
    /// Abschnitt in einem ununterbrochenen Block (Gegenstaende 19-34, Magizite 57-59,
    /// Wirkungen 67-82 im Auszug vom 2026-08-12), das ist also die Ordnung des Fensters
    /// selbst und keine Vermutung ueber das Layout - und die gesprochene Zeile traegt die
    /// Position ("3 von 16"), so wird dem Spieler nie ein Name genannt, ohne dass er auch
    /// erfaehrt, aus welchem Platz er kommt.
    /// </summary>
    public unsafe string NameSlot(AtkResNode* node, string text, string addonName)
    {
        if (node == null || addonName != AddonName) return text;

        // Die AUSRUESTUNGS-Zeilen sind der eine Teil dieses Fensters, der schon spricht:
        // sie tragen "Aetherpool Arm" / "Aetherpool Armor" als echten Text. Was sie NICHT
        // tragen, ist die Staerke, die das Fenster ins Symbol zeichnet - und die Staerke
        // ist der ganze Grund, auf die Zeile zu schauen. Dieser Zweig ERGAENZT also, statt
        // zu ersetzen, und er laeuft auch, wenn der Text nicht leer ist.
        var gear = Augment(node, text);
        if (gear != null) return Dedup(node, gear);

        if (!string.IsNullOrEmpty(text)) return text;

        // Der Fokus sitzt auf einem Collision-Knoten INNERHALB des Platzes, und ein
        // Gegenstands-Platz schachtelt noch eine Komponente (Comp(1007) -> Comp(1004)
        // Schaltflaeche -> Collision). Der Aufstieg sucht deshalb gezielt eine
        // Platz-Komponente und nicht die naechstbeste Komponente irgendeiner Art - beim
        // ersten Halt waere man auf der Schaltflaeche gelandet.
        var component = ClimbToSlotComponent(node);
        if (component == null)
        {
            // Jedes Bedienelement dieses Fensters, das diese Klasse nicht kennt, wird ins
            // Log BESCHRIEBEN, statt einen Namen zu bekommen, den diese Datei erfunden
            // haette. (Der "Platz unter der Ausruestung", nach dem der User am 2026-08-12
            // fragte, entpuppte sich als gewoehnlicher Pomander-Platz, der jetzt als
            // "leerer Platz" gelesen wird - das hier bleibt fuer das Naechste.)
            LogUnknownControl(node);
            return text;
        }

        var slot = FocusedSlot(component, out var index, out var count, out var isEffect);
        if (slot is not { } s) return text;

        return Dedup(node, DescribeSlot(s, index, count, isEffect));
    }

    /// <summary>
    /// Der Platz, auf dem der Fokus steht, mit seiner Stellung im Abschnitt.
    ///
    /// Gegenstaende und Magizite sind BEIDE <c>Comp(1007)</c> und haengen beide an der
    /// Fensterwurzel, die LAENGE des Laufs unterscheidet sie also: das Spiel gibt
    /// Gegenstaenden 16 Plaetze und Magiziten 3. Verglichen wird gegen die Array-Laenge
    /// des Directors und nicht gegen eine hier hingeschriebene Zahl.
    /// </summary>
    private unsafe Slot? FocusedSlot(AtkResNode* component, out int index, out int count, out bool isEffect)
    {
        var compId = (int)component->Type;
        var run    = SlotRun(component, compId);

        index    = run.IndexOf((nint)component);
        count    = run.Count;
        isEffect = compId == CompItemEffect;
        if (index < 0) return null;

        var isMagicite = compId == CompItemOrMagicite && run.Count < ItemSlotCount;
        return isMagicite ? Magicite(index) : Pomander(index);
    }

    /// <summary>
    /// Haengt einer Ausruestungszeile die Aetherpool-Staerke an, oder null, wenn der Fokus
    /// nicht auf einer steht.
    ///
    /// WELCHE ZEILE WELCHE IST, kommt aus dem Spiel und nicht aus dem Abgleich mit einem
    /// englischen Wort: die <c>AetherpoolArm</c>- und <c>AetherpoolArmor</c>-Referenzen
    /// des Gewoelbes benennen ihre <c>DeepDungeonEquipment</c>-Zeilen, und der Name dieser
    /// Zeile ist genau der Text, den das Fenster in die Zeile schreibt. Das funktioniert
    /// also in jeder Client-Sprache, und einem Gewoelbe, das seine Ausruestung anders
    /// nennt (Empyrean, Orthos), wird gefolgt statt danebengegriffen.
    /// </summary>
    private unsafe string? Augment(AtkResNode* node, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        var dd = _state.GetDirector();
        if (dd == null) return null;

        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dd->DeepDungeonId);
        if (dungeon is not { } d) return null;

        var arm   = d.AetherpoolArm.ValueNullable?.Name.ExtractText().Trim();
        var armor = d.AetherpoolArmor.ValueNullable?.Name.ExtractText().Trim();

        if (!string.IsNullOrEmpty(arm) && text.Equals(arm, StringComparison.OrdinalIgnoreCase))
            return AccessibilityStrings.DeepGearStrength(text, dd->WeaponLevel);
        if (!string.IsNullOrEmpty(armor) && text.Equals(armor, StringComparison.OrdinalIgnoreCase))
            return AccessibilityStrings.DeepGearStrength(text, dd->ArmorLevel);

        return null;
    }

    /// <summary>
    /// Protokolliert eine Zeile einmal je Aenderung statt einmal je Frame und gibt sie
    /// zurueck.
    ///
    /// Der Fokus-Leser ruft <see cref="NameSlot"/> VOR seiner eigenen Entdopplung auf,
    /// das hier laeuft also in jedem Frame, in dem der Cursor auf einem Platz ruht. Die
    /// Sprache war nie betroffen - das faengt jene Entdopplung ab - das Log aber schon:
    /// ein Platz erzeugte am 2026-08-12 um 10:45:02 55 gleiche Zeilen in der Sekunde, und
    /// das rollt das Log und begraebt die Belege, die hier gesammelt werden.
    /// </summary>
    private unsafe string Dedup(AtkResNode* node, string line)
    {
        if ((nint)node == _lastLoggedNode && line == _lastLoggedLine) return line;
        _lastLoggedNode = (nint)node;
        _lastLoggedLine = line;
        _log.Info($"[DeepPanel] {line}");
        return line;
    }

    private nint   _lastLoggedNode;
    private string _lastLoggedLine = string.Empty;

    /// <summary>Wie viele Gegenstands-Plaetze das Fenster und der Director beide haben
    /// (die Anzahlen wurden gegen den Auszug des Fensters geprueft und stimmen exakt
    /// ueberein).</summary>
    private const int ItemSlotCount = 16;

    /// <summary>
    /// Schreibt ein Bedienelement dieses Fensters auf, das keinen Text erzeugt hat und das
    /// diese Klasse nicht kennt: seine Komponenten-Id, seine Vorfahren und die Texte
    /// ringsum. Rein diagnostisch - es spricht nie und raet nie eine Beschriftung.
    ///
    /// Eine Zeile je Komponenten-Id, damit ein Durchlauf durch das Fenster das Log nicht
    /// flutet.
    /// </summary>
    private unsafe void LogUnknownControl(AtkResNode* node)
    {
        var ancestry = new List<string>();
        var texts    = new List<string>();

        var cur = node;
        for (var guard = 0; cur != null && guard < 8; guard++, cur = cur->ParentNode)
        {
            ancestry.Add($"{(int)cur->Type}#{cur->NodeId}");

            // Geschwistertexte, und die sind es, die in diesem Fenster einen Abschnitt
            // kenntlich machen: das Spiel legt die Abschnitts-BESCHRIFTUNG zu den
            // Kacheln, zu denen sie gehoert.
            var parent = cur->ParentNode;
            if (parent == null) continue;
            var sib = 0;
            for (var child = parent->ChildNode; child != null && sib++ < 512; child = child->PrevSiblingNode)
            {
                if (child->Type != NodeType.Text) continue;
                // AtkText, nie Utf8String.ToString() - siehe den Block ueber Pomander.
                // Jener abgesicherte Leser existiert WEGEN genau dieses Absturzes.
                var t = AtkText.Read((AtkTextNode*)child);
                if (t.Length > 0 && !texts.Contains(t)) texts.Add(t);
            }
            if (texts.Count > 0) break;
        }

        var key = ancestry.Count > 0 ? ancestry[0] : "?";
        if (!_loggedUnknown.Add(key)) return;

        _log.Info($"[DeepPanel] Unbekanntes Bedienelement {key}: Vorfahren [{string.Join(" < ", ancestry)}] "
                  + $"Nachbartexte [{string.Join(" | ", texts)}]");
    }

    private readonly HashSet<string> _loggedUnknown = new();

    /// <summary>
    /// Ein Platz als fertige Zeile.
    ///
    /// Ein Platz, den das Gewoelbe nicht benutzt, sagt "leer". Ein Platz, den das Gewoelbe
    /// SEHR WOHL benutzt, von dem der Spieler aber keinen hat, nennt den Pomander und
    /// "mal 0" - User, 2026-08-12: *"needs to announce Empty slot if a slot is empty or if
    /// it shows which pomadors you can have, pomador of (x) times 0."* Genau das zeichnet
    /// das Fenster auch: das Symbol ist da und ausgegraut, der Platz ist also auch dann
    /// eine Information, wenn nichts darin liegt.
    /// </summary>
    private string DescribeSlot(Slot slot, int index, int count, bool isEffect)
    {
        // Die Position ist LEER, wenn der Spieler Listenpositionen abgeschaltet hat, sie
        // wird deshalb als Teil angehaengt statt eingesetzt - sonst endet jede Zeile in
        // einem haengenden Komma, und genau das zeigte das Log am 2026-08-12 um 10:45
        // ("Pomander of Safety times 0, ").
        var parts = new List<string>();

        if (slot.Name.Length == 0)
            parts.Add(AccessibilityStrings.DeepSlotEmpty);
        else if (isEffect)
            // Die Wirkungs-Zeile beantwortet nur eine Frage: laeuft die Wirkung dieses
            // Pomanders? Genau das Bit zeichnet der Abschnitt "Item Effects" des Fensters
            // (16 Gegenstands-Plaetze, 16 Wirkungs-Plaetze - die Anzahlen passen exakt).
            parts.Add($"{slot.Name}, {(slot.Active ? AccessibilityStrings.DeepEffectActive : AccessibilityStrings.DeepEffectInactive)}");
        else
        {
            parts.Add(AccessibilityStrings.DeepSlotCount(slot.Name, slot.Count));
            if (slot.Count > 0 && !slot.Usable) parts.Add(AccessibilityStrings.DeepItemUnusable);
        }

        var position = AccessibilityStrings.DeepSlotPosition(index + 1, count);
        if (position.Length > 0) parts.Add(position);

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Der naechste Vorfahr, der eine der PLATZ-Komponenten des Fensters ist, beginnend
    /// beim Knoten selbst, oder null.
    ///
    /// Ein Komponentenknoten traegt seine ULD-Id als rohen <c>Type</c>; der Aufstieg geht
    /// ueber Komponententypen hinweg, die er nicht kennt, weil ein Gegenstands-Platz eine
    /// Schaltflaeche umschliesst und der Fokus darin landet.
    /// </summary>
    private static unsafe AtkResNode* ClimbToSlotComponent(AtkResNode* node)
    {
        var cur = node;
        for (var guard = 0; cur != null && guard < 8; guard++, cur = cur->ParentNode)
        {
            var type = (int)cur->Type;
            if (type == CompItemOrMagicite || type == CompItemEffect) return cur;
        }
        return null;
    }

    /// <summary>
    /// Der ununterbrochene Lauf gleichartiger Platz-Komponenten, zu dem der Knoten
    /// gehoert, nach aufsteigender Knoten-Id.
    ///
    /// WARUM EIN LAUF UND NICHT EINFACH ALLE GESCHWISTER. Das Fenster haengt jeden
    /// Abschnitt an die Fensterwurzel, die 16 Gegenstands-Plaetze und die 3
    /// Magizit-Plaetze sind also Geschwister voneinander UND teilen eine Komponenten-Id.
    /// Was sie trennt, ist, dass die Knoten-Ids jedes Abschnitts fortlaufend sind, mit
    /// einer Luecke zwischen den Abschnitten (Gegenstaende 19-34, Magizite 57-59 im Auszug
    /// vom 2026-08-12). An dieser Luecke zu trennen verlangt keine hier hingeschriebene
    /// Id: es ist die Gruppierung des Fensters selbst, aus dem Fenster gelesen.
    /// </summary>
    private static unsafe List<nint> SlotRun(AtkResNode* component, int compId)
    {
        var all = new List<(uint Id, nint Ptr)>();
        var parent = component->ParentNode;
        if (parent == null) return new List<nint> { (nint)component };

        // Begrenzt, anders als vergleichbare Durchlaeufe anderswo. Eine Geschwisterliste,
        // die je auf sich selbst zurueckzeigt, wuerde das Spiel haengen lassen statt es
        // abstuerzen zu lassen, und ein Haenger ist der eine Fehler, den ein blinder
        // Spieler nicht einmal melden kann; 512 liegt weit jenseits jedes echten Fensters
        // (dieses hat insgesamt 97 Knoten).
        var guard = 0;
        for (var child = parent->ChildNode; child != null && guard++ < 512; child = child->PrevSiblingNode)
            if ((int)child->Type == compId)
                all.Add((child->NodeId, (nint)child));

        all.Sort((a, b) => a.Id.CompareTo(b.Id));

        var run = new List<(uint Id, nint Ptr)>();
        foreach (var entry in all)
        {
            if (run.Count > 0 && entry.Id != run[^1].Id + 1)
            {
                if (run.Any(r => r.Ptr == (nint)component)) break;
                run.Clear();
            }
            run.Add(entry);
        }

        return run.Any(r => r.Ptr == (nint)component)
            ? run.Select(r => r.Ptr).ToList()
            : new List<nint> { (nint)component };
    }

    // ── Die Beschreibungstaste: das Ding unter dem Cursor, und sonst nichts ──

    /// <summary>
    /// Die eigene Beschreibung des fokussierten Platzes, als Zeilen, durch die sich mit
    /// den Pfeiltasten blaettern laesst, oder leer, wenn der Fokus auf nichts steht, das
    /// dieses Fenster beschreibt.
    ///
    /// EIN PLATZ, NICHT DAS FENSTER. Das gab frueher die gesamte Tafel zurueck - jeden
    /// Pomander, die Magizite, die Aetherpool-Werte - und das Urteil des Users lautete am
    /// 2026-08-12, es *"is reading the entire window contents, which is not helpful"*. Es
    /// brach ausserdem die Zusage, die eine Beschreibungstaste sonst ueberall gibt: sie
    /// beschreibt, was FOKUSSIERT ist.
    ///
    /// Zwei Zeilen statt einer: erst Name und Anzahl des Platzes, dann der spieleigene
    /// Tooltip, damit die Pfeiltasten von "welcher Pomander" zu "was er tut" gehen, statt
    /// jedes Mal beides zu wiederholen.
    ///
    /// Ohne Aufrufer im Plugin - dies ist der fertige Anschlusspunkt fuer eine
    /// Beschreibungs- oder Detailtaste.
    /// </summary>
    public unsafe List<string> DescribeFocused(AtkResNode* node)
    {
        var lines = new List<string>();
        if (node == null) return lines;

        // Ausruestungszeilen: die Zeile sagt schon, welches Teil es ist, die Beschreibung
        // ergaenzt also die Staerke und den spieleigenen Text zum Teil.
        if (GearRow(node) is { } gear)
        {
            lines.Add(gear.Header);
            if (gear.Description.Length > 0) lines.Add(gear.Description);
            return lines;
        }

        var component = ClimbToSlotComponent(node);
        if (component == null) return lines;

        var slot = FocusedSlot(component, out var index, out var count, out var isEffect);
        if (slot is not { } s || s.Name.Length == 0) return lines;

        lines.Add(DescribeSlot(s, index, count, isEffect));
        if (s.Description.Length > 0) lines.Add(s.Description);
        return lines;
    }

    /// <summary>Die fokussierte Aetherpool-Zeile - ihre Ueberschrift mit der Staerke und
    /// die Beschreibung des Teils durch das Spiel - oder null, wenn der Fokus woanders
    /// steht.</summary>
    private unsafe (string Header, string Description)? GearRow(AtkResNode* node)
    {
        var dd = _state.GetDirector();
        if (dd == null) return null;

        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dd->DeepDungeonId);
        if (dungeon is not { } d) return null;

        // Der eigene Text der Zeile ist es, der sie kenntlich macht, und er wird ueber den
        // abgesicherten Leser gelesen - nie Utf8String.ToString(), siehe den Block ueber
        // Pomander.
        var text = ClimbToText(node);
        if (text.Length == 0) return null;

        foreach (var (piece, strength) in new[]
                 {
                     (d.AetherpoolArm.ValueNullable,   (int)dd->WeaponLevel),
                     (d.AetherpoolArmor.ValueNullable, (int)dd->ArmorLevel),
                 })
        {
            if (piece is not { } p) continue;
            var name = p.Name.ExtractText().Trim();
            if (name.Length == 0 || !text.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            return (AccessibilityStrings.DeepGearStrength(name, strength), _text.Read(p.Description));
        }
        return null;
    }

    /// <summary>Der naechstgelegene Text, den dieser Knoten oder seine Vorfahren tragen,
    /// gelesen ueber <see cref="AtkText"/>.</summary>
    private static unsafe string ClimbToText(AtkResNode* node)
    {
        for (var cur = node; cur != null; cur = cur->ParentNode)
        {
            var sib = 0;
            for (var child = cur->ChildNode; child != null && sib++ < 64; child = child->PrevSiblingNode)
            {
                if (child->Type != NodeType.Text) continue;
                var t = AtkText.Read((AtkTextNode*)child);
                if (t.Length > 0) return t;
            }
            if (cur->Type == NodeType.Text)
            {
                var own = AtkText.Read((AtkTextNode*)cur);
                if (own.Length > 0) return own;
            }
        }
        return string.Empty;
    }
}
