using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// DER EIGENE ZUSTAND DES TIEFEN GEWOELBES, aus dem Spiel gelesen.
///
/// WARUM ES DAS GIBT. User, 2026-08-12: *"our buff tracker does track beneficial
/// buffs, but does not appear to track effects from things like cairns, traps etc
/// or any debuffs that have been applied ... other effects like the brand new ring
/// bonus don't appear to be tracked."*
///
/// Der Puffer der aktiven Effekte war nicht kaputt - er las die einzige Liste, die er
/// kannte: <c>IBattleChara.StatusList</c>. Das Tiefe Gewoelbe fuehrt das meiste, worauf
/// es ankommt, WOANDERS. Gemessen im Log der Sitzung des Users vom 2026-08-12 im Palast
/// der Toten:
///
///   06:11:46.998  [StatusProbe] id=1411 'Preferred World Bonus' cat=1 ...
///   06:11:46.999  [Status] 1 positive, 0 negative Effekte.
///
/// Ein Eintrag, in einem Tiefen Gewoelbe, auf einer Ebene mit laufenden Wirkungen. Die
/// ebenenweiten Zustaende sind ueberhaupt keine Status auf dem Spieler: sie liegen auf
/// dem Content-Director und loesen ueber die spieleigenen Sheets zu Zeilen von
/// <c>DeepDungeonFloorEffectUI</c> auf - "Area Effect: Haste", "Area Effect: Gloom",
/// "Area Effect: Item Penalty" und zwoelf weitere (Offline-Sheet-Auszug 2026-08-12,
/// 16 Zeilen). Dasselbe Log hat einen davon dabei erwischt, wie er sich als blosses
/// Popup meldete und sonst nirgends: 06:01:51.575 <c>_PopUpText ... '+ Haste'</c>.
///
/// Diese Klasse erfindet also KEINEN Katalog. Sie liest
/// <c>InstanceContentDeepDungeon</c> - den Director, den das Spiel selbst antreibt -
/// und benennt jeden Wert ueber das Sheet, mit dem das Spiel ihn benennt.
///
/// WAS WORAUF ABGEBILDET WIRD, und wie jede Abbildung geprueft statt angenommen wurde:
///
///  - <c>Items</c> (16 Plaetze) sind die Pomander. Der Array-INDEX ist der Platz, und
///    der Platz loest ueber <c>DeepDungeon[id].PomanderSlot[index]</c> auf - die
///    spieleigene Tabelle je Gewoelbe. FFXIVClientStructs sagt das bei
///    <c>UsePomander</c> ausdruecklich: *"slot: Slot number in the range 0-15. This is
///    an index into the PomanderSlot field of the DeepDungeon sheet."* Das ist wichtig
///    und keine Erbsenzaehlerei: derselbe Platz haelt je Gewoelbe VERSCHIEDENE Pomander
///    (Platz 11 ist Zeile 12 "Pomander of Rage" im Palast der Toten, Zeile 17 "Pomander
///    of Frailty" am Himmelsberg, Zeile 20 "Protomander of Lethargy" in Eureka Orthos,
///    Zeile 36 "Pomander of Haste" auf dem Pilgerpfad - Offline-Auszug 2026-08-12). Die
///    rohe <c>ItemId</c> als Sheet-Zeile zu lesen wuerde in drei von vier Gewoelben den
///    falschen Pomander benennen.
///  - <c>Items[i].IsActive</c> ist die Zeile "Item Effects" des Fensters. Die Anzahlen
///    stimmen exakt mit dem UI-Auszug ueberein: 16 Gegenstands-Plaetze, 16
///    Wirkungs-Plaetze, 3 Magizite.
///  - <c>DeepDungeonStatusId</c> / <c>BanId</c> / <c>DangerId</c> indizieren jeweils das
///    gleichnamige Sheet, und jedes davon traegt eine <c>FloorEffectUI</c>-Referenz mit
///    dem Namen und der Beschreibung, die der Spieler hoeren soll.
///  - <c>DeepDungeonGimmickEffectIdCurrent/Next</c> sind die Ebenen-Besonderheiten des
///    Pilgerpfads, benannt durch <c>DeepDungeon4GimmickEffectTransient</c>
///    ("Primordial Flesh", "Immolation", "Anointment", ... 10 Zeilen).
///
/// NICHTS WIRD ZWISCHENGESPEICHERT. Jeder Aufruf liest den Director neu: ein gepufferter
/// Wert wird zu einer veralteten Ansage, und der Ebenenzustand aendert sich in dem
/// Moment, in dem der Spieler eine Treppe nimmt.
/// </summary>
public sealed class DeepDungeonState
{
    private readonly IDataManager    _data;
    private readonly IPluginLog      _log;
    private readonly DeepDungeonText _text;

    public DeepDungeonState(IDataManager data, IPluginLog log, DeepDungeonText text)
    {
        _data = data;
        _log  = log;
        _text = text;
    }

    /// <summary>Ein benanntes Ding, das das Tiefe Gewoelbe auf den Spieler gelegt hat.</summary>
    /// <param name="Name">Der spieleigene Name dafuer.</param>
    /// <param name="Description">Die spieleigene Beschreibung, oder leer.</param>
    /// <param name="Kind">Aus welchem Teil des Gewoelbes es kommt, zur Gruppierung.</param>
    public readonly record struct DeepEffect(string Name, string Description, string Kind);

    /// <summary>Ob der Spieler gerade in einem Tiefen Gewoelbe ist.</summary>
    public unsafe bool IsActive => GetDirector() != null;

    /// <summary>
    /// Jede Wirkung, die das Tiefe Gewoelbe selbst anlegt, in der Reihenfolge, in der
    /// ein Spieler sie haben will: was die Ebene macht, dann was er selbst ausgeloest hat.
    ///
    /// Ausserhalb eines Tiefen Gewoelbes leer - ob das eine Ansage wert ist, entscheidet
    /// der Aufrufer, genau wie bei den Status-Kategorien.
    /// </summary>
    public unsafe List<DeepEffect> CollectEffects()
    {
        var effects = new List<DeepEffect>();

        var dd = GetDirector();
        if (dd == null) return effects;

        // -- Ebenenweite Zustaende: die drei, die der Director getrennt fuehrt. --
        // Id 0 heisst bei allen dreien "keiner" (Zeile 0 jedes dieser Sheets ist leer -
        // Offline-Auszug 2026-08-12), eine Null ist also Schweigen und kein Nachschlagen.
        AddFloorEffect(effects, dd->DeepDungeonStatusId, AccessibilityStrings.DeepKindFloor,
                       id => _data.GetExcelSheet<DeepDungeonStatus>()?.GetRowOrDefault(id)?.FloorEffectUI);
        AddFloorEffect(effects, dd->DeepDungeonBanId, AccessibilityStrings.DeepKindBan,
                       id => _data.GetExcelSheet<DeepDungeonBan>()?.GetRowOrDefault(id)?.FloorEffectUI);
        AddFloorEffect(effects, dd->DeepDungeonDangerId, AccessibilityStrings.DeepKindDanger,
                       id => _data.GetExcelSheet<DeepDungeonDanger>()?.GetRowOrDefault(id)?.FloorEffectUI);

        // -- Die Ebenen-Besonderheit des Pilgerpfads, aktuell und naechste. -----
        AddGimmick(effects, dd->DeepDungeonGimmickEffectIdCurrent, AccessibilityStrings.DeepKindGimmick);
        AddGimmick(effects, dd->DeepDungeonGimmickEffectIdNext,    AccessibilityStrings.DeepKindGimmickNext);

        // -- Pomander-Wirkungen, die der Spieler laufen hat. --------------------
        // IsActive ist dasselbe Bit, aus dem die Zeile "Item Effects" des Fensters zeichnet.
        var items = dd->Items;
        for (var slot = 0; slot < items.Length; slot++)
        {
            if (!items[slot].IsActive) continue;
            var item = ResolvePomander(dd->DeepDungeonId, slot, items[slot].ItemId);
            if (item is not { } row) continue;
            effects.Add(new DeepEffect(row.Name.ExtractText().Trim(),
                                       _text.Read(row.Tooltip),
                                       AccessibilityStrings.DeepKindItemEffect));
        }

        _log.Info($"[DeepDungeon] {effects.Count} Effekte: dungeon={dd->DeepDungeonId} floor={dd->Floor} "
                  + $"statusId={dd->DeepDungeonStatusId} banId={dd->DeepDungeonBanId} "
                  + $"dangerId={dd->DeepDungeonDangerId} gimmick={dd->DeepDungeonGimmickEffectIdCurrent}"
                  + $"/{dd->DeepDungeonGimmickEffectIdNext}.");
        return effects;
    }

    /// <summary>
    /// Der Pomander in einem Platz, aufgeloest so, wie das Spiel ihn aufloest.
    ///
    /// <paramref name="rawItemId"/> wird protokolliert, nie geglaubt: es ist das eigene
    /// Byte des Directors fuer den Platz, und ob es eine Sheet-Zeile oder eine
    /// Wiederholung des Platzes ist, steht nirgends belegt. Es neben dem aufgeloesten
    /// Namen ins Log zu schreiben ist das, was einen kuenftigen Widerspruch sichtbar
    /// macht statt still.
    /// </summary>
    private DeepDungeonItem? ResolvePomander(byte dungeonId, int slot, byte rawItemId)
    {
        var dungeon = _data.GetExcelSheet<DeepDungeon>()?.GetRowOrDefault(dungeonId);
        if (dungeon is not { } d)
        {
            _log.Warning($"[DeepDungeon] Keine DeepDungeon-Zeile {dungeonId} - Slot {slot} unbenannt.");
            return null;
        }

        if (slot < 0 || slot >= d.PomanderSlot.Count) return null;

        var reference = d.PomanderSlot[slot];
        var row = reference.ValueNullable;
        if (row is not { } r || r.RowId == 0) return null;

        if (rawItemId != r.RowId)
            _log.Info($"[DeepDungeon] Slot {slot}: ItemId={rawItemId}, PomanderSlot={r.RowId} "
                      + $"('{r.Name.ExtractText()}') - der Tabelle des Spiels wird gefolgt.");
        return r;
    }

    /// <summary>Fuegt eine Ebenen-Wirkungszeile hinzu, wenn die Id eine benennt.</summary>
    private void AddFloorEffect(List<DeepEffect> into, byte id, string kind,
                                Func<uint, RowRef<DeepDungeonFloorEffectUI>?> lookup)
    {
        if (id == 0) return;

        var reference = lookup(id);
        if (reference?.ValueNullable is not { } ui) return;

        var name = ui.Name.ExtractText().Trim();
        if (name.Length == 0) return;

        into.Add(new DeepEffect(name, _text.Read(ui.Description), kind));
    }

    /// <summary>Fuegt eine Ebenen-Besonderheit des Pilgerpfads hinzu, wenn die Id eine
    /// benennt.</summary>
    private void AddGimmick(List<DeepEffect> into, byte id, string kind)
    {
        if (id == 0) return;

        var row = _data.GetExcelSheet<DeepDungeon4GimmickEffectTransient>()?.GetRowOrDefault(id);
        if (row is not { } r) return;

        var name = r.Name.ExtractText().Trim();
        if (name.Length == 0) return;

        into.Add(new DeepEffect(name, _text.Read(r.Description), kind));
    }

    /// <summary>
    /// Der laufende Gewoelbe-Director, oder null, wenn der Spieler in keinem ist.
    ///
    /// try-catch, weil ClientStructs die Instanz ueber eine Byte-Signatur aufloest und
    /// WIRFT, wenn ein Patch sie verschiebt. Eine verschobene Signatur darf die
    /// Gewoelbe-Zeilen kosten, nicht die ganze Wirkungsliste.
    /// </summary>
    internal unsafe InstanceContentDeepDungeon* GetDirector()
    {
        try
        {
            var framework = EventFramework.Instance();
            if (framework == null) return null;

            var director = framework->GetInstanceContentDirector();
            if (director == null) return null;

            // Die Typpruefung ist das, was die Umwandlung sicher macht: jeder
            // instanzierte Inhalt gibt einen InstanceContentDirector zurueck, und nur
            // einer davon ist ein Tiefes Gewoelbe (InstanceContentType.DeepDungeon).
            // Mit Absicht voll qualifiziert: Lumina hat ein gleichnamiges SHEET
            // InstanceContentType, und die beiden haben nichts miteinander zu tun. Genau
            // diese Kollision hat hier schon einmal zugeschlagen - ein plausibel
            // aussehendes Nachschlagen im falschen Namensraum uebersetzt sauber und
            // antwortet dann ueber etwas anderes.
            if (director->InstanceContentType
                != FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.InstanceContentType.DeepDungeon)
                return null;

            return (InstanceContentDeepDungeon*)director;
        }
        catch (Exception ex)
        {
            _log.Warning($"[DeepDungeon] Director nicht lesbar: {ex.Message}");
            return null;
        }
    }
}
