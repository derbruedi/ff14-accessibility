using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace FF14Accessibility.Services;

/// <summary>
/// Whether an option row of the duty-finder settings window
/// (ContentsFinderSetting) is switched on, plus the names of its language boxes.
///
/// WHY NOT THE OBVIOUS SOURCE: Client::Game::UI::ContentsFinder holds all these
/// settings as plain bools (IsUnrestrictedParty, IsLevelSync, IsMinimalIL,
/// IsSilenceEcho, IsExplorerMode, IsLimitedLevelingRoulette, LootRules) - but
/// they are the SAVED state, not what the open window shows. MEASURED, twice,
/// in both directions (log 2026-08-19 19:36):
///   19:36:22 window opens, saved state True; player switches the row OFF at
///            19:36:28; "Ok" -> the game itself writes "Teilnahmebedingungen
///            wurden wie folgt festgelegt: -" (nothing set).
///   19:36:39 window opens, saved state now False; player switches the row ON
///            at 19:36:46; "Ok" -> "... festgelegt: Keine Beschränkungen".
/// The window edits a working copy and only writes it back on "Ok". Announcing
/// the singleton would therefore have kept saying "aus" right after the player
/// switched a row on - and they cannot see the box to notice.
///
/// WHERE THE WORKING COPY IS: in the row itself. Each row is an
/// AtkComponentButton (NOT a CheckBox - its IsChecked bit stayed False through
/// every toggle, exactly as FFXIVClientStructs documents that bit to be for
/// CheckBox/RadioButton only). What flips is the PartId of the state glyph at
/// the row's right edge, together with a background and a label swap:
///   AN : Bild-Teil 0, NineGrid 8 sichtbar / 9 versteckt, Text 6 sichtbar
///   AUS: Bild-Teil 1, NineGrid 9 sichtbar / 8 versteckt, Text 7 sichtbar
/// The glyph is read because it is the one carrier with a meaning of its own -
/// it is the box a sighted player looks at - and it is found by GEOMETRY (the
/// rightmost image of the row), so no node id has to hold still across patches.
/// </summary>
internal sealed unsafe class ContentsFinderSettingText
{
    /// <summary>Image part of the state glyph when the row is switched on. Measured
    /// 2026-08-19 against the game's own confirmation message, in both directions.</summary>
    private const ushort PartOn  = 0;
    private const ushort PartOff = 1;

    /// <summary>Language boxes left to right. The four options exist under exactly
    /// these names in the game's own configuration (Dalamud UiConfigOption:
    /// ContentsFinderUseLangTypeJA / EN / DE / FR). CONFIRMED in game 2026-08-19:
    /// on a German client with only German selected, the boxes read out as
    /// "Japanisch, aus", "Englisch, aus", "Deutsch, an".</summary>
    private static readonly string[] LanguageOrder = ["JA", "EN", "DE", "FR"];

    private readonly IPluginLog _log;

    public ContentsFinderSettingText(IPluginLog log) => _log = log;

    /// <summary>
    /// On/off of an option row, read from the state glyph at its right edge.
    /// Null when the row carries no such glyph (then it is not an option row -
    /// "Ok" and "Schließen" hold no image at all) or the glyph shows a part
    /// whose meaning was never measured, in which case the caller announces the
    /// row without a state rather than guessing at it.
    /// </summary>
    public bool? RowState(AtkResNode* rowNode, AtkComponentBase* comp, out ushort part)
    {
        part = ushort.MaxValue;
        if (rowNode == null || comp == null) return null;

        // Rightmost image of the row. The row also carries a category icon on
        // its LEFT, which must not be mistaken for the state glyph.
        AtkImageNode* glyph = null;
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var n = comp->UldManager.NodeList[i];
            if (n == null || n->Type != NodeType.Image || !n->IsVisible()) continue;
            if (glyph == null || n->ScreenX > glyph->AtkResNode.ScreenX) glyph = (AtkImageNode*)n;
        }
        if (glyph == null) return null;

        // Must sit in the row's right half, otherwise the row has no state glyph
        // at all and the left icon would be read as one.
        if (glyph->AtkResNode.ScreenX < rowNode->ScreenX + rowNode->Width / 2f) return null;

        part = glyph->PartId;
        return glyph->PartId switch
        {
            PartOn  => true,
            PartOff => false,
            _       => LogUnknownPart(glyph->PartId),
        };
    }

    private bool? LogUnknownPart(ushort part)
    {
        if (part == _warnedPart) return null;
        _warnedPart = part;
        _log.Warning($"[Inhaltssuche] Zustandssymbol einer Optionszeile zeigt Teil {part} - "
                     + "gemessen sind nur 0 (an) und 1 (aus). Zustand wird nicht angesagt.");
        return null;
    }

    private int _warnedPart = -1;

    /// <summary>Name of the language box at <paramref name="index"/> from the left,
    /// or "" when the window shows a different number of boxes than the game has
    /// language options.</summary>
    public string LanguageAt(int index, int visibleBoxes)
    {
        if (visibleBoxes != LanguageOrder.Length || index < 0 || index >= LanguageOrder.Length)
            return string.Empty;
        return LanguageOrder[index] switch
        {
            "JA" => AccessibilityStrings.DutyLanguageJapanese,
            "EN" => AccessibilityStrings.DutyLanguageEnglish,
            "DE" => AccessibilityStrings.DutyLanguageGerman,
            "FR" => AccessibilityStrings.DutyLanguageFrench,
            _    => string.Empty,
        };
    }
}
