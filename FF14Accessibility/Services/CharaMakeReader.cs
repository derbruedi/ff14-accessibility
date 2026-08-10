using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Plugin.Services;
using FF14Accessibility.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using CsCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using CsDrawData = FFXIVClientStructs.FFXIV.Client.Game.Character.DrawDataContainer;
using CsGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CsObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace FF14Accessibility.Services;

/// <summary>
/// Speaks the Appearance step of character creation.
/// THE PROBLEM: the step is one long list of image grids and sliders. Upstream's
/// answer was a "random appearance" hotkey, which was removed here - it
/// hands a blind player a character they had no say in. User 2026-08-08: *"I (and
/// others) would prefer to be able to make our own character, especially choosing
/// the voice."*
/// HOW IT READS THE STATE - not from the UI. There is no <c>AgentCharaMake</c> and
/// no <c>AddonCharaMakeFeature</c> in this FFXIVClientStructs (verified by
/// enumerating every type in the assembly, 2026-08-08), so there is no struct that
/// holds the in-progress character. What there IS, and what this uses, is the live
/// PREVIEW MODEL: all 32 race/tribe/sex combinations sit in the object table at
/// once and exactly one has <c>DrawObject.IsVisible</c> (verified 2026-07-10,
/// indices 200-231). Its <c>DrawData.CustomizeData</c> is the character being
/// built, and the byte offsets of that struct are ALMOST exactly the
/// <c>Customize</c> column of <c>CharaMakeType.CharaMakeStruct</c> - the sheet and
/// the struct index the same array. So every menu's current value can be read and
/// named.
/// "ALMOST" is doing real work in that sentence, and skipping over it was the
/// 2026-08-08 defect. Two things break the one-menu-one-byte assumption, and both
/// are handled where <see cref="Menu.Byte"/> and <see cref="Menu.Mask"/> are set:
/// three bytes pack a 7-bit value plus an unrelated flag in bit 7, and Iris Size's
/// <c>Customize</c> column names a byte it does not write. See the comments on
/// <see cref="LowSevenBitBytes"/> and <see cref="IrisSizeCustomize"/> - each is
/// backed by the decompiled struct AND by a live log line, because the sheet alone
/// says the wrong thing here.
/// WHY THAT IS THE RIGHT SOURCE: it is independent of how the player navigates.
/// The user reports the creation menus respond to the numpad, i.e. the game moves
/// its own selection; this service only has to notice and describe the result, so
/// it works for mouse, numpad, or anything else the game supports. It also cannot
/// drift out of sync with the model on screen, because it IS the model on screen.
/// WHAT IT WILL NOT DO: it never invents a name. Menus whose entries have no name
/// in the game data (hairstyles, faces, face paint - checked: empty Hint, no
/// HintItem, and the named ones are aesthetician unlocks not offered at creation)
/// are announced as position only. Colours are named from the game's own palette
/// file via <see cref="CharaMakePalette"/>; if that file cannot be read, the
/// announcement silently degrades to the position and says nothing false.
/// </summary>
public sealed unsafe class CharaMakeReader
{
    private readonly IObjectTable _objects;
    private readonly IDataManager _data;
    private readonly IGameGui _gui;
    private readonly TolkService _tolk;
    private readonly IPluginLog _log;
    private readonly CharaMakePalette _palette;
    /// <summary>Only source of real words for the voice picker's icon-only sample
    /// buttons - see <see cref="UpdateVoiceSample"/>.</summary>
    private readonly TooltipService _tooltips;

    /// <summary>Number of bytes in <c>CustomizeData</c> (Race@0 .. FacePaintColor@25).</summary>
    private const int CustomizeBytes = 26;

    /// <summary>Previous values of the bytes PAST the diff window, for
    /// the Eye Color probe in <see cref="Update"/>. Sized generously; only indices that
    /// actually exist in the live span are ever touched.</summary>
    private readonly byte[] _lastWide = new byte[64];

    private readonly byte[] _last = new byte[CustomizeBytes];
    private bool _haveSnapshot;
    private ushort _lastVoice;
    private bool _haveVoice;

    /// <summary>Which MENU was announced last, so a run of changes on one slider
    /// does not repeat the label on every step. Keyed on the menu rather than its
    /// byte because Eye Shape and Iris Size share byte 16.</summary>
    private string _lastSpokenMenu = string.Empty;

    /// <summary>
    /// The menu the player is WORKING IN, which is a different
    /// question from <see cref="_lastSpokenMenu"/> ("what did we say last") and is
    /// why it needs its own field: a side-effect announcement used to take ownership
    /// of that one and then drop the label off the next real change.
    /// Set ONLY from <see cref="DescribeCategory"/>, i.e. from the game's own focus
    /// on a <c>_CharaMakeFeature</c> category button - which is how a picker is
    /// opened in the first place, so it still names the right menu once the focus has
    /// moved inside the picker. Nothing else writes it, deliberately: if a value
    /// change could claim it, the bleed this exists to suppress would claim it first.
    /// Empty means "not known yet" (the first frames of the step, or straight after a
    /// race change) and then NOTHING is suppressed - the fallback is the old
    /// behaviour, because silence is the one failure a blind player cannot detect.
    /// </summary>
    private string _currentMenu = string.Empty;

    /// <summary>
    /// Is a radio button's POSITION IN THE WINDOW the same thing as
    /// its position in the sheet's offered list? arrow-press summary hangs on
    /// that, and it is NOT established, so this ships false and the summary stays off.
    /// WHY IT MATTERS AND WHY IT IS NOT OBVIOUS. The position the player already hears
    /// ("3 of 12") is the RADIO's own position and is honest whatever the sheet says -
    /// it describes where the cursor is in the window. A shape description is a
    /// different kind of claim: <see cref="CharaMakeShapeText"/> is keyed by the
    /// SHEET's entry number, so attaching it to the radio position asserts that the two
    /// orders agree. If they do not, every type-0 arrow press describes the wrong
    /// shape - which is exactly the class of defect caught in the hairstyle blocks,
    /// where entry order and id order were assumed equal and 125 entries were wrong.
    /// HOW THE LOG SETTLES IT, no extra key and no extra build. The game applies a
    /// radio option the instant the cursor lands on it, 18 ms after the focus event
    /// (measured, see <see cref="IsRadioPickerOpen"/>). So both numbers already reach
    /// the log on every arrow press inside a picker, and <see cref="Announce"/>'s
    /// suppressed branch now prints them side by side:
    ///   "(radio picker open ...) focus said N of M; the value the game then applied is
    ///    entry K of M"
    /// Arrow through a type-0 menu - Jaw, Nose, Mouth, Eyebrows - and read those lines.
    /// **N == K on every line means the orders agree**; flip this to true and the
    /// summary is live. Any line where they differ means they do not, and the summary
    /// must be keyed off the applied value instead, not off the focus.
    /// </summary>
    /// [ANSWERED 2026-08-09, in game.] The orders agree. 14 probe lines across two menus
    /// and two sizes, every one of them N == K: `focus said 3 of 4 ... entry 3 of 4 in
    /// 'Jaw'`, `focus said 4 of 6 ... entry 4 of 6 in 'Eye Shape'`. One line reads
    /// `4 of 4 ... entry 3 of 4` and is NOT a counter-example: it is a fast reversal where
    /// the next press's focus event landed before the previous value had settled, so the
    /// two halves of that line belong to different presses. Turned on.
    private const bool RadioOrderIsSheetOrder = true;

    /// <summary>What the focus reader last said inside a picker, for
    /// the pairing above. Zero means no radio focus has been seen yet.</summary>
    private int _lastRadioIndex;
    private int _lastRadioCount;

    /// <summary>
    /// The UI icon of the face currently on the preview, or 0 when
    /// it is not known yet. It is the key <see cref="CharaMakeShapeText"/> is written
    /// against, because a type-0 entry is a morph target on THAT face's model - the
    /// same "Nose, 3 of 6" is a different nose on face 2 than on face 1.
    /// Re-read every frame from the live customize bytes rather than cached per row,
    /// so changing the Face changes every type-0 description with it. Zero disables
    /// the lookup entirely, which is the same null-and-say-nothing path an
    /// undescribed icon takes.
    /// </summary>
    private uint _faceIcon;

    /// <summary>
    /// Whether the lip colour is APPLIED - bit 7 of byte 19,
    /// which the struct names <c>Lipstick</c>. Held as a field for the same reason
    /// <see cref="_faceIcon"/> is: it is a second key that every description of byte 20
    /// needs, and it is re-read from the live bytes at each of the three places that
    /// read them (the frame tick, the category focus, the Ctrl+F10 summary) rather than
    /// cached per row. See <see cref="LipstickByte"/> for why the flag is not simply
    /// another <c>Menu.Mask</c>.
    /// </summary>
    private bool _lipstick;

    /// <summary>
    /// Menus the GAME changed by itself while the player was
    /// working in a different one. Not spoken when it happens (see
    /// <see cref="IsSideEffect"/>); reported by <see cref="ReadSummary"/> instead,
    /// which is where the user asked for it to go.
    /// </summary>
    private readonly HashSet<string> _sideEffects = new();

    private (byte Race, byte Tribe, byte Sex) _lastWho;
    private readonly Dictionary<(byte, byte, byte), RowMenus> _menuCache = new();

    public CharaMakeReader(IObjectTable objects, IDataManager data, IGameGui gui,
                           TolkService tolk, IPluginLog log, TooltipService tooltips)
    {
        _objects = objects;
        _data = data;
        _gui = gui;
        _tolk = tolk;
        _log = log;
        _tooltips = tooltips;
        _palette = new CharaMakePalette(data, log);
    }

    // ── One menu of the Appearance step, resolved from CharaMakeType ──────────

    /// <summary>
    /// CustomizeData bytes that are NOT one value: the struct
    /// packs a 7-bit field plus a flag in bit 7 (ilspycmd
    /// <c>Client.Game.Character.CustomizeData</c>):
    /// <c>16 = EyeShape(0,7) + SmallIris(7,1)</c>,
    /// <c>19 = Mouth(0,7) + Lipstick(7,1)</c>,
    /// <c>24 = FacePaint(0,7) + FacePaintReversed(7,1)</c>.
    /// The menu that owns the 7-bit field must therefore be matched against the LOW
    /// BITS, not the raw byte. Reading the raw byte is what produced the user's
    /// report: with a small iris set, byte 16 reads 132 (= 128 + 4), which is in no
    /// menu's list, so Eye Shape announced "selection unknown" (log 2026-08-08
    /// 06:12:40, eight times).
    /// (Byte 12 is eight separate flags and is handled by AnnounceFeatureBits;
    /// byte 7's bit 7 is Highlights and no menu in any of the 32 rows writes it.)
    /// </summary>
    private static readonly uint[] LowSevenBitBytes = { 16, 19, 24 };

    /// <summary>
    /// The one menu whose sheet <c>Customize</c> does NOT name
    /// the byte it really writes.
    /// <c>CharaMakeType</c> puts Iris Size on byte 15 (offline sheet dump: the ONLY
    /// menu on that byte in any of the 32 rows, type 0, n=2, values [0,1],
    /// "Large"/"Small"). But byte 15 is <c>EyeColorLeft</c> in the struct, a
    /// 0..191 colour, so the live value is never 0 or 1 and the category list said
    /// "Iris Size, selection unknown" - the user's report.
    /// What the menu ACTUALLY drives is bit 7 of byte 16, which the struct names
    /// <c>SmallIris</c>. Proven at runtime, not assumed: toggling Iris Size moved
    /// byte 16 to 132 (log 06:12:40), i.e. it set 0x80 while leaving Eye Shape's
    /// value 4 in the low bits. Keyed off the NUMBER 15, never off the label, so it
    /// holds in every client language.
    /// </summary>
    private const uint IrisSizeCustomize = 15;

    /// <summary>The two eye-colour bytes, named from the struct
    /// (ilspycmd <c>CustomizeData</c>: <c>[FieldOffset(9)] EyeColorRight</c>,
    /// <c>[FieldOffset(15)] EyeColorLeft</c>) rather than from the sheet, which puts
    /// only ONE Eye Color menu in all 32 rows and puts it on byte 9.</summary>
    private const int EyeRightByte = 9;
    private const int EyeLeftByte  = 15;

    /// <summary>
    /// Lip Color is the one menu whose value does not say
    /// what the face shows. The colour is in byte 20 (<c>LipColorFurPattern</c>,
    /// docs/game-api.md:244); whether it is APPLIED is bit 7 of byte 19, which the
    /// struct names <c>Lipstick</c> (game-api.md:2077).
    /// THIS IS NOT THE <see cref="IrisSizeCustomize"/> SHAPE, and reading it as one
    /// builds the wrong thing. For Iris Size the menu's own value IS the flag
    /// (<c>Byte = 16</c>, <c>Mask = 0x80</c>), so the existing mechanism covers it.
    /// Here the menu's value is the colour and the flag sits on a FOREIGN byte that
    /// no menu in any of the 32 rows claims: byte 19 carries Mouth and Fang Length,
    /// both 7-bit, and <see cref="LowSevenBitBytes"/> masks bit 7 away from both. So
    /// nothing read it. Measured 2026-08-10: clearing lipstick logged
    /// <c>CustomizeData[19] 128 -&gt; 0, no menu owns the changed bits</c> and said
    /// nothing, while the colour menu went on naming a colour that is not on the
    /// face - which is the report this fixes.
    /// Byte 24 bit 7 (<c>FacePaintReversed</c>) has the same shape and is likewise
    /// unclaimed. It is deliberately NOT handled here - it is its own piece of work.
    /// </summary>
    private const int  LipstickByte = 19;
    private const byte LipstickFlag = 0x80;

    /// <summary>The byte <see cref="LipstickFlag"/> switches
    /// on. Two different menus live on it across the 32 rows - Fur Pattern (type 1) on
    /// Hrothgar, Lip Color (type 2) everywhere else - so the menu is identified by byte
    /// AND type, never by its label, which arrives in the client's language.</summary>
    private const uint LipColorByte = 20;

    /// <summary>The game's own word for "no lip colour":
    /// <c>Lobby</c> row 2127. Verified offline against the installed sqpack
    /// (<c>cmdump lobby 2115 2140</c>) - it prints as "None" among Odd Eyes, Randomize
    /// Appearance, Hair Color and Highlights, i.e. inside the character-creation
    /// vocabulary rather than borrowed from some unrelated block. Read through
    /// <see cref="LobbyText"/>, so it arrives in the client's language like every other
    /// word this reader speaks.</summary>
    private const uint NoneLobbyRow = 2127;

    private sealed class Menu
    {
        public string Label = string.Empty;
        public byte Type;
        public int Count;
        /// <summary>The sheet's own column. Kept for logging; use <see cref="Byte"/>
        /// to read the model.</summary>
        public uint Customize;
        /// <summary>The CustomizeData byte this menu really writes.</summary>
        public uint Byte;
        /// <summary>Which bits of <see cref="Byte"/> belong to this menu. 0xFF for
        /// the ordinary menus that own their byte outright.</summary>
        public byte Mask = 0xFF;
        /// <summary>The Voice menu, which writes NO CustomizeData byte at all - its
        /// <c>Customize</c> column is 0 and its <c>SubMenuGraphic</c> is all zeroes,
        /// because the twelve ids live in <c>VoiceStruct</c> instead. Without this
        /// flag it reads byte 0 (Race) and matches nothing, which is why the Ctrl+F10
        /// summary said "Voice, selection unknown" right before the real voice line.
        /// Its value comes from the preview model's <c>Vfx.VoiceId</c>.</summary>
        public bool IsVoice;

        /// <summary>This menu's value, extracted from the raw byte. A bit-7 flag
        /// yields 0 or 1, which is exactly what the sheet stores for it.</summary>
        public byte ValueFrom(byte raw)
            => Mask == 0x80 ? (byte)((raw & 0x80) != 0 ? 1 : 0) : (byte)(raw & Mask);
        /// <summary>Spoken name per entry. Empty strings where the game has none.</summary>
        public string[] OptionNames = Array.Empty<string>();
        /// <summary>The CustomizeData value each entry writes. Empty for types
        /// where the index IS the value (colours) or the value is free (sliders).</summary>
        public byte[] Values = Array.Empty<byte>();
        /// <summary>The UI icon each entry shows, parallel to
        /// <see cref="OptionNames"/>. Only type 1 (the icon grids) has these, and it
        /// is the key <see cref="CharaMakeIconText"/> is written against - the icon
        /// id is the same number in every client language and never moves.</summary>
        public uint[] Icons = Array.Empty<uint>();
        /// <summary>Slider end labels, low then high. Only for type 5.</summary>
        public string LowLabel = string.Empty;
        public string HighLabel = string.Empty;
    }

    private sealed class RowMenus
    {
        public List<Menu> Menus = new();
        /// <summary>The 12 voice ids offered, in display order.</summary>
        public byte[] Voices = Array.Empty<byte>();
        public string[] VoiceNames = Array.Empty<string>();

        /// <summary>
        /// The icon id of the face currently selected, or 0.
        /// This is the key <see cref="CharaMakeShapeText"/> is written against, and it
        /// has to come from the Face MENU rather than from the raw byte: the byte is a
        /// per-row graphic value (1..7, and 5..8 on Hrothgar) that repeats across
        /// races, while the icon id is globally unique and identifies the face MODEL
        /// the shape keys live on. Found by the sheet's own <c>Customize</c> column
        /// (5 = Face), never by the label, which arrives in the client's language.
        /// </summary>
        public uint FaceIcon(byte faceValue)
            => FaceIndex(faceValue) is var i && i >= 0 ? FaceMenu!.Icons[i] : 0u;

        private Menu? FaceMenu => Menus.FirstOrDefault(m => m.Byte == 5 && m.Type == 1);

        /// <summary>The Face menu's 0-based ENTRY index for a raw byte-5 value, or -1.
        /// That index - not the byte - is what <c>FacialFeatureOption</c> is subscripted
        /// by: Hrothgar female offers faces 5..8 and its populated option structs are
        /// [0..3].</summary>
        public int FaceIndex(byte faceValue)
        {
            var face = FaceMenu;
            if (face == null) return -1;
            var idx = Array.IndexOf(face.Values, faceValue);
            return idx >= 0 && idx < face.Icons.Length ? idx : -1;
        }

        /// <summary>
        /// <c>CharaMakeType.FacialFeatureOption</c>: eight structs of
        /// seven UI icon ids, one struct per FACE. These are the pictures of the type-4
        /// toggles - what "Facial Features"/"Other Features" and
        /// "Tattoos"/"Ear Clasps"/"Limbal Ring" actually put on the face.
        /// MEASURED (cmdump featicons, read off the contact sheets): the 5-entry menu
        /// owns slots 1-5 and the 2-entry menu owns slots 6-7, whichever order the two
        /// appear in <c>CharaMakeStruct</c> - Elezen Wildwood lists Ear Clasps FIRST and
        /// its ear jewellery is still slots 6 and 7.
        /// NOT YET PROVEN, and the reason nothing here is spoken: that slot i is bit
        /// i-1 of CustomizeData byte 12 (<c>FacialFeature1..7</c>). The two lists are
        /// the game's own 1..7 numbering and the identity mapping is the obvious
        /// reading, but the sheet does not state it. <see cref="AnnounceFeatureBits"/>
        /// LOGS the pairing so one in-game toggle settles it; see its comment.
        /// </summary>
        public uint[][] FeatureIcons = Array.Empty<uint[]>();
    }

    // ── Frame tick ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once per frame. Does nothing at all unless a creation step this
    /// service covers is on screen, so it costs nothing during normal play.
    /// </summary>
    public void Update()
    {
        var appearance = IsAddonVisible("_CharaMakeFeature");
        var classStep  = IsAddonVisible("_CharaMakeClassSelector");

        if (!appearance)
        {
            _haveSnapshot = false;
            _haveVoice = false;
            _lastSpokenMenu = string.Empty;
            _currentMenu = string.Empty;   // left the step: no menu is current
            _faceIcon = 0;                 // and no face is on the preview
            _sideEffects.Clear();          // and nothing is owed to the summary
            _pendingSlider = string.Empty;
            _lastSample = -1;
            _sampleGroupLogged = false;
        }
        if (!classStep) _haveClass = false;
        if (!appearance && !classStep) return;

        TrackWindows(); // must run before any announcement decides how to speak

        var model = FindPreviewModel();
        if (model == null) return;

        if (classStep) UpdateClass(model);
        if (!appearance) return;

        Span<byte> now = stackalloc byte[CustomizeBytes];
        var src = LiveCustomize(model);
        for (var i = 0; i < CustomizeBytes && i < src.Length; i++) now[i] = src[i];

        var who = (now[0], now[4], now[1]); // Race, Tribe, Sex
        if (!_haveSnapshot || who != _lastWho)
        {
            // First frame of the step, or the player went back and changed race.
            // Nothing is announced here: the race/tribe steps have their own
            // handlers, and re-reading 20 values on entry would bury them.
            _lastWho = who;
            for (var i = 0; i < CustomizeBytes; i++) _last[i] = now[i];
            _haveSnapshot = true;
            _lastSpokenMenu = string.Empty;
            // A race change rebuilds every value, so anything the
            // game did to the OLD body is not news about the new one.
            _currentMenu = string.Empty;
            _sideEffects.Clear();
            return;
        }

        var menus = GetMenus(who.Item1, who.Item2, who.Item3);
        if (menus == null) return;

        // Which FACE is on screen, as its icon id. That is the key
        // CharaMakeShapeText is written against, and it has to be re-read every frame
        // because changing the Face changes every type-0 description with it - the
        // shapes are morph targets on THAT face's model.
        _faceIcon = menus.FaceIcon(now[5]);

        // ...and whether the lip colour is on the face at
        // all. Read BEFORE anything describes byte 20 - see LipstickByte.
        _lipstick = (now[LipstickByte] & LipstickFlag) != 0;

        // THE TWO EYES ARE DECIDED TOGETHER, before the
        // generic loop, because neither byte means anything without the other.
        // Measured 2026-08-10 05:55 in CMFColorEye. With Odd Eyes OFF the game
        // writes byte 9 (EyeColorRight) and byte 15 (EyeColorLeft) in the SAME
        // frame, to the same value - the log shows the swatch announcement from
        // byte 9 and "CustomizeData[15] 30 -> 31, no menu owns the changed bits" at
        // the same millisecond. With Odd Eyes ON the eyes decouple and the pane the
        // player is in writes ITS byte alone; editing the left eye therefore moved
        // byte 15 by itself, no menu owned it, and the picker went completely
        // silent. That is the user's report ("it let me select the other color, but
        // after that the menu goes completely silent") and the addon really is a
        // split pane: the dump has TWO 192-entry lists, id=17 Sel=21 and id=9
        // Sel=30, each with its own Current/Previous swatch and RGB text.
        // A per-byte loop cannot get this right in either direction. Announcing
        // byte 15 unconditionally would speak every colour TWICE while the eyes are
        // linked; comparing the two bytes inside the loop would compare a new value
        // against a stale one, because i=9 is reached before i=15 is copied. So the
        // pair is resolved here, from `now`, where both values are current.
        if (now[EyeRightByte] != _last[EyeRightByte] || now[EyeLeftByte] != _last[EyeLeftByte])
        {
            AnnounceEyes(menus, who.Item2, who.Item3, now);
            _last[EyeRightByte] = now[EyeRightByte];
            _last[EyeLeftByte]  = now[EyeLeftByte];
        }

        // THE LIPSTICK FLAG IS ALSO DECIDED BEFORE THE LOOP,
        // for the same reason the eyes are: it lives in byte 19 but what it switches is
        // byte 20, and the loop reaches 19 while _last[20] still holds the previous
        // frame's colour - so an "on" announcement would name the colour one frame
        // stale. Only BIT 7 is levelled here; byte 19's low bits stay for the loop, so
        // the menu that owns them (Mouth, or Fang Length on Hrothgar) still announces
        // itself normally on a frame where both moved.
        if (((now[LipstickByte] ^ _last[LipstickByte]) & LipstickFlag) != 0)
        {
            AnnounceLipstick(menus, who.Item2, who.Item3, now);
            _last[LipstickByte] = (byte)((_last[LipstickByte] & ~LipstickFlag)
                                       | (now[LipstickByte] & LipstickFlag));
        }

        for (var i = 0; i < CustomizeBytes; i++)
        {
            if (now[i] == _last[i]) continue;   // the eye bytes are already level
            var before = _last[i];
            _last[i] = now[i];
            Announce(menus, who.Item2, who.Item3, i, before, now[i]);
        }

        // PROBE for the Eye Color silence. User 2026-08-09: *"the eye
        // color screen is not vocalizing its options at all."* Measured from the log: while
        // the cursor moved inside CMFColorEye there is NO [CharaMake] line whatsoever -
        // and every early return in Announce logs one - so Announce was never reached,
        // i.e. no byte in 0..25 changed at all.
        // WHY is not established. This line tests the cheapest hypothesis: that the value
        // lives PAST the 26 bytes this reader diffs, so the loop above cannot see it. It
        // logs only when something outside the window actually moves, so it stays quiet if
        // that is not the cause - in which case the answer is elsewhere and this must not
        // be read as evidence for it.
        for (var i = CustomizeBytes; i < src.Length && i < 64; i++)
        {
            if (i < _lastWide.Length && src[i] == _lastWide[i]) continue;
            if (i < _lastWide.Length)
                _log.Info($"[CharaMake][widebyte] CustomizeData[{i}] {_lastWide[i]} -> {src[i]} " +
                          $"- PAST the {CustomizeBytes}-byte window this reader diffs.");
            if (i < _lastWide.Length) _lastWide[i] = src[i];
        }
        for (var i = CustomizeBytes; i < src.Length && i < _lastWide.Length; i++) _lastWide[i] = src[i];

        UpdateVoice(model, menus);
        UpdateVoiceSample(menus); // after UpdateVoice: same buffer, newest wins
        FlushSlider();
    }

    /// <summary>
    /// True while one of the radio pickers (<c>CMFRadio*</c>) is
    /// on screen. Inside those, the global focus reader already speaks the option
    /// AND its index as one sentence
    /// (<c>UIReaderService.TryReadCharaMakeRadioPosition</c>), so this service must
    /// not say the same thing a second time - the user's report:
    /// *"still double-announcing ... you don't actually need two spoken
    /// announcements there."*
    /// SAFE, because it is not a case of hoping something else speaks: the game
    /// applies a radio option the instant the cursor lands on it, so the focus
    /// reader and the value change fire on the SAME cursor move, 18 ms apart (log
    /// 06:14:11.057 focus 'Small' → 06:14:11.075 byte 16 changed). Every value
    /// this suppresses has just been announced by name and position.
    /// The sample axis is exempt: it has no text and moves without the focus going
    /// anywhere, so nothing else covers it.
    /// CONSEQUENCE FOR THE TYPE-0 SHAPE TEXT, stated here because it
    /// is the one place someone will look for it. Type-0 menus - Jaw, Eye Shape,
    /// Eyebrows, Nose, Mouth, Fang Length, Elezen/Lalafell Ear Shape - open exactly
    /// these <c>CMFRadio*</c> windows, so this gate suppresses the value announcement
    /// on every cursor move inside one. <see cref="CharaMakeShapeText"/> therefore
    /// reaches the player through the CATEGORY focus (<see cref="DescribeCategory"/>,
    /// full text) and through Strg+F10 (<see cref="ReadSummary"/>), but NOT as the
    /// short summary on the arrow press that 61e asked for.
    /// Do NOT fix that by loosening this gate: it exists because the user reported
    /// double announcements, and the focus reader is the utterance that fires on that
    /// cursor move.
    /// The clean fix named here is now WIRED, in the only place it
    /// belongs - <c>UIReaderService</c>'s radio branch passes
    /// <see cref="SummarizeRadioOption"/> as the last argument of
    /// <c>AccessibilityStrings.CharaMakeOption</c>, so the summary rides the utterance that
    /// already speaks and this gate is untouched. It stays SILENT until
    /// <see cref="RadioOrderIsSheetOrder"/> says the window's option order and the
    /// sheet's are the same; read that field before turning it on.
    /// </summary>
    private bool IsRadioPickerOpen => FindVisibleRadioAddon() != null;

    /// <summary>
    /// True while one of the ICON GRID pickers (<c>CMFIcon*</c>) is on
    /// screen - Face, Hairstyle, Face Paint and the two type-4 windows.
    /// WHY IT IS NEEDED. Those grids get a position from the global focus reader on every
    /// cursor move ("28 of 52"), and this service then said the whole thing again with the
    /// description on the end ("28 of 52, short, spiky") - the user's double-read. The fix
    /// is NOT to silence the focus reader: that utterance is what fires on movement and on
    /// Confirm, and silencing it took both out (tested 2026-08-09). Instead this service
    /// drops the label and the position and contributes only the part the focus reader
    /// cannot know - the description. Index once, description once.
    /// The COLOUR pickers are deliberately excluded. Their rows are pure swatches with no
    /// text at all, so the focus reader says nothing for them (`[Focus] STUMM`, log 13:32)
    /// and this service is their only speaker - it must keep the full sentence there.
    /// </summary>
    private bool IsIconGridPickerOpen
    {
        get
        {
            var mgr = RaptureAtkUnitManager.Instance();
            if (mgr == null) return false;
            for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
            {
                var a = mgr->AllLoadedUnitsList.Entries[i].Value;
                if (a == null || !a->IsVisible) continue;
                if (a->NameString.StartsWith("CMFIcon", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The same question for the COLOUR pickers - the
    /// addons whose name starts <c>CMFColor</c> (<c>CMFColorEye</c> captured
    /// 2026-08-10 05:55).
    /// WHY IT IS NEEDED NOW AND WAS NOT BEFORE: the colour branch below used to say
    /// "colours keep the full sentence because nothing else speaks for them". The
    /// log disproves it. Inside CMFColorEye the focus reader resolves the swatch
    /// row to a position and speaks it with SpeakInterrupt, and the colour
    /// announcement then interrupts THAT ~140 ms later:
    /// <code>
    /// 05:55:33.413 [Focus] id=6 Text='30 of 192'  -> [Speak] INT '30 of 192'
    /// 05:55:33.552                                   [Speak] INT 'muted warm brown, group 4 shade 6'
    /// </code>
    /// which is the user's report of the eye-colour screen "reading index, then
    /// interrupted by color". Identical in kind to the face-paint defect /// already fixed for the icon grids, and it takes the same fix: the position
    /// leads, the description is QUEUED behind it, and the ORDER OF THE CALLS
    /// guarantees the result at any speech rate.
    /// </summary>
    private bool IsColorPickerOpen
    {
        get
        {
            var mgr = RaptureAtkUnitManager.Instance();
            if (mgr == null) return false;
            for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
            {
                var a = mgr->AllLoadedUnitsList.Entries[i].Value;
                if (a == null || !a->IsVisible) continue;
                if (a->NameString.StartsWith("CMFColor", StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    /// <summary>Just the picture description for an icon-grid value -
    /// no label, no position. Empty when this icon has no authored text, in which case
    /// nothing is spoken at all and the focus reader's position stands alone.</summary>
    private string IconDescriptionOnly(Menu menu, byte value, bool brief)
    {
        var idx = Array.IndexOf(menu.Values, value);
        if (idx < 0 || idx >= menu.Icons.Length) return string.Empty;
        var icon = menu.Icons[idx];
        return (brief ? CharaMakeIconText.Summarize(icon) : CharaMakeIconText.Describe(icon))
               ?? string.Empty;
    }

    /// <summary>
    /// The decal a TYPE-4 picker row shows - Facial Features, Tattoos,
    /// Ear Clasps, Limbal Ring. Answers the user's *"facial features have no descriptions,
    /// and neither do tattoos"*, and it is only answerable now because the slot-to-bit
    /// mapping was measured in game today (see AccessibilityStrings.CharaMakeFeatureNamed).
    /// SLOTS RUN ACROSS BOTH MENUS. `CharaMakeType.FacialFeatureOption` is SEVEN icons per
    /// face, and the two type-4 menus divide them: the 5-entry menu owns slots 1-5 and the
    /// 2-entry menu owns slots 6-7 (measured off the icons, 83d - Hyur's 6-7 are tattoos,
    /// Elezen Wildwood's are ear clasps, Au Ra's are limbal rings). So a row's POSITION in
    /// its own picker is not its slot: the bigger menu's slots come first, and the offset
    /// is computed from the menus themselves rather than hardcoded, so a row with a
    /// different split cannot silently shift every description by five.
    /// </summary>
    /// <param name="position">1-based row position within the picker on screen.</param>
    /// <param name="count">How many rows that picker has - identifies WHICH type-4 menu.</param>
    /// <param name="brief">Short form for a cursor move, full sentence otherwise.</param>
    /// <summary>
    /// The same row, with its CURRENT STATE on the end - what the
    /// player is about to toggle and whether it is on. User: *"the facial features screen
    /// needs to read the toggle when highlighted, not just when toggled. EG: 1 of 5:
    /// pointed chin beard: off."*
    /// The state comes from CustomizeData byte 12, bit slot-1, using the mapping measured
    /// in game 2026-08-09 (see AccessibilityStrings.CharaMakeFeatureNamed). A row whose decal has
    /// no authored text still reports its state, because "off" is useful even when the
    /// thing being toggled cannot be named yet.
    /// </summary>
    /// <summary>
    /// Dieselbe Zeile, aber vom Fokus-Leser aus aufgerufen, der das FENSTER kennt und
    /// nicht wissen kann, ob es eines von uns ist. Leer fuer jedes andere Fenster.
    /// Am Fenster-NAMEN und an der Zeilenzahl gemeinsam festgemacht, und beide muessen
    /// zustimmen. Die Zahl allein reicht nicht: ein Gesichter-Menue kann ebenfalls 5
    /// Eintraege haben, und eine Aufkleber-Beschreibung an ein Gesicht zu haengen waere
    /// genau die Sorte selbstbewusster Falschaussage, gegen die die Icon-Tabellen
    /// gebaut sind.
    /// </summary>
    public string? DescribeFeatureRow(string addonName, int position, int count)
        => addonName is "CMFIconFeature" or "CMFIconTatoo"
            ? DescribeFeatureRow(position, count)
            : null;

    public string? DescribeFeatureRow(int position, int count)
    {
        var slot = FeatureSlotFor(position, count);
        if (slot <= 0) return null;

        var model = FindPreviewModel();
        if (model == null) return null;
        var src = LiveCustomize(model);
        if (src.Length <= 12) return null;

        var on   = (src[12] & (1 << (slot - 1))) != 0;
        var what = SummarizeFeatureSlot(position, count);
        return string.IsNullOrEmpty(what)
            ? AccessibilityStrings.CharaMakeFeatureState(on)
            : AccessibilityStrings.CharaMakeFeatureRow(what!, on);
    }

    /// <summary>The 1-based FacialFeatureOption slot a picker row maps to, or 0. Split out
    /// so the description and the on/off state cannot disagree about which slot they mean.
    /// </summary>
    private int FeatureSlotFor(int position, int count)
    {
        if (position <= 0 || count <= 0) return 0;
        var model = FindPreviewModel();
        if (model == null) return 0;
        var src = LiveCustomize(model);
        if (src.Length < CustomizeBytes) return 0;
        var menus = GetMenus(src[0], src[4], src[1]);
        if (menus == null) return 0;

        var type4 = menus.Menus.Where(m => m.Type == 4).OrderByDescending(m => m.Count).ToList();
        var mine  = type4.FirstOrDefault(m => m.Count == count);
        if (mine == null) return 0;

        var slot = position;
        foreach (var m in type4)
        {
            if (m == mine) break;
            slot += m.Count;
        }
        return slot;
    }

    public string? SummarizeFeatureSlot(int position, int count, bool brief = true)
    {
        if (position <= 0 || count <= 0) return null;

        var model = FindPreviewModel();
        if (model == null) return null;
        var src = LiveCustomize(model);
        if (src.Length < CustomizeBytes) return null;
        var menus = GetMenus(src[0], src[4], src[1]);
        if (menus == null) return null;

        // Biggest first, so the 5-entry menu takes slots 1..5 and the 2-entry menu 6..7.
        var type4 = menus.Menus.Where(m => m.Type == 4).OrderByDescending(m => m.Count).ToList();
        var mine  = type4.FirstOrDefault(m => m.Count == count);
        if (mine == null) return null;               // not a type-4 picker of this size

        var slot = position;
        foreach (var m in type4)
        {
            if (m == mine) break;
            slot += m.Count;
        }

        var faceIdx = menus.FaceIndex(src[5]);
        if (faceIdx < 0 || faceIdx >= menus.FeatureIcons.Length) return null;
        var icons = menus.FeatureIcons[faceIdx];
        if (slot < 1 || slot > icons.Length) return null;

        var icon = icons[slot - 1];
        return brief ? CharaMakeIconText.Summarize(icon) : CharaMakeIconText.Describe(icon);
    }

    /// <summary>The decal a BIT owns, for the on/off announcement.
    /// slot = bit + 1, measured 2026-08-09 - see AccessibilityStrings.CharaMakeFeatureNamed.</summary>
    private string? DescribeFeatureBit(RowMenus menus, byte face, int bit, bool brief)
    {
        var faceIdx = menus.FaceIndex(face);
        if (faceIdx < 0 || faceIdx >= menus.FeatureIcons.Length) return null;
        var icons = menus.FeatureIcons[faceIdx];
        if (bit < 0 || bit >= icons.Length) return null;
        var icon = icons[bit];
        return brief ? CharaMakeIconText.Summarize(icon) : CharaMakeIconText.Describe(icon);
    }

    /// <summary>
    /// missing half: the SHORT shape summary for the radio
    /// option the cursor has just landed on, to be appended to the sentence the focus
    /// reader is already about to speak. Returns null - and the sentence is byte-for-byte
    /// what it was - for anything that is not a described type-0 entry.
    /// It is deliberately the caller's utterance that carries this, not a second one:
    /// <see cref="IsRadioPickerOpen"/> exists because the user reported double
    /// announcements, and loosening that gate is what double-counter defect was.
    /// This adds a clause to the one utterance that already fires on this cursor move.
    /// GATED on <see cref="RadioOrderIsSheetOrder"/>, which is false until the log says
    /// the window's order and the sheet's order are the same thing. Read that field
    /// before enabling: the failure mode is a confident description of the wrong shape,
    /// which a blind player has no way to catch.
    /// Also records the position for that same log line, and does so BEFORE the gate,
    /// so the probe keeps running while the feature stays off - the probe is the whole
    /// reason the feature is off.
    /// </summary>
    /// <param name="index">The 1-based radio position the focus reader resolved.</param>
    /// <param name="count">How many radios are in that group.</param>
    public string? SummarizeRadioOption(int index, int count)
    {
        _lastRadioIndex = index;
        _lastRadioCount = count;
        if (!RadioOrderIsSheetOrder || index <= 0 || _faceIcon == 0) return null;

        var model = FindPreviewModel();
        if (model == null) return null;
        var src = LiveCustomize(model);
        if (src.Length < CustomizeBytes) return null;

        var menus = GetMenus(src[0], src[4], src[1]);          // Race, Tribe, Sex
        if (menus == null) return null;

        // WHICH MENU IS THIS PICKER? It used to be "_currentMenu, or
        // give up", and giving up is what the user hit: *"jaw reads as type 1, type 2
        // alone ... should read type and descriptor, always"*. The confirm-chain fix
        // CLEARS _currentMenu by design (87a), so on the first press in a chained picker
        // there was no label and the descriptor vanished.
        // Resolved in three steps, and EVERY one is then checked against the radio
        // group's own size. That check is the safety: a label that does not match the
        // picker on screen describes a different menu's shapes, which is worse than
        // saying nothing.
        //   1. the group size, when exactly ONE type-0 menu has it - unambiguous by
        //      construction, and the only step that works on the very first press;
        //   2. _currentMenu, when the player opened this picker from the category list;
        //   3. _lastSpokenMenu, which after one value change in this picker IS this menu.
        Menu? menu = null;
        var sized = menus.Menus.Where(m => m.Type == 0 && !m.IsVoice && m.Count == count).ToList();
        if (sized.Count == 1) menu = sized[0];
        if (menu == null && _currentMenu.Length > 0)
            menu = menus.Menus.FirstOrDefault(m => m.Label == _currentMenu);
        if (menu == null && _lastSpokenMenu.Length > 0)
            menu = menus.Menus.FirstOrDefault(m => m.Label == _lastSpokenMenu);

        // Voice opens a CMFRadio window too, and its Customize column is 0 - a byte
        // that belongs to Race. Excluded by name rather than by type so a future type
        // change cannot let it through silently.
        if (menu == null || menu.Type != 0 || menu.IsVoice) return null;
        if (count > 0 && menu.Count != count) return null;   // wrong picker - say nothing

        // Entry 1 is the base mesh - same reasoning as DescribeValue, same gate.
        if (index == 1 && CharaMakeShapeText.Has(_faceIcon, menu.Customize, 2))
            return AccessibilityStrings.CharaMakeShapeBase;

        return CharaMakeShapeText.Summarize(_faceIcon, menu.Customize, index);
    }

    /// <summary>
    /// Voice is NOT part of CustomizeData - its <c>Customize</c> column is 0. The
    /// only candidate field in FFXIVClientStructs is <c>Character.Vfx.VoiceId</c>,
    /// which is also what <c>LoadCharacterSound</c> takes, but nothing in the
    /// headers proves it holds the SELECTED voice rather than a sound currently
    /// playing. So this announces only when the value is one of the 12 ids the
    /// sheet lists for this race/tribe/sex, and logs every value it sees either
    /// way. It cannot be confidently wrong; at worst it is quiet and the log says
    /// what the field actually did.
    /// </summary>
    private void UpdateVoice(CsCharacter* model, RowMenus menus)
    {
        var voice = model->Vfx.VoiceId;
        if (_haveVoice && voice == _lastVoice) return;
        var first = !_haveVoice;
        _lastVoice = voice;
        _haveVoice = true;

        var idx = Array.IndexOf(menus.Voices, (byte)voice);
        _log.Info($"[CharaMake] Vfx.VoiceId={voice} -> display index {idx} " +
                  $"(offered: {string.Join(",", menus.Voices)})");
        if (first || idx < 0) return;

        if (IsRadioPickerOpen) return; // the focus reader says "Type 4, 4 of 12"

        var name = idx < menus.VoiceNames.Length ? menus.VoiceNames[idx] : string.Empty;
        var label = AccessibilityStrings.CharaMakeVoiceLabel;
        // Same buffer as every other value, so it obeys the window-settle rule too.
        _pendingSlider = AccessibilityStrings.CharaMakeOption(label, name, idx + 1, menus.Voices.Length);
        _pendingSliderAt = Environment.TickCount64;
        _lastSpokenMenu = label; // voice is not a CustomizeData byte at all
    }

    // ── The voice picker's SECOND axis: which sample is being played ──────────

    /// <summary>Display index of the sample category last seen, -1 = none yet.</summary>
    private int _lastSample = -1;
    /// <summary>Logged once per opening of the picker, so the structure is in the
    /// log without repeating it every frame.</summary>
    private bool _sampleGroupLogged;

    /// <summary>
    /// Speaks the voice picker's SECOND axis - the sample the
    /// game plays so voices can actually be compared. User 2026-08-08: *"up/down on
    /// the num keys (4/6) switches voice type, left/right (8/2) switches category
    /// ... there are categories like laugh, grunt, thinking etc that need to be
    /// spoken."* Nothing announced it, so the player could not tell which sample
    /// they were hearing, nor that the axis existed at all.
    /// WHERE IT IS. The F5 dump of the open picker (2026-08-08, CMFRadio12) shows
    /// TWO radio groups. Twelve <c>AtkComponentRadioButton</c>s carry the text
    /// "Type 1".."Type 12" - the voice types, already covered by UpdateVoice. Seven
    /// more carry no text node at all, only images. Those seven are this axis.
    /// WHY IT IS SAFE TO ACT ON. <c>CMFRadio*</c> is the GENERIC radio window - Iris
    /// Size opens <c>CMFRadio2</c> - so "a window with seven icon radio buttons"
    /// would eventually fire somewhere it does not belong. The gate is exact
    /// instead: the labelled group must read back the twelve voice names that THIS
    /// row of <c>CharaMakeType</c> offers. That identifies the window as the voice
    /// picker from game data rather than from its addon name, and it re-proves the
    /// ordering rule in the same pass - if the labelled buttons come out
    /// "Type 1".."Type 12" when sorted by ascending node id, then ascending node id
    /// IS display order in this window, and the icon group can be read the same
    /// way. (The dump agrees: node id 17 is "Type 1" and id 28 is "Type 12".) When
    /// it does not line up, nothing is spoken and the log says exactly what was
    /// found instead.
    /// NAMES COME FROM THE GAME OR NOT AT ALL. The buttons have no text, and /// already ruled out the two sheets that looked like they held these names
    /// (<c>Lobby</c> has only "Type N"; the <c>Addon</c> block that reads
    /// Neutral/Smirking/Thinking belongs to FELLOWSHIP portraits). The one remaining
    /// source of real words is the tooltip the game binds while building the addon -
    /// the same route that named the text-less icon buttons in the Character window
    /// (game-api.md, 2026-07-20). Where there is no tooltip the announcement is the
    /// position alone, which is honest about what is known.
    /// </summary>
    private void UpdateVoiceSample(RowMenus menus)
    {
        var addon = FindVisibleRadioAddon();
        if (addon == null)
        {
            _lastSample = -1;
            _sampleGroupLogged = false;
            return;
        }

        // Split the radio buttons into the labelled group and the icon-only group.
        var labelled = new List<(uint Id, string Text)>();
        var icons    = new List<(uint Id, bool Checked, nint Node)>();
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || (int)node->Type < 1000) continue;
            var comp = ((AtkComponentNode*)node)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.RadioButton) continue;

            var text = ReadComponentText(comp);
            if (text.Length > 0) labelled.Add((node->NodeId, text));
            else                 icons.Add((node->NodeId, ((AtkComponentButton*)comp)->IsChecked, (nint)node));
        }

        labelled.Sort((a, b) => a.Id.CompareTo(b.Id));
        icons.Sort((a, b) => a.Id.CompareTo(b.Id));

        // The gate: this must be the voice picker, and ascending node id must be
        // display order. Both are answered by the same comparison.
        var isVoicePicker = menus.VoiceNames.Length > 0
                         && labelled.Count == menus.VoiceNames.Length
                         && !labelled.Where((e, i) => e.Text != menus.VoiceNames[i]).Any();

        // Log once the picture is STABLE, not on the first frame the window turns
        // visible: the game has not filled the text nodes in by then, so the first
        // frame reports every radio as icon-only. That is what made the 06:12 log
        // read "CMFRadio12: 0 labelled radios [], 19 icon radios" while the gate
        // itself was working correctly 19 ms later.
        if (!_sampleGroupLogged && (isVoicePicker || labelled.Count > 0))
        {
            _sampleGroupLogged = true;
            _log.Info($"[CharaMake] {addon->NameString}: {labelled.Count} labelled radios " +
                      $"[{string.Join(", ", labelled.Select(e => $"{e.Id}:{e.Text}"))}], " +
                      $"{icons.Count} icon radios [{string.Join(", ", icons.Select(e => $"{e.Id}{(e.Checked ? "*" : "")}"))}] " +
                      $"-> voice picker: {isVoicePicker}");
        }

        if (!isVoicePicker || icons.Count < 2) return;

        var idx = icons.FindIndex(e => e.Checked);
        if (idx < 0 || idx == _lastSample) return;
        var first = _lastSample < 0;
        _lastSample = idx;

        // The name only if the GAME has one. Tooltips are bound per node pointer
        // while the addon is built, so this is the live binding, not a table.
        var name = _tooltips.TryGetTooltipDeep((AtkResNode*)icons[idx].Node) ?? string.Empty;
        name = TolkService.Sanitize(name).Trim();
        _log.Info($"[CharaMake] voice sample -> {idx + 1}/{icons.Count} " +
                  $"(node id={icons[idx].Id}, tooltip='{name}')");

        // Not on the first sighting: the picker has just opened and the category
        // list is already saying "Voice, Type 4, 4 of 12" for the same keypress.
        if (first) return;

        // Same buffer as every other value, so it obeys the settle and the
        // interrupt-versus-queue rule with everything else in this step.
        _pendingSlider = AccessibilityStrings.CharaMakeVoiceSample(name, idx + 1, icons.Count);
        _pendingSliderAt = Environment.TickCount64;
    }

    /// <summary>The visible <c>CMFRadio*</c> window, or null. Walks the loaded
    /// units rather than asking by name: the exact name varies per menu
    /// (<c>CMFRadio2</c> for Iris Size, <c>CMFRadio12</c> for Voice), and the
    /// by-name lookup is the trap that already cost a round on CMFSlider.</summary>
    private AtkUnitBase* FindVisibleRadioAddon()
    {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return null;
        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var addon = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (addon == null || !addon->IsVisible) continue;
            if (addon->NameString.StartsWith("CMFRadio", StringComparison.Ordinal)) return addon;
        }
        return null;
    }

    /// <summary>First non-empty text anywhere in a component. Used only to tell a
    /// labelled radio button from an icon-only one.</summary>
    private static string ReadComponentText(AtkComponentBase* comp)
    {
        for (var i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var node = comp->UldManager.NodeList[i];
            if (node == null || node->Type != NodeType.Text) continue;
            var text = AtkText.Read((AtkTextNode*)node).Trim();
            if (text.Length > 0) return text;
        }
        return string.Empty;
    }

    // ── Slider debounce ───────────────────────────────────────────────────────

    private string _pendingSlider = string.Empty;
    private long _pendingSliderAt;

    /// <summary>How long a value has to hold still before it is spoken. Only long
    /// enough to swallow the repeat rate of a held key - the first cut used 350 ms
    /// and the user felt it as *"a long silence between when the slider value
    /// changes and when the value is announced"*.</summary>
    private const int SliderQuietMs = 120;

    /// <summary>
    /// How long a value defers to the windows after one of them opens or closes.
    /// Confirming a picker applies its value AND swaps the window in the same
    /// breath, so the value announcement raced the new window's title and the
    /// button label and won - user 2026-08-08: *"when confirming on height the
    /// player is taken directly to the face select screen, but the window title is
    /// interrupted with the value. the confirm button is also being interrupted
    /// with the value."* Inside this window the value QUEUES instead of
    /// interrupting, so the title reads in full and the value follows it. Sweeping
    /// within one picker never touches this path and stays snappy.
    /// </summary>
    private const int WindowSettleMs = 900;

    /// <summary>Names of the CharaMake windows visible on the last frame. A change
    /// here is what "a screen just advanced" means.</summary>
    private string _windowSignature = string.Empty;
    private long _windowChangedAt;

    /// <summary>Notices a window opening or closing so <see cref="FlushSlider"/>
    /// knows to defer. Cheap: only names, only while the step is on screen.</summary>
    private void TrackWindows()
    {
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return;

        var sb = new StringBuilder();
        var pickers = new List<string>();
        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var addon = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (addon == null || !addon->IsVisible) continue;
            var n = addon->NameString;
            var isPicker = n.StartsWith("CMF", StringComparison.Ordinal);
            if (!isPicker && !n.StartsWith("_CharaMake", StringComparison.Ordinal)) continue;
            if (isPicker) pickers.Add(n);
            sb.Append(n).Append('|');
        }

        var signature = sb.ToString();
        if (signature == _windowSignature) return;
        _windowSignature = signature;
        _windowChangedAt = Environment.TickCount64;
        // A value change caused BY the swap belongs to the new window, not to
        // anything the player did in the old one - drop whatever was pending.
        _pendingSlider = string.Empty;
        _log.Info($"[CharaMake] windows -> {signature}");

        // THE CONFIRM-CHAIN. See _currentMenu: it is written ONLY
        // from a category-button focus, because that is how a picker is normally opened.
        // The guided walk-through opens the next picker by CONFIRMING the current one,
        // so the feature list is never focused, _currentMenu keeps naming the PREVIOUS
        // menu, and IsSideEffect then swallows every value the player changes from there
        // on. That is the whole of the 2026-08-09 report: the Muscle Tone slider "not
        // working" (it worked - log 13:07:20.769 shows 50 -> 51 while suppressed as a
        // side effect "of Height"), and the Face/Skin Color/Hair pickers going silent.
        // The swap is identifiable EXACTLY, with no timing threshold: the set of open
        // PICKER windows goes from one non-empty set to a DISJOINT non-empty set. Backing
        // out to the feature list passes through a picker-free state instead, and merely
        // opening a second picker beside the first keeps the old one in the set. Verified
        // against the recorded session line by line - the three confirm-chains
        // (Slider -> Face, Face -> Skin Color, Skin Color -> Hair) are the only three
        // transitions in it with an empty intersection.
        // Clearing is the right answer rather than guessing the new menu's name: empty is
        // this field's documented "not known yet", under which NOTHING is suppressed. The
        // cost is that during a confirm-chain a game-driven bleed is spoken too - which is
        // the pre-2026-08-08 behaviour, and silence is the failure a blind player cannot
        // detect. The picker's own title cannot substitute: CMFIconHair calls itself
        // "Menu", so there is no label to match on.
        var replaced = _pickers.Count > 0 && pickers.Count > 0 && !pickers.Intersect(_pickers).Any();
        if (replaced && _currentMenu.Length > 0)
        {
            _log.Info($"[CharaMake] picker replaced without returning to the list " +
                      $"({string.Join(",", _pickers)} -> {string.Join(",", pickers)}); " +
                      $"'{_currentMenu}' is no longer the menu the player is in - cleared.");
            _currentMenu = string.Empty;
        }
        _pickers = pickers;
    }

    /// <summary>The picker windows open on the previous signature, for
    /// the confirm-chain test in <see cref="TrackWindows"/>.</summary>
    private List<string> _pickers = new();

    /// <summary>
    /// Speaks the settled slider as ONE sentence: label, value, and the game's own
    /// read-out of what that value means.
    /// The first cut left the game's sentence to the generic text scanner and only
    /// queued the value behind it. That was the wrong shape - two announcements
    /// racing, and the queue put the number a beat late while the scanner's other
    /// fragments cut in anyway. Now the descriptive node is read here and spoken
    /// in the same breath, so there is nothing to race and nothing to queue.
    /// The node is `CMFSlider` id=8, verified from the mod's own scan of the live
    /// window (log 2026-08-08 05:06:34: `[Scan] CMFSlider id=8: 'Approximately
    /// 71.0 inches'`, beside id=7 'Tall' and id=3 'Height'). Sliders without a
    /// read-out simply have nothing there and get the value alone.
    /// </summary>
    private void FlushSlider()
    {
        if (_pendingSlider.Length == 0) return;
        if (Environment.TickCount64 - _pendingSliderAt < SliderQuietMs) return;
        var text = _pendingSlider;
        _pendingSlider = string.Empty;

        var readout = ReadSliderReadout();
        if (readout.Length > 0) text = $"{text}, {readout}";

        // Defer to a window that just opened or closed; otherwise this is the
        // player sweeping a value and the newest one should win immediately.
        if (Environment.TickCount64 - _windowChangedAt < WindowSettleMs) _tolk.Speak(text);
        else _tolk.SpeakInterrupt(text);
    }

    /// <summary>The game's own sentence about the current slider value
    /// ("Approximately 71.0 inches"), or empty when this slider has none.</summary>
    private string ReadSliderReadout()
    {
        // NOT GetAddonByName: the game keeps TWO CMFSlider addons loaded
        // (game-api.md, live addon list "CMFSlider (2x)") and the by-name lookup
        // returns the first, which is the inactive one - that is why the sentence
        // was missing from the 05:19 log while the window plainly showed it. Walk
        // the loaded units and take the visible one that actually has the text.
        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return string.Empty;

        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var addon = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (addon == null || !addon->IsVisible) continue;
            if (addon->NameString != "CMFSlider") continue;

            var node = addon->GetNodeById(8);
            if (node == null || node->Type != NodeType.Text || !node->IsVisible()) continue;
            var text = AtkText.Read((AtkTextNode*)node).Trim();
            if (text.Length > 0) return text;
        }
        return string.Empty;
    }

    // ── Class step ────────────────────────────────────────────────────────────

    private ulong _lastWeapon;
    private bool _haveClass;

    /// <summary>
    /// Names the class the preview model is currently showing.
    /// WHY THE WEAPON: the class icons carry no text (which is what made the step
    /// silent - see UIReaderService.IsCharaMakeIconList), but the game equips the
    /// starting gear of the highlighted class onto the preview model, and
    /// <c>CharaMakeClassEquip</c> is the sheet that says which weapon belongs to
    /// which class. Its eight rows carry eight DISTINCT packed model ids with zero
    /// in the stain bytes (verified 2026-08-08: Gladiator 0x0001000A00C9, Marauder
    /// 0x000100070191, and so on), so a match identifies the class exactly.
    /// It is match-or-silence by construction. If the model has no weapon, or the
    /// game only equips it on confirm rather than on highlight, nothing is spoken
    /// and the log says what the field actually held - the position announcement
    /// from the list reader still covers movement. Nothing here can name the wrong
    /// class.
    /// </summary>
    private void UpdateClass(CsCharacter* model)
    {
        // Low 48 bits only: Id/Type/Variant identify the weapon, the top two bytes
        // are dye stains and are zero in the sheet.
        const ulong ModelMask = 0x0000_FFFF_FFFF_FFFFul;
        var weapon = model->DrawData.Weapon(CsDrawData.WeaponSlot.MainHand).ModelId.Value & ModelMask;

        if (_haveClass && weapon == _lastWeapon) return;
        _lastWeapon = weapon;
        _haveClass = true;

        var name = string.Empty;
        foreach (var row in _data.GetExcelSheet<CharaMakeClassEquip>())
        {
            if ((row.Weapon & ModelMask) != weapon) continue;
            name = row.Class.ValueNullable?.Name.ExtractText() ?? string.Empty;
            break;
        }

        _log.Info($"[CharaMake] class step: main-hand model 0x{weapon:X12} -> " +
                  $"{(name.Length > 0 ? name : "no CharaMakeClassEquip match")}");
        if (name.Length == 0) return;

        _tolk.SpeakInterrupt(AccessibilityStrings.CharaMakeClass(Capitalise(name)));
    }

    /// <summary>The ClassJob sheet stores names lower-case ("gladiator"); the game
    /// capitalises them for display and so does this.</summary>
    private static string Capitalise(string s)
        => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ── Announcing one changed value ──────────────────────────────────────────

    /// <summary>
    /// Announces an eye-colour change, deciding from BOTH
    /// bytes which eye - if either - has to be named.
    /// The rule, and the reason it is a rule rather than a flag: the mod does not
    /// track whether "Odd Eyes" is switched on, because it does not need to. Two
    /// eyes holding the same colour are one fact and get one unlabelled
    /// announcement, exactly as before this change. Two eyes holding DIFFERENT
    /// colours are two facts, and then - and only then - each is named. That is
    /// true whether the player reached the split through Odd Eyes or the game
    /// arrived there some other way, and it adds no words to the far commoner case.
    /// </summary>
    private void AnnounceEyes(RowMenus menus, byte tribe, byte sex, Span<byte> now)
    {
        var right = now[EyeRightByte];
        var left  = now[EyeLeftByte];

        // Linked: one announcement, no eye named. Routed through the Eye Color menu
        // on byte 9 so the palette naming, the side-effect gate and the settle
        // buffer all behave exactly as they did before the pair was split out.
        if (right == left)
        {
            if (right != _last[EyeRightByte])
                Announce(menus, tribe, sex, EyeRightByte, _last[EyeRightByte], right);
            else
                _log.Info($"[CharaMake] eyes level at {right}; the left byte caught up, nothing to say.");
            return;
        }

        // Split. Name whichever actually moved - moving one eye must not re-read the
        // other, which has not changed and which the player is not on.
        if (right != _last[EyeRightByte])
            Announce(menus, tribe, sex, EyeRightByte, _last[EyeRightByte], right, AccessibilityStrings.EyeRight);

        // The left eye has no menu of its own in any of the 32 CharaMakeType rows,
        // so it borrows the Eye Color menu's palette and count by announcing under
        // byte 9's menu with the left byte's VALUE. Both eyes draw from the same
        // shared palette block (CharaMakePalette: `case 9: case 15:` -> the same
        // entry), so this is the same lookup, not an approximation of one.
        if (left != _last[EyeLeftByte])
            Announce(menus, tribe, sex, EyeRightByte, _last[EyeLeftByte], left, AccessibilityStrings.EyeLeft);
    }

    /// <summary>This row's Lip Color menu, or null on a row
    /// that has none - Hrothgar puts Fur Pattern on the same byte and offers no lip
    /// colour at all. See <see cref="LipColorByte"/> for why byte and type identify it
    /// and the label never does.</summary>
    private static Menu? LipColorMenu(RowMenus menus)
        => menus.Menus.FirstOrDefault(m => m.Byte == LipColorByte && m.Type == 2);

    /// <summary>True when this menu is the Lip Color one and
    /// its switch is off, i.e. the swatch it holds is NOT on the face. Every place that
    /// would otherwise name that colour asks here first.</summary>
    private bool IsUnappliedLipColour(Menu menu)
        => menu.Type == 2 && menu.Byte == LipColorByte && !_lipstick;

    /// <summary>
    /// Bit 7 of byte 19 moved: the player switched their lip
    /// colour on or off. It is spoken HERE because no menu owns that bit, so
    /// <see cref="Announce"/> files it under "no menu owns the changed bits" and says
    /// nothing - which is exactly what the user heard.
    /// The sentence is the ordinary one for this menu, from <see cref="DescribeValue"/>,
    /// so switching ON names the colour that has just been applied and switching OFF
    /// names the game's own word for having none. Nothing about the wording is special
    /// to this path; the gate lives in DescribeValue so the summary and the category
    /// focus give the same answer.
    /// </summary>
    private void AnnounceLipstick(RowMenus menus, byte tribe, byte sex, Span<byte> now)
    {
        var lip = LipColorMenu(menus);
        if (lip == null)
        {
            // A row with no lip colour menu (Hrothgar). Nothing can be named, so
            // nothing is said - but it is logged, because the flag moving on a row
            // that has no menu for it would be worth knowing about.
            _log.Info($"[CharaMake] CustomizeData[{LipstickByte}] bit 7 -> " +
                      $"{(_lipstick ? "set" : "clear")} on a row with no Lip Color menu.");
            return;
        }

        // The bleed gate, exactly as Announce applies it: the game clears this flag by
        // itself when a race change rebuilds the body, and that is not news the player
        // asked for while they are working somewhere else. Strg+F10 still reports it.
        if (IsSideEffect(lip.Label))
        {
            _log.Info($"[CharaMake] side effect: {lip.Label} switched " +
                      $"{(_lipstick ? "on" : "off")} while the player is in " +
                      $"'{_currentMenu}'. Not spoken; Strg+F10 reports it.");
            return;
        }

        var text = DescribeValue(lip, tribe, sex, lip.ValueFrom(now[(int)LipColorByte]), withLabel: true);
        _log.Info($"[CharaMake] {lip.Label} switched {(_lipstick ? "on" : "off")}: " +
                  $"{TolkService.Sanitize(text)}");
        _lastSpokenMenu = lip.Label;
        // Queued, not interrupting: the button the player just pressed is announced by
        // the focus reader with an interrupt, and this belongs after it - the same
        // ordering rule the icon grids and the colour pickers already follow.
        _tolk.Speak(text);
    }

    /// <param name="eye">"left eye" / "right eye" when the
    /// two eyes hold DIFFERENT colours and the player is editing one of them alone,
    /// empty otherwise. Set by <see cref="AnnounceEyes"/>; see there for why the
    /// label appears only when the eyes have actually been split.</param>
    private void Announce(RowMenus menus, byte tribe, byte sex, int byteIndex, byte before, byte after,
                          string eye = "")
    {
        // Byte 12 is a bitmask shared by every type-4 menu (facial features,
        // tattoos, limbal ring, ear clasps). The sheet does not say which bit
        // belongs to which menu, so the bit is reported plainly rather than
        // attributed to a menu that might be the wrong one.
        if (byteIndex == 12)
        {
            AnnounceFeatureBits(menus, before, after);
            return;
        }

        // Only the menus whose OWN BITS moved. On a shared byte
        // this is what routes the change to the right one: flipping Iris Size sets
        // bit 7 of byte 16 and leaves the low bits alone, so Eye Shape is filtered
        // out and does not announce a value that did not change.
        var owners = menus.Menus
            .Where(m => m.Byte == (uint)byteIndex && m.Type != 4 && !m.IsVoice
                     && ((before ^ after) & m.Mask) != 0)
            .ToList();
        if (owners.Count == 0)
        {
            _log.Info($"[CharaMake] CustomizeData[{byteIndex}] {before} -> {after}, no menu owns the changed bits.");
            return;
        }
        if (owners.Count > 1)
            _log.Info($"[CharaMake] CustomizeData[{byteIndex}] {before} -> {after} changed bits of {owners.Count} menus " +
                      $"({string.Join(", ", owners.Select(o => o.Label))}); using the first.");

        var menu  = owners[0];

        // The bleed gate - see IsSideEffect. Logged with the raw
        // transition so the log still records every value the game moved.
        if (IsSideEffect(menu.Label))
        {
            _log.Info($"[CharaMake] side effect: {menu.Label} {before} -> {after} " +
                      $"while the player is in '{_currentMenu}'. Not spoken; Strg+F10 reports it.");
            return;
        }

        var value = menu.ValueFrom(after);
        // Keyed on the MENU, not the byte: Eye Shape and Iris Size share byte 16,
        // and keying on the byte would drop the label when the player moved from
        // one to the other.
        var withLabel = _lastSpokenMenu != menu.Label;
        _lastSpokenMenu = menu.Label;

        if (menu.Type is 2 or 3)
            _log.Info($"[CharaMake] {menu.Label} swatch {value + 1}/{menu.Count} " +
                      $"{_palette.HexOrEmpty(menu.Byte, tribe, sex, value)} -> " +
                      $"'{_palette.DescribeSwatch(menu.Byte, tribe, sex, value) ?? "(no palette)"}'");
        else if (menu.Type is 0 or 1 && Array.IndexOf(menu.Values, value) < 0)
            // The value is not one this menu offers. DescribeValue says the change,
            // not a position that would be wrong. Types 0 and 1 only: those are the
            // ones that HAVE an offered list. A slider carries no Values at all, so
            // testing it here would log "not in the offered list" on every step.
            _log.Info($"[CharaMake] {menu.Label}: value {value} (raw byte {after}, mask 0x{menu.Mask:X2}) " +
                      "is not in the offered list.");

        // Sliders used to have their own branch here. They no longer need one:
        // they do NOT interrupt and do not speak every step.
        // User on the first live test: *"slider and setting values are interrupting
        // the prompts that read out what the setting actually says. EG, when
        // adjusting height, the mod tries to read 'Aprox. 71 inches' but is quickly
        // interrupted by '58', the slider value."* The game writes that sentence
        // into the CMFSlider window on every step and it is the more useful of the
        // two - a height in inches beats a position on an abstract 0..100 scale -
        // so FlushSlider appends it and speaks once. The debounce is what makes
        // that safe: holding the key would otherwise stack thirty numbers that keep
        // talking long after the player let go.
        // brief: this is the cursor move. See - the full
        // description belongs on Strg+F10, and composing a six-clause sentence for
        // every arrow press is the expensive half of the job done in the wrong place.
        var text = DescribeValue(menu, tribe, sex, value, withLabel, brief: true);

        // Inside an ICON GRID the focus reader has just said the
        // position, so contribute the description ONLY - see IsIconGridPickerOpen. Type 1
        // is the icon grids; colours and everything else keep the full sentence because
        // nothing else speaks for them.
        // Colour pickers, exactly as the icon grids above:
        // the focus reader has already said the position, so contribute the COLOUR
        // only and queue it. Without this the two race and the position is cut off.
        if (menu.Type is 2 or 3 && IsColorPickerOpen)
        {
            // Off is off inside the picker too. The focus
            // reader has already said the position; what belongs after it is the state
            // of the FACE, and naming a swatch here would describe a colour that is not
            // on it.
            if (IsUnappliedLipColour(menu))
            {
                _lastSpokenMenu = menu.Label;
                _tolk.Speak(LobbyText(NoneLobbyRow));
                return;
            }

            var swatch = _palette.DescribeSwatch(menu.Byte, tribe, sex, value);
            if (swatch == null)
            {
                _log.Info($"[CharaMake] {menu.Label}: colour picker open, no palette for " +
                          $"swatch {value + 1} - the focus reader's position stands alone.");
                return;
            }
            _lastSpokenMenu = menu.Label;
            _tolk.Speak(AccessibilityStrings.CharaMakeColourOnly(
                swatch, (value / CharaMakePalette.ShadesPerRamp) + 1,
                (value % CharaMakePalette.ShadesPerRamp) + 1, eye));
            return;
        }

        if (menu.Type == 1 && IsIconGridPickerOpen)
        {
            var only = IconDescriptionOnly(menu, value, brief: true);
            if (only.Length == 0)
            {
                _log.Info($"[CharaMake] {menu.Label}: icon grid open, no description for " +
                          "this entry - the focus reader's position stands alone.");
                return;
            }
            // QUEUED, not interrupting, and NOT through the settle
            // buffer. User: *"face paint screen is interrupting the index with its
            // description ... don't use an ms settle, use tolk speech queueing ... you
            // can't rely on MS because people will have their speech rates set
            // differently and what might work on my machine won't work for someone who
            // has their speech set to half my speed."*
            // Exactly right, and it is not a tuning problem: the focus reader speaks the
            // position with SpeakInterrupt, so ANY later interrupt cuts it off, and how
            // much of it was lost depends on the listener's speech rate. Tolk_Output with
            // interrupt=false appends to the screen reader's own queue, so the order is
            // guaranteed by the ORDER OF THE CALLS - position first, description second -
            // with no delay to tune and nothing to get wrong at a different rate.
            // Arrowing on to the next entry still interrupts, which is correct: that is a
            // new selection, and the queued tail of the old one should not outlive it.
            _lastSpokenMenu = menu.Label;
            _tolk.Speak(only);
            return;
        }

        // EVERY value goes through the same buffer, not just
        // sliders. Colours and icon grids advance just as fast when a key is held,
        // and they land on the same window swaps when a picker is confirmed - the
        // user hit exactly this on Face after it was fixed for Height. One path,
        // one set of rules: settle for a moment, then interrupt while sweeping or
        // queue while the windows are changing.
        // The radio pickers speak for themselves - see
        // IsRadioPickerOpen. Still logged, so the value is in the log either way.
        if (IsRadioPickerOpen)
        {
            // Both halves of the order question on one line - see
            // RadioOrderIsSheetOrder. `entry` is the position of the value the game has
            // just APPLIED within this menu's offered list; _lastRadioIndex is where the
            // focus reader said the cursor was, 18 ms earlier, on the same key press.
            var entry = Array.IndexOf(menu.Values, value) + 1;
            _log.Info($"[CharaMake] (radio picker open, focus reader speaks it) {text} " +
                      $"| ORDER PROBE: focus said {_lastRadioIndex} of {_lastRadioCount}; " +
                      $"the value the game then applied is entry {entry} of {menu.Count} " +
                      $"in '{menu.Label}'.");
            return;
        }

        // The picker is the normal place an eye is edited and
        // it is handled above; this is the path for a colour the game changed with
        // no picker open. The eye still has to be named when the two differ, or the
        // player is told a colour without being told whose.
        _pendingSlider = string.IsNullOrEmpty(eye) ? text : $"{eye}, {text}";
        _pendingSliderAt = Environment.TickCount64;
    }

    /// <summary>
    /// True when a value the game just changed does NOT belong to
    /// the menu the player is working in - so it must be recorded rather than spoken.
    /// WHY THIS EXISTS. User 2026-08-08: *"hairstyle should only be announced when on
    /// the hairstyle window, not anywhere else. otherwise it gets confusing. hairstyle
    /// change can be announced in the dynamically generated description of the overall
    /// character preview pane."*
    /// The report behind it is NOT a mod defect and that matters for the fix:
    /// landing on Hrothgar face 1 really does re-map the hairstyle byte. Proven from a
    /// settled Strg+F10 summary, not inferred - *"Face, 1 of 4 ... Hairstyle, 2 of
    /// 45"* at 13:03:54.753, against hairstyle 14 with face 4 set. So the sentence was
    /// TRUE; what was wrong was that it interrupted the position the player was
    /// steering by, 150 ms later, as a separate utterance about a menu they were not
    /// in. The value is still read - by <see cref="ReadSummary"/>, which is where the
    /// user asked for it - and it is still logged either way.
    /// IT IS GENERAL, NOT A HAIRSTYLE SPECIAL CASE, on purpose. Testing for "the
    /// hairstyle menu" would mean hardcoding a <c>Lobby</c> label, and those arrive in
    /// the client's language, so the test would silently stop matching on a German or
    /// French client - the exact class of bug the icon-id key in
    /// <see cref="CharaMakeIconText"/> was chosen to avoid. Comparing two labels that
    /// both came out of the same sheet row needs no such assumption. It also covers
    /// the next re-map without another round trip: a face change that moves the
    /// default decals with it is the same shape of event.
    /// WHAT IT CANNOT SUPPRESS BY MISTAKE. The player reaches every picker through a
    /// category button, and that focus is what sets <see cref="_currentMenu"/>, so the
    /// menu they are in is always the one named. Before any category has been focused
    /// the field is empty and this returns false for everything, which leaves the old
    /// behaviour in place rather than risking silence.
    /// KNOWN CONSEQUENCE, stated because it changes what the next test will hear: the
    /// Face change was previously CLOBBERED by the hairstyle one - both bytes move on
    /// the same frame, <see cref="_pendingSlider"/> holds a single value, and byte 6
    /// comes after byte 5 in the loop. So on Hrothgar the face description never
    /// arrived while sweeping the picker. It will now.
    /// </summary>
    private bool IsSideEffect(string menuLabel)
    {
        if (_currentMenu.Length == 0 || menuLabel == _currentMenu) return false;
        _sideEffects.Add(menuLabel);
        return true;
    }

    /// <summary>
    /// Formats one menu's current value. Shared by the change announcements, the
    /// Ctrl+F10 summary and the category list, so a value is described the same way
    /// wherever the player meets it - which is what lets the label be dropped on a
    /// repeat without the sentence changing shape.
    /// </summary>
    /// <param name="withLabel">False while the player is working inside one menu:
    /// "Skin Color" is orientation you need once, not on every one of 192 swatches.
    /// A value the menu does not offer always keeps its label - that announcement
    /// is unusual enough that the player needs to know which menu it came from.</param>
    /// <param name="brief">True on the CURSOR MOVE, where the
    /// authored picture text is cut to its one-or-two-word summary (, the user's
    /// design: *"control+f10 along with the precise 1 or 2 word summary"*). False on
    /// Strg+F10 and on the category focus, which both get the full sentence - /// confirmed the category focus is the right moment for it. Entries with no
    /// summary written fall back to the full text, so Face is unchanged until test
    /// item 1 has been answered.</param>
    private string DescribeValue(Menu menu, byte tribe, byte sex, byte value, bool withLabel,
                                 bool brief = false)
    {
        var label = withLabel ? menu.Label : string.Empty;
        switch (menu.Type)
        {
            case 5: // slider, always 0..100
                return AccessibilityStrings.CharaMakeSlider(menu.Label, value,
                                                    withLabel ? menu.LowLabel : string.Empty,
                                                    withLabel ? menu.HighLabel : string.Empty);

            case 2:
            case 3: // colour palette - the index IS the value
                // The one palette that can be switched off.
                // Naming its swatch would describe a colour the face does not show, and
                // the game's own word is both true and already familiar - it is the
                // button the player pressed. The label is FORCED here for the same
                // reason a value outside the offered list keeps its label below: this
                // announcement is unusual enough that the player needs to know which
                // menu it came from.
                if (IsUnappliedLipColour(menu))
                    return AccessibilityStrings.CharaMakeColourOff(menu.Label, LobbyText(NoneLobbyRow));

                return AccessibilityStrings.CharaMakeColour(
                    label, _palette.DescribeSwatch(menu.Byte, tribe, sex, value),
                    value + 1, menu.Count,
                    (value / CharaMakePalette.ShadesPerRamp) + 1,
                    (value % CharaMakePalette.ShadesPerRamp) + 1);

            default: // 0 = named selector, 1 = icon grid
            {
                var idx  = Array.IndexOf(menu.Values, value);
                var name = idx >= 0 && idx < menu.OptionNames.Length ? menu.OptionNames[idx] : string.Empty;
                // The icon grids have no name in the game data at
                // all, so a position was everything they could say. Where a
                // description has been written for that icon it goes in here; an
                // icon with none returns null and the sentence is unchanged.
                var shape = idx >= 0 && idx < menu.Icons.Length
                    ? (brief ? CharaMakeIconText.Summarize(menu.Icons[idx])
                             : CharaMakeIconText.Describe(menu.Icons[idx]))
                    : null;
                // ...and the TYPE-0 menus, which have no icon at
                // all: those entries are morph targets on the current face's model and
                // are described from the MEASURED vertex deltas (CharaMakeShapeText).
                // Keyed on the face icon plus this menu's CustomizeData byte, so the
                // language of the label never enters into it. Entry 1 is the untouched
                // base mesh and is deliberately absent from that table, which is why
                // the game's own "Type 1" still stands alone there.
                // Keyed on menu.Customize, the SHEET's column, and never on menu.Byte:
                // Iris Size is re-pointed at byte 16 (see IrisSizeCustomize) and would
                // otherwise collide with Eye Shape and read out Eye Shape's text. The
                // sheet keeps them apart at 15 and 16, and 15 is not in the table at
                // all - the game names Iris Size itself.
                if (shape == null && menu.Type == 0 && idx >= 0 && _faceIcon != 0)
                    shape = brief ? CharaMakeShapeText.Summarize(_faceIcon, menu.Customize, idx + 1)
                                  : CharaMakeShapeText.Describe(_faceIcon, menu.Customize, idx + 1);
                // ...and entry 1 is the BASE MESH, which is absent from
                // that table by construction - see AccessibilityStrings.CharaMakeShapeBase. Gated on
                // this menu actually having measured entries (entry 2 present) so it cannot
                // fire on a type-0 menu the table does not cover: Iris Size is the live
                // example, where the GAME names its own entries ("Large"/"Small") and the
                // mod must not talk over that with "unmodified".
                if (shape == null && menu.Type == 0 && idx == 0 && _faceIcon != 0
                    && CharaMakeShapeText.Has(_faceIcon, menu.Customize, 2))
                    shape = AccessibilityStrings.CharaMakeShapeBase;
                return AccessibilityStrings.CharaMakeOption(idx < 0 ? menu.Label : label, name, idx + 1, menu.Count, shape);
            }
        }
    }

    /// <summary>
    /// The ONE sentence the top-level Appearance category list
    /// speaks, built from the category name the global focus reader just resolved.
    /// WHY IT LIVES HERE AND NOT IN THE UI READER: the count does not have to come
    /// from the window at all, and for half the categories it cannot.
    /// <c>CharaMakeType.CharaMakeStruct</c> carries <c>SubMenuNum</c> for every
    /// menu, and this service has already resolved that row for the race/tribe/sex
    /// on screen. Reading it from the addon instead is what produced the bug the
    /// user reported: a type-0 menu opens a <c>CMFRadio*</c> window, which has no
    /// list for <c>FindListInAddon</c> to count, so Jaw / Eye Shape / Iris Size got
    /// a bare name while Face and Skin Color got "N entries" - and "Iris Size77"
    /// when a stray number landed against the empty clause.
    /// Matching is by LABEL because both sides come from the same place: the button
    /// text in <c>_CharaMakeFeature</c> and <c>Menu.Label</c> are both the
    /// <c>Lobby</c> row named by the sheet, so they are the same string in whatever
    /// language the client is running. A focus that is not a menu at all (Confirm,
    /// Cancel, Randomize Appearance) matches nothing and returns empty, and the
    /// caller speaks the plain button text exactly as before.
    /// </summary>
    /// <returns>The sentence, or empty when this label is not one of the menus.</returns>
    public string DescribeCategory(string label)
    {
        if (string.IsNullOrEmpty(label)) return string.Empty;

        var model = FindPreviewModel();
        if (model == null) return string.Empty;

        var src = LiveCustomize(model);
        if (src.Length < CustomizeBytes) return string.Empty;

        var menus = GetMenus(src[0], src[4], src[1]); // Race, Tribe, Sex
        var menu  = menus?.Menus.FirstOrDefault(m => m.Label == label);
        if (menus == null || menu == null) return string.Empty;
        _faceIcon = menus.FaceIcon(src[5]);   // see the field: type-0 lookup key
        _lipstick = (src[LipstickByte] & LipstickFlag) != 0;   // , same reason

        // This focus IS the answer to "which menu is the player
        // in" - see _currentMenu. Set for every menu type including the type-4 and
        // voice branches below, both of which return before the bottom of the method.
        // A focus that is not a menu at all (Confirm, Randomize Appearance) has
        // already returned empty above and correctly leaves the field alone.
        _currentMenu = menu.Label;

        // Voice is the one menu that writes no CustomizeData byte (its Customize
        // column is 0); its value lives on the preview model's Vfx - see
        // UpdateVoice, and the user's 2026-08-08 test confirmed the field really
        // does hold the SELECTED voice.
        if (menu.IsVoice)
        {
            var vidx = Array.IndexOf(menus.Voices, (byte)model->Vfx.VoiceId);
            _lastSpokenMenu = label;
            return AccessibilityStrings.CharaMakeOption(
                label, vidx >= 0 && vidx < menus.VoiceNames.Length ? menus.VoiceNames[vidx] : string.Empty,
                vidx + 1, menus.Voices.Length);
        }

        // Type 4 is the shared bitmask (facial features, tattoos, ear clasps). The
        // sheet does not say which bit belongs to which menu, so there is no single
        // "current value" to name - the count is the honest answer, and the
        // individual toggles still announce themselves via AnnounceFeatureBits.
        if (menu.Type == 4 || menu.Byte >= (uint)CustomizeBytes)
            return AccessibilityStrings.CharaMakeCategory(label, menu.Count);

        // The label has now been spoken for this menu, so the first value change
        // inside the picker does not repeat it - "one announcement of the name per
        // menu option" (user, 2026-08-08).
        _lastSpokenMenu = menu.Label;
        return DescribeValue(menu, src[4], src[1], menu.ValueFrom(src[(int)menu.Byte]), withLabel: true);
    }

    private void AnnounceFeatureBits(RowMenus menus, byte before, byte after)
    {
        var changed = before ^ after;
        var label = menus.Menus.FirstOrDefault(m => m.Type == 4)?.Label ?? AccessibilityStrings.CharaMakeFeatureLabel;
        var only = menus.Menus.Count(m => m.Type == 4) == 1;

        // The same bleed gate as the ordinary values, and it earns
        // its place here twice over: these toggles speak with SpeakInterrupt rather
        // than through the settle buffer, so a face change that flips the default
        // decals with it (CharaMakeType.InitVal is 15 on Hrothgar The Lost male, i.e.
        // FOUR of them on) would cut into the position with up to four utterances in
        // a row. Byte 12 belongs to whichever type-4 menu the player is actually in;
        // when they are in some other menu entirely, nothing here is their doing.
        // Matched against the type-4 labels rather than the single `label` above,
        // because a row can have several type-4 menus and any of them being current
        // makes the change the player's own.
        if (_currentMenu.Length > 0 && !menus.Menus.Any(m => m.Type == 4 && m.Label == _currentMenu))
        {
            _sideEffects.Add(label);
            _log.Info($"[CharaMake] side effect: feature bits {before:X2} -> {after:X2} " +
                      $"while the player is in '{_currentMenu}'. Not spoken; Strg+F10 reports it.");
            return;
        }

        _lastSpokenMenu = label;

        // The preview model can be absent for a frame while the step is loading, and
        // byte 5 must never be indexed out of an empty span - the announcement then falls
        // back to the bare number rather than taking the plugin down mid-creation.
        byte faceValue = 0;
        var previewModel = FindPreviewModel();
        if (previewModel != null)
        {
            var live = LiveCustomize(previewModel);
            if (live.Length > 5) faceValue = live[5];
        }

        for (var bit = 0; bit < 8; bit++)
        {
            if ((changed & (1 << bit)) == 0) continue;
            var on   = (after & (1 << bit)) != 0;
            var name = only ? label : AccessibilityStrings.CharaMakeFeatureLabel;
            // Name the decal where one is written for it. An icon with
            // no authored text still falls back to the bare number, so a future patch's
            // new features stay audible instead of going silent.
            var what = previewModel == null ? null
                     : DescribeFeatureBit(menus, faceValue, bit, brief: true);
            _tolk.SpeakInterrupt(string.IsNullOrEmpty(what)
                ? AccessibilityStrings.CharaMakeFeatureBit(name, bit + 1, on)
                : AccessibilityStrings.CharaMakeFeatureNamed(name, bit + 1, what!, on));
        }
        _log.Info($"[CharaMake] feature bits {before:X2} -> {after:X2}");
        LogFeatureBitProbe(menus, before, after);
    }

    /// <summary>
    /// THE AUDIT PROBE for the one type-4 question the game data
    /// cannot answer. It only writes to the log; nothing here is ever spoken, and it
    /// runs after the announcement so it cannot delay it.
    /// WHAT IS ALREADY SETTLED, OFFLINE, and therefore NOT what this is for:
    /// - every one of the 32 CharaMakeType rows has exactly TWO type-4 menus, one of
    ///   5 entries and one of 2, both on byte 12, and 5 + 2 = 7 = the number of
    ///   FacialFeature bits in that byte and the number of FacialFeatureOption slots
    ///   per face (cmdump menus). The old blocker - "Au Ra wants 5 + 2 + 5 = 12
    ///   against 7 slots" - was arithmetic on a row that does not exist: Au Ra has no
    ///   Facial Features menu at all, it has Limbal Ring (2) plus Other Features (5).
    /// - the 5-entry menu owns FacialFeatureOption slots 1-5 and the 2-entry menu owns
    ///   slots 6-7, read off the icons themselves: Hyur Midlander male's slots 6 and 7
    ///   are tattoos, Elezen Wildwood's are ear clasps, Au Ra Raen's are limbal rings -
    ///   and Elezen and Au Ra list the 2-entry menu FIRST in CharaMakeStruct, which
    ///   rules out menu order as the rule.
    /// WHAT IS NOT SETTLED: that slot i is bit i-1 of byte 12. Both lists are the
    /// game's own 1..7 numbering so the identity mapping is the obvious reading, but
    /// obvious is not measured, and a wrong reading would attach every type-4
    /// description to the wrong toggle.
    /// THE TEST, which takes one minute in game:
    ///   1. Create a character of any race that has a Tattoos, Ear Clasps or Limbal
    ///      Ring menu - i.e. any race at all - and reach the Appearance step.
    ///   2. Focus that TWO-entry menu and toggle its FIRST entry on.
    ///   3. Read the line this writes. If the flipped bit is 5, the 2-entry menu owns
    ///      bits 5-6 and slot i is bit i-1 across the board; the whole mapping follows.
    ///      If it is bit 0, the 2-entry menu owns the LOW bits and the slot order is
    ///      reversed against the bit order.
    ///   4. Toggle the FIVE-entry menu's first entry on as a control: it must flip the
    ///      other end of the byte (bit 0 under the first reading).
    /// </summary>
    private void LogFeatureBitProbe(RowMenus menus, byte before, byte after)
    {
        var model = FindPreviewModel();
        if (model == null) return;
        var src = LiveCustomize(model);
        if (src.Length < CustomizeBytes) return;

        var faceIdx = menus.FaceIndex(src[5]);
        var icons = faceIdx >= 0 && faceIdx < menus.FeatureIcons.Length
            ? menus.FeatureIcons[faceIdx] : Array.Empty<uint>();
        var t4 = menus.Menus.Where(m => m.Type == 4).Select(m => $"'{m.Label}' n={m.Count}").ToList();

        for (var bit = 0; bit < 8; bit++)
        {
            if (((before ^ after) & (1 << bit)) == 0) continue;
            var slot = bit + 1;   // the reading under test, NOT an established fact
            var icon = slot <= icons.Length ? icons[slot - 1] : 0;
            _log.Info($"[CharaMake][bitprobe] bit {bit} ({(((after >> bit) & 1) != 0 ? "on" : "off")}) " +
                      $"while the player is in '{_currentMenu}'. Row menus: {string.Join(" + ", t4)}. " +
                      $"Face entry {faceIdx + 1} (icon {_faceIcon}), its 7 option icons [{string.Join(",", icons)}]. " +
                      $"IF slot == bit+1 then this bit is slot {slot}, icon {icon} - UNVERIFIED, this line is the test.");
        }
    }

    // ── Ctrl+F10: the whole appearance, on demand ─────────────────────────────

    /// <summary>
    /// True while the Appearance step owns the screen, so the read-current-menu
    /// key can route here instead of to the generic focus reader.
    /// </summary>
    public bool IsActive => IsAddonVisible("_CharaMakeFeature");

    // ── Der EINE Eingriff in den Fokus-Leser ──────────────────────────────────

    /// <summary>
    /// Veredelt den Text, den der globale Fokus-Leser gerade sprechen will, WENN der
    /// Fokus im Aussehen-Schritt steht. Sonst gibt sie ihn unveraendert zurueck.
    /// Das ist mit Absicht EIN Aufruf an EINER Stelle in <c>UIReaderService</c>, statt
    /// eines halben Dutzends Sonderfaelle verteilt ueber dessen Fokus-Kette. Alles,
    /// was dieses Feature vom Fokus-Leser braucht, ist der Knoten und der Text, den er
    /// ohne uns sagen wuerde - beides steht an dieser einen Stelle bereits fest.
    /// Zwei Faelle:
    /// <list type="number">
    /// <item>DIE KATEGORIENLISTE (<c>_CharaMakeFeature</c>). Der Knotentext ist das
    ///   nackte Label ("Irisgroesse"); Anzahl und aktuell gesetzter Wert stehen in
    ///   <c>CharaMakeType</c>, das dieser Leser fuer Volk/Stamm/Geschlecht ohnehin
    ///   schon aufgeloest hat. Sie HIER einzufalten - beim einzigen ueberlebenden
    ///   Sprecher - ist das, was daraus EINE Ansage macht statt dreier.</item>
    /// <item>IN EINEM WAEHLER traegt der Optionsname seine eigene Position, der
    ///   Waehler spricht also einmal pro Cursorbewegung statt zweimal (User: *"you can
    ///   just merge the indexing onto the end of the type announcement that is already
    ///   vocalizing"*). Ein Fokus, der kein Radiobutton ist (Bestaetigen, Abbrechen),
    ///   passt auf nichts und behaelt sein Label.</item>
    /// </list>
    /// </summary>
    public unsafe string DescribeFocus(AtkResNode* node, string text)
    {
        if (node == null) return text;

        var addon = FindAddonForNode(node);
        if (addon == null) return text;
        var owner = addon->NameString;

        if (owner == "_CharaMakeFeature")
        {
            if (string.IsNullOrEmpty(text)) return text;
            var described = DescribeCategory(text);
            return described.Length > 0 ? described : text;
        }

        // Nur die Aussehen-Waehler. Die Schritte Schutzgott/Stadt/Klasse sehen genauso
        // aus, werden von diesem Leser aber nicht bedient - dort etwas anzuhaengen
        // hiesse, eine funktionierende Ansage gegen eine halbe zu tauschen.
        if (!owner.StartsWith("CMF", StringComparison.Ordinal)) return text;

        if (!TryReadRadioPosition(node, addon, out var index, out var count)) return text;

        return AccessibilityStrings.CharaMakeOption(
            string.Empty, text, index, count, SummarizeRadioOption(index, count));
    }

    /// <summary>
    /// Position eines Radiobuttons innerhalb seiner Gruppe, oder false.
    /// Die Typ-0-Menues oeffnen ein <c>CMFRadio*</c>-Fenster, dessen Eintraege
    /// Radiobutton-KOMPONENTEN auf oberster Ebene sind - keine Liste, also findet
    /// <c>AtkComponentList</c> hier nichts und es gibt keinen Index abzulesen. Die
    /// Position ist die Stelle des Knotens in der nach Knoten-Id sortierten Gruppe.
    /// Die BESCHRIFTETE und die reine ICON-Gruppe werden getrennt gezaehlt: das
    /// Stimmen-Fenster enthaelt beide (zwoelf benannte Stimmen, sieben namenlose
    /// Hoerproben), und sie zusammenzuwerfen ergaebe "3 von 19" fuer eine Zeile, die in
    /// einer Liste von sieben steht.
    /// </summary>
    private unsafe bool TryReadRadioPosition(AtkResNode* node, AtkUnitBase* addon,
                                             out int index, out int count)
    {
        index = 0;
        count = 0;

        var withText = new List<(uint Id, nint Ptr)>();
        var noText   = new List<(uint Id, nint Ptr)>();
        nint owner = 0;

        // Der Fokus sitzt auf dem Kollisionskind des Knopfes, nicht auf dem Knopf -
        // deshalb wird gegen den Knoten UND seine Vorfahren geprueft.
        var chain = new HashSet<nint>();
        var cur = node;
        for (var up = 0; up < 6 && cur != null; up++)
        {
            chain.Add((nint)cur);
            cur = cur->ParentNode;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var top = addon->UldManager.NodeList[i];
            if (top == null || (int)top->Type < 1000) continue;
            var comp = ((AtkComponentNode*)top)->Component;
            if (comp == null || comp->GetComponentType() != ComponentType.RadioButton) continue;

            var entry = (top->NodeId, (nint)top);
            if (FirstTextInComponent(top).Length > 0) withText.Add(entry);
            else                                     noText.Add(entry);

            if (chain.Contains((nint)top)) owner = (nint)top;
            else
                for (var j = 0; j < comp->UldManager.NodeListCount && owner == 0; j++)
                    if (chain.Contains((nint)comp->UldManager.NodeList[j])) owner = (nint)top;
        }

        if (owner == 0) return false;
        var group = withText.Any(e => e.Ptr == owner) ? withText : noText;
        if (group.Count < 2) return false;

        group.Sort((a, b) => a.Id.CompareTo(b.Id));
        var pos = group.FindIndex(e => e.Ptr == owner);
        if (pos < 0) return false;

        index = pos + 1;
        count = group.Count;
        return true;
    }

    /// <summary>Der Addon, zu dem ein Knoten gehoert: bis zum Wurzelknoten
    /// hochklettern und den geladenen Addon mit derselben Wurzel suchen. Vergleich
    /// ueber Identitaet, kein Raten am Namen.</summary>
    private static unsafe AtkUnitBase* FindAddonForNode(AtkResNode* node)
    {
        if (node == null) return null;

        var root = node;
        var guard = 0;
        while (root->ParentNode != null && guard++ < 64) root = root->ParentNode;

        var mgr = RaptureAtkUnitManager.Instance();
        if (mgr == null) return null;

        for (var i = 0; i < mgr->AllLoadedUnitsList.Count && i < 256; i++)
        {
            var a = mgr->AllLoadedUnitsList.Entries[i].Value;
            if (a != null && a->RootNode == root) return a;
        }
        return null;
    }

    /// <summary>Erster nicht leerer Textknoten in einer Komponente, oder "". Nur
    /// dazu da, eine beschriftete Radiogruppe von einer reinen Icon-Gruppe zu
    /// unterscheiden.</summary>
    private static unsafe string FirstTextInComponent(AtkResNode* compNode)
    {
        if (compNode == null || (int)compNode->Type < 1000) return string.Empty;
        var comp = ((AtkComponentNode*)compNode)->Component;
        if (comp == null) return string.Empty;
        for (var k = 0; k < comp->UldManager.NodeListCount; k++)
        {
            var gc = comp->UldManager.NodeList[k];
            if (gc == null || gc->Type != NodeType.Text) continue;
            var t = AtkText.Read((AtkTextNode*)gc).Trim();
            if (!string.IsNullOrWhiteSpace(t) && t.Length > 1) return t;
        }
        return string.Empty;
    }

    /// <summary>
    /// Reads every appearance value of the character being built. This is the one
    /// thing the game itself offers no way to check: the values are spread over
    /// twenty picker windows and none of them is text.
    /// </summary>
    public void ReadSummary()
    {
        var model = FindPreviewModel();
        if (model == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CharaMakeNoPreview);
            return;
        }

        var src = LiveCustomize(model);
        var race = src[0];
        var sex = src[1];
        var tribe = src[4];
        var menus = GetMenus(race, tribe, sex);
        if (menus == null)
        {
            _tolk.SpeakInterrupt(AccessibilityStrings.CharaMakeNoPreview);
            return;
        }
        _faceIcon = menus.FaceIcon(src[5]);   // see the field: type-0 lookup key
        _lipstick = (src[LipstickByte] & LipstickFlag) != 0;   // , same reason

        // RAW SNAPSHOT, for the Eye Color question. User: *"no matter
        // what I arrow to, even if I press confirm, the mod announces very pale gold,
        // group 2 shade 1 ... it changes if I hit randomize appearance or changes races,
        // but not if I set the eye color."*
        // Two readings fit that, and they need opposite fixes: either the game is not
        // writing the pick to CustomizeData at all (nothing for the mod to read), or it
        // writes somewhere this reader does not look. The diff loop already argues for the
        // first - it logs on ANY byte change in 0..25 and stayed silent - but "no line in
        // the log" is weak evidence and this makes it strong.
        // THE TEST: pick an eye colour, press the description key, pick a different one,
        // press it again, then compare these two lines. Identical bytes mean the game
        // never applied the pick and the fix is not in this reader. A byte that moved names
        // itself, and with it the menu that should own it.
        _log.Info($"[CharaMake][bytes] {string.Join(" ", src.ToArray().Select((b, i) => $"{i}:{b}"))}");

        var sb = new StringBuilder();
        var authored = false;   // did any line use the mod's own picture text?
        // Side effects still owed to the player, ticked off as the
        // menus they belong to are read out. See ReportSideEffects for the rest.
        var owed = new HashSet<string>(_sideEffects);
        foreach (var menu in menus.Menus)
        {
            if (menu.IsVoice) continue;   // announced from Vfx.VoiceId at the end
            if (menu.Type == 4) continue; // bit flags: covered by the toggles themselves
            if (menu.Byte >= (uint)CustomizeBytes) continue;
            var value = menu.ValueFrom(src[(int)menu.Byte]);
            var idx = Array.IndexOf(menu.Values, value);
            if (menu.Type == 1 && idx >= 0 && idx < menu.Icons.Length && CharaMakeIconText.Has(menu.Icons[idx]))
                authored = true;
            // The measured type-0 text is the mod's wording too, so
            // it owes the same note - see CharaMakeShapeText's class comment.
            if (menu.Type == 0 && idx >= 0 && CharaMakeShapeText.Has(_faceIcon, menu.Customize, idx + 1))
                authored = true;
            sb.Append(DescribeValue(menu, tribe, sex, value, withLabel: true));
            // ...and say so where the GAME set this value rather
            // than the player. This is the other half of the bleed fix (IsSideEffect):
            // the change is not allowed to interrupt, so it has to be discoverable
            // here, or a hairstyle that re-mapped itself would be undiscoverable -
            // which is worse than the interruption was.
            if (owed.Remove(menu.Label)) sb.Append(", ").Append(AccessibilityStrings.CharaMakeChangedByGame);
            sb.Append(". ");
        }

        // Voice last: it is the one value that does not come from CustomizeData.
        var voiceIdx = Array.IndexOf(menus.Voices, (byte)model->Vfx.VoiceId);
        if (voiceIdx >= 0)
            sb.Append(AccessibilityStrings.CharaMakeOption(AccessibilityStrings.CharaMakeVoiceLabel,
                                                   voiceIdx < menus.VoiceNames.Length ? menus.VoiceNames[voiceIdx] : string.Empty,
                                                   voiceIdx + 1, menus.Voices.Length)).Append('.');

        // Anything the loop above could not carry - the type-4 bit
        // menus are skipped there, so a decal set the game flipped has no line of its
        // own. Named without a value, which is all that can be said honestly: the
        // sheet does not say which bit belongs to which type-4 menu.
        foreach (var label in owed)
            sb.Append(label).Append(", ").Append(AccessibilityStrings.CharaMakeChangedByGame).Append(". ");

        // News exactly once. The value stays whatever the game made it, and the line
        // above will keep reading it out; what expires here is the claim that it
        // CHANGED, which stops being informative after the player has been told.
        _sideEffects.Clear();

        if (authored) sb.Append(' ').Append(AccessibilityStrings.CharaMakeAuthoredNote);

        var text = sb.ToString().Trim();
        _log.Info($"[CharaMake] Summary: {TolkService.Sanitize(text)}");
        _tolk.SpeakInterrupt(text.Length == 0 ? AccessibilityStrings.CharaMakeNoPreview : text);
    }

    // ── Resolving the sheet row ───────────────────────────────────────────────

    /// <summary>
    /// Builds (and caches) the menu list for one race/tribe/sex from
    /// <c>CharaMakeType</c>. Labels and per-entry names come from the <c>Lobby</c>
    /// sheet through IDataManager, so they arrive in the client's language and no
    /// text is hardcoded here.
    /// </summary>
    private RowMenus? GetMenus(byte race, byte tribe, byte sex)
    {
        var key = (race, tribe, sex);
        if (_menuCache.TryGetValue(key, out var cached)) return cached;

        var sheet = _data.GetExcelSheet<CharaMakeType>();
        var custom = _data.GetExcelSheet<CharaMakeCustomize>();
        CharaMakeType? found = null;
        foreach (var row in sheet)
        {
            if (row.Race.RowId != race || row.Tribe.RowId != tribe || row.Gender != sex) continue;
            found = row;
            break;
        }
        if (found == null)
        {
            _log.Warning($"[CharaMake] No CharaMakeType row for race={race} tribe={tribe} sex={sex}.");
            return null;
        }

        var type = found.Value;
        var result = new RowMenus
        {
            Voices = type.VoiceStruct.ToArray(),
        };

        // The type-4 pictures, one struct of seven icon ids per face.
        // See RowMenus.FeatureIcons for what is measured about them and what is not.
        var opts = new uint[8][];
        for (var f = 0; f < 8; f++)
        {
            var o = type.FacialFeatureOption[f];
            opts[f] = new[] { (uint)o.Option1, (uint)o.Option2, (uint)o.Option3, (uint)o.Option4,
                              (uint)o.Option5, (uint)o.Option6, (uint)o.Option7 };
        }
        result.FeatureIcons = opts;

        foreach (var m in type.CharaMakeStruct)
        {
            var label = m.Menu.ValueNullable?.Text.ExtractText() ?? string.Empty;
            if (string.IsNullOrEmpty(label)) continue;

            var count = m.SubMenuNum;
            var menu = new Menu
            {
                Label = label,
                Type = m.SubMenuType,
                Count = count,
                Customize = m.Customize,
                Byte = m.Customize,
            };

            // Where the sheet's byte is not the whole story.
            if (m.Customize == IrisSizeCustomize)
            {
                menu.Byte = 16;      // SmallIris lives with EyeShape
                menu.Mask = 0x80;
            }
            else if (LowSevenBitBytes.Contains(m.Customize))
            {
                menu.Mask = 0x7F;    // the flag in bit 7 belongs to another menu
            }

            if (m.SubMenuType == 5)
            {
                // Slider: params 0 and 1 are Lobby rows for the two end labels,
                // params 2 and 3 the range (always 0..100 in this build).
                menu.LowLabel = LobbyText(m.SubMenuParam[0]);
                menu.HighLabel = LobbyText(m.SubMenuParam[1]);
            }
            else if (m.SubMenuType is 0 or 1)
            {
                var names = new string[count];
                var values = new byte[count];
                var icons = new uint[count];
                for (var i = 0; i < count && i < m.SubMenuParam.Count; i++)
                {
                    var param = m.SubMenuParam[i];
                    var gfx = i < m.SubMenuGraphic.Count ? m.SubMenuGraphic[i] : (byte)0;

                    if (m.SubMenuType == 0)
                    {
                        // Every entry has a name: the param is a Lobby row.
                        names[i] = LobbyText(param);
                        values[i] = gfx;
                        // Deliberately NO icon here. A type-0
                        // param is a Lobby row, and CharaMakeCustomize has rows with
                        // the SAME ids that are HAIRSTYLES - Jaw's params 1050-1053
                        // resolve to hairstyle thumbnails. Looking one up would
                        // describe a player's jaw as shoulder-length wavy hair.
                    }
                    else if (custom.TryGetRow(param, out var cmc))
                    {
                        // Icon grid: the param is a CharaMakeCustomize row and the
                        // value written is its FeatureID (NOT the row id - e.g.
                        // hairstyle param 85 writes 178).
                        names[i] = string.Empty;
                        values[i] = cmc.FeatureID;
                        icons[i] = cmc.Icon;
                    }
                    else
                    {
                        // Face is the exception: its params are raw icon ids with
                        // no CharaMakeCustomize row, and the value is the graphic.
                        // Tail Shape, Fur Pattern and Viera Ear Shape are the same
                        // family (verified offline 2026-08-08).
                        names[i] = string.Empty;
                        values[i] = gfx;
                        icons[i] = param;
                    }
                }
                menu.OptionNames = names;
                menu.Values = values;
                menu.Icons = icons;
            }

            result.Menus.Add(menu);

            if (m.SubMenuType == 0 && m.Customize == 0)
            {
                menu.IsVoice = true;
                // The Voice menu is the only type-0 menu that writes no
                // CustomizeData byte; its params name the 12 voices.
                result.VoiceNames = menu.OptionNames;
            }
        }

        _menuCache[key] = result;
        _log.Info($"[CharaMake] race={race} tribe={tribe} sex={sex}: {result.Menus.Count} menus, " +
                  $"{result.Voices.Length} voices, palette={( _palette.IsAvailable ? "loaded" : "MISSING")}");
        return result;
    }

    private string LobbyText(uint rowId)
        => _data.GetExcelSheet<Lobby>().TryGetRow(rowId, out var row) ? row.Text.ExtractText() : string.Empty;

    // ── Finding the model on screen ───────────────────────────────────────────

    private bool IsAddonVisible(string name)
    {
        var ptr = _gui.GetAddonByName(name);
        return !ptr.IsNull && ((AtkUnitBase*)(nint)ptr)->IsVisible;
    }

    /// <summary>
    /// The customize bytes the game is actually RENDERING.
    /// The first cut read <c>Character.DrawData.CustomizeData</c> and it did not
    /// move: on the first live test (log 2026-08-08 05:06:34-37) the height slider
    /// stepped 57 - 58 - 57 - 58, the game's own readout followed it
    /// ("Approximately 71.0 inches" / "71.1 inches"), and this service announced
    /// nothing at all - the only [CharaMake] lines in the whole run are the menu
    /// resolve and the voice. The character-level copy is evidently not what the
    /// picker writes while a value is being previewed.
    /// <c>Human.Customize</c> on the DRAW OBJECT is the buffer the model is built
    /// from, so it cannot lag what is on screen. Falls back to the character-level
    /// copy whenever the draw object is not a Human, which keeps behaviour for
    /// anything unexpected rather than going silent.
    /// </summary>
    private static Span<byte> LiveCustomize(CsCharacter* model)
    {
        var draw = ((CsGameObject*)model)->DrawObject;
        if (draw != null && draw->Object.GetObjectType() == ObjectType.CharacterBase)
        {
            var cbase = (CharacterBase*)draw;
            if (cbase->GetModelType() == CharacterBase.ModelType.Human)
                return ((Human*)draw)->Customize.Data;
        }
        return model->DrawData.CustomizeData.Data;
    }

    /// <summary>
    /// The single visible preview model, or null. Same rule the race/gender reader
    /// uses (verified 2026-07-10: 32 Pc objects at indices 200-231, exactly one
    /// with DrawObject.IsVisible). Silent when the count is not exactly one - a
    /// second visible model would mean the reading is not trustworthy.
    /// </summary>
    private CsCharacter* FindPreviewModel()
    {
        CsCharacter* found = null;
        var visible = 0;
        foreach (var obj in _objects)
        {
            if (obj.Address == nint.Zero) continue;
            var chara = (CsCharacter*)obj.Address;
            if (chara->ObjectKind != CsObjectKind.Pc) continue;

            var go = (CsGameObject*)obj.Address;
            var draw = go->DrawObject;
            if (draw == null || !draw->IsVisible) continue;

            visible++;
            found = chara;
        }
        return visible == 1 ? found : null;
    }
}
