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

    /// <summary>
    /// Dreisprachige Wortwahl: DE / EN / RU. Russisch wird am Bildschirmrand
    /// vorgelesen, waehrend der Spieler mit den Pfeiltasten ueber 192 Felder
    /// faehrt - ein Wort, kein Satz, wie im Deutschen und Englischen.
    /// </summary>
    private static string W(string de, string en, string ru) =>
        Loc.IsRussian ? ru : Loc.IsGerman ? de : en;

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
        < 0.06 => W("schwarz", "black", "чёрный"),
        < 0.16 => W("fast schwarz", "near black", "почти чёрный"),
        < 0.28 => W("anthrazit", "charcoal", "антрацитовый"),
        < 0.42 => W("dunkelgrau", "dark grey", "тёмно-серый"),
        < 0.58 => W("mittelgrau", "medium grey", "средне-серый"),
        < 0.72 => W("grau", "grey", "серый"),
        < 0.85 => W("hellgrau", "light grey", "светло-серый"),
        < 0.95 => W("sehr helles Grau", "off white", "почти белый"),
        _      => W("weiß", "white", "белый"),
    };

    // ── Generic hue naming ────────────────────────────────────────────────────

    /// <summary>
    /// Hue family for a saturated colour. Bands are deliberately uneven: the
    /// warm end (0-60°) carries most of the skin/hair/eye range and needs finer
    /// resolution than the greens, where the palettes place few swatches.
    /// </summary>
    private static string HueFamily(double h) => h switch
    {
        < 8   => W("Rot", "red", "красный"),
        < 16  => W("Ziegelrot", "brick red", "кирпично-красный"),
        < 24  => W("Orangerot", "orange red", "оранжево-красный"),
        < 34  => W("Orange", "orange", "оранжевый"),
        < 43  => W("Bernstein", "amber", "янтарный"),
        < 52  => W("Gold", "gold", "золотой"),
        < 63  => W("Gelb", "yellow", "жёлтый"),
        < 78  => W("Gelbgrün", "yellow green", "жёлто-зелёный"),
        < 100 => W("Limettgrün", "lime green", "лаймовый"),
        < 140 => W("Grün", "green", "зелёный"),
        < 160 => W("Smaragd", "emerald green", "изумрудный"),
        < 176 => W("Blaugrün", "sea green", "морской волны"),
        < 192 => W("Türkis", "teal", "бирюзовый"),
        < 205 => W("Cyan", "cyan", "циан"),
        < 220 => W("Himmelblau", "sky blue", "небесно-голубой"),
        < 240 => W("Blau", "blue", "синий"),
        < 258 => W("Indigo", "indigo", "индиго"),
        < 275 => W("Violett", "violet", "фиолетовый"),
        < 292 => W("Lila", "purple", "сиреневый"),
        < 315 => W("Magenta", "magenta", "маджента"),
        < 335 => W("Pink", "pink", "розовый"),
        < 348 => W("Himbeerrot", "raspberry", "малиновый"),
        _     => W("Rot", "red", "красный"),
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
            if (h < 20) return W("Rotbraun", "reddish brown", "красно-коричневый");
            if (h < 32) return W("Braun", "brown", "коричневый");
            if (h < 45) return W("Warmbraun", "warm brown", "тёпло-коричневый");
            return           W("Olivbraun", "olive brown", "оливково-коричневый");
        }

        // Light and washed out -> the cream family.
        if (l >= 0.72 && s < 0.55)
        {
            if (h < 18) return W("Rosébeige", "rosy beige", "розово-бежевый");
            if (h < 30) return W("Pfirsich", "peach", "персиковый");
            if (h < 45) return W("Creme", "cream", "кремовый");
            return           W("Elfenbein", "ivory", "цвет слоновой кости");
        }

        // Mid lightness, low saturation -> beige/khaki rather than a hue word.
        if (s < 0.28)
        {
            if (h < 30) return W("Beige", "beige", "бежевый");
            if (h < 48) return W("Khaki", "khaki", "хаки");
            return           W("Oliv", "olive", "оливковый");
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
            if (h >= 176 && h < 258) family = W("Blaugrau", "blue grey", "сине-серый");
            else if (h >= 60 && h < 176) family = W("Graugrün", "sage", "серо-зелёный");
            else if (h >= 258) family = W("Mauve", "mauve", "лиловый");
        }

        return Join(Lightness(l), Intensity(s, l), family);
    }

    /// <summary>Lightness qualifier, or null in the middle of the range where it
    /// carries no information.</summary>
    private static string? Lightness(double l) => l switch
    {
        < 0.14 => W("sehr dunkles", "very dark", "очень тёмный"),
        < 0.30 => W("dunkles", "dark", "тёмный"),
        < 0.42 => W("gedecktes", "deep", "глубокий"),
        < 0.62 => null,
        < 0.75 => W("helles", "light", "светлый"),
        < 0.88 => W("blasses", "pale", "бледный"),
        _      => W("sehr blasses", "very pale", "очень бледный"),
    };

    /// <summary>Saturation qualifier. Suppressed at the extremes of lightness,
    /// where "vivid near-black" would be nonsense.</summary>
    private static string? Intensity(double s, double l)
    {
        if (l < 0.15 || l > 0.90) return null;
        if (s < 0.16) return W("gräuliches", "greyish", "сероватый");
        if (s < 0.34) return W("gedämpftes", "muted", "приглушённый");
        if (s > 0.78) return W("kräftiges", "vivid", "насыщенный");
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
                < 0.22 => W("sehr dunkles Moosgrün", "very dark moss green", "очень тёмный моховой зелёный"),
                < 0.40 => W("dunkles Seegrün", "dark sea green", "тёмный морской зелёный"),
                < 0.62 => W("Seegrün", "sea green", "морской зелёный"),
                < 0.80 => W("helles Seegrün", "pale sea green", "светлый морской зелёный"),
                _      => W("sehr blasses Grün", "very pale green", "очень бледный зелёный"),
            };
            return g;
        }

        if (s >= 0.06 && h >= 200 && h < 320)
        {
            return l switch
            {
                < 0.22 => W("sehr dunkles Schiefergrau", "very dark slate", "очень тёмный шиферный"),
                < 0.40 => W("dunkles Blaugrau", "dark blue grey", "тёмный сине-серый"),
                < 0.62 => W("Blaugrau", "blue grey", "сине-серый"),
                < 0.80 => W("helles Blaugrau", "pale blue grey", "светлый сине-серый"),
                _      => W("eisblasses Weiß", "ice pale white", "ледяной белый"),
            };
        }

        // Near-neutral: ashen rather than grey, which reads better for a face.
        if (s < 0.06)
        {
            return l switch
            {
                < 0.20 => W("fast schwarz", "near black", "почти чёрный"),
                < 0.38 => W("dunkles Aschgrau", "dark ashen", "тёмный пепельный"),
                < 0.60 => W("Aschgrau", "ashen grey", "пепельно-серый"),
                < 0.80 => W("helles Aschgrau", "pale ashen", "светлый пепельный"),
                _      => W("porzellanweiß", "porcelain white", "фарфоровый"),
            };
        }

        // The warm skin ramp. Saturation separates rosy/olive from plain.
        var warmth = h < 18 ? (W("rosiges ", "rosy ", "розоватый "))
                   : h >= 40 ? (W("oliv ", "olive ", "оливковый "))
                   : string.Empty;

        var baseTone = l switch
        {
            < 0.16 => W("fast schwarzes Braun", "near black brown", "почти чёрный коричневый"),
            < 0.28 => W("sehr dunkles Braun", "very dark brown", "очень тёмный коричневый"),
            < 0.38 => W("dunkles Braun", "dark brown", "тёмный коричневый"),
            < 0.48 => W("warmes Braun", "warm brown", "тёплый коричневый"),
            < 0.57 => W("Bronze", "bronze", "бронзовый"),
            < 0.66 => W("gebräunt", "tan", "загорелый"),
            < 0.74 => W("warmes Beige", "warm beige", "тёплый бежевый"),
            < 0.82 => W("helles Beige", "light beige", "светлый бежевый"),
            < 0.90 => W("hell", "fair", "светлый"),
            _      => W("sehr hell", "very fair", "очень светлый"),
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
                < 0.08 => W("schwarz", "black", "чёрный"),
                < 0.22 => W("fast schwarz", "near black", "почти чёрный"),
                < 0.38 => W("dunkelgrau", "dark grey", "тёмно-серый"),
                < 0.58 => W("grau", "grey", "серый"),
                < 0.74 => W("silbergrau", "silver grey", "серебристо-серый"),
                < 0.90 => W("silber", "silver", "серебряный"),
                _      => W("weiß", "white", "белый"),
            };
        }

        // Natural warm range: blond / brown / red, chosen by lightness.
        if (h >= 8 && h < 60)
        {
            if (l >= 0.72)
                return s < 0.30 ? (W("platinblond", "platinum blond", "платиновая блондинка"))
                     : h < 30   ? (W("erdbeerblond", "strawberry blond", "земляничная блондинка"))
                                : (W("goldblond", "golden blond", "золотистая блондинка"));
            if (l >= 0.56)
                return s < 0.28 ? (W("aschblond", "ash blond", "пепельная блондинка"))
                                : (W("honigblond", "honey blond", "медовая блондинка"));
            if (l >= 0.42)
                return h < 24   ? (W("kupferrot", "copper red", "медный"))
                     : s < 0.30 ? (W("dunkelblond", "dark blond", "тёмная блондинка"))
                                : (W("hellbraun", "light brown", "светло-каштановый"));
            if (l >= 0.26)
                return h < 22   ? (W("kastanienbraun", "auburn", "каштановый"))
                                : (W("schokobraun", "chocolate brown", "шоколадный"));
            return h < 22 ? (W("dunkles Rotbraun", "dark auburn", "тёмный красно-коричневый"))
                          : (W("dunkelbraun", "dark brown", "тёмно-коричневый"));
        }

        // Reds outside the blond/brown ramp.
        if (h >= 335 || h < 8)
        {
            if (l < 0.30) return W("dunkles Weinrot", "dark wine red", "тёмный винный");
            if (l < 0.55) return W("rot", "red", "красный");
            return W("helles Rosé", "pale rose", "светло-розовый");
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
