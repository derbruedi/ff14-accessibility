using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LuminaAozAction = Lumina.Excel.Sheets.AozAction;
using LuminaAozActionTransient = Lumina.Excel.Sheets.AozActionTransient;

namespace FF14Accessibility.Services;

/// <summary>
/// Ein Blaumagie-Zauber, so wie ihn die Sheets des Spiels beschreiben.
/// Zusammengesetzt aus <c>AozAction</c> (Rang, Verweis auf die Aktion) und
/// <c>AozActionTransient</c> (Nummer, Werte, Beschreibung).
/// </summary>
/// <param name="AozRowId">Zeile in AozAction/AozActionTransient (1 bis 124).</param>
/// <param name="ActionId">Aktions-Id, wie sie das Fenster in der Kachel fuehrt.</param>
/// <param name="Number">Die Nummer im Zauberbuch ("Nr. 1"), 1 bis 124.</param>
/// <param name="Rank">Sternrang 1 bis 5.</param>
/// <param name="Name">Zaubername in der Spielsprache.</param>
/// <param name="Stats">Typus/Element/Rang, bereits vorlesbar aufbereitet.</param>
/// <param name="Description">Beschreibungstext des Zaubers.</param>
public readonly record struct AozSpell(
    uint   AozRowId,
    uint   ActionId,
    byte   Number,
    byte   Rank,
    string Name,
    string Stats,
    string Description);

/// <summary>
/// Liest das Zauberbuch der Blaumagie (Addon <c>AOZNotebook</c>).
///
/// <para>
/// WARUM ES EINEN EIGENEN LESER BRAUCHT. Das Fenster war fuer den allgemeinen
/// Leser eine Wand aus Symbolen. Gemessen im Log vom 2026-09-02 (07:10 bis
/// 07:11): beim Blaettern ueber das Raster kam auf JEDEM Feld nur die nackte
/// Nummer - "Nr. 14", "Nr. 15", "Nr. 65", "Nr. 80" - denn der Fokus sitzt auf
/// der Kollision des Ankreuzfeldes (id=4), und der einzige Text, den die
/// Textsuche von dort aus erreicht, ist das Nummernschild der Kachel (id=9).
/// Der Zaubername steht nirgends auf der Kachel. Dazu meldete der Listen-Leser
/// beim Oeffnen "Liste noch leer" und danach "Liste bleibt leer": das Fenster
/// fuehrt gar keine AtkComponentList, sondern ein Raster aus 16 Kacheln.
/// </para>
///
/// <para>
/// DIE QUELLE IST BENANNT, ES WIRD KEINE KNOTEN-ID GERATEN. FFXIVClientStructs
/// beschreibt <c>AddonAOZNotebook</c> vollstaendig: <c>SpellbookBlocks</c> sind
/// die 16 Kacheln der aktuellen Seite (je mit <c>ActionId</c>, <c>Name</c> und
/// Zeigern auf ihre eigenen Knoten), <c>ActiveActions</c> die 24 aktiven
/// Kommandos, dazu <c>TabIndex</c> und <c>TabCount</c>. Die Kachel wird ueber
/// den ZEIGER erkannt, nicht ueber ihre Id - siehe die Node-Id-Falle in
/// <c>UIReaderService.FindTopLevelNode</c>.
/// </para>
///
/// <para>
/// GEGEN DIE SHEETS GEPRUEFT (offline, 2026-09-02, deutsche Fassung):
/// AozAction fuehrt 124 Zauber, jeder mit eigener Aktions-Id (124 Ids, 124
/// verschieden - der Rueckschluss von der Kachel auf die Zeile ist also
/// eindeutig). <c>Number</c> laeuft lueckenlos von 1 bis 124 und deckt sich mit
/// dem "Nr." auf der Kachel. Die Rang-Zeile in <c>Stats</c> ist in ALLEN 124
/// Faellen genau <c>Rank</c> mal das Zeichen U+2605 - der Stern wird deshalb
/// durch die Zahl ersetzt, sonst liest der Screenreader fuenf Sternchen vor.
/// </para>
/// </summary>
public sealed unsafe class AozNotebookService
{
    /// <summary>Name des Fensters, wie das Spiel es fuehrt.</summary>
    public const string AddonName = "AOZNotebook";

    /// <summary>Der Stern, mit dem das Spiel den Rang in <c>Stats</c> malt.</summary>
    private const char RankStar = '★';

    // Knoten des Detail-Bereichs rechts. Alle liegen in der OBERSTEN Knotenliste
    // des Fensters (Dump 2026-09-02), werden also ueber die Fensterebene gesucht
    // und nie ueber GetNodeById - Knoten-Ids sind nur innerhalb ihres Containers
    // eindeutig.
    private const uint NodeDetailNumber   = 69; // "Nr. 1"
    private const uint NodeDetailName     = 70; // "Wasserkanone"
    private const uint NodeNotLearned     = 68; // "Noch nicht erlernt." - Sichtbarkeit traegt den Zustand
    private const uint NodeSourceLabel    = 73; // "Erlernbar durch:"
    private const uint NodeSourceFirst    = 75; // erste Fundstelle
    private const uint NodeSourceSecond   = 78; // zweite Fundstelle (oft ausgeblendet)
    private const uint NodeLearnedCounter = 17; // "1/124"
    private const uint NodeActiveCounter  = 37; // "1/24"

    private readonly IGameGui    _gameGui;
    private readonly IDataManager _data;
    private readonly IPluginLog  _log;

    private Dictionary<uint, AozSpell>? _byActionId;
    private Dictionary<byte, AozSpell>? _byNumber;

    // Zuletzt geloggte Kachel-Nummer, damit die Zeile einmal pro Wechsel kommt
    // und nicht zweimal pro Frame (siehe TryDescribeSpellTile).
    private byte _lastLoggedTile;

    public AozNotebookService(IGameGui gameGui, IDataManager data, IPluginLog log)
    {
        _gameGui = gameGui;
        _data    = data;
        _log     = log;
    }

    /// <summary>Ob das Zauberbuch gerade offen und sichtbar ist.</summary>
    public bool IsOpen
    {
        get
        {
            var addon = Addon();
            return addon != null && addon->AtkUnitBase.IsVisible;
        }
    }

    /// <summary>Das Fenster, oder <c>null</c> wenn es nicht offen ist.</summary>
    public AddonAOZNotebook* Addon()
    {
        var ptr = _gameGui.GetAddonByName(AddonName);
        return ptr.IsNull ? null : (AddonAOZNotebook*)(nint)ptr;
    }

    // ── Kacheln des Zauberrasters ────────────────────────────────────

    /// <summary>
    /// Findet die Zauber-Kachel, auf der (oder in der) der Fokus sitzt, und
    /// beschreibt sie: Name und Nummer, dazu "ausgewaehlt", wenn ihr
    /// Ankreuzfeld gesetzt ist.
    ///
    /// <para>
    /// ZWEI WEGE ZUM ZAUBER, damit eine leere <c>ActionId</c> nicht in
    /// Schweigen endet: normal ueber die Aktions-Id der Kachel, ersatzweise
    /// ueber die Nummer auf ihrem Nummernschild. Der zweite Weg traegt immer,
    /// weil <c>Number</c> lueckenlos von 1 bis 124 laeuft (offline geprueft).
    /// </para>
    /// </summary>
    /// <param name="focus">Der Knoten, auf dem der Fokus steht.</param>
    /// <param name="text">Die fertige Ansage.</param>
    /// <param name="spell">Der erkannte Zauber, fuer die spaetere Verweil-Ansage.</param>
    /// <returns>Wahr, wenn der Fokus wirklich auf einer Zauber-Kachel steht.</returns>
    public bool TryDescribeSpellTile(AtkResNode* focus, out string text, out AozSpell? spell)
    {
        text  = string.Empty;
        spell = null;

        var addon = Addon();
        if (addon == null || focus == null) return false;

        if (!TryFindSpellBlock(addon, focus, out var index)) return false;

        ref var block = ref addon->SpellbookBlocks[index];

        var found = Lookup(block.ActionId);
        if (found is null)
        {
            // Kein Treffer ueber die Aktions-Id: das Nummernschild der Kachel
            // fuehrt zum selben Zauber, und es ist immer da.
            var number = ReadTileNumber(ref block);
            if (number > 0) found = LookupByNumber(number);
        }

        if (found is not { } value)
        {
            // Die Kachel ist erkannt, aber der Zauber nicht aufloesbar. Lieber
            // die nackte Position ansagen als schweigen - Stillstand ist die
            // schlechteste Auskunft.
            text = AccessibilityStrings.AozUnknownSpell(index + 1);
            _log.Info($"[Aoz] Kachel {index} ohne Zuordnung: ActionId={block.ActionId}.");
            return true;
        }

        spell = value;
        var selected = IsBlockChecked(ref block);
        text = AccessibilityStrings.AozSpellTile(value.Name, value.Number, selected);

        // NUR bei Wechsel loggen. Diese Methode laeuft pro Frame und wird dabei
        // zweimal gerufen (Fokus-Zweig und Verweil-Uhr) - ungebremst schrieb sie
        // zehn identische Zeilen in 70 ms ins Log (gemessen 2026-09-02, 18:46:25)
        // und machte es fuer jede andere Diagnose unlesbar.
        if (value.Number != _lastLoggedTile)
        {
            _lastLoggedTile = value.Number;
            _log.Info($"[Aoz] Kachel {index}: ActionId={block.ActionId} -> Nr. {value.Number} " +
                      $"'{value.Name}', angekreuzt={selected}.");
        }
        return true;
    }

    /// <summary>
    /// Findet den aktiven Kommando-Platz unter dem Fokus (die 24 Plaetze unten)
    /// und beschreibt ihn: Platznummer und der Zauber, der darin liegt - oder
    /// dass er leer ist.
    /// </summary>
    public bool TryDescribeActiveSlot(AtkResNode* focus, out string text)
    {
        text = string.Empty;

        var addon = Addon();
        if (addon == null || focus == null) return false;

        if (!TryFindActiveSlot(addon, focus, out var index)) return false;

        ref var slot = ref addon->ActiveActions[index];
        var found = Lookup(slot.ActionId);

        text = found is { } value
            ? AccessibilityStrings.AozActiveSlot(index + 1, value.Name, value.Number)
            : AccessibilityStrings.AozActiveSlotEmpty(index + 1);
        _log.Info($"[Aoz] Platz {index + 1}: ActionId={slot.ActionId} -> " +
                  $"'{found?.Name ?? "leer"}'.");
        return true;
    }

    // ── Details, die erst beim Verweilen kommen ──────────────────────

    /// <summary>
    /// Die ausfuehrliche Auskunft zu einem Zauber: Typus, Element, Rang,
    /// Beschreibung, dazu - wenn das Fenster gerade denselben Zauber zeigt -
    /// ob er erlernt ist und wo er zu holen waere.
    ///
    /// <para>
    /// WARUM DAS DETAIL-FELD GEGENGEPRUEFT WIRD. Werte und Beschreibung stehen
    /// in den Sheets und gehoeren fest zur Kachel. "Erlernt" und "Erlernbar
    /// durch" gibt es dort NICHT vollstaendig: von 124 Zaubern tragen 13 gar
    /// keinen Fundort im Sheet (LocationKey 2, alle mit leerem Verweis) und
    /// einer ist der Startzauber - im Fenster steht bei ihm "Erster Zauber".
    /// Diese Angaben kommen deshalb aus dem Detail-Feld des Spiels, das sie
    /// bereits fertig formuliert. Das Feld gehoert aber dem AUSGEWAEHLTEN
    /// Zauber, nicht zwingend dem, auf dem der Fokus steht. Statt auf ein
    /// Nachziehen zu vertrauen, wird die Nummer im Feld (Knoten 69) mit der
    /// erwarteten verglichen: passt sie nicht, entfaellt der Teil, statt zum
    /// falschen Zauber vorgelesen zu werden.
    /// </para>
    /// </summary>
    public string DescribeSpellDetails(AozSpell spell)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(spell.Stats)) parts.Add(spell.Stats);

        var addon = Addon();
        var panelNumber = addon == null ? (byte)0 : ParseLeadingNumber(ReadTopLevelText(addon, NodeDetailNumber));
        // MESSUNG, solange nicht bestaetigt ist, dass das Detail-Feld der
        // Tastatur folgt: passt seine Nummer zur Kachel, kommen Erlernt-Zustand
        // und Fundort mit - passt sie nie, steht es im Log statt in einer
        // falschen Ansage.
        _log.Info($"[Aoz] Details Nr. {spell.Number} '{spell.Name}': Feld zeigt Nr. {panelNumber} " +
                  $"({(panelNumber == spell.Number ? "passt" : "passt nicht")}).");

        if (addon != null && panelNumber == spell.Number)
        {
            var learned = ReadLearnedState(addon);
            if (learned is { } isLearned) parts.Add(isLearned
                ? AccessibilityStrings.AozLearned
                : AccessibilityStrings.AozNotLearned);

            var source = ReadSource(addon);
            if (!string.IsNullOrWhiteSpace(source)) parts.Add(source);
        }

        if (!string.IsNullOrWhiteSpace(spell.Description)) parts.Add(spell.Description);

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Ueberblick beim Oeffnen und auf Wunsch: welcher Reiter, wie viele Zauber
    /// erlernt, wie viele Kommandos belegt. Die beiden Zaehler stehen fertig im
    /// Fenster ("1/124", "1/24") und werden gelesen, nicht nachgerechnet.
    /// </summary>
    public string DescribeOverview()
    {
        var addon = Addon();
        if (addon == null) return string.Empty;

        var learned = ReadTopLevelText(addon, NodeLearnedCounter);
        var active  = ReadTopLevelText(addon, NodeActiveCounter);
        return AccessibilityStrings.AozOverview(addon->TabIndex + 1, addon->TabCount, learned, active);
    }

    // ── Kachel- und Platz-Erkennung ueber Zeiger ─────────────────────

    /// <summary>
    /// Welche der 16 Kacheln traegt diesen Fokus-Knoten? Verglichen wird gegen
    /// ALLE Knoten, die die Kachel laut FFXIVClientStructs besitzt - der Fokus
    /// sitzt naemlich nicht auf der Kachel selbst, sondern auf der Kollision
    /// ihres Ankreuzfeldes (Log 2026-09-02: durchgehend id=4).
    /// </summary>
    private static bool TryFindSpellBlock(AddonAOZNotebook* addon, AtkResNode* focus, out int index)
    {
        index = -1;
        var blocks = addon->SpellbookBlocks;
        for (var i = 0; i < blocks.Length; i++)
        {
            ref var b = ref blocks[i];
            if (FocusBelongsTo(focus,
                    OwnerOf((AtkComponentBase*)b.AtkComponentBase),
                    (AtkResNode*)b.AtkCollisionNode,
                    OwnerOf((AtkComponentBase*)b.AtkComponentCheckBox),
                    OwnerOf((AtkComponentBase*)b.AtkComponentIcon),
                    (AtkResNode*)b.AtkTextNode,
                    b.AtkResNode1,
                    b.AtkResNode2))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>Welcher der 24 aktiven Plaetze traegt diesen Fokus-Knoten?</summary>
    private static bool TryFindActiveSlot(AddonAOZNotebook* addon, AtkResNode* focus, out int index)
    {
        index = -1;
        var slots = addon->ActiveActions;
        for (var i = 0; i < slots.Length; i++)
        {
            ref var s = ref slots[i];
            if (FocusBelongsTo(focus,
                    OwnerOf((AtkComponentBase*)s.AtkComponentDragDrop),
                    (AtkResNode*)s.AtkTextNode))
            {
                index = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Der Knoten, dem diese Komponente gehoert. Alle Komponenten des Fensters
    /// erben von <c>AtkComponentBase</c>, deshalb genuegt ein Helfer fuer alle -
    /// der <c>?.</c>-Operator ist auf Zeigern nicht zu haben.
    /// </summary>
    private static AtkResNode* OwnerOf(AtkComponentBase* comp) =>
        comp == null ? null : (AtkResNode*)comp->OwnerNode;

    /// <summary>
    /// Ob der Fokus auf einem dieser Knoten sitzt oder in einem von ihnen. Der
    /// Fokus liegt in der Regel auf einem Kollisionskind, deshalb wird von ihm
    /// aus nach oben verglichen - per Zeiger, nie per Id.
    /// </summary>
    private static bool FocusBelongsTo(AtkResNode* focus, params AtkResNode*[] candidates)
    {
        var guard = 0;
        for (var n = focus; n != null && guard++ < 6; n = n->ParentNode)
        {
            foreach (var c in candidates)
                if (c != null && c == n) return true;
        }
        return false;
    }

    // ── Zustaende an der Kachel ──────────────────────────────────────

    /// <summary>Ob das Ankreuzfeld der Kachel gesetzt ist.</summary>
    private static bool IsBlockChecked(ref AddonAOZNotebook.SpellbookBlock block)
    {
        var box = block.AtkComponentCheckBox;
        return box != null && box->IsChecked;
    }

    /// <summary>
    /// Die Nummer vom Nummernschild der Kachel ("Nr. 14" liefert 14). Nur
    /// Ersatzweg, wenn die Aktions-Id nichts hergibt.
    /// </summary>
    private static byte ReadTileNumber(ref AddonAOZNotebook.SpellbookBlock block)
    {
        var node = block.AtkTextNode;
        if (node == null) return 0;
        return ParseLeadingNumber(AtkText.ReadClean(node));
    }

    // ── Detail-Feld ──────────────────────────────────────────────────

    /// <summary>
    /// Erlernt oder nicht - abgelesen an der Sichtbarkeit des Hinweises "Noch
    /// nicht erlernt." (Knoten 68). Sichtbar heisst: noch nicht erlernt.
    /// <c>null</c>, wenn der Knoten fehlt; dann wird gar nichts behauptet.
    /// </summary>
    private static bool? ReadLearnedState(AddonAOZNotebook* addon)
    {
        var node = FindTopLevelNode(addon, NodeNotLearned);
        if (node == null) return null;
        return !IsEffectivelyVisible(node);
    }

    /// <summary>
    /// "Erlernbar durch: ..." aus dem Detail-Feld, mit beiden Fundstellen, wenn
    /// zwei sichtbar sind. Fertig formuliert vom Spiel - auch fuer die 14
    /// Zauber, zu denen die Sheets keinen Fundort fuehren.
    /// </summary>
    private static string ReadSource(AddonAOZNotebook* addon)
    {
        var label  = ReadTopLevelText(addon, NodeSourceLabel);
        var first  = ReadVisibleTopLevelText(addon, NodeSourceFirst);
        var second = ReadVisibleTopLevelText(addon, NodeSourceSecond);

        var sources = new List<string>();
        if (!string.IsNullOrWhiteSpace(first))  sources.Add(first.Trim());
        if (!string.IsNullOrWhiteSpace(second)) sources.Add(second.Trim());
        if (sources.Count == 0) return string.Empty;

        var joined = string.Join(", ", sources);
        return string.IsNullOrWhiteSpace(label) ? joined : $"{label.Trim()} {joined}";
    }

    // ── Sheets ───────────────────────────────────────────────────────

    /// <summary>Der Zauber zu dieser Aktions-Id, oder <c>null</c>.</summary>
    public AozSpell? Lookup(uint actionId)
    {
        if (actionId == 0) return null;
        BuildCache();
        return _byActionId!.TryGetValue(actionId, out var s) ? s : null;
    }

    /// <summary>Der Zauber zu dieser Zauberbuch-Nummer, oder <c>null</c>.</summary>
    public AozSpell? LookupByNumber(byte number)
    {
        if (number == 0) return null;
        BuildCache();
        return _byNumber!.TryGetValue(number, out var s) ? s : null;
    }

    /// <summary>
    /// Baut die beiden Nachschlagetabellen einmalig auf. Referenzen werden
    /// zwischengespeichert, keine Werte des Spielzustands - die Sheets aendern
    /// sich waehrend einer Sitzung nicht.
    /// </summary>
    private void BuildCache()
    {
        if (_byActionId != null) return;

        var byAction = new Dictionary<uint, AozSpell>();
        var byNumber = new Dictionary<byte, AozSpell>();

        var actions    = _data.GetExcelSheet<LuminaAozAction>();
        var transients = _data.GetExcelSheet<LuminaAozActionTransient>();
        if (actions == null || transients == null)
        {
            _log.Warning("[Aoz] Sheets AozAction/AozActionTransient nicht verfuegbar.");
            _byActionId = byAction;
            _byNumber   = byNumber;
            return;
        }

        foreach (var row in actions)
        {
            var actionId = row.Action.RowId;
            if (actionId == 0) continue;

            var name = row.Action.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var t = transients.GetRowOrDefault(row.RowId);
            var number      = t?.Number ?? 0;
            var stats       = SpeakableStats(t?.Stats.ExtractText() ?? string.Empty, row.Rank);
            var description = Tidy(t?.Description.ExtractText() ?? string.Empty);

            var spell = new AozSpell(row.RowId, actionId, number, row.Rank, name.Trim(), stats, description);
            byAction[actionId] = spell;
            if (number > 0) byNumber[number] = spell;
        }

        _byActionId = byAction;
        _byNumber   = byNumber;
        _log.Info($"[Aoz] Sheets geladen: {byAction.Count} Zauber, {byNumber.Count} mit Nummer.");
    }

    /// <summary>
    /// Macht die Werte-Zeilen vorlesbar: die Rang-Zeile malt das Spiel als
    /// Sterne (U+2605), und zwar genau <c>rank</c> Stueck - offline gegen alle
    /// 124 Zauber geprueft. Vorgelesen wird stattdessen die Zahl. Die Suche
    /// laeuft ueber das STERNZEICHEN, nicht ueber das Wort "Rang", damit sie in
    /// jeder Spielsprache greift.
    /// </summary>
    private static string SpeakableStats(string raw, byte rank)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var parts = new List<string>();
        foreach (var line in raw.Split('\n'))
        {
            var clean = line.Trim().Trim('\r').Trim();
            if (clean.Length == 0) continue;

            if (clean.IndexOf(RankStar) >= 0)
            {
                // Sterne raus, Zahl rein - der Rest der Zeile (das lokalisierte
                // "Rang:" davor) bleibt stehen.
                var withoutStars = clean.Replace(RankStar.ToString(), string.Empty).Trim();
                clean = $"{withoutStars} {rank}".Trim();
            }

            parts.Add(EnsureSentenceEnd(clean));
        }
        return string.Join(" ", parts);
    }

    /// <summary>Zeilenumbrueche zu Leerzeichen, damit die Sprachausgabe fliesst.</summary>
    private static string Tidy(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(c is '\n' or '\r' ? ' ' : c);
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string EnsureSentenceEnd(string s) =>
        s.Length == 0 || s.EndsWith('.') || s.EndsWith('!') || s.EndsWith('?') ? s : s + ".";

    /// <summary>
    /// Die erste Zahl in einem Text ("Nr. 14" liefert 14). 0, wenn keine da
    /// ist oder sie nicht in ein Byte passt.
    /// </summary>
    private static byte ParseLeadingNumber(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var value = 0;
        var seen  = false;
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
            {
                seen  = true;
                value = value * 10 + (c - '0');
                if (value > byte.MaxValue) return 0;
            }
            else if (seen) break;
        }
        return seen ? (byte)value : (byte)0;
    }

    // ── Knoten-Helfer (Fensterebene, nie GetNodeById) ────────────────

    private static string ReadTopLevelText(AddonAOZNotebook* addon, uint nodeId)
    {
        var node = FindTopLevelNode(addon, nodeId);
        if (node == null || node->Type != NodeType.Text) return string.Empty;
        return AtkText.ReadClean((AtkTextNode*)node);
    }

    /// <summary>Wie <see cref="ReadTopLevelText"/>, aber leer bei ausgeblendetem Knoten.</summary>
    private static string ReadVisibleTopLevelText(AddonAOZNotebook* addon, uint nodeId)
    {
        var node = FindTopLevelNode(addon, nodeId);
        if (node == null || node->Type != NodeType.Text) return string.Empty;
        if (!IsEffectivelyVisible(node)) return string.Empty;
        return AtkText.ReadClean((AtkTextNode*)node);
    }

    /// <summary>
    /// Der Knoten mit dieser Id in der OBERSTEN Knotenliste des Fensters.
    /// Bewusst nicht <c>GetNodeById</c>: Knoten-Ids sind nur innerhalb ihres
    /// Containers eindeutig, und dieses Fenster hat in seinen Kacheln erneut
    /// niedrige Ids (die Kachel traegt id=9 fuer ihr Nummernschild).
    /// </summary>
    private static AtkResNode* FindTopLevelNode(AddonAOZNotebook* addon, uint nodeId)
    {
        if (addon == null) return null;
        ref var uld = ref addon->AtkUnitBase.UldManager;
        for (var i = 0; i < uld.NodeListCount; i++)
        {
            var n = uld.NodeList[i];
            if (n != null && n->NodeId == nodeId) return n;
        }
        return null;
    }

    private static bool IsEffectivelyVisible(AtkResNode* node)
    {
        var guard = 0;
        for (var n = node; n != null && guard++ < 32; n = n->ParentNode)
        {
            if (((ushort)n->NodeFlags & (ushort)NodeFlags.Visible) == 0) return false;
        }
        return true;
    }
}
