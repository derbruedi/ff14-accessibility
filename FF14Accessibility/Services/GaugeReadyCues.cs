using System;
using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// Distinct ready-cue identities for job-gauge rising edges. Each resource the
/// mod announces gets its own two-note blip so a blind player can tell Zorn
/// from Lilie by ear before the warning voice finishes the sentence.
///
/// Frequencies are assigned from a fixed chromatic ladder by enum ordinal —
/// levels stay out of this file; only the mapping "this resource → these Hz"
/// lives here. Preview labels come from <see cref="AccessibilityStrings"/>.
/// </summary>
public enum GaugeReadyCueId : byte
{
    // Tanks
    Beast = 0,
    Oath,
    Blood,
    DarkArts,
    Ammo,

    // Melee
    Chakra,
    BeastChakra,
    ThreeBeastChakra,
    NadiLunar,
    NadiSolar,
    NadiBoth,
    Eyes,
    Firstminds,
    Lotd,
    Ninki,
    Kazematoi,
    Getsu,
    Ka,
    Setsu,
    ThreeSen,
    Kenki,
    Meditation,
    Tsubame,
    Soul,
    Shroud,
    Enshroud,
    VoidShroud,
    RattlingCoil,
    SerpentOffering,
    SerpentFollowUp,

    // Ranged / caster
    SoulVoice,
    Repertoire,
    CodaMage,
    CodaArmy,
    CodaWanderer,
    Heat,
    Battery,
    Overheat,
    Robot,
    Feathers,
    Esprit,
    Polyglot,
    Paradox,
    AstralSoul,
    UmbralHearts,
    WhiteMana,
    BlackMana,
    ManaStacks,
    Palette,
    Paint,
    CreatureMotif,
    WeaponMotif,
    LandscapeMotif,
    MooglePortrait,
    MadeenPortrait,

    // Healers + SMN
    Lily,
    BloodLily,
    Aetherflow,
    Fairy,
    Addersgall,
    Addersting,
    Eukrasia,
    Card,
    CrownCard,
    Ruby,
    Topaz,
    Emerald,
    AllGems,
}

/// <summary>Lookup table: cue id → localized name + note pair.</summary>
public static class GaugeReadyCues
{
    // Chromatic ladder roughly C5..D#6 — above the walk-beacon range, clear of
    // the steady waypoint (1175) and aligned (880) cues. Skill-ready (784→1047)
    // stays reserved for CooldownService.
    private static readonly float[] Ladder =
    {
        523.25f, 554.37f, 587.33f, 622.25f, 659.25f, 698.46f, 739.99f,
        783.99f, 830.61f, 880.00f, 932.33f, 987.77f, 1046.50f, 1108.73f,
        1174.66f, 1244.51f, 1318.51f, 1396.91f,
    };

    /// <summary>
    /// Preview menu grouping: jobs in ClassJob order, each with the cues that
    /// job's Collect path can fire. Aetherflow appears under Scholar and
    /// Summoner (same tone — both spend it).
    /// </summary>
    public static IReadOnlyList<(string JobName, GaugeReadyCueId[] Cues)> ByJob()
    {
        // Names match the German/English client ClassJob sheet abbreviations
        // players already know from the job list.
        return new (string, GaugeReadyCueId[])[]
        {
            (Job("Paladin", "Paladin", "Паладин"), new[]
            {
                GaugeReadyCueId.Oath,
            }),
            (Job("Mönch", "Monk", "Монах"), new[]
            {
                GaugeReadyCueId.Chakra,
                GaugeReadyCueId.BeastChakra,
                GaugeReadyCueId.ThreeBeastChakra,
                GaugeReadyCueId.NadiLunar,
                GaugeReadyCueId.NadiSolar,
                GaugeReadyCueId.NadiBoth,
            }),
            (Job("Krieger", "Warrior", "Воин"), new[]
            {
                GaugeReadyCueId.Beast,
            }),
            (Job("Dragoon", "Dragoon", "Драгун"), new[]
            {
                GaugeReadyCueId.Eyes,
                GaugeReadyCueId.Firstminds,
                GaugeReadyCueId.Lotd,
            }),
            (Job("Barde", "Bard", "Бард"), new[]
            {
                GaugeReadyCueId.SoulVoice,
                GaugeReadyCueId.Repertoire,
                GaugeReadyCueId.CodaMage,
                GaugeReadyCueId.CodaArmy,
                GaugeReadyCueId.CodaWanderer,
            }),
            (Job("Weißmagier", "White Mage", "Белый маг"), new[]
            {
                GaugeReadyCueId.Lily,
                GaugeReadyCueId.BloodLily,
            }),
            (Job("Schwarzmagier", "Black Mage", "Чёрный маг"), new[]
            {
                GaugeReadyCueId.Polyglot,
                GaugeReadyCueId.Paradox,
                GaugeReadyCueId.AstralSoul,
                GaugeReadyCueId.UmbralHearts,
            }),
            // Hermetiker und Beschwörer teilen denselben SMNGauge-Pfad.
            (Job("Hermetiker", "Arcanist", "Арканист"), new[]
            {
                GaugeReadyCueId.Ruby,
                GaugeReadyCueId.Topaz,
                GaugeReadyCueId.Emerald,
                GaugeReadyCueId.AllGems,
                GaugeReadyCueId.Aetherflow,
            }),
            (Job("Beschwörer", "Summoner", "Призыватель"), new[]
            {
                GaugeReadyCueId.Ruby,
                GaugeReadyCueId.Topaz,
                GaugeReadyCueId.Emerald,
                GaugeReadyCueId.AllGems,
                GaugeReadyCueId.Aetherflow,
            }),
            (Job("Gelehrter", "Scholar", "Учёный"), new[]
            {
                GaugeReadyCueId.Aetherflow,
                GaugeReadyCueId.Fairy,
            }),
            // Schurke (29) hat keine eigene Anzeige — erst Ninja.
            (Job("Ninja", "Ninja", "Ниндзя"), new[]
            {
                GaugeReadyCueId.Ninki,
                GaugeReadyCueId.Kazematoi,
            }),
            (Job("Maschinist", "Machinist", "Механик"), new[]
            {
                GaugeReadyCueId.Heat,
                GaugeReadyCueId.Battery,
                GaugeReadyCueId.Overheat,
                GaugeReadyCueId.Robot,
            }),
            (Job("Dunkelritter", "Dark Knight", "Тёмный рыцарь"), new[]
            {
                GaugeReadyCueId.Blood,
                GaugeReadyCueId.DarkArts,
            }),
            (Job("Astrologe", "Astrologian", "Астролог"), new[]
            {
                GaugeReadyCueId.Card,
                GaugeReadyCueId.CrownCard,
            }),
            (Job("Samurai", "Samurai", "Самурай"), new[]
            {
                GaugeReadyCueId.Getsu,
                GaugeReadyCueId.Ka,
                GaugeReadyCueId.Setsu,
                GaugeReadyCueId.ThreeSen,
                GaugeReadyCueId.Kenki,
                GaugeReadyCueId.Meditation,
                GaugeReadyCueId.Tsubame,
            }),
            (Job("Rotmagier", "Red Mage", "Красный маг"), new[]
            {
                GaugeReadyCueId.WhiteMana,
                GaugeReadyCueId.BlackMana,
                GaugeReadyCueId.ManaStacks,
            }),
            (Job("Revolverheld", "Gunbreaker", "Ганбрейкер"), new[]
            {
                GaugeReadyCueId.Ammo,
            }),
            (Job("Tänzer", "Dancer", "Танцор"), new[]
            {
                GaugeReadyCueId.Feathers,
                GaugeReadyCueId.Esprit,
            }),
            (Job("Schnitter", "Reaper", "Жнец"), new[]
            {
                GaugeReadyCueId.Soul,
                GaugeReadyCueId.Shroud,
                GaugeReadyCueId.Enshroud,
                GaugeReadyCueId.VoidShroud,
            }),
            (Job("Weiser", "Sage", "Мудрец"), new[]
            {
                GaugeReadyCueId.Addersgall,
                GaugeReadyCueId.Addersting,
                GaugeReadyCueId.Eukrasia,
            }),
            (Job("Viper", "Viper", "Вайпер"), new[]
            {
                GaugeReadyCueId.RattlingCoil,
                GaugeReadyCueId.SerpentOffering,
                GaugeReadyCueId.SerpentFollowUp,
            }),
            (Job("Pictomancer", "Pictomancer", "Пиктомант"), new[]
            {
                GaugeReadyCueId.Palette,
                GaugeReadyCueId.Paint,
                GaugeReadyCueId.CreatureMotif,
                GaugeReadyCueId.WeaponMotif,
                GaugeReadyCueId.LandscapeMotif,
                GaugeReadyCueId.MooglePortrait,
                GaugeReadyCueId.MadeenPortrait,
            }),
        };
    }

    // Russisch ueber denselben Helfer wie alle anderen Texte: die Namen
    // stehen dort ausgeschrieben, weil die Tabelle oben ohnehin je Job
    // ein Paar aus deutscher und englischer Schreibweise traegt.
    private static string Job(string de, string en, string ru) =>
        AccessibilityStrings.L(de, en, ru);

    /// <summary>Localized short name for menus and logs (no "ready"/"full").</summary>
    public static string Name(GaugeReadyCueId id) => id switch
    {
        GaugeReadyCueId.Beast => AccessibilityStrings.GaugeNameBeast,
        GaugeReadyCueId.Oath => AccessibilityStrings.GaugeNameOath,
        GaugeReadyCueId.Blood => AccessibilityStrings.GaugeNameBlood,
        GaugeReadyCueId.DarkArts => AccessibilityStrings.GaugeNameDarkArts,
        GaugeReadyCueId.Ammo => AccessibilityStrings.GaugeNameAmmo,
        GaugeReadyCueId.Chakra => AccessibilityStrings.GaugeNameChakra,
        GaugeReadyCueId.BeastChakra => AccessibilityStrings.GaugeNameBeastChakra,
        GaugeReadyCueId.ThreeBeastChakra => AccessibilityStrings.GaugeNameThreeBeastChakra,
        GaugeReadyCueId.NadiLunar => AccessibilityStrings.GaugeNameNadiLunar,
        GaugeReadyCueId.NadiSolar => AccessibilityStrings.GaugeNameNadiSolar,
        GaugeReadyCueId.NadiBoth => AccessibilityStrings.GaugeNameNadiBoth,
        GaugeReadyCueId.Eyes => AccessibilityStrings.GaugeNameEyes,
        GaugeReadyCueId.Firstminds => AccessibilityStrings.GaugeNameFirstminds,
        GaugeReadyCueId.Lotd => AccessibilityStrings.GaugeNameLotd,
        GaugeReadyCueId.Ninki => AccessibilityStrings.GaugeNameNinki,
        GaugeReadyCueId.Kazematoi => AccessibilityStrings.GaugeNameKazematoi,
        GaugeReadyCueId.Getsu => AccessibilityStrings.L("Getsu", "Getsu", "Гэцу"),
        GaugeReadyCueId.Ka => AccessibilityStrings.L("Ka", "Ka", "Ка"),
        GaugeReadyCueId.Setsu => AccessibilityStrings.L("Setsu", "Setsu", "Сэцу"),
        GaugeReadyCueId.ThreeSen => AccessibilityStrings.L("drei Sen", "three Sen", "три сэн"),
        GaugeReadyCueId.Kenki => AccessibilityStrings.L("Kenki", "Kenki", "Кэнки"),
        GaugeReadyCueId.Meditation => AccessibilityStrings.L("Meditation", "Meditation", "Медитация"),
        GaugeReadyCueId.Tsubame => AccessibilityStrings.L("Tsubame", "Tsubame", "Цубамэ"),
        GaugeReadyCueId.Soul => AccessibilityStrings.GaugeNameSoul,
        GaugeReadyCueId.Shroud => AccessibilityStrings.GaugeNameShroud,
        GaugeReadyCueId.Enshroud => AccessibilityStrings.GaugeNameEnshroud,
        GaugeReadyCueId.VoidShroud => AccessibilityStrings.GaugeNameVoidShroud,
        GaugeReadyCueId.RattlingCoil => AccessibilityStrings.GaugeNameRattlingCoil,
        GaugeReadyCueId.SerpentOffering => AccessibilityStrings.GaugeNameSerpentOffering,
        GaugeReadyCueId.SerpentFollowUp => AccessibilityStrings.GaugeNameSerpentFollowUp,
        GaugeReadyCueId.SoulVoice => AccessibilityStrings.GaugeNameSoulVoice,
        GaugeReadyCueId.Repertoire => AccessibilityStrings.GaugeNameRepertoire,
        GaugeReadyCueId.CodaMage => AccessibilityStrings.GaugeNameCodaMage,
        GaugeReadyCueId.CodaArmy => AccessibilityStrings.GaugeNameCodaArmy,
        GaugeReadyCueId.CodaWanderer => AccessibilityStrings.GaugeNameCodaWanderer,
        GaugeReadyCueId.Heat => AccessibilityStrings.GaugeNameHeat,
        GaugeReadyCueId.Battery => AccessibilityStrings.GaugeNameBattery,
        GaugeReadyCueId.Overheat => AccessibilityStrings.GaugeNameOverheat,
        GaugeReadyCueId.Robot => AccessibilityStrings.GaugeNameRobot,
        GaugeReadyCueId.Feathers => AccessibilityStrings.GaugeNameFeathers,
        GaugeReadyCueId.Esprit => AccessibilityStrings.GaugeNameEsprit,
        GaugeReadyCueId.Polyglot => AccessibilityStrings.GaugeNamePolyglot,
        GaugeReadyCueId.Paradox => AccessibilityStrings.GaugeNameParadox,
        GaugeReadyCueId.AstralSoul => AccessibilityStrings.GaugeNameAstralSoul,
        GaugeReadyCueId.UmbralHearts => AccessibilityStrings.GaugeNameUmbralHearts,
        GaugeReadyCueId.WhiteMana => AccessibilityStrings.GaugeNameWhiteMana,
        GaugeReadyCueId.BlackMana => AccessibilityStrings.GaugeNameBlackMana,
        GaugeReadyCueId.ManaStacks => AccessibilityStrings.GaugeNameManaStacks,
        GaugeReadyCueId.Palette => AccessibilityStrings.GaugeNamePalette,
        GaugeReadyCueId.Paint => AccessibilityStrings.GaugeNamePaint,
        GaugeReadyCueId.CreatureMotif => AccessibilityStrings.GaugeNameCreatureMotif,
        GaugeReadyCueId.WeaponMotif => AccessibilityStrings.GaugeNameWeaponMotif,
        GaugeReadyCueId.LandscapeMotif => AccessibilityStrings.GaugeNameLandscapeMotif,
        GaugeReadyCueId.MooglePortrait => AccessibilityStrings.GaugeNameMooglePortrait,
        GaugeReadyCueId.MadeenPortrait => AccessibilityStrings.GaugeNameMadeenPortrait,
        GaugeReadyCueId.Lily => AccessibilityStrings.GaugeNameLily,
        GaugeReadyCueId.BloodLily => AccessibilityStrings.GaugeNameBloodLily,
        GaugeReadyCueId.Aetherflow => AccessibilityStrings.GaugeNameAetherflow,
        GaugeReadyCueId.Fairy => AccessibilityStrings.GaugeNameFairy,
        GaugeReadyCueId.Addersgall => AccessibilityStrings.GaugeNameAddersgall,
        GaugeReadyCueId.Addersting => AccessibilityStrings.GaugeNameAddersting,
        GaugeReadyCueId.Eukrasia => AccessibilityStrings.GaugeNameEukrasia,
        GaugeReadyCueId.Card => AccessibilityStrings.GaugeNameCard,
        GaugeReadyCueId.CrownCard => AccessibilityStrings.GaugeNameCrownCard,
        GaugeReadyCueId.Ruby => AccessibilityStrings.L("Rubin", "Ruby", "Рубин"),
        GaugeReadyCueId.Topaz => AccessibilityStrings.L("Topas", "Topaz", "Топаз"),
        GaugeReadyCueId.Emerald => AccessibilityStrings.L("Smaragd", "Emerald", "Изумруд"),
        GaugeReadyCueId.AllGems => AccessibilityStrings.L("alle drei bereit", "all three ready", "все три готовы"),
        _ => id.ToString(),
    };

    /// <summary>Rising two-note pair unique per cue id.</summary>
    public static (float Note1, float Note2) Notes(GaugeReadyCueId id)
    {
        var i = (int)id;
        var n = Ladder.Length;
        var a = Ladder[i % n];
        // Second note: jump 3–5 steps so neighbouring resources do not share
        // the same interval; wrap keeps everything on the ladder.
        var step = 3 + (i / n) % 3;
        var b = Ladder[(i + step) % n];
        if (b <= a) b = Ladder[Math.Min((i % n) + step, n - 1)];
        if (b <= a) b = a * 1.25f;
        return (a, b);
    }
}
