using System.Collections.Generic;

namespace FF14Accessibility.Services;

/// <summary>
/// Spoken descriptions for the character-creation ICON grids.
/// WHY THIS FILE HAS TO EXIST. The icon menus - Gesicht, Frisur, Schwanzform,
/// Fellzeichnung, Ohrenform (Viera) - carry NO name and NO description anywhere in
/// the game data. Base entries have an empty <c>Hint</c> and no <c>HintItem</c>;
/// the only named entries are Friseur-Freischaltungen, which are not offered during
/// creation. So the mod can announce a position and nothing else, and
/// "Gesicht, Typ 3, 3 von 7" tells a blind player where the cursor is and nothing
/// at all about the face.
/// WHERE THE TEXT COMES FROM, and how far it may be trusted:
/// - The entries ARE pictures: 192x192 colour portrait renders under
///   <c>ui/icon/13xxxx/</c>. They were pulled offline with Lumina, composited and
///   looked at, one race/tribe/sex sheet at a time, and described. See
///   <c>docs/charamake-descriptions.md</c> for the extraction pipeline.
/// - **These are MOD-AUTHORED words, not the game's.** The game says nothing here.
///   They are never presented as a quotation of the game, and
///   <see cref="AccessibilityStrings.CharaMakeAuthoredNote"/> is what the Ctrl+F10 summary
///   uses to say so once.
/// - **Structure only, never colour.** The thumbnail is a fixed preview render: its
///   hair and skin are whatever the render used, NOT what the player picked. Hair
///   colour, skin colour and eye colour are their own menus and are described from
///   the real palette by <see cref="ColorNamer"/>. (The earlier note that the icons
///   carry a "blue tint" was wrong - that was a B8G8R8A8/RGBA channel swap in the
///   extraction, corrected 2026-08-08. The no-colour rule stands for the reason
///   above, which is the better one.)
/// - **ONE measured exception, added 2026-08-09 by user decision: a baked ORNAMENT's
///   colour, and only where entries differ by nothing else.** Several rows carry
///   clusters of entries that are the same hair mesh with a differently coloured clip,
///   pin, headband, crown ornament or feather - Lalafell female 133204/133214/133215/
///   133216, Elezen female 132203/132213/132214/132215, Hyur Midlander female
///   131201/131216/131217/131218, Roegadyn male 135003/135014/135015, Miqo'te female
///   134201/134213/134214. These were proven identical the strong way: the ALPHA
///   channel is byte-for-byte equal across the cluster, so the silhouette and geometry
///   are the same and only the ornament's hue moves. The rule's own rationale - "the
///   render's colour is not what the player picked" - does not reach these, because the
///   ornament colour is baked per entry, and without it the entries are indistinguishable
///   to a player who cannot see them while a sighted player picks between them on colour
///   alone. **The colour word is MEASURED, never impressionistic**
///   (tools/charamake-dump + the session's ornament probe: hue/saturation/value over the
///   most saturated pixels of the differing mask). Where saturation is too low to name a
///   hue honestly, brightness is used instead - 133214 is 5 % saturated and is therefore
///   "pale", not "pink", which is what a first pass had called it.
///   NOT VERIFIED, and deliberately not claimed anywhere: whether the game tints these
///   ornaments with the player's hair colour on the 3D model. The icons cannot answer
///   that. The text describes the THUMBNAIL, as all of this file does.
/// - Skin detail IS described - stubble, freckles, lines, tribal markings, scales,
///   fur pattern - because it is proven to belong to the ENTRY and not to a menu
///   that can switch it off: those marks sit in the face's own
///   <c>..._fac_base.tex</c>, checked for Hyur (face 7 carries the stubble),
///   Miqo'te (faces 2-4 carry progressively heavier stripes) and Hrothgar
///   (face 2 tiger stripes, face 3 rosettes). "Facial Features" is a separate menu
///   with its own decals per face and is not what the thumbnails show.
/// SIDES ARE NEVER NAMED - user decision: *"just say one eye, one ear, one hand
/// etc. no need for one/the other, they are indistinguishable."* No description
/// says left or right anywhere.
/// - **Why.** Several type-4 decal slots are the SAME marking mirrored - proven
///   numerically for the Au Ra limbal rings (slots 6 and 7, mirrored-and-aligned
///   MAE 11-20 against 20-37 unmirrored) and true again for the Lalafell ear rings
///   and cheek marks. Which HALF of the face a 192x192 close-up shows cannot be read
///   off the icon reliably: the six race readers disagreed with each other, and the
///   two that refused to call it were the honest ones. A confident wrong side is worse
///   than no side, and the user cannot see the model to catch the error.
/// - **The wording.** A marking that sits on one of a pair of features reads
///   "ein Auge / ein Ohr / eine Wange / eine Braue / einer Gesichtshälfte"
///   ("one eye / one ear / one cheek / one brow / one side of the face").
/// - **Mirrored pairs get the SAME text**, brief and long. Two menu entries that read
///   identically is the honest outcome, not a bug to fix: the entries differ only by a
///   side the mod refuses to guess at.
/// - **The one exception is GEOMETRY, not identity.** A mark that CROSSES the face keeps
///   "von der einen ... zur anderen" / "from one ... to the other", because there the
///   two halves describe the mark's SHAPE and dropping them would lose real information.
/// - Reopening this needs visual confirmation of which half of UV space each decal
///   texture occupies - a measurable fact, and the only route back. Do NOT reintroduce
///   a side from the icon alone.
/// SHARED ENTRIES ARE SHARED DELIBERATELY. Several tribes are drawn from the same
/// faces: Au Ra Raen and Xaela icons are byte-for-byte IDENTICAL, and Lalafell and
/// Roegadyn tribe pairs differ only in the render's skin tone (measured 2026-08-08,
/// under 0.5 % of pixels past a difference of 16). Those ids therefore point at ONE
/// description each, which is why the same text appears against two icon ids.
/// COVERAGE, 2026-08-09. ALL SIX icon menus are COMPLETE:
/// - **Face** - 132/132 icons, all 32 rows.
/// - **Hairstyle** - 879/879 icons over 1,551 slots, all 32 rows.
/// - **Face Paint** - 832/832 real icons, all 32 rows (see NotAnIcon for the 833rd).
/// - **Tail Shape** - 64/64, all 12 rows (Miqo'te, Au Ra, Hrothgar).
/// - **Fur Pattern** - 20/20, all 4 Hrothgar rows.
/// - **Ear Shape (Viera)** - 16/16, all 4 Viera rows.
/// An icon with no entry still returns null and the announcement is byte-identical to
/// what it was, so the null path stays live and is not dead code: it is what a future
/// patch's new hairstyles will hit until they are described.
/// The type-0 menus (Jaw, Eye Shape, Eyebrows, Nose, Mouth, Fang Length, Elezen and
/// Lalafell Ear Shape) have no icon at all and are NOT this table's job - they are
/// shape keys on the face model and want a measurement, not authoring. See
///
public static class CharaMakeIconText
{
    /// <summary>
    /// Icon id 0 is NOT an icon and can never carry a description.
    /// This is a hard guard, not a tidiness check. <c>CharaMakeReader</c> leaves
    /// <c>Icons[i]</c> at its default 0 for every **type-0** menu (see the comment at
    /// its SubMenuType==0 branch: looking a type-0 param up as an icon would describe
    /// a jaw as a hairstyle), and the reader's describe path serves type 0 and type 1
    /// through the SAME branch with no type test. So a single description registered
    /// against 0 would be spoken for every Jaw, Eye Shape, Eyebrows, Nose, Mouth,
    /// Iris Size, Fang Length and Elezen/Lalafell Ear Shape entry in the game.
    /// It is reachable: **Face Paint entry 1 ("no paint") has param 2401, whose
    /// CharaMakeCustomize row has Icon == 0** - so the id is sitting in the dumped
    /// index of a menu currently being authored, one careless paste away from being
    /// registered. Batch 3 spotted it and left it out; this makes that permanent
    /// instead of leaving it to discipline. Same family as docs "THE TRAP".
    /// </summary>
    private const uint NotAnIcon = 0;

    /// <summary>The description for an icon id, or null when none is written yet.</summary>
    public static string? Describe(uint iconId)
        => iconId != NotAnIcon && Text.TryGetValue(iconId, out var t)
            ? (Loc.IsGerman ? t.De : t.En)
            : null;

    /// <summary>
    /// The one- or two-word form, for the CURSOR MOVE. User's
    /// design: *"you might as well just use control+f10 along with the precise
    /// 1 or 2 word summary that would be used in the description."* The full sentence
    /// is what Strg+F10 reads; the summary is what a player sweeping a grid can
    /// actually take in before the next arrow press cuts it off.
    /// Falls back to the FULL text where no summary is written, which is every Face
    /// entry: test item 1 asks the user whether the four-to-six-clause face
    /// sentence is too long to steer by, and until that is answered Face must keep
    /// behaving exactly as it was tested. So this can only ever shorten an entry that
    /// was authored with a short form on purpose.
    /// </summary>
    public static string? Summarize(uint iconId)
    {
        if (iconId == NotAnIcon) return null;                  // see NotAnIcon
        if (!Text.TryGetValue(iconId, out var t)) return null;
        var brief = Loc.IsGerman ? t.BriefDe : t.BriefEn;
        return brief.Length > 0 ? brief : (Loc.IsGerman ? t.De : t.En);
    }

    /// <summary>True when this icon has an authored description.</summary>
    public static bool Has(uint iconId) => iconId != NotAnIcon && Text.ContainsKey(iconId);

    private static readonly Dictionary<uint, (string De, string En, string BriefDe, string BriefEn)> Text = new();

    /// <summary>Registers one description against every icon that shows it, with no
    /// separate short form - the full text is used on the cursor move too.</summary>
    private static void F(string de, string en, params uint[] icons)
    {
        foreach (var i in icons) Text[i] = (de, en, string.Empty, string.Empty);
    }

    /// <summary>Registers a description WITH the one-or-two-word
    /// summary <see cref="Summarize"/> uses on the cursor move. Summary first, because
    /// that is the part a player hears most often.</summary>
    private static void S(string briefDe, string briefEn, string de, string en, params uint[] icons)
    {
        foreach (var i in icons) Text[i] = (de, en, briefDe, briefEn);
    }

    static CharaMakeIconText()
    {
        // ── Gesicht / Face ────────────────────────────────────────────────────
        // Icon ids run 1311xx (Hyur Midlander male) to 1388xx (Viera Veena female),
        // one contiguous block of n per CharaMakeType row. Where two tribes share a
        // block, both ids are listed on the same line.

        // Hyur, Midlander, male - 7 entries
        F("breites Gesicht, gerade Brauen, schmale Augen, kräftige Nase, ebener Mund",
          "broad face, straight brows, narrow eyes, strong nose, level mouth", 131101);
        F("rundes weiches Gesicht, große runde Augen, kleine Nase, Sommersprossen",
          "round soft face, large round eyes, small nose, freckles", 131102);
        F("langes schmales Gesicht, tief liegende Augen, hohe Wangenknochen, ausgeprägte Falten neben dem Mund",
          "long narrow face, deep-set eyes, high cheekbones, pronounced lines beside the mouth", 131103);
        F("kantiges Gesicht, schwere Oberlider, lange gerade Nase, schmale Lippen, spitzes Kinn",
          "angular face, heavy eyelids, long straight nose, thin lips, pointed chin", 131104);
        F("schlankes Gesicht, kräftige gerade Brauen, weit stehende Augen, markanter Kiefer",
          "lean face, strong straight brows, wide-set eyes, pronounced jaw", 131105);
        F("jugendliches Gesicht, feine Züge, runde Augen, kleine Nase, weiches Kinn",
          "youthful face, fine features, round eyes, small nose, soft chin", 131106);
        F("breites Gesicht mit Bartschatten an Wangen, Kinn und Oberlippe, schmale Augen",
          "broad face with stubble on cheeks, chin and upper lip, narrow eyes", 131107);

        // Hyur, Midlander, female - 5 entries
        F("ovales Gesicht, sanft geschwungene Brauen, große mandelförmige Augen, volle Lippen",
          "oval face, softly arched brows, large almond eyes, full lips", 131301);
        F("längeres Gesicht, gerade Brauen, schmalere Augen, längere Nase, breiter schmaler Mund",
          "longer face, straight brows, narrower eyes, longer nose, wide thin mouth", 131302);
        F("schmales Gesicht, hoch geschwungene Brauen, große weit geöffnete Augen, volle Lippen, spitzes Kinn",
          "narrow face, high arched brows, large wide-open eyes, full lips, pointed chin", 131303);
        F("schmales Gesicht, kräftige gerade Brauen, betonter Wimpernkranz, kleiner voller Mund",
          "narrow face, strong straight brows, emphasised lash line, small full mouth", 131304);
        F("ovales Gesicht, stark geschwungene Brauen, große klare Augen, kleine Nase, zierliches Kinn",
          "oval face, strongly arched brows, large clear eyes, small nose, delicate chin", 131305);

        // Hyur, Highlander, male - 4 entries
        F("zerfurchte Stirn, schwerer Brauenbogen, zusammengekniffene Augen, breite Nase, herabgezogener Mund, eckiger Kiefer",
          "furrowed forehead, heavy brow ridge, narrowed eyes, broad nose, downturned mouth, square jaw", 131601);
        F("glattes langes Gesicht, dichte gerade Brauen, ruhige Augen, gerade Nase, voller ebener Mund",
          "smooth long face, thick straight brows, calm eyes, straight nose, full level mouth", 131602);
        F("stark gealtertes Gesicht, schwere Lider, tiefe Falten neben der Nase, herabgezogener Mund, hängende Wangen",
          "heavily aged face, hooded lids, deep folds beside the nose, downturned mouth, sagging cheeks", 131603);
        F("breites ruhiges Gesicht, dünne Brauen, kleine schmale Augen, breite Nase, gerader Mund, volle Wangen",
          "broad calm face, thin brows, small narrow eyes, broad nose, straight mouth, full cheeks", 131604);

        // Hyur, Highlander, female - 4 entries
        F("langes Gesicht, hohe dünne Brauen, schmale schräge Augen, breiter voller Mund, Grübchen im Kinn, Sommersprossen",
          "long face, high thin brows, narrow slanted eyes, wide full mouth, dimpled chin, freckles", 131801);
        F("weicheres rundes Gesicht, gerade Brauen, weit geöffnete Augen, kleine Nase, volle Lippen, dichte Sommersprossen",
          "softer round face, straight brows, wide-open eyes, small nose, full lips, heavy freckles", 131802);
        F("kantiges Gesicht, scharf angewinkelte Brauen, schmale schräge Augen, breiter Mund, kräftiger Kiefer",
          "angular face, sharply angled brows, narrow slanted eyes, wide mouth, strong jaw", 131803);
        F("schmales Gesicht, blasse geschwungene Brauen, ruhige mandelförmige Augen, kleiner Mund",
          "narrow face, pale arched brows, calm almond eyes, small mouth", 131804);

        // Elezen, male - 4 entries, Wildwood and Duskwight share the same faces
        F("langes Gesicht, gerade Brauen, ruhige schmale Augen, feine gerade Nase, schmaler Mund, spitzes Kinn",
          "long face, straight brows, calm narrow eyes, fine straight nose, thin mouth, pointed chin", 132101, 132601);
        F("sehr langes hageres Gesicht, eingefallene Wangen, tief liegende Augen, scharfe Wangenknochen, dünner Mund",
          "very long gaunt face, hollow cheeks, deep-set eyes, sharp cheekbones, thin mouth", 132102, 132602);
        F("weiches jugendliches Gesicht, rundere Wangen, große klare Augen, kleine Nase, kleiner voller Mund",
          "soft youthful face, rounder cheeks, large clear eyes, small nose, small full mouth", 132103, 132603);
        F("stark gealtertes Gesicht, zerfurchte Stirn, schwere Lider, tiefe Falten neben der Nase, herabgezogener breiter Mund",
          "heavily aged face, furrowed forehead, hooded lids, deep folds beside the nose, wide downturned mouth", 132104, 132604);

        // Elezen, female - 4 entries, both tribes
        F("rundlich ovales Gesicht, sanft geschwungene Brauen, große weit geöffnete Augen, kleiner voller Mund, schmales Kinn",
          "rounded oval face, softly arched brows, large wide-open eyes, small full mouth, narrow chin", 132301, 132801);
        F("längeres Gesicht, dünne gerade Brauen, schmalere Augen, längere Nase, breiter schmaler Mund",
          "longer face, thin straight brows, narrower eyes, longer nose, wide thin mouth", 132302, 132802);
        F("breitere Wangen, schräg ansteigende Augen, kleine Nase, breiter voller Mund, weicher Kiefer, Sommersprossen",
          "broader cheeks, upward-slanting eyes, small nose, wide full mouth, soft jaw, freckles", 132303, 132803);
        F("schmales Gesicht, eng stehende Augen, kleine Nase, schmaler Mund, spitzes Kinn",
          "narrow face, close-set eyes, small nose, thin mouth, pointed chin", 132304, 132804);

        // Lalafell, male - 4 entries, Plainsfolk and Dunesfolk share the same faces
        F("rundes Gesicht, sehr feine helle Brauen, große runde Augen, winzige Nase, kleiner Mund",
          "round face, very fine pale brows, large round eyes, tiny nose, small mouth", 133101, 133601);
        F("rundes Gesicht, kräftige gerade Brauen, große Augen, Sommersprossen über Nase und Wangen",
          "round face, strong straight brows, large eyes, freckles across nose and cheeks", 133102, 133602);
        F("rundes Gesicht, dünne hoch angewinkelte Brauen, schmalere Augen mit spitzem Außenwinkel",
          "round face, thin high-angled brows, narrower eyes with a pointed outer corner", 133103, 133603);
        F("rundes Gesicht, dicke helle Brauen, schmale mandelförmige Augen, dichte Sommersprossen",
          "round face, thick pale brows, narrow almond eyes, heavy freckles", 133104, 133604);

        // Lalafell, female - 4 entries, both tribes
        F("rundes Gesicht, sehr dünne geschwungene Brauen, große runde Augen mit langen Wimpern, winziger Mund",
          "round face, very thin arched brows, large round eyes with long lashes, tiny mouth", 133301, 133801);
        F("rundes Gesicht, kaum sichtbare Brauen, sehr große weit stehende Augen, kräftig gerötete Wangen",
          "round face, barely visible brows, very large wide-set eyes, strongly flushed cheeks", 133302, 133802);
        F("rundes Gesicht, kräftigere gerade Brauen, große Augen mit spitzem Außenwinkel, Sommersprossen",
          "round face, stronger straight brows, large eyes with a pointed outer corner, freckles", 133303, 133803);
        F("rundes Gesicht, kurze feine Brauen, schmalere Augen mit betontem Lid, kleiner Mund",
          "round face, short fine brows, narrower eyes with a pronounced lid, small mouth", 133304, 133804);

        // Miqo'te, male - 4 entries, Seeker and Keeper share the same faces
        F("schmales weiches Gesicht, gerade Brauen, ruhige Augen, kleine Nase, volle Lippen, dunkle Zeichnung unter den Augen",
          "narrow soft face, straight brows, calm eyes, small nose, full lips, dark markings under the eyes", 134101, 134601);
        F("zorniges Gesicht, zerfurchte Brauen, schwere Lider, kräftige Nase, schmaler Mund, breite Zeichnung um Augen und Wangen",
          "angry face, furrowed brows, heavy lids, strong nose, thin mouth, broad markings around eyes and cheeks", 134102, 134602);
        F("jugendliches Gesicht, runde Wangen, große Augen, kleine Nase, kleiner voller Mund, feine Zeichnung unter den Augen",
          "youthful face, round cheeks, large eyes, small nose, small full mouth, fine markings under the eyes", 134103, 134603);
        F("langes kantiges Gesicht, hohe Wangenknochen, schmale schräge Augen, schmaler Mund, Streifen über Braue und Wange",
          "long angular face, high cheekbones, narrow slanted eyes, thin mouth, stripes over brow and cheek", 134104, 134604);

        // Miqo'te, female - 4 entries, both tribes
        F("rundes weiches Gesicht, dünne geschwungene Brauen, große runde Augen, kleiner voller Mund, kurze Streifen auf einer Wange",
          "round soft face, thin arched brows, large round eyes, small full mouth, short stripes on one cheek", 134301, 134801);
        F("schmaleres Gesicht, angewinkelte Brauen, mandelförmige Augen, schmaler Mund, lange geschwungene Streifen an Wange und Schläfe",
          "narrower face, angled brows, almond eyes, thin mouth, long sweeping stripes on cheek and temple", 134302, 134802);
        F("rundes Gesicht mit vollen Wangen, große Augen, kleine Nase, breiter Mund, mehrere Streifen auf Wange und Stirn",
          "round face with full cheeks, large eyes, small nose, wide mouth, several stripes on cheek and forehead", 134303, 134803);
        F("schmales Gesicht, feine Züge, kleiner Mund, breite Streifen über Stirn und Wange",
          "narrow face, fine features, small mouth, broad stripes across forehead and cheek", 134304, 134804);

        // Roegadyn, male - 4 entries, Sea Wolf and Hellsguard share the same faces
        F("sehr breites schweres Gesicht, tiefer Brauenbogen, kleine tief liegende Augen, breite flache Nase, gewaltiger eckiger Kiefer",
          "very broad heavy face, low brow ridge, small deep-set eyes, broad flat nose, huge square jaw", 135101, 135601);
        F("schmaleres Gesicht mit scharfen Wangenknochen, zornig angewinkelte Brauen, starrende Augen, herabgezogener Mund, kräftiges Kinn",
          "narrower face with sharp cheekbones, angrily angled brows, staring eyes, downturned mouth, strong chin", 135102, 135602);
        F("stark zerfurchtes Gesicht, Falten über Stirn und Wangen, kleine verdeckte Augen, breite flache Nase, breiter schmaler Mund",
          "heavily lined face, creases over forehead and cheeks, small hooded eyes, broad flat nose, wide thin mouth", 135103, 135603);
        F("breites Gesicht, tiefe gerade Brauen, kleine ruhige Augen, breite Nase, schmaler gerader Mund, massiger Kiefer",
          "broad face, low straight brows, small calm eyes, broad nose, thin straight mouth, massive jaw", 135104, 135604);

        // Roegadyn, female - 4 entries, both tribes
        F("langes Gesicht, sanft geschwungene Brauen, große ruhige Augen, gerade Nase, volle Lippen, Sommersprossen",
          "long face, softly arched brows, large calm eyes, straight nose, full lips, freckles", 135301, 135801);
        F("scharf geschnittenes Gesicht, stark angewinkelte Brauen, schmale schräge Augen mit betontem Lidstrich, kleiner Mund",
          "sharply cut face, strongly angled brows, narrow slanted eyes with a pronounced lid line, small mouth", 135302, 135802);
        F("weiches ovales Gesicht, sanft geschwungene Brauen, große mandelförmige Augen, kleine Nase, volle Lippen",
          "soft oval face, softly arched brows, large almond eyes, small nose, full lips", 135303, 135803);
        F("langes schmales Gesicht, gerade Brauen, ruhige Augen, feine gerade Nase, breiter schmaler Mund",
          "long narrow face, straight brows, calm eyes, fine straight nose, wide thin mouth", 135304, 135804);

        // Au Ra, male - 4 entries; the Raen and Xaela icons are byte-for-byte identical
        F("langes Gesicht, Schuppenkrone über den Brauen, schmale angewinkelte Augen, gerade Nase, schmaler Mund",
          "long face, a crown of scales above the brows, narrow angled eyes, straight nose, thin mouth", 136101, 136601);
        F("langes Gesicht, kleine Schuppenhörner an den Schläfen, freie Stirn, scharfe Wangenknochen, ruhige schmale Augen",
          "long face, small scale horns at the temples, clear forehead, sharp cheekbones, calm narrow eyes", 136102, 136602);
        F("schmales glattes Gesicht, wenig Beschuppung, große kantige Augen, kleine Nase, kleiner Mund",
          "narrow smooth face, little scaling, large angular eyes, small nose, small mouth", 136103, 136603);
        F("breiteres Gesicht, kräftige Schuppen auf Nasenrücken und Wangen, schwere Lider unter gerader Braue, breiterer Mund",
          "broader face, strong scales on the nose bridge and cheeks, heavy lids under a straight brow, wider mouth", 136104, 136604);

        // Au Ra, female - 4 entries, both tribes
        F("weiches ovales Gesicht, kleiner Schuppenkamm zwischen den Brauen, große klare Augen, kleiner voller Mund",
          "soft oval face, a small scale crest between the brows, large clear eyes, small full mouth", 136301, 136801);
        F("rundere Wangen, Schuppen an Schläfe und Kieferlinie, große Augen, kleine Nase, vollere Lippen",
          "rounder cheeks, scales at the temple and jawline, large eyes, small nose, fuller lips", 136302, 136802);
        F("schmales Gesicht, Schuppenfelder über Wangenknochen und Kiefer, große Augen, voller Mund",
          "narrow face, patches of scales over cheekbones and jaw, large eyes, full mouth", 136303, 136803);
        F("schmales Gesicht mit der stärksten Beschuppung, Kamm über den Brauen, Schuppen über Wangen und Kiefer, schmale schräge Augen",
          "narrow face with the heaviest scaling, a crest above the brows, scales over cheeks and jaw, narrow slanted eyes", 136304, 136804);

        // Hrothgar, Helions, male - 4 entries. Helions and The Lost do NOT share
        // faces: they are separate models, and The Lost render shows no pattern.
        F("glattes Fell ohne Musterung, dunkle Maske um Nase und Maul, heller Bart entlang des Kiefers, kleine runde Ohren",
          "plain fur with no pattern, a dark mask around nose and muzzle, pale beard along the jaw, small round ears", 137105);
        F("kräftige dunkle Streifen über Braue, Wange und Nasenrücken, größere spitze Ohren",
          "strong dark stripes over brow, cheek and nose bridge, larger pointed ears", 137106);
        F("dicht getupftes Muster über Stirn und Wange, feine Tupfen am Maul, buschige Brauen",
          "densely spotted pattern over forehead and cheek, fine spots on the muzzle, bushy brows", 137107);
        F("wenige breite Streifen an Braue und Wange mit einzelnen Tupfen, breites Maul",
          "a few broad stripes on brow and cheek with scattered spots, broad muzzle", 137108);

        // Hrothgar, Helions, female - 4 entries
        F("glattes Fell ohne Musterung, dunkle Linie unter jedem Auge, breites flaches Maul, runde Ohren",
          "plain fur with no pattern, a dark line under each eye, broad flat muzzle, round ears", 137305);
        F("kräftige dunkle Streifen an Wange und Braue, dunkler Strich über dem Nasenrücken, spitze Ohren",
          "strong dark stripes on cheek and brow, a dark line over the nose bridge, pointed ears", 137306);
        F("fein getupftes Muster über Stirn und Wange, lange aufrechte Ohren",
          "finely spotted pattern over forehead and cheek, long upright ears", 137307);
        F("einzelne kurze Streifen an Wangen und Brauen, helles Maul, große Ohren",
          "scattered short stripes on cheeks and brows, pale muzzle, large ears", 137308);

        // Hrothgar, The Lost, male - 4 entries
        F("breites schweres Maul, kleine verdeckte Augen, dichte Mähne, kurzer Bart unter dem Kinn, keine sichtbare Musterung",
          "broad heavy muzzle, small hooded eyes, thick mane, short beard under the chin, no visible pattern", 137605);
        F("längeres schmaleres Maul, ruhige Augen, glattes Gesicht, keine sichtbare Musterung",
          "longer narrower muzzle, calm eyes, smooth face, no visible pattern", 137606);
        F("scharf angewinkelte Brauen, zusammengekniffene Augen, kürzeres Maul, keine sichtbare Musterung",
          "sharply angled brows, narrowed eyes, shorter muzzle, no visible pattern", 137607);
        F("sehr breites Gesicht, schwerer finsterer Brauenbogen, tief liegende schmale Augen, breites flaches Maul",
          "very broad face, heavy scowling brow ridge, deep-set narrow eyes, broad flat muzzle", 137608);

        // Hrothgar, The Lost, female - 4 entries
        F("rundes Maul, ruhige Augen, runde Ohren, keine sichtbare Musterung",
          "round muzzle, calm eyes, round ears, no visible pattern", 137805);
        F("feineres Maul, größere Augen, spitze büschelige Ohren, keine sichtbare Musterung",
          "finer muzzle, larger eyes, pointed tufted ears, no visible pattern", 137806);
        F("zusammengekniffene Augen, kantige Brauen, lange aufrechte Ohren",
          "narrowed eyes, angular brows, long upright ears", 137807);
        F("breiteres flacheres Gesicht, ruhige Augen, breites Maul, große Ohren",
          "broader flatter face, calm eyes, broad muzzle, large ears", 137808);

        // Viera, male - 4 entries, Rava and Veena share the same faces
        F("langes Gesicht, gerade Brauen, ruhige schmale Augen, feine gerade Nase, schmaler Mund, schmales Kinn",
          "long face, straight brows, calm narrow eyes, fine straight nose, thin mouth, narrow chin", 138101, 138601);
        F("weicheres Gesicht, höher geschwungene Brauen, größere rundere Augen, kleine gerade Nase, vollerer Mund",
          "softer face, higher arched brows, larger rounder eyes, small straight nose, fuller mouth", 138102, 138602);
        F("schmaleres Gesicht mit schärferen Wangenknochen, dünne geschwungene Brauen, leicht schräge Augen, kleiner Mund",
          "narrower face with sharper cheekbones, thin arched brows, slightly slanted eyes, small mouth", 138103, 138603);
        F("längeres Gesicht, kräftige dunkle Brauen, tiefer liegende Augen, längere Nase, breiter voller Mund",
          "longer face, strong dark brows, deeper-set eyes, longer nose, wide full mouth", 138104, 138604);

        // Viera, female - 4 entries, both tribes
        F("ovales Gesicht, gerade helle Brauen, große mandelförmige Augen, kleine gerade Nase, kleiner voller Mund",
          "oval face, straight pale brows, large almond eyes, small straight nose, small full mouth", 138301, 138801);
        F("schmaleres Gesicht, feinere hohe Brauen, größere rundere Augen, vollere Unterlippe",
          "narrower face, finer high brows, larger rounder eyes, fuller lower lip", 138302, 138802);
        F("rundere Wangen, weit stehende große Augen, kleine Nase, kleiner Mund",
          "rounder cheeks, wide-set large eyes, small nose, small mouth", 138303, 138803);
        F("schmaleres Gesicht, ruhige Brauen, schmalere Augen mit mehr Lid, vollerer Mund, betontes Kinn",
          "narrower face, calm brows, narrower eyes showing more lid, fuller mouth, defined chin", 138304, 138804);

        // ── Frisur / Hairstyle ────────────────────────────────────────────────
        // 879 unique icons over 1,551 slots. Two structural facts,
        // both MEASURED off the sheet dump, that decide how this is batched:
        // 1. **The game names none of them.** All 879 CharaMakeCustomize params carry
        //    an EMPTY Hint AND an empty HintItem, and none is IsPurchasable (cmdump
        //    `names Hairstyle`, 2026-08-08). said as much; this checks it per
        //    entry rather than on trust, because a game-supplied name would have to
        //    win over anything authored here. Face Paint is the same: 0 of 833.
        // 2. **The blocks do not overlap AT ALL.** The 32 rows collapse to 18 distinct
        //    icon sets - the two tribes of a race always share their hairstyle list,
        //    except Hyur, where Midlander and Highlander are genuinely different - and
        //    between those 18 sets the intersection is EMPTY. Measured: Hyur Midlander
        //    male's 53 icons appear in no other row. So a finished block covers exactly
        //    its own row(s), and no description can be reused across races.
        // COMPLETE as of 2026-08-09: all 18 blocks, 879 icons, 1,551 slots, 32 of 32
        // rows. Coverage grew one block at a time because it had to - between the 18
        // sets the intersection is empty, so nothing was ever reusable across races.
        // **Entry order is NOT icon order, and this is the trap that has already bitten
        // this table once.** Entry 13 of Hyur Midlander male is icon 131002; Hrothgar
        // interleaves two runs outright (entry 1 = 137001, entry 2 = 137009, entry 3 =
        // 137002). The batch-2 contact sheets labelled each cell with its ENTRY NUMBER
        // ONLY (mksheet.py), so an author reading them had to map entry -> icon by hand,
        // and a block that assumed "entry k is the k-th icon id" silently attached every
        // description to the wrong picture. The sheets now label cells "<entry> #<iconId>"
        // and the icon id is copied, never inferred; verify.py's entry-order check will
        // catch a transposition. Do not go back to entry-only labels.

        // Hyur, Midlander, male - 53 entries, exclusive to this row
        // RE-AUTHORED, same fault as the Hrothgar blocks: written
        // against the sorted id list while the sheet was in ENTRY order, and the two
        // differ here too (entry 13 is icon 131002, entry 17 is 131008, entry 19 is
        // 131022 BEFORE entry 20 = 131021). A four-entry spot-check passed before the
        // re-read and did NOT catch it, because it sampled the distinctive entries -
        // the shaved scrollwork, the buzz cut, the bald crown tuft - which are the
        // hardest to misdescribe. The plain ones were wrong: 131007 is combed straight
        // back with no parting, 131010 is swept BACK not forward, and 131012 has no
        // crown tuft at all. Verified on the pictures before replacing.
        S("kurz, stufig", "short, layered",
          "kurzer stufiger Schnitt, oben ausgedünnt und struppig, Seiten kurz, einzelne Spitzen auf der Stirn",
          "short layered cut, thinned and choppy on top, short sides, single points on the forehead", 131001);
        S("kurze Seiten, Stachelquiff", "short sides, spiked quiff",
          "Seiten kurz getrimmt, das Deckhaar steil nach hinten oben in lange Stacheln gestellt",
          "sides trimmed short, the top hair styled steeply up and back into long spikes", 131003);
        S("zottig, lange Franse", "shaggy, long fringe",
          "mittellanger Schnitt, eine lange Franse fällt seitlich über die Braue, hinten spitz auslaufende Stufen",
          "medium length cut, a long fringe falls sideways over the brow, layers tapering to points at the back", 131004);
        S("rasiert, Muster", "shaved, cut pattern",
          "der ganze Kopf kurz rasiert, seitlich über dem Ohr ein eingeschnittenes, verzweigtes Muster",
          "the whole head shaved close, a branching pattern cut into the side above the ear", 131005);
        S("Flechtreihen, Stirnband", "cornrows, headband",
          "enge Flechtreihen laufen vom Haaransatz nach hinten und enden in einem kleinen Knoten, dazu ein schmales Stirnband",
          "tight braided rows run back from the hairline and end in a small knot, plus a narrow headband", 131006);
        S("glatt zurückgekämmt", "combed straight back",
          "kurzes Haar glatt aus der Stirn nach hinten gekämmt, eng am Kopf anliegend, Ohren frei",
          "short hair combed smoothly back off the forehead, lying close to the head, ears free", 131007);
        S("runde Kappe", "rounded cap",
          "glatte runde Kappe mit Franse bis zur Braue, vor dem Ohr spitz zulaufend, Nacken kurz",
          "smooth rounded cap with a fringe to the brow, tapering to a point in front of the ear, short nape", 131009);
        S("wellig nach hinten", "wavy, swept back",
          "das Haar in einer weichen Welle nach hinten gestrichen, Seiten über das Ohr gelegt, Nacken federt aus",
          "hair swept back in a soft wave, sides laid over the ear, the nape feathering out", 131010);
        S("zottig, wehende Spitzen", "shaggy, flicked points",
          "zottig gestuftes Haar, feine Strähnen auf der Stirn, die Spitzen stehen im Nacken nach außen ab",
          "shaggy layered hair, fine strands on the forehead, the points standing out at the nape", 131011);
        S("hoch zurück, Wangenspitzen", "swept high, cheek points",
          "das Haar hoch nach hinten gestrichen, spitze Strähnen hängen vor und hinter dem Ohr bis zum Kiefer",
          "hair swept high to the back, pointed strands hanging in front of and behind the ear to the jaw", 131012);
        S("lange Stacheln", "long spikes",
          "das ganze Haar in lange, scharfe Stacheln nach hinten gestellt, die Spitzen stehen weit ab",
          "the whole head styled into long sharp spikes pointing back, the points standing well clear", 131013);
        S("Bürstenschnitt", "buzz cut",
          "rundum sehr kurz geschorenes Haar, oben nur wenig länger als an den kurzen Seiten",
          "hair clipped very short all around, only slightly longer on top than at the short sides", 131014);
        S("gescheitelt, lange Strähne", "parted, long strand",
          "seitlich gescheitelt, eine lange Strähne fällt vor dem Ohr über die Wange bis unters Kinn, Nacken ausgefranst",
          "parted at the side, a long strand falls in front of the ear over the cheek below the chin, frayed nape", 131002);
        S("zurückgelegt, Ohrsträhne", "laid back, ear strand",
          "das Haar aus der Stirn nach hinten gelegt, eine Strähne fällt vor dem Ohr auf die Wange, Nacken ausgefranst",
          "the hair laid back off the forehead, a strand falls in front of the ear onto the cheek, frayed nape", 131015);
        S("gescheitelt, kinnlang", "parted, chin length",
          "seitlich gescheitelt, das Haar fällt glatt über die Schläfe bis zum Kiefer, die Spitzen im Nacken gezackt",
          "parted at the side, hair falling smoothly over the temple to the jaw, jagged points at the nape", 131016);
        S("zurückgelegt, Ohren frei", "laid back, ears free",
          "das Haar glatt nach hinten gelegt, Schläfe und Ohr bleiben frei, im Nacken gezackte Spitzen",
          "hair laid smoothly back, temple and ear left free, jagged points at the nape", 131017);
        S("Undercut, Knoten", "undercut, bun",
          "Seiten und Hinterkopf kurz rasiert, das Deckhaar glatt nach hinten gekämmt und zu einem kleinen Knoten gebunden",
          "sides and back shaved short, the top hair combed smoothly back and tied into a small bun", 131008);
        S("Undercut, volles Deckhaar", "undercut, fuller top",
          "Seiten rasiert, das vollere Deckhaar liegt tiefer über der Schläfe und endet hinten in einem kleinen Knoten",
          "sides shaved, the fuller top hair lying lower over the temple and ending in a small bun at the back", 131018);
        S("kahl, Schopf", "bald, topknot",
          "kahl geschorener Kopf mit einem einzelnen kleinen Schopf am Scheitel",
          "shaved head with a single small tuft at the crown", 131022);
        S("gestuft, abstehende Strähne", "layered, stray strand",
          "mittellang und gestuft, das Haar fällt über ein Auge, am Scheitel steht eine einzelne Strähne senkrecht ab",
          "medium length and layered, hair falling over one eye, a single strand standing upright at the crown", 131021);
        S("wellig gestuft", "wavy, layered",
          "mittellang und gestuft, die Spitzen schwingen im Nacken nach außen, feine Strähnen hängen von den Schläfen herab",
          "medium length and layered, the points swinging outwards at the nape, fine strands hanging down from the temples", 131023);
        S("lang, Stirnband", "long, headband",
          "langes glattes Haar über die Schultern, in der Mitte gescheitelt, ein schmales Band quer über der Stirn",
          "long straight hair over the shoulders, parted in the middle, a narrow band across the forehead", 131024);
        S("weich, aufstehende Locke", "soft, upstanding lock",
          "weich gestuftes kurzes Haar mit voller Franse, vorn am Scheitel steht eine Locke keck ab",
          "softly layered short hair with a full fringe, a lock standing up jauntily at the front of the crown", 131025);
        S("zottig, Seitenfranse", "shaggy, side fringe",
          "zottig gestuftes Haar, die Franse seitlich über die Stirn gelegt, spitze Strähnen an Wange und Nacken",
          "shaggy layered hair, the fringe swept sideways over the forehead, pointed strands at cheek and nape", 131026);
        S("kurz, ausgedünnte Franse", "short, thinned fringe",
          "kurzes zottiges Haar, die ausgedünnte Franse fällt in einzelnen Spitzen auf die Stirn, Nacken kurz",
          "short shaggy hair, the thinned fringe falling onto the forehead in single points, short nape", 131028);
        S("gerollte Tolle", "rolled quiff",
          "das Deckhaar zu einer hohen, glatt gerollten Tolle nach hinten gelegt, Seiten kurz angelegt",
          "the top hair rolled back into a high smooth quiff, the sides laid down short", 131027);
        S("hoher Fächerzopf", "high fanned topknot",
          "das Haar straff nach hinten gezogen, am Scheitel ein hoher, fächerförmig gespreizter Zopf, seitlich eine Spange",
          "hair pulled tightly back, a high fan-shaped topknot at the crown, a clasp at the side", 131030);
        S("kurz, anliegend", "short, close-lying",
          "kurzes Haar glatt nach hinten anliegend gebürstet, an der Stirn eine kleine Spitze, Ohren frei",
          "short hair brushed back flat against the head, a small peak at the forehead, ears free", 131031);
        S("kurz, dicht stachelig", "short, densely spiky",
          "kurzes Haar, oben dicht in kleine Stacheln gestellt, gezackter Haaransatz an der Schläfe, Ohren frei",
          "short hair spiked densely on top, a jagged hairline at the temple, ears free", 131032);
        S("kurz, Seitenscheitel", "short, side parting",
          "kurzer Schnitt mit Seitenscheitel, die glatte Franse streicht über die Stirn zur Schläfe, Nacken kurz",
          "short cut with a side parting, the smooth fringe sweeping across the forehead to the temple, short nape", 131033);
        S("Nacken nach außen", "nape flicking out",
          "das Haar vom Scheitel nach hinten gestrichen, die Wange bleibt frei, die Spitzen schwingen im Nacken nach außen",
          "hair swept back from the parting, the cheek left free, the points swinging outwards at the nape", 131034);
        S("gescheitelt, Wangenlocke", "parted, lock on cheek",
          "gescheiteltes Haar nach hinten gelegt, eine Strähne fällt auf die Wange, die Länge reicht bis in den Nacken",
          "parted hair laid back, a strand falling onto the cheek, the length reaching into the nape", 131037);
        S("gezackte Franse", "jagged fringe",
          "eine gezackte Franse liegt auf den Brauen, die Längen am Kiefer schwingen kräftig nach außen",
          "a jagged fringe sits on the brows, the lengths at the jaw swinging strongly outwards", 131038);
        S("wuchtiger Schopf", "bulky crest",
          "viel Volumen, das Deckhaar zerzaust zur Seite geworfen und am Scheitel stachelig, die Franse deckt ein Auge",
          "lots of volume, the top hair tossed tousled to one side and spiky at the crown, the fringe covering one eye", 131039);
        S("voluminös zurückgestrichen", "voluminous, swept back",
          "das Haar voll nach hinten gestrichen, einzelne feine Strähnen fallen über die Braue, Nacken stachelig",
          "hair swept fully back, a few fine strands falling over the brow, spiky nape", 131040);
        S("Ponyfranse, dicker Zopf", "blunt fringe, thick braid",
          "glatte Ponyfranse bis zu den Brauen, seitlich fällt das Haar bis zum Kiefer, hinten ein dicker Flechtzopf",
          "straight blunt fringe to the brows, the sides falling to the jaw, a thick braid down the back", 131041);
        S("weicher Bob", "soft bob",
          "weicher, kinnlanger Schnitt, feine Strähnen auf der Stirn, die Spitzen biegen sich im Nacken nach außen",
          "soft chin-length cut, fine strands on the forehead, the points curving outwards at the nape", 131042);
        S("hochgekämmt, Flechtzopf", "combed up, braid",
          "das Deckhaar hoch nach hinten gekämmt, hinter dem Ohr ein schmaler Flechtzopf, der in einem Band endet",
          "the top hair combed high to the back, a narrow braid behind the ear ending in a ribbon", 131043);
        S("riesige Tolle", "huge pompadour",
          "eine sehr große, glatt gekämmte Tolle wölbt sich weit über die Stirn, Seiten nach hinten gelegt",
          "a very large smoothly combed pompadour arching far over the forehead, the sides laid back", 131044);
        S("zottig, aufragender Stachel", "shaggy, jutting spike",
          "zottig gestuftes Haar bis zum Kiefer, die Franse deckt ein Auge, am Scheitel ragt ein Stachel auf",
          "shaggy layered hair to the jaw, the fringe covering one eye, a spike jutting up at the crown", 131045);
        S("kurz, glatte Seitenfranse", "short, sleek side fringe",
          "kurzer Schnitt, eine glatte lange Franse streicht seitlich bis zum Wangenknochen, Hinterkopf kurz und struppig",
          "short cut, a long sleek fringe sweeping sideways to the cheekbone, short tousled back", 131047);
        S("Seitenscheitel, spitze Enden", "side parting, pointed ends",
          "mittellang mit Seitenscheitel, die Franse fällt über die Braue, die Längen laufen am Kiefer spitz aus",
          "medium length with a side parting, the fringe falling over the brow, the lengths tapering to points at the jaw", 131048);
        S("Undercut, Stachelmähne", "undercut, spiky mane",
          "eine Seite kurz rasiert, das lange Deckhaar stachelig nach hinten gestellt und gezackt in den Nacken fallend",
          "one side shaved short, the long top hair spiked backwards and falling jagged into the nape", 131049);
        S("Ponyfranse, hinten lang", "blunt fringe, long behind",
          "glatte Ponyfranse über den Brauen, das übrige Haar hinter das Ohr gelegt und lang herabhängend",
          "straight blunt fringe over the brows, the rest laid behind the ear and hanging down long", 131051);
        S("gestuft, gefiederte Spitzen", "layered, feathered points",
          "oben glatt gescheitelt, die Längen laufen rund um den Kiefer in stark gefiederte Spitzen aus",
          "smoothly parted on top, the lengths running out into heavily feathered points around the jaw", 131052);
        S("kurze Locken", "short curls",
          "sehr kurz geschnittenes, dicht gekräuseltes Haar, gleichmäßig über den ganzen Kopf, Ohren frei",
          "very short densely curled hair, even over the whole head, ears free", 131055);
        S("volle Seitenfranse", "full side fringe",
          "viel Volumen nach hinten gelegt, eine dicke Franse deckt eine Braue und läuft spitz auf die Wange",
          "plenty of volume laid back, a thick fringe covering one brow and tapering to a point on the cheek", 131056);
        S("hoher Zopf", "high ponytail",
          "das Haar straff nach oben zu einem stacheligen Zopf am Scheitel gebunden, eine lange Strähne fällt vor dem Ohr",
          "hair bound tightly up into a spiky ponytail at the crown, a long strand falling in front of the ear", 131057);
        S("Knoten, offenes Haar", "bun, loose hair",
          "das Deckhaar oben zu einem Knoten gebunden, das übrige lange Haar fällt offen über die Schultern",
          "the top hair tied into a knot above, the rest of the long hair falling loose over the shoulders", 131058);
        S("Pilzschnitt", "bowl cut",
          "runder Pilzschnitt, die Franse fällt weich auf die Stirn, das Haar deckt die Ohren, Nacken spitz zulaufend",
          "rounded bowl cut, the fringe falling softly onto the forehead, hair covering the ears, tapering nape", 131059);
        S("lang, glatte Ponyfranse", "long, blunt fringe",
          "langes glattes Haar fällt über die Schultern, davor eine gerade abgeschnittene Ponyfranse bis zu den Brauen",
          "long straight hair falling over the shoulders, with a bluntly cut fringe down to the brows", 131060);
        S("zerzaust, Ohren verdeckt", "tousled, ears hidden",
          "struppig geschnittenes Haar, die stark ausgedünnte Franse fällt gezackt über Stirn und Ohren",
          "tousled cut hair, the heavily thinned fringe falling jagged over forehead and ears", 131068);
        S("zurückgestrichen, zwei Strähnen", "swept back, two strands",
          "das Haar voluminös nach hinten gestrichen, zwei feine Strähnen hängen an der Schläfe ins Gesicht",
          "hair swept back with volume, two fine strands hanging down at the temple into the face", 131085);

        // Hrothgar, male - 45 entries. Helions and The Lost use the SAME icon ids,
        // so one entry covers both tribes with no second id to list - unlike the Face
        // block, where the two tribes have separate icons and separate text.
        // RE-AUTHORED. The batch-2 text for this block was written
        // against the sorted id list while the contact sheet was laid out in ENTRY
        // order, and the two differ: entries 1-16 are eight base cuts INTERLEAVED with
        // their eight "+8" twins (137001/137009, 137002/137010, ... 137008/137016),
        // each twin being the same cut plus one added element. Describing them
        // positionally swapped every pair. Proven on the pictures before replacing:
        // 137013 is a wrapped ponytail with a ring and a clasp (was "back, flicked"),
        // 137015 carries two clasped temple braids (was "mane, full"), and 137005 is a
        // tall wave over an undercut (was "very short with a small tuft").
        // Only the HAIR is described. These renders carry heavy face markings and a
        // chin ruff, and both belong to the Face entry, not here.
        S("hohe Mähne", "tall mane",
          "volle Mähne hoch aufgetürmt und nach hinten gestrichen, Stirn frei, das Haar fällt breit über den Nacken",
          "full mane piled high and swept back, forehead clear, the hair falling broadly over the nape", 137001);
        S("Mähne mit Vordersträhne", "mane with front strand",
          "hohe Mähne nach hinten gestrichen, eine lange Strähne fällt vorn über Schläfe und Wange",
          "tall mane swept back, one long strand falling forward over temple and cheek", 137009);
        S("Stirn frei, Kieferlänge", "forehead clear, jaw length",
          "mittellang, schräg nach hinten gekämmt, die Stirn bleibt frei, das Seitenhaar fällt hinter dem Ohr bis zum Kiefer",
          "medium length, combed diagonally back, the forehead stays clear, the side hair falling behind the ear to the jaw", 137002);
        S("Franse über der Stirn", "fringe over the forehead",
          "mittellang, das Deckhaar fällt als Franse über die Stirn, die Seiten ziehen am Ohr vorbei bis zum Kiefer",
          "medium length, the top falls as a fringe over the forehead, the sides passing the ear to the jaw", 137010);
        S("breiter Fächer oben", "broad fan on top",
          "das Deckhaar steigt als breiter Fächer auf, seitlich hängen einzelne lange Spitzen bis zum Kiefer",
          "the top hair rises in a broad fan, single long points hanging at the sides down to the jaw", 137003);
        S("Schläfenflechte mit Perle", "temple braid with bead",
          "breiter Fächer oben, an der Schläfe eine dünne Flechte mit Perle, die bis unter das Kinn reicht",
          "broad fan on top, a thin braid with a bead at the temple, reaching below the chin", 137011);
        S("Schopf mit Seitenflügeln", "crest with side wings",
          "spitzer Schopf über dem Scheitel, das Seitenhaar steht in Flügeln vom Kopf ab",
          "pointed crest over the crown, the side hair standing out from the head in wings", 137004);
        S("Schopf, Locke vorn", "crest, lock in front",
          "spitzer Schopf über dem Scheitel, eine lange Locke fällt vorn über Schläfe und Auge",
          "pointed crest over the crown, a long lock falling forward over temple and eye", 137012);
        S("Undercut, Welle", "undercut, wave",
          "Schläfen und Seiten kurz gehalten, das Deckhaar in einer hohen Welle nach hinten gestrichen",
          "temples and sides kept short, the top hair swept back in a tall wave", 137005);
        S("umwickelter Zopf mit Ring", "wrapped ponytail with ring",
          "Deckhaar nach hinten gestrichen, hinten ein umwickelter Zopf mit Ring und Metallspange",
          "top hair swept back, at the back a wrapped ponytail with a ring and a metal clasp", 137013);
        S("schmaler Scheitelstreif", "narrow crown strip",
          "nur ein schmaler Streifen auf dem Scheitel, nach hinten gestrichen und hinter dem Ohr bis zum Kiefer fallend",
          "only a narrow strip on the crown, swept back and falling behind the ear to the jaw", 137006);
        S("Scheitelstreif mit Schläfenspitze", "crown strip with temple point",
          "schmaler Streifen auf dem Scheitel, dazu eine spitze Strähne vor dem Ohr an der Schläfe",
          "narrow strip on the crown, plus a pointed lock at the temple in front of the ear", 137014);
        S("hoher Stachelschopf", "tall spiky crest",
          "hohe stachelige Spitzen am Scheitel, das Seitenhaar fällt glatt bis unter den Kiefer",
          "tall spiky points at the crown, the side hair falling smoothly to below the jaw", 137007);
        S("zwei Schläfenzöpfe", "two temple braids",
          "stacheliger Scheitel, an beiden Schläfen je eine dünne Flechte mit Spange bis unter das Kinn",
          "spiky crown, a thin braid with a clasp at each temple, reaching below the chin", 137015);
        S("Knoten am Scheitel", "bun at the crown",
          "kleiner gebundener Knoten oben auf dem Scheitel, das übrige Haar kurz und struppig",
          "small tied bun on top of the crown, the rest of the hair short and tousled", 137008);
        S("Knoten, Front aufgestellt", "bun, raised front",
          "kleiner Knoten oben auf dem Scheitel, das vordere Deckhaar steil nach oben gestellt",
          "small bun on top of the crown, the front hair standing steeply upward", 137016);
        S("Franse überm Auge", "fringe over the eye",
          "glattes Haar bis zum Kinn, eine schwere Seitenfranse fällt über ein Auge, die Spitzen laufen spitz aus",
          "smooth hair to the chin, a heavy side fringe falling over one eye, the ends tapering to points", 137018);
        S("kurz, struppig", "short, tousled",
          "kurzer, rundum struppiger Schnitt mit abstehenden Spitzen, im Nacken voll, die Stirn zum Teil bedeckt",
          "short cut, tousled all over with jutting points, full at the nape, the forehead partly covered", 137025);
        S("Strähne mit Feder", "strand with feather",
          "mittellang und glatt bis zum Kiefer, vor dem Ohr eine umwickelte Strähne mit Spange und Feder",
          "medium length and smooth to the jaw, a wrapped strand with clasp and feather in front of the ear", 137026);
        S("lang, Stirnkettchen", "long, forehead chain",
          "langes glattes Haar über die Schultern, mittig gescheitelt, ein feines Kettchen quer über der Stirn",
          "long straight hair over the shoulders, parted in the middle, a fine chain across the forehead", 137027);
        S("glatt, Stirnlocke", "smooth, forelock",
          "kurzer glatter Schnitt, die Franse bedeckt die Stirn, vorn kringelt sich eine Locke nach oben",
          "short smooth cut, the fringe covering the forehead, a lock curling upward at the front", 137034);
        S("kurz, spitz gestuft", "short, spiky layers",
          "kurzer Schnitt in spitzen Stufen, einzelne lange Zacken hängen über die Stirn, die Enden stehen ab",
          "short cut in spiky layers, single long points hanging over the forehead, the ends jutting out", 137036);
        S("Welle über der Stirn", "wave above the forehead",
          "Stirn frei, das Deckhaar in einer aufgestellten Welle nach hinten gelegt, Seiten glatt bis zum Kiefer",
          "forehead clear, the top hair laid back in a raised wave, sides smooth to the jaw", 137035);
        S("abstehende Spitzen", "flaring points",
          "das Haar ist zur Seite gelegt und steht über dem Ohr in langen federartigen Spitzen ab",
          "the hair is laid to one side and flares out over the ear in long feathery points", 137041);
        S("kurz, fedrig", "short, feathery",
          "kurzer, dicht am Kopf liegender Schnitt mit fedriger Kante, die Spitzen laufen vor dem Ohr aus",
          "short cut lying close to the head with a feathery edge, the points running out in front of the ear", 137042);
        S("Seitenscheitel, Franse", "side parting, fringe",
          "kurz, vom Seitenscheitel fällt die Franse schräg über die Stirn, die Seiten enden unter dem Ohr",
          "short, from a side parting the fringe falls diagonally over the forehead, the sides ending below the ear", 137043);
        S("Wangensträhnen", "cheek strands",
          "Haar glatt nach hinten gestrichen, Stirn frei, zwei lange dünne Strähnen hängen lose an der Wange",
          "hair combed smoothly back, forehead clear, two long thin strands hanging loose at the cheek", 137048);
        S("zurückgekämmt, glatt", "combed back, smooth",
          "glattes Haar vom Scheitel nach hinten gekämmt, die Seitenpartie fällt breit und fedrig bis zum Kiefer",
          "smooth hair combed back from the parting, the side section falling broad and feathery to the jaw", 137055);
        S("spitz auslaufende Seiten", "tapered sides",
          "glatte Strähnen vom Seitenscheitel über die Stirn, die Seiten laufen unter dem Kiefer spitz aus",
          "smooth strands from a side parting across the forehead, the sides tapering to points below the jaw", 137056);
        S("zerzaust, Hakenlocke", "tousled, hooked lock",
          "zerzaustes Haar mit viel Fülle, eine gebogene Locke hängt wie ein Haken über die Stirn",
          "tousled hair with plenty of volume, a curved lock hanging like a hook over the forehead", 137057);
        S("sehr kurz, dicht", "very short, dense",
          "sehr kurz geschnitten und dicht am Kopf anliegend, die Kante endet gerade über den Brauen",
          "cut very short and lying close to the head, the edge ending straight above the brows", 137058);
        S("nach vorn gebürstet", "brushed forward",
          "kurzes Haar nach vorn gebürstet, es bedeckt gleichmäßig die Stirn und reicht seitlich über das Ohr",
          "short hair brushed forward, evenly covering the forehead and reaching over the ear at the sides", 137063);
        S("gedrehte Strähne hinterm Ohr", "twisted strand behind the ear",
          "Haar bis zum Kiefer, die Franse fällt über ein Auge, hinter dem Ohr liegt eine gedrehte Strähne",
          "hair to the jaw, the fringe falling over one eye, a twisted strand lying behind the ear", 137064);
        S("runder Zottelschnitt", "rounded shaggy cut",
          "zottiger, rund geschnittener Kopf, die spitze Franse reicht bis in die Augen, die Seiten bis zum Kiefer",
          "shaggy, roundly cut shape, the pointed fringe reaching into the eyes, the sides down to the jaw", 137068);
        S("gerade Franse, Kieferlänge", "straight fringe, jaw length",
          "gerade abgeschnittene Franse über den Brauen, das Haar endet am Kiefer und schwingt leicht nach außen",
          "fringe cut straight above the brows, the hair ending at the jaw and flicking slightly outward", 137073);
        S("abstehende Seitenpartie", "flared side section",
          "zottiges Haar mit Franse, hinter dem Ohr steht die Seitenpartie in einem breiten Bogen ab",
          "shaggy hair with a fringe, behind the ear the side section flaring out in a broad arc", 137075);
        S("Stirnsträhnen", "forehead strands",
          "Deckhaar stachelig nach hinten gestrichen, mehrere dünne Strähnen hängen gerade über die Stirn",
          "top hair swept back and spiky, several thin strands hanging straight over the forehead", 137076);
        S("gezackte Franse", "jagged fringe",
          "die Franse ist in spitze Zacken geschnitten, seitlich fällt das Haar glatt bis unter den Kiefer",
          "the fringe is cut into sharp teeth, at the side the hair falls smoothly to below the jaw", 137077);
        S("Spitzen nach hinten", "points swept back",
          "das Deckhaar steht in Spitzen nach hinten, die Seiten fallen zottig bis zum Kiefer",
          "the top hair stands in points swept backward, the sides falling shaggy to the jaw", 137078);
        S("schräge Franse, gestuft", "diagonal fringe, layered",
          "gestuftes Haar, die Franse fällt schräg über die Brauen, die Seiten schwingen über dem Ohr nach hinten",
          "layered hair, the fringe falling diagonally over the brows, the sides sweeping back over the ear", 137085);
        S("hohe Tolle", "high pompadour",
          "das Deckhaar ist zu einer großen glatten Tolle hoch über die Stirn gerollt, die Seiten kürzer und gestuft",
          "the top hair rolled into a large smooth pompadour high above the forehead, the sides shorter and layered", 137086);
        S("lange Vorderlocke", "long front lock",
          "das Haar ist oben zu einem stacheligen Busch aufgenommen, vorn hängt eine lange Locke bis unter das Kinn",
          "the hair is gathered into a spiky burst on top, a long lock hanging down the front below the chin", 137087);
        S("Mittelscheitel, Kinnlänge", "centre parting, chin length",
          "am Scheitel gescheiteltes Haar, das beidseitig glatt am Gesicht entlang bis zum Kinn fällt",
          "hair parted at the crown, falling smoothly along both sides of the face down to the chin", 137088);
        S("zurückgestrichen, gestuft", "swept back, layered",
          "Stirn frei, das Haar in Stufen nach hinten gestrichen und im Nacken ausgestellt",
          "forehead clear, the hair swept back in layers and flaring at the nape", 137089);
        S("dichte Franse, hinters Ohr", "thick fringe, tucked behind the ear",
          "dichte gerade Franse über den Brauen, vorn glatt bis unter den Kiefer, hinten hinter das Ohr gelegt",
          "thick straight fringe above the brows, smooth to below the jaw in front, tucked behind the ear at the back", 137090);

        // Hrothgar, female - 27 entries. Same icon ids in both tribes, as above.
        // RE-AUTHORED for the same reason, and this is the block the
        // fault was found in: 137213 is a small twisted knot at the back with a broad
        // clasp, seen in profile, and was described as "short hair standing in short
        // points". Same base/+8 interleave: 137201/137209 ... 137208/137216.
        S("zurückgestrichen, lang", "swept back, long",
          "Deckhaar seitlich über den Kopf gestrichen, eine Seite fällt lang bis über die Schulter, Stirn frei",
          "top hair swept sideways across the head, one side falling long past the shoulder, forehead clear", 137201);
        S("lose Gesichtssträhne", "loose face strand",
          "Deckhaar seitlich über den Kopf gestrichen, lange Seite über der Schulter, eine lose Strähne fällt über die Wange",
          "top hair swept sideways across the head, long side over the shoulder, a loose strand falling across the cheek", 137209);
        S("Kronenflechte, Bob", "crown braid, bob",
          "zottiger Schnitt bis zum Kinn, eine geflochtene Strähne läuft wie ein Band über den Scheitel",
          "shaggy cut to the chin, a braided strand running like a band over the crown", 137202);
        S("Kronenflechte, lange Strähne", "crown braid, long strand",
          "zottiger Schnitt mit geflochtenem Band über dem Scheitel, eine Strähne reicht seitlich bis auf die Schulter",
          "shaggy cut with a braided band over the crown, one strand reaching down to the shoulder", 137210);
        S("stachelige Mähne", "spiky mane",
          "Haar aus der Stirn nach oben gekämmt, oben in Spitzen aufgestellt, lange gestufte Seiten bis zur Schulter",
          "hair combed up off the forehead, standing in points on top, long layered sides to the shoulder", 137203);
        S("Mähne, dünne Flechte", "mane, thin braid",
          "aufgestellte Mähne mit langen gestuften Seiten, vor dem Ohr eine dünne Flechte mit Ring am Ende",
          "upswept mane with long layered sides, a thin braid in front of the ear with a ring at its end", 137211);
        S("Bob, freie Stirn", "bob, clear forehead",
          "kinnlanger Schnitt aus tiefem Seitenscheitel über den Kopf gelegt, Stirn frei, Spitzen nach außen",
          "chin-length cut laid over the head from a deep side parting, forehead clear, ends flicking outward", 137204);
        S("Bob mit Franse", "bob with fringe",
          "kinnlanger Schnitt mit Seitenscheitel, eine dichte Franse fällt bis zu den Brauen, Spitzen nach außen",
          "chin-length cut with a side parting, a thick fringe falling to the brows, ends flicking outward", 137212);
        S("zackige Franse", "jagged fringe",
          "spitz ausgefranste Franse über der Stirn, glatte Strähnen fallen beidseitig bis zum Schlüsselbein",
          "pointed ragged fringe over the forehead, straight strands falling on both sides to the collarbone", 137205);
        S("kleiner Knoten hinten", "small knot at back",
          "vorn strähnig bis zum Kinn, hinten zu einem kleinen gedrehten Knoten mit breiter Spange gebunden",
          "stringy to the chin at the front, tied at the back into a small twisted knot with a broad clasp", 137213);
        S("volle wellige Mähne", "full wavy mane",
          "üppige Mähne, aus der Stirn zurückgebürstet, wellige Längen fallen bis auf die Schultern",
          "lush mane brushed back off the forehead, wavy lengths falling down to the shoulders", 137206);
        S("Mähne, Stirnlocke", "mane, forelock",
          "üppige zurückgebürstete Mähne, eine dicke Locke fällt vom Scheitel bis zwischen die Augen",
          "lush mane brushed back, a thick lock falling from the crown down between the eyes", 137214);
        S("aufgestellter Schopf", "raised crest",
          "Deckhaar nach oben und hinten aufgestellt, zottige gezackte Seiten reichen bis zum Kiefer",
          "top hair raised up and back, shaggy jagged sides reaching to the jaw", 137207);
        S("Flechten mit Spangen", "braids with clasps",
          "aufgestelltes Deckhaar, vorn beidseitig je eine Flechte mit breiter Metallspange und Quaste",
          "raised top hair, a braid at the front on each side with a broad metal clasp and tassel", 137215);
        S("hoher Rollenknoten", "high rolled knot",
          "Haar hoch am Hinterkopf zu einer Rolle gedreht, lange Locken fallen seitlich bis auf die Schulter",
          "hair twisted into a roll high at the back, long curls falling at the side to the shoulder", 137208);
        S("Rollenknoten, Schläfensträhne", "rolled knot, temple strand",
          "Haar hoch zu einer Rolle gedreht, lange Locken seitlich, eine Strähne fällt an der Schläfe über die Braue",
          "hair twisted high into a roll, long curls at the sides, a strand falling over the brow at the temple", 137216);
        S("kurz, zwei Strähnen", "short, two strands",
          "kurzes Haar aus der Stirn nach hinten gekämmt, zwei dünne Strähnen hängen frei über das Gesicht",
          "short hair combed back off the forehead, two thin strands hanging free across the face", 137217);
        S("lang mit Franse", "long with fringe",
          "sehr langes glattes Haar mit dichter Franse bis zu den Brauen, fällt vorn über beide Schultern",
          "very long straight hair with a thick fringe to the brows, falling forward over both shoulders", 137225);
        S("Zopf am Ohr", "ponytail at the ear",
          "gerade Franse bis zu den Brauen, kinnlange Seiten, seitlich am Ohr ein locker abstehender Zopf",
          "straight fringe to the brows, chin-length sides, a loosely tied ponytail standing out at the ear", 137232);
        S("Seitenscheitel, lang", "side parting, long",
          "tiefer Seitenscheitel, das Deckhaar fällt schräg über die Stirn, lange glatte Längen bis unter die Schultern",
          "deep side parting, the top hair falling diagonally across the forehead, long smooth lengths below the shoulders", 137233);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "dicke gedrehte Rastalocken, mehrere fallen nach vorn über das Gesicht, einzelne mit Metallringen",
          "thick twisted dreadlocks, several falling forward over the face, some banded with metal rings", 137234);
        S("glatt, Seitenzopf", "sleek, side ponytail",
          "glattes Haar mit Seitenscheitel, hinter dem Ohr gebunden, ein Büschel steht ab, der Zopf fällt gerade herab",
          "sleek hair with a side parting, tied behind the ear, a tuft sticking up, the ponytail hanging straight down", 137242);
        S("zwei Zöpfe", "two braids",
          "Franse über der Stirn, beidseitig je ein geflochtener Zopf, der vorn über die Schulter fällt",
          "fringe over the forehead, one plaited braid on each side falling forward over the shoulder", 137243);
        S("kurz, zerzaust", "short, tousled",
          "kurzer struppiger Schnitt, feine spitze Strähnen fallen über die Stirn und bis zum Kiefer",
          "short tousled cut, fine pointed strands falling over the forehead and down to the jaw", 137244);
        S("Franse, hoher Zopf", "fringe, high ponytail",
          "gerade Franse, spitz zulaufende Seiten am Kinn, hoch am Hinterkopf ein gerade fallender Zopf",
          "straight fringe, side pieces tapering to points at the chin, a ponytail high at the back falling straight down", 137254);
        S("schulterlang, wellig", "shoulder-length, wavy",
          "schulterlanges welliges Haar, eine lange Franse fällt schräg über eine Braue, Spitzen leicht geschwungen",
          "shoulder-length wavy hair, a long fringe falling diagonally over one brow, ends softly curved", 137255);
        S("nach vorn gekämmt", "combed forward",
          "kurzer Schnitt, das Deckhaar nach vorn über die Stirn gekämmt, die Seiten enden spitz am Kiefer",
          "short cut, the top hair combed forward over the forehead, the sides ending in points at the jaw", 137256);

        // Hyur, Midlander, female - 54 entries, exclusive to this row
        S("Bob, Seitenscheitel", "bob, side parting",
          "kinnlanger glatter Bob mit Seitenscheitel, das Deckhaar fällt schräg über die Stirn, Spitzen am Kiefer",
          "chin-length straight bob with a side parting, the top hair falling slantwise over the forehead, points at the jaw", 131203);
        S("Bob, freie Stirn", "bob, bare forehead",
          "kinnlanger Bob, das Haar aus der Stirn nach hinten gestrichen, eine lange dünne Strähne fällt an der Wange herab",
          "chin-length bob, the hair swept back off the forehead, one long thin strand falling down at the cheek", 131204);
        S("Flechte, Schopf", "braid, tuft",
          "das Haar hochgesteckt, eine Flechte über der Schläfe, kurzer gerader Pony, ein kleiner Schopf am Scheitel",
          "hair pinned up, a braid above the temple, short blunt fringe, a small tuft at the crown", 131205);
        S("Knoten im Nacken", "nape knot",
          "das Haar glatt aus dem Gesicht gestrichen und im Nacken zu einem tiefen Knoten gedreht, Ohren frei",
          "hair swept smoothly back off the face and twisted into a low knot at the nape, ears free", 131206);
        S("hohe Welle", "high sweep",
          "das Haar in einer hohen Welle nach hinten gestrichen, lange dünne Strähnen fallen an Schläfe und Ohr herab",
          "hair swept back in a high wave, long thin strands falling down at the temple and the ear", 131207);
        S("Nacken eingerollt", "nape rolled under",
          "kurzer Schnitt mit tiefem Seitenscheitel, das Haar hinter das Ohr gelegt und im Nacken nach innen eingerollt",
          "short cut with a deep side parting, the hair tucked behind the ear and rolled under at the nape", 131208);
        S("Nacken offen", "nape left loose",
          "tiefer Seitenscheitel, die Seiten glatt angelegt, die Länge fällt hinter dem Ohr offen bis auf die Schulter",
          "deep side parting, the sides laid flat, the length falling loose behind the ear down to the shoulder", 131209);
        S("lang, Seitenfranse", "long, side fringe",
          "langes glattes Haar mit schräger Franse über der Braue, hinter das Ohr gelegt, die Länge fällt über die Schulter",
          "long straight hair with a slanted fringe over the brow, tucked behind the ear, the length falling over the shoulder", 131210);
        S("spitzer Bob", "tapered bob",
          "glatter Bob mit Seitenscheitel, die Strähnen laufen unterhalb des Kiefers spitz aus, ein Ohr bleibt frei",
          "straight bob with a side parting, the strands tapering to points below the jaw, one ear left free", 131211);
        S("Spitzen nach außen", "flicked ends",
          "langes Haar mit Seitenscheitel und freier Stirn, die Spitzen schwingen an der Schulter nach außen",
          "long hair with a side parting and bare forehead, the ends swinging outward at the shoulder", 131212);
        S("Pony, kinnlang", "blunt fringe, chin-length",
          "gerade geschnittener Pony über den Brauen, kinnlanger Bob, die Spitzen leicht ausgefranst, Ohren bedeckt",
          "blunt fringe cut straight above the brows, chin-length bob, the ends slightly frayed, ears covered", 131213);
        S("Pony, Seitenzopf", "fringe, side ponytail",
          "gerader Pony, das übrige Haar seitlich hinter dem Ohr zu einem hohen Zopf gebunden, lange Strähnen am Gesicht",
          "blunt fringe, the rest tied into a high ponytail at the side behind the ear, long strands at the face", 131214);
        S("Flechten, Knoten oben", "cornrows, top knot",
          "die Seiten in engen Flechten nach oben gelegt, oben auf dem Kopf ein großer lockerer Knoten",
          "the sides laid upward in tight braids, a large loose knot on top of the head", 131215);
        S("Stirnband, dunkel glatt", "headband, dark smooth",
          "kurzer, zum Nacken hin ausgefranster Schnitt mit Pony, darüber ein breites glattes, sehr dunkles Stirnband",
          "short cut frayed toward the nape with a fringe, a broad smooth, very dark headband over it", 131201);
        S("Stoffband, grau", "cloth band, grey",
          "gleicher kurzer Schnitt mit Pony, darüber ein breites graues Stoffband mit sichtbarer Webung",
          "the same short cut with a fringe, a broad grey cloth band with visible weave over it", 131216);
        S("Stoffband, dunkelblau", "cloth band, dark blue",
          "gleicher kurzer Schnitt mit Pony, darüber ein breites dunkelblaues Stoffband mit sichtbarer Webung",
          "the same short cut with a fringe, a broad dark blue cloth band with visible weave over it", 131217);
        S("Stoffband, hell", "cloth band, pale",
          "gleicher kurzer Schnitt mit Pony, darüber ein breites, sehr helles Stoffband mit sichtbarer Webung",
          "the same short cut with a fringe, a broad, very pale cloth band with visible weave over it", 131218);
        S("Knoten, lange Strähnen", "knot, long strands",
          "das Haar hoch zu einem Knoten gebunden, eine Flechte an der Seite, lange dünne Strähnen fallen vorn herab",
          "hair tied high into a knot, a braid along the side, long thin strands falling down at the front", 131202);
        S("Knoten, Flechte", "knot, braid",
          "hoch gebundener Knoten mit einer Flechte an der Seite, das übrige Haar kurz im Nacken, Stirn frei",
          "knot tied high with a braid along the side, the rest kept short at the nape, forehead bare", 131219);
        S("Undercut, lange Mähne", "undercut, long mane",
          "eine Seite kurz geschoren, das lange Deckhaar auf die andere Seite gekämmt und über die Schulter fallend",
          "one side cropped short, the long top hair combed to the other side and falling over the shoulder", 131223);
        S("lang, weiche Franse", "long, soft fringe",
          "langes Haar mit weicher, gefiederter Franse über den Brauen, die Spitzen fallen leicht gewellt über die Schultern",
          "long hair with a soft feathered fringe over the brows, the ends falling in gentle waves over the shoulders", 131222);
        S("lang, Ohr frei", "long, ear free",
          "langes glattes Haar mit Seitenscheitel, hinter einem Ohr zurückgelegt, eine Strähne fällt an der Schläfe herab",
          "long straight hair with a side parting, tucked behind one ear, a strand falling down at the temple", 131224);
        S("Rastalocken, Ringe", "dreadlocks, rings",
          "dicke Rastalocken mit Metallringen, aus der Stirn zurückgelegt und beidseitig nach vorn über die Schultern fallend",
          "thick dreadlocks with metal rings, laid back off the forehead and falling forward over both shoulders", 131225);
        S("hoher Zopf, glatt", "high ponytail, sleek",
          "das Haar glatt nach hinten zu einem hohen Zopf gebunden, am Band aufgefächert, die Länge fällt gerade herab",
          "hair drawn sleekly back into a high ponytail, flared at the tie, the length falling straight down", 131226);
        S("Haarreif, lang", "hairband, long",
          "langes glattes Haar mit einem schmalen Haarreif über dem Scheitel, darunter eine gefiederte Franse",
          "long straight hair with a narrow hairband across the crown, a feathered fringe beneath it", 131227);
        S("kurz, gezackt", "short, jagged",
          "kurzer stufiger Schnitt, die Franse läuft in Spitzen über die Stirn, alle Enden gezackt",
          "short layered cut, the fringe running in points across the forehead, all the ends jagged", 131229);
        S("zwei Zöpfe", "two braids",
          "das Haar beidseitig zu Zöpfen geflochten, die vorn über die Schultern fallen, weiche Franse über der Stirn",
          "hair braided into a plait on each side, falling forward over the shoulders, soft fringe over the forehead", 131228);
        S("hoher Zopf, gewellt", "high ponytail, wavy",
          "hoch gebundener Zopf, der in dichten Wellen herabfällt, gerader Pony und lange Strähnen am Gesicht",
          "high-tied ponytail falling in thick waves, blunt fringe and long strands at the face", 131231);
        S("lange Wellen", "long waves",
          "langes gewelltes Haar, aus der Stirn zurückgenommen, die Wellen fallen offen über die Schultern",
          "long wavy hair taken back off the forehead, the waves falling loose over the shoulders", 131232);
        S("Wellen, lange Franse", "waves, long fringe",
          "langes gewelltes Haar mit einer langen Franse, die schräg über die Braue fällt",
          "long wavy hair with a long fringe falling slantwise over the brow", 131233);
        S("kurz, weich gestuft", "short, softly layered",
          "kurzer Schnitt, das Deckhaar weich zur Seite gelegt, die Enden um das Ohr herum gestuft",
          "short cut, the top hair laid softly to one side, the ends layered around the ear", 131234);
        S("Pagenkopf", "pageboy",
          "kurzer Pagenkopf, die Seiten hinter die Ohren gelegt, der Nacken rundum nach innen eingedreht",
          "short pageboy, the sides laid behind the ears, the nape curling under all round", 131235);
        S("nackenlang, gestuft", "neck length, layered",
          "nackenlanger gestufter Schnitt, seitlich gescheitelt und über die Ohren zurückgelegt, die Enden stehen ab",
          "neck-length layered cut, side-parted and laid back over the ears, the ends standing out", 131239);
        S("Bob, Spitzen abstehend", "bob, flared ends",
          "kurzer Bob, dessen Spitzen am Kiefer kräftig nach außen schwingen, dünne Franse über den Brauen",
          "short bob whose ends swing strongly outward at the jaw, a thin fringe over the brows", 131240);
        S("zottig, wild", "shaggy, wild",
          "wild zottiger Schnitt, lange spitze Strähnen fallen quer über die Stirn und bis über den Kiefer",
          "wildly shaggy cut, long pointed strands falling across the forehead and past the jaw", 131241);
        S("stachelig zurück", "spiky, swept back",
          "kurzes Haar stachelig nach hinten gebürstet, einzelne dünne Strähnen fallen über die Stirn",
          "short hair brushed back into spikes, single thin strands falling over the forehead", 131242);
        S("gedrehte Stränge", "twisted strands",
          "schwerer Pony bis zu den Augen, hinten dicke gedrehte Stränge, die beidseitig nach vorn über die Schultern fallen",
          "heavy fringe down to the eyes, thick twisted strands at the back falling forward over both shoulders", 131243);
        S("weicher Kurzbob", "soft short bob",
          "kurzer weicher Bob bis zum Kiefer mit feiner Franse, die Enden nur wenig ausgefranst, Ohren bedeckt",
          "short soft bob to the jaw with a fine fringe, the ends only slightly frayed, ears covered", 131244);
        S("Undercut, Flechte", "undercut, braid",
          "die Schläfe kurz ausrasiert, das Deckhaar nach hinten aufgestellt, hinten eine geflochtene Strähne mit einem Band gebunden",
          "the temple shaved short, the top hair raised backward, a braided strand at the back bound with a tie", 131245);
        S("hohe Tolle", "high pompadour",
          "eine hohe glatte Tolle wölbt sich über die Stirn, die Seiten kurz und spitz auslaufend",
          "a high smooth pompadour arching over the forehead, the sides short and tapering to points", 131246);
        S("Stufen, langer Nacken", "layers, long nape",
          "stufiger Schnitt mit aufgestelltem Scheitel, lange spitze Strähnen am Gesicht, im Nacken deutlich länger",
          "layered cut with the crown raised, long pointed strands at the face, distinctly longer at the nape", 131247);
        S("kurz, tiefer Scheitel", "short, deep parting",
          "kurzer Schnitt mit tiefem Scheitel, eine breite Strähne fällt über die Stirn, hinten kurz hinters Ohr gelegt",
          "short cut with a deep parting, a wide strand falling over the forehead, kept short and tucked behind the ear", 131249);
        S("kinnlang, glatt", "chin length, straight",
          "kinnlanger Schnitt mit Scheitel am Oberkopf, das Haar fällt glatt herab und läuft am Kiefer spitz aus",
          "chin-length cut parted at the top, the hair falling straight down and tapering to points at the jaw", 131250);
        S("Undercut, Nackenspitzen", "undercut, spiky nape",
          "die Seite kahl rasiert, das Deckhaar stachelig nach hinten, im Nacken lange spitze Strähnen",
          "the side shaved bare, the top hair spiky and swept back, long pointed strands at the nape", 131251);
        S("Pony, kleiner Zopf", "fringe, small ponytail",
          "gerader Pony, das Haar über den Ohren zurückgelegt und im Nacken zu einem kleinen Zopf gebunden",
          "blunt fringe, the hair laid back over the ears and tied into a small ponytail at the nape", 131253);
        S("breite Franse, rund", "wide fringe, rounded",
          "kinnlanger Schnitt mit Seitenscheitel, eine breite Franse fällt über eine Braue, die Enden ausgefranst und rund",
          "chin-length cut with a side parting, a wide fringe falling over one brow, the ends frayed and rounded", 131254);
        S("sehr kurz", "very short",
          "sehr kurz geschorener Schnitt, überall struppig aufgeraut, Ohren und Nacken bleiben frei",
          "very closely cropped cut, roughed up and tousled all over, ears and nape left free", 131257);
        S("Franse, Scheitelspitze", "fringe, crown spike",
          "kurzer Schnitt mit einer aufragenden Spitze am Scheitel, eine breite Franse fällt schräg über eine Braue",
          "short cut with a spike rising at the crown, a wide fringe falling slantwise over one brow", 131258);
        S("Undercut, Stachelzopf", "undercut, spiky ponytail",
          "die Seiten kurz geschoren, oben ein hoher stachelig aufgefächerter Zopf, eine lange Strähne fällt vor dem Ohr",
          "the sides cropped short, a high ponytail fanned out in spikes above, a long strand falling before the ear", 131259);
        S("halb hochgesteckt", "half pinned up",
          "das Deckhaar zu einem Knoten gebunden, die Länge bleibt offen und fällt hinten herab, lange Strähnen am Gesicht",
          "the upper hair tied into a knot, the length left loose falling down the back, long strands at the face", 131260);
        S("Bob, Ohren bedeckt", "bob, ears covered",
          "kurzer dichter Bob bis zum Kiefer, der die Ohren bedeckt, eine schwere Franse liegt schräg über der Braue",
          "short dense bob to the jaw covering the ears, a heavy fringe lying slantwise over the brow", 131261);
        S("lang, gerader Pony", "long, blunt fringe",
          "sehr glattes langes Haar mit gerade geschnittenem Pony, die Strähnen fallen schnurgerade über die Schultern",
          "very straight long hair with a bluntly cut fringe, the strands falling dead straight over the shoulders", 131262);
        S("struppig, tiefe Franse", "shaggy, low fringe",
          "struppiger Schnitt, das Haar fällt nach vorn, die Franse reicht bis über die Augen, Enden ausgefranst",
          "shaggy cut, the hair falling forward, the fringe reaching down over the eyes, the ends frayed", 131270);
        S("zurückgekämmt", "combed straight back",
          "das Haar eng aus der Stirn nach hinten gekämmt, dünne Strähnen fallen an den Schläfen, Nacken kurz",
          "hair combed tightly back off the forehead, thin strands falling at the temples, short at the nape", 131288);

        // Hyur, Highlander, male - 52 entries, exclusive to this row
        S("enge Flechten, Stirnband", "tight braids, headband",
          "eng am Kopf geflochtene Reihen laufen nach hinten, eine dünne Schnur liegt quer über der Stirn",
          "rows braided tight to the head running back, a thin cord lying across the forehead", 131502);
        S("glatt zurück, Schläfenzopf", "sleek back, temple braid",
          "Deckhaar voluminös nach hinten gestrichen, ein dünner Zopf hängt vor dem Ohr bis zum Kiefer",
          "top hair swept back with volume, a thin braid hanging in front of the ear to the jaw", 131503);
        S("Bürstenschnitt, rasiertes Muster", "buzz cut, shaved pattern",
          "rundum sehr kurz geschoren, über dem Ohr ein verästeltes Muster in die Stoppeln geschnitten",
          "shorn very short all over, a branching pattern cut into the stubble above the ear", 131504);
        S("kahl", "bald",
          "vollständig kahl geschorener Kopf, kein Haar an Stirn, Schläfen oder Nacken",
          "completely shaved head, no hair at the forehead, temples or nape", 131505);
        S("Halbglatze, zurückgestrichen", "receding, swept back",
          "Stirn und Scheitel kahl, das verbliebene Haar an Schläfe und Hinterkopf glatt nach hinten gestrichen",
          "forehead and crown bare, the remaining hair at temple and back combed smoothly back", 131506);
        S("zottig, ausgestellte Spitzen", "shaggy, flicked-out ends",
          "mittellanger Stufenschnitt, lange Strähnen fallen über die Augen, die Spitzen stehen im Nacken ab",
          "medium layered cut, long strands falling over the eyes, the ends flicking out at the nape", 131507);
        S("lang, glatt zurück", "long, slicked back",
          "glatt aus der Stirn nach hinten gestrichen, fällt lang und gerade bis auf die Schulter, Ohren frei",
          "combed smoothly back off the forehead, falling long and straight to the shoulder, ears clear", 131508);
        S("Flechtansatz, stachelig", "braided front, spiky",
          "am Ansatz feine geflochtene Reihen, dahinter federt das Haar in Stacheln nach hinten, Nacken kurz",
          "fine braided rows at the hairline, behind them the hair feathering back into spikes, short at the nape", 131509);
        S("lange Stacheln", "long spikes",
          "lange spitze Strähnen stehen weit nach hinten und oben ab, Stirn frei, Nacken kurz",
          "long pointed strands standing far out backwards and upwards, forehead clear, short at the nape", 131510);
        S("voluminös zurück", "voluminous, swept back",
          "volles Deckhaar in weichen Spitzen nach hinten gekämmt, Stirnspitze frei, endet kurz im Nacken",
          "full top hair combed back into soft points, the peak of the hairline clear, ending short at the nape", 131511);
        S("krause Seiten, Knoten", "tight-curled sides, knot",
          "Seiten kurz und eng gekräuselt, das glatte Deckhaar nach hinten zu einem kleinen Knoten gebunden",
          "sides short and tightly curled, the smooth top hair tied back into a small knot", 131512);
        S("kurz, nach vorn gebürstet", "short, brushed forward",
          "kurzer stufiger Schnitt, das Haar in Spitzen nach vorn über die Stirn gebürstet, Ohren bedeckt",
          "short layered cut, hair brushed forward over the forehead in points, ears covered", 131513);
        S("Flechtkamm, Schnur, Doppelzopf", "braided crest, cord, two braids",
          "Deckhaar in Flechten nach hinten, Schläfe frei, zwei geknotete Zöpfe und eine dünne Schnur vor dem Ohr",
          "top hair in braids swept back, temple bare, two knotted braids and a thin cord in front of the ear", 131501);
        S("Flechtkamm, Schnur, Einzelzopf", "braided crest, cord, one braid",
          "Deckhaar in Flechten nach hinten, Schläfe bedeckt, ein geknoteter Zopf und eine dünne Schnur vor dem Ohr",
          "top hair in braids swept back, temple covered, one knotted braid and a thin cord before the ear", 131514);
        S("Flechtkamm, Doppelzopf", "braided crest, two braids",
          "Deckhaar in Flechten nach hinten, Schläfe frei, zwei geknotete Zöpfe hängen vor dem Ohr herab",
          "top hair in braids swept back, temple bare, two knotted braids hanging down in front of the ear", 131515);
        S("Flechtkamm, Einzelzopf", "braided crest, one braid",
          "Deckhaar in Flechten nach hinten, Schläfe bedeckt, ein einzelner geknoteter Zopf hängt vor dem Ohr",
          "top hair in braids swept back, temple covered, a single knotted braid hanging in front of the ear", 131516);
        S("zottig, Spitzen nach hinten", "shaggy, tips swept back",
          "mittellang und zottig, die Spitzen stehen fedrig nach hinten ab, kurze Franse über der Braue",
          "medium and shaggy, the tips standing out feathered to the back, short fringe above the brow", 131520);
        S("geschoren, Knoten oben", "shaved, topknot",
          "Kopf ringsum kahl geschoren, nur am Scheitel ein kleiner hochgebundener Knoten",
          "head shaved bald all round, only a small tied knot at the crown", 131521);
        S("mittellang, Strähne steht ab", "medium, one strand upright",
          "mittellanger glatter Schnitt über den Ohren, eine einzelne Strähne steht am Scheitel senkrecht ab",
          "medium smooth cut over the ears, a single strand standing straight up at the crown", 131519);
        S("mittellang, hinters Ohr", "medium, tucked behind the ear",
          "mittellanges Haar hinter das Ohr gestrichen, die Spitzen stehen im Nacken ab, feine Strähne an der Wange",
          "medium hair tucked behind the ear, the ends flicking out at the nape, a fine strand at the cheek", 131522);
        S("lang, Stirnband", "long, headband",
          "langes glattes Haar bis über die Schultern, mittig gescheitelt, ein geflochtenes Band quer über der Stirn",
          "long straight hair past the shoulders, parted in the middle, a braided band across the forehead", 131523);
        S("kurz, aufgestellte Locke", "short, upturned flick",
          "kurzer Schnitt nach vorn gekämmt, über der Stirn steht eine Strähne aufgerollt hoch",
          "short cut combed forward, one strand curling up above the forehead", 131524);
        S("zottig, Wangensträhnen", "shaggy, strands at the cheek",
          "zottig gestufter Schnitt, spitze Strähnen fallen an der Wange herab, die Spitzen stehen im Nacken ab",
          "shaggy layered cut, pointed strands falling down at the cheek, the ends flicking out at the nape", 131525);
        S("zottig, Ohr frei", "shaggy, ear left clear",
          "zottig gestufter Schnitt mit gezackter Franse, das Haar streicht am Ohr vorbei und lässt es frei",
          "shaggy layered cut with a jagged fringe, the hair sweeping past the ear and leaving it clear", 131527);
        S("Seitenscheitel, glatte Welle", "side parting, smooth wave",
          "tiefer Seitenscheitel, das Haar in einer glatten Welle zur Seite und nach hinten gekämmt",
          "deep side parting, the hair combed in a smooth wave to the side and back", 131526);
        S("Stachelzopf, Spange", "spiky tail, hair clip",
          "Seiten glatt zurück, das Deckhaar hoch zu einem stacheligen Zopf gebunden, kleine Spange an der Schläfe",
          "sides smoothly back, the top hair tied high into a spiky tail, a small clip at the temple", 131529);
        S("kurz, flach nach vorn", "short, laid flat forward",
          "kurzer Schnitt, das Haar flach nach vorn gelegt, die Franse endet in gezackten Spitzen",
          "short cut, the hair laid flat forward, the fringe ending in jagged points", 131530);
        S("kurz, stachelig", "short, spiky",
          "kurzer Schnitt, das Haar steht überall in kleinen Stacheln ab, Ohren frei",
          "short cut, the hair standing up in small spikes all over, ears clear", 131531);
        S("gescheitelt, ohrlang", "parted, ear length",
          "kurzer gescheitelter Schnitt, das Haar reicht seitlich knapp bis zum Ohr, Nacken kurz",
          "short parted cut, the hair reaching just to the ear at the sides, short at the nape", 131532);
        S("gescheitelt, kinnlang", "parted, jaw length",
          "gescheiteltes Haar über die Ohren gelegt, die Spitzen stehen am Kiefer leicht ab",
          "parted hair laid over the ears, the ends flicking out slightly at the jaw", 131533);
        S("gescheitelt, nackenlang", "parted, nape length",
          "gescheiteltes Haar, glatt über die Ohren bis in den Nacken fallend, dort in weichen Spitzen endend",
          "parted hair falling smoothly over the ears down to the nape, ending there in soft points", 131536);
        S("Franse, Seiten nach hinten", "fringe, sides flared back",
          "gerade Franse über den Brauen, die Seiten schwingen nach hinten aus und geben das Ohr frei",
          "straight fringe above the brows, the sides flaring back and leaving the ear clear", 131537);
        S("zerzaust, Strähnen im Gesicht", "tousled, strands across the face",
          "zerzauster Schnitt, am Scheitel nach hinten gewirbelt, lange spitze Strähnen fallen quer ins Gesicht",
          "tousled cut, swirled back at the crown, long pointed strands falling across the face", 131538);
        S("zurück, lose Stirnsträhnen", "swept back, loose forehead strands",
          "Haar glatt nach hinten gestrichen, mehrere dünne Strähnen fallen lose bis auf die Brauen",
          "hair combed smoothly back, several thin strands falling loose down to the brows", 131539);
        S("Franse, umwickelter Zopf", "fringe, wrapped ponytail",
          "gerade Franse über den Brauen, das hintere Haar zu einem umwickelten Zopf gefasst",
          "straight fringe above the brows, the back hair gathered into a wrapped ponytail", 131540);
        S("Franse, glatte Seiten", "fringe, straight sides",
          "gerade Franse über den Brauen, die Seiten fallen glatt bis zum Kiefer, Ohren bedeckt",
          "straight fringe above the brows, the sides falling smoothly to the jaw, ears covered", 131541);
        S("zurück, Nackenzopf", "swept back, nape braid",
          "Deckhaar nach hinten gestrichen mit einzelnen Spitzen, im Nacken ein kleiner gedrehter Zopf",
          "top hair swept back with a few spikes, a small twisted braid at the nape", 131542);
        S("hohe Tolle", "high pompadour",
          "sehr hohe voluminöse Tolle über der Stirn, die Seiten glatt nach hinten gestrichen",
          "a very high voluminous pompadour above the forehead, the sides combed smoothly back", 131543);
        S("zottig, Spitze am Scheitel", "shaggy, spike at the crown",
          "zottig gestufter Schnitt bis zum Kiefer, am Scheitel ragt eine einzelne lange Spitze auf",
          "shaggy layered cut down to the jaw, a single long spike rising at the crown", 131544);
        S("glatt, kleiner Knoten", "sleek, small knot",
          "glattes Deckhaar seitlich über die Stirn gelegt, am Hinterkopf ein kleiner gedrehter Knoten",
          "smooth top hair laid sideways over the forehead, a small twisted knot at the back of the head", 131546);
        S("gescheitelt, glatter Fall", "parted, straight fall",
          "gescheiteltes Haar fällt glatt bis zum Kiefer, einzelne Strähnen hängen über Schläfe und Braue",
          "parted hair falling straight to the jaw, single strands hanging over temple and brow", 131547);
        S("Undercut, langer Nacken", "undercut, long at the nape",
          "Seiten kurz geschoren, das Deckhaar stachelig nach hinten, hinten fällt langes Haar in den Nacken",
          "sides shorn short, the top hair spiky to the back, long hair falling down at the nape", 131548);
        S("Franse, glatter Zopf", "fringe, smooth ponytail",
          "gerade Franse über den Brauen, das hintere Haar zu einem glatten Zopf im Nacken gebunden",
          "straight fringe above the brows, the back hair tied into a smooth ponytail at the nape", 131550);
        S("gescheitelt, Franse überm Auge", "parted, fringe over the eye",
          "gescheiteltes Haar, die lange Franse fällt schräg über ein Auge, Spitzen gestuft am Kiefer",
          "parted hair, the long fringe falling slanted over one eye, layered ends at the jaw", 131551);
        S("sehr kurz, struppig", "very short, bristly",
          "sehr kurzer Schnitt, dicht und struppig nach hinten gebürstet, Ohren und Nacken frei",
          "very short cut, dense and bristly brushed back, ears and nape clear", 131554);
        S("seitlich gekämmt, Locke oben", "combed sideways, curl on top",
          "das Deckhaar in einer breiten Strähne seitlich über die Stirn gelegt, am Scheitel ringelt sich eine Spitze hoch",
          "the top hair laid in a wide sweep sideways over the forehead, a tip curling up at the crown", 131555);
        S("hoher Zopf, Gesichtsträhnen", "high ponytail, face strands",
          "Haar straff nach hinten und hoch am Scheitel zu einem stacheligen Zopf gebunden, zwei lange Strähnen fallen vorn herab",
          "hair pulled tight and tied high at the crown into a spiky ponytail, two long strands falling forward", 131556);
        S("hoher Knoten, offene Seiten", "high bun, loose sides",
          "das Haar hoch am Hinterkopf zu einem runden Knoten gedreht, lange Strähnen fallen seitlich offen herab",
          "the hair twisted into a round bun high at the back, long strands falling loose at the sides", 131557);
        S("weicher Pilzkopf", "soft bowl cut",
          "weich gerundeter Schnitt, die Strähnen fallen vom Scheitel bis über die Braue, Nacken kurz ausgedünnt",
          "softly rounded cut, the strands falling from the crown down over the brow, thinned short at the nape", 131558);
        S("Franse, sehr lange Strähnen", "fringe, very long strands",
          "gerade Franse über den Brauen, an beiden Seiten fallen sehr lange glatte Strähnen bis auf die Brust",
          "straight fringe above the brows, very long straight strands falling to the chest on both sides", 131559);
        S("zottig, dichte Franse", "shaggy, heavy fringe",
          "dichter zottiger Schnitt, die Franse fällt schwer über die Augen, die Spitzen enden am Kiefer",
          "dense shaggy cut, the fringe falling heavy over the eyes, the ends finishing at the jaw", 131567);
        S("wellig zurück, zwei Strähnen", "wavy back, two strands",
          "welliges Haar nach hinten gestrichen, zwei lange Strähnen hängen von der Schläfe bis zur Wange",
          "wavy hair combed back, two long strands hanging from the temple down to the cheek", 131584);

        // Hyur, Highlander, female - 48 entries, exclusive to this row
        S("zurückgestrichen, wellig", "swept back, wavy",
          "schulterlang, aus der Stirn nach hinten gestrichen, Spitzen im Nacken nach außen gewellt, dünne Strähnen vor dem Ohr",
          "shoulder-length, swept back off the forehead, ends waving outward at the nape, thin strands in front of the ear", 131701);
        S("zurückgekämmt, Flechte", "combed back, braid",
          "das Haar glatt nach hinten gekämmt, eine einzelne dünne Flechte fällt vor dem Ohr bis unter den Kiefer",
          "hair combed smoothly back, a single thin braid falling in front of the ear to below the jaw", 131702);
        S("Flechtband, Fächerzopf", "braided fan tail",
          "Haar hochgenommen, ein Flechtband um den Hinterkopf, oben ein kurzer stacheliger Fächerzopf, eine Strähne an der Schläfe",
          "hair up, a braid around the back, a short spiky fan tail on top, one strand at the temple", 131703);
        S("lang, Mittelscheitel", "long, centre parting",
          "langes glattes Haar mit Mittelscheitel, fällt beidseitig am Gesicht vorbei über die Schultern, Spitzen leicht ausgestellt",
          "long straight hair with a centre parting, falling past the face on both sides over the shoulders, ends slightly flicked", 131704);
        S("kleiner Nackenknoten", "small nape knot",
          "Haar nach hinten gestrichen, tief im Nacken zu einem kleinen gedrehten Knoten gebunden, lange Strähne vor dem Ohr",
          "hair swept back, tied low at the nape into a small twisted knot, one long strand at the ear", 131705);
        S("struppiger Pixie", "choppy pixie",
          "sehr kurzer Pixie, oben struppig ausgedünnt, Seiten kurz, kurze zackige Strähnen auf der Stirn",
          "very short pixie, choppy and thinned on top, short sides, short jagged strands on the forehead", 131706);
        S("Bob, gerader Pony", "bob, blunt fringe",
          "kinnlanger Bob mit gerade geschnittenem Pony über den Brauen, die Spitzen leicht nach außen gedreht",
          "chin-length bob with a blunt fringe cut above the brows, the ends turning slightly outward", 131707);
        S("lang, Seitenfranse", "long, side fringe",
          "langes glattes Haar, eine breite Seitenfranse fällt schräg über die Braue, die Längen reichen weit über die Schultern",
          "long sleek hair, a wide side fringe falling across the brow, the lengths reaching well past the shoulders", 131708);
        S("zottig, Ohren frei", "shaggy, ears free",
          "kurzer zottiger Schnitt bis zum Kiefer, Strähnen fallen schräg über die Stirn, die Ohren bleiben frei",
          "short shaggy cut to the jaw, strands falling diagonally over the forehead, the ears left free", 131709);
        S("geteilte Franse", "split fringe",
          "kurzes Haar, die Franse teilt sich über der Stirn, an den Seiten laufen spitze Strähnen bis zum Kiefer",
          "short hair, the fringe splitting over the forehead, pointed strands running down to the jaw at the sides", 131710);
        S("hoch zurückgekämmt", "combed high back",
          "das Haar hoch aus der Stirn nach hinten gekämmt, eine spitze Strähne schwingt vor dem Ohr zur Wange herunter",
          "hair combed high back off the forehead, a pointed strand curving down in front of the ear to the cheek", 131711);
        S("sehr kurz, glatt", "very short, sleek",
          "sehr kurzer glatter Schnitt, das Deckhaar aus einem Seitenscheitel über die Stirn gelegt, Seiten dicht am Kopf",
          "very short sleek cut, the top laid over the forehead from a side parting, sides close to the head", 131712);
        S("Seitenzopf, Pony", "side ponytail, fringe",
          "gerader Pony über den Brauen, das Haar hinter dem Ohr zu einem Zopf gebunden, Strähne an der Wange",
          "blunt fringe above the brows, the hair tied into a ponytail behind the ear, a strand at the cheek", 131716);
        S("Undercut, langer Fall", "undercut, long sweep",
          "eine Kopfseite kurz geschoren, das lange Deckhaar zur anderen Seite über die Schulter gelegt, spitze Stirnsträhnen",
          "one side cropped short, the long top hair laid over the other side past the shoulder, pointed forehead strands", 131717);
        S("lang gewellt, Pony", "long wavy, fringe",
          "langes leicht gewelltes Haar mit weichem Pony über den Brauen, die Längen fallen über beide Schultern",
          "long, softly waved hair with a soft fringe over the brows, the lengths falling over both shoulders", 131715);
        S("tiefer Seitenscheitel", "deep side parting",
          "langes Haar mit tiefem Seitenscheitel, über die Stirn geführt und hinter das Ohr gestrichen, Längen bis unter die Schultern",
          "long hair with a deep side parting, taken across the forehead and tucked behind the ear, lengths below the shoulders", 131718);
        S("Rastalocken, Ringe", "dreadlocks, rings",
          "dicke gedrehte Rastalocken über den ganzen Kopf, mit Metallringen besetzt, fallen vorn beidseitig über die Schultern",
          "thick twisted dreadlocks over the whole head, set with metal rings, falling forward on both sides over the shoulders", 131719);
        S("glatt zurück, Zopf", "sleek back, ponytail",
          "das Haar glatt nach hinten gestrichen und hoch am Hinterkopf gebunden, der Zopf fällt gerade bis zur Schulter",
          "hair swept smoothly back and tied high at the back of the head, the ponytail falling straight to the shoulder", 131720);
        S("Haarband", "hairband",
          "ein glattes Haarband liegt quer über dem Scheitel, darunter fällt das lange Haar herab, feine Strähnen auf der Stirn",
          "a smooth hairband lies across the crown, the long hair falling below it, fine strands on the forehead", 131721);
        S("kurz, gezackt", "short, jagged",
          "kurzer stark gestufter Schnitt, gezackte Franse über der Stirn, seitlich spitz zulaufende Strähnen am Kiefer",
          "short heavily layered cut, a jagged fringe over the forehead, strands tapering to points at the jaw", 131723);
        S("Pony, zwei Flechten", "fringe, two braids",
          "gerader Pony über den Brauen, an beiden Seiten je eine dünne Flechte, die nach vorn über die Schulter fällt",
          "blunt fringe above the brows, a thin braid on each side falling forward over the shoulder", 131722);
        S("hoher Zopf, Pony", "high ponytail, fringe",
          "gerader Pony über den Brauen, das Haar hoch am Scheitel zu einem Pferdeschwanz gebunden, lange Strähnen rahmen das Gesicht",
          "blunt fringe above the brows, the hair tied into a ponytail high at the crown, long strands framing the face", 131725);
        S("lang, gewellt", "long, wavy",
          "langes gewelltes Haar ohne Pony, am Scheitel gescheitelt, fällt in weichen Wellen über beide Schultern",
          "long wavy hair with no fringe, parted at the crown, falling in soft waves over both shoulders", 131726);
        S("gewellt, Seitenfranse", "wavy, side fringe",
          "langes gewelltes Haar, eine Seitenfranse legt sich über die Braue, die Wellen fallen weit über die Schultern",
          "long wavy hair, a side fringe laid across the brow, the waves falling well past the shoulders", 131727);
        S("kurz, nach vorn", "short, brushed forward",
          "kurzer Schnitt, das Deckhaar nach vorn und zur Seite über die Stirn gebürstet, Spitzen auf Ohrhöhe",
          "short cut, the top brushed forward and to the side across the forehead, ends at ear height", 131728);
        S("hinters Ohr gestrichen", "tucked behind ear",
          "kinnlanger Schnitt, vom Scheitel hinter das Ohr gestrichen, Spitzen im Nacken ausgefranst, eine Strähne an der Schläfe",
          "chin-length cut, swept from the crown behind the ear, frayed ends at the nape, one strand at the temple", 131729);
        S("gestuft, zurückgelegt", "layered, swept back",
          "mittellanger zottiger Schnitt, aus der Stirn nach hinten gelegt, gestufte Strähnen fallen über Ohr und Nacken",
          "mid-length shaggy cut, laid back off the forehead, layered strands falling over the ear and nape", 131733);
        S("abstehende Spitzen", "flicked-out ends",
          "kinnlanger Schnitt mit voller Franse über den Brauen, die Spitzen stehen ringsum kräftig nach außen ab",
          "chin-length cut with a full fringe over the brows, the ends flicking strongly outward all around", 131734);
        S("voluminös zerzaust", "voluminous, tousled",
          "voluminöser zerzauster Schnitt, Strähnen stehen nach allen Seiten ab, lange Spitzen fallen über Stirn und Wange",
          "voluminous tousled cut, strands sticking out in all directions, long points falling over the forehead and cheek", 131735);
        S("nach hinten gestachelt", "spiked back",
          "das Haar nach hinten hochgestachelt, Seiten und Nacken kurz und struppig, nur einzelne dünne Fäden hängen über die Stirn",
          "hair spiked up and back, sides and nape short and shaggy, only a few thin threads hanging over the forehead", 131736);
        S("gedrehte Seitensträhnen", "twisted side locks",
          "eine breite Franse fällt über das Auge, beidseitig hängen dicke gedrehte Strähnen nach vorn über die Schultern",
          "a wide fringe falls over one eye, thick twisted locks hanging forward over the shoulders on both sides", 131737);
        S("kinnlang, feine Franse", "chin-length, wispy fringe",
          "kinnlanger Schnitt mit feiner Franse über den Brauen, die Spitzen im Nacken leicht ausgestellt",
          "chin-length cut with a wispy fringe over the brows, the ends at the nape slightly flicked", 131738);
        S("hochgekämmt, Nackenflechte", "nape braid",
          "Seiten und Deckhaar straff nach hinten gekämmt, im Nacken eine schmale Flechte, spitze Strähnen an der Stirn",
          "sides and top combed tightly back, a narrow braid at the nape, pointed strands at the forehead", 131739);
        S("große Tolle", "large pompadour",
          "das Haar zu einer großen glatten Tolle nach oben und hinten gerollt, Seiten nach hinten gestrichen, Strähne an der Schläfe",
          "hair rolled up and back into a large smooth pompadour, sides swept back, a strand at the temple", 131740);
        S("Spitze am Scheitel", "crown spike",
          "kurzer Schnitt mit einer einzelnen hohen Spitze am Scheitel, seitlich fallen lange spitze Strähnen bis unter den Kiefer",
          "short cut with a single tall spike at the crown, long pointed strands falling below the jaw at the sides", 131741);
        S("kurz, gekräuselter Nacken", "short, ruffled nape",
          "kurzer Schnitt, glatte Franse schräg über die Stirn gelegt, am Hinterkopf und im Nacken gekräuselte Spitzen",
          "short cut, a smooth fringe laid diagonally across the forehead, ruffled ends at the back and nape", 131743);
        S("kinnlang, glatte Spitzen", "chin-length, smooth ends",
          "kinnlanger Schnitt mit glatt geschnittenen Spitzen, eine Strähne hakt über die Braue, hinten etwas länger",
          "chin-length cut with smoothly cut ends, one strand hooking over the brow, a little longer at the back", 131744);
        S("rasierte Seite, Mähne", "shaved side, mane",
          "eine Kopfseite bis auf Stoppeln geschoren, oben ein aufgerichteter Kamm, im Nacken fällt eine lange struppige Mähne herab",
          "one side shaved to stubble, a raised crest on top, a long shaggy mane falling down the nape", 131745);
        S("tiefer Zopf, Pony", "low ponytail, fringe",
          "gerader Pony über den Brauen, das Haar tief im Nacken zu einem kurzen Zopf gebunden, Strähnen rahmen das Gesicht",
          "blunt fringe above the brows, the hair tied into a short ponytail low at the nape, strands framing the face", 131747);
        S("kinnlang, ausgefranst", "chin-length, frayed",
          "kinnlanger Schnitt mit stark ausgefransten Spitzen, eine spitze Strähne schwingt über die Wange, Nacken zackig",
          "chin-length cut with heavily frayed ends, a pointed strand swinging over the cheek, jagged at the nape", 131748);
        S("sehr kurz, kraus", "very short, crinkled",
          "sehr kurz geschnitten, das Deckhaar dicht und kraus stehend, Seiten und Nacken eng am Kopf, Stirn frei",
          "cut very short, the top standing dense and crinkled, sides and nape close to the head, forehead clear", 131751);
        S("Franse überm Auge", "fringe over eye",
          "kurzer Schnitt, eine lange Franse fällt über ein Auge, am Scheitel steht eine Strähne ab, Seiten spitz",
          "short cut, a long fringe falling over one eye, a strand sticking up at the crown, pointed sides", 131752);
        S("Stachelzopf, Sichelsträhne", "spiky tail, sidelock",
          "das Haar straff hochgenommen und am Scheitel zu einem stacheligen Zopf gebunden, eine lange Strähne schwingt vor das Gesicht",
          "hair pulled up tightly into a spiky tail at the crown, one long strand curving in front of the face", 131753);
        S("hoher Knoten", "high bun",
          "das Haar hoch am Hinterkopf zu einem kleinen Knoten gedreht, Seitenfranse über der Braue, lange Strähnen hängen herab",
          "hair twisted into a small bun high at the back, a side fringe over the brow, long strands hanging down", 131754);
        S("runder Bob, Franse", "round bob, fringe",
          "runder kinnlanger Bob mit voller Franse über den Brauen, die Spitzen zum Kiefer hin nach innen gedreht",
          "round chin-length bob with a full fringe over the brows, the ends turning inward toward the jaw", 131755);
        S("sehr lang, Pony", "very long, fringe",
          "sehr langes glattes Haar mit gerade geschnittenem vollem Pony, die Längen fallen vorn beidseitig über die Brust",
          "very long straight hair with a full blunt fringe, the lengths falling forward on both sides over the chest", 131756);
        S("zottiger Topfschnitt", "shaggy bowl cut",
          "zottiger Topfschnitt, das Haar hängt ringsum gerade herab, bedeckt die Stirn bis zu den Brauen und die Ohren",
          "shaggy bowl cut, the hair hanging straight down all around, covering the forehead to the brows and the ears", 131764);
        S("zurück, struppiger Nacken", "swept back, shaggy",
          "das Haar aus der Stirn nach hinten gestrichen, Oberfläche grob gedreht, im Nacken struppige Spitzen, dünne Stirnsträhne",
          "hair swept back off the forehead, the surface roughly twisted, shaggy ends at the nape, a thin forehead strand", 131782);

        // Elezen, male - 48 entries. Wildwood and Duskwight share the icon set
        S("mittellang, gescheitelt", "medium, parted",
          "mittellanges glattes Haar, seitlich gescheitelt, die Stirn bleibt frei, die Längen fallen bis unter den Kiefer",
          "medium-length straight hair, parted at the side, the forehead left clear, lengths falling below the jaw", 132001);
        S("zottig, tiefe Franse", "shaggy, low fringe",
          "zottig gestufter Schnitt, spitze Strähnen hängen über Brauen und Augen, die Seiten reichen bis zum Kiefer",
          "shaggy layered cut, pointed strands hanging over the brows and eyes, the sides reaching down to the jaw", 132002);
        S("Knoten mit Band", "knot with ribbon",
          "hoch am Hinterkopf zu einem Knoten mit Band gebunden, zwei dünne Strähnen fallen an der Schläfe herab",
          "gathered high at the back into a knot tied with a ribbon, two thin strands falling at the temple", 132003);
        S("Nackenzopf", "low ponytail",
          "streng nach hinten gekämmt, im Nacken mit einem Band zu einem schmalen Zopf gebunden",
          "combed straight back off the forehead, tied with a band at the nape into a narrow ponytail", 132004);
        S("zurückgestrichen, offene Spitzen", "swept back, loose ends",
          "glatt nach hinten gestrichen, die Stirn frei, im Nacken stellen sich die Spitzen locker nach außen",
          "smoothly swept back with the forehead clear, the ends flaring loosely outward at the nape", 132005);
        S("hohe Stachelmähne", "tall spiked mane",
          "das ganze Haar steil nach oben und hinten gebürstet, lange Stacheln ragen hoch über den Scheitel",
          "all the hair brushed steeply up and back, long spikes rising high above the crown", 132006);
        S("schräge Franse", "slanted fringe",
          "kurz gestufter zottiger Schnitt, eine breite Franse fällt schräg über die Stirn, die Spitzen zerzaust am Ohr",
          "short shaggy layered cut, a broad fringe falling diagonally across the forehead, tousled ends around the ear", 132007);
        S("kurz zurückgekämmt", "short, combed back",
          "kurzes Haar glatt nach hinten gekämmt, Stirn und Schläfen frei, im Nacken kurz gehalten",
          "short hair combed smoothly back, forehead and temples clear, kept short at the nape", 132008);
        S("stachelig zurückgeworfen", "spiky, thrown back",
          "das Deckhaar breit nach hinten geworfen und stachelig aufgefächert, eine dünne Strähne fällt vor dem Ohr herab",
          "the top hair thrown back in a broad spiky fan, a thin strand falling down in front of the ear", 132009);
        S("voluminös zurückgekämmt", "voluminous, combed back",
          "voluminös nach hinten gekämmt, glatt anliegend, die Längen enden hinter dem Ohr im Nacken",
          "combed back with volume, lying smooth, the lengths ending behind the ear at the nape", 132010);
        S("nach vorn gebürstet", "brushed forward",
          "rundlicher Schnitt, das Deckhaar nach vorn gebürstet, gezackte Franse bis zu den Brauen, kurzer Nacken",
          "rounded cut with the top brushed forward, a jagged fringe down to the brows, short at the nape", 132011);
        S("kurz, fein stachelig", "short, fine spikes",
          "kurzes Haar in vielen feinen Stacheln nach hinten gebürstet, gezackter Haaransatz, kurze Spitzen im Nacken",
          "short hair brushed back into many fine spikes, jagged hairline, short points at the nape", 132012);
        S("voluminös zerzaust", "voluminously tousled",
          "volles zerzaustes Haar wie vom Wind nach hinten getrieben, eine Strähne fällt über das Auge, Spitzen am Kiefer",
          "full tousled hair driven back as if by wind, a strand falling over the eye, points at the jaw", 132016);
        S("geschoren, Knoten", "shaved, topknot",
          "der Kopf ist bis auf einen schmalen Mittelstreifen geschoren, dieser am Scheitel zu einem kleinen Knoten gebunden",
          "the head shaved bare but for a narrow central strip, tied into a small knot at the crown", 132017);
        S("lang, dünne Flechte", "long, thin braid",
          "langes glattes Haar mit Seitenscheitel, hinter dem Ohr hängt eine dünne Flechte, die Längen fallen über die Schulter",
          "long straight hair with a side parting, a thin braid hanging behind the ear, lengths falling over the shoulder", 132018);
        S("lang gestuft", "long, layered",
          "lang gestuftes Haar, eine lange Seitenfranse fällt über die Stirn, am Scheitel steht ein kleiner Schopf ab",
          "long layered hair, a long side fringe falling over the forehead, a small tuft standing up at the crown", 132015);
        S("lang, Stirnband", "long, headband",
          "sehr langes Haar mit Mittelscheitel, ein schmales geflochtenes Stirnband liegt quer über der Stirn",
          "very long hair with a centre parting, a narrow braided headband lying across the forehead", 132019);
        S("aufgestellte Stirnlocke", "upturned front curl",
          "weicher runder Schnitt, die Franse liegt auf der Stirn, vorn am Scheitel steht eine Strähne geschwungen auf",
          "soft rounded cut, the fringe lying on the forehead, a curled strand standing up at the front of the crown", 132020);
        S("lange Wangensträhne", "long cheek strand",
          "gestufter Schnitt, eine lange spitze Strähne fällt vor dem Ohr über die Wange bis zum Kiefer",
          "layered cut, a long pointed strand falling in front of the ear over the cheek to the jaw", 132021);
        S("spitze Franse", "spiky fringe",
          "kurz gestuft, die Franse teilt sich in spitze Strähnen über den Brauen, gezackte Spitzen im Nacken",
          "short and layered, the fringe splitting into pointed strands over the brows, jagged points at the nape", 132023);
        S("Seitenscheitel, Tolle", "side part, quiff",
          "tiefer Seitenscheitel, das Deckhaar glatt nach oben und hinten zu einer Tolle gelegt, Seiten eng anliegend",
          "deep side parting, the top laid smoothly up and back into a quiff, the sides lying close", 132022);
        S("Undercut, Federschopf", "undercut, fanned crest",
          "Seiten kurz geschoren, das Deckhaar zu einem hohen gefächerten Schopf gelegt, eine kleine Spange über dem Ohr",
          "sides cropped short, the top swept into a tall fanned crest, a small clasp sitting above the ear", 132025);
        S("sehr kurz, anliegend", "very short, close-cropped",
          "sehr kurzer Schnitt, dicht am Kopf anliegend, gezackter Haaransatz an der Stirn, Ohren und Nacken frei",
          "very short cut lying close to the head, jagged hairline at the forehead, ears and nape clear", 132026);
        S("kurz, struppig", "short, tousled",
          "kurzer struppiger Schnitt, das Deckhaar steht in kurzen Büscheln ab, gezackter Ansatz, einzelne Spitzen im Nacken",
          "short tousled cut, the top standing up in short tufts, jagged hairline, single points at the nape", 132027);
        S("Seitenscheitel, kurz", "side part, short",
          "kurzes Haar mit Seitenscheitel, seitlich glatt übergelegt, die Stirn frei, die Spitzen laufen vor dem Ohr aus",
          "short hair with a side parting laid smoothly across, forehead clear, the points ending in front of the ear", 132028);
        S("Seitenscheitel, kinnlang", "side part, chin-length",
          "Seitenscheitel, die Strähnen fallen an der Schläfe bis zum Kiefer, der Nacken bleibt zottig und ausgefranst",
          "side parting with strands falling at the temple down to the jaw, the nape left shaggy and frayed", 132029);
        S("Mittelscheitel, halslang", "centre part, neck-length",
          "Mittelscheitel, das glatte Haar fällt beidseitig am Gesicht vorbei bis in den Nacken, die Spitzen ausgefranst",
          "centre parting, the straight hair falling past the face on both sides to the neck, the ends frayed", 132032);
        S("voll, gezackte Spitzen", "full, jagged ends",
          "volles gestuftes Haar bis zum Kiefer, dichte Franse über den Brauen, hinten stehen gezackte Spitzen ab",
          "full layered hair to the jaw, a thick fringe over the brows, jagged points flaring at the back", 132033);
        S("asymmetrisch, abstehend", "asymmetric, jutting",
          "asymmetrisch, das Deckhaar steht seitlich stachelig ab, eine dicke Strähne fällt schräg über Stirn und Auge",
          "asymmetric, the top jutting out spikily at one side, a thick strand falling diagonally over forehead and eye", 132034);
        S("stachelig, Stirnsträhnen", "spiky, forehead strands",
          "das Haar stachelig nach hinten gebürstet, einzelne dünne Strähnen hängen lose über die Stirn",
          "hair brushed back into spikes, a few thin strands hanging loose over the forehead", 132035);
        S("hinten aufgerollt", "rolled up at the back",
          "glatte Franse bis zu den Brauen, das Hinterhaar ist zu einem dicken geriffelten Strang aufgerollt",
          "a smooth fringe to the brows, the back hair rolled up into a thick ridged strand", 132036);
        S("kinnlang, ausgestellt", "chin-length, flicked out",
          "kinnlanges Haar mit spitzer Franse, die Spitzen stellen sich am Kiefer nach außen, kleiner Schopf am Scheitel",
          "chin-length hair with a pointed fringe, the ends flicking outward at the jaw, a small tuft at the crown", 132037);
        S("Flechte mit Band", "braid with a band",
          "das Deckhaar stachelig nach hinten gelegt, hinter dem Ohr hängt eine Flechte, am Ende mit einem Band gebunden",
          "the top swept back in spikes, a braid hanging behind the ear, tied with a band near its end", 132038);
        S("riesige Tolle", "huge pompadour",
          "das Haar zu einer riesigen glatten Tolle gelegt, die weit über die Stirn ragt, die Seiten nach hinten",
          "hair swept up into a huge smooth pompadour jutting far out over the forehead, the sides swept back", 132039);
        S("gestuft, aufragende Spitze", "layered, jutting spike",
          "gestufter Schnitt, vorn ragt eine lange Spitze auf, Strähnen fallen beidseitig über die Schläfen bis zum Kiefer",
          "layered cut with a long spike rising at the front, strands falling over both temples to the jaw", 132040);
        S("glatt übergekämmt", "smoothly combed over",
          "das glatte Deckhaar seitlich über die Stirn gekämmt und bis zur Wange gelegt, hinten kurz gehalten",
          "the sleek top combed sideways across the forehead down to the cheek, kept short at the back", 132042);
        S("glatt, gesichtsumrahmend", "sleek, face-framing",
          "glattes Haar, das vom Scheitel nach vorn fällt und in langen Strähnen Stirn und Wangen umrahmt",
          "straight hair falling forward from the parting, framing forehead and cheeks in long strands", 132043);
        S("Undercut, Nackenmähne", "undercut, long nape",
          "die Seiten kurz rasiert, das Deckhaar stachelig nach hinten gelegt, das Nackenhaar bleibt lang und zottig",
          "the sides shaved short, the top swept back in spikes, the nape hair left long and shaggy", 132044);
        S("glatter Pagenschnitt", "sleek pageboy",
          "glatter Pagenschnitt, gerade geschnittene Franse über den Brauen, die Längen fallen glatt bis unter den Kiefer",
          "sleek pageboy cut, a straight-cut fringe over the brows, the lengths falling smoothly below the jaw", 132046);
        S("Strähne übers Auge", "strand over the eye",
          "Seitenscheitel, eine lange spitze Strähne fällt schräg über das Auge, die Spitzen gestuft am Kiefer",
          "side parting, a long pointed strand falling diagonally over the eye, layered ends at the jaw", 132047);
        S("kurze Locken", "short curls",
          "sehr kurzes, dicht gekräuseltes Haar liegt wie eine Kappe eng am Kopf, gezackter Ansatz an der Stirn",
          "very short tightly curled hair lying close to the head like a cap, jagged hairline at the forehead", 132050);
        S("Seitenfranse, Wirbelspitze", "side fringe, crown flick",
          "eine schwere Seitenfranse fällt über Stirn und Auge, am Wirbel steht eine Strähne ab, hinten spitz auslaufend",
          "a heavy side fringe falling over forehead and eye, a strand flicking up at the crown, pointed at the back", 132051);
        S("hoher Stachelzopf", "high spiky ponytail",
          "das Haar straff nach oben zu einem hohen stacheligen Zopf gebunden, zwei lange Strähnen fallen an den Schläfen herab",
          "hair pulled tightly up into a high spiky ponytail, two long strands falling down at the temples", 132052);
        S("Knoten, lange Seitensträhnen", "bun, long side strands",
          "das Haar hoch am Hinterkopf zu einem kleinen runden Knoten gedreht, lange Strähnen fallen seitlich am Gesicht herab",
          "hair twisted high at the back into a small round bun, long strands falling down beside the face", 132053);
        S("runder Bob", "round bob",
          "runder glatter Bob, die Franse fällt bis zu den Augen, die Spitzen biegen sich am Kiefer nach innen",
          "round sleek bob, the fringe falling to the eyes, the ends curving inward at the jaw", 132054);
        S("lang, gerader Pony", "long, blunt fringe",
          "sehr langes glattes Haar mit gerade geschnittener Franse über den Brauen, die Längen fallen vorn über die Schultern",
          "very long straight hair with a blunt-cut fringe over the brows, the lengths falling forward over the shoulders", 132055);
        S("strähnige Franse", "strandy fringe",
          "kurzer zottiger Schnitt, eine dichte strähnige Franse bedeckt die Stirn, dünne Spitzen laufen im Nacken aus",
          "short shaggy cut, a dense strandy fringe covering the forehead, thin points trailing at the nape", 132063);
        S("wellig zurückgestrichen", "wavy, swept back",
          "das Haar wellig nach hinten gestrichen, die Stirn frei, zwei dünne Strähnen hängen an der Schläfe bis zur Wange",
          "hair swept back in waves with the forehead clear, two thin strands hanging at the temple to the cheek", 132080);

        // Elezen, female - 52 entries. Wildwood and Duskwight share the icon set
        S("Bob, eingerollte Spitzen", "bob, ends curled in",
          "glatter kinnlanger Bob mit tiefem Seitenscheitel, die Spitzen rollen sich am Nacken nach innen",
          "sleek chin-length bob with a deep side parting, the ends curling inward at the nape", 132201);
        S("Scheitelflechte", "crown braid",
          "streng nach hinten gestrichen, eine schmale Flechte über dem Scheitel, dünne Strähne vor dem Ohr",
          "swept back tightly, a narrow braid over the crown, a thin strand in front of the ear", 132202);
        S("Franse, kleiner Nackenzopf", "fringe, small nape ponytail",
          "eine Franse und lange Gesichtssträhnen, das übrige Haar zu einem kleinen Zopf im Nacken gebunden",
          "fringe and long face-framing strands, the rest tied into a small flicked-out ponytail at the nape", 132204);
        S("Deckhaar zurückgerollt", "top rolled back",
          "das Deckhaar aus der Stirn nach hinten gerollt und am Scheitel befestigt, die Seiten fallen kinnlang",
          "the top hair rolled back off the forehead and fastened at the crown, sides falling to the chin", 132205);
        S("schulterlang, ausgefranst", "shoulder-length, ragged",
          "glattes schulterlanges Haar mit Scheitel, lange Strähnen vorn, die Spitzen stark ausgefranst",
          "straight shoulder-length hair with a parting, long strands in front, heavily ragged tips", 132206);
        S("sehr kurz, anliegend", "very short, close",
          "sehr kurzer Schnitt, vom Seitenscheitel glatt über den Kopf gelegt, Nacken und Ohren frei",
          "very short cut, laid sleekly across from a side parting, nape and ears left free", 132208);
        S("Seitenflechte, tiefer Zopf", "side braid, low ponytail",
          "gerade Franse, eine Flechte über dem Ohr, der Rest im Nacken mit breitem Band zum Zopf gebunden",
          "straight fringe, a braid above the ear, the rest tied at the nape with a wide band into a ponytail", 132209);
        S("raspelkurz", "buzz cut",
          "rundum raspelkurz geschorenes Haar, keine Franse, Ohren und Nacken völlig frei",
          "hair clipped to a close buzz all over, no fringe at all, ears and nape completely free", 132210);
        S("Flechtkranz, lange Zöpfchen", "braided crown, thin braids",
          "mehrere enge Flechten über den Kopf nach hinten, davor hängen lange dünne Zöpfchen mit kleinen Bändern",
          "several tight braids running back over the head, long thin braids hanging in front with small ties", 132211);
        S("Bob, volle Franse", "bob, full fringe",
          "kinnlanger glatter Bob mit voller gerader Franse bis zu den Brauen, die Spitzen stumpf geschnitten",
          "chin-length sleek bob with a full straight fringe to the brows, the ends cut blunt", 132212);
        S("Hochsteckrolle, graue Nadeln", "rolled updo, grey pins",
          "das Haar hinten zu einer Rolle hochgesteckt und mit mehreren geraden grauen Nadeln fixiert, vorn lose Strähnen",
          "hair pinned up into a roll at the back, held by several straight grey pins, loose strands in front", 132203);
        S("Rolle, hellgelbe Nadeln", "updo, pale yellow pins",
          "dieselbe hochgesteckte Rolle, die geraden Nadeln sind hell und blass gelb",
          "the same pinned-up roll, the straight pins pale and washed-out yellow", 132213);
        S("Rolle, olivfarbene Nadeln", "updo, olive pins",
          "dieselbe hochgesteckte Rolle, die geraden Nadeln sind dunkel olivfarben",
          "the same pinned-up roll, the straight pins a dark olive shade", 132214);
        S("Rolle, bernsteinfarbene Nadeln", "updo, amber pins",
          "dieselbe hochgesteckte Rolle, die geraden Nadeln sind hell bernsteinfarben",
          "the same pinned-up roll, the straight pins a pale amber shade", 132215);
        S("glatt, Seitenfranse", "straight, side fringe",
          "langes glattes Haar, die Seiten nach hinten gestrichen, eine Franse fällt schräg über eine Braue",
          "long straight hair, the sides swept back, a fringe falling slantwise over one brow", 132207);
        S("zurückgestrichen, gezackter Ansatz", "swept back, jagged hairline",
          "langes Haar streng nach hinten gestrichen, über der Stirn stehen kurze gezackte Spitzen am Ansatz",
          "long hair combed straight back, short jagged points standing along the hairline above the forehead", 132216);
        S("hoher Seitenzopf, Zierband", "high side ponytail, ornament",
          "gerade Franse, das Haar seitlich hoch zum langen Zopf gebunden, am Binder sitzt eine kleine Zierspange",
          "straight fringe, the hair tied high at the side into a long ponytail, a small ornament on the tie", 132220);
        S("voluminöse Mähne", "voluminous mane",
          "voluminöse Mähne weit nach hinten gestrichen, spitz zulaufender Ansatz, über dem Ohr kurz, seitlich lange Wellen",
          "voluminous mane swept far back, a pointed hairline, short above the ear, long waves at the side", 132221);
        S("lang, ohne Franse", "long, no fringe",
          "langes glattes Haar mit Scheitel, ohne Franse, fällt schwer über Rücken und Schultern",
          "long straight hair with a parting, no fringe, falling heavily over back and shoulders", 132222);
        S("Wellen, volle Franse", "waves, full fringe",
          "langes gewelltes Haar mit voller weicher Franse, die Spitzen fallen in breiten Wellen über die Schultern",
          "long wavy hair with a full soft fringe, the ends falling in broad waves over the shoulders", 132219);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "dicke Rastalocken nach hinten gelegt, einzelne Locken fallen vorn übers Gesicht und tragen Metallringe",
          "thick dreadlocks laid back, single locks falling forward over the face and carrying metal rings", 132223);
        S("hoher glatter Zopf", "high sleek ponytail",
          "das Deckhaar glatt zur Seite gelegt, hinten hoch gebunden, der lange Zopf fällt glatt herab",
          "the top hair laid smoothly to one side, tied high at the back, the long ponytail hanging straight down", 132224);
        S("Haarreif", "headband",
          "langes glattes Haar mit weicher Franse, über dem Scheitel liegt ein breiter flacher Haarreif",
          "long straight hair with a soft fringe, a wide flat headband sitting over the crown", 132225);
        S("kurz, zottig", "short, shaggy",
          "kurzer zottiger Schnitt, spitze Strähnen fallen über die Stirn, die Seiten reichen bis über die Ohren",
          "short shaggy cut, pointed strands falling over the forehead, the sides reaching down past the ears", 132227);
        S("zwei Zöpfe vorn", "two braids in front",
          "eine Franse über den Brauen, das Haar zu zwei Flechten gebunden, die vorn über beide Schultern fallen",
          "fringe over the brows, the hair worked into two braids that fall forward over both shoulders", 132226);
        S("hoher Zopf, Franse", "high ponytail, fringe",
          "gerade Franse und lange Gesichtssträhnen, das übrige Haar hoch am Hinterkopf zum langen Zopf gebunden",
          "straight fringe and long face-framing strands, the rest tied high at the back into a long ponytail", 132229);
        S("Wellen, Stirn frei", "waves, open forehead",
          "langes gewelltes Haar mit Seitenscheitel, die Stirn bleibt frei, die Wellen fallen vorn über beide Schultern",
          "long wavy hair with a side parting, the forehead left free, the waves falling forward over both shoulders", 132230);
        S("Wellen, Stirnfranse", "waves, brow fringe",
          "langes gewelltes Haar, eine lange Franse verdeckt die Stirn bis über eine Braue, die Spitzen wellen sich",
          "long wavy hair, a long fringe covering the forehead down over one brow, the ends waving", 132231);
        S("Pixie, Ohren frei", "pixie, ears free",
          "kurzer Schnitt, vom Scheitel nach hinten gestrichen, die Spitzen laufen fein am Nacken aus",
          "short cut, brushed back from the parting, the ends running out finely at the nape", 132232);
        S("Strähne überm Ohr", "strand over the ear",
          "kurzer Schnitt mit Seitenscheitel, eine glatte Strähne legt sich über das Ohr, im Nacken zerfranste Spitzen",
          "short side-parted cut, a smooth strand laid over the ear, frayed points at the nape", 132233);
        S("Mittelscheitel, feine Spitzen", "middle parting, fine ends",
          "kinnlanges Haar mit Mittelscheitel, die Stirn frei, die Spitzen laufen fein und stufig aus",
          "chin-length hair with a middle parting, the forehead free, the ends running out fine and layered", 132237);
        S("Bob, abstehende Spitzen", "bob, flicked-out ends",
          "kurzer Bob mit ausgefranster Franse, die Spitzen stehen rundum nach außen ab",
          "short bob with a choppy fringe, the ends flicking outward all round", 132238);
        S("zerzaust, asymmetrisch", "tousled, asymmetric",
          "voluminös zerzaustes Kurzhaar, lange Spitzen fallen schräg über die Stirn, hinten ausgedünnte Strähnen",
          "voluminous tousled short hair, long points falling slantwise over the forehead, thinned strands at the back", 132239);
        S("stachelig, nach hinten", "spiky, brushed back",
          "kurzes stacheliges Haar nach oben und hinten gebürstet, einzelne dünne Strähnen hängen über die Stirn",
          "short spiky hair brushed up and back, a few thin strands hanging over the forehead", 132240);
        S("gedrehte Stränge", "twisted ropes",
          "volle Franse, das seitliche Haar zu dicken gedrehten Strängen gelegt, die vorn über beide Schultern fallen",
          "full fringe, the side hair worked into thick twisted ropes falling forward over both shoulders", 132241);
        S("zottiger Bob", "shaggy bob",
          "kinnlanger zottiger Bob mit feiner Franse, die Spitzen dünn ausgefranst, im Nacken etwas länger",
          "chin-length shaggy bob with a fine fringe, thinly frayed ends, a little longer at the nape", 132242);
        S("Tolle, Nackenflechte", "quiff, nape braid",
          "das Deckhaar zur Tolle nach hinten aufgekämmt, die Seiten kurz, im Nacken eine Flechte mit breitem Band",
          "the top combed up into a quiff, the sides short, a braid at the nape held by a broad band", 132243);
        S("riesige Tolle", "huge pompadour",
          "das Haar zu einer sehr großen glatten Tolle über der Stirn aufgetürmt, Seiten und Nacken kurz",
          "the hair piled into a very large smooth pompadour above the forehead, sides and nape short", 132244);
        S("spitze Strähnen", "pointed strands",
          "zottiger Schnitt mit langen spitzen Strähnen über Stirn und Schläfen, im Nacken federig verlängert",
          "shaggy cut with long pointed strands over forehead and temples, feathered and longer at the nape", 132245);
        S("kurz, Seitenscheitel", "short, side parting",
          "kurzer Schnitt mit tiefem Seitenscheitel, das Deckhaar glatt zur Seite gelegt, Nacken kurz geschnitten",
          "short cut with a deep side parting, the top laid smoothly to one side, the nape cut short", 132247);
        S("lange Franse, gestuft", "long fringe, layered",
          "gestufter Schnitt mit tiefem Scheitel, eine lange Franse fällt über eine Braue, die Spitzen laufen am Kiefer aus",
          "layered cut with a deep parting, a long fringe falling over one brow, the ends running out at the jaw", 132248);
        S("Undercut, langer Nacken", "undercut, long nape",
          "eine Seite kurz geschoren, das Deckhaar stachelig nach hinten gekämmt und im Nacken lang und federig",
          "one side shaved close, the top combed back in spikes and left long and feathered at the nape", 132249);
        S("hinters Ohr gelegt", "tucked behind ear",
          "glattes Haar mit weicher Franse, an den Seiten hinter das Ohr gelegt, im Nacken länger als vorn",
          "sleek hair with a soft fringe, tucked behind the ear at the sides, longer at the nape than in front", 132251);
        S("ausgefranster Bob", "frayed bob",
          "stark gestufter kinnlanger Bob, eine spitze Franse fällt über ein Auge, die Spitzen stark ausgefranst",
          "heavily layered chin-length bob, a pointed fringe falling over one eye, the ends strongly frayed", 132252);
        S("sehr kurz, federig", "very short, feathery",
          "rundum sehr kurz geschnittenes Haar mit dichter federiger Struktur, Ohren und Nacken frei",
          "very short cut all over with a dense feathery texture, ears and nape left free", 132255);
        S("Schopf am Wirbel", "tuft at the crown",
          "lange Seitenfranse über einem Auge, das Deckhaar nach hinten gelegt und am Wirbel zu Spitzen aufgestellt",
          "long side fringe over one eye, the top laid back and standing up in points at the crown", 132256);
        S("hoher Federzopf", "high fanned ponytail",
          "streng aus der Stirn hochgebunden, der Zopf fächert spitz auf, zwei lange Schläfensträhnen fallen herab",
          "pulled up tightly off the forehead, the ponytail fanning out in points, two long temple strands hanging down", 132257);
        S("hoher Knoten", "high bun",
          "das Haar hoch am Hinterkopf zu einem kleinen runden Knoten gedreht, vorn Seitenfranse und lange lose Strähnen",
          "the hair twisted into a small round bun high at the back, a side fringe and loose strands in front", 132258);
        S("ohrlanger Bob", "ear-length bob",
          "kurzer ohrlanger Bob, die Franse läuft in feinen Spitzen aus, im Nacken kurz zulaufend",
          "short ear-length bob, the fringe running out in fine points, tapering short at the nape", 132259);
        S("lange Vordersträhnen", "long front strands",
          "gerade Franse und kinnlanges Deckhaar, davor fallen zwei sehr lange glatte Strähnen bis über die Brust",
          "straight fringe and chin-length hair, with two very long straight strands falling past the chest", 132260);
        S("Franse übers Auge", "fringe over the eye",
          "kurzer federiger Schnitt, die lange Franse fällt geschlossen über die Stirn bis unter ein Auge",
          "short feathery cut, the long fringe falling in a closed sweep over the forehead below one eye", 132268);
        S("zurückgestrichen, lose Strähnen", "swept back, loose strands",
          "kurzes Haar glatt nach hinten gestrichen, einzelne lange Strähnen lösen sich und fallen über die Stirn",
          "short hair swept smoothly back, single long strands coming loose and falling over the forehead", 132286);

        // Lalafell, male - 49 entries. Plainsfolk and Dunesfolk share the icon set
        S("runder Topfschnitt", "rounded bowl cut",
          "kurzer runder Topfschnitt, kurze Franse auf der Stirn, die Spitzen biegen sich an den Seiten nach außen",
          "short rounded bowl cut, short fringe on the forehead, the ends curling outward at the sides", 133001);
        S("Wirbel am Scheitel", "cowlick on top",
          "kurzer struppiger Schnitt mit spitzer Franse, eine einzelne Strähne steht am Scheitel aufrecht ab",
          "short tousled cut with a pointed fringe, a single strand standing upright at the crown", 133002);
        S("kleiner Knoten oben", "small topknot",
          "das Haar nach oben gestrichen und oben hinten zu einem kleinen Knoten gebunden, eine dünne Strähne fällt seitlich herab",
          "hair swept upward and tied into a small bun high at the back, a thin strand falling at the side", 133003);
        S("glatt, lange Seitensträhnen", "sleek, long strands",
          "glattes kurzes Deckhaar, lange gerade Strähnen fallen seitlich über die Wangen bis unters Kinn",
          "sleek short top hair, long straight strands falling at the sides over the cheeks below the chin", 133004);
        S("Schopf, nach vorn", "crest, brushed forward",
          "kurzes Haar nach vorn gebürstet, am vorderen Scheitel ein aufgestellter Schopf, spitze Strähnen über der Stirn",
          "short hair brushed forward, a raised crest at the front of the crown, pointed strands over the forehead", 133005);
        S("glatt, Stirn frei", "straight, bare forehead",
          "in der Mitte gescheitelt, das glatte Haar fällt beidseitig am Gesicht vorbei bis unters Kinn, die Stirn bleibt frei",
          "centre parting, the straight hair falling past the face on both sides below the chin, the forehead left bare", 133006);
        S("Bürstenschnitt", "buzz cut",
          "rundum kurz geschorenes Haar, dicht am Kopf, hoher Haaransatz, Stirn und Nacken frei",
          "hair shorn short all around, close to the head, high hairline, forehead and nape bare", 133007);
        S("am Scheitel gebunden", "tied on top",
          "das ganze Haar am Scheitel zu einem kleinen Büschel gebunden, der Rest fällt lose rundum bis über die Ohren",
          "all the hair tied into a small tuft at the crown, the rest falling loose all around past the ears", 133008);
        S("Flechtreihen, stachelig", "braided rows, spiky",
          "geflochtene Reihen über den Scheitel, die hinten in eine stachelige, nach hinten gestrichene Masse auslaufen",
          "braided rows across the crown running into a spiky swept-back mass at the back", 133009);
        S("zottig, deckt Ohren", "shaggy, covers ears",
          "mittellanger zottiger Schnitt, deckt die Ohren, dünne Strähnen über der Stirn, ausgefranste Spitzen am Nacken",
          "medium shaggy cut covering the ears, thin strands over the forehead, frayed ends at the nape", 133010);
        S("Cornrows, Stirnband", "cornrows, headband",
          "eng am Kopf nach hinten geflochtene Cornrows, ein schmales Band verläuft quer über die Stirn",
          "cornrows braided tight to the head running back, a narrow band crossing the forehead", 133011);
        S("nach hinten gestrichen", "swept back",
          "das Haar voll nach hinten gestrichen, die Stirn frei, das Deckhaar steht als breite Mähne ab",
          "hair swept fully back, the forehead bare, the top standing out as a broad mane", 133012);
        S("hohe Stacheln", "tall spikes",
          "hoch aufragende, nach hinten gerichtete Stacheln, die Seiten kurz, eine dünne Strähne am Nacken",
          "tall spikes rising and pointing backward, short sides, a thin strand at the nape", 133013);
        S("zerzauste Mähne", "tousled mane",
          "dichte zerzauste Mähne, nach hinten und außen abstehend, ausgefranste Spitzen an Ohr und Nacken",
          "thick tousled mane standing out back and outward, frayed ends at the ear and nape", 133017);
        S("kahl, Schopf", "shaved, topknot",
          "der Kopf kahl geschoren bis auf ein schmales Feld am Scheitel, dort zu einem kurzen Schopf gebunden",
          "the head shaved bald except a narrow patch at the crown, tied there into a short tuft", 133018);
        S("schulterlang, seitlich", "shoulder length, side-swept",
          "das Haar seitlich gelegt und bis zur Schulter lang, eine einzelne Strähne ragt am Scheitel auf",
          "hair laid to one side and long to the shoulder, a single strand rising at the crown", 133016);
        S("lang, wellig", "long, wavy",
          "langes welliges Haar mit Scheitel, fällt beidseitig über die Schultern, dünne Strähnen am Gesicht",
          "long wavy hair with a parting, falling over the shoulders on both sides, thin strands at the face", 133019);
        S("Stirnband, lang", "headband, long",
          "langes welliges Haar mit Mittelscheitel, ein geflochtenes Band liegt quer über der Stirn",
          "long wavy hair with a centre parting, a braided band lying across the forehead", 133020);
        S("runde Kappe, Locke", "round cap, curl",
          "kurze runde Kappe, dicht und weich, vorn am Haaransatz biegt sich eine Locke nach vorn auf",
          "short round cap, thick and soft, a curl flicking up and forward at the front hairline", 133021);
        S("spitze Strähnen", "pointed strands",
          "gestufter Schnitt, lange spitze Strähnen hängen beidseitig bis ans Kinn, das Deckhaar zerzaust",
          "layered cut, long pointed strands hanging to the chin on both sides, the top tousled", 133022);
        S("Pilzschnitt, spitze Franse", "mushroom, pointed fringe",
          "dichter Pilzschnitt, die schwere Franse reicht spitz bis an die Brauen, die Seiten bis ans Kinn",
          "dense mushroom cut, the heavy fringe reaching the brows in points, the sides down to the chin", 133024);
        S("Tolle, kurze Seiten", "pompadour, short sides",
          "das Deckhaar hoch zu einer Tolle aufgerollt und nach hinten gelegt, die Seiten kurz geschoren",
          "the top rolled up into a high pompadour and laid back, the sides cropped short", 133023);
        S("Fächerzopf hinten", "fanned tail",
          "die Seiten kurz, das Deckhaar hinten hoch zusammengefasst und fächerförmig gespreizt, eine dünne Strähne am Ohr",
          "short sides, the top gathered high at the back and spread into a fan, a thin strand at the ear", 133026);
        S("kurz, nach vorn", "short, brushed forward",
          "kurzer Schnitt dicht am Kopf, nach vorn gekämmt, gezackter Haaransatz, die Ohren frei",
          "short cut close to the head, combed forward, jagged hairline, the ears free", 133027);
        S("kurz, struppig", "short, tousled",
          "kurzer struppiger Schnitt, das Deckhaar steht büschelig ab, gezackte Spitzen an Stirn und Schläfe",
          "short tousled cut, the top standing up in tufts, jagged points at the forehead and temple", 133028);
        S("kurz, Seitenfranse", "short, side fringe",
          "kurzer glatter Schnitt, die Franse zur Seite gestrichen, die Seiten laufen vor dem Ohr spitz aus",
          "short smooth cut, the fringe swept to one side, the sides tapering to a point in front of the ear", 133029);
        S("Scheitel, deckt Schläfen", "parting, covers temples",
          "deutlicher Scheitel, das glatte Haar fällt über Schläfen und Ohr bis unters Kinn, ausgefranste Spitzen am Nacken",
          "clear parting, the straight hair falling over the temples and ear below the chin, frayed ends at the nape", 133030);
        S("Scheitel, nach hinten", "parting, swept back",
          "gescheitelt am Oberkopf, das Haar nach hinten gestrichen, Stirn und Ohren frei, die Spitzen biegen sich am Nacken ab",
          "parting on the crown, the hair swept back, forehead and ears free, the ends flicking out at the nape", 133033);
        S("Pagenschnitt, volle Franse", "pageboy, full fringe",
          "kinnlanger Pagenschnitt, volle Franse mit spitzen Enden über den Brauen, die Spitzen stehen hinten ab",
          "pageboy down to the chin, full fringe with pointed ends over the brows, the ends flaring at the back", 133034);
        S("voluminös seitlich", "voluminous side sweep",
          "voluminöses stacheliges Haar stark zur Seite gelegt, lange spitze Strähnen fallen über Schläfe und Wange bis ans Kinn",
          "voluminous spiky hair swept hard to one side, long pointed strands falling over temple and cheek to the chin", 133035);
        S("nach hinten, Stirnsträhnen", "swept back, forehead strands",
          "das Haar stachelig nach hinten gebürstet, einzelne dünne Strähnen hängen über die freie Stirn",
          "hair brushed back in spikes, single thin strands hanging over the bare forehead", 133036);
        S("dünne Flechte seitlich", "thin side braid",
          "glatte Frisur mit schwerer Franse über den Augen, hinter dem Ohr hängt eine dünne Flechte lang herab",
          "smooth style with a heavy fringe over the eyes, a thin braid hanging down long behind the ear", 133037);
        S("dünne Fransensträhnen", "thin fringe wisps",
          "weicher runder Schnitt bis ans Kinn, dünne einzelne Strähnen auf der Stirn, am Scheitel steht ein kleines Härchen ab",
          "soft round cut to the chin, thin single strands on the forehead, a small hair standing up at the crown", 133038);
        S("Undercut, Deckhaar gelegt", "undercut, top swept over",
          "die Seiten kurz und gezackt, das lange Deckhaar in einer Welle darüber gelegt, eine Strähne fällt vor dem Ohr herab",
          "sides short and jagged, the long top laid over them in a wave, a strand falling past the ear", 133039);
        S("riesige Welle", "huge swept wave",
          "eine mächtige Haarwelle, weit über den Kopf hinaus nach hinten geschwungen, die Schläfen kurz",
          "a massive wave of hair sweeping far back beyond the head, the temples short", 133040);
        S("lange Franse, Stacheln", "long fringe, spikes",
          "seitlich gescheitelt, die lange Franse fällt schräg über Stirn und Wange, am Scheitel stehen einzelne Spitzen auf",
          "side parting, the long fringe falling diagonally over forehead and cheek, single spikes rising at the crown", 133041);
        S("gescheitelt, hinten ausgefranst", "parted, frayed back",
          "das Haar seitlich gescheitelt und glatt über die Stirn gelegt, der Hinterkopf voll und ausgefranst, Spitzen am Kiefer",
          "side parting, the hair laid smoothly over the forehead, the back full and frayed, points at the jaw", 133043);
        S("Mittelscheitel, spitze Enden", "centre parting, pointed ends",
          "mittig gescheitelt, ein freier Keil auf der Stirn, glatte Strähnen fallen beidseitig spitz bis unters Kinn",
          "parted in the middle, a bare wedge on the forehead, straight strands falling in points below the chin", 133044);
        S("Irokese, rasierte Seiten", "mohawk, shaved sides",
          "ein stacheliger Kamm über die Mitte, die Seiten bis auf Stoppeln rasiert, im Nacken hängt eine längere Strähne",
          "a spiky crest down the middle, the sides shaved to stubble, a longer strand hanging at the nape", 133045);
        S("dichte Franse, kinnlang", "thick fringe, chin length",
          "dichtes glattes Haar mit feiner gerader Franse an den Brauen, fällt rundum bis unters Kinn",
          "thick smooth hair with a fine straight fringe at the brows, falling all around below the chin", 133047);
        S("glatte Seitenfranse", "smooth side fringe",
          "glattes Haar mit Seitenscheitel, die Franse fällt schräg über ein Auge, die Spitzen am Nacken ausgefranst",
          "smooth hair with a side parting, the fringe falling diagonally over one eye, frayed ends at the nape", 133048);
        S("kurze Locken", "short curls",
          "kurzes dicht gelocktes Haar eng am Kopf, gezackter Ansatz an der Stirn, die Ohren frei",
          "short densely curled hair close to the head, jagged hairline at the forehead, the ears free", 133051);
        S("geschwungene Spitze oben", "curved crown spike",
          "gestuftes Haar, am Scheitel schwingt eine Strähne bogenförmig auf, eine lange Spitze fällt über die Wange",
          "layered hair, a strand curving upward at the crown, a long point falling over the cheek", 133052);
        S("hoher Stachelzopf", "high spiky ponytail",
          "die Seite kurz geschoren, das Haar hoch zu einem stacheligen Zopf gebunden, eine dicke Strähne fällt vorn übers Gesicht",
          "the side cropped short, the hair tied high into a spiky ponytail, a thick strand falling forward over the face", 133053);
        S("großer Knoten oben", "large bun on top",
          "das Haar hinten oben zu einem großen gewickelten Knoten gedreht, lange Strähnen bleiben vorn lose und fallen bis zur Schulter",
          "hair twisted into a large wrapped bun at the back, long strands left loose in front falling to the shoulder", 133054);
        S("Bob, schräge Franse", "bob, diagonal fringe",
          "runder Bob bis ans Kinn, die weiche Franse liegt schräg über der ganzen Stirn",
          "rounded bob to the chin, the soft fringe lying diagonally across the whole forehead", 133055);
        S("Pony, sehr lang", "fringe, very long",
          "gerade abgeschnittener Pony an den Brauen, sehr langes glattes Haar fällt beidseitig weit über die Schultern",
          "bluntly cut fringe at the brows, very long straight hair falling well past the shoulders on both sides", 133056);
        S("dicht gestuft", "densely layered",
          "dicht gestuftes Haar, lange Strähnen fallen über die Stirn und ein Auge, gezackte Spitzen am Ohr",
          "densely layered hair, long strands falling over the forehead and one eye, jagged points at the ear", 133064);
        S("zurückgekämmt, zwei Strähnen", "combed back, two strands",
          "das Haar in Wellen glatt nach hinten gekämmt, zwei dünne Strähnen hängen an den Schläfen herab",
          "the hair combed smoothly back in waves, two thin strands hanging down at the temples", 133081);

        // Lalafell, female - 52 entries. Plainsfolk and Dunesfolk share the icon set
        S("hoher Zopf, zerfranst", "high frayed tail",
          "vorn lange glatte Strähnen bis unter den Kiefer, am Scheitel ein hoch gebundener, zerfranst auslaufender Zopf",
          "long straight strands to below the jaw at the front, a high-tied tail fraying out at the crown", 133201);
        S("Zöpfe, Scheitelschopf", "braids, crown tuft",
          "feine Franse, an beiden Seiten dünne Flechten bis unters Kinn, am Scheitel ein kleiner aufrechter Schopf",
          "fine fringe, thin braids on both sides down past the chin, a small upright tuft at the crown", 133202);
        S("hoher Knoten, Flechte", "high bun, braid",
          "das Haar hoch aufgesteckt zu einem runden Knoten, vor dem Ohr fällt eine dünne Flechte herab",
          "hair swept up into a round bun high at the back, a thin braid falling in front of the ear", 133203);
        S("hohe Tolle", "high quiff",
          "das Deckhaar stirnfrei zu einer hohen Tolle gestrichen, seitlich fallen glatte Strähnen über die Schultern",
          "top hair swept off the forehead into a high quiff, straight strands falling past the shoulders at the sides", 133205);
        S("Bob mit Haarreif", "bob with headband",
          "kinnlanger Bob mit gerader Franse, darüber ein schmaler glatter Haarreif",
          "chin-length bob with a straight fringe, a narrow smooth headband over the crown", 133206);
        S("asymmetrisch, Seitenscheitel", "asymmetric, side parting",
          "tiefer Seitenscheitel, eine Seite kurz und stufig am Kiefer, die andere fällt glatt unter die Schulter",
          "deep side parting, one side short and layered at the jaw, the other falling straight below the shoulder", 133207);
        S("Franse, Nackensträhne", "fringe, nape strand",
          "gerade Franse, kieferlanges Haar, am Nacken hängt eine dünne ausgefranste Strähne herab",
          "straight fringe, jaw-length hair, a thin frayed strand hanging down at the nape", 133208);
        S("breiter Haarreif", "broad headband",
          "breiter weicher Haarreif weit hinten auf dem Kopf, gerade Franse, am Nacken kurze ausgefranste Spitzen",
          "a broad soft headband set well back, straight fringe, short frayed points at the nape", 133209);
        S("Schopf mit Spange", "tuft with clasp",
          "schwere gerade Franse, das Deckhaar am Scheitel senkrecht hochgesteckt und mit einer Spange gefasst",
          "heavy straight fringe, the top hair pinned upright at the crown and held with a clasp", 133210);
        S("vorn längerer Bob", "forward-angled bob",
          "glatter Bob mit gerader Franse, hinten kürzer, vorn spitz bis zum Schlüsselbein verlängert",
          "sleek bob with a straight fringe, shorter at the back, tapering longer to the collarbone in front", 133211);
        S("voluminös, hinten lang", "voluminous, long at back",
          "lange Seitenfranse, das Deckhaar voluminös nach hinten gelegt, hinten fällt das Haar über die Schulter",
          "long side fringe, the top swept back with volume, the length falling past the shoulder at the back", 133212);
        S("seitliche Flechte", "side braid",
          "langes glattes Haar mit feiner Franse, eine dünne Flechte läuft von der Schläfe zum Nacken und ist dort gebunden",
          "long straight hair with a fine fringe, a thin braid running from the temple to the nape and tied there", 133213);
        S("Bob, dunkle Spangen", "bob, dark clips",
          "kinnlanger Bob mit gerader Franse, hinter dem Ohr eine nach außen geschwungene Strähne, oben beidseitig eine dunkle Spange",
          "chin-length bob with a straight fringe, a strand curving outwards behind the ear, a dark clip on each side", 133204);
        S("Bob, helle Spangen", "bob, pale clips",
          "kinnlanger Bob mit gerader Franse, hinter dem Ohr eine nach außen geschwungene Strähne, oben beidseitig eine helle, fast farblose Spange",
          "chin-length bob with a straight fringe, a strand curving outwards behind the ear, a pale, almost colourless clip on each side", 133214);
        S("Bob, rote Spangen", "bob, red clips",
          "kinnlanger Bob mit gerader Franse, hinter dem Ohr eine nach außen geschwungene Strähne, oben beidseitig eine rote Spange",
          "chin-length bob with a straight fringe, a strand curving outwards behind the ear, a red clip on each side", 133215);
        S("Bob, blaugrüne Spangen", "bob, blue-green clips",
          "kinnlanger Bob mit gerader Franse, hinter dem Ohr eine nach außen geschwungene Strähne, oben beidseitig eine gedämpft blaugrüne Spange",
          "chin-length bob with a straight fringe, a strand curving outwards behind the ear, a muted blue-green clip on each side", 133216);
        S("hoher Seitenzopf", "high side ponytail",
          "gerade Franse, kinnlange Seitensträhnen, über dem Ohr ein hoch gebundener Zopf, der zur Seite fällt",
          "straight fringe, chin-length side strands, a ponytail tied high above the ear falling to the side", 133220);
        S("Mähne, Stirnspitze", "mane, pointed forelock",
          "voluminöse Mähne nach hinten gestrichen, vorn eine spitz zulaufende Strähne auf der Stirn, hinten lange Wellen",
          "voluminous mane swept back, a strand tapering to a point on the forehead, long waves behind", 133221);
        S("schulterlang, volle Franse", "shoulder-length, full fringe",
          "weiches schulterlanges Haar mit voller Franse, die Spitzen leicht nach innen gewellt",
          "soft shoulder-length hair with a full fringe, the ends curling gently inwards", 133219);
        S("zurückgekämmt, lang", "combed back, long",
          "das Haar glatt aus der Stirn nach hinten und zur Seite gekämmt, hinten fällt es über die Schulter",
          "hair combed smoothly back and to one side off the forehead, falling past the shoulder behind", 133222);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "enge Reihen über den Kopf, davor lange dünne Rastalocken mit Ringen, die vorn beidseitig herabfallen",
          "tight rows across the head, long thin dreadlocks with rings falling forward on both sides", 133223);
        S("hoher glatter Zopf", "high sleek ponytail",
          "das Haar glatt zurückgekämmt und am Hinterkopf hoch gebunden, der lange Zopf fällt über die Schulter",
          "hair combed smoothly back and tied high at the back, the long tail falling past the shoulder", 133224);
        S("Haarreif, langes Haar", "headband, long hair",
          "langes glattes Haar mit langer Seitenfranse, darüber ein schmaler glatter Haarreif",
          "long straight hair with a long side fringe, a narrow smooth headband over the crown", 133225);
        S("kurz, zottig", "short, shaggy",
          "kurzer zottiger Schnitt, spitz auslaufende Strähnen über der Stirn und rund um den Kopf",
          "short shaggy cut, strands tapering to points over the forehead and all around the head", 133227);
        S("zwei lange Flechten", "two long braids",
          "kurzer Schnitt mit Franse über der Stirn, an beiden Seiten hängt eine lange dünne Flechte herab",
          "short cut with a fringe over the forehead, a long thin braid hanging down on each side", 133226);
        S("umwickelter Zopf", "wrapped ponytail",
          "gerade Franse, das Haar hoch am Hinterkopf zu einem umwickelten Zopf gebunden, davor lange Seitensträhnen",
          "straight fringe, hair tied high at the back into a wrapped ponytail, long side strands in front", 133229);
        S("Mittelscheitel, Wellen", "centre parting, waves",
          "Mittelscheitel, das Haar aus der Stirn gestrichen, lange weiche Wellen fallen beidseitig über die Schultern",
          "centre parting, hair brushed off the forehead, long soft waves falling over the shoulders on both sides", 133230);
        S("Seitenscheitel, Wellen", "side parting, waves",
          "Seitenscheitel, eine lange Strähne fällt über die Stirn, darunter lange weiche Wellen bis unter die Schultern",
          "side parting, a long strand sweeping across the forehead, long soft waves below the shoulders", 133231);
        S("kurz, anliegend", "short, sleek",
          "kurzer anliegender Schnitt mit Scheitel, das Haar glatt über die Ohren gelegt, Spitzen am Kiefer",
          "short close-fitting cut with a parting, hair laid smoothly over the ears, points at the jaw", 133232);
        S("kurz, Nackenspitzen", "short, frayed nape",
          "kurzer gescheitelter Schnitt, feine Strähnen auf der Stirn, am Nacken lange ausgefranste Spitzen",
          "short parted cut, fine strands on the forehead, long frayed points at the nape", 133233);
        S("kurz, zurückgestrichen", "short, swept back",
          "kurzer Schnitt mit Scheitel, die Seiten nach hinten gestrichen, am Nacken dünne lange Spitzen",
          "short cut with a parting, the sides swept back, thin long points at the nape", 133237);
        S("abstehende Spitzen", "flicked-out ends",
          "kurzer Schnitt mit gerader Franse, die Seiten enden in abstehenden gefiederten Spitzen",
          "short cut with a straight fringe, the sides ending in flicked-out feathered points", 133238);
        S("zottig, lange Stirnsträhne", "shaggy, long forelock",
          "zottiger Schnitt, das Deckhaar nach hinten aufgestellt, eine lange spitze Strähne fällt über Stirn und Wange",
          "shaggy cut, the top spiked up and back, a long pointed strand falling over forehead and cheek", 133239);
        S("stachelig zurück", "spiked back",
          "kurzes Haar stachelig nach hinten gebürstet, einzelne feine Strähnen hängen auf die Stirn",
          "short hair brushed spikily back, a few fine strands hanging onto the forehead", 133240);
        S("Ringellocken vorn", "ringlets in front",
          "langes glattes Haar mit schwerer Franse, vorn hängen beidseitig dicke Ringellocken herab",
          "long straight hair with a heavy fringe, thick ringlets hanging down at the front on both sides", 133241);
        S("runder Kurzbob", "round short bob",
          "kurzer runder Bob mit feiner Franse, die Spitzen nach innen gelegt, am Scheitel steht eine Strähne ab",
          "short rounded bob with a fine fringe, the ends turned inwards, one strand sticking up at the crown", 133242);
        S("geschorene Seiten, Tolle", "shaved sides, quiff",
          "die Seiten kurz geschoren, das Deckhaar voluminös nach hinten gestrichen, vorn eine spitze Strähne auf der Stirn",
          "sides cropped short, the top hair swept back with volume, a pointed strand on the forehead", 133243);
        S("große runde Tolle", "large round pompadour",
          "eine große runde Tolle, das Haar glatt nach oben und hinten gelegt, die Seiten kurz",
          "a large round pompadour, hair swept smoothly up and back, the sides short", 133244);
        S("Scheitelspitze, gezackt", "crown spike, jagged",
          "stufiger Schnitt mit einer aufragenden Spitze am Scheitel, gezackte Strähnen rahmen beidseitig das Gesicht",
          "layered cut with a spike rising at the crown, jagged strands framing the face on both sides", 133245);
        S("kurz, Seitenscheitel", "short, side parting",
          "kurzer Schnitt mit tiefem Seitenscheitel, die Franse quer über die Stirn gelegt, hinten gefiederte Spitzen",
          "short cut with a deep side parting, the fringe laid across the forehead, feathered points behind", 133247);
        S("spitze Seitensträhnen", "pointed side strands",
          "kurzer Schnitt mit Seitenscheitel, beidseitig laufen spitze Strähnen bis zum Kiefer herab",
          "short cut with a side parting, pointed strands running down to the jaw on both sides", 133248);
        S("Undercut, Kamm", "undercut, crest",
          "eine Kopfseite kurz rasiert, das Deckhaar zu einem stacheligen Kamm nach hinten gestellt, am Nacken lange dünne Strähnen",
          "one side shaved short, the top raised into a spiky crest swept back, long thin strands at the nape", 133249);
        S("glatter Bob", "smooth bob",
          "glatter Bob mit gerader Franse, das Haar hinter das Ohr gelegt, am Nacken etwas länger",
          "smooth bob with a straight fringe, hair tucked behind the ear, a little longer at the nape", 133251);
        S("zerzaust, Strähnen vorn", "tousled, front strands",
          "kurzer zerzauster Schnitt mit Seitenscheitel, lange spitze Strähnen fallen vorn bis zum Kinn",
          "short tousled cut with a side parting, long pointed strands falling to the chin at the front", 133252);
        S("sehr kurz, struppig", "very short, choppy",
          "sehr kurzer struppiger Schnitt, eng am Kopf anliegend, die Stirn bleibt frei",
          "very short choppy crop lying close to the head, the forehead left bare", 133255);
        S("Franse überm Auge", "fringe over one eye",
          "stufiger kurzer Schnitt, lange Franse über einem Auge, am Scheitel steht eine Strähne auf",
          "layered short cut, a long fringe over one eye, a strand standing up at the crown", 133256);
        S("kurze Seiten, Zopf", "short sides, ponytail",
          "die Seiten kurz, das Haar hoch am Scheitel zu einem stacheligen Zopf gebunden, vorn eine lange Strähne",
          "short sides, hair tied high at the crown into a spiky tail, one long strand falling forward", 133257);
        S("Knoten, lange Franse", "bun, long fringe",
          "das Haar hinten zu einem Knoten hochgesteckt, vorn fallen eine lange Seitenfranse und lose Strähnen herab",
          "hair pinned up into a knot at the back, a long side fringe and loose strands falling in front", 133258);
        S("Kurzbob, Ohren frei", "short bob, ears free",
          "glatter kurzer Bob mit gescheitelter Franse, die Spitzen enden am Kiefer, die Ohren bleiben frei",
          "smooth short bob with a parted fringe, the ends finishing at the jaw, the ears left free", 133259);
        S("sehr lang, glatt", "very long, straight",
          "sehr langes glattes Haar mit schwerer gerader Franse, die Seiten fallen glatt weit über die Schultern",
          "very long straight hair with a heavy blunt fringe, the sides falling smoothly well past the shoulders", 133260);
        S("kurz, gefiedert", "short, feathered",
          "kurzer weich gefiederter Schnitt, feine spitze Franse auf der Stirn, die Spitzen enden unter dem Ohr",
          "short softly feathered cut, a fine pointed fringe on the forehead, the ends finishing below the ear", 133268);
        S("Tolle, zwei Strähnen", "quiff, two strands",
          "das Deckhaar voluminös nach hinten gestrichen, die Seiten kurz, vorn fallen zwei dünne lange Strähnen ins Gesicht",
          "top hair swept back with volume, the sides short, two thin long strands falling into the face", 133286);

        // Miqo'te, male - 47 entries. Seeker and Keeper share the icon set
        S("kurz, weich gewellt", "short, softly waved",
          "kurzer weicher Schnitt, oben aufgelockert, die Spitzen stellen sich um Ohr und Nacken nach außen",
          "short soft cut, loose on top, the ends flicking outward around the ear and nape", 134001);
        S("kurz, stachelig zurück", "short, spiked back",
          "kurz und stachelig, aus der Stirn nach hinten gestrichen, die Spitzen laufen scharf aus",
          "short and spiky, swept back off the forehead, the tips running out sharply", 134002);
        S("kurz, zottig", "short, shaggy",
          "durchgehend kurz und zottig gefiedert, eine zerfranste Franse auf der Stirn, das Haar legt sich um die Ohren",
          "shaggy and feathered all over, a frayed fringe on the forehead, the hair wrapping around the ears", 134003);
        S("zottig, dünner Zopf", "shaggy, thin braid",
          "zottiger kurzer Schnitt, davor ein dünner geflochtener Zopf mit Perle, der bis zum Kiefer fällt",
          "shaggy short cut, with a thin braid and bead falling in front down to the jaw", 134004);
        S("kinnlang, wellig", "chin-length, wavy",
          "kinnlanges welliges Haar, gescheitelt, einzelne Strähnen auf der Stirn, die Enden ringeln sich am Kiefer",
          "chin-length wavy hair, parted, single strands on the forehead, the ends curling at the jaw", 134005);
        S("kinnlang, gestuft", "chin-length, layered",
          "kinnlang und glatt gestuft, kurze spitze Franse, die Seiten schwingen am Kiefer nach außen",
          "chin-length and smoothly layered, short pointed fringe, the sides swinging outward at the jaw", 134006);
        S("zurückgestrichen, kinnlang", "swept back, chin-length",
          "aus der zackigen Stirnlinie nach hinten gestrichen, die Masse endet kinnlang und stellt sich ab",
          "swept back from a jagged hairline, the mass ending at the chin and flicking out", 134007);
        S("Bob, Wangensträhne", "bob, cheek strand",
          "kurzer Bob mit dünner gescheitelter Franse, eine lange spitze Strähne läuft über die Wange zum Kiefer",
          "short bob with a thin parted fringe, a long pointed strand running over the cheek to the jaw", 134008);
        S("viele dünne Zöpfe", "many thin braids",
          "der Scheitel in schmale Flechten gelegt, lange dünne Zöpfe mit Ringen fallen vorn bis auf die Brust",
          "the crown laid in narrow braids, long thin plaits with rings falling forward to the chest", 134009);
        S("struppig, lange Vordersträhnen", "tousled, long front strands",
          "struppig nach vorn fallend, spitze Strähnen über den Augen, eine lange Strähne vor dem Ohr",
          "tousled and falling forward, pointed strands over the eyes, one long strand in front of the ear", 134010);
        S("Mähne nach hinten", "mane swept back",
          "volle Mähne weit nach hinten gestrichen, lange spitze Enden am Nacken, lose Strähnen an der Schläfe",
          "full mane swept far back, long pointed ends at the nape, loose strands at the temple", 134011);
        S("Franse überm Auge", "fringe over the eye",
          "glatter kinnlanger Bob, die schwere Seitenfranse verdeckt ein Auge, hinten hängt ein dünner geflochtener Strang",
          "smooth chin-length bob, the heavy side fringe covering one eye, a thin plaited strand hanging at the back", 134012);
        S("voll, gefiedert", "full, feathered",
          "dichter voller Schnitt, ringsum fein gefiedert, deckt die Ohransätze, im Nacken struppig ausgefranst",
          "dense full cut, finely feathered all round, covering the base of the ears, frayed and shaggy at the nape", 134016);
        S("mittellang, gescheitelt", "medium length, parted",
          "mittellanges glattes Haar, gescheitelt und ins Gesicht fallend, einzelne Strähnen stehen am Scheitel ab",
          "medium-length straight hair, parted and falling into the face, single strands standing up at the crown", 134015);
        S("schulterlang, gestuft", "shoulder-length, layered",
          "schulterlang und gestuft, Seitenscheitel, eine lange Strähne vor dem Ohr, die Enden schwingen aus",
          "shoulder-length and layered, side parting, a long lock in front of the ear, the ends swinging out", 134017);
        S("lang, Stirnband", "long, headband",
          "langes glattes Haar über die Schultern, mittig gescheitelt, ein schmales geflochtenes Band auf der Stirn",
          "long straight hair past the shoulders, centre parting, a narrow braided band across the forehead", 134018);
        S("Seitenscheitel, spitze Strähnen", "side parting, pointed strands",
          "seitlich gescheitelt, lange spitze Strähnen fallen über Schläfe und Wange, hinten gestuft und ausgefranst",
          "side parting, long pointed strands falling over temple and cheek, layered and frayed at the back", 134020);
        S("runder Topfschnitt", "round bowl cut",
          "runder Topfschnitt, glatte Franse bis zu den Brauen, vorn am Scheitel steht eine Strähne ab",
          "round bowl cut, smooth fringe down to the brows, one strand standing up at the front of the crown", 134019);
        S("gezackte Franse", "notched fringe",
          "kurz und glatt gestuft, die Franse gezackt über der Stirn, die Enden spitz bis in den Nacken",
          "short and smoothly layered, the fringe notched over the forehead, pointed ends down to the nape", 134022);
        S("glatt zurückgekämmt", "slicked straight back",
          "glatt aus der Stirn nach hinten gekämmt, eng am Kopf anliegend, ohne Franse, im Nacken kurz",
          "combed smoothly back off the forehead, lying close to the head, no fringe, short at the nape", 134021);
        S("hoher Schopf, Spange", "high tuft, clasp",
          "die Seiten nach hinten gestrichen, das Deckhaar hoch zu einem gefächerten Schopf gerafft, seitlich eine kleine Spange",
          "the sides swept back, the top hair gathered high into a fanned tuft, a small clasp at the side", 134024);
        S("sehr kurz, anliegend", "very short, close-lying",
          "sehr kurz und dicht anliegend, die Stirnlinie gezackt, vor dem Ohr läuft das Haar spitz aus",
          "very short and close-lying, the hairline notched, tapering to a point in front of the ear", 134025);
        S("kurz, spitze Enden", "short, pointed tips",
          "kurz und struppig, spitze Strähnen stehen über Stirn und Ohr ab, im Nacken stellen sich die Enden ab",
          "short and tousled, pointed strands sticking out over forehead and ear, the ends flicking at the nape", 134026);
        S("kurz, weich gestuft", "short, softly layered",
          "kurz und weich gestuft, die Franse legt sich schräg über die Stirn, feine Spitzen vor dem Ohr",
          "short and softly layered, the fringe lying slanted across the forehead, fine points in front of the ear", 134027);
        S("Seitenscheitel, struppiger Nacken", "side parting, shaggy nape",
          "seitlich gescheitelt, oben glatt gelegt, die Seiten reichen zum Kiefer, der Nacken bleibt struppig ausgefranst",
          "side parting, smooth on top, the sides reaching the jaw, the nape left shaggy and frayed", 134028);
        S("Mittelscheitel, kinnlang", "centre parting, chin-length",
          "mittig gescheitelt und glatt, das Haar fällt bis zum Kiefer, die Enden gefiedert und leicht abstehend",
          "parted in the middle and smooth, the hair falling to the jaw, the ends feathered and slightly flicking", 134031);
        S("volle Franse, ausgestellt", "full fringe, flicked out",
          "volle gezackte Franse bis zu den Augen, kinnlang, die Enden schwingen unter dem Ohr weit nach außen",
          "full notched fringe down to the eyes, chin-length, the ends swinging far outward below the ear", 134032);
        S("zerzaust, Scheitelknoten", "tousled, crown knot",
          "zerzaust und stachelig, am Scheitel ist eine Partie zu einem Knoten verdreht, lange Spitzen fallen vorn",
          "tousled and spiky, a section twisted into a knot at the crown, long points falling at the front", 134033);
        S("windzerzaust zurück", "windswept back",
          "wie vom Wind nach hinten geblasen, dünne Strähnen hängen lose auf die Stirn, der Nacken bleibt kurz",
          "as if blown back by the wind, thin strands hanging loose on the forehead, the nape kept short", 134034);
        S("Franse, langer Flechtzopf", "fringe, long braid",
          "gerade Franse bis zu den Brauen, vorn kinnlang, hinten fällt ein dicker Flechtzopf bis unter die Schulter",
          "straight fringe to the brows, chin-length at the front, a thick braid falling below the shoulder behind", 134035);
        S("Bob, dünne Franse", "bob, thin fringe",
          "glatter Bob mit dünner luftiger Franse, das Haar reicht hinten bis in den Nacken und stellt sich leicht ab",
          "smooth bob with a thin airy fringe, the hair reaching the nape at the back and flicking slightly", 134036);
        S("Undercut, gedrehter Strang", "undercut, twisted strand",
          "die Seite kurz geschoren mit gezackter Kante, das lange Deckhaar nach hinten gelegt, hinterm Ohr ein gedrehter Strang",
          "the side shaved short with a jagged edge, the long top laid back, a twisted strand behind the ear", 134037);
        S("große Rolltolle", "big rolled quiff",
          "das Deckhaar zu einer großen glatten Rolle über die Stirn geführt, die Seiten kurz, der Nacken gefiedert",
          "the top hair rolled into a big smooth quiff over the forehead, short sides, feathered nape", 134038);
        S("gestuft, hinten schulterlang", "layered, shoulder-length behind",
          "gestufter zottiger Schnitt, gescheitelt, lange Strähnen rahmen das Gesicht, hinten reicht das Haar zur Schulter",
          "layered shaggy cut, parted, long strands framing the face, the hair reaching the shoulder at the back", 134039);
        S("breite Seitenpartie", "wide side sweep",
          "eine breite Haarpartie fällt schräg über die Stirn, die Seiten kinnlang, der Nacken kurz und frei",
          "a broad section falling slanted across the forehead, chin-length sides, the nape short and bare", 134041);
        S("gestuft, hinten kinnlang", "layered, chin-length behind",
          "gestuft und gescheitelt, dünne lange Strähnen fallen beidseitig übers Kinn, hinten endet das Haar am Kiefer",
          "layered and parted, thin long strands falling past the chin on both sides, ending at the jaw behind", 134042);
        S("hinten lang gestuft", "layered, long behind",
          "vorn aus der Stirn hochgekämmt und an den Schläfen kurz, hinten fällt das Haar lang und gestuft",
          "combed up off the forehead and short at the temples, the hair falling long and layered at the back", 134043);
        S("Franse, gewickelter Zopf", "fringe, wrapped ponytail",
          "gerade Franse und kinnlange Seiten, hinten ist das Haar zu einer Rolle gewickelt, mit langem Schweif",
          "straight fringe and chin-length sides, the hair wrapped into a coil at the back, with a long tail", 134045);
        S("nach vorn gebürstet", "brushed forward",
          "das Haar vom Scheitel nach vorn gebürstet, es verdeckt Stirn und ein Auge, die Enden zerfranst",
          "the hair brushed forward from the crown, covering forehead and one eye, the ends frayed", 134046);
        S("sehr kurz, borstig", "very short, bristly",
          "rundum sehr kurz und borstig geschnitten, eng am Kopf, die Stirnlinie zackig, der Hals bleibt frei",
          "cut very short and bristly all round, close to the head, jagged hairline, the neck left bare", 134049);
        S("voll, breite Franse", "full, broad fringe",
          "voller vielschichtiger Schnitt, eine breite Partie fällt über Stirn und Auge, die Enden grob gestuft",
          "full multi-layered cut, a broad section falling over forehead and eye, the ends roughly layered", 134050);
        S("Undercut, Schopf", "undercut, topknot",
          "die Seiten hart ausrasiert, das Deckhaar hoch zu einem stacheligen Schopf gebunden, eine lange Strähne an der Schläfe",
          "sides shaved hard, the top hair tied high into a spiky topknot, one long lock hanging at the temple", 134051);
        S("Seitenknoten, offen lang", "side bun, loose",
          "hinterm Ohr ein kleiner runder Knoten gebunden, das übrige Haar bleibt lang und offen über der Schulter",
          "a small round bun tied behind the ear, the rest of the hair left long and loose over the shoulder", 134052);
        S("glatt, schräge Franse", "smooth, slanted fringe",
          "glatt anliegend, die Franse zieht schräg über die Stirn, die Seiten kinnlang, der Nacken spitz und kurz",
          "smooth and close-lying, the fringe slanting across the forehead, chin-length sides, short pointed nape", 134053);
        S("schwere Franse, lang", "heavy fringe, long",
          "schwere gerade geschnittene Franse bis zu den Augen, die Seiten stumpf am Kiefer, das übrige Haar sehr lang",
          "heavy blunt fringe down to the eyes, blunt at the jaw at the sides, the rest very long", 134054);
        S("zottige Stirnfranse", "shaggy forehead fringe",
          "eine zottige Franse hängt gerade über die ganze Stirn bis zu den Augen, Seiten und Nacken bleiben kurz",
          "a shaggy fringe hanging straight over the whole forehead to the eyes, sides and nape kept short", 134062);
        S("zurück, lose Strähnen", "swept back, loose strands",
          "voll nach hinten gestrichen, mehrere dünne Strähnen hängen lose an den Schläfen herab, die Ohren bleiben frei",
          "swept fully back, several thin strands hanging loose at the temples, leaving the ears clear", 134079);

        // Miqo'te, female - 52 entries. Seeker and Keeper share the icon set
        S("kurz, zottig", "short, shaggy",
          "kurzer zottiger Schnitt, ringsum ausgefranste Spitzen, zackige Franse auf der Stirn, Spitzen bis zum Kiefer",
          "short shaggy cut, frayed points all round, jagged fringe on the forehead, ends at the jaw", 134202);
        S("Bob mit Ponyfranse", "bob with blunt fringe",
          "glatter kinnlanger Bob, gerade abgeschnittene Franse über den Brauen, die Spitzen fallen glatt nach vorn",
          "sleek chin-length bob, fringe cut straight above the brows, the ends falling smoothly forward", 134203);
        S("schulterlang, glatt", "shoulder-length, straight",
          "schulterlanges glattes Haar, in der Mitte gescheitelt, Stirn frei, gerade Strähnen fallen vor den Ohren herab",
          "shoulder-length straight hair, parted in the middle, forehead bare, straight strands falling in front of the ears", 134205);
        S("seitlich hochgefegt", "swept up sideways",
          "mittellanges Haar schräg zur Seite gefegt, spitz auslaufende Strähnen fächern über dem Ohr auf",
          "mid-length hair swept diagonally to one side, tapering strands fanning out above the ear", 134206);
        S("Bob mit Haarreif", "bob with headband",
          "kinnlanger Bob mit Franse, ein breiter glatter Haarreif läuft über den Scheitel",
          "chin-length bob with a fringe, a broad smooth headband running across the crown", 134207);
        S("Zopf mit Schleife", "ponytail with a bow",
          "Franse über den Brauen, das übrige Haar hoch zu einem Zopf mit Schleife gebunden, Spitzen gewellt",
          "fringe above the brows, the rest tied high into a ponytail with a bow, ends waved", 134209);
        S("hochgefegter Schopf", "upswept crest",
          "das Haar aus der Stirn hoch nach hinten gefegt, spitze Strähnen fächern über dem Ohr bis unters Kinn",
          "hair swept high back off the forehead, pointed strands fanning past the ear to below the chin", 134210);
        S("Bob, Schläfenzöpfe", "bob, temple braids",
          "kinnlanger Bob mit Franse, beidseitig ein dünner Schläfenzopf mit Band am Ende, kleiner Schopf am Scheitel",
          "chin-length bob with a fringe, a thin braid at each temple tied at the end, small tuft at the crown", 134211);
        S("hochgesteckt, Zöpfchen", "pinned up, thin braid",
          "das Haar glatt nach hinten hochgesteckt, ein dünner Zopf hängt vor dem Ohr bis unters Kinn",
          "hair pinned smoothly up at the back, one thin braid hanging in front of the ear past the chin", 134212);
        S("Wellenbob, bernsteinfarbene Feder", "wave bob, amber feather",
          "kurzer welliger Bob aus der Stirn gestrichen, eine schmale bernsteinfarbene Feder steckt hinter dem Ohr",
          "short wavy bob swept off the forehead, a slim amber feather tucked behind the ear", 134201);
        S("Wellenbob, grüne Feder", "wave bob, green feather",
          "kurzer welliger Bob aus der Stirn gestrichen, eine schmale grüne Feder steckt hinter dem Ohr",
          "short wavy bob swept off the forehead, a slim green feather tucked behind the ear", 134213);
        S("Wellenbob, blauviolette Feder", "wave bob, blue-violet feather",
          "kurzer welliger Bob aus der Stirn gestrichen, eine schmale blauviolette Feder steckt hinter dem Ohr",
          "short wavy bob swept off the forehead, a slim blue-violet feather tucked behind the ear", 134214);
        S("Wellenbob, schmucklos", "wave bob, plain",
          "kurzer welliger Bob aus der Stirn gestrichen, die Spitzen stellen sich am Kiefer nach außen, ohne Schmuck",
          "short wavy bob swept off the forehead, the ends flicking outward at the jaw, no ornament", 134215);
        S("Bob, schlichte Ringe", "bob, plain rings",
          "kurzer Bob mit Franse, vor jedem Ohr eine Strähne, von glatten Ringen gefasst und in Quasten endend",
          "short bob with a fringe, one strand in front of each ear, held by plain rings and ending in tassels", 134204);
        S("Bob, verzierte Spangen", "bob, decorated clasps",
          "kurzer Bob mit Franse, vor jedem Ohr eine Strähne, von breiten verzierten Spangen gefasst und in Quasten endend",
          "short bob with a fringe, one strand in front of each ear, held by broad decorated clasps, ending in tassels", 134216);
        S("lang, Franse, Flechte", "long, fringe, braid",
          "langes glattes Haar mit gerader Franse, eine schmale Flechte läuft seitlich am Kopf entlang und ist geklammert",
          "long straight hair with a straight fringe, a narrow braid running down the side of the head and clasped", 134208);
        S("lang, Mittelscheitel, Flechte", "long, centre parting, braid",
          "langes glattes Haar ohne Franse, in der Mitte gescheitelt, seitlich eine schmale geklammerte Flechte",
          "long straight hair without a fringe, parted in the middle, a narrow clasped braid at the side", 134217);
        S("hoher Seitenzopf", "high side ponytail",
          "Franse über den Brauen, das Haar hoch seitlich zusammengebunden, der Zopf fällt in weichen Wellen herab",
          "fringe above the brows, hair gathered high at the side, the tail falling down in soft waves", 134221);
        S("lang, gewellt, Franse", "long, wavy, fringe",
          "langes offenes Haar mit voller Franse, weich gewellt, fällt weit über die Schultern",
          "long loose hair with a full fringe, softly waved, falling well over the shoulders", 134220);
        S("lang, Seitenscheitel", "long, side parting",
          "langes Haar mit tiefem Seitenscheitel, eine breite Strähne fegt über die Stirn, gestufte Spitzen",
          "long hair with a deep side parting, a broad sweep across the forehead, layered ends", 134222);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "lange gedrehte Rastalocken, mehrere fallen nach vorn über Gesicht und Schultern, von Ringen gefasst",
          "long twisted dreadlocks, several falling forward over the face and shoulders, held by rings", 134223);
        S("lang mit Haarreif", "long with headband",
          "langes glattes Haar mit schräger Franse, ein schmaler Haarreif läuft über den Scheitel",
          "long straight hair with a slanted fringe, a narrow headband running across the crown", 134225);
        S("hoher Pferdeschwanz", "high ponytail",
          "das Haar glatt nach hinten gestrichen und am Hinterkopf hoch gebunden, der lange Zopf fällt hinten herab",
          "hair swept smoothly back and tied high at the back of the head, the long tail falling behind", 134224);
        S("kurz, spitze Franse", "short, pointed fringe",
          "kurzer gestufter Schnitt, die Franse fällt in dünnen Spitzen über die Stirn, Nacken kurz",
          "short layered cut, the fringe falling in thin points over the forehead, nape short", 134227);
        S("zwei lange Zöpfe", "two long braids",
          "Franse über den Brauen, beidseitig ein langer Zopf, der vorn über die Schultern herabhängt",
          "fringe above the brows, one long braid on each side hanging forward over the shoulders", 134226);
        S("halb hochgebunden", "half tied up",
          "Franse über den Brauen, das Deckhaar hoch am Hinterkopf gebunden, lange Strähnen fallen vor den Ohren herab",
          "fringe above the brows, the top hair tied high at the back, long strands falling in front of the ears", 134229);
        S("lange Wellen, Mittelscheitel", "long waves, centre parting",
          "langes gewelltes Haar, in der Mitte gescheitelt, Stirn frei, die Wellen fallen über beide Schultern",
          "long wavy hair, parted in the middle, forehead bare, the waves falling over both shoulders", 134230);
        S("lange Wellen, Seitenfranse", "long waves, side fringe",
          "langes gewelltes Haar, eine lange Franse fegt schräg über die Stirn bis zur Braue",
          "long wavy hair, a long fringe sweeping diagonally across the forehead to the brow", 134231);
        S("kurzer Pixie", "short pixie",
          "kurzer glatter Schnitt mit Seitenscheitel, die Strähnen liegen eng an, Spitzen enden unter dem Ohr",
          "short smooth cut with a side parting, the strands lying close, ends finishing below the ear", 134232);
        S("kurz, Nacken gewellt", "short, waved nape",
          "kurzer Schnitt mit Seitenscheitel, eine dünne Strähne fällt übers Auge, im Nacken wellen sich die Spitzen nach außen",
          "short cut with a side parting, a thin strand falling over the eye, the ends waving outward at the nape", 134233);
        S("Stirn frei, gestuft", "forehead bare, layered",
          "das Haar aus der Stirn nach hinten gestrichen, kinnlang gestuft, die Spitzen stellen sich im Nacken ab",
          "hair swept back off the forehead, layered to the jaw, the ends kicking out at the nape", 134237);
        S("voluminöser Bob", "voluminous bob",
          "kinnlanger Bob mit fedriger Franse, das Haar steht füllig ab und die Spitzen biegen nach außen",
          "chin-length bob with a feathery fringe, the hair standing full and the ends curving outward", 134238);
        S("struppig, lange Franse", "tousled, long fringe",
          "struppiger kurzer Schnitt mit abstehenden Spitzen, eine lange Franse fällt schräg über Stirn und Auge",
          "tousled short cut with jutting points, a long fringe falling diagonally over the forehead and eye", 134239);
        S("nach hinten gebürstet", "brushed back",
          "das Haar nach hinten gebürstet, nur einzelne dünne Strähnen hängen gerade über die Stirn",
          "hair brushed back, only a few thin strands hanging straight over the forehead", 134240);
        S("Bob, umwickelte Strähnen", "bob, wrapped strands",
          "Bob mit gerader Franse, lange dick umwickelte Strähnen fallen beidseitig über die Schultern",
          "bob with a straight fringe, long thickly wrapped strands falling over both shoulders", 134241);
        S("gestufter Bob", "layered bob",
          "kieferlanger gestufter Bob mit dünner Franse, die Spitzen laufen weich aus und stellen sich leicht ab",
          "jaw-length layered bob with a thin fringe, the ends tapering softly and flicking slightly out", 134242);
        S("Flechte im Nacken", "braid at the nape",
          "das Haar seitlich nach hinten gefegt und im Nacken zu einer Flechte gebunden, eine Strähne fällt über die Stirn",
          "hair swept back at the side and gathered into a braid at the nape, one strand over the forehead", 134243);
        S("runde Tolle", "rounded pompadour",
          "das Deckhaar zu einer großen runden Tolle aufgetürmt, die über die Stirn ragt, Nacken kurz und spitz",
          "the top hair piled into a large rounded pompadour overhanging the forehead, nape short and pointed", 134244);
        S("kurz gestuft, Seitenscheitel", "short layered, side parting",
          "kurzer gestufter Schnitt mit tiefem Seitenscheitel, spitze Strähnen rahmen Wange und Kiefer",
          "short layered cut with a deep side parting, pointed strands framing the cheek and jaw", 134245);
        S("kurz und glatt", "short and sleek",
          "kurzer glatter Schnitt, eine lange gerade Franse fegt schräg über die Stirn, Nacken kurz",
          "short sleek cut, a long straight fringe sweeping diagonally across the forehead, nape short", 134247);
        S("gestuft, Stirnsträhnen", "layered, forehead strands",
          "kieferlang gestufter Schnitt, dünne Strähnen fallen über Stirn und Schläfen, die Spitzen laufen spitz aus",
          "jaw-length layered cut, thin strands falling over forehead and temples, the ends tapering to points", 134248);
        S("stacheliger Kamm", "spiky crest",
          "das Haar aus der Stirn hoch nach hinten gebürstet, oben stachelig, im Nacken stellen sich die Spitzen ab",
          "hair brushed high back off the forehead, spiky on top, the ends kicking out at the nape", 134249);
        S("Franse, gedrehter Nacken", "fringe, twisted nape",
          "gerade Franse über den Brauen, das Seitenhaar hinterm Ohr eingedreht, eine glatte Länge fällt in den Nacken",
          "straight fringe above the brows, the side hair twisted up behind the ear, a smooth length falling to the nape", 134251);
        S("zottig, spitze Strähne", "shaggy, pointed strand",
          "zottig gestufter Schnitt mit ausgefransten Spitzen, eine lange spitze Strähne fällt über das Auge",
          "shaggy layered cut with frayed ends, one long pointed strand falling over the eye", 134252);
        S("sehr kurz, struppig", "very short, tousled",
          "sehr kurz geschnittenes Haar, dicht am Kopf und struppig, zackige kurze Franse auf der Stirn",
          "very short cut hair, close to the head and tousled, short jagged fringe on the forehead", 134255);
        S("Schopf, schräge Franse", "tuft, slanted fringe",
          "kurzer Schnitt mit abstehenden Schöpfen am Scheitel, eine lange Franse fegt schräg über die Stirn",
          "short cut with jutting tufts at the crown, a long fringe sweeping diagonally across the forehead", 134256);
        S("hoher Fächerzopf", "high fanned ponytail",
          "das Haar hoch gebunden und stachelig aufgefächert, zwei lange Strähnen fallen vorn am Gesicht herab",
          "hair tied high and fanned out spikily, two long strands falling down at the front of the face", 134257);
        S("Knoten seitlich", "knot at the side",
          "schulterlanges Haar mit Seitenscheitel, seitlich über dem Nacken zu einem kleinen gedrehten Knoten gebunden",
          "shoulder-length hair with a side parting, tied into a small twisted knot at the side above the nape", 134258);
        S("Bob, Seitenfranse", "bob, side fringe",
          "kurzer Bob, eine lange Franse fegt schräg über die Stirn bis übers Auge, Spitzen am Kiefer",
          "short bob, a long fringe sweeping diagonally over the forehead and past the eye, ends at the jaw", 134259);
        S("lang, kurze Seitensträhnen", "long, short side lengths",
          "langes glattes Haar mit gerader Franse, die Seitensträhnen sind kurz auf Wangenhöhe geschnitten",
          "long straight hair with a straight fringe, the side strands cut short at cheek level", 134260);
        S("kurz, fedrige Franse", "short, feathery fringe",
          "kurzer zerzauster Schnitt, die Franse hängt in fedrigen Strähnen über Stirn und Auge",
          "short tousled cut, the fringe hanging in feathery strands over the forehead and eye", 134268);
        S("zerzaust, nach hinten", "tousled, swept back",
          "das Haar zerzaust nach hinten gefegt, füllig über dem Ohr, dünne Strähnen fallen an der Schläfe herab",
          "hair tousled and swept back, full above the ear, thin strands falling at the temple", 134286);

        // Roegadyn, male - 54 entries. Sea Wolf and Hellsguard share the icon set
        S("zurückgestrichen, spitz", "swept back, pointed",
          "das Deckhaar glatt nach hinten gestrichen, spitze Franse über der Stirn, die Spitzen laufen am Ohr aus",
          "the top hair swept smoothly back, a pointed fringe over the forehead, the points running out at the ear", 135001);
        S("wellig, zurückgekämmt", "wavy, swept back",
          "welliges Haar nach hinten gekämmt, weiche Wellen legen sich über das Ohr, Spitzen ringeln im Nacken",
          "wavy hair combed back, soft waves lying over the ear, the points curling at the nape", 135002);
        S("windzerzauste Mähne", "windswept mane",
          "volle Mähne nach hinten und zur Seite geweht, spitze Franse an der Stirn, lange Strähnen bis zum Kiefer",
          "full mane blown back and to the side, a pointed fringe at the forehead, long strands down to the jaw", 135004);
        S("Flechte, stachelig", "braid, spiky",
          "stachelig nach hinten gestelltes Haar, eine flache Flechte zieht sich über den Scheitel, Seiten kurz",
          "hair spiked backwards, a flat braid running across the crown, short sides", 135005);
        S("Undercut, Ohrbüschel", "undercut, ear tuft",
          "kurzes stacheliges Deckhaar, darunter eine ausrasierte Partie und ein eigenes Büschel über dem Ohr",
          "short spiky top hair, a shaved section below it and a separate tuft over the ear", 135007);
        S("hohe Tolle", "high quiff",
          "das Haar vorn zu einer hohen glatten Tolle nach hinten gebürstet, Ohr bedeckt, Spitzen am Kiefer",
          "the hair brushed back into a high smooth quiff at the front, ear covered, points at the jaw", 135008);
        S("kahl", "bald",
          "vollständig kahl geschorener Kopf, weder am Scheitel noch an Schläfen oder Nacken bleibt Haar",
          "completely shaved head, no hair left at the crown, the temples or the nape", 135009);
        S("lange Kinnsträhne", "long chin strand",
          "zottiger Schnitt mit Franse über einem Auge, eine lange spitze Strähne hängt bis unter das Kinn",
          "shaggy cut with a fringe over one eye, a long pointed strand hanging below the chin", 135010);
        S("geflochten, lange Zöpfe", "braided, long plaits",
          "eng am Kopf nach hinten geflochtenes Haar, drei dünne Zöpfe hängen mit kleinen Bändern vor dem Ohr herab",
          "hair braided back close to the head, three thin plaits hanging down in front of the ear with small bands", 135011);
        S("lang, Mittelscheitel", "long, centre parting",
          "langes Haar mit Mittelscheitel, hinten weit zur Seite geweht, eine Strähne fällt vorn bis zum Kinn",
          "long hair with a centre parting, swept wide to the side at the back, one strand falling to the chin", 135012);
        S("stachelig, Ohr frei", "spiky, ears clear",
          "das Deckhaar in getrennte Stacheln nach hinten gestellt, die Seiten kurz, das Ohr bleibt frei",
          "the top hair set back into separate spikes, the sides short, the ear staying clear", 135013);
        S("Mähne, gelbgrüner Schmuck", "mane, yellow-green trim",
          "volle Mähne nach hinten gestrichen, am Scheitel eine Reihe kleiner gedämpft gelbgrüner Zierstücke",
          "full mane swept back, a row of small muted yellow-green ornaments at the crown", 135003);
        S("Mähne, heller Schmuck", "mane, pale trim",
          "volle Mähne nach hinten gestrichen, am Scheitel eine Reihe kleiner heller, farbloser Zierstücke",
          "full mane swept back, a row of small pale, colourless ornaments at the crown", 135014);
        S("Mähne, blauer Schmuck", "mane, blue trim",
          "volle Mähne nach hinten gestrichen, am Scheitel eine Reihe kleiner blauer Zierstücke",
          "full mane swept back, a row of small blue ornaments at the crown", 135015);
        S("Halbglatze, lange Seiten", "receding, long sides",
          "lichtes Deckhaar über die Halbglatze nach hinten gekämmt, die Seitenpartie reicht spitz bis unter den Kiefer",
          "thin top hair combed back over a receding crown, the side section reaching in a point below the jaw", 135006);
        S("Halbglatze, kurze Seiten", "receding, short sides",
          "lichtes Deckhaar über die Halbglatze nach hinten gekämmt, die Seitenpartie endet knapp am Kiefer",
          "thin top hair combed back over a receding crown, the side section ending just at the jaw", 135016);
        S("Glatze, lange Seiten", "bald top, long sides",
          "der Scheitel bleibt völlig kahl, das Haar setzt erst hinter der Schläfe an und fällt spitz unter den Kiefer",
          "the crown stays completely bare, the hair starting only behind the temple and falling in a point below the jaw", 135017);
        S("Glatze, kurze Seiten", "bald top, short sides",
          "der Scheitel bleibt völlig kahl, das Haar setzt hinter der Schläfe an und endet gebogen am Kiefer",
          "the crown stays completely bare, the hair starting behind the temple and ending in a curve at the jaw", 135018);
        S("borstiger Scheitel", "bristly crown",
          "mittellang und zottig, am Scheitel borstig aufgestellt, Franse auf der Stirn, Spitzen stehen im Nacken ab",
          "medium length and shaggy, bristling up at the crown, fringe on the forehead, points standing out at the nape", 135022);
        S("geschoren, kleiner Knoten", "shaved, small topknot",
          "die Seiten und der Nacken kahl geschoren, ein schmaler Streifen Deckhaar endet in einem kleinen Knoten am Scheitel",
          "sides and nape shaved bare, a narrow strip of top hair ending in a small knot at the crown", 135023);
        S("seitlicher Vorhang", "side curtain",
          "glattes mittellanges Haar seitlich gescheitelt, fällt als Vorhang über ein Auge und bis in den Nacken",
          "smooth medium-length hair parted at the side, falling as a curtain over one eye and down to the nape", 135021);
        S("hinters Ohr gestrichen", "tucked behind the ear",
          "mittellanges Haar auf einer Seite hinter das Ohr gestrichen, eine dünne Strähne fällt davor herab",
          "medium-length hair tucked behind the ear on one side, a thin strand falling down in front of it", 135024);
        S("lang, Stirnband", "long, headband",
          "sehr langes glattes Haar über Gesicht und Schultern, ein geflochtenes Band liegt quer über der Stirn",
          "very long straight hair over the face and shoulders, a woven band lying across the forehead", 135025);
        S("Topfschnitt, Wirbel", "bowl cut, cowlick",
          "runder Topfschnitt, vorn steht eine Locke als Wirbel ab, der Nacken kurz auslaufend",
          "rounded bowl cut, a curl standing up at the front as a cowlick, the nape tapering short", 135026);
        S("Franse, Seitensträhnen", "fringe, side strands",
          "gestufter Schnitt mit gerader Franse, dünne lange Strähnen hängen vor dem Ohr bis unter den Kiefer",
          "layered cut with a straight fringe, thin long strands hanging in front of the ear below the jaw", 135027);
        S("gestufte Franse", "layered fringe",
          "zottiger mittellanger Schnitt, dicke gerade Franse über den Brauen, gestufte Spitzen rund um den Kiefer",
          "shaggy medium cut, a thick straight fringe over the brows, layered points all around the jaw", 135029);
        S("glatter Seitenscheitel", "sleek side parting",
          "tiefer Seitenscheitel, das Haar glatt zur Seite und nach hinten gekämmt, Seiten kurz, Ohr frei",
          "deep side parting, the hair combed sleekly to the side and back, short sides, ear clear", 135028);
        S("hoher Fächerzopf", "high fanned ponytail",
          "die Seiten nach hinten gestrichen, am Scheitel ein hoher gefächerter Zopf mit einer Spange am Ansatz",
          "the sides swept back, a high fanned ponytail at the crown with a clasp at its base", 135031);
        S("sehr kurz, anliegend", "very short, close",
          "sehr kurz geschnittenes, flach anliegendes Haar mit gerader Stirnlinie, kurze Koteletten vor dem Ohr",
          "very short hair lying flat with a straight hairline, short sideburns in front of the ear", 135032);
        S("kurz, struppig", "short, choppy",
          "kurzer struppiger Schnitt, ausgefranste kurze Franse über der Stirn, das Ohr bleibt frei",
          "short choppy cut, a short frayed fringe over the forehead, the ear staying clear", 135033);
        S("Franse nach vorn", "fringe brushed forward",
          "kurzes weiches Haar nach vorn über die Stirn gebürstet, gestufte Spitzen reichen bis über das Ohr",
          "short soft hair brushed forward over the forehead, layered points reaching down over the ear", 135034);
        S("flach zurückgekämmt", "combed flat back",
          "das Haar flach von der Stirn nach hinten gekämmt, ohne Volumen, feine Spitzen fransen im Nacken aus",
          "the hair combed flat back from the forehead without volume, fine points fraying at the nape", 135035);
        S("Mittelscheitel, glatt", "centre parting, straight",
          "glattes mittellanges Haar mit Mittelscheitel, fällt beidseitig gerade bis zum Kiefer herab",
          "smooth medium-length hair with a centre parting, falling straight down to the jaw on both sides", 135038);
        S("Spitzen nach außen", "flicked-out ends",
          "gerade Franse über den Brauen, die Seiten stehen breit ab und schwingen am Kiefer nach außen",
          "a straight fringe over the brows, the sides standing out wide and flicking outward at the jaw", 135039);
        S("wild zerzaust", "wildly tousled",
          "stark zerzaustes Haar, in dicken Strähnen zur Seite geworfen, lange Spitzen fallen über die Braue",
          "heavily tousled hair thrown to the side in thick strands, long points falling over the brow", 135040);
        S("stachelig, Stirnsträhnen", "spiky, forehead strands",
          "hoch nach hinten gestelltes stacheliges Haar, einzelne glatte Strähnen liegen quer über der Stirn",
          "spiky hair set high and swept back, a few smooth strands lying across the forehead", 135041);
        S("Franse, tiefer Zopf", "fringe, low ponytail",
          "gerade Franse über den Brauen, das übrige Haar im Nacken zu einem tiefen Zopf gefasst",
          "a straight fringe over the brows, the rest of the hair gathered into a low ponytail at the nape", 135042);
        S("Franse, zottig", "fringe, shaggy",
          "gerade Franse, darunter zottig gestuftes Haar, das seitlich bis unter den Kiefer reicht",
          "a straight fringe, below it shaggy layered hair reaching below the jaw at the sides", 135043);
        S("geflochtener Nackenzopf", "braided nape tail",
          "kurzes stacheliges Deckhaar nach hinten, Seiten eng anliegend, im Nacken ein schmaler geflochtener Zopf mit Band",
          "short spiky top hair swept back, sides close-cropped, a narrow braided tail with a band at the nape", 135044);
        S("mächtige Tolle", "huge pompadour",
          "eine mächtige glatte Tolle wölbt sich hoch über die Stirn, Seiten und Nacken kurz und stachelig",
          "a huge smooth pompadour arching high over the forehead, sides and nape short and spiky", 135045);
        S("stachelig, lange Seiten", "spiky top, long sides",
          "spitz aufgestelltes Deckhaar, darunter lange gestufte Seiten, die spitz bis zum Kiefer fallen",
          "top hair spiked upward, long layered sides beneath falling in points to the jaw", 135046);
        S("schwere Franse", "heavy fringe",
          "runder mittellanger Schnitt, die schwere Franse verdeckt ein Auge ganz, hinten steht ein Büschel ab",
          "rounded medium cut, the heavy fringe completely covering one eye, a tuft standing out at the back", 135048);
        S("geteilte Franse", "parted fringe",
          "weiches mittellanges Haar, die Franse über der Stirn geteilt, die Spitzen liegen glatt über dem Ohr",
          "soft medium-length hair, the fringe parted over the forehead, the points lying smooth over the ear", 135049);
        S("rasierte Seite, Mähne", "shaved side, mane",
          "eine Seite bis auf Stoppeln ausrasiert, darüber legt sich eine lange stachelige Mähne nach hinten",
          "one side shaved down to stubble, a long spiky mane laid back over it", 135050);
        S("Franse, langer Nacken", "fringe, long nape",
          "glatte gerade Franse, seitlich kinnlang, im Nacken bleibt eine längere schmale Partie stehen",
          "smooth straight fringe, chin-length at the sides, a longer narrow section left standing at the nape", 135052);
        S("zerfranste Spitzen", "frayed ends",
          "seitlich gescheiteltes Haar, die lange Franse fällt über ein Auge, die Spitzen sind stark ausgefranst",
          "hair parted at the side, the long fringe falling over one eye, the points heavily frayed", 135053);
        S("kurze Locken", "short curls",
          "sehr kurz geschnittenes, dicht gekräuseltes Haar, das eng am Kopf anliegt, die Ohren bleiben frei",
          "very short, densely curled hair lying close to the head, the ears staying clear", 135056);
        S("schräge Franse", "sweeping fringe",
          "das Deckhaar glatt zur Seite gelegt, die lange Franse deckt ein Auge, der Nacken läuft kurz aus",
          "the top hair laid smoothly to one side, the long fringe covering one eye, the nape tapering short", 135057);
        S("hoher Stachelzopf", "high spiky ponytail",
          "die Schläfen kurz geschnitten, am Scheitel ein hoher stacheliger Zopf, zwei dünne Strähnen fallen vorn herab",
          "temples cut short, a high spiky ponytail at the crown, two thin strands falling forward", 135058);
        S("zwei Knoten", "two buns",
          "das Haar oben hinten zu zwei kleinen Knoten gedreht, lange Strähnen fallen vor dem Ohr herab",
          "the hair twisted into two small buns high at the back, long strands falling down in front of the ear", 135059);
        S("glatter Bob", "smooth bob",
          "glatter kinnlanger Bob mit gerader Franse, rundum gleich lang, die Ohren bleiben bedeckt",
          "smooth chin-length bob with a straight fringe, the same length all round, the ears staying covered", 135060);
        S("Franse, sehr lang", "fringe, very long",
          "gerade Franse über den Brauen, das übrige Haar fällt glatt weit über die Schultern hinab",
          "a straight fringe over the brows, the rest of the hair falling straight far past the shoulders", 135061);
        S("zottig, kinnlang", "shaggy, chin-length",
          "gleichmäßig kinnlanges zottiges Haar, die Franse verdeckt ein Auge, die Ohren bleiben bedeckt",
          "evenly chin-length shaggy hair, the fringe covering one eye, the ears staying covered", 135069);
        S("zurückgebunden, Strähnen", "tied back, strands",
          "das lange Haar glatt nach hinten gestrichen und im Nacken gebunden, dünne Strähnen hängen an der Schläfe",
          "the long hair swept smoothly back and tied at the nape, thin strands hanging at the temple", 135086);

        // Roegadyn, female - 48 entries. Sea Wolf and Hellsguard share the icon set
        S("ausschwingender Bob", "flicked-out bob",
          "kinnlanger Bob mit Seitenscheitel, das Haar schwingt nach hinten und die Spitzen fliegen nach außen",
          "chin-length bob with a side parting, sweeping back with the ends flicking outward", 135201);
        S("stachelig, langer Nacken", "spiky, long nape",
          "kurz und stachelig oben, gezackte Franse auf der Stirn, im Nacken fallen längere Strähnen bis zum Hals",
          "short and spiky on top, jagged fringe on the forehead, longer strands falling to the neck at the nape", 135202);
        S("hochgekämmt, gewellt", "swept up, wavy",
          "das Deckhaar nach oben und hinten gekämmt, gewellte Längen fallen seitlich bis zum Kiefer, Ohr frei",
          "the top hair combed up and back, wavy lengths falling to the jaw at the sides, ear free", 135203);
        S("stachliger Schopf hinten", "spiky tuft behind",
          "kurz und stufig, seitlich eine geflochtene Partie, am Hinterkopf steht ein kleiner stachliger Schopf ab",
          "short and layered, a plaited section at the side, a small spiky tuft standing up at the back", 135204);
        S("Stirnband", "headband",
          "breites glattes Stirnband über dem Scheitel, das kurze Haar nach hinten gestrichen, dahinter eine Flechte",
          "a broad smooth headband over the crown, the short hair swept back, a braid behind it", 135205);
        S("stachelig, lange Mähne", "spiky, long mane",
          "oben stachelig aufgerichtet, die langen glatten Längen fallen hinten über die Schultern",
          "spiked up on top, the long straight lengths falling over the shoulders at the back", 135206);
        S("abstehende Spitzen", "flared spikes",
          "kurzer Schnitt, oben glatt gescheitelt, hinten und seitlich stehen die Spitzen weit ab",
          "short cut, smooth parting on top, the points standing far out at the back and sides", 135207);
        S("lang, tiefer Seitenscheitel", "long, deep parting",
          "langes glattes Haar mit tiefem Seitenscheitel, eine breite Partie fällt über Stirn und Auge",
          "long straight hair with a deep side parting, a broad section falling over the forehead and eye", 135208);
        S("glatter Bob", "sleek bob",
          "glatter kinnlanger Bob mit Seitenscheitel, eine Seite liegt flach auf der Wange, die andere gibt das Ohr frei",
          "sleek chin-length bob with a side parting, one side lying flat on the cheek, the other leaving the ear free", 135209);
        S("sehr kurz, struppig", "very short, tousled",
          "sehr kurzer Schnitt, ringsum struppig ausgedünnt, kurze Fransen auf der Stirn, im Nacken feine Strähnen",
          "very short cut, thinned and tousled all round, short fringe pieces on the forehead, fine strands at the nape", 135210);
        S("Bob mit Haarnadeln", "bob with hairpins",
          "kinnlanger Bob, gescheitelt, über dem Ohr stecken mehrere gerade Haarnadeln fächerförmig im Haar",
          "chin-length bob, parted, several straight hairpins set in a fan above the ear", 135211);
        S("zurückgekämmt, Seitenflechte", "swept-back, side braid",
          "das Haar glatt nach hinten gestrichen, eine Flechte läuft über dem Ohr entlang und hängt bis zum Kiefer",
          "hair smoothed straight back, a braid running above the ear and hanging down to the jaw", 135212);
        S("hoher Seitenzopf", "high side ponytail",
          "hoch am Kopf seitlich zusammengebunden, gerade Franse über den Brauen, der Zopf fällt bis zur Schulter",
          "tied high at the side of the head, straight fringe over the brows, the ponytail falling to the shoulder", 135216);
        S("Undercut, lange Seite", "undercut, long sweep",
          "eine Seite kurz geschoren und das Ohr frei, das lange Deckhaar in Wellen über die andere Seite gelegt",
          "one side cropped short with the ear free, the long top hair laid in waves over the other side", 135217);
        S("lang, weicher Pony", "long, soft fringe",
          "langes glattes Haar mit voller Franse über den Brauen, die Längen fallen weich über beide Schultern",
          "long straight hair with a full fringe over the brows, the lengths falling softly over both shoulders", 135215);
        S("lang ohne Pony", "long, no fringe",
          "langes Haar ohne Franse, vorn aus dem Gesicht nach hinten gestrichen, Ohr frei, Längen über die Schultern",
          "long hair without a fringe, swept back off the face at the front, ear free, lengths past the shoulders", 135218);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "lange gedrehte Rastalocken mit Metallringen, fallen vorn beidseitig über Gesicht und Schultern",
          "long twisted dreadlocks with metal rings, falling forward on both sides over face and shoulders", 135219);
        S("glatter Pferdeschwanz", "sleek ponytail",
          "glattes Deckhaar mit Seitenfranse, hinten hoch zusammengebunden, der lange Zopf fällt gerade herab",
          "smooth top hair with a side-swept fringe, tied high at the back, the long ponytail falling straight down", 135220);
        S("Haarreif, langes Haar", "hairband, long hair",
          "schmaler Haarreif über dem Scheitel, Franse über der Stirn, das lange glatte Haar fällt über die Schultern",
          "narrow hairband over the crown, fringe over the forehead, the long straight hair falling over the shoulders", 135221);
        S("kurz, ausgefranst", "short, frayed",
          "kurzer stufiger Schnitt, ausgefranste Strähnen über der Stirn, längere Spitzen vor dem Ohr und im Nacken",
          "short layered cut, frayed strands over the forehead, longer points in front of the ear and at the nape", 135223);
        S("zwei Zöpfe", "two braids",
          "zwei Zöpfe hängen hinter den Ohren nach vorn über die Schultern, dazu eine Seitenfranse über der Braue",
          "two braids hanging forward from behind the ears over the shoulders, with a side-swept fringe over the brow", 135222);
        S("Pferdeschwanz mit Pony", "ponytail with fringe",
          "gerade Franse über den Brauen, lange Strähnen am Gesicht, das übrige Haar hoch zum Pferdeschwanz gebunden",
          "straight fringe over the brows, long strands at the face, the rest tied high into a ponytail", 135225);
        S("lang gewellt, Mittelscheitel", "long waves, centre-parted",
          "langes gewelltes Haar mit Mittelscheitel, Stirn frei, die Wellen fallen über beide Schultern",
          "long wavy hair with a centre parting, forehead free, the waves falling over both shoulders", 135226);
        S("lang gewellt, Seitenpony", "long waves, side-swept",
          "langes gewelltes Haar, seitlich gescheitelt, eine Fransenpartie streicht schräg über die Stirn",
          "long wavy hair, side-parted, a sweep of fringe running diagonally across the forehead", 135227);
        S("kurz, weiche Franse", "short, soft fringe",
          "kurzer Schnitt mit Seitenscheitel, das Deckhaar weich bis zur Braue nach vorn gekämmt, Ohr frei",
          "short cut with a side parting, the top hair combed softly forward to the brow, ear free", 135228);
        S("kurz, Stirn frei", "short, open forehead",
          "kurzer Schnitt, gescheitelt und zu beiden Seiten glatt herabfallend, Stirn frei, im Nacken gezackte Spitzen",
          "short cut, parted and falling smoothly to both sides, forehead free, jagged points at the nape", 135229);
        S("mittellang, zottig", "medium-length, shaggy",
          "mittellang und zottig gestuft, Stirn frei gescheitelt, die zerfransten Längen reichen bis in den Nacken",
          "medium length and shaggily layered, parted off the forehead, the frayed lengths reaching the nape", 135233);
        S("zottiger Bob", "shaggy bob",
          "kinnlanger zottiger Bob, dichte gezackte Franse über der Stirn, die Stufen stehen seitlich ab",
          "chin-length shaggy bob, thick jagged fringe over the forehead, the layers standing out at the sides", 135234);
        S("Pony übers Auge", "fringe over eye",
          "stufiger Schnitt, eine lange Ponypartie fällt schräg über Stirn und Auge bis zur Wange, oben stachelig",
          "layered cut, a long fringe falling diagonally over forehead and eye to the cheek, spiky on top", 135235);
        S("nach hinten gestachelt", "spiked back",
          "das Haar nach hinten gekämmt und in Spitzen aufgerichtet, einzelne feine Strähnen hängen in die Stirn",
          "hair combed back and standing up in points, a few fine strands hanging onto the forehead", 135236);
        S("dicke gedrehte Zöpfe", "thick twisted braids",
          "gerade Franse über den Brauen, beidseitig fallen dicke gedrehte Zöpfe bis über die Schultern",
          "straight fringe over the brows, thick twisted braids falling past the shoulders on both sides", 135237);
        S("Bob mit Franse", "bob with fringe",
          "kinnlanger gestufter Bob mit Franse über den Brauen, die Spitzen biegen sich am Kiefer nach außen",
          "chin-length layered bob with a fringe over the brows, the ends bending outward at the jaw", 135238);
        S("zurückgekämmt, Nackenzopf", "swept-back, nape braid",
          "das Deckhaar voluminös nach hinten gekämmt, eine Strähne fällt vor dem Ohr, im Nacken ein gedrehter Zopf",
          "the top hair combed back with volume, a strand in front of the ear, a twisted braid at the nape", 135239);
        S("hohe Tolle", "tall pompadour",
          "das Deckhaar zu einer hohen runden Tolle über der Stirn aufgetürmt, Seiten glatt zurück, Nacken ausgefranst",
          "the top hair piled into a tall round pompadour above the forehead, sides smoothed back, frayed nape", 135240);
        S("zerzaust, Stachel oben", "tousled, top spike",
          "zerzaust gestufter Schnitt, am Scheitel steht eine Strähne stachelig ab, lange Spitzen bis zum Kiefer",
          "tousled layered cut, a strand standing up spikily at the crown, long points down to the jaw", 135241);
        S("kurz, tiefer Scheitel", "short, deep parting",
          "kurzer glatter Schnitt mit tiefem Seitenscheitel, das Deckhaar schräg über die Stirn, hinten eingedreht",
          "short sleek cut with a deep side parting, the top hair slanting across the forehead, rolled in at the back", 135243);
        S("kinnlang gestuft", "layered, chin-length",
          "kinnlanger gestufter Schnitt, das Haar seitlich gescheitelt und glatt bis zum Kiefer fallend",
          "chin-length layered cut, parted at the side and falling smoothly to the jaw", 135244);
        S("geschorene Seite, Mähne", "shaved side, mane",
          "eine Seite bis auf Stoppeln geschoren, oben ein stachliger Kamm, im Nacken fällt eine lange zottige Mähne",
          "one side shaved to stubble, a spiky crest on top, a long shaggy mane falling at the nape", 135245);
        S("Pony, Ohr frei", "fringe, ear free",
          "kinnlanger glatter Schnitt mit gerader Franse, eine Seite hinters Ohr gelegt, hinten eine längere Strähne",
          "chin-length straight cut with a straight fringe, one side tucked behind the ear, a longer strand at the back", 135247);
        S("zottig, Seitenpony", "shaggy, side fringe",
          "kinnlanger zottiger Schnitt, eine spitze Ponysträhne läuft quer über die Stirn, die Spitzen stehen ab",
          "chin-length shaggy cut, a pointed fringe strand crossing the forehead, the ends standing out", 135248);
        S("kurze Locken", "short curls",
          "sehr kurzer Schnitt, das Haar dicht gekräuselt und eng am Kopf, Ohren und Stirn frei",
          "very short cut, densely curled and close to the head, ears and forehead free", 135251);
        S("spitzer Seitenpony", "pointed side fringe",
          "kurzer Schnitt, eine lange spitze Ponysträhne fällt schräg über die Stirn bis zum Kiefer, hinten kurz",
          "short cut, a long pointed fringe strand falling diagonally across the forehead to the jaw, short at the back", 135252);
        S("rasiert, Stachelzopf", "shaved, spiky ponytail",
          "seitlich kurz rasiert, das Deckhaar hoch zu einem großen stachligen Zopf gebunden, vorn hängt eine lange Strähne",
          "sides shaved short, the top hair tied high into a big spiky ponytail, a long strand hanging at the front", 135253);
        S("hoher Knoten", "high bun",
          "hoch am Hinterkopf zu einem Knoten gewickelt, lange Strähnen fallen daraus herab, Seitenfranse über der Stirn",
          "wound into a knot high at the back, long strands falling from it, side fringe over the forehead", 135254);
        S("runder Kurzbob", "round short bob",
          "kurzer runder Bob, glatt anliegend mit Franse über den Brauen, die Spitzen enden am Ohr",
          "short round bob, lying smooth with a fringe over the brows, the ends finishing at the ear", 135255);
        S("Blockpony, lang", "blunt fringe, long",
          "gerade abgeschnittene Franse über den Brauen, kurze Seitensträhnen am Kiefer, das übrige Haar sehr lang",
          "bluntly cut fringe over the brows, short side locks at the jaw, the rest of the hair very long", 135256);
        S("kurz, federig", "short, feathery",
          "kurzer federiger Schnitt, die Franse reicht bis über die Augen, ringsum gezackte weiche Spitzen",
          "short feathery cut, the fringe reaching over the eyes, soft jagged points all round", 135264);
        S("streng zurückgekämmt", "slicked straight back",
          "das Haar glatt nach hinten gestrichen, oben voluminös, eine einzelne Strähne hängt in die Stirn",
          "hair smoothed straight back, voluminous on top, a single strand hanging onto the forehead", 135282);

        // Au Ra, male - 47 entries. Raen and Xaela share the icon set
        S("hoch aufgetürmt, stachelig", "piled high, spiky",
          "das Haar hoch aufgetürmt und stachelig nach hinten, eine lange Strähne fällt vor dem Ohr herab",
          "hair piled high and spiky towards the back, one long strand falling in front of the ear", 136001);
        S("Flechte mit Perlen", "braid with beads",
          "langes Haar nach hinten gestrichen, seitlich eine schmale Flechte, die in Perlen endet",
          "long hair swept back, a narrow braid at the side ending in beads", 136002);
        S("wilde Mähne", "wild mane",
          "zottige Mähne mit abstehenden Spitzen, schwere Franse über der Braue, kinnlange Seiten",
          "shaggy mane with spikes standing out, a heavy fringe over the brow, chin-length sides", 136003);
        S("lang glatt, Zierspangen", "long straight, clips",
          "langes glattes Haar mit Mittelscheitel, zwei kleine Zierspangen sitzen seitlich am Scheitel",
          "long straight hair with a centre parting, two small ornamental clips at the side of the crown", 136004);
        S("hoher Knoten", "high bun",
          "das Haar hoch am Hinterkopf zu einem Knoten gebunden, lange Strähnen fallen vorn ums Gesicht",
          "the hair bound into a knot high at the back, long strands falling around the face in front", 136005);
        S("aufgestellte Spitzen, lang", "raised spikes, long",
          "das Deckhaar in hohen geschwungenen Spitzen aufgestellt, hinten fällt das Haar lang über die Schultern",
          "the top hair raised in tall curving spikes, the back falling long over the shoulders", 136006);
        S("Stachelschopf, Seitenflechte", "spiky crest, side braid",
          "vorn ein hoher stacheliger Schopf, eine schmale Flechte verläuft seitlich über dem Ohr",
          "a tall spiky crest at the front, a narrow braid running along the side above the ear", 136007);
        S("windzerzaust nach hinten", "windswept back",
          "langes Haar ganz aus der Stirn nach hinten gestrichen und windzerzaust nach außen wehend",
          "long hair swept fully back off the forehead and blowing outwards as if windswept", 136008);
        S("Seitenscheitel, kinnlang", "side part, chin length",
          "kinnlanger Schnitt mit Seitenscheitel, eine breite Strähne fällt über die Schläfe, Ohren bedeckt",
          "chin-length cut with a side parting, a broad lock over the temple, ears covered", 136009);
        S("gestufte Stirnfranse", "layered fringe",
          "kinnlanges Haar, die Franse fällt in einzelnen gestuften Strähnen über die Braue",
          "chin-length hair, the fringe falling in separate layered strands over the brow", 136010);
        S("weich nach vorn", "softly forward",
          "kurzer weicher Schnitt, nach vorn gebürstet, feine Strähnen auf der Stirn, Ohren bedeckt",
          "short soft cut brushed forward, fine strands on the forehead, ears covered", 136011);
        S("glatt zurück, schulterlang", "smooth back, shoulder length",
          "das Haar glatt aus der Stirn nach hinten gestrichen, hinten bis auf die Schulter fallend",
          "hair combed smoothly back off the forehead, falling to the shoulder behind", 136012);
        S("kurz, feine Stacheln", "short, fine spikes",
          "kurzes Haar nach oben und hinten gestrichen, in vielen feinen Stacheln auslaufend",
          "short hair swept up and back, ending in many fine spikes", 136016);
        S("Mittelscheitel, schulterlang", "centre part, shoulder length",
          "glattes Haar mit Mittelscheitel, dünne Strähnen rahmen das Gesicht, die Länge reicht bis zur Schulter",
          "straight hair with a centre parting, thin strands framing the face, the length reaching the shoulder", 136017);
        S("Einzelsträhne am Scheitel", "single strand at crown",
          "mittellang und gestuft, die Franse fällt über ein Auge, am Scheitel steht eine feine Strähne ab",
          "medium and layered, the fringe falling over one eye, a fine strand standing up at the crown", 136015);
        S("lang, Stirnband", "long, headband",
          "langes glattes Haar, ein schmales geflochtenes Band verläuft quer über die Stirn",
          "long straight hair, a narrow braided band running across the forehead", 136018);
        S("kurze Wellen, Stirnlocke", "short waves, forelock",
          "kurzes gewelltes Haar nach hinten gekämmt, vorn ringelt sich eine kleine Locke am Haaransatz",
          "short wavy hair combed back, a small curl coiling at the hairline in front", 136019);
        S("lange Seitenfranse", "long side fringe",
          "das Deckhaar nach hinten gestrichen, eine lange Franse fällt schräg über die Braue, Nacken kurz",
          "the top hair swept back, a long fringe falling slantwise over the brow, short at the nape", 136020);
        S("zottig, spitze Franse", "shaggy, pointed fringe",
          "zottiger Schnitt, die Franse endet in einzelnen Spitzen über der Stirn, Ohren bedeckt",
          "shaggy cut, the fringe ending in separate points over the forehead, ears covered", 136022);
        S("voluminös zurückgekämmt", "voluminous, combed back",
          "kurzes Haar voluminös nach hinten gekämmt, vorn deutlich aufgebauscht, Nacken kurz",
          "short hair combed back with volume, clearly puffed up at the front, short at the nape", 136021);
        S("Fächerschopf mit Spange", "fanned crest with clasp",
          "die Seiten kurz nach hinten gelegt, das Deckhaar mit einer Spange zu einem fächerartigen Schopf gebunden",
          "the sides laid short to the back, the top hair bound with a clasp into a fanned crest", 136024);
        S("kurz, streng zurück", "short, slicked back",
          "sehr kurzes Haar eng am Kopf glatt nach hinten gekämmt, die Stirn bleibt frei",
          "very short hair combed flat back close to the head, the forehead left free", 136025);
        S("kurz, ausgefranste Spitzen", "short, ragged tips",
          "kurzer Schnitt mit ausgefransten Spitzen, leicht nach vorn gebürstet, hoher Haaransatz",
          "short cut with ragged tips, brushed slightly forward, high hairline", 136026);
        S("weich zur Seite", "softly to the side",
          "kurzes Haar weich nach hinten und zur Seite gelegt, ohne Franse, die Stirn frei",
          "short hair laid softly back and to one side, no fringe, the forehead free", 136027);
        S("tiefer Seitenscheitel", "deep side part",
          "mittellanges Haar mit tiefem Seitenscheitel, quer über den Kopf gelegt, Spitzen am Kiefer",
          "medium hair with a deep side parting, laid across the head, ends at the jaw", 136028);
        S("gescheitelt, ausgestellt", "parted, flicked out",
          "mittellanges gescheiteltes Haar, feine Strähnen auf der Stirn, die Spitzen stellen sich im Nacken nach außen",
          "medium parted hair, fine strands on the forehead, the ends flicking outwards at the nape", 136031);
        S("volle Stirnfranse", "full fringe",
          "eine volle gerade Franse bis zur Braue, das übrige Haar gestuft und im Nacken nach außen gedreht",
          "a full straight fringe down to the brow, the rest layered and turned outwards at the nape", 136032);
        S("zerzaust, abstehender Wirbel", "tousled, standing tuft",
          "mittellanges zerzaustes Haar, am Scheitel steht eine Partie ab, feine Strähnen an den Schläfen",
          "medium tousled hair, a section standing up at the crown, fine strands at the temples", 136033);
        S("zurückgekämmt, Stirnsträhnen", "swept back, forehead strands",
          "das Haar voluminös nach hinten gekämmt, einige feine Strähnen fallen wieder auf die Stirn",
          "hair combed back with volume, a few fine strands falling back onto the forehead", 136034);
        S("gerade Franse, glatt", "straight fringe, sleek",
          "glattes Haar mit gerade geschnittener Franse über der Braue, die Seiten glatt nach hinten gelegt",
          "sleek hair with a straight-cut fringe above the brow, the sides laid smoothly back", 136035);
        S("runder Bob, ausgefranst", "round bob, ragged",
          "runder kinnlanger Bob mit Franse, die Spitzen leicht ausgefranst, am Scheitel ein kleiner Wirbel",
          "rounded chin-length bob with a fringe, slightly ragged ends, a small cowlick at the crown", 136036);
        S("lange Seitensträhne", "long side strand",
          "das Deckhaar nach hinten gestrichen, eine Spitze fällt auf die Stirn, seitlich hängt eine lange Strähne herab",
          "the top hair swept back, a point falling onto the forehead, a long strand hanging at the side", 136037);
        S("hohe Tolle", "towering pompadour",
          "eine mächtige hohe Tolle über der Stirn aufgerollt, die Seiten flach nach hinten, Nacken kurz",
          "a massive high pompadour rolled up above the forehead, the sides flat to the back, short nape", 136038);
        S("gestuft, spitze Strähnen", "layered, spiky strands",
          "mittellang und stark gestuft, spitz auslaufende Strähnen an Schläfe und Scheitel",
          "medium and heavily layered, sharply pointed strands at the temple and crown", 136039);
        S("kurz, quer gelegt", "short, swept across",
          "kurzes Haar von einem Scheitel quer über den Kopf gelegt, hinten ein kleiner Schwung, Stirn frei",
          "short hair laid across the head from a parting, a small flick at the back, forehead free", 136041);
        S("Mittelscheitel, kinnlang", "centre part, chin length",
          "glattes Haar mit Mittelscheitel, fällt beidseitig gerade bis zum Kiefer herab",
          "straight hair with a centre parting, falling straight down to the jaw on both sides", 136042);
        S("Undercut, zurückgestrichen", "undercut, swept back",
          "die Seiten sehr kurz geschoren, das lange Deckhaar stachelig nach hinten bis in den Nacken gestrichen",
          "the sides shorn very short, the long top hair swept spikily back down to the nape", 136043);
        S("weiche Franse, halblang", "soft fringe, medium",
          "glattes mittellanges Haar mit weicher Franse über der Stirn, die Länge reicht über den Kiefer hinaus",
          "sleek medium hair with a soft fringe over the forehead, the length reaching past the jaw", 136045);
        S("Franse übers Auge", "fringe across the eye",
          "aus einem Seitenscheitel streicht die lange Franse quer über die Stirn und verdeckt ein Auge",
          "from a side parting the long fringe sweeps across the forehead and covers one eye", 136046);
        S("sehr kurz, borstig", "very short, bristly",
          "sehr kurz geschnittenes Haar, dicht und borstig aufrecht stehend, die Stirn ganz frei",
          "very short cut hair, dense and bristly standing upright, the forehead fully free", 136049);
        S("Seitenwelle mit Spitze", "side wave with point",
          "das Deckhaar breit zur Seite gelegt und über die Braue fallend, am Hinterkopf hebt sich eine Spitze",
          "the top hair laid broadly to one side and falling over the brow, a point lifting at the back", 136050);
        S("stacheliger Hochzopf", "spiky topknot",
          "die Seiten kurz, das Haar hoch am Hinterkopf zu einem stacheligen Zopf gebunden, vorn eine lange Strähne",
          "the sides short, the hair bound high at the back into a spiky ponytail, one long strand in front", 136051);
        S("kleiner Knoten, offen", "small bun, hair loose",
          "ein kleiner runder Knoten am Hinterkopf, das übrige lange Haar hängt offen herab",
          "a small round bun at the back of the head, the rest of the long hair hanging loose", 136052);
        S("glatter Bob", "sleek bob",
          "glatter kinnlanger Bob, die weiche Franse aus einem Seitenscheitel über die Stirn gebürstet",
          "sleek chin-length bob, the soft fringe brushed over the forehead from a side parting", 136053);
        S("sehr lang, Blockfranse", "very long, blunt fringe",
          "sehr langes glattes Haar mit gerade geschnittener Franse, fällt beidseitig weit über die Brust",
          "very long straight hair with a straight-cut fringe, falling far over the chest on both sides", 136054);
        S("struppig, schwere Franse", "tousled, heavy fringe",
          "struppiges kinnlanges Haar, die schwere Franse fällt in dicken Strähnen über die Braue",
          "tousled chin-length hair, the heavy fringe falling in thick strands over the brow", 136062);
        S("zurückgewellt, zwei Strähnen", "waved back, two strands",
          "das Haar in weichen Wellen nach hinten gestrichen, zwei lange Strähnen fallen an der Schläfe herab",
          "hair swept back in soft waves, two long strands falling down at the temple", 136079);

        // Au Ra, female - 47 entries. Raen and Xaela share the icon set
        S("zurückgestrichen, Spange", "swept back, clasp",
          "vom Seitenscheitel glatt nach hinten gestrichen, seitlich eine kantige Spange, lange Strähnen fallen vors Ohr",
          "swept smoothly back from a side parting, an angular clasp at the side, long strands in front of the ear", 136201);
        S("Blattfächer seitlich", "leaf fan at the side",
          "langes Haar vom tiefen Seitenscheitel glatt zur Seite gelegt, hinten ein Fächer aus spitzen Blättern",
          "long hair laid smoothly to one side from a deep side parting, a fan of pointed leaves at the back", 136202);
        S("Blüten, Blockfranse", "blossoms, blunt fringe",
          "gerade Blockfranse, schulterlanges Haar, an beiden Schläfen ein Blütenschmuck ins Haar gesteckt",
          "straight blunt fringe, shoulder-length hair, a blossom ornament pinned into the hair at each temple", 136203);
        S("lang, zwei Spangen", "long, two clips",
          "langes glattes Haar mit weicher Franse, an beiden Schläfen je eine kleine Haarspange",
          "long straight hair with a soft fringe, a small hair clip at each temple", 136204);
        S("Bob, Augenfranse", "bob, fringe over the eye",
          "kinnlanger Bob, die lange Franse fällt schräg über ein Auge, Spitzen schwingen am Kiefer aus",
          "chin-length bob, the long fringe falling diagonally over one eye, ends flicking out at the jaw", 136205);
        S("kurz, lange Seitensträhnen", "short, long side strands",
          "gerade Franse, hinten kurz und ausgestellt, zwei sehr lange Strähnen fallen vorn über die Schultern",
          "straight fringe, short and flicked out at the back, two very long strands falling forward over the shoulders", 136206);
        S("Bob mit Blüten", "bob with blossoms",
          "runder kinnlanger Bob mit Seitenfranse, beidseitig eine große Blüte ins Haar gesteckt",
          "round chin-length bob with a side fringe, a large blossom pinned into the hair on each side", 136207);
        S("sehr lang, glatt", "very long, straight",
          "sehr langes glattes Haar mit voller Franse, fällt schwer über beide Schultern bis auf die Brust",
          "very long straight hair with a full fringe, falling heavily over both shoulders to the chest", 136208);
        S("lang, Schrägfranse", "long, swept fringe",
          "langes glattes Haar vom Seitenscheitel, die Franse fegt schräg über eine Braue",
          "long straight hair from a side parting, the fringe sweeping diagonally across one brow", 136209);
        S("Mittelscheitel, schulterlang", "centre part, shoulder length",
          "Mittelscheitel mit freier Stirn, schulterlanges Haar, die Spitzen leicht nach außen gewellt",
          "centre parting with a bare forehead, shoulder-length hair, the ends slightly waved outward", 136210);
        S("Franse, tiefer Zopf", "fringe, low tail",
          "gerade Franse, das Haar im Nacken zu einem kurzen Zopf gefasst, dünne Strähnen vor den Ohren",
          "straight fringe, the hair gathered into a short tail at the nape, thin strands in front of the ears", 136211);
        S("Bob mit Haarreif", "bob with headband",
          "kinnlanger Bob mit Franse, ein breiter glatter Haarreif liegt über dem Scheitel",
          "chin-length bob with a fringe, a broad smooth headband lying over the crown", 136212);
        S("hoher Seitenzopf", "high side ponytail",
          "gerade Franse, seitlich hoch mit einer kleinen Spange gebunden, der Zopf fällt lang über die Schulter",
          "straight fringe, tied up high at the side with a small clasp, the ponytail falling long over the shoulder", 136216);
        S("lang, zurückgestrichen", "long, swept back",
          "langes glattes Haar ohne Franse, vom Scheitel weit aus der Stirn nach hinten gestrichen",
          "long straight hair without a fringe, swept far back off the forehead from the parting", 136217);
        S("Franse, gewellte Spitzen", "fringe, waved ends",
          "weiche Franse, schulterlanges Haar, die Spitzen wellen sich locker nach außen",
          "soft fringe, shoulder-length hair, the ends waving loosely outward", 136215);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "dicke gedrehte Rastalocken, oben eng am Kopf gelegt, mit Metallringen besetzt, fallen vorn beidseitig herab",
          "thick twisted dreadlocks, laid close to the head on top, set with metal rings, falling forward on both sides", 136218);
        S("Zopf, gefächert", "ponytail, fanned base",
          "glatt nach hinten gestrichen und am Hinterkopf hoch gebunden, der Ansatz fächert auf, die Länge fällt gerade herab",
          "swept smoothly back and tied high at the back, the base fanning out, the length falling straight down", 136219);
        S("Haarreif, lang", "headband, long",
          "langes Haar mit Seitenfranse, ein breiter glatter Haarreif hält es aus der Stirn",
          "long hair with a side fringe, a broad smooth headband holding it off the forehead", 136220);
        S("kurz, zerzaust", "short, tousled",
          "kurzer stufiger Schnitt, spitze Strähnen fallen zerzaust über die Stirn, Nacken kurz",
          "short layered cut, pointed strands falling tousled over the forehead, short at the nape", 136222);
        S("zwei Zöpfe", "two braids",
          "weiche Franse, das Haar zu zwei langen Flechten gefasst, die vorn über beide Schultern fallen",
          "soft fringe, the hair gathered into two long braids falling forward over both shoulders", 136221);
        S("Pferdeschwanz, Blockfranse", "ponytail, blunt fringe",
          "gerade Blockfranse, das Haar am Oberkopf zu einem hohen Pferdeschwanz gebunden, lange Strähnen rahmen das Gesicht",
          "straight blunt fringe, the hair tied into a high ponytail at the crown, long strands framing the face", 136224);
        S("Mittelscheitel, lange Wellen", "centre part, long waves",
          "Mittelscheitel ohne Franse, langes Haar fällt in weichen Wellen über beide Schultern",
          "centre parting without a fringe, long hair falling in soft waves over both shoulders", 136225);
        S("Seitenscheitel, Wellen", "side part, waves",
          "tiefer Seitenscheitel, eine breite Partie fegt über die Stirn, langes gewelltes Haar bis unter die Schultern",
          "deep side parting, a broad section sweeping across the forehead, long wavy hair below the shoulders", 136226);
        S("kurz, glatt zurück", "short, smoothed back",
          "kurzer Schnitt, das Deckhaar vom Seitenscheitel flach nach hinten gelegt, Nacken kurz",
          "short cut, the top hair laid flat back from a side parting, short at the nape", 136227);
        S("Mittelscheitel, kinnlang", "centre part, chin length",
          "kurzes Haar mittig gescheitelt und nach hinten geführt, die Spitzen enden am Kiefer",
          "short hair parted in the middle and led back, the ends finishing at the jaw", 136228);
        S("Mittelscheitel, Wangensträhnen", "centre part, cheek strands",
          "kurzes mittig gescheiteltes Haar, mehrere feine Strähnen hängen vor den Ohren an den Wangen herab",
          "short hair parted in the middle, several fine strands hanging down the cheeks in front of the ears", 136232);
        S("zottiger Bob", "shaggy bob",
          "kurzer stufiger Bob mit Franse, die Spitzen stehen zottig nach außen ab",
          "short layered bob with a fringe, the ends standing out shaggily", 136233);
        S("wild, stachelig", "wild, spiky",
          "kurzes Haar wild nach hinten aufgestellt, lange spitze Strähnen fallen über Stirn und Wange",
          "short hair spiked wildly backward, long pointed strands falling over the forehead and cheek", 136234);
        S("aufgefächerte Mähne", "fanned mane",
          "das Haar streng nach hinten gebürstet und zu einer breit aufgefächerten Mähne gestellt, wenige Strähnen auf der Stirn",
          "the hair brushed firmly back and raised into a broadly fanned mane, a few strands on the forehead", 136235);
        S("Korkenzieherlocken", "corkscrew ringlets",
          "gerade Franse, an beiden Seiten hängen dicke gedrehte Korkenzieherlocken bis über die Schultern",
          "straight fringe, thick twisted corkscrew ringlets hanging down past the shoulders on both sides", 136236);
        S("runder Bob", "round bob",
          "glatter runder Bob mit voller Franse, die Spitzen enden am Kiefer und biegen nach innen",
          "smooth round bob with a full fringe, the ends finishing at the jaw and curving inward", 136237);
        S("hochgestrichen, freie Schläfen", "swept up, bare temples",
          "das Deckhaar schräg nach hinten hochgestrichen, die Schläfen bleiben frei, zwei feine Strähnen fallen über die Wange",
          "the top hair swept up and diagonally back, the temples left bare, two fine strands falling over the cheek", 136238);
        S("hohe Tolle", "high pompadour",
          "das Deckhaar zu einer mächtigen Tolle nach oben und vorn über die Stirn gerollt, Seiten kurz",
          "the top hair rolled up and forward over the forehead into a massive pompadour, sides short", 136239);
        S("stachelig, spitze Seiten", "spiky, pointed sides",
          "kurzes Haar mit stacheligem Oberkopf, vorn gescheitelt, die Seiten enden in langen spitzen Strähnen am Kiefer",
          "short hair with a spiky crown, parted at the front, the sides ending in long pointed strands at the jaw", 136240);
        S("kurz, Seitenfranse", "short, side fringe",
          "sehr kurzer Schnitt, das Deckhaar nach vorn zu einer Seitenfranse gekämmt, der Nacken bleibt frei",
          "very short cut, the top hair combed forward into a side fringe, the nape left bare", 136242);
        S("Vorhangfranse", "curtain fringe",
          "kurzes Haar, die Franse teilt sich mittig über der Stirn, die Seiten laufen spitz am Kiefer aus",
          "short hair, the fringe parting in the middle over the forehead, the sides tapering to points at the jaw", 136243);
        S("kurze Seiten, Nackenmähne", "short sides, nape mane",
          "Seiten sehr kurz, das Deckhaar stachelig nach hinten gestellt, im Nacken fällt eine zottige Mähne herab",
          "sides very short, the top hair spiked backward, a shaggy mane falling down at the nape", 136244);
        S("glatter Longbob", "smooth long bob",
          "glatter Bob bis zum Hals mit gerader Franse, die Spitzen fallen ohne Schwung herab",
          "smooth bob down to the neck with a straight fringe, the ends falling without any flick", 136246);
        S("stufiger Bob, Seitenscheitel", "layered bob, side part",
          "kinnlanger Bob mit hohem Seitenscheitel, eine breite Strähne fällt schräg über die Braue, Spitzen zerfranst",
          "chin-length bob with a high side parting, a broad section falling diagonally over the brow, frayed ends", 136247);
        S("sehr kurz, federig", "very short, feathery",
          "sehr kurz geschnittenes Haar, überall federig gestuft, liegt dicht am Kopf und lässt die Stirn frei",
          "very short cut hair, feathered all over, lying close to the head and leaving the forehead bare", 136250);
        S("voluminöse Schrägfranse", "voluminous swept fringe",
          "kurzes volles Haar, die dicke Franse fegt schräg über eine Braue, am Scheitel steht eine Spitze ab",
          "short full hair, the thick fringe sweeping diagonally over one brow, a point standing up at the crown", 136251);
        S("stacheliger Schopf", "spiky topknot",
          "das Haar streng aus der Stirn hoch zu einem stacheligen Schopf gebunden, lange Strähnen fallen vor den Ohren herab",
          "the hair pulled tightly up off the forehead into a spiky topknot, long strands falling in front of the ears", 136252);
        S("halb hochgesteckt, Knoten", "half up, bun",
          "ein Teil des Haars seitlich oben zu einem Knoten gedreht, der Rest fällt offen bis auf die Schultern",
          "part of the hair twisted into a bun high at the side, the rest hanging loose to the shoulders", 136253);
        S("kurz, anliegend", "short, close-fitting",
          "kurzer glatter Schnitt, der eng am Kopf anliegt, die Seitenfranse deckt eine Braue, Spitzen am Kiefer",
          "short smooth cut lying close to the head, the side fringe covering one brow, ends at the jaw", 136254);
        S("lang, schwere Blockfranse", "long, heavy blunt fringe",
          "sehr langes, schnurgerades Haar, die schwere Franse ist waagerecht über den Brauen abgeschnitten",
          "very long, dead-straight hair, the heavy fringe cut level straight across above the brows", 136255);
        S("struppig, volle Franse", "tousled, full fringe",
          "kurzer stufiger Schnitt, feine Strähnen bedecken die ganze Stirn, die Spitzen enden struppig am Kiefer",
          "short layered cut, fine strands covering the whole forehead, the ends finishing tousled at the jaw", 136263);
        S("zurückgekämmt, lose Strähnen", "combed back, loose strands",
          "das Haar aus der Stirn nach hinten gekämmt, einzelne feine Strähnen lösen sich über Stirn und Wangen",
          "the hair combed back off the forehead, single fine strands coming loose over the forehead and cheeks", 136281);

        // Viera, male - 52 entries. Rava and Veena share the icon set
        S("zottig, Seitenfranse", "shaggy, side fringe",
          "schulterlanges gestuftes Haar, Seitenscheitel, die Franse fällt schräg über eine Braue, Spitzen wellen sich nach außen",
          "shoulder-length layered hair, side parting, the fringe falling diagonally over one brow, ends waving outwards", 138001);
        S("zottig, Mittelscheitel", "shaggy, centre parting",
          "schulterlanges gestuftes Haar, in der Mitte gescheitelt, die Stirn bleibt frei, die Spitzen wellen sich nach außen",
          "shoulder-length layered hair, parted in the middle, the forehead stays free, the ends waving outwards", 138002);
        S("Franse in den Augen", "fringe into the eyes",
          "kurzer zottiger Schnitt bis zum Kiefer, die Franse hängt in einzelnen Spitzen bis in die Augen",
          "short shaggy cut to the jaw, the fringe hanging in separate points down into the eyes", 138003);
        S("kurz zottig, Stirn frei", "short shaggy, forehead free",
          "kurzer zottiger Schnitt bis zum Kiefer, vorn gescheitelt und zu beiden Seiten gestrichen, die Stirn bleibt frei",
          "short shaggy cut to the jaw, parted at the front and swept to both sides, the forehead stays free", 138004);
        S("Band im Nacken", "ribbon at the nape",
          "glattes kinnlanges Haar, eine lange Strähne quert das Gesicht, im Nacken mit einem Band gebunden, dessen Enden herabhängen",
          "sleek chin-length hair, a long strand crossing the face, tied at the nape with a ribbon whose ends hang down", 138005);
        S("glatt, kinnlang", "sleek, chin-length",
          "glattes kinnlanges Haar, am Wirbel gescheitelt und beidseitig nach vorn gelegt, dünne Spitzsträhnen vor den Ohren",
          "sleek chin-length hair, parted at the crown and laid forward on both sides, thin pointed strands before the ears", 138006);
        S("zerzaust, abstehende Spitzen", "tousled, ends sticking out",
          "kinnlanges Haar aus der Stirn nach hinten gestrichen, zerzaust, die Spitzen stehen hinten weit ab",
          "chin-length hair swept back off the forehead, tousled, the ends standing well out at the back", 138007);
        S("nach hinten, weiche Wellen", "swept back, soft waves",
          "kinnlanges Haar aus der Stirn nach hinten gestrichen, in weichen Wellen anliegend, dünne Strähnen vor den Ohren",
          "chin-length hair swept back off the forehead, lying in soft waves, thin strands in front of the ears", 138008);
        S("Zierreif aus Metall", "ornate metal circlet",
          "kurzes, nach hinten gestrichenes Haar unter einem fein verzierten Metallreif, der über Schläfe und Wange reicht",
          "short hair swept back beneath a finely worked metal circlet that reaches over temple and cheek", 138009);
        S("zurückgekämmt, Stufenspitzen", "combed back, layered points",
          "kurzes Haar aus der Stirn nach hinten gekämmt, im Nacken und vor den Ohren in gezackten Stufenspitzen",
          "short hair combed back off the forehead, in jagged layered points at the nape and in front of the ears", 138010);
        S("Feder an der Seite", "feather at the side",
          "kinnlanges zerzaustes Haar mit schwerer Franse über einer Braue, seitlich am Kopf eine große Feder eingebunden",
          "chin-length tousled hair with a heavy fringe over one brow, a large feather bound in at the side", 138011);
        S("Spitzenfranse, ohne Schmuck", "pointed fringe, no ornament",
          "kinnlanges zerzaustes Haar, die Franse fällt in einzelnen Spitzen über beide Brauen, ohne jeden Schmuck",
          "chin-length tousled hair, the fringe falling in separate points over both brows, without any ornament", 138012);
        S("Mähne, Seitenscheitel", "mane, side parting",
          "schulterlange, stark gestufte Mähne mit tiefem Seitenscheitel, die Franse deckt die Stirn bis über eine Braue",
          "shoulder-length heavily layered mane with a deep side parting, the fringe covering the forehead down over one brow", 138013);
        S("Mähne, Stirn frei", "mane, forehead free",
          "schulterlange, stark gestufte Mähne, vorn mittig geteilt, die Stirn bleibt frei, die Spitzen laufen fransig aus",
          "shoulder-length heavily layered mane, split in the middle at the front, the forehead stays free, the ends running out frayed", 138014);
        S("Zöpfe und Spangen", "braids and clips",
          "langes Haar, oben zurückgenommen, dünne Zöpfe mit Perlenringen vor den Ohren, seitlich kleine Spangen im Deckhaar",
          "long hair taken back on top, thin braids with beaded rings before the ears, small clips at the side", 138015);
        S("zwei Zöpfe, Stirn frei", "two braids, forehead free",
          "langes Haar aus der Stirn zurückgenommen, beidseitig ein dünner Zopf mit dunklen Bändern vor dem Ohr",
          "long hair taken back off the forehead, a thin braid with dark bands before each ear", 138016);
        S("Zierhelm, langes Haar", "ornate helm, long hair",
          "ein verzierter Metallhelm deckt Scheitel und Schläfe, darunter fällt zottiges Haar bis auf die Schultern",
          "an ornate metal helm covers crown and temple, shaggy hair falling to the shoulders beneath it", 138017);
        S("glatt zur Seite gelegt", "smoothly laid to one side",
          "kinnlanges Haar glatt zu einer Seite gestrichen, die Franse quert die Stirn, die Spitzen laufen fransig aus",
          "chin-length hair swept smoothly to one side, the fringe crossing the forehead, the ends running out frayed", 138019);
        S("kurz und dicht", "short and dense",
          "sehr kurzer, dichter Schnitt, kurze Franse auf der Stirn, im Nacken gezackt auslaufend",
          "very short, dense cut, a short fringe on the forehead, running out jagged at the nape", 138024);
        S("Franse über einem Auge", "fringe over one eye",
          "nackenlanges Haar mit tiefem Seitenscheitel, die schwere Franse fällt über ein Auge, die Spitzen stellen sich ab",
          "neck-length hair with a deep side parting, the heavy fringe falling over one eye, the ends flicking out", 138025);
        S("lang, Stirnband", "long, headband",
          "langes glattes Haar weit über die Schultern, mittig gescheitelt, ein schmales Perlenband quert die Stirn",
          "long straight hair well past the shoulders, parted in the middle, a narrow beaded band crossing the forehead", 138026);
        S("Locke am Scheitel", "curl at the crown",
          "kurzer, rund geschnittener Kopf mit weicher Franse, am Scheitel steht eine einzelne Locke ab",
          "short, roundly cut head with a soft fringe, a single curl standing up at the crown", 138030);
        S("kurz, stachelig", "short, spiky",
          "kurzer Schnitt, ringsum in scharfe Spitzen geschnitten, die Franse zackt in einzelnen Stacheln über die Stirn",
          "short cut, cut into sharp points all round, the fringe jagging in separate spikes across the forehead", 138032);
        S("glatt zurückgelegt", "slicked back",
          "das Deckhaar glatt aus der Stirn nach oben und hinten gelegt, die Stirn frei, im Nacken fransige Spitzen",
          "the top hair laid smoothly up and back off the forehead, the forehead free, frayed points at the nape", 138031);
        S("Irokese, gefächert", "mohawk, fanned",
          "die Seiten sehr kurz, das Deckhaar zu einem breit gefächerten Kamm aufgestellt, der nach hinten ausläuft",
          "the sides very short, the top hair raised into a broadly fanned crest running out to the back", 138038);
        S("sehr kurz geschoren", "cropped very short",
          "sehr kurz geschorener Schnitt, oben knapp und struppig, die Stirnkante läuft gezackt aus",
          "a very short cropped cut, close and bristly on top, the hairline running out jagged", 138039);
        S("kurz, gefiederte Franse", "short, feathered fringe",
          "kurzer gestufter Schnitt, die gefiederte Franse liegt schräg auf der Stirn, um die Ohren fransige Spitzen",
          "short layered cut, the feathered fringe lying at an angle on the forehead, frayed points around the ears", 138040);
        S("glatt, Mittelscheitel", "sleek, centre parting",
          "glattes Haar, am Wirbel mittig geteilt, fällt geschlossen bis zum Kiefer, im Nacken kurz und gezackt",
          "sleek hair, split down the middle at the crown, falling closed to the jaw, short and jagged at the nape", 138047);
        S("lange Strähnen am Ohr", "long strands at the ear",
          "gestuftes Haar mit Mittelscheitel, vor den Ohren hängen dünne Strähnen weit über den Kiefer hinab",
          "layered hair with a centre parting, thin strands hanging well past the jaw in front of the ears", 138048);
        S("gebundener Nackenstrang", "bound strand at the nape",
          "das Haar nach hinten aufgenommen, feine Strähnen fallen auf die Stirn, im Nacken ein schmaler gebundener Strang",
          "the hair taken up and back, fine strands falling on the forehead, a narrow bound strand down the nape", 138049);
        S("kurz, borstig aufgestellt", "short, bristly upswept",
          "kurzer borstiger Schnitt, das Deckhaar steht auf, die Stirnfranse zackt in kurzen Spitzen",
          "short bristly cut, the top hair standing up, the forehead fringe jagging in short points", 138050);
        S("kurz, spitzer Stirnansatz", "short, pointed hairline",
          "kurzer dichter Schnitt, die Franse läuft mittig in einer Spitze auf die Stirn, seitlich kurze Zacken",
          "short dense cut, the fringe running to a point in the middle of the forehead, short jags at the sides", 138054);
        S("asymmetrisch, eine Seite anliegend", "asymmetric, one side flat",
          "glatter asymmetrischer Bob, die schwere Franse quert schräg über ein Auge, die andere Seite liegt eng gefasst an",
          "sleek asymmetric bob, the heavy fringe crossing diagonally over one eye, the other side gathered flat to the head", 138055);
        S("struppig, volle Franse", "tousled, full fringe",
          "kurzer struppiger Schnitt, die volle Franse hängt zerfranst über beide Brauen, um die Ohren gezackt",
          "short tousled cut, the full fringe hanging frayed over both brows, jagged around the ears", 138059);
        S("zwei Strähnen im Gesicht", "two strands in the face",
          "das Haar glatt nach hinten gestrichen, zwei lange dünne Strähnen fallen vorn bis über die Wange",
          "the hair swept smoothly back, two long thin strands falling forward past the cheek", 138061);
        S("Bob, weit ausgestellt", "bob, widely flared",
          "kinnlanger Bob mit gerader Franse über den Brauen, die Spitzen stellen sich rund nach außen",
          "chin-length bob with a straight fringe over the brows, the ends flaring roundly outwards", 138070);
        S("Bob, Spitzen nach außen", "bob, ends turned out",
          "glatter kieferlanger Bob, gerade Franse auf den Brauen, die Spitzen biegen sich leicht nach außen",
          "sleek jaw-length bob, a straight fringe on the brows, the ends bending slightly outwards", 138068);
        S("voluminös zurückgestrichen", "voluminous swept back",
          "voll nach hinten gestrichenes Deckhaar mit viel Volumen, einzelne Strähnen fallen zwischen die Brauen",
          "the top hair swept fully back with much volume, single strands falling between the brows", 138071);
        S("Bob mit Nackenzopf", "bob with a nape tail",
          "kieferlanger Bob mit gerader Franse, hinten ein langer, eng gebundener Zopf den Nacken hinab",
          "jaw-length bob with a straight fringe, a long tightly bound tail running down the nape at the back", 138072);
        S("zerfranste Mähne", "ragged mane",
          "stark zerfranste, gestufte Mähne bis unter den Kiefer, die Stirnmitte bleibt zwischen den Strähnen frei",
          "heavily ragged layered mane down past the jaw, the middle of the forehead left free between the strands", 138073);
        S("geschlossene Franse, gestuft", "closed fringe, layered",
          "gestufter Schnitt bis zum Kiefer, die Franse liegt geschlossen auf der Stirn und endet in Spitzen über den Brauen",
          "layered cut to the jaw, the fringe lying closed on the forehead and ending in points over the brows", 138076);
        S("große Tolle", "large pompadour",
          "das Deckhaar zu einer großen runden Tolle über der Stirn aufgerollt, die Seiten nach hinten gelegt",
          "the top hair rolled into a large round pompadour above the forehead, the sides laid back", 138077);
        S("aufgetürmt, eine Stirnsträhne", "piled up, one face strand",
          "das Haar in stacheligen Spitzen nach hinten aufgetürmt, vorn hängt eine lange gebogene Strähne bis zum Kiefer",
          "the hair piled back in spiky points, one long curved strand hanging at the front down to the jaw", 138078);
        S("gespaltene Franse", "split fringe",
          "vom Wirbel nach vorn gekämmtes Haar, die Franse teilt sich in einem spitzen Keil und gibt die Stirnmitte frei",
          "hair combed forward from the crown, the fringe splitting in a sharp wedge and leaving the mid-forehead free", 138079);
        S("Stirn frei, zottiger Nacken", "bare forehead, shaggy nape",
          "das Haar vollständig aus der Stirn nach hinten gebürstet, im Nacken fällt es zottig und gestuft herab",
          "the hair brushed entirely back off the forehead, falling shaggy and layered at the nape", 138080);
        S("lang glatt mit Franse", "long straight with fringe",
          "langes glattes Haar mit gerader Franse über den Brauen, die Seiten fallen glatt bis auf die Schultern",
          "long straight hair with a straight fringe over the brows, the sides falling smoothly to the shoulders", 138081);
        S("lange Spitze auf der Wange", "long point on the cheek",
          "gestuftes Haar bis in den Nacken, die schräge Franse endet in einer langen Spitze auf der Wange",
          "layered hair down to the nape, the slanting fringe ending in one long point on the cheek", 138087);
        S("kurz, Seitenscheitel", "short, side parting",
          "kurzer, leicht zerzauster Schnitt mit Seitenscheitel, die Strähnen laufen bis zum Kiefer in Zacken aus",
          "short, slightly tousled cut with a side parting, the strands running out in jags to the jaw", 138088);
        S("halb hochgesteckt, Knoten", "half up, knot",
          "langes Haar, hinten oben zu einem Knoten gedreht, der Rest fällt glatt über die Schultern, lange Seitenfranse",
          "long hair twisted into a knot high at the back, the rest falling smoothly over the shoulders, long side fringe", 138090);
        S("Bob, Spitzen nach innen", "bob, ends turned in",
          "kinnlanger runder Bob, die dichte Franse fällt bis auf die Brauen, die Spitzen biegen sich nach innen",
          "chin-length round bob, the thick fringe falling to the brows, the ends bending inwards", 138091);
        S("wild zerzaust", "wildly tousled",
          "in alle Richtungen abstehende stachelige Strähnen, die Franse fällt spitz über ein Auge, hinten struppig",
          "spiky strands sticking out in all directions, the fringe falling in a point over one eye, shaggy at the back", 138099);
        S("gerade Franse, Seitensträhnen", "blunt fringe, sidelocks",
          "gerade abgeschnittene Franse, zwei kurze Seitensträhnen enden am Kiefer, das übrige Haar fällt lang und glatt",
          "a bluntly cut fringe, two short sidelocks ending at the jaw, the rest of the hair falling long and straight", 251600);

        // Viera, female - 52 entries. Rava and Veena share the icon set
        S("lang, geschnürte Strähne", "long, laced strand",
          "langes, aus der Stirn zurückgestrichenes Haar in weichen Wellen, an der Schläfe eine kreuzweise geschnürte Strähne",
          "long hair swept back off the forehead in soft waves, a cross-laced strand at the temple", 138201);
        S("geschnürte Strähne, Stirnlocke", "laced strand, forelock",
          "langes zurückgestrichenes Haar mit geschnürter Schläfensträhne, eine lange Strähne fällt quer über die Stirn",
          "long swept-back hair with a laced strand at the temple, one long lock falling across the forehead", 138202);
        S("zottig, dichte Franse", "shaggy, heavy fringe",
          "dichte, gezackte Franse bis zu den Augen, stark gestufte Seiten, lange zottige Länge über die Schultern",
          "heavy jagged fringe down to the eyes, strongly layered sides, long shaggy length over the shoulders", 138203);
        S("lang, Mittelscheitel", "long, centre parting",
          "langes Haar mit Mittelscheitel, die Seiten glatt nach hinten gelegt, die Spitzen stehen gestuft ab",
          "long hair with a centre parting, the sides laid smoothly back, the ends layered and flicking out", 138204);
        S("Bob mit Spange", "bob with hair clip",
          "kinnlanger, leicht gewellter Bob mit gerader Franse, seitlich über dem Ohr eine schmale Haarspange",
          "chin-length, lightly waved bob with a straight fringe, a narrow hair clip above the ear at the side", 138205);
        S("Bob, Blütenspange", "bob, flower pin",
          "kinnlanger gewellter Bob ohne Franse, die Stirn frei, seitlich eine Spange mit drei kleinen Blüten",
          "chin-length waved bob without a fringe, forehead free, a pin with three small blossoms at the side", 138206);
        S("schulterlang, gewellt", "shoulder-length, waved",
          "schulterlanges Haar mit Mittelscheitel, die Stirn frei, gestufte Wellen mit abstehenden Spitzen",
          "shoulder-length hair with a centre parting, forehead free, layered waves with ends flicking outwards", 138207);
        S("Seitenfranse, Wellen", "side fringe, waves",
          "schulterlanges gewelltes Haar, eine lange Franse fällt schräg über die Stirn bis zu den Brauen",
          "shoulder-length waved hair, a long fringe falling diagonally across the forehead to the brows", 138208);
        S("kurz, nach hinten gefegt", "short, swept back",
          "kurzer gestufter Schnitt, die Seiten nach hinten gefegt mit abstehenden Spitzen, dünne Strähnen vor den Ohren",
          "short layered cut, the sides swept back with ends flicking out, thin strands in front of the ears", 138209);
        S("zerzaust, dünne Flechte", "tousled, thin braid",
          "kurzer zerzauster Schnitt mit langer Seitenfranse, vorn eine dünne, am Ende gebundene Flechte bis übers Kinn",
          "short tousled cut with a long side fringe, a thin braid tied at the end hangs past the chin", 138210);
        S("lang, Seitenscheitel", "long, side parting",
          "langes Haar mit tiefem Seitenscheitel, die volle Masse fällt gewellt über eine Schulter",
          "long hair with a deep side parting, the full mass falling in waves over one shoulder", 138211);
        S("gezackte Franse, gewellt", "jagged fringe, waved",
          "langes gewelltes Haar mit gezackter Franse über der Stirn, die Länge fällt über beide Schultern",
          "long waved hair with a jagged fringe over the forehead, the length falling over both shoulders", 138212);
        S("glatt, gedrehter Strang", "sleek, twisted strand",
          "langes glattes Haar mit Mittelscheitel, das Deckhaar seitlich zu einem Strang gedreht und nach hinten geführt",
          "long sleek hair with a centre parting, the top hair twisted into a strand and led back at the side", 138213);
        S("glatt, lose Strähnen", "sleek, loose wisps",
          "langes glattes Haar mit gedrehtem Deckhaar, dazu lose gewellte Strähnen, die das Gesicht umspielen",
          "long sleek hair with the top twisted back, plus loose waved wisps framing the face", 138214);
        S("Seitenzopf, Blüte", "side tail, blossom",
          "das Haar seitlich zu einem Zopf gebunden, der Binder trägt eine kleine Blüte, die Stirn bleibt frei",
          "hair tied into a tail at the side, the tie carrying a small blossom, the forehead left free", 138215);
        S("Franse, Seitenzopf", "fringe, side tail",
          "volle Franse über der Stirn, das Haar seitlich zu einem Zopf mit Blütenbinder gebunden",
          "full fringe over the forehead, the hair tied into a side tail with a blossom-trimmed tie", 138216);
        S("Ziermaske, langes Haar", "ornate headpiece, long hair",
          "reich verzierter Kopfschmuck über einer Kopfhälfte bis zur Wange, darunter langes offenes Haar",
          "richly worked headpiece over one half of the head down to the cheek, long loose hair beneath", 138217);
        S("gerade Franse, lang", "blunt fringe, long",
          "langes, fast glattes Haar mit dichter, gerade geschnittener Franse auf Brauenhöhe",
          "long, nearly straight hair with a thick fringe cut straight across at brow level", 138219);
        S("volle Franse, hoher Zopf", "full fringe, high ponytail",
          "volle Franse über der Stirn, das Haar hoch am Hinterkopf zu einem gewickelten Zopf gebunden",
          "full fringe over the forehead, the hair tied high at the back into a wrapped ponytail", 138224);
        S("lang, glatt, seitlich", "long, sleek, side-parted",
          "langes glattes Haar mit Seitenscheitel, eine Strähne quer über die Schläfe, die Länge fällt gerade herab",
          "long sleek hair with a side parting, one strand across the temple, the length falling straight down", 138225);
        S("Rastalocken mit Ringen", "dreadlocks with rings",
          "lange, gedrehte Rastalocken mit Metallringen, fallen beidseitig vorn über die Schultern",
          "long twisted dreadlocks with metal rings, falling forward on both sides over the shoulders", 138226);
        S("Seitenfranse, Seitenzopf", "side fringe, side ponytail",
          "glattes Haar mit langer Seitenfranse über einer Braue, seitlich hoch zu einem geraden Zopf gebunden",
          "sleek hair with a long side fringe over one brow, tied up at the side into a straight ponytail", 138231);
        S("kurz, spitze Strähnen", "short, pointed strands",
          "kurzer, stark ausgedünnter Schnitt, spitze Strähnen fallen über die Stirn, die Spitzen enden gezackt am Kiefer",
          "short heavily thinned cut, pointed strands over the forehead, the ends finishing jagged at the jaw", 138233);
        S("zwei Zöpfe", "two braids",
          "weiche Franse über der Stirn, hinter den Ohren zwei lange Flechten, die vorn über die Schultern fallen",
          "soft fringe over the forehead, two long braids behind the ears falling forward over the shoulders", 138232);
        S("Zopf am Hinterkopf", "ponytail at the back",
          "volle Franse, das Haar glatt nach hinten genommen und am Hinterkopf zu einem geraden Zopf gebunden",
          "full fringe, the hair taken smoothly back and tied at the back of the head into a straight tail", 138239);
        S("lange Wellen, Vorhangfranse", "long waves, curtain fringe",
          "lange weiche Wellen, die Franse in der Mitte geteilt und beidseitig bis zu den Brauen fallend",
          "long soft waves, the fringe split in the middle and falling to the brows on both sides", 138240);
        S("Pixie, seitlich gelegt", "pixie, swept to one side",
          "kurzer weicher Pixie, das Deckhaar quer über den Kopf zur Seite gelegt, Nacken kurz",
          "short soft pixie, the top hair laid across the head to one side, short at the nape", 138241);
        S("kinnlang, glatt", "chin-length, sleek",
          "kinnlanger glatter Schnitt, das Haar fällt vom feinen Scheitel als geschlossener Vorhang bis zum Kiefer",
          "chin-length sleek cut, the hair falling from a fine parting as a closed curtain to the jaw", 138248);
        S("kurz, zottig", "short, shaggy",
          "kurzer zottiger Schnitt bis zum Kiefer, eine Strähne fällt auf die Stirn, die Spitzen stehen gezackt ab",
          "short shaggy cut to the jaw, one strand falling on the forehead, the ends jagged and standing out", 138249);
        S("hochgekämmt, Nackenzopf", "swept up, nape tail",
          "das Haar nach oben und hinten gekämmt, im Nacken zu einem kleinen gedrehten Zopf gebunden",
          "the hair combed up and back, tied at the nape into a small twisted tail", 138250);
        S("sehr kurz, gefiedert", "very short, feathered",
          "sehr kurzer, dicht gefiederter Schnitt, eng am Kopf, mit gezacktem Rand über der Stirn",
          "very short, densely feathered crop lying close to the head, with a jagged edge over the forehead", 138251);
        S("lang, Gesicht umrahmt", "long, framing the face",
          "langes Haar, das beidseitig als breite Strähnen das Gesicht umrahmt, die Stirn frei, die Spitzen gewellt",
          "long hair framing the face with broad strands on both sides, forehead free, the ends waved", 138255);
        S("Seitenpony, kleiner Knoten", "side fringe, small knot",
          "kurzer glatter Schnitt, langer Pony über ein Auge, das Deckhaar am Oberkopf zu einem kleinen Knoten gedreht",
          "short sleek cut, long fringe over one eye, the top hair twisted into a small knot at the crown", 138256);
        S("kurz, schwere Franse", "short, heavy fringe",
          "kurzer, stark gestufter Schnitt, eine schwere Franse bedeckt die ganze Stirn, die Spitzen federn am Kiefer",
          "short heavily layered cut, a heavy fringe covering the whole forehead, the ends feathering at the jaw", 138260);
        S("hochgekämmt, zwei Strähnen", "swept up, two loose strands",
          "das Haar nach oben zu einem struppigen Schopf gekämmt, zwei dünne Strähnen fallen neben dem Gesicht herab",
          "the hair combed up into a tousled crest, two thin strands falling down beside the face", 138262);
        S("Bob, abstehende Spitzen", "bob, flicked-out ends",
          "kinnlanger Bob mit gezackter Franse, die Spitzen stehen kräftig nach außen ab",
          "chin-length bob with a jagged fringe, the ends flicking strongly outwards", 138271);
        S("Bob, eingerollte Spitzen", "bob, ends curling in",
          "kinnlanger Bob mit gerader Franse, die Spitzen rollen sich am Kiefer nach innen",
          "chin-length bob with a straight fringe, the ends curling inwards at the jaw", 138269);
        S("windzerzaust, kurz", "windswept, short",
          "kurzes Haar nach hinten gefegt wie im Wind, einzelne Strähnen kreuzen die Stirn, Nacken sehr kurz",
          "short hair swept back as if by wind, single strands crossing the forehead, very short at the nape", 138272);
        S("umwickelte Strähnen", "wrapped strands",
          "runde Franse über der Stirn, hinten dicke, mit Bändern umwickelte Strähnen über die Schultern nach vorn",
          "rounded fringe over the forehead, thick strands wrapped in bands falling forward over the shoulders", 138273);
        S("kurz gestuft, voll", "short layered, full",
          "kurzer, stark gestufter Schnitt mit viel Volumen an den Seiten, spitze Strähnen bis unter den Kiefer",
          "short heavily layered cut with much volume at the sides, pointed strands down past the jaw", 138274);
        S("hochgesteckt, lang offen", "pinned up, long and loose",
          "das Deckhaar am Scheitel hochgesteckt, darunter fällt langes glattes Haar, die Franse reicht bis zu den Brauen",
          "the top hair pinned up at the crown, long sleek hair falling beneath, the fringe reaching the brows", 138277);
        S("runde Haube", "rounded cap",
          "das Deckhaar zu einer großen, glatten runden Haube geformt, darunter kurze abstehende Spitzen",
          "the top hair shaped into a large smooth rounded cap, short flicked-out ends beneath", 138278);
        S("stachelige Mähne", "spiky mane",
          "die Stirn ganz frei, das Haar hinten zu einer breiten stacheligen Mähne aufgestellt, vorn eine lange dünne Strähne",
          "forehead completely free, the hair standing up behind in a broad spiky mane, one long thin strand at the front", 138279);
        S("kurz gestuft, anliegend", "short layered, close-lying",
          "kurzer gestufter Schnitt, der eng am Kopf anliegt, eine Strähne über der Braue, spitze Enden am Kiefer",
          "short layered cut lying close to the head, one strand over the brow, pointed ends at the jaw", 138280);
        S("glatt zurück, zottiger Nacken", "swept back, shaggy nape",
          "das Haar glatt aus der Stirn zurückgenommen, hinten fällt es in zottigen Stufen bis in den Nacken",
          "the hair taken smoothly back off the forehead, falling behind in shaggy layers down to the nape", 138281);
        S("geteilte Franse, glatt", "parted fringe, sleek",
          "glattes langes Haar, das in geraden Strähnen herabfällt, die Franse ist über der Stirn leicht geteilt",
          "sleek long hair falling in straight strands, the fringe slightly parted over the forehead", 138282);
        S("kurz, langer Seitenpony", "short, long side fringe",
          "kurzer gestufter Schnitt mit Wirbel am Scheitel, ein langer Pony fällt schräg über eine Braue",
          "short layered cut with a swirl at the crown, a long fringe falling diagonally over one brow", 138288);
        S("kurz, Knoten am Scheitel", "short, knot at the crown",
          "kurzer Schnitt mit langem Seitenpony, das Haar am Scheitel zu einem kleinen Knoten gedreht",
          "short cut with a long side fringe, the hair twisted into a small knot at the crown", 138289);
        S("runder Knoten, offenes Haar", "round bun, hair worn down",
          "hoch am Hinterkopf ein runder Knoten, das übrige Haar fällt offen bis auf die Schultern",
          "a round bun high at the back, the rest of the hair falling loose to the shoulders", 138291);
        S("kurzer glatter Bob", "short sleek bob",
          "kurzer glatter Bob bis zum Kiefer mit Franse über der Stirn, die Enden liegen glatt an",
          "short sleek bob to the jaw with a fringe over the forehead, the ends lying flat", 138292);
        S("zerzaust, gedrehter Knoten", "tousled, twisted knot",
          "stark zerzauster kurzer Schnitt mit langem Pony über einem Auge, hinten ein gedrehter, stachelig endender Knoten",
          "heavily tousled short cut with a long fringe over one eye, a twisted knot with spiky ends behind", 251700);
        S("gerade Franse, Seitensträhnen", "blunt fringe, cut sidelocks",
          "schwere gerade Franse über den Augen, kinnlang gerade geschnittene Seitensträhnen, dahinter sehr langes glattes Haar",
          "heavy blunt fringe above the eyes, sidelocks cut straight at chin length, very long sleek hair behind", 251702);

        // ── Schweifform / Tail Shape ──────────────────────────────────────────
        // 64 icons, 12 rows, 16 descriptions. The thumbnail is the
        // TAIL ALONE against the vignette - no body, no head - so shape, length,
        // thickness and what sits at the tip are the whole content, and there is
        // nothing here that another menu could switch off.
        // SHARING IS MEASURED, not assumed, same test the Face batch used (share =
        // under 0.5 % of pixels differing by more than 16):
        //   Miqo'te - all FOUR rows byte-for-byte identical, 0.00 % on all 8 entries.
        //     Seeker/Keeper and male/female alike, so 8 strings cover 32 slots.
        //   Au Ra   - Raen and Xaela identical per sex, 0.00 % on all 4. Male against
        //     female differs 15.4 %, but that is the render's proportions: the four
        //     SHAPES are plainly the same four, so one set of strings covers both.
        //   Hrothgar- Helions and The Lost identical per sex, 0.00 % on all 4. Male
        //     against female differs 19.3 %, again the camera angle and proportions,
        //     not the shapes.
        // NO COLOUR, as everywhere here: the dark tuft on the Hrothgar tails and the
        // cuffs on the Miqo'te ones are the preview render's colours, so only their
        // SHAPE and size are described.

        // Miqo'te - 8 entries, all four rows share them
        S("schlank", "slim",
          "schlanker gleichmäßig behaarter Schweif, zur Spitze hin dünner, mit Knick nach unten",
          "slim evenly furred tail, thinning towards the tip, with a downward kink",
          134191, 134391, 134691, 134891);
        S("Pinselquaste", "brush tuft",
          "kurz behaarter Schaft mit großer buschiger Quaste am Ende",
          "short-haired shaft with a large bushy tuft at the end",
          134192, 134392, 134692, 134892);
        S("buschig", "bushy",
          "durchgehend lang und dicht behaarter Schweif in weitem Bogen, sehr voluminös",
          "long, densely furred along its whole length, in a wide curve, very full",
          134193, 134393, 134693, 134893);
        S("breite Manschette", "broad cuff",
          "schlanker Schweif, dessen letztes Drittel in einer breiten Manschette steckt",
          "slim tail whose last third sits inside a broad cuff",
          134194, 134394, 134694, 134894);
        S("mittel", "medium",
          "gleichmäßig behaarter Schweif, etwas voller als der schlanke, Spitze leicht abgeknickt",
          "evenly furred tail, somewhat fuller than the slim one, tip slightly kinked",
          134195, 134395, 134695, 134895);
        S("dick", "thick",
          "durchgehend dickerer, dichter behaarter Schweif, kräftiger als die schlanken Varianten",
          "thicker and more densely furred throughout, heavier than the slim variants",
          134196, 134396, 134696, 134896);
        S("schmaler Wickel", "narrow wrap",
          "schlanker Schweif mit schmalem Wickel direkt an der Spitze",
          "slim tail with a narrow wrap right at the tip",
          134197, 134397, 134697, 134897);
        S("Manschette, Spitze frei", "cuff, tip free",
          "schlanker Schweif mit breiter Manschette kurz vor dem Ende, die Spitze schaut heraus",
          "slim tail with a broad cuff just short of the end, the tip showing beyond it",
          134198, 134398, 134698, 134898);

        // Au Ra - 4 entries, all four rows share them
        S("glatt geschuppt", "smooth scaled",
          "gleichmäßig geschuppter Schweif, glatt zulaufend, ohne Flossen oder Stacheln",
          "evenly scaled tail tapering smoothly, without fins or spines",
          136191, 136351, 136691, 136891);
        S("Flossenfortsatz", "fin spur",
          "geschuppter Schweif mit spitzem Flossenfortsatz im oberen Drittel",
          "scaled tail with a pointed fin spur on the upper third",
          136192, 136352, 136692, 136892);
        S("kurz, beplattet", "short, plated",
          "kurzer schwerer Schweif mit großen überlappenden Platten und breitem Kamm auf der Oberseite",
          "short heavy tail with large overlapping plates and a broad ridge along the top",
          136193, 136353, 136693, 136893);
        S("gespaltene Spitze", "split tip",
          "sehr schlanker peitschenartiger Schweif, dessen Spitze sich in mehrere feine Enden aufteilt",
          "very slim whip-like tail whose tip splits into several fine ends",
          136194, 136354, 136694, 136894);

        // Hrothgar - 4 entries, all four rows share them
        S("runde Quaste", "round tuft",
          "langer Schweif mit großer runder buschiger Quaste am Ende",
          "long tail with a large round bushy tuft at the end",
          137191, 137391, 137691, 137891);
        S("spitze Quaste", "pointed tuft",
          "voller behaarter Schweif mit kleinerer, spitz zulaufender Quaste",
          "fully furred tail with a smaller tuft tapering to a point",
          137192, 137392, 137692, 137892);
        S("schlicht", "plain",
          "glatt zulaufender Schweif, nur ein feiner Büschel an der Spitze",
          "smoothly tapering tail with only a fine wisp at the tip",
          137193, 137393, 137693, 137893);
        S("lange Fahne", "long plume",
          "langer Schweif mit lang ausgezogener, seitlich ausgebreiteter Fahne am Ende",
          "long tail with a drawn-out plume spread sideways at the end",
          137194, 137394, 137694, 137894);

        // ── Fellzeichnung / Fur Pattern ───────────────────────────────────────
        // 20 icons, 4 Hrothgar rows, 5 descriptions.
        // The thumbnail shows the FUR RUFF and nothing else: the male render is the
        // chest seen from the front, the female render the shoulders and upper back.
        // Both show the same five patterns - compared side by side across all four
        // rows, Helions against The Lost and male against female.
        // THIS ONE IS A VISUAL COMPARISON AND NOT A MEASUREMENT, and the difference
        // matters. The pixel test says Helions and The Lost differ by ~30 %, but that
        // is the coat colour (tan against white) - the exact thing these descriptions
        // must not talk about, so the number answers the wrong question. A high-pass
        // "pattern mask" variant was tried and FAILED ITS OWN CONTROL: two visibly
        // different patterns from the same row scored a higher overlap (72-76 %) than
        // the same pattern across tribes (62 %), because it was measuring the outline
        // of the ruff rather than the markings. It was discarded rather than reported.
        // So: five patterns, matched by eye, and said so here.
        // Nothing about how DARK a marking is - that is the render's fur colour.
        S("Tigerstreifen", "tiger stripes",
          "Tigerzeichnung: eine Mittellinie mit Rippenstreifen, die nach unten und außen auslaufen",
          "tiger marking: a centre line with rib stripes running down and outward",
          137401, 137411, 137901, 137911);
        S("feine Rosetten", "fine rosettes",
          "dicht gesetzte kleine Rosetten, viele Ringflecken mit hellerem Kern",
          "densely set small rosettes, many ring spots with a lighter centre",
          137402, 137412, 137902, 137912);
        S("große Flecken", "large spots",
          "gröbere Fleckung, größere unregelmäßige Flecken mit Abstand zueinander",
          "coarser spotting, larger irregular spots set apart from one another",
          137403, 137413, 137903, 137913);
        S("Querbänder", "cross bands",
          "breite Querbänder, fünf bis sechs durchgehende Streifen über den Fellkragen",
          "broad cross bands, five or six continuous stripes across the fur ruff",
          137404, 137414, 137904, 137914);
        S("Ziermuster", "ornate motif",
          "verschnörkeltes, spiegelsymmetrisches Ziermuster in der Mitte des Fellkragens",
          "ornate, mirror-symmetric motif in the middle of the fur ruff",
          137405, 137415, 137905, 137915);

        // ── Ohrenform (Viera) / Ear Shape (Viera) ─────────────────────────────
        // 16 icons, 4 Viera rows, 8 descriptions.
        // Measured sharing: Rava and Veena are BYTE-FOR-BYTE IDENTICAL, 0.00 % on all
        // eight entries, so one string covers both tribes. The control confirms the
        // test can tell things apart at all - male against female differs 18.2 %.
        // THE ENTRY ORDER IS NOT THE SAME FOR THE TWO SEXES, which is exactly why this
        // table is keyed by ICON ID and not by (row, entry). Male reads long-shaggy,
        // drooping, long-smooth, short-shaggy; female reads short-shaggy, long-shaggy,
        // drooping, long-smooth. An index-based table would have swapped three of the
        // four descriptions on every female character.

        // Viera male - Rava and Veena share
        S("lang, zottig", "long, shaggy",
          "lange aufrechte Ohren, schmal, mit zottig ausgefranster Außenkante",
          "long upright ears, narrow, with a shaggy ragged outer edge",
          138191, 138691);
        S("hängend", "drooping",
          "Ohren, die weit nach außen und unten abknicken und tief hängen",
          "ears bending far out and downward, hanging low",
          138192, 138692);
        S("lang, glatt", "long, smooth",
          "lange aufrechte Ohren mit glatter Kante, zur Spitze hin fein auslaufend",
          "long upright ears with a smooth edge, tapering to fine points",
          138193, 138693);
        S("kurz, zottig", "short, shaggy",
          "kürzere Ohren, weiter auseinander stehend, mit zottiger Kante",
          "shorter ears, set wider apart, with a shaggy edge",
          138194, 138694);

        // Viera female - Rava and Veena share. Different order from the male list.
        S("kurz, zottig", "short, shaggy",
          "kurze Ohren, auseinander stehend, mit stark büschelig ausgefranster Spitze",
          "short ears, set apart, with a heavily tufted ragged tip",
          138391, 138891);
        S("lang, zottig", "long, shaggy",
          "lange hoch aufgerichtete Ohren mit ausgefranster Außenkante und büscheliger Spitze",
          "long, high upright ears with a ragged outer edge and a tufted tip",
          138392, 138892);
        S("hängend", "drooping",
          "Ohren, die weit nach außen und unten abknicken und tief hängen",
          "ears bending far out and downward, hanging low",
          138393, 138893);
        S("lang, glatt", "long, smooth",
          "lange aufrechte Ohren mit glatter Kante, zur Spitze hin fein auslaufend",
          "long upright ears with a smooth edge, tapering to fine points",
          138394, 138894);

        // ── Gesichtsbemalung / Face Paint ─────────────────────────────────────
        // Batch 3. 833 CharaMakeCustomize params over 32 rows,
        // 27 entries each. Four things were MEASURED off the sheet dump before a
        // word was written, and the last one changes how the rest is batched:
        // 1. **The game names none of them.** All 833 params carry an EMPTY Hint AND
        //    an empty HintItem, and none is IsPurchasable (cmdump `names "Face Paint"`,
        //    re-run 2026-08-09). A game-supplied name would have to beat authored text,
        //    so this is checked per entry rather than taken from the earlier note.
        // 2. **Entry 1 of every row is "no paint" and CANNOT be keyed here.** Its param
        //    is 2401 and CharaMakeCustomize[2401].Icon is 0, so CharaMakeReader stores
        //    icon id 0 for it - and 0 is also what every TYPE-0 menu leaves in its
        //    Icons array. Registering 0 would put this text on every Jaw, Nose, Mouth
        //    and Eye Shape entry in the game. Do not do it. That leaves 26 real icons
        //    per row, 832 in total.
        // 3. **The 32 rows' icon sets are pairwise DISJOINT.** Excluding the shared
        //    "none" param, no icon id is offered by two rows: 32 sets of 26, union 832,
        //    zero intersections. So an id always identifies its row, and a description
        //    has to be registered against every row's id explicitly.
        // 4. **But the DESIGNS repeat.** The same 26 designs appear in the same entry
        //    order in every row looked at so far - the same catalogue rendered on each
        //    race's own face - so ONE string can cover several rows' ids.
        // HOW FAR THAT SHARING IS PROVEN, and where it is only seen:
        //   MEASURED, and safe: Helions against The Lost is 0.00 % of pixels differing
        //     by more than 16, on all 26 entries, per sex - byte-for-byte identical
        //     renders. Same test the Face and Tail batches used.
        //   NOT MEASURABLE, and stated as a VISUAL comparison: male against female
        //     scores 67 %, and Hrothgar against Hyur 82 %, but that is the base render,
        //     not the paint. The control proves the metric cannot answer this question:
        //     two plainly DIFFERENT designs inside one row score 1.6-4.4 %, because the
        //     paint is a few per cent of the pixels. So the claim that Hrothgar male,
        //     Hrothgar female and Hyur Midlander male carry the same 26 designs comes
        //     from looking at all 26 in all three sets, entry by entry, at scale 3 and
        //     at scale 5-7 on the eye and cheek regions. Same footing as the Fur
        //     Pattern sharing, and labelled the same way.
        // NO COLOUR, as everywhere in this file: every one of these renders is red on
        // a fixed preview face. The player picks the colour in its own menu, so only
        // WHERE a mark sits and WHAT SHAPE it has is described.
        // COMPLETE as of 2026-08-09: all 32 rows, 832 ids, against these same 26 strings.
        // The "expected, not measured" caveat that stood here is now DISCHARGED, in two
        // steps that answer two different questions:
        //   MEASURED, per tribe pair: every pair of tribe rows within a race and sex is
        //     byte-for-byte identical on all 26 entries - Elezen, Lalafell, Miqo'te,
        //     Roegadyn, Au Ra and Viera all 0 differing pixels; Hrothgar female differs
        //     on 2 entries by 0.23 %, i.e. antialiasing. The ONE genuine exception is
        //     Hyur, where Midlander and Highlander are different faces - the same split
        //     the Hairstyle menu has. So a tribe pair never needed looking at twice.
        //   LOOKED AT, per remaining block: the 15 distinct race/sex blocks were read
        //     entry by entry against this catalogue, with the confusable groups checked
        //     deliberately (2/3/4 are three different eyeshadows; 17 and 19 are both
        //     broad diagonal bands; 5 vs 6 is edgeless flush vs hard-edged oval). All 15
        //     carry the 26 designs in this order. No deviation was found.
        // Two things seen while checking that are FACE, not paint, and must not be read
        // as a design: the Miqo'te rows have a dark curved cheek marking present on every
        // entry including the plain one, and the Lalafell rows have a shaded cheek dimple
        // sitting exactly where a cheek design would go.

        S("Lidschatten, voll", "full eyeshadow",
          "kräftiger Strich am oberen Wimpernkranz, darüber ein weicher Lidschatten über das ganze Lid bis zur Braue",
          "a strong line along the upper lash line, above it a soft wash over the whole lid up to the brow",
          130001, 130041, 130081, 130121, 130161, 130201, 130241, 130281,
          130321, 130361, 130401, 130441, 130481, 130521, 130561, 130601,
          130641, 130681, 130721, 130761, 130801, 130841, 130881, 130921,
          139001, 139041, 139081, 139121, 139161, 139201, 139241, 139281);
        S("Lidschatten, innen", "eyeshadow, inner",
          "Lidschatten am inneren Augenwinkel, schräg zur Braue hin ansteigend, der Strich am Wimpernkranz reicht nur bis zur Lidmitte",
          "eyeshadow at the inner corner of the eye rising towards the brow, the lash line marked only as far as the middle of the lid",
          130002, 130042, 130082, 130122, 130162, 130202, 130242, 130282,
          130322, 130362, 130402, 130442, 130482, 130522, 130562, 130602,
          130642, 130682, 130722, 130762, 130802, 130842, 130882, 130922,
          139002, 139042, 139082, 139122, 139162, 139202, 139242, 139282);
        S("Lidstrich, außen", "liner, outer",
          "feiner Strich am Wimpernkranz, der über den äußeren Augenwinkel hinausläuft und dort weich nach außen verwischt",
          "a fine line along the lash line running past the outer corner of the eye and smudging softly outward there",
          130003, 130043, 130083, 130123, 130163, 130203, 130243, 130283,
          130323, 130363, 130403, 130443, 130483, 130523, 130563, 130603,
          130643, 130683, 130723, 130763, 130803, 130843, 130883, 130923,
          139003, 139043, 139083, 139123, 139163, 139203, 139243, 139283);
        S("Wangenhauch", "cheek flush",
          "weicher randloser Hauch auf dem Wangenknochen, ohne jede Kontur",
          "a soft edgeless flush over the cheekbone, with no outline at all",
          130004, 130044, 130084, 130124, 130164, 130204, 130244, 130284,
          130324, 130364, 130404, 130444, 130484, 130524, 130564, 130604,
          130644, 130684, 130724, 130764, 130804, 130844, 130884, 130924,
          139004, 139044, 139084, 139124, 139164, 139204, 139244, 139284);
        S("Ovalfleck", "oval spot",
          "ein einzelner satter Ovalfleck mitten auf der Wange, scharf begrenzt",
          "a single solid oval spot in the middle of the cheek, sharply edged",
          130005, 130045, 130085, 130125, 130165, 130205, 130245, 130285,
          130325, 130365, 130405, 130445, 130485, 130525, 130565, 130605,
          130645, 130685, 130725, 130765, 130805, 130845, 130885, 130925,
          139005, 139045, 139085, 139125, 139165, 139205, 139245, 139285);
        S("Drache", "dragon",
          "ein langer geschlängelter Drache zieht von der Schläfe an der Wange hinab bis zum Kiefer",
          "a long winding dragon running from the temple down the cheek to the jaw",
          130006, 130046, 130086, 130126, 130166, 130206, 130246, 130286,
          130326, 130366, 130406, 130446, 130486, 130526, 130566, 130606,
          130646, 130686, 130726, 130766, 130806, 130846, 130886, 130926,
          139006, 139046, 139086, 139126, 139166, 139206, 139246, 139286);
        S("Stammesranke", "tribal barbs",
          "verzweigtes Stammesmuster auf der Wange, spitze Widerhaken nach oben, ein geschwungener Ausläufer zieht zum Kiefer hinab",
          "a branching tribal motif on the cheek, barbed points upward and a curved spur running down to the jaw",
          130007, 130047, 130087, 130127, 130167, 130207, 130247, 130287,
          130327, 130367, 130407, 130447, 130487, 130527, 130567, 130607,
          130647, 130687, 130727, 130767, 130807, 130847, 130887, 130927,
          139007, 139047, 139087, 139127, 139167, 139207, 139247, 139287);
        S("Blüte am Stiel", "flower on a stem",
          "eine Blüte in Umrisslinien mit rundem Blütenkranz, auf kurzem Stiel mit zwei Blättern, auf der Wange",
          "an outlined flower with a round ring of petals, on a short stem with two leaves, on the cheek",
          130008, 130048, 130088, 130128, 130168, 130208, 130248, 130288,
          130328, 130368, 130408, 130448, 130488, 130528, 130568, 130608,
          130648, 130688, 130728, 130768, 130808, 130848, 130888, 130928,
          139008, 139048, 139088, 139128, 139168, 139208, 139248, 139288);
        S("zwei Sterne", "two stars",
          "ein großer fünfzackiger Stern auf der Wange, schräg darunter ein kleinerer",
          "a large five-pointed star on the cheek, with a smaller one below and to the outside",
          130009, 130049, 130089, 130129, 130169, 130209, 130249, 130289,
          130329, 130369, 130409, 130449, 130489, 130529, 130569, 130609,
          130649, 130689, 130729, 130769, 130809, 130849, 130889, 130929,
          139009, 139049, 139089, 139129, 139169, 139209, 139249, 139289);
        S("Schmetterling", "butterfly",
          "ein Schmetterling mit gemusterten Flügeln in Umrisslinien auf der Wange",
          "an outlined butterfly with patterned wings on the cheek",
          130010, 130050, 130090, 130130, 130170, 130210, 130250, 130290,
          130330, 130370, 130410, 130450, 130490, 130530, 130570, 130610,
          130650, 130690, 130730, 130770, 130810, 130850, 130890, 130930,
          139010, 139050, 139090, 139130, 139170, 139210, 139250, 139290);
        S("Herz", "heart",
          "ein einzelnes kleines Herz hoch auf der Wange, dicht unter dem äußeren Augenwinkel",
          "a single small heart high on the cheek, just below the outer corner of the eye",
          130011, 130051, 130091, 130131, 130171, 130211, 130251, 130291,
          130331, 130371, 130411, 130451, 130491, 130531, 130571, 130611,
          130651, 130691, 130731, 130771, 130811, 130851, 130891, 130931,
          139011, 139051, 139091, 139131, 139171, 139211, 139251, 139291);
        S("Stirnwappen", "brow crest",
          "feingliedriges spiegelsymmetrisches Rankenwappen mitten auf der Stirn, oben zu zwei Spitzen im Haaransatz geöffnet, nach unten zwischen den Brauen auslaufend",
          "a fine, mirror-symmetric scrollwork crest in the middle of the forehead, opening into two points at the hairline and tapering out between the brows",
          130012, 130052, 130092, 130132, 130172, 130212, 130252, 130292,
          130332, 130372, 130412, 130452, 130492, 130532, 130572, 130612,
          130652, 130692, 130732, 130772, 130812, 130852, 130892, 130932,
          139012, 139052, 139092, 139132, 139172, 139212, 139252, 139292);
        S("sechs Punkte", "six dots",
          "sechs Punkte in zwei senkrechten Dreierreihen mitten auf der Stirn",
          "six dots in two vertical rows of three in the middle of the forehead",
          130013, 130053, 130093, 130133, 130173, 130213, 130253, 130293,
          130333, 130373, 130413, 130453, 130493, 130533, 130573, 130613,
          130653, 130693, 130733, 130773, 130813, 130853, 130893, 130933,
          139013, 139053, 139093, 139133, 139173, 139213, 139253, 139293);
        S("drei Striche", "three strokes",
          "drei kurze schräge Striche nebeneinander auf der Wange, nach außen leicht gefächert",
          "three short slanted strokes side by side on the cheek, fanning slightly outward",
          130014, 130054, 130094, 130134, 130174, 130214, 130254, 130294,
          130334, 130374, 130414, 130454, 130494, 130534, 130574, 130614,
          130654, 130694, 130734, 130774, 130814, 130854, 130894, 130934,
          139014, 139054, 139094, 139134, 139174, 139214, 139254, 139294);
        S("Vogelküken", "bird chick",
          "ein kleiner Vogel mit rundem Kopf, der in einer halben Eierschale steht, auf der Wange",
          "a small round-headed bird standing in a half eggshell, on the cheek",
          130015, 130055, 130095, 130135, 130175, 130215, 130255, 130295,
          130335, 130375, 130415, 130455, 130495, 130535, 130575, 130615,
          130655, 130695, 130735, 130775, 130815, 130855, 130895, 130935,
          139015, 139055, 139095, 139135, 139175, 139215, 139255, 139295);
        S("zwei Schrägbänder", "two diagonal bands",
          "zwei breite, gezackt gerissene Bänder laufen schräg über das ganze Gesicht, über Stirn, Auge und Wange hinweg",
          "two broad bands with ragged torn edges running diagonally across the whole face, over forehead, eye and cheek",
          130016, 130056, 130096, 130136, 130176, 130216, 130256, 130296,
          130336, 130376, 130416, 130456, 130496, 130536, 130576, 130616,
          130656, 130696, 130736, 130776, 130816, 130856, 130896, 130936,
          139016, 139056, 139096, 139136, 139176, 139216, 139256, 139296);
        S("Flammenbrauen", "flame brows",
          "über jedem Auge ein breiter geschwungener Zug, der über der Braue nach oben züngelt und am äußeren Winkel unter das Auge einrollt",
          "over each eye a broad curving stroke that licks upward over the brow and curls in under the outer corner",
          130017, 130057, 130097, 130137, 130177, 130217, 130257, 130297,
          130337, 130377, 130417, 130457, 130497, 130537, 130577, 130617,
          130657, 130697, 130737, 130777, 130817, 130857, 130897, 130937,
          139017, 139057, 139097, 139137, 139177, 139217, 139257, 139297);
        S("großes X", "large X",
          "ein großes X aus zwei breiten Bändern mit ausgefransten Enden, über der Nase gekreuzt",
          "a large X of two broad bands with frayed ends, crossing over the nose",
          130018, 130058, 130098, 130138, 130178, 130218, 130258, 130298,
          130338, 130378, 130418, 130458, 130498, 130538, 130578, 130618,
          130658, 130698, 130738, 130778, 130818, 130858, 130898, 130938,
          139018, 139058, 139098, 139138, 139178, 139218, 139258, 139298);
        S("Striche und Punkte", "strokes and dots",
          "unter jedem Auge ein breiter Strich schräg zur Nase hin, außen daneben eine Reihe kleiner Punkte, weitere Punkte über einer Braue",
          "a broad stroke under each eye slanting towards the nose, a row of small dots beside it on the outside, more dots above one brow",
          130019, 130059, 130099, 130139, 130179, 130219, 130259, 130299,
          130339, 130379, 130419, 130459, 130499, 130539, 130579, 130619,
          130659, 130699, 130739, 130779, 130819, 130859, 130899, 130939,
          139019, 139059, 139099, 139139, 139179, 139219, 139259, 139299);
        S("großes Stammesmuster", "large tribal pattern",
          "großflächiges Stammesmuster: eine symmetrische Krone füllt die Stirn, von ihr laufen spitze Zacken um beide Augen und über beide Wangen",
          "a large tribal pattern: a symmetric crown fills the forehead, with pointed tines running around both eyes and over both cheeks",
          130020, 130060, 130100, 130140, 130180, 130220, 130260, 130300,
          130340, 130380, 130420, 130460, 130500, 130540, 130580, 130620,
          130660, 130700, 130740, 130780, 130820, 130860, 130900, 130940,
          139020, 139060, 139100, 139140, 139180, 139220, 139260, 139300);
        S("Augenbinde", "eye band",
          "ein breites waagerechtes Band quer über beide Augen, von Schläfe zu Schläfe, mit scharfen Kanten",
          "a broad horizontal band across both eyes from temple to temple, with sharp edges",
          130021, 130061, 130101, 130141, 130181, 130221, 130261, 130301,
          130341, 130381, 130421, 130461, 130501, 130541, 130581, 130621,
          130661, 130701, 130741, 130781, 130821, 130861, 130901, 130941,
          139021, 139061, 139101, 139141, 139181, 139221, 139261, 139301);
        S("halbes Gesicht", "half the face",
          "eine ganze Gesichtshälfte flächig ausgefüllt, senkrecht in der Mitte geteilt",
          "one whole half of the face filled solid, split vertically down the middle",
          130022, 130062, 130102, 130142, 130182, 130222, 130262, 130302,
          130342, 130382, 130422, 130462, 130502, 130542, 130582, 130622,
          130662, 130702, 130742, 130782, 130822, 130862, 130902, 130942,
          139022, 139062, 139102, 139142, 139182, 139222, 139262, 139302);
        S("volle Maske", "full mask",
          "eine Maske über das ganze Gesicht, umrandet von einer hellen Bordüre, die über die Stirn bogt und beidseitig bis zum Kinn hinabläuft, über den Brauen zwei ausgesparte Haken",
          "a mask over the whole face, edged by a pale border arcing over the forehead and running down both sides to the chin, with two hook-shaped cut-outs above the brows",
          130023, 130063, 130103, 130143, 130183, 130223, 130263, 130303,
          130343, 130383, 130423, 130463, 130503, 130543, 130583, 130623,
          130663, 130703, 130743, 130783, 130823, 130863, 130903, 130943,
          139023, 139063, 139103, 139143, 139183, 139223, 139263, 139303);
        S("weicher Schleier", "soft veil",
          "ein sehr weicher wolkiger Schleier über Schläfen und Wangenknochen, ohne jede Kante",
          "a very soft, cloudy veil over the temples and cheekbones, with no edge at all",
          130024, 130064, 130104, 130144, 130184, 130224, 130264, 130304,
          130344, 130384, 130424, 130464, 130504, 130544, 130584, 130624,
          130664, 130704, 130744, 130784, 130824, 130864, 130904, 130944,
          139024, 139064, 139104, 139144, 139184, 139224, 139264, 139304);
        S("Brauenbalken", "brow bars",
          "ein breiter Balken liegt auf jeder Braue und folgt ihrem Verlauf, nach außen spitz auslaufend",
          "a broad bar laid along each brow, following its line and tapering to a point outward",
          130025, 130065, 130105, 130145, 130185, 130225, 130265, 130305,
          130345, 130385, 130425, 130465, 130505, 130545, 130585, 130625,
          130665, 130705, 130745, 130785, 130825, 130865, 130905, 130945,
          139025, 139065, 139105, 139145, 139185, 139225, 139265, 139305);
        S("Sprenkel", "speckles",
          "feine Punkte, locker über die Wange unter dem Auge gestreut",
          "fine dots scattered loosely over the cheek below the eye",
          130026, 130066, 130106, 130146, 130186, 130226, 130266, 130306,
          130346, 130386, 130426, 130466, 130506, 130546, 130586, 130626,
          130666, 130706, 130746, 130786, 130826, 130866, 130906, 130946,
          139026, 139066, 139106, 139146, 139186, 139226, 139266, 139306);
        // ── TYPE-4 / Gesichtsmerkmale - BEGIN GENERATED-MERGE ───────────────────

        // The TYPE-4 menus: Gesichtsmerkmale / Weitere Merkmale
        // (5 entries) and Tätowierungen / Ohrspangen / Limbalring (2 entries).
        // WHERE THE IDS COME FROM. CharaMakeType.FacialFeatureOption is 8 structs of 7
        // UI icon ids, one struct per FACE - not flags, actual ui/icon renders, one
        // close-up per toggle. `cmdump featicons` pulls all 924 of them and writes the
        // index; `mksheet.py "Facial Features"` lays them out 7 to a sheet with each
        // cell labelled `<slot> #<iconId>`, and the text below was written from those
        // sheets, one face at a time.
        // MEASURED, and it is what makes the sheets readable: the 5-entry menu owns
        // slots 1-5 and the 2-entry menu owns slots 6-7, in every row - Hyur's slots
        // 6-7 are tattoos, Elezen Wildwood's are ear clasps, Au Ra's are limbal rings.
        // Elezen and Au Ra list the 2-entry menu FIRST in CharaMakeStruct and their
        // jewellery is still in slots 6-7, so menu order is not the rule.
        // NOT REACHABLE YET, and that is deliberate. The mod has no path from a type-4
        // toggle to its icon: byte 12 is eight bits and NOTHING in the sheet says that
        // bit i is slot i+1. Until CharaMakeReader.LogFeatureBitProbe answers that in
        // game (see its comment for the four-step test), AnnounceFeatureBits keeps
        // saying "Merkmal 3, ein" rather than naming a decal it cannot pin down. These
        // strings are registered now because they are keyed by ICON ID, which the bit
        // question cannot change - only the wiring waits on it.
        // Slot order IS icon order here (slot k is always the id ending in k), so the
        // entry-order trap cannot bite this family. Everything else applies:
        // German authored first, structure only, never colour - tattoo, ear-clasp and
        // limbal-ring colour are their own menus and all write CustomizeData byte 13.

        // ---- feat-aura.cs ----
        // 20_Au_Ra_Raen_male_face1
        S("Schuppenplatte, Stirn", "scale plate on the forehead",
          "verzweigte Schuppenplatte, die vom Nasenrücken über die Stirn wächst und geweihartige Zacken über beide Brauen streckt", "branching scale plate growing from the bridge of the nose up over the forehead, throwing antler-like prongs above both brows",
          136111);
        S("Schuppen, Wange und Kiefer", "scales on cheek and jaw",
          "krallenförmige Schuppenplatte auf der Wange, die zum Kiefer hin ausläuft, dazu weitere Schuppenbahnen an Kiefer und Hals", "claw-shaped scale plate on the cheek running out toward the jaw, with further bands of scales along jaw and neck",
          136112);
        S("Hörner am Kiefer", "horns at the jaw",
          "lange, klingenförmige Hörner, die von Wangenknochen und Kiefer nach vorn über das Gesicht hinausragen", "long blade-shaped horns projecting forward from cheekbone and jaw, out past the face",
          136113);
        S("Schuppe über der Braue", "scale above the brow",
          "Nahaufnahme des Auges: gelappte Schuppenplatte über der Braue, die Braue selbst unbehaart", "close-up of the eye: lobed scale plate above the brow, the brow ridge itself bare",
          136114);
        S("Gefiederte Braue", "feathered brow",
          "Nahaufnahme des Auges: buschige, gefiederte Braue über dem Auge, die an einer Schuppenplatte ansetzt", "close-up of the eye: bushy feathered brow above the eye, set against a scale plate at its end",
          136115);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136116);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136117);

        // 20_Au_Ra_Raen_male_face2
        S("Schuppen, Nasenrücken", "scales on the bridge of the nose",
          "kleine spitze Schuppen auf der Stirn und ein flügelartig ausgebreiteter Kranz spitzer Platten über dem Nasenrücken", "small pointed scales on the forehead and a wing-like spread of pointed plates across the bridge of the nose",
          136121);
        S("Kantige Schuppen, Wange", "angular scales on the cheek",
          "breite Bahn kantiger, spitz zulaufender Platten, die die Wange vom Auge bis zum Kiefer bedeckt", "broad sheet of angular, sharply tapering plates covering the cheek from the eye down to the jaw",
          136122);
        S("Schuppenflügel, Schläfe", "scale wings at the temple",
          "große klingenartige Schuppenflügel, die von Schläfe und Wangenknochen abstehen und nach hinten weisen", "large blade-like scale wings standing off temple and cheekbone and sweeping backward",
          136123);
        S("Dunkle Platte, Augenwinkel", "dark plate at the corner of the eye",
          "Nahaufnahme des Auges: dunkle, kantige Platte über dem Lid, die über den äußeren Augenwinkel hinaus in einen abwärts gerichteten Haken ausläuft", "close-up of the eye: dark angular plate over the lid, running past the outer corner into a downward hook",
          136124);
        S("Struppige Braue", "shaggy brow",
          "Nahaufnahme des Auges: struppige, gefiederte Braue über dem Auge, an ihrem äußeren Ende eine spitze Schuppe", "close-up of the eye: shaggy feathered brow above the eye, with a pointed scale at its outer end",
          136125);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136126);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136127);

        // 20_Au_Ra_Raen_male_face3
        S("Schuppenspange, Nasenrücken", "band of scales across the nose",
          "flache, flügelförmige Schuppenspange quer über dem Nasenrücken zwischen den Augen, kleine Schuppen an den Schläfen", "flat wing-shaped band of scales lying across the bridge of the nose between the eyes, with small scales at the temples",
          136131);
        S("Schnabelplatte, Wange", "beaked plate on the cheek",
          "große gebogene Schuppenplatte auf der Wange, die schnabelartig nach vorn greift, dahinter Schuppenbahnen an Kiefer und Hals", "large curved scale plate on the cheek hooking forward like a beak, with sheets of scales behind it on jaw and neck",
          136132);
        S("Panzerplatte am Ohr", "armour plate at the ear",
          "harte, facettierte Platte mit dunkler Einlage an Ohr und Schläfe, dazu Schuppenbahnen über Kiefer und Hals", "hard faceted plate with a dark inset at ear and temple, with sheets of scales over jaw and neck",
          136133);
        S("Dunkles Lidband", "dark band on the lid",
          "Nahaufnahme des Auges: kräftiges dunkles Band entlang des Oberlids, das weit über den äußeren Augenwinkel hinaus in eine lange Spitze ausläuft", "close-up of the eye: heavy dark band along the upper lid, running far past the outer corner into a long point",
          136134);
        S("Glatte Brauenleiste", "smooth brow ridge",
          "Nahaufnahme des Auges: glatte, harte Brauenleiste, die sich klingenartig über dem Auge wölbt", "close-up of the eye: smooth hard brow ridge arching over the eye like a blade",
          136135);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136136);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136137);

        // 20_Au_Ra_Raen_male_face4
        S("Stirnband und Sprenkel", "forehead band and flecks",
          "breites gemustertes Schuppenband über Stirn und Nasenrücken, das sich zwischen den Brauen zur Raute weitet, dazu kleine tropfenförmige Schuppen über Stirn, Schläfen und Wangen", "broad patterned band of scales down forehead and nose bridge, widening into a diamond between the brows, with small teardrop scales scattered over forehead, temples and cheeks",
          136141);
        S("Schuppen, Kinn und Oberlippe", "scales on chin and upper lip",
          "Schuppenplatte auf der Nasenspitze, gebogene Hornsporne seitlich über der Oberlippe und eine Reihe großer, nach oben gerichteter Platten über Kinn und Kiefer", "scale plate on the tip of the nose, curved horn spurs to either side above the upper lip, and a row of large upward-pointing plates over chin and jaw",
          136142);
        S("Panzerplatten, Kiefer", "armour plates on the jaw",
          "schwere, geschichtete Panzerplatten über Wange, Kiefer und Hals, dazu eine abstehende Ohrflosse mit eingelassener Zierplatte", "heavy layered armour plates over cheek, jaw and neck, with a flared ear fin carrying an inset ornament",
          136143);
        S("Dunkler Lidrand", "dark rim around the eye",
          "Nahaufnahme des Auges: dicker dunkler Rand, der die ganze Lidspalte umschließt und zum äußeren Augenwinkel hin breiter wird", "close-up of the eye: thick dark rim enclosing the whole eye opening and broadening toward the outer corner",
          136144);
        S("Lange gefiederte Braue", "long feathered brow",
          "Nahaufnahme des Auges: lange, gefiederte Braue, die sich in hohem Bogen über dem Auge spannt", "close-up of the eye: long feathered brow arching high above the eye",
          136145);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136146);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136147);

        // 21_Au_Ra_Raen_female_face1
        S("Zierplatte, Stirn", "ornate plate on the forehead",
          "schmale, verzierte Schuppenplatte mittig auf der Stirn, mit seitlichen Zacken, die spitz zwischen den Brauen zum Nasenrücken ausläuft", "narrow ornate scale plate centred on the forehead, with side prongs, tapering to a point between the brows onto the bridge of the nose",
          136311);
        S("Schuppen, Wange", "scales on the cheek",
          "hakenförmige Schuppenplatte auf der Wange, dazu Schuppen an Kiefer und Hals", "hook-shaped scale plate on the cheek, with scales along jaw and neck",
          136312);
        S("Langes Horn, Schläfe", "long horn at the temple",
          "langes, schlankes, sanft gebogenes Horn, das von der Schläfe nach hinten weist und in einer feinen Spitze endet", "long slender gently curved horn sweeping back from the temple and ending in a fine point",
          136313);
        S("Feine Lidlinie", "fine line on the lid",
          "Nahaufnahme des Auges: feine dunkle Linie am Oberlid, die Wimpern zum äußeren Augenwinkel hin gebündelt", "close-up of the eye: fine dark line along the upper lid, lashes gathered toward the outer corner",
          136314);
        S("Kräftige Lidlinie", "heavy line on the lid",
          "Nahaufnahme des Auges: kräftigere dunkle Linie, die Ober- und Unterlid umläuft, mit dichteren Wimpern", "close-up of the eye: heavier dark line running around both upper and lower lid, with denser lashes",
          136315);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136316);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136317);

        // 21_Au_Ra_Raen_female_face2
        S("Kronenplatte, Stirn", "crown-like plate on the forehead",
          "breite, kronenartige Schuppenplatte auf der Stirn mit mehreren nach oben gerichteten Spitzen und einem Dorn, der zwischen die Brauen herabläuft", "broad crown-like scale plate on the forehead with several upward points and a spike running down between the brows",
          136321);
        S("Fächerschuppen, Wange", "fan of scales on the cheek",
          "breite, fächerförmige Schuppenfläche über der Wange, deren zackige Finger zum Kiefer weisen, dazu weitere Schuppenbahnen an Kiefer und Hals", "wide fan-shaped web of scales over the cheek whose jagged fingers reach toward the jaw, with further sheets at jaw and neck",
          136322);
        S("Zwei Stirnschuppen", "two scales on the forehead",
          "zwei kleine spitze Schuppen weit auseinander hoch auf der Stirn, dazu eine einzelne fächerförmige Schuppe auf dem Nasenrücken", "two small pointed scales set wide apart high on the forehead, plus a single fan-shaped scale on the bridge of the nose",
          136323);
        S("Feine Lidlinie", "fine line on the lid",
          "Nahaufnahme des Auges: feine dunkle Lidlinie, Wimpern vor allem am äußeren Augenwinkel", "close-up of the eye: fine dark lid line, lashes mainly at the outer corner",
          136324);
        S("Kräftige Lidlinie", "heavy line on the lid",
          "Nahaufnahme des Auges: kräftige dunkle Linie, die das ganze Auge umrandet, mit dichten Wimpern an Ober- und Unterlid", "close-up of the eye: heavy dark line ringing the whole eye, with dense lashes on both lids",
          136325);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136326);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136327);

        // 21_Au_Ra_Raen_female_face3
        S("Schlanke Zierplatte, Stirn", "slender ornate plate on the forehead",
          "schlanke, symmetrisch verzierte Schuppenplatte auf der Stirn mit seitlichen Zacken und langer Spitze zwischen den Brauen", "slender symmetrically ornamented scale plate on the forehead with side prongs and a long point between the brows",
          136331);
        S("Filigrane Schuppen, Wange", "lacy scales on the cheek",
          "breites, filigran zerfranstes Schuppenfeld, das sich vom Unterlid über die Wange bis zu Ohr und Kiefer zieht", "wide lacy, ragged-edged field of scales spreading from below the eye across the cheek to ear and jaw",
          136332);
        S("Einzelschuppe, Kiefer", "single scale on the jaw",
          "einzelne kleine, spitze Schuppe auf dem Kiefer, darüber ein filigranes Schuppenfeld an der Wange", "single small pointed scale on the jaw, with a lacy field of scales above it on the cheek",
          136333);
        S("Zurückhaltende Lidlinie", "restrained line on the lid",
          "Nahaufnahme des Auges: schmale, zurückhaltende Lidlinie, feine Wimpern über das Unterlid verteilt", "close-up of the eye: narrow, restrained lid line, fine lashes spread along the lower lid",
          136334);
        S("Betontes Oberlid", "emphasised upper lid",
          "Nahaufnahme des Auges: kräftige dunkle Linie am Oberlid, die über den äußeren Augenwinkel hinausreicht, mit stärkeren Wimpern", "close-up of the eye: heavy dark line along the upper lid reaching past the outer corner, with stronger lashes",
          136335);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136336);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136337);

        // 21_Au_Ra_Raen_female_face4
        S("Große Stirnplatte", "large plate on the forehead",
          "große, symmetrische Schuppenplatte, die fast die ganze Stirn und beide Brauenbögen bedeckt, in blattartige Segmente geschichtet, mit langer Spitze über dem Nasenrücken", "large symmetric scale plate covering nearly the whole forehead and both brow ridges, layered in leaf-like segments, with a long point down the bridge of the nose",
          136341);
        S("Schuppenkragen, Hals", "collar of scales on the neck",
          "spitze Platten an Wangen und Kiefer und ein dichter Kragen sich überlappender Schuppen über Kehle und Hals", "pointed plates flanking cheeks and jaw and a dense collar of overlapping scales over throat and neck",
          136342);
        S("Wangenstacheln", "spikes on the cheek",
          "lange, nach vorn gerichtete Schuppenstacheln, die von der Wange über das Gesicht hinausragen, dazu ein breites Schuppenfeld über Wange und Kiefer und ein Schuppenkragen am Hals", "long forward-pointing scale spikes projecting from the cheek out past the face, with a wide field of scales over cheek and jaw and a scaled collar down the neck",
          136343);
        S("Perlleiste am Oberlid", "beaded ridge on the upper lid",
          "Nahaufnahme des Auges: perlenartig gegliederte Schuppenleiste entlang des Oberlids, die Wimpern zurückhaltend", "close-up of the eye: beaded scale ridge running along the upper lid, lashes restrained",
          136344);
        S("Betonte Perlleiste", "emphasised beaded ridge",
          "Nahaufnahme des Auges: dieselbe Perlleiste am Oberlid, darunter ein breiteres dunkles Band und stärkere Wimpern am Unterlid", "close-up of the eye: the same beaded ridge on the upper lid, with a broader dark band beneath it and stronger lashes on the lower lid",
          136345);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136346);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136347);

        // 22_Au_Ra_Xaela_male_face1
        S("Schuppenplatte, Stirn", "scale plate on the forehead",
          "verzweigte Schuppenplatte, die vom Nasenrücken über die Stirn wächst und geweihartige Zacken über beide Brauen streckt", "branching scale plate growing from the bridge of the nose up over the forehead, throwing antler-like prongs above both brows",
          136611);
        S("Schuppen, Wange und Kiefer", "scales on cheek and jaw",
          "krallenförmige Schuppenplatte auf der Wange, die zum Kiefer hin ausläuft, dazu weitere Schuppenbahnen an Kiefer und Hals", "claw-shaped scale plate on the cheek running out toward the jaw, with further bands of scales along jaw and neck",
          136612);
        S("Hörner am Kiefer", "horns at the jaw",
          "lange, klingenförmige Hörner, die von Wangenknochen und Kiefer nach vorn über das Gesicht hinausragen", "long blade-shaped horns projecting forward from cheekbone and jaw, out past the face",
          136613);
        S("Schuppe über der Braue", "scale above the brow",
          "Nahaufnahme des Auges: gelappte Schuppenplatte über der Braue, die Braue selbst unbehaart", "close-up of the eye: lobed scale plate above the brow, the brow ridge itself bare",
          136614);
        S("Gefiederte Braue", "feathered brow",
          "Nahaufnahme des Auges: buschige, gefiederte Braue über dem Auge, die an einer Schuppenplatte ansetzt", "close-up of the eye: bushy feathered brow above the eye, set against a scale plate at its end",
          136615);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136616);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136617);

        // 22_Au_Ra_Xaela_male_face2
        S("Schuppen, Nasenrücken", "scales on the bridge of the nose",
          "kleine spitze Schuppen auf der Stirn und ein flügelartig ausgebreiteter Kranz spitzer Platten über dem Nasenrücken", "small pointed scales on the forehead and a wing-like spread of pointed plates across the bridge of the nose",
          136621);
        S("Kantige Schuppen, Wange", "angular scales on the cheek",
          "breite Bahn kantiger, spitz zulaufender Platten, die die Wange vom Auge bis zum Kiefer bedeckt", "broad sheet of angular, sharply tapering plates covering the cheek from the eye down to the jaw",
          136622);
        S("Schuppenflügel, Schläfe", "scale wings at the temple",
          "große klingenartige Schuppenflügel, die von Schläfe und Wangenknochen abstehen und nach hinten weisen", "large blade-like scale wings standing off temple and cheekbone and sweeping backward",
          136623);
        S("Dunkle Platte, Augenwinkel", "dark plate at the corner of the eye",
          "Nahaufnahme des Auges: dunkle, kantige Platte über dem Lid, die über den äußeren Augenwinkel hinaus in einen abwärts gerichteten Haken ausläuft", "close-up of the eye: dark angular plate over the lid, running past the outer corner into a downward hook",
          136624);
        S("Struppige Braue", "shaggy brow",
          "Nahaufnahme des Auges: struppige, gefiederte Braue über dem Auge, an ihrem äußeren Ende eine spitze Schuppe", "close-up of the eye: shaggy feathered brow above the eye, with a pointed scale at its outer end",
          136625);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136626);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136627);

        // 22_Au_Ra_Xaela_male_face3
        S("Schuppenspange, Nasenrücken", "band of scales across the nose",
          "flache, flügelförmige Schuppenspange quer über dem Nasenrücken zwischen den Augen, kleine Schuppen an den Schläfen", "flat wing-shaped band of scales lying across the bridge of the nose between the eyes, with small scales at the temples",
          136631);
        S("Schnabelplatte, Wange", "beaked plate on the cheek",
          "große gebogene Schuppenplatte auf der Wange, die schnabelartig nach vorn greift, dahinter Schuppenbahnen an Kiefer und Hals", "large curved scale plate on the cheek hooking forward like a beak, with sheets of scales behind it on jaw and neck",
          136632);
        S("Panzerplatte am Ohr", "armour plate at the ear",
          "harte, facettierte Platte mit dunkler Einlage an Ohr und Schläfe, dazu Schuppenbahnen über Kiefer und Hals", "hard faceted plate with a dark inset at ear and temple, with sheets of scales over jaw and neck",
          136633);
        S("Dunkles Lidband", "dark band on the lid",
          "Nahaufnahme des Auges: kräftiges dunkles Band entlang des Oberlids, das weit über den äußeren Augenwinkel hinaus in eine lange Spitze ausläuft", "close-up of the eye: heavy dark band along the upper lid, running far past the outer corner into a long point",
          136634);
        S("Glatte Brauenleiste", "smooth brow ridge",
          "Nahaufnahme des Auges: glatte, harte Brauenleiste, die sich klingenartig über dem Auge wölbt", "close-up of the eye: smooth hard brow ridge arching over the eye like a blade",
          136635);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136636);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136637);

        // 22_Au_Ra_Xaela_male_face4
        S("Stirnband und Sprenkel", "forehead band and flecks",
          "breites gemustertes Schuppenband über Stirn und Nasenrücken, das sich zwischen den Brauen zur Raute weitet, dazu kleine tropfenförmige Schuppen über Stirn, Schläfen und Wangen", "broad patterned band of scales down forehead and nose bridge, widening into a diamond between the brows, with small teardrop scales scattered over forehead, temples and cheeks",
          136641);
        S("Schuppen, Kinn und Oberlippe", "scales on chin and upper lip",
          "Schuppenplatte auf der Nasenspitze, gebogene Hornsporne seitlich über der Oberlippe und eine Reihe großer, nach oben gerichteter Platten über Kinn und Kiefer", "scale plate on the tip of the nose, curved horn spurs to either side above the upper lip, and a row of large upward-pointing plates over chin and jaw",
          136642);
        S("Panzerplatten, Kiefer", "armour plates on the jaw",
          "schwere, geschichtete Panzerplatten über Wange, Kiefer und Hals, dazu eine abstehende Ohrflosse mit eingelassener Zierplatte", "heavy layered armour plates over cheek, jaw and neck, with a flared ear fin carrying an inset ornament",
          136643);
        S("Dunkler Lidrand", "dark rim around the eye",
          "Nahaufnahme des Auges: dicker dunkler Rand, der die ganze Lidspalte umschließt und zum äußeren Augenwinkel hin breiter wird", "close-up of the eye: thick dark rim enclosing the whole eye opening and broadening toward the outer corner",
          136644);
        S("Lange gefiederte Braue", "long feathered brow",
          "Nahaufnahme des Auges: lange, gefiederte Braue, die sich in hohem Bogen über dem Auge spannt", "close-up of the eye: long feathered brow arching high above the eye",
          136645);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136646);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136647);

        // 23_Au_Ra_Xaela_female_face1
        S("Zierplatte, Stirn", "ornate plate on the forehead",
          "schmale, verzierte Schuppenplatte mittig auf der Stirn, mit seitlichen Zacken, die spitz zwischen den Brauen zum Nasenrücken ausläuft", "narrow ornate scale plate centred on the forehead, with side prongs, tapering to a point between the brows onto the bridge of the nose",
          136811);
        S("Schuppen, Wange", "scales on the cheek",
          "hakenförmige Schuppenplatte auf der Wange, dazu Schuppen an Kiefer und Hals", "hook-shaped scale plate on the cheek, with scales along jaw and neck",
          136812);
        S("Langes Horn, Schläfe", "long horn at the temple",
          "langes, schlankes, sanft gebogenes Horn, das von der Schläfe nach hinten weist und in einer feinen Spitze endet", "long slender gently curved horn sweeping back from the temple and ending in a fine point",
          136813);
        S("Feine Lidlinie", "fine line on the lid",
          "Nahaufnahme des Auges: feine dunkle Linie am Oberlid, die Wimpern zum äußeren Augenwinkel hin gebündelt", "close-up of the eye: fine dark line along the upper lid, lashes gathered toward the outer corner",
          136814);
        S("Kräftige Lidlinie", "heavy line on the lid",
          "Nahaufnahme des Auges: kräftigere dunkle Linie, die Ober- und Unterlid umläuft, mit dichteren Wimpern", "close-up of the eye: heavier dark line running around both upper and lower lid, with denser lashes",
          136815);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136816);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136817);

        // 23_Au_Ra_Xaela_female_face2
        S("Kronenplatte, Stirn", "crown-like plate on the forehead",
          "breite, kronenartige Schuppenplatte auf der Stirn mit mehreren nach oben gerichteten Spitzen und einem Dorn, der zwischen die Brauen herabläuft", "broad crown-like scale plate on the forehead with several upward points and a spike running down between the brows",
          136821);
        S("Fächerschuppen, Wange", "fan of scales on the cheek",
          "breite, fächerförmige Schuppenfläche über der Wange, deren zackige Finger zum Kiefer weisen, dazu weitere Schuppenbahnen an Kiefer und Hals", "wide fan-shaped web of scales over the cheek whose jagged fingers reach toward the jaw, with further sheets at jaw and neck",
          136822);
        S("Zwei Stirnschuppen", "two scales on the forehead",
          "zwei kleine spitze Schuppen weit auseinander hoch auf der Stirn, dazu eine einzelne fächerförmige Schuppe auf dem Nasenrücken", "two small pointed scales set wide apart high on the forehead, plus a single fan-shaped scale on the bridge of the nose",
          136823);
        S("Feine Lidlinie", "fine line on the lid",
          "Nahaufnahme des Auges: feine dunkle Lidlinie, Wimpern vor allem am äußeren Augenwinkel", "close-up of the eye: fine dark lid line, lashes mainly at the outer corner",
          136824);
        S("Kräftige Lidlinie", "heavy line on the lid",
          "Nahaufnahme des Auges: kräftige dunkle Linie, die das ganze Auge umrandet, mit dichten Wimpern an Ober- und Unterlid", "close-up of the eye: heavy dark line ringing the whole eye, with dense lashes on both lids",
          136825);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136826);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136827);

        // 23_Au_Ra_Xaela_female_face3
        S("Schlanke Zierplatte, Stirn", "slender ornate plate on the forehead",
          "schlanke, symmetrisch verzierte Schuppenplatte auf der Stirn mit seitlichen Zacken und langer Spitze zwischen den Brauen", "slender symmetrically ornamented scale plate on the forehead with side prongs and a long point between the brows",
          136831);
        S("Filigrane Schuppen, Wange", "lacy scales on the cheek",
          "breites, filigran zerfranstes Schuppenfeld, das sich vom Unterlid über die Wange bis zu Ohr und Kiefer zieht", "wide lacy, ragged-edged field of scales spreading from below the eye across the cheek to ear and jaw",
          136832);
        S("Einzelschuppe, Kiefer", "single scale on the jaw",
          "einzelne kleine, spitze Schuppe auf dem Kiefer, darüber ein filigranes Schuppenfeld an der Wange", "single small pointed scale on the jaw, with a lacy field of scales above it on the cheek",
          136833);
        S("Zurückhaltende Lidlinie", "restrained line on the lid",
          "Nahaufnahme des Auges: schmale, zurückhaltende Lidlinie, feine Wimpern über das Unterlid verteilt", "close-up of the eye: narrow, restrained lid line, fine lashes spread along the lower lid",
          136834);
        S("Betontes Oberlid", "emphasised upper lid",
          "Nahaufnahme des Auges: kräftige dunkle Linie am Oberlid, die über den äußeren Augenwinkel hinausreicht, mit stärkeren Wimpern", "close-up of the eye: heavy dark line along the upper lid reaching past the outer corner, with stronger lashes",
          136835);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136836);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136837);

        // 23_Au_Ra_Xaela_female_face4
        S("Große Stirnplatte", "large plate on the forehead",
          "große, symmetrische Schuppenplatte, die fast die ganze Stirn und beide Brauenbögen bedeckt, in blattartige Segmente geschichtet, mit langer Spitze über dem Nasenrücken", "large symmetric scale plate covering nearly the whole forehead and both brow ridges, layered in leaf-like segments, with a long point down the bridge of the nose",
          136841);
        S("Schuppenkragen, Hals", "collar of scales on the neck",
          "spitze Platten an Wangen und Kiefer und ein dichter Kragen sich überlappender Schuppen über Kehle und Hals", "pointed plates flanking cheeks and jaw and a dense collar of overlapping scales over throat and neck",
          136842);
        S("Wangenstacheln", "spikes on the cheek",
          "lange, nach vorn gerichtete Schuppenstacheln, die von der Wange über das Gesicht hinausragen, dazu ein breites Schuppenfeld über Wange und Kiefer und ein Schuppenkragen am Hals", "long forward-pointing scale spikes projecting from the cheek out past the face, with a wide field of scales over cheek and jaw and a scaled collar down the neck",
          136843);
        S("Perlleiste am Oberlid", "beaded ridge on the upper lid",
          "Nahaufnahme des Auges: perlenartig gegliederte Schuppenleiste entlang des Oberlids, die Wimpern zurückhaltend", "close-up of the eye: beaded scale ridge running along the upper lid, lashes restrained",
          136844);
        S("Betonte Perlleiste", "emphasised beaded ridge",
          "Nahaufnahme des Auges: dieselbe Perlleiste am Oberlid, darunter ein breiteres dunkles Band und stärkere Wimpern am Unterlid", "close-up of the eye: the same beaded ridge on the upper lid, with a broader dark band beneath it and stronger lashes on the lower lid",
          136845);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136846);
        S("Limbalring, ein Auge", "limbal ring on one eye",
          "Nahaufnahme eines Auges: breiter Ring, der die Iris an ihrem äußeren Rand rundum einfasst", "close-up of one eye: a broad ring framing the iris all the way round at its outer edge",
          136847);

        // ---- feat-elezen.cs ----
        // Facial Features / Ear Clasps / Tattoos — ELEZEN (rows 4, 5, 6, 7)
        // Authored 2026-08-09 from the contact sheets in
        // tools\icons\sheets\Facial_Features\. Every icon id below was COPIED from the
        // cell label, never inferred from the cell's position; all 112 labels were
        // cross-checked against tools\icons\idx-Facial_Features.tsv.
        // Menu layout per the index file:
        //   Elezen Wildwood  male/female : slots 1-5 Facial Features, slots 6-7 Ear Clasps
        //   Elezen Duskwight male/female : slots 1-5 Facial Features, slots 6-7 Tattoos
        // NO SIDE IS NAMED here any more - see the SIDES section in the class summary.
        // The ear clasp and the cheek scar are each a mirrored pair, so both entries now
        // carry the SAME text and read "an einem Ohr" / "auf einer Wange".
        // NOT INCLUDED — 12 icons whose feature could not be identified, see the report:
        //   132311 132312 132321 132322 132331 132332 132341 132342
        //   132811 132821 132831 132841

        // 04_Elezen_Wildwood_male_face1
        S("Bartfleck unter der Lippe", "patch under the lip",
          "kleiner Bartfleck direkt unter der Unterlippe, nach unten spitz zulaufend; der Rest des Gesichts ist glatt",
          "small patch of beard directly under the lower lip, tapering to a point; the rest of the face is clean",
          132111);
        S("Kinnbackenbart", "jawline beard",
          "schmaler, borstiger Bartstreifen, der von den Koteletten am Kieferrand entlang bis zum Kinn läuft; Wangen und Oberlippe bleiben frei",
          "narrow bristly strip of beard running from the sideburns along the jaw to the chin; cheeks and upper lip stay bare",
          132112);
        S("Narbe quer übers Auge", "scar across the eye",
          "senkrechte Narbe, die die Augenbraue durchtrennt und unterhalb des Auges auf der Wange weiterläuft",
          "vertical scar cutting through the eyebrow and continuing below the eye onto the cheek",
          132113);
        S("lange Schläfennarbe", "long temple scar",
          "lange schräge Narbe vom Haaransatz über die Schläfe und das äußere Brauenende hinunter bis auf die Wange",
          "long diagonal scar from the hairline across the temple and the outer end of the eyebrow down onto the cheek",
          132114);
        S("zwei Schnitte auf der Wange", "two cuts on the cheek",
          "zwei kurze, parallel gegeneinander versetzte Schnitte schräg über der Wangenmitte",
          "two short cuts, offset parallel to one another, running diagonally across the middle of the cheek",
          132115);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132116);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132117);

        // 04_Elezen_Wildwood_male_face2
        S("Schnauzer und Kinnbart", "moustache and chin beard",
          "voller Schnurrbart über der Oberlippe und dazu ein spitz zulaufender Kinnbart unter der Unterlippe; die Wangen bleiben frei",
          "full moustache over the upper lip plus a tapering chin beard below the lower lip; the cheeks stay bare",
          132121);
        S("Kieferbart bis zum Kinn", "jaw beard to the chin",
          "kurzer, dichter Bart, der von den Koteletten am Kiefer entlang läuft und das Kinn mit einschließt; die Oberlippe bleibt frei",
          "short dense beard running from the sideburns along the jaw and taking in the chin; the upper lip stays bare",
          132122);
        S("Wangennarbe", "cheek scar",
          "lange schräge Narbe auf einer Wange, die am inneren Augenwinkel ansetzt und bis zum Kiefer hinunterzieht",
          "long diagonal scar on one cheek, starting at the inner corner of the eye and running down to the jaw",
          132123);
        S("Wangennarbe", "cheek scar",
          "lange schräge Narbe auf einer Wange, die am inneren Augenwinkel ansetzt und bis zum Kiefer hinunterzieht",
          "long diagonal scar on one cheek, starting at the inner corner of the eye and running down to the jaw",
          132124);
        S("Narbe quer über der Stirn", "scar across the forehead",
          "waagrechte Narbe, die oberhalb der Augenbrauen quer über die Stirn läuft",
          "horizontal scar running across the forehead above the eyebrows",
          132125);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132126);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132127);

        // 04_Elezen_Wildwood_male_face3
        S("Stoppeln am Kinn", "stubble on the chin",
          "dünner, kurzer Stoppelbewuchs auf dem Kinn und an dessen Unterseite; Wangen und Oberlippe bleiben frei",
          "thin, short stubble on the chin and along its underside; cheeks and upper lip stay bare",
          132131);
        S("schmaler Schnurrbart", "thin moustache",
          "schmaler, gerade gezogener Schnurrbart, der die ganze Oberlippe entlangläuft",
          "narrow, straight moustache running the whole width of the upper lip",
          132132);
        S("Narbe quer über die Stirn", "scar across the forehead",
          "lange Narbe, die schräg über die ganze Stirn zieht, vom Haaransatz bis zum inneren Ende der gegenüberliegenden Augenbraue",
          "long scar running diagonally across the whole forehead, from the hairline to the inner end of the opposite eyebrow",
          132133);
        S("gezackte Narbe unter dem Auge", "jagged scar under the eye",
          "kräftige, gezackte Narbe, die unter dem inneren Augenwinkel beginnt und steil schräg über die Wange abwärts läuft",
          "thick, jagged scar starting below the inner corner of the eye and running steeply down across the cheek",
          132134);
        S("kurze Narbe auf der Wange", "short scar on the cheek",
          "kurze, kerbige Narbe, die flach schräg mitten auf der Wange liegt, deutlich unterhalb des Auges",
          "short, notched scar lying at a shallow angle in the middle of the cheek, well below the eye",
          132135);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132136);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132137);

        // 04_Elezen_Wildwood_male_face4
        S("Vollbart ohne Schnauzer", "full beard, no moustache",
          "dichter, langer Bart über Wangen, Kiefer und Kinn, an den Koteletten angewachsen; die Oberlippe bleibt unbehaart",
          "dense, long beard over cheeks, jaw and chin, joined to the sideburns; the upper lip stays bare",
          132141);
        S("Hängeschnauzer", "drooping moustache",
          "Schnurrbart, dessen Enden beidseits am Mundwinkel vorbei nach unten hängen; die Mitte über der Oberlippe bleibt frei",
          "moustache whose ends hang down past both corners of the mouth; the middle above the upper lip stays bare",
          132142);
        S("Stirnnarbe", "forehead scar",
          "schräge Narbe, die vom Haaransatz über die Stirn bis dicht über die Augenbrauen läuft",
          "diagonal scar running from the hairline down the forehead to just above the eyebrows",
          132143);
        S("gekreuzte Narben am Auge", "crossed scars at the eye",
          "zwei Narben, die sich am inneren Ende der Augenbraue kreuzen: eine lange, flach schräg über die Wange nach hinten, eine steil abwärts zum Kiefer",
          "two scars crossing at the inner end of the eyebrow: one long and shallow, running back across the cheek, the other steeply down toward the jaw",
          132144);
        S("geknickte Narbe auf der Wange", "bent scar on the cheek",
          "zwei schräge Schnitte, die fast aneinanderstoßen und eine geknickte Linie vom Kiefer hinauf zum Wangenknochen bilden",
          "two diagonal cuts meeting almost end to end, forming a bent line from the jaw up to the cheekbone",
          132145);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132146);
        S("Ohrspange, ein Ohr", "ear clasp, one ear",
          "Spange am unteren Rand eines Ohrs, nahe der Ohrspitze: ein breiter, mehrfach gerippter Bogen und daneben zum Kopf hin ein kleineres Stück mit einem Dorn nach unten",
          "clasp on the lower edge of one ear, near the tip: a broad arch of several ribs with a smaller piece beside it toward the head, carrying a spur that points down",
          132147);

        // 05_Elezen_Wildwood_female_face1   (slots 1 and 2 not described — see header)
        S("Narbe über der Augenbraue", "scar above the eyebrow",
          "kurze, spindelförmige Narbe, die schräg auf der Stirn zwischen Augenbraue und Haaransatz liegt",
          "short, spindle-shaped scar set diagonally on the forehead between eyebrow and hairline",
          132313);
        S("Muttermal am Mundwinkel", "mole by the mouth",
          "kleines rundes Muttermal auf der Wange, ein Stück neben dem Mundwinkel",
          "small round mole on the cheek, a little way beside the corner of the mouth",
          132314);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal oben auf der Wange, dicht unter dem äußeren Augenwinkel",
          "small round mole high on the cheek, just below the outer corner of the eye",
          132315);
        S("kleine Ohrspange", "small ear clasp",
          "kleine Spange am unteren Ohrrand: ein gerippter, gerundeter Kopf, unter dem ein schmaler Dorn hervorsteht",
          "small clasp on the lower edge of the ear: a ribbed, rounded head with a narrow spur projecting below it",
          132316);
        S("breite Ohrspange", "broad ear clasp",
          "breite Spange am unteren Ohrrand: ein Bogen aus mehreren ineinanderliegenden Rippen legt sich über die Ohrkante, darunter ein kurzer stumpfer Fuß",
          "broad clasp on the lower edge of the ear: an arch of several nested ribs hooked over the ear's edge, with a short blunt foot below",
          132317);

        // 05_Elezen_Wildwood_female_face2   (slots 1 and 2 not described — see header)
        S("Narbe auf der Wange", "scar on the cheek",
          "einzelne lange, gerade Narbe, die vom äußeren Augenwinkel schräg abwärts über die Wange verläuft",
          "single long, straight scar running from the outer corner of the eye diagonally down across the cheek",
          132323);
        S("Muttermal neben dem Mund", "mole beside the mouth",
          "kleines rundes Muttermal auf der Wange, ein gutes Stück hinter dem Mundwinkel und auf gleicher Höhe",
          "small round mole on the cheek, well behind the corner of the mouth and level with it",
          132324);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf dem Wangenknochen, knapp unter dem äußeren Augenwinkel",
          "small round mole on the cheekbone, just below the outer corner of the eye",
          132325);
        S("kleine Ohrspange", "small ear clasp",
          "kleine Spange am unteren Ohrrand: ein gerippter, gerundeter Kopf, unter dem ein schmaler Dorn hervorsteht",
          "small clasp on the lower edge of the ear: a ribbed, rounded head with a narrow spur projecting below it",
          132326);
        S("breite Ohrspange", "broad ear clasp",
          "breite Spange am unteren Ohrrand: ein Bogen aus mehreren ineinanderliegenden Rippen legt sich über die Ohrkante, darunter ein kurzer stumpfer Fuß",
          "broad clasp on the lower edge of the ear: an arch of several nested ribs hooked over the ear's edge, with a short blunt foot below",
          132327);

        // 05_Elezen_Wildwood_female_face3   (slots 1 and 2 not described — see header)
        S("Narbe über und unter dem Auge", "scar above and below the eye",
          "senkrechte Narbe in zwei Abschnitten: einer über der Augenbraue, der längere unter dem Auge abwärts über die Wange",
          "vertical scar in two parts: one above the eyebrow, the longer one below the eye running down the cheek",
          132333);
        S("Muttermal unter dem Mundwinkel", "mole below the mouth corner",
          "kleines rundes Muttermal auf der Wange, schräg unterhalb und hinter dem Mundwinkel",
          "small round mole on the cheek, diagonally below and behind the corner of the mouth",
          132334);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal oben auf der Wange, unter dem äußeren Augenwinkel",
          "small round mole high on the cheek, below the outer corner of the eye",
          132335);
        S("kleine Ohrspange", "small ear clasp",
          "kleine Spange am unteren Ohrrand: ein gerippter, gerundeter Kopf, unter dem ein schmaler Dorn hervorsteht",
          "small clasp on the lower edge of the ear: a ribbed, rounded head with a narrow spur projecting below it",
          132336);
        S("breite Ohrspange", "broad ear clasp",
          "breite Spange am unteren Ohrrand: ein Bogen aus mehreren ineinanderliegenden Rippen legt sich über die Ohrkante, darunter ein kurzer stumpfer Fuß",
          "broad clasp on the lower edge of the ear: an arch of several nested ribs hooked over the ear's edge, with a short blunt foot below",
          132337);

        // 05_Elezen_Wildwood_female_face4   (slots 1 and 2 not described — see header)
        S("senkrechte Stirnnarbe", "vertical forehead scar",
          "lange, leicht geschwungene Narbe, die senkrecht über die Stirn vom Haaransatz bis dicht über die Augenbraue läuft",
          "long, slightly curved scar running vertically down the forehead from the hairline to just above the eyebrow",
          132343);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf der Wange, knapp unter dem äußeren Augenwinkel",
          "small round mole on the cheek, just below the outer corner of the eye",
          132344);
        S("Muttermal am Mundwinkel", "mole by the mouth corner",
          "kleines rundes Muttermal auf der Wange, dicht hinter dem Mundwinkel und auf gleicher Höhe",
          "small round mole on the cheek, close behind the corner of the mouth and level with it",
          132345);
        S("kleine Ohrspange", "small ear clasp",
          "kleine Spange am unteren Ohrrand: ein gerippter, gerundeter Kopf, unter dem ein schmaler Dorn hervorsteht",
          "small clasp on the lower edge of the ear: a ribbed, rounded head with a narrow spur projecting below it",
          132346);
        S("breite Ohrspange", "broad ear clasp",
          "breite Spange am unteren Ohrrand: ein Bogen aus mehreren ineinanderliegenden Rippen legt sich über die Ohrkante, darunter ein kurzer stumpfer Fuß",
          "broad clasp on the lower edge of the ear: an arch of several nested ribs hooked over the ear's edge, with a short blunt foot below",
          132347);

        // 06_Elezen_Duskwight_male_face1
        S("langer Kinnbart", "long chin beard",
          "langer, spitz auslaufender Kinnbart, der vom Kinn deutlich über den Kieferrand hinunterhängt; Wangen und Oberlippe bleiben frei",
          "long, tapering chin beard hanging well below the jaw line; cheeks and upper lip stay bare",
          132611);
        S("voller Schnauzer", "full moustache",
          "dichter Schnurrbart über der ganzen Oberlippe, dessen Enden sich an den Mundwinkeln nach unten biegen",
          "thick moustache over the whole upper lip, its ends curving down at the corners of the mouth",
          132612);
        S("Narbe quer übers Auge", "scar across the eye",
          "schräge Narbe in zwei Abschnitten: der obere zieht vom Haaransatz über die Augenbraue, der untere setzt unter dem Auge an und läuft über die Wange weiter",
          "diagonal scar in two parts: the upper one runs from the hairline across the eyebrow, the lower one starts below the eye and continues across the cheek",
          132613);
        S("Narbe über der Nase", "scar across the nose",
          "waagrechte Narbe, die knapp unter den Augen quer über den Nasenrücken von einer Wange zur anderen läuft",
          "horizontal scar running just below the eyes, straight across the bridge of the nose from one cheek to the other",
          132614);
        S("kerbige Wangennarbe", "notched cheek scar",
          "kurze, dicke Narbe mit kerbigem Rand, die schräg mitten auf der Wange liegt",
          "short, thick scar with a notched edge, lying diagonally in the middle of the cheek",
          132615);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein widerhakenartig gegabeltes Zeichen auf der Schläfe über dem äußeren Brauenende und ein zweites, kleineres auf dem Wangenknochen, dessen langer Ausläufer zur Kinnlade hinunterschwingt",
          "tribal mark on one side of the face: a barbed, forked sign on the temple above the outer end of the eyebrow, and a second, smaller one on the cheekbone whose long tail sweeps down toward the jaw",
          132616);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein widerhakenartig gegabeltes Zeichen auf der Schläfe über dem äußeren Brauenende und ein zweites, kleineres auf dem Wangenknochen, dessen langer Ausläufer zur Kinnlade hinunterschwingt",
          "tribal mark on one side of the face: a barbed, forked sign on the temple above the outer end of the eyebrow, and a second, smaller one on the cheekbone whose long tail sweeps down toward the jaw",
          132617);

        // 06_Elezen_Duskwight_male_face2
        S("Schnauzer und Kinnbart", "moustache and chin beard",
          "Schnurrbart über der Oberlippe und dazu ein spitzer Kinnbart unter der Unterlippe; die Wangen bleiben frei",
          "moustache over the upper lip plus a pointed chin beard below the lower lip; the cheeks stay bare",
          132621);
        S("Kinnbackenbart", "jawline beard",
          "borstiger Bartstreifen, der von den Koteletten am Kieferrand entlang bis zum Kinn läuft; Wangen und Oberlippe bleiben frei",
          "bristly strip of beard running from the sideburns along the jaw to the chin; cheeks and upper lip stay bare",
          132622);
        S("Stirnnarbe", "forehead scar",
          "lange, leicht gewellte Narbe, die schräg über die Stirn vom Haaransatz bis zwischen die Augenbrauen läuft",
          "long, slightly wavy scar running diagonally down the forehead from the hairline to between the eyebrows",
          132623);
        S("lange Narbe über die Wange", "long scar across the cheek",
          "sehr lange Narbe auf einer Gesichtshälfte, die am Nasenrücken zwischen den Augen ansetzt und schräg über die ganze Wange bis zum Kiefer zieht",
          "very long scar on one side of the face, starting at the bridge of the nose between the eyes and running diagonally across the whole cheek to the jaw",
          132624);
        S("Narbe unter dem Auge", "scar below the eye",
          "lange, gerade Narbe auf einer Wange, die unter dem Auge ansetzt und schräg nach hinten unten zum Kiefer läuft",
          "long, straight scar on one cheek, starting below the eye and running diagonally back and down to the jaw",
          132625);
        S("Tätowierung", "tattoo",
          "großflächiges Stammeszeichen auf einer Gesichtshälfte: ein geschwungenes Zeichen von der Braue über die Schläfe bis zum Haaransatz, ein vielarmiges Zeichen auf dem Wangenknochen und darunter ein weiterer Haken zur Kinnlade hin",
          "large tribal mark on one side of the face: a curling sign from the brow across the temple to the hairline, a many-armed sign on the cheekbone, and a further hook below it toward the jaw",
          132626);
        S("Tätowierung", "tattoo",
          "großflächiges Stammeszeichen auf einer Gesichtshälfte: ein geschwungenes Zeichen von der Braue über die Schläfe bis zum Haaransatz, ein vielarmiges Zeichen auf dem Wangenknochen und darunter ein weiterer Haken zur Kinnlade hin",
          "large tribal mark on one side of the face: a curling sign from the brow across the temple to the hairline, a many-armed sign on the cheekbone, and a further hook below it toward the jaw",
          132627);

        // 06_Elezen_Duskwight_male_face3
        S("Stoppeln am Kiefer", "stubble along the jaw",
          "dünner, kurzer Stoppelbewuchs am Kieferrand und unter dem Kinn; Wangen und Oberlippe bleiben frei",
          "thin, short stubble along the jaw and under the chin; cheeks and upper lip stay bare",
          132631);
        S("schmaler Schnurrbart", "thin moustache",
          "schmaler, gerade gezogener Schnurrbart, der die ganze Oberlippe entlangläuft und etwas über die Mundwinkel hinausreicht",
          "narrow, straight moustache running the whole width of the upper lip and reaching a little past its corners",
          132632);
        S("dünne Stirnnarbe", "thin forehead scar",
          "lange, dünne, gerade Narbe, die schräg über die Stirn vom Haaransatz bis zum inneren Ende der Augenbraue läuft",
          "long, thin, straight scar running diagonally down the forehead from the hairline to the inner end of the eyebrow",
          132633);
        S("Narbe zur Ohrseite", "scar toward the ear",
          "gerade Narbe auf einer Wange, die unten vorn ansetzt und schräg nach hinten oben zum Ohr hin ansteigt",
          "straight scar on one cheek, starting low at the front and rising diagonally back toward the ear",
          132634);
        S("kurze Narbe mit Knick", "short bent scar",
          "kurze Narbe mitten auf einer Wange, die an ihrem vorderen Ende leicht abknickt",
          "short scar in the middle of one cheek, bending slightly at its forward end",
          132635);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein widerhakiges Zeichen auf der Schläfe, eine Rosette aus mehreren gebogenen Blättern auf dem Wangenknochen und darunter ein weiterer Haken zur Kinnlade hin",
          "tribal mark on one side of the face: a barbed sign on the temple, a rosette of several curved leaves on the cheekbone, and a further hook below it toward the jaw",
          132636);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein widerhakiges Zeichen auf der Schläfe, eine Rosette aus mehreren gebogenen Blättern auf dem Wangenknochen und darunter ein weiterer Haken zur Kinnlade hin",
          "tribal mark on one side of the face: a barbed sign on the temple, a rosette of several curved leaves on the cheekbone, and a further hook below it toward the jaw",
          132637);

        // 06_Elezen_Duskwight_male_face4
        S("Vollbart ohne Schnauzer", "full beard, no moustache",
          "dichter, langer Bart über Wangen, Kiefer und Kinn, an den Koteletten angewachsen; die Oberlippe bleibt unbehaart",
          "dense, long beard over cheeks, jaw and chin, joined to the sideburns; the upper lip stays bare",
          132641);
        S("Hängeschnauzer", "drooping moustache",
          "Schnurrbart, dessen lange Enden beidseits am Mundwinkel vorbei bis zum Kiefer hinunterhängen; das Kinn bleibt frei",
          "moustache whose long ends hang past both corners of the mouth down to the jaw; the chin stays bare",
          132642);
        S("Narbe quer übers Auge", "scar across the eye",
          "senkrechte Narbe in zwei Abschnitten: der obere von der Stirn bis zur Augenbraue, der untere unter dem Auge abwärts über die Wange",
          "vertical scar in two parts: the upper one from the forehead to the eyebrow, the lower one below the eye running down the cheek",
          132643);
        S("Narbe vom Auge zum Kiefer", "scar from eye to jaw",
          "lange, dünne Narbe, die am äußeren Augenwinkel ansetzt und schräg nach hinten unten über die Wange zum Kiefer läuft",
          "long, thin scar starting at the outer corner of the eye and running diagonally back and down across the cheek to the jaw",
          132644);
        S("Narbe quer über der Stirn", "scar across the forehead",
          "waagrechte, kerbige Narbe, die oberhalb der Augenbrauen quer über die Stirn läuft",
          "horizontal, notched scar running across the forehead above the eyebrows",
          132645);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein gegabeltes Zeichen über dem äußeren Brauenende und ein langer, gebogener Arm, der vom Wangenknochen die Wange hinunterschwingt",
          "tribal mark on one side of the face: a forked sign over the outer end of the eyebrow and a long, curved arm sweeping from the cheekbone down the cheek",
          132646);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein gegabeltes Zeichen über dem äußeren Brauenende und ein langer, gebogener Arm, der vom Wangenknochen die Wange hinunterschwingt",
          "tribal mark on one side of the face: a forked sign over the outer end of the eyebrow and a long, curved arm sweeping from the cheekbone down the cheek",
          132647);

        // 07_Elezen_Duskwight_female_face1   (slot 1 not described — see header)
        S("Narbe hinter dem Mundwinkel", "scar behind the mouth corner",
          "dünne, leicht erhabene Narbe auf der unteren Wange, die dicht hinter dem Mundwinkel ansetzt und schräg nach hinten unten zum Kiefer läuft",
          "thin, slightly raised scar on the lower cheek, starting just behind the corner of the mouth and running diagonally back and down to the jaw",
          132812);
        S("Mal am Ohrrand", "mark on the ear's edge",
          "kleiner, unregelmäßig gezackter Fleck, der etwa auf halber Länge am unteren Rand des Spitzohrs sitzt",
          "small, irregularly jagged patch sitting about halfway along the lower edge of the pointed ear",
          132813);
        S("Muttermal am Mundwinkel", "mole by the mouth",
          "kleines rundes Muttermal auf der Wange, ein Stück neben dem Mundwinkel und auf gleicher Höhe",
          "small round mole on the cheek, a little way beside the corner of the mouth and level with it",
          132814);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf dem Wangenknochen, ein Stück unterhalb des Auges",
          "small round mole on the cheekbone, a little way below the eye",
          132815);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132816);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132817);

        // 07_Elezen_Duskwight_female_face2   (slot 1 not described — see header)
        S("kurze Narbe über der Braue", "short scar above the brow",
          "kurze, gerade Narbe auf der Stirn, die von der Schläfe schräg abwärts bis zum äußeren Ende der Augenbraue läuft",
          "short, straight scar on the forehead, running diagonally down from the temple to the outer end of the eyebrow",
          132822);
        S("Mal am Ohrrand", "mark on the ear's edge",
          "kleiner, unregelmäßig gezackter Fleck, der etwa auf halber Länge am unteren Rand des Spitzohrs sitzt",
          "small, irregularly jagged patch sitting about halfway along the lower edge of the pointed ear",
          132823);
        S("Muttermal am Mundwinkel", "mole by the mouth",
          "kleines rundes Muttermal auf der Wange, schräg neben und etwas unterhalb des Mundwinkels",
          "small round mole on the cheek, diagonally beside and a little below the corner of the mouth",
          132824);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf dem Wangenknochen, ein Stück unterhalb des Auges",
          "small round mole on the cheekbone, a little way below the eye",
          132825);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132826);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132827);

        // 07_Elezen_Duskwight_female_face3   (slot 1 not described — see header)
        S("Narbe über dem Nasenrücken", "scar over the bridge of the nose",
          "schräge, leicht erhabene Narbe, die zwischen den Augenbrauen ansetzt und über den Nasenrücken bis zur Nasenseite hinunterläuft",
          "diagonal, slightly raised scar starting between the eyebrows and running down over the bridge of the nose to its side",
          132832);
        S("Mal am Ohrrand", "mark on the ear's edge",
          "kleiner, unregelmäßig gezackter Fleck, der etwa auf halber Länge am unteren Rand des Spitzohrs sitzt",
          "small, irregularly jagged patch sitting about halfway along the lower edge of the pointed ear",
          132833);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf der Wange, ein Stück unter dem äußeren Augenwinkel",
          "small round mole on the cheek, a little way below the outer corner of the eye",
          132834);
        S("Muttermal am Mundwinkel", "mole by the mouth",
          "kleines rundes Muttermal auf der Wange, ein Stück hinter dem Mundwinkel und auf gleicher Höhe",
          "small round mole on the cheek, a little way behind the corner of the mouth and level with it",
          132835);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132836);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132837);

        // 07_Elezen_Duskwight_female_face4   (slot 1 not described — see header)
        S("kräftige Stirnnarbe", "bold forehead scar",
          "dicke, erhabene Narbe mitten auf der Stirn, die schräg vom Haaransatz herab bis über das innere Brauenende läuft",
          "thick, raised scar in the middle of the forehead, running diagonally down from the hairline to above the inner end of the eyebrow",
          132842);
        S("Mal am Ohrrand", "mark on the ear's edge",
          "kleiner, unregelmäßig gezackter Fleck, der etwa auf halber Länge am unteren Rand des Spitzohrs sitzt",
          "small, irregularly jagged patch sitting about halfway along the lower edge of the pointed ear",
          132843);
        S("Muttermal am Mundwinkel", "mole by the mouth",
          "kleines rundes Muttermal auf der Wange, schräg hinter und etwas unterhalb des Mundwinkels",
          "small round mole on the cheek, diagonally behind and a little below the corner of the mouth",
          132844);
        S("Muttermal unter dem Auge", "mole under the eye",
          "kleines rundes Muttermal auf der Wange, knapp unter und etwas außerhalb des äußeren Augenwinkels",
          "small round mole on the cheek, just below and a little outside the outer corner of the eye",
          132845);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132846);
        S("Tätowierung", "tattoo",
          "Stammeszeichen auf einer Gesichtshälfte: ein schlanker, widerhakiger Bogen, der vom äußeren Brauenende auf die Schläfe hinaufzieht, und ein spinnenartiges Zeichen auf dem Wangenknochen, dessen längster Arm die Wange hinunterläuft",
          "tribal mark on one side of the face: a slender barbed hook rising from the outer end of the eyebrow onto the temple, and a spidery sign on the cheekbone whose longest arm runs down the cheek",
          132847);

        // ---- feat-hrothgar.cs ----
        // Hrothgar - CharaMakeType.FacialFeatureOption
        // Slots 1-5 = the 5-entry menu ("Facial Features" male / "Other Features" female),
        // slots 6-7 = the 2-entry "Tattoos" menu. Icon ids copied from the cell labels.
        // Structural descriptions only - no colour, no coat pattern.

        // 24_Hrothgar_Helions_male_face1
        S("Glattes Ohr", "smooth ear",
          "kleines, spitz zulaufendes Ohr mit glattem Rand, das aus der Mähne ragt", "small tapering ear with a smooth rim, standing out of the mane",
          137151);
        S("Genähte Narbe", "stitched scar",
          "lange Narbe mit Nahtspuren, die schräg vom Nasenrücken über die Wange bis zum Kiefer verläuft", "long scar with suture marks running diagonally from the bridge of the nose across the cheek to the jaw",
          137152);
        S("Voller Kinnbart", "full chin beard",
          "dichter, langer Bart, der Kinn und Unterkiefer bedeckt und gerade endet", "dense long beard covering the chin and lower jaw, ending in a straight edge",
          137153);
        S("Feine Schnurrhaare", "fine whiskers",
          "lange, dünne Schnurrhaare, die beidseits von der Schnauze abstehen, dazu kurzes, zottiges Wangenfell", "long thin whiskers standing out from both sides of the muzzle, with short shaggy fur on the cheek",
          137154);
        S("Genähte Stirnnarbe", "stitched forehead scar",
          "Narbe mit Nahtspuren, die schräg über die Stirn bis zwischen die Brauen läuft", "scar with suture marks running diagonally down the forehead to between the brows",
          137155);
        S("Sichel unter dem Auge", "crescent under the eye",
          "breite Sichel, die dem Unterlid folgt und vom inneren Augenwinkel bis über den äußeren hinaus reicht", "broad crescent following the lower lid, reaching from the inner corner of the eye past the outer one",
          137156);
        S("Tropfen über dem Auge", "teardrop above the eye",
          "tropfenförmiges Zeichen über dem Auge, oben breit und rund, das nach unten spitz zum inneren Augenwinkel ausläuft", "teardrop-shaped mark above the eye, broad and round at the top, tapering to a point toward the inner corner of the eye",
          137157);

        // 24_Hrothgar_Helions_male_face2
        S("Ohr mit Haarsaum", "ear with a fringe of hair",
          "breites Ohr, dessen Oberkante ein dichter Saum spitz abstehender Haare säumt", "broad ear whose upper edge is lined with a dense fringe of sharply protruding hairs",
          137161);
        S("Drei Kratznarben", "three claw scars",
          "drei parallele Kratznarben, die schräg von Augenhöhe über die Wange bis zum Kiefer ziehen", "three parallel claw scars running diagonally from eye level across the cheek to the jaw",
          137162);
        S("Bart am Kieferrand", "beard along the jaw",
          "zottiger Bartsaum, der dem Unterkiefer folgt und unter dem Kinn spitz ausläuft", "shaggy beard fringe following the lower jaw and tapering to a point beneath the chin",
          137163);
        S("Hängender Schnauzbart", "drooping moustache",
          "langer Schnauzbart, der beidseits der Nase herabhängt und über die Mundwinkel hinausreicht", "long moustache hanging down on both sides of the nose and reaching past the corners of the mouth",
          137164);
        S("Brauenbüschel", "brow tufts",
          "abstehende Haarbüschel über beiden Augen, dazu ein Fellkamm, der mittig über die Stirn nach oben läuft", "protruding tufts of hair above both eyes, plus a crest of fur running up the middle of the forehead",
          137165);
        S("Haken unter dem Auge", "hook under the eye",
          "hakenförmiger Strich auf dem Wangenknochen unter dem Auge, der nach vorn unten in eine Spitze ausläuft", "hook-shaped stroke on the cheekbone below the eye, tapering to a point toward the front and below",
          137166);
        S("Gezacktes Feld über dem Auge", "jagged patch above the eye",
          "breites, in Zacken auslaufendes Feld über dem Auge, das die Braue bedeckt und zur Schläfe hin zeigt", "broad patch above the eye ending in jagged points, covering the brow and pointing toward the temple",
          137167);

        // 24_Hrothgar_Helions_male_face3
        S("Großes glattes Ohr", "large smooth ear",
          "großes, breites Ohr mit glattem Rand, das frei aus der Mähne aufragt", "large broad ear with a smooth rim, rising clear of the mane",
          137171);
        S("Schwere Schnurrhaare", "heavy whiskers",
          "kräftige, gedrehte Schnurrhaare, die beidseits der Schnauze weit nach außen und unten schwingen", "thick twisted whiskers sweeping far outward and downward from both sides of the muzzle",
          137172);
        S("Bart mit Zackenrand", "beard with a serrated edge",
          "kurzer Bart unter der Schnauze, dessen Unterkante in eine Reihe spitzer Zacken geschnitten ist", "short beard beneath the muzzle whose lower edge is cut into a row of sharp points",
          137173);
        S("Wangenkranz", "cheek ruff",
          "langer, zottiger Haarkranz, der von der Schläfe über die Wange nach unten absteht", "long shaggy ruff of hair standing out from the temple down across the cheek",
          137174);
        S("Kratznarben auf der Stirn", "claw scars on the forehead",
          "drei lange, schmale Kratznarben, die schräg über die Stirn zwischen den Brauen verlaufen", "three long narrow claw scars running diagonally across the forehead between the brows",
          137175);
        S("Zwei Dreiecke am Augenwinkel", "two triangles at the corner of the eye",
          "zwei Dreiecke am äußeren Augenwinkel, versetzt übereinander, mit den Spitzen zum Auge weisend", "two triangles at the outer corner of the eye, offset one above the other, their points aimed at the eye",
          137176);
        S("Drei Rauten über der Braue", "three lozenges above the brow",
          "drei längliche Rauten in einer Reihe über der Braue, die zur Schläfe hin ansteigen", "three elongated lozenges in a row above the brow, rising toward the temple",
          137177);

        // 24_Hrothgar_Helions_male_face4
        S("Ohr mit Borstensaum", "ear with a bristly edge",
          "spitzes Ohr, dessen Oberkante ein schmaler Saum kurzer, borstiger Haare begleitet", "pointed ear whose upper edge is accompanied by a narrow band of short bristly hairs",
          137181);
        S("Langes glattes Ohr", "long smooth ear",
          "langes, glattes Ohr von vorn gesehen, das sich spitz nach hinten biegt; das zweite Ohr steht dahinter", "long smooth ear seen from the front, curving back to a point; the second ear stands behind it",
          137182);
        S("Kinnbüschel", "chin tuft",
          "dichtes Haarbüschel, das unter dem Kinn herabhängt und in Spitzen ausläuft", "dense tuft of hair hanging beneath the chin and ending in points",
          137183);
        S("Struppiger Wangensaum", "bristly cheek fringe",
          "kurzer, struppiger Haarsaum, der entlang der Wange absteht", "short bristly fringe of hair standing out along the cheek",
          137184);
        S("Falten auf dem Nasenrücken", "furrows on the bridge of the nose",
          "tiefe Falten, die vom Nasenspiegel über den Nasenrücken nach oben verlaufen", "deep furrows running upward from the nose pad across the bridge of the nose",
          137185);
        S("Großes Wangenmuster", "large cheek pattern",
          "großflächiges Muster über die ganze Wange: eine Reihe langer Zacken zum Kiefer hin, Schnörkel neben Nase und Maul, ein Bogen aus Punkten über dem Wangenknochen und zwei Striche am Kinn", "large pattern across the whole cheek: a row of long spikes toward the jaw, scrollwork beside the nose and mouth, an arc of dots over the cheekbone and two strokes on the chin",
          137186);
        S("Knotenmuster an der Schläfe", "knotwork on the temple",
          "verschlungenes Knotenmuster an der Schläfe über dem Auge, an der Unterkante von einer Punktreihe begleitet und oben von kleinen Zacken gesäumt", "interlaced knotwork on the temple above the eye, accompanied by a row of dots along its lower edge and edged with small spikes on top",
          137187);

        // 25_Hrothgar_Helions_female_face1
        S("Spitzes Ohr", "pointed ear",
          "schlankes, spitz zulaufendes Ohr mit glattem Rand, das aus der Mähne ragt; am Ohrgrund steht feines Fell", "slender tapering ear with a smooth rim, standing out of the mane; fine fur sits at its base",
          137351);
        S("Lidstrich", "line along the lid",
          "schmaler Strich entlang des oberen Lids, der über den äußeren Augenwinkel hinaus spitz ausläuft", "narrow line along the upper lid, tapering to a point beyond the outer corner of the eye",
          137352);
        S("Haare über dem Auge", "hairs above the eye",
          "einzelne feine, lange Haare, die über der Braue entspringen und nach oben zur Schläfe hin auffächern", "individual fine long hairs springing from above the brow and fanning upward toward the temple",
          137353);
        S("Lange Schnurrhaare", "long whiskers",
          "lange, feine Schnurrhaare, die beidseits der Schnauze weit nach außen auffächern", "long fine whiskers fanning far outward from both sides of the muzzle",
          137354);
        S("Stirnschmuck", "forehead ornament",
          "rautenförmiger Schmuckstein in gefasstem Rahmen, mittig auf der Stirn über den Brauen", "lozenge-shaped gem in a mounted frame, centred on the forehead above the brows",
          137355);
        S("Sichel unter dem Auge", "crescent under the eye",
          "schmale Sichel, die dem Unterlid folgt und zum äußeren Augenwinkel hin ansteigt", "narrow crescent following the lower lid and rising toward the outer corner of the eye",
          137356);
        S("Punktgruppe an der Schläfe", "cluster of spots on the temple",
          "drei rundliche, oben eingekerbte Flecken im Dreieck an der Schläfe über dem Auge, darunter ein kleinerer vierter", "three rounded spots notched at the top, set in a triangle on the temple above the eye, with a smaller fourth one below",
          137357);

        // 25_Hrothgar_Helions_female_face2
        S("Ohr mit hohem Büschel", "ear with a tall tuft",
          "Ohr, das tief in der Mähne sitzt und dessen Oberkante ein hoher, dichter Saum spitzer Haare krönt", "ear set deep in the mane, its upper edge crowned by a tall dense fringe of pointed hairs",
          137361);
        S("Strich unter dem Auge", "line under the eye",
          "kräftiger, gebogener Strich dicht unter dem Unterlid, der vom inneren Augenwinkel nach außen zieht und dort ansteigt", "bold curved line close beneath the lower lid, running from the inner corner outward and rising at the outer end",
          137362);
        S("Lidstrich", "line along the lid",
          "Strich entlang des oberen Lids, der über den äußeren Augenwinkel hinaus in eine feine Spitze ausgezogen ist", "line along the upper lid drawn out beyond the outer corner of the eye into a fine point",
          137363);
        S("Lange Wimpern", "long lashes",
          "lange, dichte Wimpern am oberen Lid, die sich zum äußeren Augenwinkel hin auffächern, dazu Wimpern am Unterlid", "long dense lashes on the upper lid fanning out toward the outer corner of the eye, with lashes on the lower lid too",
          137364);
        S("Buschige Braue", "bushy brow",
          "kräftige, buschige Braue aus längerem Fell, die sich über dem Auge wölbt", "strong bushy brow of longer fur arching above the eye",
          137365);
        S("Breiter Bogen unter dem Auge", "broad arc under the eye",
          "breiter, geschwungener Bogen unterhalb und außerhalb des Auges, in der Mitte am dicksten und an beiden Enden spitz", "broad sweeping arc below and outside the eye, thickest in the middle and pointed at both ends",
          137366);
        S("Zackenstriche über dem Auge", "jagged strokes above the eye",
          "mehrere spitz zulaufende Zackenstriche über dem Auge, ein langer von der Schläfe zur inneren Braue und zwei kürzere daneben", "several sharply tapering jagged strokes above the eye, one long one from the temple to the inner brow and two shorter ones beside it",
          137367);

        // 25_Hrothgar_Helions_female_face3
        S("Schmales langes Ohr", "narrow long ear",
          "langes, schmales Ohr, das sich zu einer feinen, leicht gebogenen Spitze verjüngt; am Ohrgrund steht ein Fellkranz", "long narrow ear tapering to a fine, slightly curved point; a ruff of fur sits at its base",
          137371);
        S("Haare am Ohr", "hairs at the ear",
          "mehrere lange, feine Haare, die am Ohrgrund entspringen und über den Ohrrand hinaus auffächern", "several long fine hairs springing from the base of the ear and fanning out beyond its rim",
          137372);
        S("Wimpernfächer", "fan of lashes",
          "ein Fächer langer Wimpern am äußeren Augenwinkel, der nach außen und unten absteht", "a fan of long lashes at the outer corner of the eye, standing out to the side and downward",
          137373);
        S("Strich am Unterlid", "line on the lower lid",
          "gebogener Strich, der dem Unterlid folgt und zum äußeren Augenwinkel hin ausläuft", "curved line following the lower lid and running out toward the outer corner of the eye",
          137374);
        S("Lange Schnurrhaare", "long whiskers",
          "lange, gedrehte Schnurrhaare, die beidseits der Schnauze weit über das Gesicht hinausreichen", "long twisted whiskers reaching far beyond the face on both sides of the muzzle",
          137375);
        S("Drei Spitzen über der Braue", "three spikes above the brow",
          "drei nach oben spitz zulaufende Zacken in einer Reihe über der Braue, zur Schläfe hin größer werdend", "three upward-tapering spikes in a row above the brow, growing larger toward the temple",
          137376);
        S("Zwei Keile am Augenwinkel", "two wedges at the corner of the eye",
          "zwei kantige Keile jenseits des äußeren Augenwinkels, schräg übereinander gesetzt", "two angular wedges beyond the outer corner of the eye, set diagonally one above the other",
          137377);

        // 25_Hrothgar_Helions_female_face4
        S("Ohr mit Haarsaum", "ear with a fringe of hair",
          "breites, gerundetes Ohr, dessen Oberkante ein langer Saum spitz abstehender Haare begleitet", "broad rounded ear whose upper edge is accompanied by a long fringe of sharply protruding hairs",
          137381);
        S("Ohrschmuck", "ear ornament",
          "rautenförmiges Schmuckstück mit gestuftem Rahmen am Ohrgrund, darunter eine kleine Kugel", "lozenge-shaped ornament with a stepped frame at the base of the ear, with a small bead below it",
          137382);
        S("Umrandetes Auge", "outlined eye",
          "schmale Umrandung, die Ober- und Unterlid rings um das Auge nachzeichnet", "narrow outline tracing the upper and lower lid all the way around the eye",
          137383);
        S("Lange Wimpern", "long lashes",
          "lange, dichte Wimpern, die das obere Lid säumen und sich am äußeren Augenwinkel auffächern", "long dense lashes lining the upper lid and fanning out at the outer corner of the eye",
          137384);
        S("Buschige Braue", "bushy brow",
          "kurze, buschige Braue aus längerem Fell, die sich schräg über dem Auge wölbt", "short bushy brow of longer fur arching diagonally above the eye",
          137385);
        S("Großes Gesichtsmuster", "large face pattern",
          "großflächiges Muster über die halbe Gesichtsseite, von Schläfe und Braue über die Wange bis zum Kiefer, aus ineinandergreifenden Schwüngen und Reihen von Dreiecken", "large pattern across half the side of the face, from temple and brow over the cheek to the jaw, made of interlocking curves and rows of triangles",
          137386);
        S("Stirnmuster", "forehead pattern",
          "symmetrisches, kronenartiges Zeichen mittig auf der Stirn zwischen den Brauen, von dem ein schmaler Steg über den Nasenrücken hinabläuft", "symmetrical crown-like mark centred on the forehead between the brows, with a narrow stem running down the bridge of the nose",
          137387);

        // 26_Hrothgar_The_Lost_male_face1
        S("Glattes Ohr", "smooth ear",
          "kleines, spitz zulaufendes Ohr mit glattem Rand, das aus der Mähne ragt", "small tapering ear with a smooth rim, standing out of the mane",
          137651);
        S("Genähte Narbe", "stitched scar",
          "lange Narbe mit Nahtspuren, die schräg vom Nasenrücken über die Wange bis zum Kiefer verläuft", "long scar with suture marks running diagonally from the bridge of the nose across the cheek to the jaw",
          137652);
        S("Voller Kinnbart", "full chin beard",
          "dichter, langer Bart, der Kinn und Unterkiefer bedeckt und gerade endet", "dense long beard covering the chin and lower jaw, ending in a straight edge",
          137653);
        S("Feine Schnurrhaare", "fine whiskers",
          "lange, dünne Schnurrhaare, die beidseits von der Schnauze abstehen, dazu kurzes, zottiges Wangenfell", "long thin whiskers standing out from both sides of the muzzle, with short shaggy fur on the cheek",
          137654);
        S("Genähte Stirnnarbe", "stitched forehead scar",
          "Narbe mit Nahtspuren, die schräg über die Stirn bis zwischen die Brauen läuft", "scar with suture marks running diagonally down the forehead to between the brows",
          137655);
        S("Sichel unter dem Auge", "crescent under the eye",
          "breite Sichel, die dem Unterlid folgt und vom inneren Augenwinkel bis über den äußeren hinaus reicht", "broad crescent following the lower lid, reaching from the inner corner of the eye past the outer one",
          137656);
        S("Tropfen über dem Auge", "teardrop above the eye",
          "tropfenförmiges Zeichen über dem Auge, oben breit und rund, das nach unten spitz zum inneren Augenwinkel ausläuft", "teardrop-shaped mark above the eye, broad and round at the top, tapering to a point toward the inner corner of the eye",
          137657);

        // 26_Hrothgar_The_Lost_male_face2
        S("Ohr mit Haarsaum", "ear with a fringe of hair",
          "breites Ohr, dessen Oberkante ein dichter Saum spitz abstehender Haare säumt", "broad ear whose upper edge is lined with a dense fringe of sharply protruding hairs",
          137661);
        S("Drei Kratznarben", "three claw scars",
          "drei parallele Kratznarben, die schräg von Augenhöhe über die Wange bis zum Kiefer ziehen", "three parallel claw scars running diagonally from eye level across the cheek to the jaw",
          137662);
        S("Bart am Kieferrand", "beard along the jaw",
          "zottiger Bartsaum, der dem Unterkiefer folgt und unter dem Kinn spitz ausläuft", "shaggy beard fringe following the lower jaw and tapering to a point beneath the chin",
          137663);
        S("Hängender Schnauzbart", "drooping moustache",
          "langer Schnauzbart, der beidseits der Nase herabhängt und über die Mundwinkel hinausreicht", "long moustache hanging down on both sides of the nose and reaching past the corners of the mouth",
          137664);
        S("Brauenbüschel", "brow tufts",
          "abstehende Haarbüschel über beiden Augen, dazu ein Fellkamm, der mittig über die Stirn nach oben läuft", "protruding tufts of hair above both eyes, plus a crest of fur running up the middle of the forehead",
          137665);
        S("Haken unter dem Auge", "hook under the eye",
          "hakenförmiger Strich auf dem Wangenknochen unter dem Auge, der nach vorn unten in eine Spitze ausläuft", "hook-shaped stroke on the cheekbone below the eye, tapering to a point toward the front and below",
          137666);
        S("Gezacktes Feld über dem Auge", "jagged patch above the eye",
          "breites, in Zacken auslaufendes Feld über dem Auge, das die Braue bedeckt und zur Schläfe hin zeigt", "broad patch above the eye ending in jagged points, covering the brow and pointing toward the temple",
          137667);

        // 26_Hrothgar_The_Lost_male_face3
        S("Großes glattes Ohr", "large smooth ear",
          "großes, breites Ohr mit glattem Rand, das frei aus der Mähne aufragt", "large broad ear with a smooth rim, rising clear of the mane",
          137671);
        S("Schwere Schnurrhaare", "heavy whiskers",
          "kräftige, gedrehte Schnurrhaare, die beidseits der Schnauze weit nach außen und unten schwingen", "thick twisted whiskers sweeping far outward and downward from both sides of the muzzle",
          137672);
        S("Bart mit Zackenrand", "beard with a serrated edge",
          "kurzer Bart unter der Schnauze, dessen Unterkante in eine Reihe spitzer Zacken geschnitten ist", "short beard beneath the muzzle whose lower edge is cut into a row of sharp points",
          137673);
        S("Wangenkranz", "cheek ruff",
          "langer, zottiger Haarkranz, der von der Schläfe über die Wange nach unten absteht", "long shaggy ruff of hair standing out from the temple down across the cheek",
          137674);
        S("Kratznarben auf der Stirn", "claw scars on the forehead",
          "drei lange, schmale Kratznarben, die schräg über die Stirn zwischen den Brauen verlaufen", "three long narrow claw scars running diagonally across the forehead between the brows",
          137675);
        S("Zwei Dreiecke am Augenwinkel", "two triangles at the corner of the eye",
          "zwei Dreiecke am äußeren Augenwinkel, versetzt übereinander, mit den Spitzen zum Auge weisend", "two triangles at the outer corner of the eye, offset one above the other, their points aimed at the eye",
          137676);
        S("Drei Rauten über der Braue", "three lozenges above the brow",
          "drei längliche Rauten in einer Reihe über der Braue, die zur Schläfe hin ansteigen", "three elongated lozenges in a row above the brow, rising toward the temple",
          137677);

        // 26_Hrothgar_The_Lost_male_face4
        S("Ohr mit Borstensaum", "ear with a bristly edge",
          "spitzes Ohr, dessen Oberkante ein schmaler Saum kurzer, borstiger Haare begleitet", "pointed ear whose upper edge is accompanied by a narrow band of short bristly hairs",
          137681);
        S("Langes glattes Ohr", "long smooth ear",
          "langes, glattes Ohr von vorn gesehen, das sich spitz nach hinten biegt; das zweite Ohr steht dahinter", "long smooth ear seen from the front, curving back to a point; the second ear stands behind it",
          137682);
        S("Kinnbüschel", "chin tuft",
          "dichtes Haarbüschel, das unter dem Kinn herabhängt und in Spitzen ausläuft", "dense tuft of hair hanging beneath the chin and ending in points",
          137683);
        S("Struppiger Wangensaum", "bristly cheek fringe",
          "kurzer, struppiger Haarsaum, der entlang der Wange absteht", "short bristly fringe of hair standing out along the cheek",
          137684);
        S("Falten auf dem Nasenrücken", "furrows on the bridge of the nose",
          "tiefe Falten, die vom Nasenspiegel über den Nasenrücken nach oben verlaufen", "deep furrows running upward from the nose pad across the bridge of the nose",
          137685);
        S("Großes Wangenmuster", "large cheek pattern",
          "großflächiges Muster über die ganze Wange: eine Reihe langer Zacken zum Kiefer hin, Schnörkel neben Nase und Maul, ein Bogen aus Punkten über dem Wangenknochen und zwei Striche am Kinn", "large pattern across the whole cheek: a row of long spikes toward the jaw, scrollwork beside the nose and mouth, an arc of dots over the cheekbone and two strokes on the chin",
          137686);
        S("Knotenmuster an der Schläfe", "knotwork on the temple",
          "verschlungenes Knotenmuster an der Schläfe über dem Auge, an der Unterkante von einer Punktreihe begleitet und oben von kleinen Zacken gesäumt", "interlaced knotwork on the temple above the eye, accompanied by a row of dots along its lower edge and edged with small spikes on top",
          137687);

        // 27_Hrothgar_The_Lost_female_face1
        S("Spitzes Ohr", "pointed ear",
          "schlankes, spitz zulaufendes Ohr mit glattem Rand, das aus der Mähne ragt; am Ohrgrund steht feines Fell", "slender tapering ear with a smooth rim, standing out of the mane; fine fur sits at its base",
          137851);
        S("Lidstrich", "line along the lid",
          "schmaler Strich entlang des oberen Lids, der über den äußeren Augenwinkel hinaus spitz ausläuft", "narrow line along the upper lid, tapering to a point beyond the outer corner of the eye",
          137852);
        S("Haare über dem Auge", "hairs above the eye",
          "einzelne feine, lange Haare, die über der Braue entspringen und nach oben zur Schläfe hin auffächern", "individual fine long hairs springing from above the brow and fanning upward toward the temple",
          137853);
        S("Lange Schnurrhaare", "long whiskers",
          "lange, feine Schnurrhaare, die beidseits der Schnauze weit nach außen auffächern", "long fine whiskers fanning far outward from both sides of the muzzle",
          137854);
        S("Stirnschmuck", "forehead ornament",
          "rautenförmiger Schmuckstein in gefasstem Rahmen, mittig auf der Stirn über den Brauen", "lozenge-shaped gem in a mounted frame, centred on the forehead above the brows",
          137855);
        S("Sichel unter dem Auge", "crescent under the eye",
          "schmale Sichel, die dem Unterlid folgt und zum äußeren Augenwinkel hin ansteigt", "narrow crescent following the lower lid and rising toward the outer corner of the eye",
          137856);
        S("Punktgruppe an der Schläfe", "cluster of spots on the temple",
          "drei rundliche, oben eingekerbte Flecken im Dreieck an der Schläfe über dem Auge, darunter ein kleinerer vierter", "three rounded spots notched at the top, set in a triangle on the temple above the eye, with a smaller fourth one below",
          137857);

        // 27_Hrothgar_The_Lost_female_face2
        S("Ohr mit hohem Büschel", "ear with a tall tuft",
          "Ohr, das tief in der Mähne sitzt und dessen Oberkante ein hoher, dichter Saum spitzer Haare krönt", "ear set deep in the mane, its upper edge crowned by a tall dense fringe of pointed hairs",
          137861);
        S("Strich unter dem Auge", "line under the eye",
          "kräftiger, gebogener Strich dicht unter dem Unterlid, der vom inneren Augenwinkel nach außen zieht und dort ansteigt", "bold curved line close beneath the lower lid, running from the inner corner outward and rising at the outer end",
          137862);
        S("Lidstrich", "line along the lid",
          "Strich entlang des oberen Lids, der über den äußeren Augenwinkel hinaus in eine feine Spitze ausgezogen ist", "line along the upper lid drawn out beyond the outer corner of the eye into a fine point",
          137863);
        S("Lange Wimpern", "long lashes",
          "lange, dichte Wimpern am oberen Lid, die sich zum äußeren Augenwinkel hin auffächern, dazu Wimpern am Unterlid", "long dense lashes on the upper lid fanning out toward the outer corner of the eye, with lashes on the lower lid too",
          137864);
        S("Buschige Braue", "bushy brow",
          "kräftige, buschige Braue aus längerem Fell, die sich über dem Auge wölbt", "strong bushy brow of longer fur arching above the eye",
          137865);
        S("Breiter Bogen unter dem Auge", "broad arc under the eye",
          "breiter, geschwungener Bogen unterhalb und außerhalb des Auges, in der Mitte am dicksten und an beiden Enden spitz", "broad sweeping arc below and outside the eye, thickest in the middle and pointed at both ends",
          137866);
        S("Zackenstriche über dem Auge", "jagged strokes above the eye",
          "mehrere spitz zulaufende Zackenstriche über dem Auge, ein langer von der Schläfe zur inneren Braue und zwei kürzere daneben", "several sharply tapering jagged strokes above the eye, one long one from the temple to the inner brow and two shorter ones beside it",
          137867);

        // 27_Hrothgar_The_Lost_female_face3
        S("Schmales langes Ohr", "narrow long ear",
          "langes, schmales Ohr, das sich zu einer feinen, leicht gebogenen Spitze verjüngt; am Ohrgrund steht ein Fellkranz", "long narrow ear tapering to a fine, slightly curved point; a ruff of fur sits at its base",
          137871);
        S("Haare am Ohr", "hairs at the ear",
          "mehrere lange, feine Haare, die am Ohrgrund entspringen und über den Ohrrand hinaus auffächern", "several long fine hairs springing from the base of the ear and fanning out beyond its rim",
          137872);
        S("Wimpernfächer", "fan of lashes",
          "ein Fächer langer Wimpern am äußeren Augenwinkel, der nach außen und unten absteht", "a fan of long lashes at the outer corner of the eye, standing out to the side and downward",
          137873);
        S("Strich am Unterlid", "line on the lower lid",
          "gebogener Strich, der dem Unterlid folgt und zum äußeren Augenwinkel hin ausläuft", "curved line following the lower lid and running out toward the outer corner of the eye",
          137874);
        S("Lange Schnurrhaare", "long whiskers",
          "lange, gedrehte Schnurrhaare, die beidseits der Schnauze weit über das Gesicht hinausreichen", "long twisted whiskers reaching far beyond the face on both sides of the muzzle",
          137875);
        S("Drei Spitzen über der Braue", "three spikes above the brow",
          "drei nach oben spitz zulaufende Zacken in einer Reihe über der Braue, zur Schläfe hin größer werdend", "three upward-tapering spikes in a row above the brow, growing larger toward the temple",
          137876);
        S("Zwei Keile am Augenwinkel", "two wedges at the corner of the eye",
          "zwei kantige Keile jenseits des äußeren Augenwinkels, schräg übereinander gesetzt", "two angular wedges beyond the outer corner of the eye, set diagonally one above the other",
          137877);

        // 27_Hrothgar_The_Lost_female_face4
        S("Ohr mit Haarsaum", "ear with a fringe of hair",
          "breites, gerundetes Ohr, dessen Oberkante ein langer Saum spitz abstehender Haare begleitet", "broad rounded ear whose upper edge is accompanied by a long fringe of sharply protruding hairs",
          137881);
        S("Ohrschmuck", "ear ornament",
          "rautenförmiges Schmuckstück mit gestuftem Rahmen am Ohrgrund, darunter eine kleine Kugel", "lozenge-shaped ornament with a stepped frame at the base of the ear, with a small bead below it",
          137882);
        S("Umrandetes Auge", "outlined eye",
          "schmale Umrandung, die Ober- und Unterlid rings um das Auge nachzeichnet", "narrow outline tracing the upper and lower lid all the way around the eye",
          137883);
        S("Lange Wimpern", "long lashes",
          "lange, dichte Wimpern, die das obere Lid säumen und sich am äußeren Augenwinkel auffächern", "long dense lashes lining the upper lid and fanning out at the outer corner of the eye",
          137884);
        S("Buschige Braue", "bushy brow",
          "kurze, buschige Braue aus längerem Fell, die sich schräg über dem Auge wölbt", "short bushy brow of longer fur arching diagonally above the eye",
          137885);
        S("Großes Gesichtsmuster", "large face pattern",
          "großflächiges Muster über die halbe Gesichtsseite, von Schläfe und Braue über die Wange bis zum Kiefer, aus ineinandergreifenden Schwüngen und Reihen von Dreiecken", "large pattern across half the side of the face, from temple and brow over the cheek to the jaw, made of interlocking curves and rows of triangles",
          137886);
        S("Stirnmuster", "forehead pattern",
          "symmetrisches, kronenartiges Zeichen mittig auf der Stirn zwischen den Brauen, von dem ein schmaler Steg über den Nasenrücken hinabläuft", "symmetrical crown-like mark centred on the forehead between the brows, with a narrow stem running down the bridge of the nose",
          137887);

        // ---- feat-hyur.cs ----
        // 00_Hyur_Midlander_male_face1
        S("Kieferstoppeln", "jawline stubble",
          "schmaler Streifen kurzer Stoppeln entlang der Kieferkante, am Kinn am dichtesten", "narrow strip of short stubble along the jawline, densest at the chin",
          131111);
        S("dünner Schnurrbart", "thin moustache",
          "spärlicher Schnurrbart über der ganzen Oberlippe, der etwas über die Mundwinkel hinausreicht", "sparse moustache across the whole upper lip, reaching a little past the corners of the mouth",
          131112);
        S("senkrechte Narbe am Auge", "vertical scar past the eye",
          "lange, fast senkrechte Narbe von der Stirn über die Augenbraue und am Auge vorbei bis auf die Wange", "long, almost vertical scar from the forehead across the eyebrow and past the eye onto the cheek",
          131113);
        S("Narbe quer über die Wange", "scar across the cheek",
          "kräftige Narbe, die vom Nasenrücken schräg abwärts über die Wange bis zum Kieferwinkel zieht", "deep scar running diagonally from the bridge of the nose across the cheek down to the jaw angle",
          131114);
        S("feine Narbe, Wangenmitte", "fine scar, mid-cheek",
          "feine, leicht geschwungene Narbe quer über die Wangenmitte, zum Wangenknochen hin ansteigend", "fine, slightly curved scar across the middle of the cheek, rising toward the cheekbone",
          131115);
        S("Stammes-Tattoo", "tribal tattoo",
          "spitzes Stammes-Muster über Braue, Schläfe und Wange einer Gesichtshälfte, das nach unten in eine lange Spitze ausläuft", "pointed tribal pattern over brow, temple and cheek on one side of the face, tapering downward into a long point",
          131116);
        S("Stammes-Tattoo", "tribal tattoo",
          "spitzes Stammes-Muster über Braue, Schläfe und Wange einer Gesichtshälfte, das nach unten in eine lange Spitze ausläuft", "pointed tribal pattern over brow, temple and cheek on one side of the face, tapering downward into a long point",
          131117);

        // 00_Hyur_Midlander_male_face2
        S("Kinnfleck", "chin patch",
          "kurzer Bartfleck auf dem Kinn unter der Unterlippe, der seitlich in dünne Stoppeln ausläuft", "short patch of hair on the chin below the lower lip, thinning into stubble toward the sides",
          131121);
        S("senkrechte Narbe am Auge", "vertical scar past the eye",
          "lange Narbe, die von der Stirn über die Augenbraue und am Auge vorbei bis zum Kiefer zieht", "long scar running from the forehead across the eyebrow and past the eye down to the jaw",
          131122);
        S("Narbe durch die Braue", "scar through the eyebrow",
          "kurze schräge Narbe, die von der Stirn herab die Augenbraue durchkreuzt", "short slanting scar coming down from the forehead and cutting across the eyebrow",
          131123);
        S("Narbe quer über die Wange", "scar across the cheek",
          "Narbe, die neben der Nase beginnt und schräg abwärts über die Wange zum Kiefer läuft", "scar starting beside the nose and running diagonally down across the cheek to the jaw",
          131124);
        S("Narbe am Nasenrücken", "scar on the nose bridge",
          "feine Narbe, die schräg über den Nasenrücken zieht", "fine scar running diagonally across the bridge of the nose",
          131125);
        S("Flügelmuster am Auge", "wing pattern at the eye",
          "geschwungenes Flügelmuster mit gezackter Unterkante, das vom äußeren Augenwinkel über die Schläfe einer Gesichtshälfte nach hinten zieht", "swept wing shape with a scalloped lower edge, running back from the outer corner of the eye across one temple",
          131126);
        S("Flügelmuster am Auge", "wing pattern at the eye",
          "geschwungenes Flügelmuster mit gezackter Unterkante, das vom äußeren Augenwinkel über die Schläfe einer Gesichtshälfte nach hinten zieht", "swept wing shape with a scalloped lower edge, running back from the outer corner of the eye across one temple",
          131127);

        // 00_Hyur_Midlander_male_face3
        S("Kinnkranzbart", "chin-strap beard",
          "voller Bart, der von den Koteletten der Kieferlinie folgt und das Kinn umschließt; Oberlippe und Wangen bleiben frei", "full beard following the jawline from the sideburns and closing around the chin; upper lip and cheeks stay bare",
          131131);
        S("Hufeisenbart", "horseshoe moustache",
          "kräftiger Schnurrbart, dessen Enden beidseits an den Mundwinkeln vorbei bis zum Kiefer herabziehen", "heavy moustache whose ends run down past both corners of the mouth to the jaw",
          131132);
        S("Narben auf beiden Wangen", "scars on both sides",
          "mehrere Narben: auf der einen Seite eine Kerbe von der Stirn zur Braue und ein Riss unter dem äußeren Augenwinkel, auf der anderen eine lange Narbe von der Schläfe bis zum Mundwinkel", "several scars: on one side a notch from the forehead down to the brow plus a gash below the outer corner of the eye, on the other a long scar from the temple to the corner of the mouth",
          131133);
        S("Narbe am Nasenrücken", "scar on the nose bridge",
          "waagerechte Narbe quer über den Nasenrücken zwischen den Augen", "horizontal scar across the bridge of the nose between the eyes",
          131134);
        S("lange Wangennarbe", "long cheek scar",
          "lange, leicht geschwungene Narbe, die von der Schläfe über die Wange bis zum Mundwinkel zieht", "long, gently curved scar running from the temple across the cheek to the corner of the mouth",
          131135);
        S("Haken am Augenwinkel", "hook at the eye corner",
          "sichelförmiger Haken an der Schläfe, der sich um den äußeren Augenwinkel legt und unter dem Auge in eine Spitze ausläuft", "crescent hook on the temple that curls around the outer corner of the eye and tapers to a point below it",
          131136);
        S("Pfeil über der Braue", "arrow above the brow",
          "eckiges, pfeilartiges Muster über der Augenbraue an der Schläfe, mit zwei Zacken nach vorn", "angular arrow-like mark above the eyebrow on the temple, with two prongs pointing forward",
          131137);

        // 00_Hyur_Midlander_male_face4
        S("breite Koteletten", "broad sideburns",
          "breite Koteletten, die vom Ohr über die Wange bis auf den Unterkiefer reichen; Kinn und Oberlippe bleiben frei", "broad sideburns reaching from the ear across the cheek onto the lower jaw; chin and upper lip stay bare",
          131141);
        S("Schnurrbart mit Kinnstreifen", "moustache with chin strip",
          "schmaler Schnurrbart über der Oberlippe und ein schmaler Bartstreifen von der Unterlippe über das Kinn", "narrow moustache on the upper lip plus a narrow strip of hair from the lower lip down over the chin",
          131142);
        S("senkrechte Narbe am Auge", "vertical scar past the eye",
          "lange, leicht schräge Narbe von der Stirn über Braue und Auge bis weit auf die Wange", "long, slightly slanted scar from the forehead across brow and eye and far down the cheek",
          131143);
        S("Narbe von der Schläfe zum Mund", "scar from temple to mouth",
          "lange, gerade Narbe, die von der Schläfe schräg über die Wange bis in die Nähe des Mundwinkels zieht", "long, straight scar running diagonally from the temple across the cheek to near the corner of the mouth",
          131144);
        S("flache Narbe unter den Augen", "flat scar under the eyes",
          "flache Narbe quer über den Nasenrücken, die sich unter beiden Augen bis auf die Wangen fortsetzt", "shallow scar across the bridge of the nose, continuing under both eyes onto the cheeks",
          131145);
        S("Dornenmuster", "thorn pattern",
          "dorniges Stammes-Muster auf einer Gesichtshälfte: ein gezackter Bogen über der Braue und ein zweiter, der unter dem Auge über den Wangenknochen in eine lange Spitze ausläuft", "barbed tribal pattern on one side of the face: a jagged arc above the brow and a second one below the eye that tapers across the cheekbone into a long point",
          131146);
        S("Dornenmuster", "thorn pattern",
          "dorniges Stammes-Muster auf einer Gesichtshälfte: ein gezackter Bogen über der Braue und ein zweiter, der unter dem Auge über den Wangenknochen in eine lange Spitze ausläuft", "barbed tribal pattern on one side of the face: a jagged arc above the brow and a second one below the eye that tapers across the cheekbone into a long point",
          131147);

        // 00_Hyur_Midlander_male_face5
        S("Stoppeln am Kiefer", "stubble on the jaw",
          "feine Stoppeln entlang der Kieferkante und unter dem Kinn; die Oberlippe bleibt frei", "fine stubble along the jawline and under the chin; the upper lip stays bare",
          131151);
        S("spärlicher Schnurrbart", "sparse moustache",
          "dünner, spärlicher Schnurrbart über der Oberlippe", "thin, sparse moustache on the upper lip",
          131152);
        S("gekreuzte Narben, Wange", "crossed scars, cheek",
          "zwei Narben auf der Wange, die sich nahe dem Kieferwinkel kreuzen: eine lange von der Nase abwärts, eine kürzere fast waagerecht am Kiefer entlang", "two scars crossing each other near the jaw angle: a long one running down from beside the nose and a shorter, almost horizontal one along the jaw",
          131153);
        S("Narbe zwischen den Brauen", "scar between the brows",
          "lange Narbe, die schräg von der Stirn über das innere Ende der Augenbraue bis neben den Nasenrücken zieht", "long scar running diagonally from the forehead across the inner end of the eyebrow to beside the bridge of the nose",
          131154);
        S("lange Wangennarbe", "long cheek scar",
          "lange Narbe, die von der Nasenseite schräg abwärts über die ganze Wange bis zum Kiefer vor dem Ohr zieht", "long scar running diagonally from beside the nose across the whole cheek to the jaw in front of the ear",
          131155);
        S("schmales Stammes-Tattoo", "narrow tribal tattoo",
          "schmales, senkrechtes Stammes-Muster, das von der Schläfe über eine Wange bis zum Kiefer läuft und in der Mitte einen Haken trägt", "narrow, upright tribal pattern running from the temple down one cheek to the jaw, with a hook at its middle",
          131156);
        S("schmales Stammes-Tattoo", "narrow tribal tattoo",
          "schmales, senkrechtes Stammes-Muster, das von der Schläfe über eine Wange bis zum Kiefer läuft und in der Mitte einen Haken trägt", "narrow, upright tribal pattern running from the temple down one cheek to the jaw, with a hook at its middle",
          131157);

        // 00_Hyur_Midlander_male_face6
        S("Kieferstoppeln", "jawline stubble",
          "schmales Band kurzer Stoppeln entlang der Kieferkante und am Kinn; Wangen und Oberlippe bleiben frei", "narrow band of short stubble along the jawline and on the chin; cheeks and upper lip stay bare",
          131161);
        S("dünner Schnurrbart", "thin moustache",
          "schmaler, spärlicher Schnurrbart über der Oberlippe", "narrow, sparse moustache on the upper lip",
          131162);
        S("senkrechte Narbe am Auge", "vertical scar past the eye",
          "lange, senkrechte Narbe von der Stirn über die Augenbraue und am Auge vorbei auf die Wange", "long, vertical scar from the forehead across the eyebrow and past the eye onto the cheek",
          131163);
        S("Narbe am Mundwinkel", "scar at the corner of the mouth",
          "senkrechte Narbe, die von der Wange herab durch den Mundwinkel bis auf das Kinn läuft und an der Lippe versetzt ist", "vertical scar running down the cheek through the corner of the mouth onto the chin, offset where it meets the lip",
          131164);
        S("kurze Narbe am Nasenrücken", "short scar on the nose bridge",
          "kurze, waagerechte Narbe quer über den Nasenrücken zwischen den Augen", "short, horizontal scar across the bridge of the nose between the eyes",
          131165);
        S("Hakenmuster über der Braue", "hook pattern above the brow",
          "eckiges Hakenmuster über einer Augenbraue, mit langer waagerechter Spitze und einem Haken, der bis auf das äußere Brauenende reicht", "angular hook shape above one eyebrow, with a long horizontal spike and a hook that dips onto the outer end of the brow",
          131166);
        S("Hakenmuster über der Braue", "hook pattern above the brow",
          "eckiges Hakenmuster über einer Augenbraue, mit langer waagerechter Spitze und einem Haken, der bis auf das äußere Brauenende reicht", "angular hook shape above one eyebrow, with a long horizontal spike and a hook that dips onto the outer end of the brow",
          131167);

        // 00_Hyur_Midlander_male_face7
        S("dichter Dreitagebart", "heavy stubble",
          "dichte, kurze Stoppeln über Wangen, Kiefer, Kinn und Oberlippe", "dense, short stubble covering cheeks, jaw, chin and upper lip",
          131171);
        S("Stoppelring um den Mund", "stubble ring around the mouth",
          "Stoppeln, die einen Ring um den Mund bilden, über Oberlippe, Kinn und Kieferpartie, während die oberen Wangen frei bleiben", "stubble forming a ring around the mouth across upper lip, chin and jaw, while the upper cheeks stay bare",
          131172);
        S("Narbe, eine Wange", "scar, one cheek",
          "geknickte Narbe, die vom Nasenflügel schräg abwärts über eine Wange zum Kiefer zieht", "kinked scar running from beside the nostril diagonally down across one cheek to the jaw",
          131173);
        S("Narbe an der Braue", "scar at the brow",
          "lange Narbe, die schräg von der Stirn über das innere Ende der Augenbraue bis neben den Nasenrücken läuft", "long scar running diagonally from the forehead across the inner end of the eyebrow to beside the bridge of the nose",
          131174);
        S("Narbe, eine Wange", "scar, one cheek",
          "geknickte Narbe, die vom Nasenflügel schräg abwärts über eine Wange zum Kiefer zieht", "kinked scar running from beside the nostril diagonally down across one cheek to the jaw",
          131175);
        S("schmales Stammes-Tattoo", "narrow tribal tattoo",
          "schmales, senkrechtes Stammes-Muster von der Schläfe über eine Wange bis zum Kiefer, in der Mitte mit einem Haken", "narrow, upright tribal pattern from the temple down one cheek to the jaw, with a hook at its middle",
          131176);
        S("schmales Stammes-Tattoo", "narrow tribal tattoo",
          "schmales, senkrechtes Stammes-Muster von der Schläfe über eine Wange bis zum Kiefer, in der Mitte mit einem Haken", "narrow, upright tribal pattern from the temple down one cheek to the jaw, with a hook at its middle",
          131177);

        // 01_Hyur_Midlander_female_face1
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131311);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131312);
        S("Narbe am Nasenrücken", "scar on the nose bridge",
          "kurze, gezackte Narbe quer über den Nasenrücken zwischen den Augen", "short, jagged scar across the bridge of the nose between the eyes",
          131313);
        S("Leberfleck unter dem Auge", "mole under the eye",
          "einzelner Leberfleck auf der Wange dicht unter dem Auge", "single mole on the cheek just below the eye",
          131314);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131315);
        S("Dreizack auf der Wange", "trident on the cheek",
          "dreizackiges Muster auf einem Wangenknochen unter dem äußeren Augenwinkel, mit zwei kurzen Spitzen nach oben und einer langen Spitze über die Wange abwärts", "three-pronged mark on one cheekbone below the outer corner of the eye, with two short points upward and one long point running down the cheek",
          131316);
        S("Dreizack auf der Wange", "trident on the cheek",
          "dreizackiges Muster auf einem Wangenknochen unter dem äußeren Augenwinkel, mit zwei kurzen Spitzen nach oben und einer langen Spitze über die Wange abwärts", "three-pronged mark on one cheekbone below the outer corner of the eye, with two short points upward and one long point running down the cheek",
          131317);

        // 01_Hyur_Midlander_female_face2
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131321);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131322);
        S("Narbe über der Braue", "scar above the eyebrow",
          "kurze, gezackte Narbe schräg über dem äußeren Ende der Augenbraue", "short, jagged scar set at an angle above the outer end of the eyebrow",
          131323);
        S("Leberfleck unter dem Auge", "mole under the eye",
          "einzelner Leberfleck auf der Wange dicht unter dem Auge", "single mole on the cheek just below the eye",
          131324);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131325);
        S("Doppelbogen am Auge", "double arc at the eye",
          "zwei geschwungene, spitz auslaufende Striche, die wie eine Klammer um den äußeren Augenwinkel einer Gesichtshälfte liegen und über die Wange abwärts zeigen", "two curved, tapering strokes bracketing the outer corner of one eye and pointing down across the cheek",
          131326);
        S("Doppelbogen am Auge", "double arc at the eye",
          "zwei geschwungene, spitz auslaufende Striche, die wie eine Klammer um den äußeren Augenwinkel einer Gesichtshälfte liegen und über die Wange abwärts zeigen", "two curved, tapering strokes bracketing the outer corner of one eye and pointing down across the cheek",
          131327);

        // 01_Hyur_Midlander_female_face3
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131331);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131332);
        S("Narbe auf der Stirn", "scar on the forehead",
          "gezackte Narbe, die schräg vom Haaransatz über die Stirn bis zur Augenbraue reicht", "jagged scar running at an angle from the hairline across the forehead down to the eyebrow",
          131333);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck neben dem Mundwinkel, etwas unterhalb der Lippenlinie", "single mole beside the corner of the mouth, a little below the lip line",
          131334);
        S("Leberfleck, Wangenmitte", "mole, mid-cheek",
          "einzelner Leberfleck mitten auf der Wange, etwa auf halbem Weg zwischen Auge und Mund", "single mole in the middle of the cheek, about halfway between eye and mouth",
          131335);
        S("Hornmuster", "horn pattern",
          "geschwungenes Hornmuster mit zwei Zacken auf einem Wangenknochen unterhalb des äußeren Augenwinkels", "curved horn shape with two prongs on one cheekbone below the outer corner of the eye",
          131336);
        S("Hornmuster", "horn pattern",
          "geschwungenes Hornmuster mit zwei Zacken auf einem Wangenknochen unterhalb des äußeren Augenwinkels", "curved horn shape with two prongs on one cheekbone below the outer corner of the eye",
          131337);

        // 01_Hyur_Midlander_female_face4
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131341);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131342);
        S("Narbe über den Nasenrücken", "scar over the nose bridge",
          "lange Narbe, die schräg von der Stirn zwischen den Brauen hindurch über den Nasenrücken bis auf die Wange zieht", "long scar running at an angle from the forehead between the brows and over the bridge of the nose onto the cheek",
          131343);
        S("Leberfleck, Wangenmitte", "mole, mid-cheek",
          "einzelner Leberfleck mitten auf der Wange, deutlich unterhalb des Auges", "single mole in the middle of the cheek, well below the eye",
          131344);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131345);
        S("Klingenmuster", "blade pattern",
          "gebogenes Klingenmuster mit Haken auf einer Wange, das unter dem äußeren Augenwinkel beginnt und in einer langen Spitze zum Kiefer ausläuft", "curved blade shape with a hook on one cheek, starting below the outer corner of the eye and tapering into a long point toward the jaw",
          131346);
        S("Klingenmuster", "blade pattern",
          "gebogenes Klingenmuster mit Haken auf einer Wange, das unter dem äußeren Augenwinkel beginnt und in einer langen Spitze zum Kiefer ausläuft", "curved blade shape with a hook on one cheek, starting below the outer corner of the eye and tapering into a long point toward the jaw",
          131347);

        // 01_Hyur_Midlander_female_face5
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131351);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131352);
        S("Narbe am Nasenrücken", "scar on the nose bridge",
          "kleine, wellige Narbe quer über den Nasenrücken zwischen den Augen", "small, wavy scar across the bridge of the nose between the eyes",
          131353);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131354);
        S("Leberfleck unter dem Auge", "mole under the eye",
          "einzelner Leberfleck auf der oberen Wange zwischen Nase und Auge", "single mole high on the cheek between nose and eye",
          131355);
        S("Sichelmuster", "crescent pattern",
          "sichelförmiges Muster mit Widerhaken auf einer Wange, das unter dem äußeren Augenwinkel sitzt und in einer langen Spitze zum Kiefer ausläuft", "crescent shape with a barb on one cheek, sitting below the outer corner of the eye and tapering into a long point toward the jaw",
          131356);
        S("Sichelmuster", "crescent pattern",
          "sichelförmiges Muster mit Widerhaken auf einer Wange, das unter dem äußeren Augenwinkel sitzt und in einer langen Spitze zum Kiefer ausläuft", "crescent shape with a barb on one cheek, sitting below the outer corner of the eye and tapering into a long point toward the jaw",
          131357);

        // 02_Hyur_Highlander_male_face1
        S("Spitzbart am Kinn", "pointed chin beard",
          "langer, spitz zulaufender Bartzipfel, der vom Kinn herabhängt; Wangen und Oberlippe bleiben frei", "long, tapering tuft of beard hanging down from the chin; cheeks and upper lip stay bare",
          131611);
        S("hängender Schnurrbart", "drooping moustache",
          "kräftiger Schnurrbart, dessen lange Enden beiderseits an den Mundwinkeln vorbei bis zum Kiefer hängen", "heavy moustache whose long ends hang down past both corners of the mouth to the jaw",
          131612);
        S("genähte Narbe am Auge", "stitched scar at the eye",
          "Narbe mit Quernähten, die von der Stirn über die Augenbraue und unterhalb des Auges weiter über die Wange verläuft", "scar with cross-stitches running from the forehead across the eyebrow and continuing below the eye over the cheek",
          131613);
        S("Narbe auf dem Kinn", "scar on the chin",
          "gezackte Narbe, die schräg über das Kinn unter der Unterlippe verläuft", "jagged scar running at an angle across the chin below the lower lip",
          131614);
        S("Narbe am Nasenrücken", "scar on the nose bridge",
          "waagerechte Narbe quer über den Nasenrücken zwischen den Augen", "horizontal scar across the bridge of the nose between the eyes",
          131615);
        S("breite Bänder", "broad bands",
          "breite, geschwungene Bänder über Wangenknochen und Wange einer Gesichtshälfte, dazu ein schmaler Haken über der Augenbraue", "broad, sweeping bands across the cheekbone and cheek on one side of the face, plus a slim hook above the eyebrow",
          131616);
        S("breite Bänder", "broad bands",
          "breite, geschwungene Bänder über Wangenknochen und Wange einer Gesichtshälfte, dazu ein schmaler Haken über der Augenbraue", "broad, sweeping bands across the cheekbone and cheek on one side of the face, plus a slim hook above the eyebrow",
          131617);

        // 02_Hyur_Highlander_male_face2
        S("Kinnbart", "chin beard",
          "kurzer, breiter Bart auf dem Kinn unter der Unterlippe; Oberlippe und Wangen bleiben frei", "short, broad beard on the chin below the lower lip; upper lip and cheeks stay bare",
          131621);
        S("Backenbart", "mutton chops",
          "buschiger Backenbart, der von den Ohren über Wangen und Kiefer bis zu den Mundwinkeln reicht; Kinn und Oberlippe bleiben frei", "bushy side whiskers reaching from the ears across cheeks and jaw to the corners of the mouth; chin and upper lip stay bare",
          131622);
        S("lange Narbe über die Braue", "long scar over the brow",
          "lange Narbe, die schräg von der Schläfe über die Augenbraue bis auf die Wange neben der Nase zieht", "long scar running at an angle from the temple across the eyebrow onto the cheek beside the nose",
          131623);
        S("Narbe quer über die Wange", "scar across the cheek",
          "Narbe, die vom Auge schräg abwärts über die Wange bis zum Kieferwinkel zieht", "scar running diagonally from the eye down across the cheek to the jaw angle",
          131624);
        S("senkrechte Narbe am Auge", "vertical scar past the eye",
          "senkrechte Narbe von der Stirn über die Augenbraue und am Auge vorbei auf die Wange", "vertical scar from the forehead across the eyebrow and past the eye onto the cheek",
          131625);
        S("Muster auf der Schläfe", "temple pattern",
          "großflächiges Muster auf einer Schläfe: breite, spitz auslaufende Schwünge über der Braue und ein Bogen, der vor dem Ohr herab bis unter den äußeren Augenwinkel zieht", "large pattern on one temple: broad, tapering sweeps above the brow and an arc that runs down in front of the ear to below the outer corner of the eye",
          131626);
        S("Muster auf der Schläfe", "temple pattern",
          "großflächiges Muster auf einer Schläfe: breite, spitz auslaufende Schwünge über der Braue und ein Bogen, der vor dem Ohr herab bis unter den äußeren Augenwinkel zieht", "large pattern on one temple: broad, tapering sweeps above the brow and an arc that runs down in front of the ear to below the outer corner of the eye",
          131627);

        // 02_Hyur_Highlander_male_face3
        S("zottiger Vollbart", "shaggy full beard",
          "zottiger Vollbart über Wangen, Kiefer und Kinn, der bis auf den Hals herabhängt; die Oberlippe bleibt frei", "shaggy full beard over cheeks, jaw and chin, hanging down onto the neck; the upper lip stays bare",
          131631);
        S("buschiger Walrossbart", "bushy walrus moustache",
          "sehr buschiger Schnurrbart, der die Oberlippe verdeckt und breit bis über die Mundwinkel hinausreicht", "very bushy moustache covering the upper lip and spreading wide past the corners of the mouth",
          131632);
        S("breite Narbe am Nasenrücken", "broad scar on the nose bridge",
          "breite, waagerechte Narbe quer über den Nasenrücken, die auf beiden Seiten etwas auf die Wangen reicht", "broad, horizontal scar across the bridge of the nose, reaching a little onto the cheeks on both sides",
          131633);
        S("Narbe zur Braue", "scar down to the brow",
          "gerade Narbe, die von der Stirn schräg abwärts bis auf die Augenbraue führt", "straight scar running at an angle down from the forehead onto the eyebrow",
          131634);
        S("breite Wangennarbe", "broad cheek scar",
          "breite Narbe, die von der Nasenseite schräg abwärts über den Wangenknochen zieht", "broad scar running diagonally from beside the nose down across the cheekbone",
          131635);
        S("Keilmuster", "wedge pattern",
          "großflächiges Keilmuster auf einer Schläfe, von dem ein breites Band senkrecht über die Wange bis zum Kiefer läuft", "large wedge shape on one temple, from which a broad band runs straight down the cheek to the jaw",
          131636);
        S("Keilmuster", "wedge pattern",
          "großflächiges Keilmuster auf einer Schläfe, von dem ein breites Band senkrecht über die Wange bis zum Kiefer läuft", "large wedge shape on one temple, from which a broad band runs straight down the cheek to the jaw",
          131637);

        // 02_Hyur_Highlander_male_face4
        S("kurzer Vollbart", "short full beard",
          "kurzer Bart über Wangen, Kiefer und Kinn; die Oberlippe bleibt unbehaart", "short beard over cheeks, jaw and chin; the upper lip is left bare",
          131641);
        S("buschiger Schnurrbart", "bushy moustache",
          "buschiger Schnurrbart, der die Oberlippe bedeckt und knapp über die Mundwinkel hinausreicht", "bushy moustache covering the upper lip and reaching just past the corners of the mouth",
          131642);
        S("Narbe am Mundwinkel", "scar at the corner of the mouth",
          "fast senkrechte Narbe, die vom Wangenknochen herab am Mundwinkel vorbei zum Kiefer zieht", "almost vertical scar running down from the cheekbone past the corner of the mouth to the jaw",
          131643);
        S("senkrechte Narbe über dem Auge", "vertical scar over the eye",
          "senkrechte Narbe, die von der Stirn über Augenbraue und Lid bis unter das Auge reicht", "vertical scar reaching from the forehead across eyebrow and eyelid to below the eye",
          131644);
        S("kurze Wangennarbe", "short cheek scar",
          "kurze, schräge Narbe auf der Wange unterhalb des Auges neben der Nase", "short, slanted scar on the cheek below the eye beside the nose",
          131645);
        S("breite Bänder", "broad bands",
          "mehrere breite Bänder auf einer Gesichtshälfte: ein Haken über der Braue, ein waagerechtes Band über dem Wangenknochen, ein Winkel auf der unteren Wange und ein Zickzack am Hals", "several broad bands on one side of the face: a hook above the brow, a horizontal band across the cheekbone, a chevron on the lower cheek and a zigzag on the neck",
          131646);
        S("breite Bänder", "broad bands",
          "mehrere breite Bänder auf einer Gesichtshälfte: ein Haken über der Braue, ein waagerechtes Band über dem Wangenknochen, ein Winkel auf der unteren Wange und ein Zickzack am Hals", "several broad bands on one side of the face: a hook above the brow, a horizontal band across the cheekbone, a chevron on the lower cheek and a zigzag on the neck",
          131647);

        // 03_Hyur_Highlander_female_face1
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131811);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131812);
        S("gezackte Narbe am Auge", "jagged scar at the eye",
          "gezackte, blitzförmige Narbe, die von der Stirn über die Augenbraue und am Auge vorbei auf die Wange läuft", "jagged, lightning-shaped scar running from the forehead across the eyebrow and past the eye onto the cheek",
          131813);
        S("Leberfleck, Wangenmitte", "mole, mid-cheek",
          "einzelner Leberfleck auf der Wange, etwa auf Höhe des Mundes, deutlich hinter dem Mundwinkel", "single mole on the cheek at about mouth height, well back from the corner of the mouth",
          131814);
        S("Leberfleck unter dem Auge", "mole under the eye",
          "einzelner Leberfleck auf der oberen Wange unter dem Auge", "single mole high on the cheek below the eye",
          131815);
        S("großes Wangenmuster", "large cheek pattern",
          "großes, gebogenes Muster mit Widerhaken über die ganze Wange, das vom Auge bis zum Kiefer reicht und sich zur Nase hin einrollt", "large, curved barbed pattern across the whole cheek, reaching from the eye to the jaw and curling inward toward the nose",
          131816);
        S("großes Wangenmuster", "large cheek pattern",
          "großes, gebogenes Muster mit Widerhaken über die ganze Wange, das vom Auge bis zum Kiefer reicht und sich zur Nase hin einrollt", "large, curved barbed pattern across the whole cheek, reaching from the eye to the jaw and curling inward toward the nose",
          131817);

        // 03_Hyur_Highlander_female_face2
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131821);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131822);
        S("lange Wangennarbe", "long cheek scar",
          "lange, dünne Narbe, die vom äußeren Augenwinkel schräg über die ganze Wange bis zum Mundwinkel zieht", "long, thin scar running diagonally from the outer corner of the eye across the whole cheek to the corner of the mouth",
          131823);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131824);
        S("Leberfleck unter dem Auge", "mole under the eye",
          "einzelner Leberfleck auf der oberen Wange unter dem Auge", "single mole high on the cheek below the eye",
          131825);
        S("Band über dem Nasenrücken", "band across the nose bridge",
          "waagerechtes, an beiden Enden spitz auslaufendes Band quer über den Nasenrücken, das unter beiden Augen auf die Wangen reicht", "horizontal band tapering to a point at both ends, running across the bridge of the nose and onto both cheeks under the eyes",
          131826);
        S("gezackte Striche", "jagged strokes",
          "mehrere einzelne, geschwungene und gezackte Striche, die untereinander über Schläfe und eine Wange verteilt sind", "several separate curved, jagged strokes spread one below the other over the temple and one cheek",
          131827);

        // 03_Hyur_Highlander_female_face3
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131831);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131832);
        S("waagerechtes Pflaster", "horizontal patch",
          "rechteckiges Pflaster, waagerecht auf die Wange unter dem äußeren Augenwinkel geklebt", "rectangular plaster patch stuck horizontally on the cheek below the outer corner of the eye",
          131833);
        S("schräges Pflaster", "tilted patch",
          "rechteckiges Pflaster, schräg auf die Wange geklebt, mit der unteren Kante zum Kiefer geneigt", "rectangular plaster patch stuck on the cheek at an angle, its lower edge tilted toward the jaw",
          131834);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck neben dem Mundwinkel", "single mole beside the corner of the mouth",
          131835);
        S("Muster über der Nase", "pattern across the nose",
          "mittiges Muster: eine kleine Sichel auf der Stirn zwischen den Brauen und ein breites, an beiden Enden spitzes Band quer über den Nasenrücken auf beide Wangen", "centred pattern: a small crescent on the forehead between the brows and a broad band, pointed at both ends, across the bridge of the nose onto both cheeks",
          131836);
        S("Blattmuster", "leaf pattern",
          "großflächiges, geschwungenes Blattmuster über Schläfe, äußere Braue und Wange einer Gesichtshälfte", "large, flowing leaf-shaped pattern over temple, outer brow and cheek on one side of the face",
          131837);

        // 03_Hyur_Highlander_female_face4
        S("Fältchen am Augenwinkel", "lines at the eye corner",
          "feine Fältchen am äußeren Augenwinkel", "fine lines at the outer corner of the eye",
          131841);
        S("Falte unter dem Auge", "crease under the eye",
          "feine Falte, die dicht unter dem Unterlid entlangläuft", "fine crease running just below the lower eyelid",
          131842);
        S("schräge Narbe am Auge", "slanted scar at the eye",
          "schräge Narbe, die über der Braue beginnt, die Augenbraue durchtrennt und unterhalb des Auges auf der Wange weiterläuft", "slanted scar starting above the brow, cutting through the eyebrow and continuing below the eye onto the cheek",
          131843);
        S("Narbe auf der Wange", "scar on the cheek",
          "schräge Narbe mitten auf der Wange hinter dem Mundwinkel, die nach vorn unten zum Kiefer zeigt", "slanted scar in the middle of the cheek behind the corner of the mouth, pointing forward and down toward the jaw",
          131844);
        S("Leberfleck am Mund", "mole by the mouth",
          "einzelner Leberfleck auf der Wange neben dem Mundwinkel", "single mole on the cheek beside the corner of the mouth",
          131845);
        S("Tupfen an der Braue", "dot at the eyebrow",
          "kleiner ovaler Tupfen am äußeren Ende der Augenbraue", "small oval dot at the outer end of the eyebrow",
          131846);
        S("zwei Schwünge", "two sweeps",
          "zwei lange, spitz auslaufende Schwünge über Wangenknochen und Wange einer Gesichtshälfte", "two long, tapering sweeps across the cheekbone and cheek on one side of the face",
          131847);

        // ---- feat-lalafell.cs ----
        // Lalafell - CharaMakeType.FacialFeatureOption, 16 (row, face) blocks, 7 icons each = 112.
        // Slots 1-5 = the 5-entry "Facial Features" menu, slots 6-7 = the 2-entry "Tattoos" menu.
        // Every icon id below is copied from the contact-sheet cell label and re-checked against
        // tools/icons/idx-Facial_Features.tsv. Structural only - no colour words.
        // Read against the matching Face-menu baseline icon (133101-04 / 133301-04 / 133601-04 /
        // 133801-04), because several Lalafell faces carry freckles, rosy cheeks or a shaded eye
        // area in the base texture; only what the baseline does NOT have is described here.
        // The natural Lalafell cheek dimple is present on every entry and is never described.

        // 08_Lalafell_Plainsfolk_male_face1
        S("Zwirbelbart", "curled moustache",
          "schmaler Schnurrbart dicht über der Oberlippe, dessen Enden beidseitig zu kleinen Spitzen nach oben gedreht sind", "a narrow moustache close above the upper lip, its ends twisted up into small points on both sides",
          133111);
        S("Kinnbart, spitz", "pointed chin tuft",
          "kleiner Bartbüschel direkt unter der Unterlippe, der spitz zum Kinn hin ausläuft", "a small tuft of beard directly under the lower lip, tapering to a point toward the chin",
          133112);
        S("Nase abgesetzt", "darkened nose",
          "die ganze Nasenkuppe ist flächig dunkel abgesetzt, die Wangen bleiben frei", "the whole tip of the nose is covered by a solid dark patch, the cheeks left clear",
          133113);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133114);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133115);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133116);
        S("Stirnzeichen", "brow mark",
          "geschwungenes Zeichen aus zwei Haken seitlich auf der Stirn, dicht über einer Braue am Haaransatz", "a curved mark made of two hooks at the side of the forehead, close above one brow at the hairline",
          133117);

        // 08_Lalafell_Plainsfolk_male_face2
        S("Hufeisenbart", "horseshoe moustache",
          "dünner Schnurrbart, dessen Enden beidseitig an den Mundwinkeln vorbei nach unten zum Kiefer laufen", "a thin moustache whose ends run down past both corners of the mouth toward the jaw",
          133121);
        S("Kieferbart", "jawline beard",
          "schmaler Bartstreifen, der dem Kinn- und Kieferrand folgt und beidseitig zu den Mundwinkeln hochzieht, die Oberlippe bleibt frei", "a narrow strip of beard following the chin and jawline and rising to both corners of the mouth, the upper lip left bare",
          133122);
        S("Nase abgesetzt", "darkened nose",
          "die ganze Nasenkuppe ist flächig dunkel abgesetzt, die Wangen bleiben frei", "the whole tip of the nose is covered by a solid dark patch, the cheeks left clear",
          133123);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133124);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133125);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133126);
        S("Stirnzeichen", "brow mark",
          "geschwungenes Zeichen aus zwei Haken seitlich auf der Stirn, dicht über einer Braue am Haaransatz", "a curved mark made of two hooks at the side of the forehead, close above one brow at the hairline",
          133127);

        // 08_Lalafell_Plainsfolk_male_face3
        S("Hängebart", "drooping moustache",
          "voller Schnurrbart, dessen lange Enden weit über den Kiefer hinaus nach unten und außen hängen", "a full moustache whose long ends hang down and outward well past the jaw",
          133131);
        S("Backenbart", "side whiskers",
          "breiter, zottiger Backenbart von den Schläfen über die Wangen bis zum Kiefer, Kinn und Oberlippe bleiben frei", "broad shaggy whiskers from the temples across the cheeks down to the jaw, chin and upper lip left bare",
          133132);
        S("Nase abgesetzt", "darkened nose",
          "die ganze Nasenkuppe ist flächig dunkel abgesetzt, die Wangen bleiben frei", "the whole tip of the nose is covered by a solid dark patch, the cheeks left clear",
          133133);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133134);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133135);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133136);
        S("Stirnzeichen", "brow mark",
          "geschwungenes Zeichen aus zwei Haken seitlich auf der Stirn, dicht über einer Braue am Haaransatz", "a curved mark made of two hooks at the side of the forehead, close above one brow at the hairline",
          133137);

        // 08_Lalafell_Plainsfolk_male_face4
        S("Bürstenbart", "brush moustache",
          "breiter, dichter Schnurrbart, der die ganze Oberlippe bedeckt und unten gerade abschließt", "a broad dense moustache covering the whole upper lip and cut off straight along the bottom",
          133141);
        S("Lange Koteletten", "long side whiskers",
          "zwei lange Bartzotteln, die von den Schläfen weit über den Kiefer hinaushängen, Kinn und Oberlippe bleiben frei", "two long tufts of whisker hanging from the temples well past the jaw, chin and upper lip left bare",
          133142);
        S("Nase abgesetzt", "darkened nose",
          "die ganze Nasenkuppe ist flächig dunkel abgesetzt, die Wangen bleiben frei", "the whole tip of the nose is covered by a solid dark patch, the cheeks left clear",
          133143);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133144);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133145);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133146);
        S("Stirnzeichen", "brow mark",
          "geschwungenes Zeichen aus zwei Haken seitlich auf der Stirn, dicht über einer Braue am Haaransatz", "a curved mark made of two hooks at the side of the forehead, close above one brow at the hairline",
          133147);

        // 09_Lalafell_Plainsfolk_female_face1
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz, der das Auge oben kräftig umrandet und zum äußeren Winkel hin breiter wird", "a dense dark fringe of lashes rimming the eye strongly along the upper lid and widening toward the outer corner",
          133311);
        S("Lidschatten", "eye shadow",
          "weicher Schatten über dem Oberlid, der nach oben zur Braue hin ausläuft", "a soft shadow laid over the upper lid, fading upward toward the brow",
          133312);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133313);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133314);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133315);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133316);
        S("Wangenzeichen", "cheek mark",
          "zwei geschwungene, spitz zulaufende Striche nebeneinander auf dem Wangenknochen, unter dem äußeren Augenwinkel", "two curved tapering strokes side by side on the cheekbone, below the outer corner of the eye",
          133317);

        // 09_Lalafell_Plainsfolk_female_face2
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz, der das Auge oben kräftig umrandet und zum äußeren Winkel hin breiter wird", "a dense dark fringe of lashes rimming the eye strongly along the upper lid and widening toward the outer corner",
          133321);
        S("Lidschatten", "eye shadow",
          "weicher Schatten über dem Oberlid, der nach oben zur Braue hin ausläuft", "a soft shadow laid over the upper lid, fading upward toward the brow",
          133322);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133323);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133324);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133325);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133326);
        S("Wangenzeichen", "cheek mark",
          "zwei geschwungene, spitz zulaufende Striche nebeneinander auf dem Wangenknochen, unter dem äußeren Augenwinkel", "two curved tapering strokes side by side on the cheekbone, below the outer corner of the eye",
          133327);

        // 09_Lalafell_Plainsfolk_female_face3
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz, der das Auge oben kräftig umrandet und zum äußeren Winkel hin breiter wird", "a dense dark fringe of lashes rimming the eye strongly along the upper lid and widening toward the outer corner",
          133331);
        S("Lidschatten", "eye shadow",
          "kräftiger, weich auslaufender Schatten auf dem ganzen Oberlid bis hinauf zur Braue", "a strong soft-edged shadow over the whole upper lid, reaching up to the brow",
          133332);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133333);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133334);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133335);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133336);
        S("Wangenzeichen", "cheek mark",
          "zwei geschwungene, spitz zulaufende Striche nebeneinander auf dem Wangenknochen, unter dem äußeren Augenwinkel", "two curved tapering strokes side by side on the cheekbone, below the outer corner of the eye",
          133337);

        // 09_Lalafell_Plainsfolk_female_face4
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz, der das Auge oben kräftig umrandet und zum äußeren Winkel hin breiter wird", "a dense dark fringe of lashes rimming the eye strongly along the upper lid and widening toward the outer corner",
          133341);
        S("Lidschatten", "eye shadow",
          "weicher Schatten über dem Oberlid, der nach oben zur Braue hin ausläuft", "a soft shadow laid over the upper lid, fading upward toward the brow",
          133342);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133343);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133344);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133345);
        S("Wangenflecken", "cheek patches",
          "je ein weicher ovaler Fleck mit unscharfem Rand hoch auf beiden Wangen, unter dem äußeren Augenwinkel", "one soft oval patch with a blurred edge high on each cheek, below the outer corner of the eye",
          133346);
        S("Wangenzeichen", "cheek mark",
          "zwei geschwungene, spitz zulaufende Striche nebeneinander auf dem Wangenknochen, unter dem äußeren Augenwinkel", "two curved tapering strokes side by side on the cheekbone, below the outer corner of the eye",
          133347);

        // 10_Lalafell_Dunesfolk_male_face1
        S("Zwirbelbart", "curled moustache",
          "schmaler Schnurrbart dicht über der Oberlippe, dessen Enden beidseitig zu kleinen Spitzen nach oben gedreht sind", "a narrow moustache close above the upper lip, its ends twisted up into small points on both sides",
          133611);
        S("Kinnbart, spitz", "pointed chin tuft",
          "schmaler Bartbüschel direkt unter der Unterlippe, der spitz nach unten ausläuft", "a narrow tuft of beard directly under the lower lip, tapering to a point downward",
          133612);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133613);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133614);
        S("Stirnschmuck, Tropfen", "teardrop brow jewel",
          "tropfenförmiger Schmuckstein mit gefasstem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a teardrop-shaped jewel with a raised rim and a round bead at its centre, set on the forehead between the brows",
          133615);
        S("Wangenstrich", "cheek stroke",
          "schmaler, an beiden Enden spitz zulaufender Strich, schräg auf dem Wangenknochen ein Stück unter dem Auge", "a slim stroke tapering at both ends, set at an angle on the cheekbone a little below the eye",
          133616);
        S("Wangenstrich", "cheek stroke",
          "schmaler, an beiden Enden spitz zulaufender Strich, schräg auf dem Wangenknochen ein Stück unter dem Auge", "a slim stroke tapering at both ends, set at an angle on the cheekbone a little below the eye",
          133617);

        // 10_Lalafell_Dunesfolk_male_face2
        S("Schnurrbart, geteilt", "split moustache",
          "dünner Schnurrbart mit einer Lücke unter der Nase, dessen Hälften nach außen und oben abstehen", "a thin moustache with a gap under the nose, its two halves flaring outward and upward",
          133621);
        S("Kinnbart, zottig", "shaggy chin tuft",
          "breiterer, zottiger Bartbüschel unter der Unterlippe, der nach unten ausfranst", "a wider shaggy tuft of beard under the lower lip, frayed out downward",
          133622);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133623);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133624);
        S("Stirnschmuck, Tropfen", "teardrop brow jewel",
          "tropfenförmiger Schmuckstein mit gefasstem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a teardrop-shaped jewel with a raised rim and a round bead at its centre, set on the forehead between the brows",
          133625);
        S("Wangensichel", "cheek crescent",
          "sichelförmiger Strich, der sich dicht an das Unterlid schmiegt", "a crescent-shaped stroke hugging the lower lid closely",
          133626);
        S("Wangensichel", "cheek crescent",
          "sichelförmiger Strich, der sich dicht an das Unterlid schmiegt", "a crescent-shaped stroke hugging the lower lid closely",
          133627);

        // 10_Lalafell_Dunesfolk_male_face3
        S("Hängebart", "drooping moustache",
          "voller Schnurrbart, dessen lange Enden weit über den Kiefer hinaus nach unten hängen", "a full moustache whose long ends hang down well past the jaw",
          133631);
        S("Kinnbart, lang", "long chin tuft",
          "längerer, voller Bartbüschel, der vom Kinn herabhängt", "a longer, fuller tuft of beard hanging down from the chin",
          133632);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133633);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133634);
        S("Stirnschmuck, Tropfen", "teardrop brow jewel",
          "tropfenförmiger Schmuckstein mit gefasstem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a teardrop-shaped jewel with a raised rim and a round bead at its centre, set on the forehead between the brows",
          133635);
        S("Wangenstrich, lang", "long cheek stroke",
          "langer, schmaler Strich, der schräg über den Wangenknochen unter dem Auge verläuft", "a long slim stroke running at an angle across the cheekbone below the eye",
          133636);
        S("Wangenstrich, lang", "long cheek stroke",
          "langer, schmaler Strich, der schräg über den Wangenknochen unter dem Auge verläuft", "a long slim stroke running at an angle across the cheekbone below the eye",
          133637);

        // 10_Lalafell_Dunesfolk_male_face4
        S("Bürstenbart", "brush moustache",
          "breiter, dichter Schnurrbart, der die ganze Oberlippe bedeckt und unten gerade abschließt", "a broad dense moustache covering the whole upper lip and cut off straight along the bottom",
          133641);
        S("Kinnstoppeln", "chin stubble",
          "diffuse Fläche kurzer Stoppeln auf dem Kinn ohne klare Umrisslinie", "a diffuse area of short stubble on the chin with no defined outline",
          133642);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133643);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133644);
        S("Stirnschmuck, Tropfen", "teardrop brow jewel",
          "tropfenförmiger Schmuckstein mit gefasstem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a teardrop-shaped jewel with a raised rim and a round bead at its centre, set on the forehead between the brows",
          133645);
        S("Wangenstrich, kurz", "short cheek stroke",
          "kurzer, schmaler Strich dicht unter dem Auge auf dem Wangenknochen", "a short slim stroke close under the eye on the cheekbone",
          133646);
        S("Wangenstrich, kurz", "short cheek stroke",
          "kurzer, schmaler Strich dicht unter dem Auge auf dem Wangenknochen", "a short slim stroke close under the eye on the cheekbone",
          133647);

        // 11_Lalafell_Dunesfolk_female_face1
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz am Oberlid, der zum äußeren Winkel hin auffächert", "a dense dark fringe of lashes on the upper lid, fanning out toward the outer corner",
          133811);
        S("Stirnschmuck, Wappen", "crest brow jewel",
          "wappenförmiger Schmuckstein mit gezacktem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a crest-shaped jewel with a scalloped rim and a round bead at its centre, set on the forehead between the brows",
          133812);
        S("Lidschatten", "eye shadow",
          "breiter, weich auslaufender Schatten über dem Oberlid bis hinauf zur Braue", "a broad soft-edged shadow over the upper lid, reaching up to the brow",
          133813);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133814);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133815);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133816);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133817);

        // 11_Lalafell_Dunesfolk_female_face2
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz am Oberlid, der zum äußeren Winkel hin auffächert", "a dense dark fringe of lashes on the upper lid, fanning out toward the outer corner",
          133821);
        S("Stirnschmuck, Wappen", "crest brow jewel",
          "wappenförmiger Schmuckstein mit gezacktem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a crest-shaped jewel with a scalloped rim and a round bead at its centre, set on the forehead between the brows",
          133822);
        S("Lidschatten", "eye shadow",
          "breiter, weich auslaufender Schatten über dem Oberlid bis hinauf zur Braue", "a broad soft-edged shadow over the upper lid, reaching up to the brow",
          133823);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133824);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133825);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133826);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133827);

        // 11_Lalafell_Dunesfolk_female_face3
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz am Oberlid, der zum äußeren Winkel hin auffächert", "a dense dark fringe of lashes on the upper lid, fanning out toward the outer corner",
          133831);
        S("Stirnschmuck, Wappen", "crest brow jewel",
          "wappenförmiger Schmuckstein mit gezacktem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a crest-shaped jewel with a scalloped rim and a round bead at its centre, set on the forehead between the brows",
          133832);
        S("Lidschatten", "eye shadow",
          "kräftiger, weich auslaufender Schatten über dem ganzen Oberlid bis hinauf zur Braue", "a strong soft-edged shadow over the whole upper lid, reaching up to the brow",
          133833);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133834);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133835);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133836);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133837);

        // 11_Lalafell_Dunesfolk_female_face4
        S("Dichte Wimpern", "dense lashes",
          "dichter dunkler Wimpernkranz am Oberlid, der zum äußeren Winkel hin auffächert", "a dense dark fringe of lashes on the upper lid, fanning out toward the outer corner",
          133841);
        S("Stirnschmuck, Wappen", "crest brow jewel",
          "wappenförmiger Schmuckstein mit gezacktem Rand und runder Perle in der Mitte, mittig auf der Stirn zwischen den Brauen", "a crest-shaped jewel with a scalloped rim and a round bead at its centre, set on the forehead between the brows",
          133842);
        S("Lidschatten", "eye shadow",
          "breiter, weich auslaufender Schatten über dem Oberlid bis hinauf zur Braue", "a broad soft-edged shadow over the upper lid, reaching up to the brow",
          133843);
        S("Nase abgesetzt", "darkened nose",
          "die Nasenkuppe ist als runder dunkler Fleck abgesetzt, die Wangen bleiben frei", "the tip of the nose is marked out as a round dark patch, the cheeks left clear",
          133844);
        S("Ohrring", "ear ring",
          "offener Reif mit ausgezogenem Haken, der am unteren Ohrrand hängt", "an open hoop with a drawn-out hook, hanging from the lower rim of the ear",
          133845);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133846);
        S("Wangensichel", "cheek crescent",
          "kräftige, nach oben offene Sichel dicht unter dem Auge, an einem Ende stumpf, am anderen spitz auslaufend", "a bold crescent opening upward close under the eye, blunt at one end and tapering at the other",
          133847);

        // ---- feat-miqote.cs ----
        // MIQO'TE — FacialFeatureOption icon descriptions
        // Icon ids copied from the cell labels of the contact sheets in
        // tools\icons\sheets\Facial_Features\
        // NOTE: the natural Miqo'te cheek marking of each face is present on every entry
        // and is therefore never described.
        // NOT INCLUDED (feature not identifiable in the render): 134142, 134642 — see report.

        // 12_Miqote_Seeker_of_the_Sun_male_face1
        S("Bartbüschel am Kinn", "tuft of beard on the chin",
          "ein kurzer, borstiger Bartbüschel auf dem Kinn direkt unter der Unterlippe", "a short, bristly tuft of beard on the chin directly below the lower lip",
          134111);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, senkrechte Narbe, die von der Stirn über Braue und Auge bis auf die Wange hinabläuft", "a long vertical scar running from the forehead across the brow and the eye down onto the cheek",
          134112);
        S("senkrechte Wangennarbe", "upright scar on the cheek",
          "eine schmale, fast senkrechte Narbe auf der hinteren Wange, die unter dem äußeren Augenwinkel beginnt und zum Kiefer hinabzieht", "a narrow, almost upright scar on the rear cheek, beginning below the outer corner of the eye and running down to the jaw",
          134113);
        S("feine Schrägnarbe", "fine slanted scar",
          "eine feine, gerade Narbe quer über die Wangenmitte, die schräg zur Nase hin ansteigt", "a fine, straight scar across the middle of the cheek, slanting upward toward the nose",
          134114);
        S("breite Schrägnarbe", "broad slanted scar",
          "eine breitere Narbe quer über die Wangenmitte, in der Mitte am dicksten und zu beiden Enden spitz auslaufend", "a broader scar across the middle of the cheek, thickest in the middle and tapering to a point at each end",
          134115);
        S("breiter Wangenstreifen", "broad stripe on the cheek",
          "ein breiter, geschwungener Streifen auf der hinteren Wange, der bis zum Kieferrand hinabreicht, dazu ein Büschel aus drei schmalen Tropfenformen neben dem Nasenrücken", "a broad curved stripe on the rear cheek reaching down to the jawline, plus a cluster of three narrow teardrop shapes beside the bridge of the nose",
          134116);
        S("Sichel am Auge", "crescent at the eye",
          "eine kräftige Sichel, die den äußeren Augenwinkel umschließt, mit zwei runden Tupfen daneben zur Schläfe hin", "a bold crescent curving around the outer corner of the eye, with two round dots beside it toward the temple",
          134117);

        // 12_Miqote_Seeker_of_the_Sun_male_face2
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134121);
        S("Bart am Kieferrand", "beard along the jaw",
          "ein kurzer Bart, der von den Koteletten am ganzen Kieferrand entlang bis zum Kinn reicht", "a short beard reaching from the sideburns along the whole jawline to the chin",
          134122);
        S("breiter Kinnbart", "broad chin beard",
          "ein breiter Bartfleck, der das Kinn und die Partie unter der Unterlippe bedeckt und fächerförmig nach unten absteht", "a broad patch of beard covering the chin and the area below the lower lip, fanning out downward",
          134123);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, senkrechte Narbe, die von der Stirn über Braue und Auge bis auf die Wange hinabläuft", "a long vertical scar running from the forehead across the brow and the eye down onto the cheek",
          134124);
        S("Schrägnarbe auf der Wange", "slanted scar on the cheek",
          "eine schmale, schräge Narbe quer über die Wange, die vom Wangenknochen nach hinten unten zum Kiefer verläuft", "a narrow slanting scar across the cheek, running from the cheekbone down and back toward the jaw",
          134125);
        S("breiter Wangenstreifen", "broad stripe on the cheek",
          "ein breiter Streifen quer über die Wange, oben breit und zum Kieferrand hin spitz zulaufend", "a broad stripe across the cheek, wide at the top and tapering to a point toward the jawline",
          134126);
        S("Zeichen auf dem Nasenrücken", "mark on the bridge of the nose",
          "ein kleines dreiteiliges Zeichen auf dem Nasenrücken: ein Tropfen in der Mitte, flankiert von zwei kleineren, spitzen Blättern", "a small three-part mark on the bridge of the nose: a teardrop in the middle flanked by two smaller pointed leaf shapes",
          134127);

        // 12_Miqote_Seeker_of_the_Sun_male_face3
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134131);
        S("Narbe über dem Auge", "scar over the eye",
          "eine feine, senkrechte Narbe, die von der Stirn über die Braue läuft und unterhalb des Auges auf der Wange weitergeht", "a fine vertical scar running from the forehead across the brow and continuing below the eye on the cheek",
          134132);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine lange, dünne Narbe, die fast waagerecht quer über eine Wange verläuft", "a long thin scar running almost horizontally across one cheek",
          134133);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine lange, dünne Narbe, die fast waagerecht quer über eine Wange verläuft", "a long thin scar running almost horizontally across one cheek",
          134134);
        S("Narbe am Mundwinkel", "scar at the corner of the mouth",
          "eine senkrechte Narbe, die von der Wange herab über den Mundwinkel bis auf das Kinn zieht", "a vertical scar running down the cheek across the corner of the mouth and onto the chin",
          134135);
        S("zwei Krallenstriche", "two claw strokes",
          "zwei breite, spitz zulaufende Striche schräg auf der äußeren Wange, die vom Haaransatz nach vorn unten weisen", "two broad tapering strokes set diagonally on the outer cheek, pointing forward and downward from the hairline",
          134136);
        S("Bogen am Auge", "arc at the eye",
          "ein geschwungener Bogen, der unter dem äußeren Augenwinkel entlang zur Schläfe zieht, mit zwei runden Tupfen darüber", "a curved arc running below the outer corner of the eye toward the temple, with two round dots above it",
          134137);

        // 12_Miqote_Seeker_of_the_Sun_male_face4
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134141);
        // 134142 — omitted, no feature identifiable in the icon
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, schräge Narbe, die über einer Braue auf der Stirn beginnt, über den Nasenrücken läuft und unter dem anderen Auge endet", "a long slanting scar starting on the forehead above one brow, crossing the bridge of the nose and ending below the other eye",
          134143);
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, schräge Narbe, die über einer Braue auf der Stirn beginnt, über den Nasenrücken läuft und unter dem anderen Auge endet", "a long slanting scar starting on the forehead above one brow, crossing the bridge of the nose and ending below the other eye",
          134144);
        S("Narbe unter dem Mundwinkel", "scar below the corner of the mouth",
          "eine feine Narbe unterhalb des Mundwinkels, die schräg nach hinten unten zum Kieferrand zieht", "a fine scar below the corner of the mouth, running diagonally down and back toward the jawline",
          134145);
        S("zwei Krallenstriche", "two claw strokes",
          "zwei breite, spitz zulaufende Striche schräg auf der äußeren Wange, die vom Haaransatz nach vorn unten weisen", "two broad tapering strokes set diagonally on the outer cheek, pointing forward and downward from the hairline",
          134146);
        S("Zeichen auf dem Nasenrücken", "mark on the bridge of the nose",
          "ein kleines dreiteiliges Zeichen auf dem Nasenrücken: ein Tropfen in der Mitte, flankiert von zwei kleineren, spitzen Blättern", "a small three-part mark on the bridge of the nose: a teardrop in the middle flanked by two smaller pointed leaf shapes",
          134147);

        // 13_Miqote_Seeker_of_the_Sun_female_face1
        S("buschige Brauen", "bushy eyebrows",
          "dichte, breite Augenbrauen mit sichtbar borstigem Rand", "thick, broad eyebrows with a visibly bristly edge",
          134311);
        S("Stein auf der Stirn", "stone on the forehead",
          "ein kleiner, runder, gefasster Stein mitten auf der Stirn zwischen den Augenbrauen", "a small round set stone in the middle of the forehead between the eyebrows",
          134312);
        S("zwei kurze Narben", "two short scars",
          "eine kurze, steile Narbe über der Augenbraue und eine zweite kurze Narbe unterhalb des Auges auf der Wange", "a short steep scar above the eyebrow and a second short scar below the eye on the cheek",
          134313);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine kurze, in der Mitte verdickte Narbe schräg über eine Wange, vom Wangenknochen nach hinten unten", "a short scar, thickened in the middle, running diagonally across one cheek from the cheekbone down and back",
          134314);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine lange, feine Narbe schräg über eine Wange, die am hinteren Ende leicht abknickt", "a long fine scar running diagonally across one cheek, with a slight kink at its rear end",
          134315);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134316);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134317);

        // 13_Miqote_Seeker_of_the_Sun_female_face2
        S("buschige Brauen", "bushy eyebrows",
          "dichte, breite Augenbrauen mit sichtbar borstigem Rand", "thick, broad eyebrows with a visibly bristly edge",
          134321);
        S("Stein auf der Stirn", "stone on the forehead",
          "ein kleiner, runder, gefasster Stein mitten auf der Stirn zwischen den Augenbrauen", "a small round set stone in the middle of the forehead between the eyebrows",
          134322);
        S("steile Narbe, eine Wange", "steep scar on one cheek",
          "eine feine Narbe, die schräg über eine Wange läuft, vom Wangenknochen abwärts in Richtung Mundwinkel", "a fine scar running diagonally across one cheek, from the cheekbone downward toward the corner of the mouth",
          134323);
        S("flache Narbe, eine Wange", "shallow scar on one cheek",
          "eine feine, flach verlaufende Narbe tiefer auf einer Wange, die nur wenig nach vorn unten abfällt", "a fine, shallow-running scar lower on one cheek, dropping only slightly forward and down",
          134324);
        S("Fältchen um die Augen", "fine wrinkles around the eyes",
          "feine Fältchen an den äußeren Augenwinkeln und eine Querfalte oben auf dem Nasenrücken", "fine wrinkles at the outer corners of the eyes and a crease across the top of the bridge of the nose",
          134325);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134326);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134327);

        // 13_Miqote_Seeker_of_the_Sun_female_face3
        S("buschige Brauen", "bushy eyebrows",
          "dichte, breite Augenbrauen mit sichtbar borstigem Rand", "thick, broad eyebrows with a visibly bristly edge",
          134331);
        S("Stein auf der Stirn", "stone on the forehead",
          "ein kleiner, runder, gefasster Stein mitten auf der Stirn zwischen den Augenbrauen", "a small round set stone in the middle of the forehead between the eyebrows",
          134332);
        S("steile Narbe, eine Wange", "steep scar on one cheek",
          "eine feine, steil verlaufende Narbe über eine Wange, die vom Wangenknochen nach vorn unten zieht", "a fine, steeply running scar across one cheek, drawn from the cheekbone forward and down",
          134333);
        S("Fältchen um die Augen", "fine wrinkles around the eyes",
          "feine Fältchen an den äußeren Augenwinkeln und eine Querfalte oben auf dem Nasenrücken", "fine wrinkles at the outer corners of the eyes and a crease across the top of the bridge of the nose",
          134334);
        S("steile Narbe, eine Wange", "steep scar on one cheek",
          "eine feine, steil verlaufende Narbe über eine Wange, die vom Wangenknochen nach vorn unten zieht", "a fine, steeply running scar across one cheek, drawn from the cheekbone forward and down",
          134335);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134336);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134337);

        // 13_Miqote_Seeker_of_the_Sun_female_face4
        S("buschige Brauen", "bushy eyebrows",
          "dichte, breite Augenbrauen mit sichtbar borstigem Rand", "thick, broad eyebrows with a visibly bristly edge",
          134341);
        S("Stein auf der Stirn", "stone on the forehead",
          "ein kleiner, runder, gefasster Stein mitten auf der Stirn zwischen den Augenbrauen", "a small round set stone in the middle of the forehead between the eyebrows",
          134342);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine lange, dünne Narbe quer über eine Wange, in der Mitte am breitesten und zu beiden Enden spitz auslaufend", "a long thin scar across one cheek, widest in the middle and tapering to a point at each end",
          134343);
        S("Narbe, eine Wange", "scar on one cheek",
          "eine lange, dünne Narbe quer über eine Wange, in der Mitte am breitesten und zu beiden Enden spitz auslaufend", "a long thin scar across one cheek, widest in the middle and tapering to a point at each end",
          134344);
        S("Fältchen um die Augen", "fine wrinkles around the eyes",
          "feine Fältchen an den äußeren Augenwinkeln und eine Querfalte oben auf dem Nasenrücken", "fine wrinkles at the outer corners of the eyes and a crease across the top of the bridge of the nose",
          134345);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134346);
        S("Sichel unter einem Auge", "crescent under one eye",
          "ein kräftiger, sichelförmiger Strich unmittelbar unter einem Auge, außen breit und zur Nase hin spitz auslaufend", "a bold crescent-shaped stroke directly below one eye, broad at the outer end and tapering toward the nose",
          134347);

        // 14_Miqote_Keeper_of_the_Moon_male_face1
        S("Bartbüschel am Kinn", "tuft of beard on the chin",
          "ein kurzer, borstiger Bartbüschel auf dem Kinn unterhalb der Unterlippe, der sich ein Stück am Kieferrand entlangzieht", "a short bristly tuft of beard on the chin below the lower lip, running a little way along the jawline",
          134611);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, senkrechte Narbe, die von der Stirn über die Braue läuft und unterhalb des Auges auf der Wange weitergeht", "a long vertical scar running from the forehead across the brow and continuing below the eye on the cheek",
          134612);
        S("Schrägnarbe, eine Wange", "slanted scar on one cheek",
          "eine schräge Narbe auf einer Wange, in der Mitte verdickt, die vom Wangenknochen nach hinten unten verläuft", "a slanting scar on one cheek, thickened in the middle, running from the cheekbone down and back",
          134613);
        S("steile Narbe, eine Wange", "steep scar on one cheek",
          "eine feine, fast senkrechte Narbe auf einer Wange, die unterhalb des Auges beginnt und zum Kiefer hinabzieht", "a fine, almost upright scar on one cheek, beginning below the eye and running down toward the jaw",
          134614);
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134615);
        S("breites Band, eine Wange", "broad band on one cheek",
          "ein breites, geknicktes Band quer über eine Wange, das von unterhalb des Wangenknochens bis zum Kieferwinkel hinabreicht", "a broad, angled band across one cheek, reaching from below the cheekbone down to the angle of the jaw",
          134616);
        S("Zeichen auf dem Nasenrücken", "mark on the bridge of the nose",
          "ein kleines dreiteiliges Zeichen auf dem Nasenrücken: ein Tropfen in der Mitte, flankiert von zwei kleineren, spitzen Blättern", "a small three-part mark on the bridge of the nose: a teardrop in the middle flanked by two smaller pointed leaf shapes",
          134617);

        // 14_Miqote_Keeper_of_the_Moon_male_face2
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134621);
        S("Bart am Kieferrand", "beard along the jaw",
          "ein kurzer Bart, der von den Koteletten am Kieferrand entlang bis zum Kinn reicht", "a short beard reaching from the sideburns along the jawline to the chin",
          134622);
        S("Schnurrbart und Kinnbart", "moustache and chin beard",
          "ein schmaler Schnurrbart über der Oberlippe und darunter ein breiter Bartfleck auf dem Kinn", "a narrow moustache above the upper lip and below it a broad patch of beard on the chin",
          134623);
        S("feine Narbe, eine Wange", "fine scar on one cheek",
          "eine feine, schräge Narbe auf einer Wange unterhalb des Auges, die nach hinten unten verläuft", "a fine slanting scar on one cheek below the eye, running down and back",
          134624);
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134625);
        S("Sichelstrich, eine Wange", "sickle stroke on one cheek",
          "ein breiter, vorn spitz zulaufender Strich, der von der Wangenmitte nach hinten unten zum Kiefer schwingt und dort in einem Haken endet", "a broad stroke tapering to a point at the front, sweeping from the middle of the cheek down and back to the jaw and ending in a hook",
          134626);
        S("langer Sichelstrich, eine Wange", "long sickle stroke on one cheek",
          "ein längerer, breiter Strich auf einer Wange, der dicht unter dem Auge spitz beginnt und weit nach hinten unten bis zum Kieferwinkel schwingt", "a longer broad stroke on one cheek, starting in a point just below the eye and sweeping far down and back to the angle of the jaw",
          134627);

        // 14_Miqote_Keeper_of_the_Moon_male_face3
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134631);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, feine, senkrechte Narbe, die von der Stirn über die Braue läuft und unterhalb des Auges auf der Wange weitergeht", "a long fine vertical scar running from the forehead across the brow and continuing below the eye on the cheek",
          134632);
        S("waagerechte Narbe, eine Wange", "horizontal scar on one cheek",
          "eine lange, dünne Narbe fast waagerecht über eine Wange, die zum Ohr hin leicht ansteigt", "a long thin scar running almost horizontally across one cheek, rising slightly toward the ear",
          134633);
        S("Narbe am Mundwinkel", "scar at the corner of the mouth",
          "eine senkrechte Narbe, die von der Wange herab über den Mundwinkel bis auf das Kinn zieht", "a vertical scar running down the cheek across the corner of the mouth and onto the chin",
          134634);
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134635);
        S("Pfotenmuster, ein Auge", "paw pattern at one eye",
          "ein Muster aus zwei runden Tupfen und einem geschwungenen Strich am äußeren Winkel eines Auges", "a pattern of two round dots and a curved stroke at the outer corner of one eye",
          134636);
        S("Pfotenmuster, ein Auge", "paw pattern at one eye",
          "ein Muster aus zwei runden Tupfen und einem geschwungenen Strich am äußeren Winkel eines Auges", "a pattern of two round dots and a curved stroke at the outer corner of one eye",
          134637);

        // 14_Miqote_Keeper_of_the_Moon_male_face4
        S("buschige Brauen", "bushy eyebrows",
          "dichte, borstige Augenbrauen, die deutlich über den Brauenwulst hinausstehen", "thick, bristly eyebrows standing out well beyond the brow ridge",
          134641);
        // 134642 — omitted, no feature identifiable in the icon
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, schräge Narbe, die über einer Braue auf der Stirn beginnt, über den Nasenrücken läuft und unter dem anderen Auge endet", "a long slanting scar starting on the forehead above one brow, crossing the bridge of the nose and ending below the other eye",
          134643);
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, schräge Narbe, die über einer Braue auf der Stirn beginnt, über den Nasenrücken läuft und unter dem anderen Auge endet", "a long slanting scar starting on the forehead above one brow, crossing the bridge of the nose and ending below the other eye",
          134644);
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134645);
        S("zwei Krallenstriche, eine Wange", "two claw strokes on one cheek",
          "zwei breite, nach vorn spitz zulaufende Striche schräg über eine Wange, die vom Haaransatz nach vorn weisen", "two broad strokes tapering forward to points, set diagonally across one cheek and pointing forward from the hairline",
          134646);
        S("zwei Krallenstriche, eine Wange", "two claw strokes on one cheek",
          "zwei breite, nach vorn spitz zulaufende Striche schräg über eine Wange, die vom Haaransatz nach vorn weisen", "two broad strokes tapering forward to points, set diagonally across one cheek and pointing forward from the hairline",
          134647);

        // 15_Miqote_Keeper_of_the_Moon_female_face1
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134811);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, schräge Narbe, die auf der Stirn beginnt, über die Braue zieht und unterhalb des Auges auf der Wange weiterläuft", "a long slanting scar that starts on the forehead, crosses the brow and continues below the eye on the cheek",
          134812);
        S("Narbe auf der Stirn", "scar on the forehead",
          "eine kurze, schräge Narbe auf der Stirn dicht über dem äußeren Ende der Augenbraue", "a short slanting scar on the forehead just above the outer end of the eyebrow",
          134813);
        S("Pflaster auf der Wange", "plaster on the cheek",
          "ein kleines, rechteckiges Pflaster mit sichtbarer Gewebestruktur, hochkant auf dem Wangenknochen", "a small rectangular plaster with visible fabric texture, set upright on the cheekbone",
          134814);
        S("Schönheitsfleck unter dem Auge", "beauty spot below the eye",
          "ein kleiner, runder Schönheitsfleck auf der Wange dicht unter dem äußeren Teil des Auges", "a small round beauty spot on the cheek just below the outer part of the eye",
          134815);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134816);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134817);

        // 15_Miqote_Keeper_of_the_Moon_female_face2
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134821);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, schräge Narbe, die auf der Stirn beginnt, über die Braue zieht und unterhalb des Auges auf der Wange weiterläuft", "a long slanting scar that starts on the forehead, crosses the brow and continues below the eye on the cheek",
          134822);
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, feine Narbe, die waagerecht quer über den Nasenrücken von einer Wange zur anderen verläuft", "a long fine scar running horizontally across the bridge of the nose from one cheek to the other",
          134823);
        S("Pflaster auf der Nase", "plaster on the nose",
          "ein schmales, rechteckiges Pflaster quer über den Nasenrücken", "a narrow rectangular plaster across the bridge of the nose",
          134824);
        S("Schönheitsfleck unter dem Auge", "beauty spot below the eye",
          "ein kleiner, runder Schönheitsfleck auf der Wange dicht unter dem äußeren Teil des Auges", "a small round beauty spot on the cheek just below the outer part of the eye",
          134825);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134826);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134827);

        // 15_Miqote_Keeper_of_the_Moon_female_face3
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134831);
        S("Narbe über den Nasenrücken", "scar across the bridge of the nose",
          "eine lange, feine Narbe, die von der einen Braue schräg über den Nasenrücken bis neben die Nase auf der anderen Seite verläuft", "a long fine scar running diagonally from one brow across the bridge of the nose to beside the nose on the other side",
          134832);
        S("geknickte Narbe auf der Wange", "kinked scar on the cheek",
          "eine feine, leicht geknickte Narbe schräg auf der Wange, die nach hinten unten zum Kiefer zieht", "a fine, slightly kinked scar set diagonally on the cheek, running down and back toward the jaw",
          134833);
        S("Pflaster auf der Wange", "plaster on the cheek",
          "ein kleines, rechteckiges Pflaster schräg auf der Wange, mit der Längsseite nach hinten unten gerichtet", "a small rectangular plaster set diagonally on the cheek, its long side pointing down and back",
          134834);
        S("Schönheitsfleck unter dem Auge", "beauty spot below the eye",
          "ein kleiner, runder Schönheitsfleck auf der Wange unterhalb des äußeren Augenwinkels", "a small round beauty spot on the cheek below the outer corner of the eye",
          134835);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134836);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134837);

        // 15_Miqote_Keeper_of_the_Moon_female_face4
        S("Tropfen auf der Stirn", "teardrop on the forehead",
          "ein tropfenförmiges Mal mitten auf der Stirn zwischen den Brauen, oben rund und nach unten spitz auslaufend", "a teardrop-shaped mark in the middle of the forehead between the brows, round at the top and tapering to a point below",
          134841);
        S("Narbe über dem Auge", "scar over the eye",
          "eine lange, schräge Narbe, die auf der Stirn beginnt, über die Braue zieht und unterhalb des Auges auf der Wange weiterläuft", "a long slanting scar that starts on the forehead, crosses the brow and continues below the eye on the cheek",
          134842);
        S("senkrechte Wangennarbe", "upright scar on the cheek",
          "eine lange, feine Narbe, die von unterhalb des Auges fast senkrecht über die Wange bis zum Kiefer hinabläuft", "a long fine scar running from below the eye almost vertically down the cheek to the jaw",
          134843);
        S("Pflaster auf der Wange", "plaster on the cheek",
          "ein kleines, rechteckiges Pflaster hochkant auf der Wange, leicht nach hinten geneigt", "a small rectangular plaster set upright on the cheek, tilted slightly backward",
          134844);
        S("Schönheitsfleck am Mund", "beauty spot by the mouth",
          "ein kleiner, runder Schönheitsfleck seitlich unterhalb des Mundwinkels", "a small round beauty spot to the side and below the corner of the mouth",
          134845);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134846);
        S("Spange an einem Ohr", "clasp on one ear",
          "eine kleine, durchbrochen gearbeitete Spange an einem Ohr: runder Kopf mit zwei großen Aussparungen und darunter ein geflügelter, nach unten spitz zulaufender Körper", "a small openwork clasp on one ear: a rounded head with two large cut-outs and below it a winged body tapering to a point",
          134847);

        // ---- feat-roegadyn.cs ----
        // Roegadyn - CharaMakeType.FacialFeatureOption
        // Slots 1-5 = "Gesichtsmerkmale" menu, slots 6-7 = "Tätowierungen" menu.
        // NO SIDE IS NAMED - see the SIDES section in the class summary. The two tattoo
        // slots are the same mark mirrored, so both now carry the SAME text.

        // 16_Roegadyn_Sea_Wolf_male_face1
        S("Kieferbart", "jaw beard",
          "dichter Bart über Kiefer und Kinn, der unter dem Kinn spitz ausläuft; die Oberlippe bleibt frei", "a dense beard over jaw and chin that tapers to a point below the chin, the upper lip left bare",
          135111);
        S("Hufeisenschnurrbart", "horseshoe moustache",
          "Schnurrbart über der Oberlippe, dessen Enden beidseits an den Mundwinkeln vorbei zum Kiefer hinablaufen; das Kinn bleibt frei", "a moustache above the upper lip whose ends run down past both corners of the mouth toward the jaw, the chin left bare",
          135112);
        S("Genähte Narben", "stitched scars",
          "lange, mit Quernähten übersäte Narbe, die vom Haaransatz schräg abwärts über eine Braue bis auf den Nasenrücken läuft, dazu waagerechte Schnitte über dem Nasenrücken auf beide Wangen", "a long scar covered with cross-stitch marks running from the hairline diagonally down over one eyebrow to the bridge of the nose, plus horizontal cuts across the bridge of the nose onto both cheeks",
          135113);
        S("Nasenrücken-Streifen", "nose-bridge stripe",
          "kurzer, breiter Querbalken quer über dem Nasenrücken zwischen den Augen", "a short broad bar straight across the bridge of the nose between the eyes",
          135114);
        S("Wangenbalken", "cheek bar",
          "schmaler Balken, der schräg auf einem Wangenknochen sitzt, unterhalb und hinter dem äußeren Augenwinkel", "a narrow bar set diagonally on one cheekbone, below and behind the outer corner of the eye",
          135115);
        S("Augenband", "eye band",
          "breites, sichelförmiges Band unter einem Auge, das am äußeren Ende zur Wange herabgezogen ist und zur Nase hin spitz ausläuft", "a broad crescent band under one eye, pulled down onto the cheek at its outer end and tapering to a point toward the nose",
          135116);
        S("Augenband", "eye band",
          "breites Band unter einem Auge, das über den Wangenknochen bis zur Schläfe reicht und an beiden Enden stumpf gerundet ist", "a broad band under one eye reaching across the cheekbone to the temple, bluntly rounded at both ends",
          135117);

        // 16_Roegadyn_Sea_Wolf_male_face2
        S("Zottiger Kinnbart", "shaggy chin beard",
          "zottiger Bart, der von den Koteletten am Kiefer entlang zieht und unter dem Kinn in langen, ausgefransten Strähnen hängt", "a shaggy beard running from the sideburns along the jaw and hanging below the chin in long ragged strands",
          135121);
        S("Wangenbalken", "cheek bar",
          "schmaler Balken, der schräg über die Wange unterhalb des Auges läuft", "a narrow bar running diagonally across the cheek below the eye",
          135122);
        S("Brauennarben", "brow scars",
          "genähte Narbe, die senkrecht über die Braue läuft, dazu ein kurzer Schnitt auf dem Wangenknochen unter dem Auge", "a stitched scar running vertically across the eyebrow, plus a short cut on the cheekbone below the eye",
          135123);
        S("Kratzer", "gashes",
          "zwei lange, parallele Schnitte, die schräg über eine Wange von der Nasenseite abwärts zum Kiefer laufen", "two long parallel cuts running diagonally across one cheek from beside the nose down to the jaw",
          135124);
        S("Kratzer", "gashes",
          "zwei lange, parallele Schnitte tiefer auf einer Wange, die schräg abwärts zum Mundwinkel hin laufen", "two long parallel cuts set lower on one cheek, running diagonally down toward the corner of the mouth",
          135125);
        S("Augenhaken", "eye hook",
          "breites Band, das unter einem Auge entlangläuft und sich am äußeren Augenwinkel nach oben hakt, mit einer Spitze, die auf die Wange hinabzeigt", "a broad band running under one eye and hooking upward at the outer corner, with a point reaching down onto the cheek",
          135126);
        S("Augenhaken", "eye hook",
          "breites Band, das unter einem Auge entlangläuft und sich am äußeren Augenwinkel nach oben hakt, mit einer Spitze, die auf die Wange hinabzeigt", "a broad band running under one eye and hooking upward at the outer corner, with a point reaching down onto the cheek",
          135127);

        // 16_Roegadyn_Sea_Wolf_male_face3
        S("Voller Kinnbart", "full chin beard",
          "dichter Bart über Kiefer und Kinn, der unter dem Kinn in einer schmalen Spitze zum Hals weiterläuft; die Oberlippe bleibt frei", "a dense beard over jaw and chin continuing below the chin in a narrow point toward the throat, the upper lip left bare",
          135131);
        S("Wangenmal", "cheek mark",
          "breites, geschwungenes Mal unter einem Auge, das sich über den Wangenknochen legt und in einem Haken ausläuft", "a broad curved mark under one eye lying across the cheekbone and ending in a hook",
          135132);
        S("Wangenmal", "cheek mark",
          "breites, geschwungenes Mal unter einem Auge, das sich über den Wangenknochen legt und in einem Haken ausläuft", "a broad curved mark under one eye lying across the cheekbone and ending in a hook",
          135133);
        S("Genähte Schnitte", "stitched cuts",
          "zwei senkrechte, mit Quernähten versehene Schnitte: einer auf der Stirn über der Braue, einer auf der Wange unter dem Auge", "two vertical cuts with cross-stitch marks: one on the forehead above the eyebrow, one on the cheek below the eye",
          135134);
        S("Feine Schnitte", "fine cuts",
          "feiner Schnitt quer durch die Braue und ein langer, feiner Schnitt darunter, der waagerecht über den Wangenknochen läuft", "a fine cut straight through the eyebrow and a long fine cut below it running horizontally across the cheekbone",
          135135);
        S("Sichel", "crescent",
          "schmale Sichel dicht unter dem unteren Lid eines Auges, die dem Lidrand folgt und zur Schläfe hin ausläuft", "a narrow crescent close under the lower lid of one eye, following the lid line and fading out toward the temple",
          135136);
        S("Sichel", "crescent",
          "schmale Sichel dicht unter dem unteren Lid eines Auges, die dem Lidrand folgt und zur Schläfe hin ausläuft", "a narrow crescent close under the lower lid of one eye, following the lid line and fading out toward the temple",
          135137);

        // 16_Roegadyn_Sea_Wolf_male_face4
        S("Wangenflaum", "cheek fuzz",
          "feiner, flaumiger Bartwuchs, der von den Koteletten nach vorn über die Wangen zieht; um den Mund bleibt die Haut frei", "fine downy whisker growth spreading forward from the sideburns across the cheeks, the skin around the mouth left bare",
          135141);
        S("Dichter Kieferbart", "dense jaw beard",
          "dichter Bart, der Kiefer und Kinn vollständig bedeckt und als breite Masse unter das Kinn reicht; die Oberlippe bleibt frei", "a dense beard covering jaw and chin completely and reaching below the chin as a broad mass, the upper lip left bare",
          135142);
        S("Schmaler Schnurrbart", "narrow moustache",
          "schmaler Streifen Barthaar direkt über der Oberlippe, der nicht über die Mundwinkel hinausreicht", "a narrow strip of hair directly above the upper lip that does not reach past the corners of the mouth",
          135143);
        S("Nasen- und Wangennarbe", "nose and cheek scar",
          "waagerechte Narbe quer über dem Nasenrücken und ein langer, schräger Schnitt, der neben einem Auge beginnt und über die Wange zum Kiefer läuft", "a horizontal scar across the bridge of the nose and a long diagonal cut starting beside one eye and running across the cheek to the jaw",
          135144);
        S("Breites Nasenband", "broad nose band",
          "breites, waagerechtes Band quer über dem Nasenrücken, das auf beiden Seiten bis auf die Wangenknochen reicht", "a broad horizontal band across the bridge of the nose reaching onto the cheekbones on both sides",
          135145);
        S("Keil", "wedge",
          "keilförmiges Feld in der Mulde unter einem Auge, das zum äußeren Augenwinkel hin breiter wird", "a wedge-shaped field in the hollow under one eye, widening toward the outer corner",
          135146);
        S("Keil", "wedge",
          "keilförmiges Feld in der Mulde unter einem Auge, das zum äußeren Augenwinkel hin breiter wird", "a wedge-shaped field in the hollow under one eye, widening toward the outer corner",
          135147);

        // 17_Roegadyn_Sea_Wolf_female_face1
        S("Schmale Augenbrauen", "narrow eyebrows",
          "schmale, scharf gezeichnete Augenbrauen mit hohem Bogen über beiden Augen", "narrow, sharply drawn eyebrows with a high arch over both eyes",
          135311);
        S("Narbe an einer Braue", "scar on one brow",
          "schräge Narbe, die über einer Braue beginnt und abwärts bis auf den Nasenrücken läuft", "a diagonal scar starting above one eyebrow and running down onto the bridge of the nose",
          135312);
        S("Narbe an einer Braue", "scar on one brow",
          "schräge Narbe, die über einer Braue beginnt und abwärts bis auf den Nasenrücken läuft", "a diagonal scar starting above one eyebrow and running down onto the bridge of the nose",
          135313);
        S("Leberfleck Wange", "mole on cheek",
          "kleiner runder Leberfleck auf einer Wange, unterhalb des Auges", "a small round mole on one cheek, below the eye",
          135314);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck knapp neben und etwas unter einem Mundwinkel", "a small round mole just beside and a little below one corner of the mouth",
          135315);
        S("Breites Wangenband", "broad cheek band",
          "breites Band, das unter beiden Augen über die Wangenknochen und quer über den Nasenrücken läuft und unten eine gestufte Kante hat", "a broad band running under both eyes across the cheekbones and straight over the bridge of the nose, with a stepped lower edge",
          135316);
        S("Stirnzeichen", "forehead emblem",
          "kleines, spitz zulaufendes Zeichen mitten auf der Stirn zwischen den Brauen", "a small pointed emblem in the middle of the forehead between the eyebrows",
          135317);

        // 17_Roegadyn_Sea_Wolf_female_face2
        S("Gerade Augenbrauen", "straight eyebrows",
          "schmale, scharf gezeichnete Augenbrauen, die fast gerade verlaufen und zum äußeren Ende hin leicht abfallen", "narrow, sharply drawn eyebrows running almost straight and dipping slightly toward the outer end",
          135321);
        S("Augenfältchen", "eye lines",
          "feine Linien, die sich vom äußeren Augenwinkel zur Schläfe hin auffächern, dazu eine Falte unter dem unteren Lid", "fine lines fanning out from the outer corner of the eye toward the temple, plus a crease under the lower lid",
          135322);
        S("Lange Narbe", "long scar",
          "lange, gerade Narbe, die auf einer Gesichtshälfte vom Haaransatz senkrecht über Braue und Auge hinweg bis auf die Wange läuft", "a long straight scar running vertically down one side of the face from the hairline over the brow and eye onto the cheek",
          135323);
        S("Lange Narbe", "long scar",
          "lange, gerade Narbe, die auf einer Gesichtshälfte vom Haaransatz senkrecht über Braue und Auge hinweg bis auf die Wange läuft", "a long straight scar running vertically down one side of the face from the hairline over the brow and eye onto the cheek",
          135324);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck auf einer Wange, dicht neben und etwas unter dem Mundwinkel", "a small round mole on one cheek, close beside and a little below the corner of the mouth",
          135325);
        S("Wappenzeichen Stirn", "shield emblem, forehead",
          "schildförmiges Zeichen mit ausgespartem Inneren mitten auf der Stirn zwischen den Brauen", "a shield-shaped emblem with a hollow centre in the middle of the forehead between the eyebrows",
          135326);
        S("Breites Wangenband", "broad cheek band",
          "breites Band, das unter beiden Augen über den Nasenrücken hinweg bis zu den Schläfen reicht und an den äußeren Enden breiter wird", "a broad band running under both eyes across the bridge of the nose out to the temples, widening at the outer ends",
          135327);

        // 17_Roegadyn_Sea_Wolf_female_face3
        S("Buschige Augenbrauen", "bushy eyebrows",
          "dichte, buschige Augenbrauen mit deutlich sichtbaren Härchen über beiden Augen", "dense bushy eyebrows with clearly visible hairs over both eyes",
          135331);
        S("Kurze Brauennarbe", "short brow scar",
          "kurze, schräge Narbe, die das innere Ende einer Braue kreuzt", "a short diagonal scar crossing the inner end of one eyebrow",
          135332);
        S("Wangennarbe", "cheek scar",
          "langer, feiner Schnitt, der waagerecht über eine Wange vom Mundwinkel zurück Richtung Ohr läuft", "a long fine cut running horizontally across one cheek from the corner of the mouth back toward the ear",
          135333);
        S("Leberfleck am Auge", "mole by the eye",
          "kleiner runder Leberfleck knapp außerhalb und etwas unter dem äußeren Winkel eines Auges", "a small round mole just outside and slightly below the outer corner of one eye",
          135334);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck tief auf einer Wange, ein Stück unterhalb und seitlich des Mundwinkels", "a small round mole low on one cheek, some way below and to the side of the corner of the mouth",
          135335);
        S("Balken", "bar",
          "gerader Balken dicht unter einem Auge, der dem unteren Lid folgt und von der Schläfenseite bis fast an die Nase reicht", "a straight bar close under one eye following the lower lid and reaching from the temple side almost to the nose",
          135336);
        S("Schrägband", "diagonal band",
          "breites Band, das neben dem inneren Winkel eines Auges beginnt und schräg abwärts nach hinten über die Wange läuft", "a broad band starting beside the inner corner of one eye and running diagonally down and back across the cheek",
          135337);

        // 17_Roegadyn_Sea_Wolf_female_face4
        S("Buschige Augenbrauen", "bushy eyebrows",
          "dichte, buschige Augenbrauen, die weit über die Augen reichen", "dense bushy eyebrows extending well over the eyes",
          135341);
        S("Augenfältchen", "eye lines",
          "feine Linien am äußeren Augenwinkel und eine deutliche Falte, die unter dem Auge über den Wangenknochen zieht", "fine lines at the outer corner of the eye and a distinct crease running under the eye across the cheekbone",
          135342);
        S("Nasenrückennarbe", "nose-bridge scar",
          "feine, leicht gewellte Narbe, die waagerecht über den Nasenrücken bis auf beide Wangen läuft", "a fine, slightly wavy scar running horizontally across the bridge of the nose onto both cheeks",
          135343);
        S("Wangennarbe", "cheek scar",
          "langer, feiner Schnitt über eine Wange, der vom Mundwinkel aus schräg abwärts zum Kiefer zieht", "a long fine cut across one cheek running diagonally down from the corner of the mouth to the jaw",
          135344);
        S("Leberfleck Wange", "mole on cheek",
          "kleiner runder Leberfleck auf einer Wange, ein Stück unterhalb des Auges", "a small round mole on one cheek, some way below the eye",
          135345);
        S("Wappenzeichen Stirn", "shield emblem, forehead",
          "schildförmiges Zeichen mit ausgespartem Inneren mitten auf der Stirn zwischen den Brauen", "a shield-shaped emblem with a hollow centre in the middle of the forehead between the eyebrows",
          135346);
        S("Wangenfeld", "cheek block",
          "großes, kantiges Feld, das eine Wange vom Auge bis zum Kiefer bedeckt und von zwei schrägen Streifen durchzogen wird", "a large angular field covering one cheek from the eye down to the jaw, crossed by two diagonal stripes",
          135347);

        // 18_Roegadyn_Hellsguard_male_face1
        S("Langer Kieferbart", "long jaw beard",
          "sehr dichter, langer Bart, der Wangen, Kiefer und Kinn bedeckt und weit unter das Kinn hängt; die Oberlippe bleibt frei", "a very dense long beard covering cheeks, jaw and chin and hanging far below the chin, the upper lip left bare",
          135611);
        S("Tiefe Falten", "deep wrinkles",
          "tiefe Falten im Gesicht: eine lange, waagerechte Furche quer über beide Wangen in Nasenhöhe und kräftige Linien um den Mund", "deep creases in the face: a long horizontal furrow across both cheeks at nose height and strong lines around the mouth",
          135612);
        S("Feine Schnitte", "fine cuts",
          "feine Schnitte auf beiden Seiten: einer quer über das äußere Ende der einen Braue mit einem zweiten darunter auf dem Wangenknochen, dazu zwei dünne Schnitte, die auf der anderen Seite vor dem Ohr die Wange hinablaufen", "fine cuts on both sides: one across the outer end of one eyebrow with a second below it on the cheekbone, plus two thin cuts running down the other cheek in front of the ear",
          135613);
        S("Augenhaken", "eye hook",
          "kräftiger, spitz auslaufender Haken am äußeren Winkel eines Auges, der zur Schläfe hin zurückschwingt, mit einem kleineren Zacken darunter", "a bold pointed hook at the outer corner of one eye sweeping back toward the temple, with a smaller barb beneath it",
          135614);
        S("Augenhaken", "eye hook",
          "kräftiger, spitz auslaufender Haken am äußeren Winkel eines Auges, der zur Schläfe hin zurückschwingt", "a bold pointed hook at the outer corner of one eye sweeping back toward the temple",
          135615);
        S("Band", "band",
          "schmales Band dicht unter einem Auge, das dem unteren Lid folgt und über den Wangenknochen läuft", "a narrow band close under one eye, following the lower lid and running across the cheekbone",
          135616);
        S("Band", "band",
          "schmales Band dicht unter einem Auge, das dem unteren Lid folgt und über den äußeren Augenwinkel hinaus zur Schläfe reicht", "a narrow band close under one eye, following the lower lid and reaching past the outer corner toward the temple",
          135617);

        // 18_Roegadyn_Hellsguard_male_face2
        S("Breiter Kinnbart", "broad chin beard",
          "Bart, der von den Koteletten am Kiefer entlang zu einem breiten Kinnbart mit ausgefranstem unteren Rand zusammenläuft; Wangen und Oberlippe bleiben frei", "a beard running from the sideburns along the jaw into a broad chin beard with a ragged lower edge, cheeks and upper lip left bare",
          135621);
        S("Krallenspuren", "claw marks",
          "mehrere parallele Schnitte wie von Krallen, die von der Stirn schräg über eine Braue und das Auge hinweg bis auf die Wange laufen", "several parallel cuts like claw marks running from the forehead diagonally across one brow and eye onto the cheek",
          135622);
        S("Stirnnarbe", "forehead scar",
          "langer, gerader Schnitt, der mitten über die Stirn abwärts bis auf den Nasenrücken läuft, dazu eine dünne Linie, die unter einem Auge die Wange hinabzieht", "a long straight cut running down the middle of the forehead to the bridge of the nose, plus a thin line running down the cheek below one eye",
          135623);
        S("Kreuznarbe Wange", "crossed cheek scar",
          "waagerechter Schnitt, der von einem Mundwinkel über die Wange zurückläuft und von einem kürzeren senkrechten Schnitt gekreuzt wird", "a horizontal cut running back from one corner of the mouth across the cheek, crossed by a shorter vertical cut",
          135624);
        S("Senkrechte Wangennarbe", "vertical cheek scar",
          "langer, fast senkrechter Schnitt, der von einem Wangenknochen am Mundwinkel vorbei bis zum Kiefer hinabläuft", "a long, almost vertical cut running down one cheek from the cheekbone past the corner of the mouth to the jaw",
          135625);
        S("Blattform", "leaf shape",
          "blattförmiges Feld in der Mulde unter einem Auge, am äußeren Ende breit und zur Nase hin spitz auslaufend", "a leaf-shaped field in the hollow under one eye, broad at its outer end and tapering to a point toward the nose",
          135626);
        S("Blattform", "leaf shape",
          "blattförmiges Feld unter einem Auge, am äußeren Ende breit und zur Nase hin spitz auslaufend", "a leaf-shaped field under one eye, broad at its outer end and tapering to a point toward the nose",
          135627);

        // 18_Roegadyn_Hellsguard_male_face3
        S("Sehr langer Bart", "very long beard",
          "sehr langer, dichter Bart, der von den Koteletten über Kiefer und Kinn bis weit auf die Brust hängt und spitz endet; die Oberlippe bleibt frei", "a very long dense beard running from the sideburns over jaw and chin and hanging far down onto the chest, ending in a point, the upper lip left bare",
          135631);
        S("Hängender Schnauzbart", "drooping moustache",
          "kräftiger Schnauzbart, dessen Enden beidseits an den Mundwinkeln vorbei zum Kiefer hinabhängen; das Kinn bleibt frei", "a heavy moustache whose ends hang down past both corners of the mouth toward the jaw, the chin left bare",
          135632);
        S("Lange Schrägnarbe", "long diagonal scar",
          "lange, gerade Narbe, die von der einen Braue schräg abwärts über den Nasenrücken bis auf die andere Wange läuft", "a long straight scar running from one eyebrow diagonally down across the bridge of the nose onto the other cheek",
          135633);
        S("Wangennarbe", "cheek scar",
          "langer, leicht gebogener Schnitt, der unterhalb eines Auges beginnt und schräg abwärts nach hinten zum Kieferwinkel zieht", "a long slightly curved cut starting below one eye and running diagonally down and back to the angle of the jaw",
          135634);
        S("Leberfleck Wange", "mole on cheek",
          "kleiner runder Leberfleck mitten auf einer Wange, unterhalb und seitlich des Auges", "a small round mole in the middle of one cheek, below and to the side of the eye",
          135635);
        S("Band", "band",
          "schmales Band dicht unter einem Auge, das dem unteren Lid folgt und über den Wangenknochen ausläuft", "a narrow band close under one eye, following the lower lid and fading out across the cheekbone",
          135636);
        S("Band", "band",
          "schmales Band dicht unter einem Auge, das dem unteren Lid folgt und über den Wangenknochen ausläuft", "a narrow band close under one eye, following the lower lid and fading out across the cheekbone",
          135637);

        // 18_Roegadyn_Hellsguard_male_face4
        S("Kinn- und Kieferbart", "chin and jaw beard",
          "Bart, der von den Koteletten über Kiefer und Kinn zieht und darunter in ausgefransten Strähnen hängt; die Oberlippe bleibt frei", "a beard running from the sideburns over jaw and chin and hanging below in ragged strands, the upper lip left bare",
          135641);
        S("Breiter Schnurrbart", "broad moustache",
          "breiter, waagerecht liegender Schnurrbart über der Oberlippe, der etwas über die Mundwinkel hinausreicht; das Kinn bleibt frei", "a broad moustache lying horizontally over the upper lip and reaching a little past the corners of the mouth, the chin left bare",
          135642);
        S("Stirn- und Wangennarbe", "forehead and cheek scar",
          "langer Schnitt, der vom Haaransatz über die Stirn bis durch das innere Ende einer Braue läuft, dazu ein kurzer Schnitt auf dem Wangenknochen unter dem Auge", "a long cut running from the hairline across the forehead through the inner end of one eyebrow, plus a short cut on the cheekbone below the eye",
          135643);
        S("Schräge Wangennarbe", "diagonal cheek scar",
          "langer, gerader Schnitt, der von der Schläfe schräg nach vorn und unten über eine Wange bis zum Kiefer läuft", "a long straight cut running from the temple diagonally forward and down across one cheek to the jaw",
          135644);
        S("Senkrechte Wangennarbe", "vertical cheek scar",
          "langer, feiner Schnitt, der unterhalb des äußeren Winkels eines Auges beginnt und fast senkrecht bis zum Kiefer hinabläuft", "a long fine cut starting below the outer corner of one eye and running almost straight down to the jawline",
          135645);
        S("Drei Brauenstriche", "three brow bars",
          "drei kurze, waagerechte Balken übereinander über der äußeren Hälfte einer Braue", "three short horizontal bars stacked above the outer half of one eyebrow",
          135646);
        S("Augenschwung", "eye sweep",
          "breites Band unter einem Auge, das zur Nase hin spitz ausläuft und am äußeren Ende hakenförmig zur Wange herabgezogen ist", "a broad band under one eye tapering to a point toward the nose and hooked down onto the cheek at its outer end",
          135647);

        // 19_Roegadyn_Hellsguard_female_face1
        S("Schmale Augenbrauen", "narrow eyebrows",
          "schmale, scharf gezeichnete Augenbrauen mit deutlichem Bogen über beiden Augen", "narrow, sharply drawn eyebrows with a distinct arch over both eyes",
          135811);
        S("Narbe an einer Braue", "scar on one brow",
          "lange, leicht gezackte Narbe, die über einer Braue beginnt und schräg abwärts über den Nasenrücken läuft", "a long slightly jagged scar starting above one eyebrow and running diagonally down across the bridge of the nose",
          135812);
        S("Narbe an einer Braue", "scar on one brow",
          "lange, leicht gezackte Narbe, die über einer Braue beginnt und schräg abwärts über den Nasenrücken läuft", "a long slightly jagged scar starting above one eyebrow and running diagonally down across the bridge of the nose",
          135813);
        S("Leberfleck Wange", "mole on cheek",
          "kleiner runder Leberfleck auf einer Wange, unterhalb des Auges", "a small round mole on one cheek, below the eye",
          135814);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck knapp neben und etwas unter einem Mundwinkel", "a small round mole just beside and a little below one corner of the mouth",
          135815);
        S("Breites Wangenband", "broad cheek band",
          "breites Band, das unter beiden Augen über die Wangenknochen und quer über den Nasenrücken läuft und eine gestufte untere Kante hat", "a broad band running under both eyes across the cheekbones and straight over the bridge of the nose, with a stepped lower edge",
          135816);
        S("Stirnzeichen", "forehead emblem",
          "kleines, blattförmig zugespitztes Zeichen mitten auf der Stirn zwischen den Brauen", "a small leaf-shaped pointed emblem in the middle of the forehead between the eyebrows",
          135817);

        // 19_Roegadyn_Hellsguard_female_face2
        S("Schmale Augenbrauen", "narrow eyebrows",
          "schmale, scharf gezeichnete Augenbrauen, die zum äußeren Ende hin spitz zulaufen", "narrow, sharply drawn eyebrows tapering to a point toward the outer end",
          135821);
        S("Augenfältchen", "eye lines",
          "feine Linien, die sich vom äußeren Augenwinkel auffächern, dazu eine Falte unter dem unteren Lid", "fine lines fanning out from the outer corner of the eye, plus a crease under the lower lid",
          135822);
        S("Lange Narbe", "long scar",
          "lange, feine Narbe, die auf einer Gesichtshälfte vom Haaransatz senkrecht über Braue und Auge hinweg bis auf die Wange läuft", "a long fine scar running vertically down one side of the face from the hairline over the brow and eye onto the cheek",
          135823);
        S("Lange Narbe", "long scar",
          "lange, feine Narbe, die auf einer Gesichtshälfte vom Haaransatz senkrecht über Braue und Auge hinweg bis auf die Wange läuft", "a long fine scar running vertically down one side of the face from the hairline over the brow and eye onto the cheek",
          135824);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck dicht neben einem Mundwinkel", "a small round mole close beside one corner of the mouth",
          135825);
        S("Wappenzeichen Stirn", "shield emblem, forehead",
          "schildförmiges Zeichen mit ausgespartem Inneren und kleiner Spitze oben, mitten auf der Stirn zwischen den Brauen", "a shield-shaped emblem with a hollow centre and a small spike on top, in the middle of the forehead between the eyebrows",
          135826);
        S("Band und Brauenfleck", "band and brow patch",
          "breites Band, das unter beiden Augen über den Nasenrücken läuft, dazu ein kantiger Fleck über dem äußeren Ende einer Braue bis zur Schläfe", "a broad band running under both eyes across the bridge of the nose, plus an angular patch over the outer end of one eyebrow reaching to the temple",
          135827);

        // 19_Roegadyn_Hellsguard_female_face3
        S("Buschige Augenbrauen", "bushy eyebrows",
          "dichte, buschige Augenbrauen mit deutlich sichtbaren Härchen über beiden Augen", "dense bushy eyebrows with clearly visible hairs over both eyes",
          135831);
        S("Schräge Stirnnarbe", "diagonal forehead scar",
          "lange, gerade Narbe, die schräg über die Stirn von oberhalb der einen Braue bis an die andere Braue läuft", "a long straight scar running diagonally across the forehead from above one eyebrow down to the other eyebrow",
          135832);
        S("Wangennarbe", "cheek scar",
          "langer, feiner Schnitt, der von einem Wangenknochen schräg abwärts nach hinten zum Kiefer läuft", "a long fine cut running from one cheekbone diagonally down and back toward the jaw",
          135833);
        S("Leberfleck am Auge", "mole by the eye",
          "kleiner runder Leberfleck knapp unterhalb und außerhalb des äußeren Winkels eines Auges", "a small round mole just below and outside the outer corner of one eye",
          135834);
        S("Leberfleck am Mund", "mole by the mouth",
          "kleiner runder Leberfleck auf einer Wange, dicht neben dem Mundwinkel", "a small round mole on one cheek, close beside the corner of the mouth",
          135835);
        S("Balken", "bar",
          "schmaler Balken dicht unter einem Auge, der dem unteren Lid folgt und nach hinten zur Schläfe ausläuft, ohne die Nase zu erreichen", "a narrow bar close under one eye, following the lower lid and running back toward the temple without reaching the nose",
          135836);
        S("Breites Nasenband", "broad nose band",
          "breiteres Band, das tiefer auf einer Wange sitzt und nach vorn über den Nasenrücken auf die andere Gesichtshälfte hinüberreicht", "a broader band set lower on one cheek, running forward over the bridge of the nose onto the other side of the face",
          135837);

        // 19_Roegadyn_Hellsguard_female_face4
        S("Kräftige Augenbrauen", "strong eyebrows",
          "dichte, scharf gezeichnete Augenbrauen, die sich über beide Augen wölben und außen spitz zulaufen", "dense, sharply drawn eyebrows arching over both eyes and tapering to a point at the outer end",
          135841);
        S("Augenfältchen", "eye lines",
          "feine Linien, die vom äußeren Augenwinkel zur Schläfe hin auffächern, dazu eine deutliche Falte unter dem Auge", "fine lines fanning from the outer corner of the eye toward the temple, plus a distinct crease under the eye",
          135842);
        S("Nasenrückennarbe", "nose-bridge scar",
          "lange, feine Narbe, die waagerecht über den Nasenrücken bis auf beide Wangen reicht", "a long fine scar running horizontally across the bridge of the nose onto both cheeks",
          135843);
        S("Wangennarbe", "cheek scar",
          "langer, feiner Schnitt, der von einem Wangenknochen schräg abwärts nach hinten zum Kieferwinkel läuft", "a long fine cut running from one cheekbone diagonally down and back to the angle of the jaw",
          135844);
        S("Leberfleck am Auge", "mole by the eye",
          "kleiner runder Leberfleck unterhalb und außerhalb des äußeren Winkels eines Auges", "a small round mole below and outside the outer corner of one eye",
          135845);
        S("Wappenzeichen Stirn", "shield emblem, forehead",
          "schildförmiges Zeichen mit ausgespartem Inneren mitten auf der Stirn zwischen den Brauen", "a shield-shaped emblem with a hollow centre in the middle of the forehead between the eyebrows",
          135846);
        S("Wangenfeld", "cheek block",
          "großes, kantiges Feld, das eine Wange vom Auge bis zum Kiefer bedeckt und von zwei schrägen Streifen durchzogen wird", "a large angular field covering one cheek from the eye down to the jaw, crossed by two diagonal stripes",
          135847);

        // ---- feat-viera.cs ----
        // Viera — CharaMakeType.FacialFeatureOption, 16 sheets x 7 slots = 112 icons.
        // Slots 1-5 = the 5-entry menu, slots 6-7 = the 2-entry (tattoo) menu.
        // Icon ids copied from the cell labels and cross-checked against
        // tools\icons\idx-Facial_Features.tsv.
        // The Veena artwork (1386xx / 1388xx) is the same design set as the Rava
        // artwork (1381xx / 1383xx) rendered on a lighter skin — verified sheet by
        // sheet, not assumed. Descriptions are therefore identical per face/slot.

        // 28_Viera_Rava_male_face1
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138111);
        S("lange Wimpern", "long lashes",
          "lange, spitz auslaufende Wimpern an Ober- und Unterlid, die über den äußeren Augenwinkel hinaus abstehen", "long, spiky lashes on the upper and lower lid, standing out past the outer corner of the eye",
          138112);
        S("Schattierung, Nase", "shading on the nose",
          "breite dunkle Schattierung, die von zwischen den Augen den Nasenrücken hinab und über die Nasenflügel läuft", "broad dark shading running from between the eyes down the bridge of the nose and over the nostrils",
          138113);
        S("Sommersprossen", "freckles",
          "locker gestreute Sommersprossen über beide Wangen und quer über den Nasenrücken", "loosely scattered freckles over both cheeks and across the bridge of the nose",
          138114);
        S("Narbe, senkrecht", "vertical scar",
          "schmale Narbe, die vom Haaransatz senkrecht durch Braue und Auge bis auf die Wange hinunterläuft", "narrow scar running vertically from the hairline through the eyebrow and the eye down onto the cheek",
          138115);
        S("Zeichnung, Augenring", "marking ringing the eye",
          "dunkle, weich verlaufende Zeichnung, die das Auge umschließt und am äußeren Winkel breit ausläuft", "dark, softly blurred marking enclosing the eye and running out broadly at the outer corner",
          138116);
        S("Federzeichnung, Wange", "feather marking on the cheek",
          "große gefiederte Zeichnung mit gezackten Fahnen, die vom Ohr schräg nach vorn unten über die Wange läuft", "large feathered marking with jagged fronds running from the ear diagonally down and forward across the cheek",
          138117);

        // 28_Viera_Rava_male_face2
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138121);
        S("Wimpern, oben und unten", "lashes above and below",
          "feine Wimpern an Ober- und Unterlid, wobei die untere Wimpernreihe deutlich abgesetzt ist", "fine lashes on the upper and lower lid, with the lower lash line clearly set off",
          138122);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung über dem Oberlid, die zum äußeren Augenwinkel hin breiter wird und darüber hinausreicht", "dark shading over the upper lid, widening toward the outer corner of the eye and reaching beyond it",
          138123);
        S("kurzer Kinnbart", "short chin beard",
          "kurzer, flach über das Kinn gestreuter Bartfleck unterhalb der Unterlippe, ohne Verbindung zum Kiefer", "short beard patch spread flat over the chin below the lower lip, not reaching the jaw",
          138124);
        S("Sommersprossen", "freckles",
          "dichtes Sommersprossenband dicht unter den Augen quer über Wangenknochen und Nasenrücken", "dense band of freckles just under the eyes, across the cheekbones and the bridge of the nose",
          138125);
        S("Dreieckszeichnung, Wange", "triangle marking on the cheek",
          "geometrische Linienzeichnung unter dem Auge: eine Punktreihe über zwei großen Dreiecken, darunter ein kleines Dreieck und eine lange, nach unten zeigende Spitze bis zum Kiefer", "geometric line design below the eye: a row of dots above two large triangles, then a small triangle and a long downward point reaching the jaw",
          138126);
        S("Zeichnung, vor dem Ohr", "marking in front of the ear",
          "schlanke senkrechte Zeichnung vor dem Ohr: ein perlenbesetzter Schaft mit zwei aufwärts gebogenen Klingen, der unterhalb des Kiefers in einer langen Spitze endet", "slim vertical design in front of the ear: a beaded shaft with two upward-curving blades, ending in a long point below the jaw",
          138127);

        // 28_Viera_Rava_male_face3
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138131);
        S("dichte lange Wimpern", "dense long lashes",
          "dichte, lange Wimpern an Ober- und Unterlid, am äußeren Augenwinkel am längsten", "dense, long lashes on the upper and lower lid, longest at the outer corner of the eye",
          138132);
        S("Muttermal, Wangenknochen", "mole on the cheekbone",
          "einzelnes rundes Muttermal auf dem Wangenknochen, etwa eine Augenbreite unter dem äußeren Augenwinkel", "single round mole on the cheekbone, about an eye's width below the outer corner of the eye",
          138133);
        S("spitzer Kinnbart", "pointed chin beard",
          "schmaler Kinnbart, der von der Unterlippe über das Kinn läuft und in einer langen Spitze unter den Kiefer hängt", "narrow chin beard running from the lower lip over the chin and hanging in a long point below the jaw",
          138134);
        S("Narbe, Wange", "scar on the cheek",
          "kurze senkrechte Narbe, die dicht unter dem Auge beginnt und gerade über die Wange hinabläuft", "short vertical scar starting just below the eye and running straight down the cheek",
          138135);
        S("Keile, Augenwinkel", "wedges at the eye corners",
          "je ein spitz zulaufender Keil an beiden Augenwinkeln, der äußere nach außen oben, der innere zur Nase hin gerichtet", "a tapering wedge at each corner of the eye, the outer one pointing outward and up, the inner one toward the nose",
          138136);
        S("Rankenzeichnung, Wange", "interlaced marking on the cheek",
          "schmale, ineinander verflochtene Bänder mit spitzen Dornen, die von der Schläfe die Wange hinab bis zum Kiefer laufen", "slim interwoven bands with pointed barbs running from the temple down the cheek to the jaw",
          138137);

        // 28_Viera_Rava_male_face4
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138141);
        S("lange Wimpern", "long lashes",
          "lange Wimpern am Oberlid und eine durchgehende Wimpernreihe am Unterlid", "long lashes on the upper lid and an unbroken lash line on the lower lid",
          138142);
        S("Haarsträhne, Wange", "lock of hair on the cheek",
          "geschwungene Haarsträhne vor dem Ohr, die nach vorn über die Wange läuft und spitz ausläuft", "curved lock of hair in front of the ear, sweeping forward across the cheek to a point",
          138143);
        S("Schnurrbart", "moustache",
          "Schnurrbart über der Oberlippe, dessen Enden über die Mundwinkel hinaus nach außen abstehen", "moustache over the upper lip, its ends standing out past the corners of the mouth",
          138144);
        S("Schnurr- und Kinnbart", "moustache and chin beard",
          "Schnurrbart, der um die Mundwinkel herum in einen Kinnbart übergeht und unter dem Kinn spitz zuläuft", "moustache curving around the corners of the mouth into a chin beard that tapers to a point below the chin",
          138145);
        S("Zeichnung, Augenschatten", "shadow marking around the eye",
          "weiche dunkle Zeichnung, die unter dem Auge und am äußeren Winkel am stärksten ist und nach außen verläuft", "soft dark marking, strongest below the eye and at the outer corner, fading outward",
          138146);
        S("Stirn- und Wangenzeichnung", "forehead and cheek marking",
          "gefüllte Zeichnung: eine Symbolkette von der Stirnmitte den Nasenrücken hinab, dazu unter jedem Auge eine Reihe nach unten zeigender Zacken über dem Wangenknochen", "filled design: a chain of symbols from the middle of the forehead down the bridge of the nose, plus a row of downward-pointing spikes under each eye across the cheekbone",
          138147);

        // 29_Viera_Rava_female_face1
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138311);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138312);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung über dem Oberlid, die am äußeren Augenwinkel in eine weiche Fahne ausläuft", "dark shading over the upper lid, running out into a soft plume at the outer corner of the eye",
          138313);
        S("lange Wimpern", "long lashes",
          "lange, dicht stehende Wimpern, die an Ober- und Unterlid weit vom Lid abstehen", "long, densely set lashes standing well out from both the upper and the lower lid",
          138314);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, seitlich unterhalb des äußeren Augenwinkels", "single round mole on the cheek, out from and below the outer corner of the eye",
          138315);
        S("Striche, Wange", "strokes on the cheek",
          "unter jedem Auge zwei senkrechte, nach unten spitz zulaufende Striche, ein kurzer innen und ein langer daneben", "two vertical strokes tapering downward under each eye, a short one inside and a long one beside it",
          138316);
        S("Raute, Nasenrücken", "diamond on the bridge of the nose",
          "schmale Raute mittig zwischen den Augen, darunter ein nach unten zeigender Winkel auf dem Nasenrücken", "slim diamond centrally between the eyes, with a downward-pointing chevron below it on the bridge of the nose",
          138317);

        // 29_Viera_Rava_female_face2
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138321);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138322);
        S("Lidschatten, breit", "broad lid shading",
          "weiche dunkle Schattierung über dem ganzen Oberlid, die bis zum Brauenknochen hinaufreicht und über den äußeren Winkel hinausläuft", "soft dark shading over the whole upper lid, reaching up to the brow bone and out past the outer corner",
          138323);
        S("sehr lange Wimpern", "very long lashes",
          "sehr lange, spitz abstehende Wimpern an Ober- und Unterlid, die nach außen auffächern", "very long, spiky lashes on the upper and lower lid, fanning outward",
          138324);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, seitlich und etwas unterhalb des Mundwinkels", "single round mole on the cheek, out from and a little below the corner of the mouth",
          138325);
        S("Bogen und Punkte", "arc and dots",
          "breiter Bogen, der jedem Unterlid folgt und am äußeren Ende aufwärts schwingt, darunter drei Punkte auf dem Wangenknochen", "broad arc following each lower lid and curving up at the outer end, with three dots below it on the cheekbone",
          138326);
        S("Haken, Wange", "hooks on the cheek",
          "beidseits der Nase ein breiter gebogener Keil unter dem inneren Augenwinkel, der nach außen unten in eine feine Spitze ausläuft", "on each side of the nose a broad curved wedge below the inner corner of the eye, tapering to a fine point down and outward",
          138327);

        // 29_Viera_Rava_female_face3
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138331);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138332);
        S("Schattierung, rund ums Auge", "shading all around the eye",
          "weiche Schattierung, die das Auge oben und unten umgibt und breit über die Lidfalte hinausreicht", "soft shading surrounding the eye above and below, spreading broadly beyond the lid crease",
          138333);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung nur auf dem Oberlid, die vom inneren Winkel der Lidfalte folgt und über den äußeren Winkel hinausreicht", "dark shading confined to the upper lid, following the crease from the inner corner and reaching past the outer corner",
          138334);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, unterhalb des Mundwinkels in Richtung Kiefer", "single round mole on the cheek, below the corner of the mouth toward the jaw",
          138335);
        S("Rankenzeichnung, Wange", "scrollwork on the cheek",
          "verschnörkelte Ranke unter jedem Auge, die mit eingerollten Haken von der Nase aus quer über den Wangenknochen läuft", "curling scrollwork under each eye, running with hooked tendrils from the nose across the cheekbone",
          138336);
        S("Lilienzeichnung, Nasenrücken", "lily marking on the bridge of the nose",
          "lilienförmiges Ornament mittig zwischen den Augen: eine senkrechte Spitze mit zwei nach außen gebogenen Armen über einem Querband", "lily-shaped ornament centrally between the eyes: an upright spike with two outward-curving arms above a crossbar",
          138337);

        // 29_Viera_Rava_female_face4
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138341);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138342);
        S("Wimpern und Lidschatten", "lashes and lid shading",
          "dichte Wimpern mit weicher dunkler Schattierung auf dem Oberlid, die über den äußeren Augenwinkel hinaus verläuft", "dense lashes with soft dark shading on the upper lid, fading out past the outer corner of the eye",
          138343);
        S("ausgezogener Lidstrich", "drawn-out lid line",
          "Lidstrich, der über den äußeren Augenwinkel hinaus in eine lange scharfe Spitze ausgezogen ist, mit dichter unterer Wimpernreihe", "lid line drawn out past the outer corner of the eye into a long sharp point, with a dense lower lash line",
          138344);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange neben dem Nasenflügel, oberhalb des Mundwinkels", "single round mole on the cheek beside the nostril, above the corner of the mouth",
          138345);
        S("drei Striche, Wange", "three strokes on the cheek",
          "drei breite Striche auf jeder Wange, die vom äußeren Rand her zur Nase hin spitz zulaufen", "three broad strokes on each cheek, tapering from the outer edge toward the nose",
          138346);
        S("Rautenkette, Nasenrücken", "chain of diamonds on the nose",
          "senkrechte Kette aus Rauten von zwischen den Brauen den Nasenrücken hinab, dazu ein kleines Ornament mit Abwärtsspitze auf dem Kinn", "vertical chain of diamonds running from between the brows down the bridge of the nose, plus a small ornament with a downward point on the chin",
          138347);

        // 30_Viera_Veena_male_face1
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138611);
        S("lange Wimpern", "long lashes",
          "lange, spitz auslaufende Wimpern an Ober- und Unterlid, die über den äußeren Augenwinkel hinaus abstehen", "long, spiky lashes on the upper and lower lid, standing out past the outer corner of the eye",
          138612);
        S("Schattierung, Nase", "shading on the nose",
          "breite dunkle Schattierung, die von zwischen den Augen den Nasenrücken hinab und über die Nasenflügel läuft", "broad dark shading running from between the eyes down the bridge of the nose and over the nostrils",
          138613);
        S("Sommersprossen", "freckles",
          "locker gestreute Sommersprossen über beide Wangen und quer über den Nasenrücken", "loosely scattered freckles over both cheeks and across the bridge of the nose",
          138614);
        S("Narbe, senkrecht", "vertical scar",
          "schmale Narbe, die vom Haaransatz senkrecht durch Braue und Auge bis auf die Wange hinunterläuft", "narrow scar running vertically from the hairline through the eyebrow and the eye down onto the cheek",
          138615);
        S("Zeichnung, Augenring", "marking ringing the eye",
          "dunkle, weich verlaufende Zeichnung, die das Auge umschließt und am äußeren Winkel breit ausläuft", "dark, softly blurred marking enclosing the eye and running out broadly at the outer corner",
          138616);
        S("Federzeichnung, Wange", "feather marking on the cheek",
          "große gefiederte Zeichnung mit gezackten Fahnen, die vom Ohr schräg nach vorn unten über die Wange läuft", "large feathered marking with jagged fronds running from the ear diagonally down and forward across the cheek",
          138617);

        // 30_Viera_Veena_male_face2
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138621);
        S("Wimpern, oben und unten", "lashes above and below",
          "feine Wimpern an Ober- und Unterlid, wobei die untere Wimpernreihe deutlich abgesetzt ist", "fine lashes on the upper and lower lid, with the lower lash line clearly set off",
          138622);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung über dem Oberlid, die zum äußeren Augenwinkel hin breiter wird und darüber hinausreicht", "dark shading over the upper lid, widening toward the outer corner of the eye and reaching beyond it",
          138623);
        S("kurzer Kinnbart", "short chin beard",
          "kurzer, flach über das Kinn gestreuter Bartfleck unterhalb der Unterlippe, ohne Verbindung zum Kiefer", "short beard patch spread flat over the chin below the lower lip, not reaching the jaw",
          138624);
        S("Sommersprossen", "freckles",
          "dichtes Sommersprossenband dicht unter den Augen quer über Wangenknochen und Nasenrücken", "dense band of freckles just under the eyes, across the cheekbones and the bridge of the nose",
          138625);
        S("Dreieckszeichnung, Wange", "triangle marking on the cheek",
          "geometrische Linienzeichnung unter dem Auge: eine Punktreihe über zwei großen Dreiecken, darunter ein kleines Dreieck und eine lange, nach unten zeigende Spitze bis zum Kiefer", "geometric line design below the eye: a row of dots above two large triangles, then a small triangle and a long downward point reaching the jaw",
          138626);
        S("Zeichnung, vor dem Ohr", "marking in front of the ear",
          "schlanke senkrechte Zeichnung vor dem Ohr: ein perlenbesetzter Schaft mit zwei aufwärts gebogenen Klingen, der unterhalb des Kiefers in einer langen Spitze endet", "slim vertical design in front of the ear: a beaded shaft with two upward-curving blades, ending in a long point below the jaw",
          138627);

        // 30_Viera_Veena_male_face3
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138631);
        S("dichte lange Wimpern", "dense long lashes",
          "dichte, lange Wimpern an Ober- und Unterlid, am äußeren Augenwinkel am längsten", "dense, long lashes on the upper and lower lid, longest at the outer corner of the eye",
          138632);
        S("Muttermal, Wangenknochen", "mole on the cheekbone",
          "einzelnes rundes Muttermal auf dem Wangenknochen, etwa eine Augenbreite unter dem äußeren Augenwinkel", "single round mole on the cheekbone, about an eye's width below the outer corner of the eye",
          138633);
        S("spitzer Kinnbart", "pointed chin beard",
          "schmaler Kinnbart, der von der Unterlippe über das Kinn läuft und in einer langen Spitze unter den Kiefer hängt", "narrow chin beard running from the lower lip over the chin and hanging in a long point below the jaw",
          138634);
        S("Narbe, Wange", "scar on the cheek",
          "kurze senkrechte Narbe, die dicht unter dem Auge beginnt und gerade über die Wange hinabläuft", "short vertical scar starting just below the eye and running straight down the cheek",
          138635);
        S("Keile, Augenwinkel", "wedges at the eye corners",
          "je ein spitz zulaufender Keil an beiden Augenwinkeln, der äußere nach außen oben, der innere zur Nase hin gerichtet", "a tapering wedge at each corner of the eye, the outer one pointing outward and up, the inner one toward the nose",
          138636);
        S("Rankenzeichnung, Wange", "interlaced marking on the cheek",
          "schmale, ineinander verflochtene Bänder mit spitzen Dornen, die von der Schläfe die Wange hinab bis zum Kiefer laufen", "slim interwoven bands with pointed barbs running from the temple down the cheek to the jaw",
          138637);

        // 30_Viera_Veena_male_face4
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138641);
        S("lange Wimpern", "long lashes",
          "lange Wimpern am Oberlid und eine durchgehende Wimpernreihe am Unterlid", "long lashes on the upper lid and an unbroken lash line on the lower lid",
          138642);
        S("Haarsträhne, Wange", "lock of hair on the cheek",
          "geschwungene Haarsträhne vor dem Ohr, die nach vorn über die Wange läuft und spitz ausläuft", "curved lock of hair in front of the ear, sweeping forward across the cheek to a point",
          138643);
        S("Schnurrbart", "moustache",
          "Schnurrbart über der Oberlippe, dessen Enden über die Mundwinkel hinaus nach außen abstehen", "moustache over the upper lip, its ends standing out past the corners of the mouth",
          138644);
        S("Schnurr- und Kinnbart", "moustache and chin beard",
          "Schnurrbart, der um die Mundwinkel herum in einen Kinnbart übergeht und unter dem Kinn spitz zuläuft", "moustache curving around the corners of the mouth into a chin beard that tapers to a point below the chin",
          138645);
        S("Zeichnung, Augenschatten", "shadow marking around the eye",
          "weiche dunkle Zeichnung, die unter dem Auge und am äußeren Winkel am stärksten ist und nach außen verläuft", "soft dark marking, strongest below the eye and at the outer corner, fading outward",
          138646);
        S("Stirn- und Wangenzeichnung", "forehead and cheek marking",
          "gefüllte Zeichnung: eine Symbolkette von der Stirnmitte den Nasenrücken hinab, dazu unter jedem Auge eine Reihe nach unten zeigender Zacken über dem Wangenknochen", "filled design: a chain of symbols from the middle of the forehead down the bridge of the nose, plus a row of downward-pointing spikes under each eye across the cheekbone",
          138647);

        // 31_Viera_Veena_female_face1
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138811);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138812);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung über dem Oberlid, die am äußeren Augenwinkel in eine weiche Fahne ausläuft", "dark shading over the upper lid, running out into a soft plume at the outer corner of the eye",
          138813);
        S("lange Wimpern", "long lashes",
          "lange, dicht stehende Wimpern, die an Ober- und Unterlid weit vom Lid abstehen", "long, densely set lashes standing well out from both the upper and the lower lid",
          138814);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, seitlich unterhalb des äußeren Augenwinkels", "single round mole on the cheek, out from and below the outer corner of the eye",
          138815);
        S("Striche, Wange", "strokes on the cheek",
          "unter jedem Auge zwei senkrechte, nach unten spitz zulaufende Striche, ein kurzer innen und ein langer daneben", "two vertical strokes tapering downward under each eye, a short one inside and a long one beside it",
          138816);
        S("Raute, Nasenrücken", "diamond on the bridge of the nose",
          "schmale Raute mittig zwischen den Augen, darunter ein nach unten zeigender Winkel auf dem Nasenrücken", "slim diamond centrally between the eyes, with a downward-pointing chevron below it on the bridge of the nose",
          138817);

        // 31_Viera_Veena_female_face2
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138821);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138822);
        S("Lidschatten, breit", "broad lid shading",
          "weiche dunkle Schattierung über dem ganzen Oberlid, die bis zum Brauenknochen hinaufreicht und über den äußeren Winkel hinausläuft", "soft dark shading over the whole upper lid, reaching up to the brow bone and out past the outer corner",
          138823);
        S("sehr lange Wimpern", "very long lashes",
          "sehr lange, spitz abstehende Wimpern an Ober- und Unterlid, die nach außen auffächern", "very long, spiky lashes on the upper and lower lid, fanning outward",
          138824);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, seitlich und etwas unterhalb des Mundwinkels", "single round mole on the cheek, out from and a little below the corner of the mouth",
          138825);
        S("Bogen und Punkte", "arc and dots",
          "breiter Bogen, der jedem Unterlid folgt und am äußeren Ende aufwärts schwingt, darunter drei Punkte auf dem Wangenknochen", "broad arc following each lower lid and curving up at the outer end, with three dots below it on the cheekbone",
          138826);
        S("Haken, Wange", "hooks on the cheek",
          "beidseits der Nase ein breiter gebogener Keil unter dem inneren Augenwinkel, der nach außen unten in eine feine Spitze ausläuft", "on each side of the nose a broad curved wedge below the inner corner of the eye, tapering to a fine point down and outward",
          138827);

        // 31_Viera_Veena_female_face3
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138831);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138832);
        S("Schattierung, rund ums Auge", "shading all around the eye",
          "weiche Schattierung, die das Auge oben und unten umgibt und breit über die Lidfalte hinausreicht", "soft shading surrounding the eye above and below, spreading broadly beyond the lid crease",
          138833);
        S("Lidschatten, Oberlid", "shading on the upper lid",
          "dunkle Schattierung nur auf dem Oberlid, die vom inneren Winkel der Lidfalte folgt und über den äußeren Winkel hinausreicht", "dark shading confined to the upper lid, following the crease from the inner corner and reaching past the outer corner",
          138834);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange, unterhalb des Mundwinkels in Richtung Kiefer", "single round mole on the cheek, below the corner of the mouth toward the jaw",
          138835);
        S("Rankenzeichnung, Wange", "scrollwork on the cheek",
          "verschnörkelte Ranke unter jedem Auge, die mit eingerollten Haken von der Nase aus quer über den Wangenknochen läuft", "curling scrollwork under each eye, running with hooked tendrils from the nose across the cheekbone",
          138836);
        S("Lilienzeichnung, Nasenrücken", "lily marking on the bridge of the nose",
          "lilienförmiges Ornament mittig zwischen den Augen: eine senkrechte Spitze mit zwei nach außen gebogenen Armen über einem Querband", "lily-shaped ornament centrally between the eyes: an upright spike with two outward-curving arms above a crossbar",
          138837);

        // 31_Viera_Veena_female_face4
        S("Schattierung, Augenpartie", "shading around the eye",
          "weiche dunkle Schattierung rund um das Auge, die über den Brauenknochen bis zur Schläfe reicht", "soft dark shading around the eye, reaching over the brow bone to the temple",
          138841);
        S("Edelstein, Stirn", "gem on the forehead",
          "rautenförmiger Edelstein, mittig zwischen den Brauen in die Stirn eingesetzt", "diamond-shaped gem set into the forehead centrally between the brows",
          138842);
        S("Wimpern und Lidschatten", "lashes and lid shading",
          "dichte Wimpern mit weicher dunkler Schattierung auf dem Oberlid, die über den äußeren Augenwinkel hinaus verläuft", "dense lashes with soft dark shading on the upper lid, fading out past the outer corner of the eye",
          138843);
        S("ausgezogener Lidstrich", "drawn-out lid line",
          "Lidstrich, der über den äußeren Augenwinkel hinaus in eine lange scharfe Spitze ausgezogen ist, mit dichter unterer Wimpernreihe", "lid line drawn out past the outer corner of the eye into a long sharp point, with a dense lower lash line",
          138844);
        S("Muttermal, Wange", "mole on the cheek",
          "einzelnes rundes Muttermal auf der Wange neben dem Nasenflügel, oberhalb des Mundwinkels", "single round mole on the cheek beside the nostril, above the corner of the mouth",
          138845);
        S("drei Striche, Wange", "three strokes on the cheek",
          "drei breite Striche auf jeder Wange, die vom äußeren Rand her zur Nase hin spitz zulaufen", "three broad strokes on each cheek, tapering from the outer edge toward the nose",
          138846);
        S("Rautenkette, Nasenrücken", "chain of diamonds on the nose",
          "senkrechte Kette aus Rauten von zwischen den Brauen den Nasenrücken hinab, dazu ein kleines Ornament mit Abwärtsspitze auf dem Kinn", "vertical chain of diamonds running from between the brows down the bridge of the nose, plus a small ornament with a downward point on the chin",
          138847);
        // ── TYPE-4 / Gesichtsmerkmale - END ─────────────────────────────────────
    }
}
