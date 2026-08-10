using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Welche CastType-Werte eine BESTAETIGTE Wirkflaechen-Form haben, und welche
/// Form das ist. Reine Nachschlage-Tabelle, keine Geometrie.
///
/// Die Zuordnung ist nicht geraten: CastType 2/3/4 wurden am 2026-07-26 im Spiel
/// gegen den Namen der Telegraph-Grafik (Omen.Path) festgenagelt - 'general' =
/// Kreis, 'gl_fan090' = Kegel, die Rechteck-Pfade = Linie. Die vier weiteren
/// Nummern kamen 2026-08-09 auf demselben Weg dazu.
///
/// Alles andere ist bewusst NICHT enthalten. Zu Donuts, Kreuzen und den
/// exotischen Nummern gibt es im Umlauf Zuordnungen, die dieses Projekt nie
/// nachgemessen hat - und eine falsch benannte Form ist schlimmer als gar keine:
/// der Spieler weicht dann in die Flaeche hinein aus. Wer eine Nummer ergaenzen
/// will, misst sie vorher im Spiel gegen Omen.Path.
/// </summary>
internal static class AoeShape
{
    /// <summary>Circle centred on the cast's target (or the caster).</summary>
    public const byte CastTypeCircle = 2;
    /// <summary>Cone from the caster along its facing.</summary>
    public const byte CastTypeCone = 3;
    /// <summary>Line / rectangle from the caster along its facing.</summary>
    public const byte CastTypeLine = 4;

    // Four more types, added from the game's OWN telegraph
    // graphics rather than from community lore - see HasProvenShape.
    /// <summary>Circle, same geometry as <see cref="CastTypeCircle"/>.</summary>
    public const byte CastTypeCircle5 = 5;
    /// <summary>Line / rectangle, same geometry as <see cref="CastTypeLine"/>.</summary>
    public const byte CastTypeLine8 = 8;
    /// <summary>Line / rectangle, same geometry as <see cref="CastTypeLine"/>.</summary>
    public const byte CastTypeLine12 = 12;
    /// <summary>Cone, same geometry as <see cref="CastTypeCone"/>.</summary>
    public const byte CastTypeCone13 = 13;

    /// <summary>
    /// Hat dieser CastType eine bestaetigte Form? Nur diese sieben; alles andere
    /// liefert false und bekommt deshalb keine Formansage. Eine fehlende Ansage
    /// ist eine Luecke, eine falsche Form waere eine Falschaussage ueber Geometrie,
    /// in der der Spieler steht.
    /// </summary>
    public static bool HasProvenShape(byte castType)
        => castType is CastTypeCircle or CastTypeCone or CastTypeLine
                    or CastTypeCircle5 or CastTypeLine8 or CastTypeLine12 or CastTypeCone13;
}
