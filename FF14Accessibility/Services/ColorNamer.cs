using System;

namespace FF14Accessibility.Services;

/// <summary>
/// Turns an RGB swatch into a short spoken description.
/// WHY THIS EXISTS: character creation is mostly colour grids - skin, hair, eyes,
/// lips, tattoos, face paint. The game has no name for a single swatch anywhere in
/// its data (verified: <c>CharaMakeType.SubMenuParam</c> is all zeroes for every
/// colour menu, and no sheet row exists per swatch). Without this the best a
/// screen reader can say is "37 of 192", which tells a blind player nothing about
/// what their character actually looks like. User 2026-08-08: *"blind people want
/// to know what their character actually looks like ... you'll probably have to
/// pull the colors from hex values or whatever the game is using to build the
/// shades and build labels for them."*
/// The RGB comes from the game's own palette file (see <see cref="CharaMakePalette"/>).
/// Everything in THIS file is presentation: no claim about game structures is made
/// here, so it is ordinary engineering rather than a game-facts question.
/// DESIGN NOTES, because naive HSL naming is actively misleading:
/// <list type="bullet">
/// <item>A dark orange is BROWN, not "dark orange". Skin and hair live almost
///   entirely in that band, so the brown/beige/olive corrections below are what
///   make the output usable at all.</item>
/// <item>Low-saturation colours must not be given a hue name with confidence -
///   they get a "greyish" qualifier or fall through to the neutral scale.</item>
/// <item>Output is kept to two or three words. This is spoken on every arrow
///   press while browsing a 192-swatch grid; a sentence would be unusable.</item>
/// </list>
/// </summary>
public static class ColorNamer
{
    private static bool De => Loc.IsGerman;

    /// <summary>What the swatch is for. Skin and hair get their own vocabulary
    /// because generic hue words ("dark orange") describe them badly.</summary>
    public enum Kind
    {
        /// <summary>Tattoo, limbal ring, ear clasp, and anything not listed below.</summary>
        Generic,
        Skin,
        Hair,
        Eye,
        Lip,
        FacePaint,
    }

    /// <summary>
    /// Short spoken description of one swatch, e.g. "warm tan", "ash blond",
    /// "vivid teal", "near black".
    /// </summary>
    public static string Describe(byte r, byte g, byte b, Kind kind)
    {
        ToHsl(r, g, b, out var h, out var s, out var l);

        return kind switch
        {
            Kind.Skin => DescribeSkin(h, s, l),
            Kind.Hair => DescribeHair(h, s, l),
            _         => DescribeGeneric(h, s, l),
        };
    }

    // ── Neutral (achromatic) scale ────────────────────────────────────────────
    // Used whenever saturation is too low for a hue name to mean anything.

    private static string Neutral(double l) => l switch
    {
        < 0.06 => De ? "schwarz"          : "black",
        < 0.16 => De ? "fast schwarz"     : "near black",
        < 0.28 => De ? "anthrazit"        : "charcoal",
        < 0.42 => De ? "dunkelgrau"       : "dark grey",
        < 0.58 => De ? "mittelgrau"       : "medium grey",
        < 0.72 => De ? "grau"             : "grey",
        < 0.85 => De ? "hellgrau"         : "light grey",
        < 0.95 => De ? "sehr helles Grau" : "off white",
        _      => De ? "weiß"             : "white",
    };

    // ── Generic hue naming ────────────────────────────────────────────────────

    /// <summary>
    /// Hue family for a saturated colour. Bands are deliberately uneven: the
    /// warm end (0-60°) carries most of the skin/hair/eye range and needs finer
    /// resolution than the greens, where the palettes place few swatches.
    /// </summary>
    private static string HueFamily(double h) => h switch
    {
        < 8   => De ? "Rot"        : "red",
        < 16  => De ? "Ziegelrot"  : "brick red",
        < 24  => De ? "Orangerot"  : "orange red",
        < 34  => De ? "Orange"     : "orange",
        < 43  => De ? "Bernstein"  : "amber",
        < 52  => De ? "Gold"       : "gold",
        < 63  => De ? "Gelb"       : "yellow",
        < 78  => De ? "Gelbgrün"   : "yellow green",
        < 100 => De ? "Limettgrün" : "lime green",
        < 140 => De ? "Grün"       : "green",
        < 160 => De ? "Smaragd"    : "emerald green",
        < 176 => De ? "Blaugrün"   : "sea green",
        < 192 => De ? "Türkis"     : "teal",
        < 205 => De ? "Cyan"       : "cyan",
        < 220 => De ? "Himmelblau" : "sky blue",
        < 240 => De ? "Blau"       : "blue",
        < 258 => De ? "Indigo"     : "indigo",
        < 275 => De ? "Violett"    : "violet",
        < 292 => De ? "Lila"       : "purple",
        < 315 => De ? "Magenta"    : "magenta",
        < 335 => De ? "Pink"       : "pink",
        < 348 => De ? "Himbeerrot" : "raspberry",
        _     => De ? "Rot"        : "red",
    };

    /// <summary>
    /// The correction that makes the whole thing work: in the 8-60° band a colour
    /// reads as brown/beige/olive rather than as "dark orange" or "pale yellow".
    /// Returns null when no correction applies.
    /// </summary>
    private static string? WarmFamily(double h, double s, double l)
    {
        if (h < 8 || h >= 60) return null;

        // Dark and at least somewhat coloured -> the brown family.
        if (l < 0.48 && s >= 0.10)
        {
            if (h < 20) return De ? "Rotbraun"     : "reddish brown";
            if (h < 32) return De ? "Braun"        : "brown";
            if (h < 45) return De ? "Warmbraun"    : "warm brown";
            return           De ? "Olivbraun"      : "olive brown";
        }

        // Light and washed out -> the cream family.
        if (l >= 0.72 && s < 0.55)
        {
            if (h < 18) return De ? "Rosébeige"    : "rosy beige";
            if (h < 30) return De ? "Pfirsich"     : "peach";
            if (h < 45) return De ? "Creme"        : "cream";
            return           De ? "Elfenbein"      : "ivory";
        }

        // Mid lightness, low saturation -> beige/khaki rather than a hue word.
        if (s < 0.28)
        {
            if (h < 30) return De ? "Beige"        : "beige";
            if (h < 48) return De ? "Khaki"        : "khaki";
            return           De ? "Oliv"           : "olive";
        }

        return null;
    }

    private static string DescribeGeneric(double h, double s, double l)
    {
        if (s < 0.07 || l < 0.04 || l > 0.97) return Neutral(l);

        var family = WarmFamily(h, s, l) ?? HueFamily(h);

        // Very desaturated blues/greens read as slate/sage, not as "blue".
        if (s < 0.14)
        {
            if (h >= 176 && h < 258) family = De ? "Blaugrau" : "blue grey";
            else if (h >= 60 && h < 176) family = De ? "Graugrün" : "sage";
            else if (h >= 258) family = De ? "Mauve" : "mauve";
        }

        return Join(Lightness(l), Intensity(s, l), family);
    }

    /// <summary>Lightness qualifier, or null in the middle of the range where it
    /// carries no information.</summary>
    private static string? Lightness(double l) => l switch
    {
        < 0.14 => De ? "sehr dunkles" : "very dark",
        < 0.30 => De ? "dunkles"      : "dark",
        < 0.42 => De ? "gedecktes"    : "deep",
        < 0.62 => null,
        < 0.75 => De ? "helles"       : "light",
        < 0.88 => De ? "blasses"      : "pale",
        _      => De ? "sehr blasses" : "very pale",
    };

    /// <summary>Saturation qualifier. Suppressed at the extremes of lightness,
    /// where "vivid near-black" would be nonsense.</summary>
    private static string? Intensity(double s, double l)
    {
        if (l < 0.15 || l > 0.90) return null;
        if (s < 0.16) return De ? "gräuliches" : "greyish";
        if (s < 0.34) return De ? "gedämpftes" : "muted";
        if (s > 0.78) return De ? "kräftiges"  : "vivid";
        return null;
    }

    // ── Skin ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Skin gets its own vocabulary. The warm band (roughly 10-45°) is where every
    /// Hyur/Elezen/Lalafell/Miqo'te/Viera tone sits, and calling those "muted
    /// orange" would be useless. The unusual tones are real and must survive:
    /// measured from the game's own palettes, Sea Wolf Roegadyn skin is green
    /// (84 of 192 swatches), Xaela and Hrothgar reach into blue and violet, and
    /// Duskwight/Keeper are near-neutral pale.
    /// </summary>
    private static string DescribeSkin(double h, double s, double l)
    {
        // Green-skinned (Sea Wolf) and blue/violet (Xaela, Hrothgar, Duskwight).
        if (s >= 0.06 && h >= 60 && h < 200)
        {
            var g = l switch
            {
                < 0.22 => De ? "sehr dunkles Moosgrün" : "very dark moss green",
                < 0.40 => De ? "dunkles Seegrün"       : "dark sea green",
                < 0.62 => De ? "Seegrün"               : "sea green",
                < 0.80 => De ? "helles Seegrün"        : "pale sea green",
                _      => De ? "sehr blasses Grün"     : "very pale green",
            };
            return g;
        }

        if (s >= 0.06 && h >= 200 && h < 320)
        {
            return l switch
            {
                < 0.22 => De ? "sehr dunkles Schiefergrau" : "very dark slate",
                < 0.40 => De ? "dunkles Blaugrau"          : "dark blue grey",
                < 0.62 => De ? "Blaugrau"                  : "blue grey",
                < 0.80 => De ? "helles Blaugrau"           : "pale blue grey",
                _      => De ? "eisblasses Weiß"           : "ice pale white",
            };
        }

        // Near-neutral: ashen rather than grey, which reads better for a face.
        if (s < 0.06)
        {
            return l switch
            {
                < 0.20 => De ? "fast schwarz"    : "near black",
                < 0.38 => De ? "dunkles Aschgrau": "dark ashen",
                < 0.60 => De ? "Aschgrau"        : "ashen grey",
                < 0.80 => De ? "helles Aschgrau" : "pale ashen",
                _      => De ? "porzellanweiß"   : "porcelain white",
            };
        }

        // The warm skin ramp. Saturation separates rosy/olive from plain.
        var warmth = h < 18 ? (De ? "rosiges " : "rosy ")
                   : h >= 40 ? (De ? "oliv " : "olive ")
                   : string.Empty;

        var baseTone = l switch
        {
            < 0.16 => De ? "fast schwarzes Braun" : "near black brown",
            < 0.28 => De ? "sehr dunkles Braun"   : "very dark brown",
            < 0.38 => De ? "dunkles Braun"        : "dark brown",
            < 0.48 => De ? "warmes Braun"         : "warm brown",
            < 0.57 => De ? "Bronze"               : "bronze",
            < 0.66 => De ? "gebräunt"             : "tan",
            < 0.74 => De ? "warmes Beige"         : "warm beige",
            < 0.82 => De ? "helles Beige"         : "light beige",
            < 0.90 => De ? "hell"                 : "fair",
            _      => De ? "sehr hell"            : "very fair",
        };

        return (warmth + baseTone).Trim();
    }

    // ── Hair ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hair vocabulary. The palette runs a natural ramp (white through blond,
    /// brown and black) and then a set of vivid dyes, so both have to be covered.
    /// </summary>
    private static string DescribeHair(double h, double s, double l)
    {
        if (s < 0.07)
        {
            return l switch
            {
                < 0.08 => De ? "schwarz"       : "black",
                < 0.22 => De ? "fast schwarz"  : "near black",
                < 0.38 => De ? "dunkelgrau"    : "dark grey",
                < 0.58 => De ? "grau"          : "grey",
                < 0.74 => De ? "silbergrau"    : "silver grey",
                < 0.90 => De ? "silber"        : "silver",
                _      => De ? "weiß"          : "white",
            };
        }

        // Natural warm range: blond / brown / red, chosen by lightness.
        if (h >= 8 && h < 60)
        {
            if (l >= 0.72)
                return s < 0.30 ? (De ? "platinblond" : "platinum blond")
                     : h < 30   ? (De ? "erdbeerblond" : "strawberry blond")
                                : (De ? "goldblond"    : "golden blond");
            if (l >= 0.56)
                return s < 0.28 ? (De ? "aschblond"   : "ash blond")
                                : (De ? "honigblond"  : "honey blond");
            if (l >= 0.42)
                return h < 24   ? (De ? "kupferrot"   : "copper red")
                     : s < 0.30 ? (De ? "dunkelblond" : "dark blond")
                                : (De ? "hellbraun"   : "light brown");
            if (l >= 0.26)
                return h < 22   ? (De ? "kastanienbraun" : "auburn")
                                : (De ? "schokobraun"    : "chocolate brown");
            return h < 22 ? (De ? "dunkles Rotbraun" : "dark auburn")
                          : (De ? "dunkelbraun"      : "dark brown");
        }

        // Reds outside the blond/brown ramp.
        if (h >= 335 || h < 8)
        {
            if (l < 0.30) return De ? "dunkles Weinrot" : "dark wine red";
            if (l < 0.55) return De ? "rot"             : "red";
            return De ? "helles Rosé" : "pale rose";
        }

        // Dyed colours: the generic namer is right for these.
        return DescribeGeneric(h, s, l);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Join(string? a, string? b, string family)
    {
        // Two qualifiers plus a family is the ceiling; beyond that it stops being
        // quicker to hear than to ignore.
        if (a != null && b != null) return $"{a} {b} {family}";
        if (a != null) return $"{a} {family}";
        if (b != null) return $"{b} {family}";
        return family;
    }

    /// <summary>RGB to HSL. Hue in degrees 0-360, saturation and lightness 0-1.</summary>
    private static void ToHsl(byte r8, byte g8, byte b8, out double h, out double s, out double l)
    {
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        var d = max - min;
        if (d < 1e-9)
        {
            h = 0;
            s = 0;
            return;
        }

        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        if (max == r)      h = ((g - b) / d + (g < b ? 6.0 : 0.0)) * 60.0;
        else if (max == g) h = ((b - r) / d + 2.0) * 60.0;
        else               h = ((r - g) / d + 4.0) * 60.0;
    }
}
