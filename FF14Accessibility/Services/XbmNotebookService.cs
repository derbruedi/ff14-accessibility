using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// Eine Bestie im Bestienbuch: Nummer, Name, optionaler Fundort, Beschreibung.
/// </summary>
/// <param name="Number">Kachel-Nummer 1..50 (= XBMPet RowId).</param>
/// <param name="Name">Anzeigename aus Sheet Pet.</param>
/// <param name="Habitat">Fundort (PlaceName), leer wenn keiner (z. B. Cu Sith).</param>
/// <param name="Description">Fliesstext aus XBMPet.</param>
/// <param name="PlaceNameId">PlaceName-Zeile des Fundorts (0 = keiner).</param>
public readonly record struct XbmPetInfo(
    byte   Number,
    string Name,
    string Habitat,
    string Description,
    ushort PlaceNameId);

/// <summary>
/// Liest das Bestienbuch des Bestienbaendigers (Addon <c>XBMMonsterNotebook</c>).
///
/// <para>
/// Kacheln tragen nur "Nr. N" (Dump/Log 2026-09-24). Aufloesung:
/// XBMPet RowId = Nummer; Col0 → Sheet <c>Pet</c> Name; Col7 → PlaceName
/// (Fundort); Col8 = Beschreibung. Typisiertes XBMPet ist unbrauchbar
/// (ColumnHash-Mismatch) — deshalb RawRow.
/// </para>
///
/// <para>
/// Ansage wie ein Sehender die Kachel/Detail liest: zuerst Nummer, Name und
/// Fundort; die Beschreibung folgt beim Verweilen (wie AOZNotebook).
/// </para>
/// </summary>
public sealed class XbmNotebookService
{
    /// <summary>Name des Fensters, wie das Spiel es fuehrt.</summary>
    public const string AddonName = "XBMMonsterNotebook";

    /// <summary>Excel-Sheet der Bestien-Zeilen.</summary>
    public const string SheetName = "XBMPet";

    /// <summary>Anzahl Bestien im Buch (XBMManager: 50 Unlocks).</summary>
    public const byte PetCount = 50;

    // RawRow-Spalten (offline DE+EN 2026-09-24 gegen Spieldaten geprueft).
    private const int ColPetId      = 0; // Int32 → Pet.RowId
    private const int ColPlaceName  = 7; // UInt16 → PlaceName.RowId (0 = keiner)
    private const int ColDescription = 8; // String

    private static readonly Regex NumberLabel = new(
        @"^(?:Nr\.|No\.|no\.)\s*(\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDataManager _data;
    private readonly IPluginLog  _log;

    private Dictionary<byte, XbmPetInfo>? _byNumber;
    private HashSet<byte>? _loggedMiss;
    private bool _sheetLogged;

    public XbmNotebookService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>
    /// Wenn <paramref name="raw"/> ein nacktes Nummernschild ist ("Nr. 12"),
    /// liefert die Kurz-Ansage (Nummer, Name, Fundort). Sonst unveraendert.
    /// </summary>
    public string EnrichNumberLabel(string raw)
    {
        if (!TryParseNumberLabel(raw, out var number)) return raw;
        return DescribeTile(number);
    }

    /// <summary>
    /// Kurz-Ansage fuer die Kachel: Nummer, Name, Fundort — ohne Beschreibung,
    /// damit schnelles Blaettern kurz bleibt.
    /// </summary>
    public string DescribeTile(byte number)
    {
        if (number == 0) return string.Empty;
        if (!TryGetPet(number, out var pet))
        {
            _loggedMiss ??= new HashSet<byte>();
            if (_loggedMiss.Add(number))
                _log.Info($"[XBM] MISS Nr. {number} — kein Eintrag in {SheetName}/Pet.");
            return AccessibilityStrings.XbmPetNumberOnly(number);
        }

        return AccessibilityStrings.XbmPetTile(pet.Number, pet.Name, pet.Habitat);
    }

    /// <summary>Beschreibungstext zum Verweilen, oder leer.</summary>
    public string DescribeDetails(byte number)
    {
        if (!TryGetPet(number, out var pet)) return string.Empty;
        return pet.Description;
    }

    /// <summary>Vollstaendiger Eintrag zur Nummer, falls bekannt.</summary>
    public bool TryGetPet(byte number, out XbmPetInfo pet)
    {
        pet = default;
        if (number == 0) return false;
        BuildCache();
        return _byNumber!.TryGetValue(number, out pet);
    }

    /// <summary>True, wenn der Text genau ein Bestienbuch-Nummernschild ist.</summary>
    public static bool TryParseNumberLabel(string text, out byte number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var m = NumberLabel.Match(text.Trim());
        if (!m.Success) return false;
        if (!byte.TryParse(m.Groups[1].Value, out var n) || n == 0 || n > PetCount)
            return false;
        number = n;
        return true;
    }

    private void BuildCache()
    {
        if (_byNumber != null) return;

        var map = new Dictionary<byte, XbmPetInfo>();
        try
        {
            var sheet = _data.GameData.GetExcelSheet<RawRow>(name: SheetName);
            if (sheet == null)
            {
                _log.Warning($"[XBM] Sheet {SheetName} nicht verfuegbar.");
                _byNumber = map;
                return;
            }

            // Pet-Namen: typisiert wenn moeglich, sonst RawRow Spalte 0.
            var petTyped = TryGetPetSheet();
            var petRaw   = petTyped == null
                ? _data.GameData.GetExcelSheet<RawRow>(name: "Pet")
                : null;

            foreach (var row in sheet)
            {
                if (row.RowId is 0 or > PetCount) continue;

                var petId = (uint)SafeInt32(row, ColPetId);
                var name  = ResolvePetName(petId, petTyped, petRaw);
                var placeId = SafeUInt16(row, ColPlaceName);
                var habitat = ResolvePlaceName(placeId);
                var desc    = Clean(SafeString(row, ColDescription));

                if (row.RowId == 1 && !_sheetLogged)
                {
                    _sheetLogged = true;
                    _log.Info(
                        $"[XBM] Zeile 1: PetId={petId} name='{name}' place={placeId} " +
                        $"habitat='{habitat}' desc='{Trunc(desc)}'.");
                }

                if (name.Length == 0) continue;
                map[(byte)row.RowId] = new XbmPetInfo(
                    (byte)row.RowId, name, habitat, desc, placeId);
            }

            _log.Info($"[XBM] Sheet geladen: {map.Count} Bestien (Pet+PlaceName+Beschreibung).");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"[XBM] Sheet {SheetName} nicht lesbar — nur Nummern.");
        }

        _byNumber = map;
    }

    private ExcelSheet<Pet>? TryGetPetSheet()
    {
        try
        {
            return _data.GetExcelSheet<Pet>();
        }
        catch (Exception ex)
        {
            _log.Info($"[XBM] Typed Pet-Sheet nicht nutzbar ({ex.GetType().Name}) — RawRow.");
            return null;
        }
    }

    private static string ResolvePetName(uint petId, ExcelSheet<Pet>? typed, ExcelSheet<RawRow>? raw)
    {
        if (petId == 0) return string.Empty;
        if (typed != null && typed.TryGetRow(petId, out var pet))
        {
            var n = Clean(pet.Name.ExtractText());
            if (n.Length > 0) return n;
        }
        if (raw != null && raw.HasRow(petId))
            return Clean(SafeString(raw.GetRow(petId), 0));
        return string.Empty;
    }

    private string ResolvePlaceName(ushort placeId)
    {
        if (placeId == 0) return string.Empty;
        try
        {
            if (_data.GetExcelSheet<PlaceName>()?.TryGetRow(placeId, out var row) == true)
                return Clean(row.Name.ExtractText());
        }
        catch (Exception ex)
        {
            _log.Info($"[XBM] PlaceName {placeId} nicht lesbar: {ex.GetType().Name}");
        }
        return string.Empty;
    }

    private static int SafeInt32(RawRow row, int column)
    {
        try { return row.ReadInt32Column(column); }
        catch { return 0; }
    }

    private static ushort SafeUInt16(RawRow row, int column)
    {
        try { return row.ReadUInt16Column(column); }
        catch { return 0; }
    }

    private static string SafeString(RawRow row, int column)
    {
        try { return row.ReadStringColumn(column).ExtractText(); }
        catch { return string.Empty; }
    }

    private static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return string.Join(" ", raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string Trunc(string s) =>
        s.Length <= 60 ? s : s[..57] + "...";
}
