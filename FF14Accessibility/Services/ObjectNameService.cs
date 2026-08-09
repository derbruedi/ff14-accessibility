using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FF14Accessibility.Services;

/// <summary>
/// One place that answers "what is this object called". Before this existed the
/// question was answered four times over (browser, target-change announcement,
/// auto-walk, follow) and each answer differed - the browser resolved gathering
/// nodes properly while the auto-walk said "Unbenannt" for the very same pick
/// (user report 2026-08-08).
///
/// Two things the raw <c>GameObject.Name</c> gets wrong:
///
/// 1. EMPTY for a whole class of objects. Extras, scenery and invisible trigger
///    spots carry no name at all (log 2026-08-06: 25 of 38 nearby EventNpc, all
///    of them untargetable).
/// 2. PRESENT BUT UNSPEAKABLE. EObj 2004123 is literally named "?" in the game
///    data - a screen reader announces nothing usable for it, yet the string is
///    non-empty so every emptiness check waves it through (log 2026-08-06
///    19:49: "?, Objekt, 55 Meter"). Hence <see cref="IsSpeakable"/> asks for a
///    letter or a digit rather than for "not whitespace".
///
/// Where the object itself is silent the game's own sheets are asked, keyed by
/// the object's BaseId. Both links are verified, not assumed:
///  - EventNpc -> ENpcResident: the same lookup NavigationService.NpcPrefix has
///    used for NPC titles all along. Cross-check against a live log
///    (2026-08-08, offline sheet dump): Robyn, Hasthwab, Muriel, Sekka,
///    Gelbjacken-Wache resolved to exactly the name the game spoke.
///  - EventObj -> EObjName: the sheet has the same row count as EObj (15710),
///    and row 2004123 reads "?" - which is precisely the name the game showed
///    live for the object with that BaseId. The row ids line up.
///
/// NOT resolved here, because no source backs the link: BattleNpc (its BaseId
/// addresses BNpcBase, and the name lives in BNpcName under a different id) and
/// Treasure. Those keep the honest fallback instead of a guessed name.
/// </summary>
public sealed class ObjectNameService
{
    private readonly IDataManager _data;

    // BaseId + kind -> sheet name ("" = sheet is silent too). Sheets are static
    // per game version, so misses are worth caching: a crowded zone asks the
    // same ids every frame the browser is used.
    private readonly Dictionary<(uint BaseId, ObjectKind Kind), string> _cache = new();

    // BaseId -> quest / warp binding. Cached the same way and for the same
    // reason; the quest STATUS is never cached, only the binding, because the
    // player accepts and finishes quests while the game runs.
    private readonly Dictionary<uint, ObjectPurpose> _purposes = new();

    public ObjectNameService(IDataManager data) => _data = data;

    /// <summary>
    /// True when a string carries something a screen reader can actually read
    /// out: at least one letter or digit after sanitizing. Punctuation-only
    /// names ("?"), icon glyphs and zero-width filler all count as no name.
    /// </summary>
    public static bool IsSpeakable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = TolkService.Sanitize(text);
        foreach (var c in cleaned)
            if (char.IsLetterOrDigit(c)) return true;
        return false;
    }

    /// <summary>
    /// The object's speakable name, or <c>null</c> when neither the object nor
    /// the game's sheets know one. Callers decide what to say instead - see
    /// <see cref="Describe"/> for the standard wording.
    /// </summary>
    public string? Resolve(IGameObject obj)
    {
        var raw = obj.Name.TextValue;
        if (IsSpeakable(raw)) return raw;

        var fromSheet = FromSheet(obj.BaseId, obj.ObjectKind);
        return IsSpeakable(fromSheet) ? fromSheet : null;
    }

    /// <summary>
    /// The object's name, or an honest stand-in naming its kind ("Objekt ohne
    /// Namen"). Used everywhere a name has to be spoken no matter what - the
    /// target-change announcement, auto-walk and follow all reach an object the
    /// player picked with the GAME's keys, where staying silent would leave
    /// them without any feedback at all.
    /// </summary>
    public string Describe(IGameObject obj)
        => (Resolve(obj) ?? AccessibilityStrings.UnnamedOfKind(obj.ObjectKind)) + Qualifier(obj);

    /// <summary>
    /// What an object is FOR, appended to its name: " für Narben im Wald" for a
    /// quest prop, " nach Neu-Gridania" for a zone transition. Empty when the
    /// game links the object to nothing usable.
    ///
    /// This is what makes the game's collective names mean something. Measured
    /// offline against the game data 2026-08-08: 1667 EObj rows are called
    /// "Zielort" and 1547 of them name their quest; 135 are called "Ausgang"
    /// and 96 of those name the zone they lead to. Without this the browser
    /// says "Zielort, Objekt, 88 Meter" and the player cannot tell one of 1667
    /// from another (user report 2026-08-08).
    /// </summary>
    /// The quest is named whether or not the player is on it. That was tried the
    /// other way round first - name it only for accepted quests, hide the rest -
    /// and it was wrong on both counts (user + probe 2026-08-08):
    ///  - the hiding removed objects the player was using. "Ein Objekt gehoert
    ///    nicht immer zu einer Quest, man kann damit auch unabhaengig agieren."
    ///    EObj.Data says an object ALSO appears in a quest, not that it is
    ///    useless otherwise.
    ///  - staying silent on foreign quests left exactly the bare "Zielort" that
    ///    started all this. The quest name is the only thing the game knows
    ///    about such an object, so withholding it helps nobody.
    /// The check itself was never broken: the probe read seven journal quests
    /// correctly, they simply were not these three.
    public string Qualifier(IGameObject obj)
    {
        var purpose = PurposeOf(obj.BaseId, obj.ObjectKind);

        if (!string.IsNullOrEmpty(purpose.QuestName))
            return AccessibilityStrings.ForQuest(purpose.QuestName);

        return !string.IsNullOrEmpty(purpose.WarpArea)
            ? AccessibilityStrings.LeadsToArea(purpose.WarpArea)
            : string.Empty;
    }

    /// <summary>Quest / warp binding of an object, cached per data id.</summary>
    private ObjectPurpose PurposeOf(uint baseId, ObjectKind kind)
    {
        // Only EventObj carry the EObj.Data link; asking for anything else would
        // hit unrelated rows that happen to share the id.
        if (kind != ObjectKind.EventObj || baseId == 0) return ObjectPurpose.None;
        if (_purposes.TryGetValue(baseId, out var cached)) return cached;

        var purpose = ReadPurpose(baseId);
        _purposes[baseId] = purpose;
        return purpose;
    }

    private ObjectPurpose ReadPurpose(uint baseId)
    {
        if (!_data.GetExcelSheet<EObj>().TryGetRow(baseId, out var eobj)) return ObjectPurpose.None;

        // EObj.Data is one reference resolved against several sheets (Quest,
        // CustomTalk, Warp - see Lumina's EObj). Ask what it actually is; do not
        // assume from the object's name.
        var link = eobj.Data;
        if (link.RowId == 0) return ObjectPurpose.None;

        if (link.Is<Quest>() && link.GetValueOrDefault<Quest>() is { } quest)
        {
            var name = StripDeclensionMarkers(quest.Name.ExtractText());
            if (IsSpeakable(name)) return new ObjectPurpose(quest.RowId, name, string.Empty);
        }

        if (link.Is<Warp>() && link.GetValueOrDefault<Warp>() is { } warp)
        {
            var area = warp.TerritoryType.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            area = StripDeclensionMarkers(area);
            if (IsSpeakable(area)) return new ObjectPurpose(0, string.Empty, area);
        }

        return ObjectPurpose.None;
    }

    /// <summary>
    /// What an object is tied to: a quest, or a warp destination.
    ///
    /// Use <see cref="None"/> for "nothing", never <c>default</c>: a record
    /// struct's default leaves the string members NULL, and reading .Length on
    /// them threw a NullReferenceException out of Qualifier for every object
    /// without a binding - which is most of them. In game that killed the whole
    /// browser announcement mid-way, so pressing the key did nothing at all
    /// (user: "die quest npcs werden nicht vorgelesen"; log 2026-08-08 18:22
    /// with the stack trace). The compiler does not warn about this even with
    /// nullable reference types on.
    /// </summary>
    private readonly record struct ObjectPurpose(uint QuestRowId, string QuestName, string WarpArea)
    {
        public static readonly ObjectPurpose None = new(0, string.Empty, string.Empty);
    }

    /// <summary>Sheet name for a data id, or "" when the sheet has none either.</summary>
    private string FromSheet(uint baseId, ObjectKind kind)
    {
        if (baseId == 0) return string.Empty;
        if (_cache.TryGetValue((baseId, kind), out var cached)) return cached;

        var name = kind switch
        {
            ObjectKind.EventNpc => _data.GetExcelSheet<ENpcResident>().TryGetRow(baseId, out var npc)
                ? StripDeclensionMarkers(npc.Singular.ExtractText())
                : string.Empty,
            ObjectKind.EventObj => _data.GetExcelSheet<EObjName>().TryGetRow(baseId, out var eobj)
                ? StripDeclensionMarkers(eobj.Singular.ExtractText())
                : string.Empty,
            _ => string.Empty,
        };

        _cache[(baseId, kind)] = name;
        return name;
    }

    /// <summary>
    /// Removes the grammar markers the sheets carry for languages that decline
    /// ("Soldat[p] von Nophicas Schar", "Highwind-Bedienstet[a]"). The game
    /// substitutes the correct ending at runtime from surrounding text; we have
    /// no sentence to derive it from, so the marker is dropped rather than
    /// guessed - a slightly clipped word beats "Bedienstet bracket a bracket".
    ///
    /// Exactly three markers exist across both sheets, counted offline against
    /// the game data 2026-08-08: [a] 6679x, [p] 2246x, [t] 32x. The pattern is
    /// therefore closed, not a catch-all for square brackets.
    /// </summary>
    private static string StripDeclensionMarkers(string text)
        => text.Length == 0
            ? string.Empty
            : DeclensionMarker.Replace(text, string.Empty).Replace("  ", " ").Trim();

    private static readonly System.Text.RegularExpressions.Regex DeclensionMarker =
        new(@"\[(a|p|t)\]", System.Text.RegularExpressions.RegexOptions.Compiled);
}
