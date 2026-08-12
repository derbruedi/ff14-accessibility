using System;
using Dalamud.Plugin.Services;
using Lumina.Data;

namespace FF14Accessibility.Services;

/// <summary>
/// Resolves a character-creation colour swatch INDEX to the
/// actual RGB the game will paint, by reading the game's own palette file
/// <c>chara/xls/charamake/human.cmp</c>.
/// WHY: the colour menus are pure image grids. <c>CharaMakeType.SubMenuParam</c> is
/// all zeroes for every one of them, and no Excel sheet has a row per swatch, so
/// the sheets can only ever say "37 of 192". The RGB has to come from this file.
/// LAYOUT - measured, not assumed. Offsets were taken from two independent
/// open-source readers (Anamnesis <c>ColorData.cs</c>, Ktisis <c>CharaCmpReader.cs</c>)
/// and then VERIFIED against the installed file on 2026-08-08:
/// <list type="bullet">
/// <item>An autocorrelation scan of the file found a 1280-entry period and a
///   256-entry sub-block with no prior knowledge - exactly the CHUNK (0x1400) and
///   PALETTE (0x400) sizes both readers use. The 32 chunks fill entries
///   4608..45567 and leave a 1120-entry tail (the racial scaling data).</item>
/// <item>The tribe ordering was confirmed SEMANTICALLY, which is what rules out an
///   off-by-one: with <c>tribeGender = (tribe-1)*2 + sex</c>, palette 3 of the Sea
///   Wolf chunk is 84/192 green swatches, Xaela is 86/192 blue, The Lost is
///   113/192 blue plus 23 green, Helions peaks at a golden #C6AF3B, and every
///   Hyur/Elezen/Lalafell chunk is pure warm skin with zero green or blue. Those
///   are the right tribes wearing the right skins.</item>
/// <item>Byte order is R,G,B,A: Midlander's lightest skin swatch reads #F6D0BB
///   (pale peach) that way and #BBD0F6 (pale blue) the other way.</item>
/// </list>
/// WHERE THE TWO READERS DISAGREE, it does not matter, and that was measured too:
/// for the eye palette Anamnesis points at block 0 and Ktisis at block 5, and those
/// two blocks are byte-identical over their first 192 entries. For tattoo colour
/// the candidates are block 0 and block 12, whose per-index colour distance is at
/// most 11.5 of a possible 441 - far below any naming boundary. Lip and face paint
/// (blocks 11 and 13) agree on 191 of 192 entries. So no menu's spoken name can
/// change depending on which reading is right.
/// NOT COVERED, deliberately: nothing here names a swatch for a menu whose palette
/// was not pinned. Every colour menu in the game maps onto one of the five
/// CustomizeData bytes below (verified from CharaMakeType: Fur Color writes byte 10
/// exactly like Hair Color, and Limbal Ring / Ear Clasp Color write byte 13 exactly
/// like Tattoo Color), so there is no menu left over.
/// </summary>
public sealed class CharaMakePalette
{
    private const string CmpPath = "chara/xls/charamake/human.cmp";

    // 0x4800 / 4 - start of the per-tribe/gender chunks.
    private const int UniqueBaseEntry = 4608;
    // 0x1400 / 4 - one chunk per (tribe, gender).
    private const int ChunkEntries = 1280;
    // 0x400 / 4 - one palette slot.
    private const int PaletteEntries = 256;

    private const int SkinPaletteSlot = 3;
    private const int HairPaletteSlot = 4;

    // Shared palettes, as entry indices.
    private const int SharedGenericEntry = 0;                    // eye, tattoo, limbal ring, ear clasp
    private const int LipEntry           = 11 * PaletteEntries;  // 2816
    private const int FacePaintEntry     = 13 * PaletteEntries;  // 3328

    /// <summary>Every palette in this file is laid out as ramps of eight shades,
    /// light to dark (measured: entries 0-7 are a grey ramp, 8-15 a cream ramp,
    /// and so on for all 24 ramps). Announcing the ramp gives a blind player a
    /// mental map of the grid that a flat index cannot.</summary>
    public const int ShadesPerRamp = 8;

    private readonly IPluginLog _log;
    private readonly byte[]? _data;

    public CharaMakePalette(IDataManager data, IPluginLog log)
    {
        _log = log;
        try
        {
            var file = data.GetFile<FileResource>(CmpPath);
            _data = file?.Data;
            if (_data == null)
                _log.Warning($"[CharaMake] {CmpPath} not found - colour names unavailable, indices only.");
            else
                _log.Info($"[CharaMake] Colour palette loaded: {_data.Length} bytes ({_data.Length / 4} swatches).");
        }
        catch (Exception ex)
        {
            // External file access: the one place a try/catch is the right tool.
            // Failure degrades to index-only announcements, never to silence.
            _data = null;
            _log.Error(ex, $"[CharaMake] Failed to read {CmpPath} - colour names unavailable.");
        }
    }

    /// <summary>True when the palette file loaded and colour names can be spoken.</summary>
    public bool IsAvailable => _data != null;

    /// <summary>
    /// Maps the CustomizeData byte a colour menu writes to the palette that menu
    /// draws from. Returns false for any byte that is not a colour.
    /// </summary>
    public static bool IsColorIndex(uint customizeIndex) => customizeIndex is 8 or 9 or 10 or 13 or 15 or 20 or 25;

    /// <summary>The vocabulary to describe this menu's swatches with.</summary>
    public static ColorNamer.Kind KindOf(uint customizeIndex) => customizeIndex switch
    {
        8  => ColorNamer.Kind.Skin,
        9  => ColorNamer.Kind.Eye,
        15 => ColorNamer.Kind.Eye,
        10 => ColorNamer.Kind.Hair,
        20 => ColorNamer.Kind.Lip,
        25 => ColorNamer.Kind.FacePaint,
        _  => ColorNamer.Kind.Generic,
    };

    /// <summary>
    /// Reads swatch <paramref name="swatch"/> of the palette that CustomizeData
    /// byte <paramref name="customizeIndex"/> selects from, for the given tribe and
    /// sex. False when the file is missing, the byte is not a colour, or the index
    /// falls outside the file - never a guessed colour.
    /// </summary>
    public bool TryGetSwatch(uint customizeIndex, byte tribe, byte sex, int swatch,
                             out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (_data == null || swatch < 0) return false;

        int entry;
        switch (customizeIndex)
        {
            case 8:  // SkinColor - per tribe and sex
            case 10: // HairColor / Fur Color - per tribe and sex
                if (tribe is < 1 or > 16 || sex > 1) return false;
                var slot = customizeIndex == 8 ? SkinPaletteSlot : HairPaletteSlot;
                var tribeGender = ((tribe - 1) * 2) + sex;
                entry = UniqueBaseEntry + (tribeGender * ChunkEntries) + (slot * PaletteEntries) + swatch;
                break;

            case 9:  // EyeColorRight
            case 15: // EyeColorLeft
            case 13: // TattooColor, and with it Limbal Ring and Ear Clasp Color
                entry = SharedGenericEntry + swatch;
                break;

            case 20: entry = LipEntry + swatch; break;       // LipColor
            case 25: entry = FacePaintEntry + swatch; break; // FacePaintColor

            default: return false;
        }

        var at = entry * 4;
        if (at < 0 || at + 3 >= _data.Length) return false;

        r = _data[at];
        g = _data[at + 1];
        b = _data[at + 2];
        return true;
    }

    /// <summary>
    /// Spoken description of a swatch, or null when the palette cannot supply it.
    /// </summary>
    public string? DescribeSwatch(uint customizeIndex, byte tribe, byte sex, int swatch)
    {
        if (!TryGetSwatch(customizeIndex, tribe, sex, swatch, out var r, out var g, out var b))
            return null;
        return ColorNamer.Describe(r, g, b, KindOf(customizeIndex));
    }

    /// <summary>Hex form, for the log only. Never spoken.</summary>
    public string HexOrEmpty(uint customizeIndex, byte tribe, byte sex, int swatch)
        => TryGetSwatch(customizeIndex, tribe, sex, swatch, out var r, out var g, out var b)
            ? $"#{r:X2}{g:X2}{b:X2}"
            : string.Empty;
}
