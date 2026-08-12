// The one thing an action tooltip does NOT say in words: the SHAPE.
//
// User, after hearing the verbatim tooltip: *"can we do anything useful with
// radius? are we able to see the shape of the skill and create phrasing to denote
// actual radius? obviously the game is saying something like 5y circle or 3y cone,
// but the mod wouldn't announce the visual element there unless we give it a text
// equivalent."*
//
// They are right, and this is the ONE place where composing from the sheet is the
// correct answer rather than the the AoE work mistake. The rule the mod follows is "read the
// window, do not recompute what the game already writes" - but the game does NOT
// write the shape anywhere in that window. It DRAWS it: the tooltip says
// "Radius, 5y" and the shape reaches a sighted player through the telegraph
// graphic. A blind player gets the number and loses the geometry. Supplying a text
// equivalent for a purely visual element is exactly what this mod is for, and it
// takes nothing away from the verbatim readout - it is appended to it.
//
// AND IT FIXES A REAL AMBIGUITY IN THE GAME'S OWN LABEL. For a cone or a line the
// field the tooltip calls "Radius" is not a radius at all, it is the LENGTH
// (Kahlrodung CastType 3, EffectRange 6 = length; Spalten CastType 4,
// EffectRange 30 = length). Treating a line as a circle was the V1 AoE bug. So for
// those two shapes the phrase says "lang"/"long" outright.
//
// WHAT IS PROVEN AND WHAT IS NOT - the whole reason this file is conservative:
// CastType 2/3/4 were pinned to real shapes empirically, from the `[AoeProbe]` log
// plus the Omen graphic name, at the training ground 2026-07-26. Every OTHER
// CastType number is community lore that this project has never verified, and
// game-api.md says in as many words: "NICHT hartcodiert raten." So an unverified
// CastType produces NO shape phrase at all. Silence is correct there; a guess would
// be a confident lie about geometry the player is standing in.
using System;
using Dalamud.Plugin.Services;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace FF14Accessibility.Services;

/// <summary>
/// The geometry of an action, as words, for the shape the game only ever
/// draws. Appended to the verbatim tooltip; never replaces any of it. Returns ""
/// whenever the shape is not one this project has proven.
/// </summary>
public sealed class ActionShapeService
{
    private readonly IDataManager _data;
    private readonly IPluginLog   _log;

    /// <summary>Creates the describer.</summary>
    public ActionShapeService(IDataManager data, IPluginLog log)
    {
        _data = data;
        _log  = log;
    }

    /// <summary>
    /// A phrase like "Kreis" / "Kegel, 90 Grad, 6 Yalm lang", or "" when the action
    /// has no verified ground shape.
    /// </summary>
    public string Describe(uint actionId)
    {
        if (actionId == 0) return string.Empty;
        if (!_data.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row)) return string.Empty;

        // The gate is CastType, never EffectRange. A single-target attack can carry
        // a non-zero EffectRange - measured, and the reason the AoE work keys on
        // shape rather than range - so "EffectRange > 0" would announce an area
        // around skills that have none.
        //
        // Renamed with the method (IsGroundTelegraph ->
        // HasProvenShape) and it now covers four more CastTypes, which brings the
        // shape phrase to abilities that never had one.
        //
        // The Omen gate that CombatService.UpdateAoeWarning gained on the same day
        // is deliberately NOT applied here, and the difference is the point. That
        // gate asks "does the game DRAW a marker on the floor" - the right question
        // for a dodge cue. A tooltip asks what the ability's AREA is, and plenty of
        // real areas are never drawn: a party heal is a circle centred on the
        // caster whether or not anything is painted on the ground, and the player
        // reading its tooltip still needs to know it is a circle.
        if (!AoeShape.HasProvenShape(row.CastType)) return string.Empty;

        switch (row.CastType)
        {
            case AoeShape.CastTypeCircle:
            case AoeShape.CastTypeCircle5:
                // The tooltip's "Radius, Ny" is accurate for a circle, and it was
                // spoken one word earlier - so the yardage is not repeated.
                return AccessibilityStrings.ShapeCircle;

            case AoeShape.CastTypeCone:
            case AoeShape.CastTypeCone13:
            {
                var full = ConeFullAngleDeg(row);
                // The word "length" used to be appended here, to
                // flag that the number the tooltip calls "Radius" is really the
                // reach. It is gone on the user's reading of the shipped phrasing:
                // *"12y cone makes sense to me and would make sense to other
                // players. it's saying a cone that extends out to 12 yalms."* The
                // shape word next to the number already carries that - a cone with a
                // yardage can only mean how far it reaches - and "cone, length"
                // parsed as two attributes rather than one clarification.
                return full > 0 ? AccessibilityStrings.ShapeConeWithAngle(full) : AccessibilityStrings.ShapeCone;
            }

            case AoeShape.CastTypeLine:
            case AoeShape.CastTypeLine8:
            case AoeShape.CastTypeLine12:
                // XAxisModifier is the half-WIDTH, but game-api.md flags that as an
                // ASSUMPTION still to be verified in game. So the width is not
                // spoken - the length is proven, the width is not.
                // ", length" dropped for the reason on the cone
                // branch above: "20y line" already says how far it reaches.
                return AccessibilityStrings.ShapeLine;

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// The cone's FULL angle in degrees from the telegraph graphic name
    /// (<c>gl_fan090</c> = 90), or 0 when the name carries no fan number.
    ///
    /// Deliberately NOT the same contract as
    /// <c>CombatService.ConeHalfAngleDeg</c>, which returns 45 when it finds
    /// nothing. That default is right for a WARNING - over-warn rather than
    /// under-warn - and wrong here: speaking "90 Grad" for a cone whose graphic
    /// never said so would state a measurement the data does not contain. 0 means
    /// "unknown", and the caller then says "Kegel" with no angle.
    /// </summary>
    private float ConeFullAngleDeg(LuminaAction row)
    {
        if (row.Omen.ValueNullable is not { } omen) return 0f;

        var path = omen.Path.ExtractText();
        var i = path.IndexOf("fan", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return 0f;

        i += 3;
        var j = i;
        while (j < path.Length && char.IsDigit(path[j])) j++;
        if (j > i && int.TryParse(path.AsSpan(i, j - i), out var deg) && deg > 0) return deg;

        _log.Info($"[Aktion] Kegel {row.RowId}: Omen-Pfad '{path}' ohne Fächer-Zahl - Winkel wird nicht genannt.");
        return 0f;
    }
}
