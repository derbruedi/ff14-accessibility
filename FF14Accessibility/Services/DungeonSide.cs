using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Fuehrt dieses Objekt in einen INHALT, und in welchen?
///
/// User: *"there needs to be a dungeon category. currently they are not set apart
/// and are simply called 'entrance' in the objects category"* und *"right now they
/// are just called entrance instead of having more useful names like the name of
/// the dungeon."* Die Antwort muss also den NAMEN tragen, nicht nur ein
/// Kategoriewort.
///
/// WIE DAS OBJEKT AN DEN INHALT KOMMT. Ein <c>EObj.Data</c> ist eine 32-Bit-
/// Ereignis-Id, deren obere 16 Bit den Handler-Typ angeben - dieselbe Zerlegung,
/// die <c>ShopNpcService</c> fuer die Laeden benutzt. Zwei Bloecke zaehlen:
///
///   EObj.Data --Block 0x001D--> InstanceContentGuide-Zeile --Spalte 0--> InstanceContent
///   EObj.Data --Block 0x000D--> ArrayEventHandler-Zeile --------------> (eine 0x001D-Id)
///                                                        --Spalte 0--> InstanceContent
///   InstanceContent --ContentFinderCondition.Content--> NAME, ContentType, Stufe
///
/// <c>ArrayEventHandler</c> ist ein VERTEILER: seine 16 Spalten sind selbst
/// Ereignis-Ids, und eine davon ist oft eine <c>InstanceContentGuide</c>-Id. Genau
/// deshalb tragen nur 18 EObj den Block 0x001D direkt, waehrend das Guide-Sheet 344
/// Zeilen hat. Gemessen: KEINE ArrayEventHandler-Zeile zeigt auf eine weitere
/// ArrayEventHandler-Zeile, ein Sprung ist also der ganze Graph - die Suche ist
/// nicht abgeschnitten.
///
/// WARUM DER NAHELIEGENDE WEG FALSCH IST, festgehalten damit er nicht noch einmal
/// probiert wird: das LOW WORD der Ereignis-Id als <c>ContentFinderCondition</c>-
/// Zeilennummer zu lesen ist keine Referenz, sondern eine Zahlenkollision. Sie
/// liefert <c>'Waking Sands entrance' -> 'Alexander (Savage)'</c> und
/// <c>'market board' -> 'the Aery'</c>. Block 0x000D gehoert
/// <c>ArrayEventHandler</c> und 0x001D <c>InstanceContentGuide</c>; das Low Word
/// allein bedeutet nichts.
///
/// GEMESSEN, offline gegen das installierte sqpack:
/// <list type="bullet">
/// <item>Der Block-zu-Sheet-Scan lief ueber alle 7.780 lesbaren Sheets und fand die
///   vier bereits bekannten Bloecke unabhaengig wieder - 0x0002 Warp 455/455,
///   0x0004 GilShop 776/776, 0x0016 GCShop 3/3, 0x001B SpecialShop 376/376. Diese
///   Kontrolle ist es, die seine Antwort fuer 0x000D brauchbar macht.</item>
/// <item>Alle 343 von 0 verschiedenen Spalte-0-Werte in
///   <c>InstanceContentGuide</c> sind Zeilen von <c>InstanceContent</c>.</item>
/// <item>182 EObj im ganzen Spiel loesen zu einem Inhalt auf, und JEDES davon
///   heisst 'entrance' (177), 'entry point' (2), 'destination' (2) oder
///   'infiltration point' (1). Null Fehltreffer - genau der Test, den der
///   Low-Word-Weg nicht besteht.</item>
/// </list>
///
/// 141 der 182 benennen einen Inhalt; die uebrigen 41 zeigen auf eine namenlose
/// <c>ContentFinderCondition</c>-Zeile fuer ein Ueberwelt-Gebiet. Die liefern null
/// und das Objekt heisst weiter "Objekt" - ein Objekt, dessen Inhalt sich nicht
/// aufloesen laesst, darf nie zur Vermutung werden.
/// </summary>
internal static class DungeonSide
{
    /// <summary>InstanceContentGuide - der Inhalts-Handler, direkt getragen.</summary>
    private const ushort BlockGuide = 0x001D;

    /// <summary>ArrayEventHandler - der Verteiler, ueber den die meisten Eingaenge gehen.</summary>
    private const ushort BlockDispatcher = 0x000D;

    /// <summary>
    /// Wohin ein Eingang fuehrt. <paramref name="Level"/> ist 0, wenn der Inhalt
    /// keine Stufe verlangt. <paramref name="TypeName"/> ist das WORT DES SPIELS
    /// fuer die Inhaltsart, in der Client-Sprache, damit auch eine hier nicht
    /// zugeordnete Art gesprochen statt verworfen wird - siehe
    /// <see cref="AccessibilityStrings.DutyEntrance"/>.
    /// </summary>
    /// <param name="ContentId">
    /// Die <c>InstanceContent</c>-Zeile hinter dem Inhalt. Nicht gesprochen -
    /// sie ist der Schluessel, mit dem das SPIEL gefragt wird, ob der Inhalt
    /// freigeschaltet ist (<c>UIState.IsInstanceContentUnlocked</c>), statt das
    /// aus Stufe oder Questfortschritt zu erraten.
    /// </param>
    internal readonly record struct Duty(string Name, uint ContentType, ushort Level, string TypeName, uint ContentId);

    /// <summary>
    /// EObj-Zeilennummer -> der Inhalt, in den sie fuehrt. Wird beim ersten Zugriff
    /// einmal gebaut: der Lauf beruehrt drei ganze Sheets und die Antwort aendert
    /// sich innerhalb einer Spielversion nie, waehrend <see cref="Describe"/> bei
    /// jedem Schritt des Objekt-Browsers gefragt wird.
    /// </summary>
    private static Dictionary<uint, Duty>? _byObject;

    /// <summary>
    /// Die ganze Tabelle EObj-Zeile -> Inhalt, ueber die ganze Welt und ohne dass
    /// ein Objekt geladen sein muss. <see cref="Describe"/> beantwortet "wohin
    /// fuehrt DIESE Tuer vor mir", diese Methode "welche Tueren gibt es
    /// ueberhaupt" - die Grundlage der weltweiten Inhaltsliste im Objekt-Browser
    /// (<see cref="DutyEntranceService"/>). Dieselbe einmal gebaute Map, es wird
    /// nichts doppelt gelesen.
    /// </summary>
    internal static IReadOnlyDictionary<uint, Duty> All(IDataManager data, IPluginLog log)
        => _byObject ??= Build(data, log);

    /// <summary>
    /// Der Inhalt, in den dieses Objekt fuehrt, oder null wenn es kein Eingang ist.
    ///
    /// Null fuer alles, was kein <c>EventObj</c> ist, fuer ein Objekt dessen
    /// Ereignisdaten keine Guide-Zeile erreichen, und fuer einen Inhalt den das
    /// Spiel nicht benennt - in jedem dieser Faelle bleibt die Ansage genau so, wie
    /// sie heute ist.
    /// </summary>
    internal static Duty? Describe(IGameObject obj, IDataManager data, IPluginLog log)
    {
        if (obj.ObjectKind != ObjectKind.EventObj) return null;

        // BaseId IST die EObj-Zeilennummer - derselbe DataId->Sheet-Weg, den
        // ShopNpcService fuer ENpcBase und NavigationService fuer Sammelpunkte geht.
        var map = _byObject ??= Build(data, log);
        return map.TryGetValue(obj.BaseId, out var duty) ? duty : null;
    }

    private static Dictionary<uint, Duty> Build(IDataManager data, IPluginLog log)
    {
        var map = new Dictionary<uint, Duty>();

        // InstanceContentGuide und ArrayEventHandler haben keine getypte
        // Lumina-Struktur, werden also als Rohzeilen NACH NAMEN und ueber den
        // Spaltenindex gelesen. Beide wurden vorher offline gedumpt: der Guide hat
        // 2 UInt32-Spalten und nur Spalte 0 ist je ungleich 0, ArrayEventHandler hat
        // 16 UInt32-Spalten mit Ereignis-Ids.
        //
        // Abgesichert, weil ein Sheet-Zugriff ueber den Namen eine WANDERNDE
        // SPIEL-API ist: benennt ein Patch eines der beiden um oder loescht es,
        // wuerde die Ausnahme sonst durch die Objektansage durchschlagen und nicht
        // nur diese Kategorie, sondern jede Objekt- und Zielansage toeten. Die leere
        // Map faellt exakt auf das bisherige Verhalten zurueck ("Objekt"), und sie
        // wird geloggt statt verschluckt: fuer einen blinden Spieler ist Stille von
        // "funktioniert" nicht zu unterscheiden.
        ExcelSheet<RawRow> guide;
        ExcelSheet<RawRow> dispatcher;
        try
        {
            guide = data.Excel.GetSheet<RawRow>(null, "InstanceContentGuide");
            dispatcher = data.Excel.GetSheet<RawRow>(null, "ArrayEventHandler");
        }
        catch (Exception ex)
        {
            log.Error(ex, "[DungeonSide] InstanceContentGuide/ArrayEventHandler nicht lesbar - "
                          + "Inhalts-Eingaenge sagen weiterhin \"Objekt\".");
            return map;
        }

        // InstanceContent-Zeile -> die Inhaltssuche-Zeile, die sie benennt.
        var byContent = new Dictionary<uint, Duty>();
        foreach (var cfc in data.GetExcelSheet<ContentFinderCondition>())
        {
            var contentId = cfc.Content.RowId;
            if (contentId == 0) continue;

            var name = cfc.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;   // die 41 namenlosen Zeilen

            // Der erste gewinnt: 299 Zeilen teilen sich einen InstanceContent, und die
            // Dubletten sind derselbe Inhalt erneut gelistet, nicht ein anderer.
            if (!byContent.ContainsKey(contentId))
                byContent[contentId] = new Duty(
                    name,
                    cfc.ContentType.RowId,
                    cfc.ClassJobLevelRequired,
                    cfc.ContentType.ValueNullable?.Name.ToString() ?? string.Empty,
                    contentId);
        }

        // Eine Guide-Ereignis-Id -> der Inhalt, in den sie fuehrt.
        Duty? FromGuide(uint eventId)
        {
            if (!guide.TryGetRow(eventId, out var row)) return null;
            var contentId = (uint)row.ReadColumn(0);
            return byContent.TryGetValue(contentId, out var duty) ? duty : null;
        }

        foreach (var obj in data.GetExcelSheet<EObj>())
        {
            var eventId = obj.Data.RowId;
            if (eventId == 0) continue;

            switch ((ushort)(eventId >> 16))
            {
                case BlockGuide:
                    if (FromGuide(eventId) is { } direct) map[obj.RowId] = direct;
                    break;

                case BlockDispatcher:
                    if (!dispatcher.TryGetRow(eventId, out var row)) break;
                    for (var col = 0; col < dispatcher.Columns.Count; col++)
                    {
                        var target = (uint)row.ReadColumn(col);
                        if (target == 0 || (ushort)(target >> 16) != BlockGuide) continue;
                        if (FromGuide(target) is not { } hopped) continue;
                        map[obj.RowId] = hopped;
                        break;
                    }
                    break;
            }
        }

        log.Info($"[DungeonSide] {map.Count} Objekte fuehren in einen benannten Inhalt.");
        return map;
    }
}
