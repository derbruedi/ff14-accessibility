using System.Text;
using cmdump;
using Lumina;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.Sheets;

const string SqPack = @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack";

var gd = new GameData(SqPack, new LuminaOptions { PanicOnSheetChecksumMismatch = false, DefaultExcelLanguage = Language.English });

string mode = args.Length > 0 ? args[0] : "menus";
var w = new StringWriter();

// The 18 body codes a face model can live under; see ResolveFaces for why the row's
// code has to be searched rather than derived.
string[] BodyCodes = { "0101", "0201", "0301", "0401", "0501", "0601", "0701", "0801",
                       "0901", "1001", "1101", "1201", "1301", "1401", "1501", "1601",
                       "1701", "1801" };

// The CustomizeData byte each type-0 menu writes -> the shape prefix on the face model.
// Keyed by the BYTE, never by the menu label: labels arrive in the client's language.
// (Byte 15 is Iris Size, which the game names itself and which is deliberately absent.)
Dictionary<uint, string> ShapePrefix = new()
{
    [14] = "brw",   // Eyebrows
    [16] = "eye",   // Eye Shape
    [17] = "nse",   // Nose
    [18] = "chk",   // Jaw
    [19] = "mth",   // Mouth - and Fang Length on Hrothgar, which has no Mouth menu
    [22] = "etc",   // Ear Shape (Elezen, Lalafell). Type 1 on Viera and not this family.
};

switch (mode)
{
    case "menus": DumpMenus(); break;
    case "faceprobe": FaceProbe(); break;
    case "shapes": Shapes(args.Length > 1 ? args[1] : "chara/human/c0101/obj/face/f0001/model/c0101f0001_fac.mdl"); break;
    case "mdlraw": MdlRaw(args[1]); break;
    case "shapecheck": ShapeCheck(); break;
    case "icons": IconTable(args[1]); break;
    case "names": MenuNames(args[1]); break;
    case "features": Features(); break;
    case "featicons": FeatureIcons(); break;
    case "mdl6": Mdl6(args[1]); break;
    case "facemodels": FaceModels(); break;
    case "facecmp": FaceCmp(); break;
    case "shapedump": ShapeDump(); break;
    case "facetex": FaceTex(args[1], args[2]); break;
    case "strings": RawStrings(args[1]); break;
    case "lobby": LobbyRows(uint.Parse(args[1]), uint.Parse(args.Length > 2 ? args[2] : args[1])); break;
    case "bands": PaletteBands(); break;
    case "tex": OneTex(args[1], args[2]); break;
}

Console.Out.Write(w.ToString());
return;

void DumpMenus()
{
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    var lobby = gd.GetExcelSheet<Lobby>()!;
    foreach (var row in sheet)
    {
        string race = row.Race.ValueNullable?.Masculine.ExtractText() ?? "?";
        string tribe = row.Tribe.ValueNullable?.Masculine.ExtractText() ?? "?";
        w.WriteLine($"== row {row.RowId}: {race} / {tribe} / gender={row.Gender}");
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            string label = m.Menu.ValueNullable?.Text.ExtractText() ?? "";
            if (m.SubMenuNum == 0 && string.IsNullOrEmpty(label)) continue;
            var ps = new List<uint>();
            for (int p = 0; p < Math.Min((int)m.SubMenuNum, 100); p++) ps.Add(m.SubMenuParam[p]);
            var gs = new List<byte>();
            for (int g = 0; g < Math.Min((int)m.SubMenuNum, 10); g++) gs.Add(m.SubMenuGraphic[g]);
            w.WriteLine($"  [{i,2}] '{label}' type={m.SubMenuType} n={m.SubMenuNum} cust={m.Customize} init={m.InitVal} mask=0x{m.SubMenuMask:X} gfx=[{string.Join(",", gs)}] params=[{string.Join(",", ps)}]");
        }
    }
}

void FaceProbe()
{
    // Which face models actually exist per race/gender code? c<race><gender>f<face>_fac.mdl
    string[] codes = { "0101", "0201", "0301", "0401", "0501", "0601", "0701", "0801",
                       "0901", "1001", "1101", "1201", "1301", "1401", "1501", "1601",
                       "1701", "1801" };
    foreach (var c in codes)
    {
        var found = new List<int>();
        for (int f = 0; f <= 220; f++)
        {
            string p = $"chara/human/c{c}/obj/face/f{f:D4}/model/c{c}f{f:D4}_fac.mdl";
            if (gd.FileExists(p)) found.Add(f);
        }
        if (found.Count > 0) w.WriteLine($"c{c}: {string.Join(",", found)}");
    }
}

void Shapes(string path)
{
    if (!gd.FileExists(path)) { w.WriteLine($"MISSING {path}"); return; }
    var mdl = gd.GetFile<MdlFile>(path)!;
    w.WriteLine($"{path}");
    w.WriteLine($"  meshes={mdl.Meshes.Length} shapes={mdl.Shapes.Length} shapeMeshes={mdl.ShapeMeshes.Length} shapeValues={mdl.ShapeValues.Length}");
    foreach (var s in mdl.Shapes)
    {
        string name = ReadStr(mdl.Strings, s.StringOffset);
        w.WriteLine($"  shape '{name}' meshStart=[{string.Join(",", s.ShapeMeshStartIndex)}] meshCount=[{string.Join(",", s.ShapeMeshCount)}]");
    }
}

// Print a range of Lobby rows with their ids. Every label and
// every entry name in the appearance step comes out of this sheet, so a row id
// quoted in the notes gets checked HERE before any code is written against it -
// a row id is not proof on its own, sheets collide on ids.
void LobbyRows(uint from, uint to)
{
    var lobby = gd.GetExcelSheet<Lobby>()!;
    for (var id = from; id <= to; id++)
    {
        if (!lobby.TryGetRow(id, out var row)) continue;
        var text = row.Text.ExtractText();
        if (text.Length > 0) w.WriteLine($"{id}\t{text}");
    }
}

void RawStrings(string path)
{
    if (!gd.FileExists(path)) { w.WriteLine($"MISSING {path}"); return; }
    var d = gd.GetFile(path)!.Data;
    var cur = new List<byte>();
    foreach (var b in d)
    {
        if (b >= 0x20 && b < 0x7F) cur.Add(b);
        else { if (cur.Count >= 6) w.WriteLine("  " + Encoding.ASCII.GetString(cur.ToArray())); cur.Clear(); }
    }
}

void OneTex(string path, string name)
{
    string outDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "..", "..", "..", "..", "icons", "facetex");
    Directory.CreateDirectory(outDir);
    if (!gd.FileExists(path)) { w.WriteLine($"MISSING {path}"); return; }
    var tex = gd.GetFile<Lumina.Data.Files.TexFile>(path)!;
    using var st = File.Create(Path.Combine(outDir, name + ".raw"));
    using var bw = new BinaryWriter(st);
    bw.Write(tex.Header.Width); bw.Write(tex.Header.Height); bw.Write(tex.ImageData);
    w.WriteLine($"{path} -> {name}.raw  {tex.Header.Width}x{tex.Header.Height}");
}

// Pull the diffuse face textures for one body code so the thumbnails' markings
// can be checked against what the face itself carries.
void FaceTex(string code, string faces)
{
    string outDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "..", "..", "..", "..", "icons", "facetex");
    Directory.CreateDirectory(outDir);
    foreach (var fs in faces.Split(','))
    {
        int f = int.Parse(fs);
        string[] cands =
        {
            $"chara/human/c{code}/obj/face/f{f:D4}/texture/--c{code}f{f:D4}_fac_d.tex",
            $"chara/human/c{code}/obj/face/f{f:D4}/texture/c{code}f{f:D4}_fac_d.tex",
        };
        string hit = cands.FirstOrDefault(gd.FileExists);
        if (hit == null) { w.WriteLine($"# no diffuse for c{code} f{f:D4}"); continue; }
        var tex = gd.GetFile<Lumina.Data.Files.TexFile>(hit)!;
        using var st = File.Create(Path.Combine(outDir, $"c{code}f{f:D4}.raw"));
        using var bw = new BinaryWriter(st);
        bw.Write(tex.Header.Width); bw.Write(tex.Header.Height); bw.Write(tex.ImageData);
        w.WriteLine($"{hit}  {tex.Header.Width}x{tex.Header.Height}");
    }
}

// Does the Face thumbnail show anything the Facial Features menu can take away?
// FacialFeatureOption is 8 structs of 7 ints - one per FACE index - and the
// Facial Features menu carries an InitVal bitmask of which of those are on when
// the step opens. If InitVal is 0 the thumbnail shows a bare face and everything
// visible on it belongs to the Face entry itself.
void Features()
{
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    foreach (var row in sheet)
    {
        string who = $"{row.Race.ValueNullable?.Masculine.ExtractText()} {row.Tribe.ValueNullable?.Masculine.ExtractText()} {(row.Gender == 0 ? "m" : "f")}";
        byte init = 0; byte n = 0; bool found = false;
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            if (m.SubMenuType == 4 && (m.Menu.ValueNullable?.Text.ExtractText() ?? "") == "Facial Features")
            { init = m.InitVal; n = m.SubMenuNum; found = true; break; }
        }
        var opts = new List<string>();
        for (int f = 0; f < 8; f++)
        {
            var o = row.FacialFeatureOption[f];
            int[] v = { o.Option1, o.Option2, o.Option3, o.Option4, o.Option5, o.Option6, o.Option7 };
            if (v.All(x => x == 0)) continue;
            opts.Add($"face{f + 1}:[{string.Join(",", v)}]");
        }
        w.WriteLine($"{row.RowId,2} {who,-34} FacialFeatures {(found ? $"n={n} init={init}" : "ABSENT")}  {string.Join(" ", opts)}");
    }
}

// Which face MODEL does a CharaMakeType row's face entry N use?
// Nothing in the sheet says. The Face menu's SubMenuGraphic is the CustomizeData byte
// (1..7, and 5..8 on Hrothgar female), and the model lives at
// chara/human/c<code>/obj/face/f<gfx+offset>/model/c<code>f<gfx+offset>_fac.mdl - but
// which of the 18 body codes, and what offset, is not written down: Elezen Wildwood and
// Duskwight SHARE code c0501 and differ only by a +100 face offset, while Hyur
// Midlander and Highlander get separate codes (c0101 vs c0301) with the SAME graphic
// values. So it is SEARCHED and the answer is only accepted when it is unique.
// The test is the one that proved the shape-key mapping in the first place: for every
// type-0 menu the row offers, the number of shapes carrying that menu's prefix must be
// exactly SubMenuNum - 1, on EVERY face the row offers. Six menus x four to seven faces
// is far too much agreement to hit by accident, and a candidate that fails any of it is
// rejected outright rather than ranked.
string FacePath(string code, int face) => $"chara/human/c{code}/obj/face/f{face:D4}/model/c{code}f{face:D4}_fac.mdl";

Dictionary<string, int> ShapeCounts(string code, int face)
{
    var d = new Dictionary<string, int>();
    foreach (var n in ModelStrings(FacePath(code, face)))
        if (n.StartsWith("shp_") && n.Length >= 8)
        {
            var pre = n.Substring(4, 3);
            d[pre] = d.GetValueOrDefault(pre) + 1;
        }
    return d;
}

// (row -> code, offset, faces[]) for every CharaMakeType row. Shared by facemodels and
// shapedump so the resolution is done once and reported the same way.
List<(uint Row, string Who, string Code, int Offset, List<(int Gfx, uint Icon)> Faces, List<(uint Byte, int N, string Label)> Type0)> ResolveFaces()
{
    var outp = new List<(uint, string, string, int, List<(int, uint)>, List<(uint, int, string)>)>();
    var cands = new List<(uint Row, string Who, uint Race, uint Tribe, sbyte Gender, string Sig,
                          List<(string Code, int Off, int Hair, bool Exact)> Hits, int HairTotal,
                          List<(int, uint)> Faces, List<(uint, int, string)> Type0)>();
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    foreach (var row in sheet)
    {
        string race = row.Race.ValueNullable?.Masculine.ExtractText() ?? "?";
        string tribe = row.Tribe.ValueNullable?.Masculine.ExtractText() ?? "?";
        string who = $"{race} {tribe} {(row.Gender == 0 ? "male" : "female")}";
        var faces = new List<(int, uint)>();
        var type0 = new List<(uint, int, string)>();
        var hairIds = new List<int>();
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            if (m.SubMenuNum == 0) continue;
            string label = m.Menu.ValueNullable?.Text.ExtractText() ?? "";
            if (label.Length == 0) continue;
            if (m.SubMenuType == 1 && m.Customize == 5)
                for (int e = 0; e < m.SubMenuNum; e++) faces.Add((m.SubMenuGraphic[e], m.SubMenuParam[e]));
            if (m.SubMenuType == 1 && m.Customize == 6)
                for (int e = 0; e < m.SubMenuNum; e++)
                {
                    var c = gd.GetExcelSheet<CharaMakeCustomize>()!.GetRowOrDefault(m.SubMenuParam[e]);
                    if (c.HasValue && c.Value.FeatureID != 0) hairIds.Add(c.Value.FeatureID);
                }
            // The LABEL is carried through because byte 19 is two different menus:
            // "Mouth" everywhere, "Fang Length" on Hrothgar, which has no Mouth menu.
            // They want different words for the same measurement (a taller mouth is
            // fuller lips; a taller fang is a longer fang), and the sheet's English
            // label is the only thing that tells them apart.
            if (m.SubMenuType == 0 && ShapePrefix.ContainsKey(m.Customize)) type0.Add((m.Customize, (int)m.SubMenuNum, label));
        }
        if (faces.Count == 0) { w.WriteLine($"# {who}: no Face menu"); continue; }

        // THE FACE-ID OFFSET, and why it is read off the TRIBE.
        // A body code can carry two face bands: c0501 (Elezen male) has f0001-0004 AND
        // f0101-0104, and they are NOT the same models (cmdump facecmp: every pair
        // differs in size). Both Elezen tribes sit on c0501, so one band belongs to
        // each. Which one is forced by the only case where the data leaves no choice:
        // Hyur Highlander is tribe 2 and its body code c0301 contains ONLY the 101 band
        // (cmdump faceprobe), while its Face menu's SubMenuGraphic is [1,2,3,4]. So a
        // tribe-2 row reads its faces from the +100 band wherever that band exists.
        // Every race whose second tribe has no such band - Au Ra, Hrothgar, Viera -
        // keeps offset 0, which is also what their shared face icons say (61a measured
        // Raen and Xaela face icons byte-for-byte identical).
        // Tribe row ids run 1..16 in race order, so an EVEN id is the second tribe.
        bool secondTribe = row.Tribe.RowId % 2 == 0;

        var hits = new List<(string Code, int Off, int Hair, bool Exact)>();
        foreach (var code in BodyCodes)
            foreach (var off in secondTribe ? new[] { 100, 0 } : new[] { 0 })
            {
                if (!faces.All(f => gd.FileExists(FacePath(code, f.Item1 + off)))) continue;
                // A second-tribe row only falls back to offset 0 when the +100 band
                // does not exist under this code at all.
                if (secondTribe && off == 0 && faces.All(f => gd.FileExists(FacePath(code, f.Item1 + 100)))) continue;
                // The count test, relaxed to ">=" after a measured exception: Miqo'te
                // male offers 5 Eyebrows entries (so 4 shapes) but c0701 carries FIVE
                // shp_brw_*. game-api.md's "always exactly SubMenuNum - 1" is therefore
                // not exact; a model can carry a shape the menu does not offer. The
                // surplus is reported by `facemodels` so it is visible rather than
                // silently absorbed.
                bool ok = true;
                foreach (var (gfx, _) in faces)
                {
                    var counts = ShapeCounts(code, gfx + off);
                    foreach (var (b, n, _) in type0)
                        if (counts.GetValueOrDefault(ShapePrefix[b]) < n - 1) { ok = false; break; }
                    if (!ok) break;
                }
                if (!ok) continue;
                // Tie-break on the row's HAIRSTYLES, which are per body code and 27 to
                // 54 entries long: count how many of them have a model under this code.
                // Scored rather than all-or-nothing because a handful of ids genuinely
                // have no model under any code, and a single one of those would
                // otherwise throw away the right answer (it did: Miqo'te scored zero
                // candidates under an all-or-nothing test).
                int hair = hairIds.Count(h => gd.FileExists($"chara/human/c{code}/obj/hair/h{h:D4}/model/c{code}h{h:D4}_hir.mdl"));
                // An EXACT count match (every prefix exactly SubMenuNum - 1) is worth
                // more than a surplus one and is ranked first; a surplus is tolerated
                // only because Miqo'te and Highlander genuinely have one.
                bool exact = faces.All(f => type0.All(t => ShapeCounts(code, f.Item1 + off).GetValueOrDefault(ShapePrefix[t.Item1]) == t.Item2 - 1));
                hits.Add((code, off, hair, exact));
            }
        if (hits.Count == 0) { w.WriteLine($"# NO CANDIDATE {who}"); continue; }
        // The row's own shape: how many faces it offers and how many entries each
        // type-0 menu has. Two rows may only be pointed at the SAME models when these
        // agree - Raen and Xaela do (4 faces, identical menus, and 61a measured their
        // face icons byte-for-byte identical), Midlander and Highlander do not
        // (7 faces vs 4, Eyebrows 6 vs 4).
        string sig = $"{string.Join(",", faces.Select(f => f.Item1))}|{string.Join(",", type0.Select(t => $"{t.Item1}:{t.Item2}"))}";
        cands.Add((row.RowId, who, row.Race.RowId, row.Tribe.RowId, row.Gender, sig, hits, hairIds.Count, faces, type0));
    }

    // ── GLOBAL ASSIGNMENT. Per-row "best hair score" is NOT enough on its own: a
    // wrong body code can carry every one of a row's hairstyles (Miqo'te male's 47 all
    // exist under c0101, c0501 and c0701 alike). What breaks those ties is a fact
    // about the whole table rather than about one row: there are 18 body codes and
    // each belongs to exactly ONE (race, sex) - both tribes of a race share a code
    // except Hyur, whose Midlander and Highlander are separate codes. So the codes are
    // allocated globally, taking the rows with the clearest evidence first (the
    // largest gap between their best and second-best hair score) and refusing any code
    // already claimed by a different race or sex.
    var claimed = new Dictionary<string, (uint Race, sbyte Gender, string Sig)>();
    foreach (var c in cands.OrderByDescending(c =>
    {
        var s = c.Hits.OrderByDescending(h => h.Exact).ThenByDescending(h => h.Hair).ToList();
        return s.Count > 1 && s[0].Exact == s[1].Exact ? s[0].Hair - s[1].Hair : int.MaxValue;
    }))
    {
        var ranked = c.Hits.OrderByDescending(h => h.Exact).ThenByDescending(h => h.Hair).ToList();
        var pick = ranked.FirstOrDefault(h => !claimed.TryGetValue($"{h.Code}+{h.Off}", out var o)
                                           || (o.Race == c.Race && o.Gender == c.Gender && o.Sig == c.Sig));
        if (pick.Code == null) { w.WriteLine($"# NO FREE CODE for {c.Who}"); continue; }
        claimed[$"{pick.Code}+{pick.Off}"] = (c.Race, c.Gender, c.Sig);
        var runners = ranked.Where(h => h.Code != pick.Code || h.Off != pick.Off).ToList();
        w.WriteLine($"# {c.Who} (tribe {c.Tribe}, offset {pick.Off}): c{pick.Code} hair {pick.Hair}/{c.HairTotal}" +
                    $"{(pick.Exact ? "" : " SURPLUS-SHAPES")}" +
                    (runners.Count > 0 ? $", next c{runners[0].Code}+{runners[0].Off} {runners[0].Hair}{(runners[0].Exact ? "" : " surplus")}" : ", sole candidate"));
        outp.Add((c.Row, c.Who, pick.Code, pick.Off, c.Faces, c.Type0));
    }
    w.WriteLine($"# {claimed.Keys.Select(k => k.Split('+')[0]).Distinct().Count()}/18 body codes used, " +
                $"{claimed.Count} distinct (code, offset) face sets, {outp.Count}/32 rows resolved");
    return outp.OrderBy(x => x.Item1).ToList();
}

// Does the +100 face band actually hold DIFFERENT models from the 1..N band? If the
// two are byte-identical the tribe-to-offset question cannot change a description and
// does not have to be settled; if they differ it has to be.
void FaceCmp()
{
    foreach (var code in BodyCodes)
        for (int f = 1; f <= 8; f++)
        {
            string a = FacePath(code, f), b = FacePath(code, f + 100);
            if (!gd.FileExists(a) || !gd.FileExists(b)) continue;
            var da = gd.GetFile(a)!.Data; var db = gd.GetFile(b)!.Data;
            bool same = da.Length == db.Length && da.SequenceEqual(db);
            w.WriteLine($"c{code} f{f:D4} vs f{f + 100:D4}: {(same ? "IDENTICAL" : $"DIFFER ({da.Length} vs {db.Length} bytes)")}");
        }
}

void FaceModels()
{
    foreach (var (rowId, who, code, off, faces, type0) in ResolveFaces())
    {
        var surplus = new List<string>();
        foreach (var (gfx, _) in faces)
        {
            var counts = ShapeCounts(code, gfx + off);
            foreach (var (b, n, _) in type0)
            {
                int have = counts.GetValueOrDefault(ShapePrefix[b]);
                if (have != n - 1) surplus.Add($"f{gfx + off:D4} {ShapePrefix[b]}:{have} vs menu {n}-1");
            }
        }
        w.WriteLine($"{rowId}\t{who}\tc{code}\toffset={off}\tfaces=[{string.Join(",", faces.Select(f => $"{f.Gfx}->f{f.Gfx + off:D4}#{f.Icon}"))}]" +
                    $"\ttype0=[{string.Join(",", type0.Select(t => $"{ShapePrefix[t.Byte]}:{t.N}"))}]" +
                    (surplus.Count > 0 ? $"\tSURPLUS[{string.Join("; ", surplus)}]" : ""));
    }
}

// The whole type-0 measurement: every row, every face it offers, every shape on that
// face model. One row per (body code, face, shape) plus the (face icon, byte, entry)
// keys the mod will look it up by. Deliberately dumps the model-level facts too, so a
// bad parse shows up as a nonsense number rather than as a plausible sentence.
void ShapeDump()
{
    var rows = ResolveFaces();
    // A face model is shared by several CharaMakeType rows (both Au Ra tribes, both
    // Viera tribes...), so measure each model ONCE and emit the keys separately.
    var measured = new Dictionary<string, List<ShapeMeasure.Result>>();
    var seen = new HashSet<string>();

    w.WriteLine("# key table: faceIcon, customize byte, entry (2..N) -> code/face/shape");
    foreach (var (_, who, code, off, faces, type0) in rows)
        foreach (var (gfx, icon) in faces)
        {
            int face = gfx + off;
            string key = $"{code}/{face}";
            if (seen.Add(key))
            {
                var mdl = MdlV6.Parse(gd.GetFile(FacePath(code, face))!.Data);
                measured[key] = ShapeMeasure.Measure(mdl);
            }
            var counts = ShapeCounts(code, face);
            foreach (var (b, n, menuLabel) in type0)
            {
                // NO KEY WHERE THE COUNT IS NOT EXACT. "Entry k is shape (k-2)" rests
                // entirely on the model carrying exactly SubMenuNum - 1 shapes with
                // that prefix, so entry 1 is the base mesh and the rest line up. Where
                // the model carries MORE (Miqo'te: five shp_brw_* against an Eyebrows
                // menu of five entries, i.e. one too many), nothing in the data says
                // WHICH four the menu offers, and a guess would put the wrong
                // description on four entries of every Miqo'te face. Left out instead.
                int have = counts.GetValueOrDefault(ShapePrefix[b]);
                if (have != n - 1)
                {
                    w.WriteLine($"# SKIP {who} face {face} {ShapePrefix[b]}: model has {have} shapes, menu offers {n} entries " +
                                $"(needs {n - 1}) - entry-to-shape mapping is not determined");
                    continue;
                }
                for (int e = 2; e <= n; e++)
                    w.WriteLine($"K\t{icon}\t{b}\t{e}\t{code}\t{face}\tshp_{ShapePrefix[b]}_{(char)('a' + e - 2)}\t{who}\t{menuLabel}");
            }
        }

    w.WriteLine("# " + ShapeMeasure.Header);
    foreach (var (key, list) in measured.OrderBy(k => k.Key))
    {
        var parts = key.Split('/');
        foreach (var r in list) w.WriteLine("M\t" + ShapeMeasure.Row(parts[0], int.Parse(parts[1]), r));
    }
    w.WriteLine($"# {measured.Count} face models measured");
}

// Everything MdlV6 parsed out of one model, so the parse can be
// eyeballed against the file before any measurement is trusted.
void Mdl6(string path)
{
    if (!gd.FileExists(path)) { w.WriteLine($"MISSING {path}"); return; }
    var d = gd.GetFile(path)!.Data;
    var m = MdlV6.Parse(d);
    foreach (var t in m.Trace) w.WriteLine($"  trace: {t}");
    w.WriteLine($"{path}  {d.Length} bytes  version=0x{m.Version:X}");
    w.WriteLine($"  meshes={m.MeshCount} attrs={m.AttributeCount} submeshes={m.SubmeshCount} materials={m.MaterialCount} " +
                $"bones={m.BoneCount} boneTables={m.BoneTableCount}");
    w.WriteLine($"  shapes={m.ShapeCount} shapeMeshes={m.ShapeMeshCount} shapeValues={m.ShapeValueCount}");
    w.WriteLine($"  bone-table block = {m.BoneTableBytes} bytes ({(m.BoneTableCount > 0 ? m.BoneTableBytes / m.BoneTableCount : 0)} per table), shapes at 0x{m.ShapeSectionOffset:X}");
    w.WriteLine($"  lod0 meshes {m.Lod0MeshIndex}..{m.Lod0MeshIndex + m.Lod0MeshCount - 1}, indices={m.Indices.Length}");
    w.WriteLine($"  attributes: {string.Join(", ", m.AttributeNames)}");
    w.WriteLine($"  materials: {string.Join(", ", m.MaterialNames)}");
    for (int i = m.Lod0MeshIndex; i < m.Lod0MeshIndex + m.Lod0MeshCount; i++)
    {
        var me = m.Meshes[i];
        w.WriteLine($"  mesh {i}: verts={me.VertexCount} idx={me.IndexCount} start={me.StartIndex} mat={me.MaterialIndex}" +
                    $" '{(me.MaterialIndex < m.MaterialNames.Length ? m.MaterialNames[me.MaterialIndex] : "?")}'" +
                    $" sub={me.SubMeshIndex}..{me.SubMeshIndex + me.SubMeshCount - 1} stride=[{string.Join(",", me.VertexBufferStride)}]" +
                    $" pos={(me.Positions.Length > 0 ? "yes" : "NONE")}");
        if (me.Positions.Length > 0)
        {
            float mnx = float.MaxValue, mny = float.MaxValue, mnz = float.MaxValue, mxx = float.MinValue, mxy = float.MinValue, mxz = float.MinValue;
            for (int v = 0; v < me.VertexCount; v++)
            {
                mnx = Math.Min(mnx, me.Positions[v, 0]); mxx = Math.Max(mxx, me.Positions[v, 0]);
                mny = Math.Min(mny, me.Positions[v, 1]); mxy = Math.Max(mxy, me.Positions[v, 1]);
                mnz = Math.Min(mnz, me.Positions[v, 2]); mxz = Math.Max(mxz, me.Positions[v, 2]);
            }
            w.WriteLine($"      bbox x[{mnx:F3},{mxx:F3}] y[{mny:F3},{mxy:F3}] z[{mnz:F3},{mxz:F3}]");
        }
        for (int s = me.SubMeshIndex; s < me.SubMeshIndex + me.SubMeshCount; s++)
        {
            var sm = m.Submeshes[s];
            var names = new List<string>();
            for (int a = 0; a < m.AttributeCount; a++) if ((sm.AttributeIndexMask & (1u << a)) != 0) names.Add(m.AttributeNames[a]);
            w.WriteLine($"      submesh {s}: idx {sm.IndexOffset}+{sm.IndexCount} attrs=[{string.Join(",", names)}]");
        }
    }
    foreach (var sh in m.Shapes)
    {
        w.WriteLine($"  shape '{sh.Name}' lod0 shapeMeshes {sh.MeshStart[0]}+{sh.MeshCount[0]}");
        for (int i = sh.MeshStart[0]; i < sh.MeshStart[0] + sh.MeshCount[0] && i < m.ShapeMeshes.Length; i++)
        {
            var sm = m.ShapeMeshes[i];
            var mesh = m.Meshes.Select((x, ix) => (x, ix)).FirstOrDefault(t => t.x.StartIndex == sm.MeshIndexOffset);
            int b0 = int.MaxValue, b1 = int.MinValue, r0 = int.MaxValue, r1 = int.MinValue;
            for (uint v = sm.ValueOffset; v < sm.ValueOffset + sm.ValueCount && v < m.ShapeValues.Length; v++)
            {
                var (bi, rep) = m.ShapeValues[(int)v];
                b0 = Math.Min(b0, bi); b1 = Math.Max(b1, bi); r0 = Math.Min(r0, rep); r1 = Math.Max(r1, rep);
            }
            w.WriteLine($"    sm{i}: meshIndexOffset={sm.MeshIndexOffset} -> mesh {(mesh.x == null ? "NONE" : mesh.ix.ToString())}" +
                        $" (start={mesh.x?.StartIndex} idx={mesh.x?.IndexCount} verts={mesh.x?.VertexCount})" +
                        $" values {sm.ValueOffset}+{sm.ValueCount} base[{b0},{b1}] replacing[{r0},{r1}]");
        }
    }
    // The bone boxes are the only spatial ground truth in the file; print the pairs
    // the axis convention is read from.
    foreach (var probe in new[] { "j_kubi", "j_kao", "j_f_dago", "j_f_mabup_01_l", "j_f_mabdn_01_l", "j_f_uhana", "j_f_hana_l", "j_f_hana_r", "j_f_mayu_l", "j_f_mayu_r", "j_f_bero_01" })
    {
        int bi = Array.IndexOf(m.BoneNames, probe);
        if (bi < 0) continue;
        w.WriteLine($"  bone {probe}: min[{m.BoneBoxMin[bi][0]:F3},{m.BoneBoxMin[bi][1]:F3},{m.BoneBoxMin[bi][2]:F3}] " +
                    $"max[{m.BoneBoxMax[bi][0]:F3},{m.BoneBoxMax[bi][1]:F3},{m.BoneBoxMax[bi][2]:F3}]");
    }
}

// The TYPE-4 family: dump CharaMakeType.FacialFeatureOption as an
// index plus one raw RGBA per icon, in exactly the shape `icons <Menu>` writes so
// verify.py and mksheet.py need no special case.
// The row key is "<race> <tribe> <sex> face<N>" rather than the bare row, because the
// seven slots are per FACE - a row with seven faces offers seven separate sets. The
// "entry" column is the SLOT number 1..7, which is the order the sheet stores them in
// and the order the bits of CustomizeData byte 12 run (FacialFeature1..7).
// There is no CharaMakeCustomize hop here: the ints ARE ui/icon ids (game-api.md,
// "Facial Features have icons after all").
void FeatureIcons()
{
    string outDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "..", "..", "..", "..", "icons", "Facial Features");
    Directory.CreateDirectory(outDir);
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    var written = new HashSet<uint>();

    foreach (var row in sheet)
    {
        string race = row.Race.ValueNullable?.Masculine.ExtractText() ?? "?";
        string tribe = row.Tribe.ValueNullable?.Masculine.ExtractText() ?? "?";
        string who = $"{race} {tribe} {(row.Gender == 0 ? "male" : "female")}";

        // How many faces does this row actually offer? The Face menu is type 1 and
        // its SubMenuNum is the count; FacialFeatureOption has eight slots regardless
        // and the trailing ones are all-zero padding.
        int faces = 0;
        var t4 = new List<string>();
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            string label = m.Menu.ValueNullable?.Text.ExtractText() ?? "";
            if (label == "Face" && m.SubMenuType == 1) faces = m.SubMenuNum;
            if (m.SubMenuType == 4 && m.SubMenuNum > 0) t4.Add($"[{i}]{label}(n={m.SubMenuNum},init={m.InitVal})");
        }
        w.WriteLine($"# {row.RowId}\t{who}\tfaces={faces}\ttype4={string.Join(" ", t4)}");

        for (int f = 0; f < 8; f++)
        {
            var o = row.FacialFeatureOption[f];
            int[] v = { o.Option1, o.Option2, o.Option3, o.Option4, o.Option5, o.Option6, o.Option7 };
            if (v.All(x => x == 0)) continue;
            if (f >= faces) { w.WriteLine($"# WARN {who}: FacialFeatureOption[{f}] is populated but the Face menu offers only {faces}"); }
            for (int s = 0; s < 7; s++)
            {
                uint icon = (uint)v[s];
                w.WriteLine($"{row.RowId}\t{who} face{f + 1}\t-1\t{s + 1}\t{icon}\t{icon}\tFacialFeatureOption");
                if (icon != 0 && written.Add(icon)) DumpIcon(icon, outDir);
            }
        }
    }
    w.WriteLine($"# {written.Count} unique icons -> {Path.GetFullPath(outDir)}");
}

// Resolve one menu family to icon ids for every CharaMakeType row, write the
// index (row, menu, entry -> icon) as TSV and every unique icon as raw RGBA.
// Two param families, both proven in docs/charamake-descriptions.md:
//   B: the param IS the icon id (Face, Tail Shape, Fur Pattern, Viera Ear Shape)
//   A: the param is a CharaMakeCustomize row whose .Icon is the id (Hairstyle,
//      Face Paint) - and looking THAT up for a type-0 menu returns a hairstyle,
//      which is the trap this tool must never walk into. Hence: menu label filter
//      plus SubMenuType, never the param alone.
void IconTable(string menuName)
{
    string outDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "..", "..", "..", "..", "icons", menuName);
    Directory.CreateDirectory(outDir);
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    var cmc = gd.GetExcelSheet<CharaMakeCustomize>()!;
    var written = new HashSet<uint>();

    foreach (var row in sheet)
    {
        string race = row.Race.ValueNullable?.Masculine.ExtractText() ?? "?";
        string tribe = row.Tribe.ValueNullable?.Masculine.ExtractText() ?? "?";
        string who = $"{race} {tribe} {(row.Gender == 0 ? "male" : "female")}";
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            string label = m.Menu.ValueNullable?.Text.ExtractText() ?? "";
            if (label != menuName || m.SubMenuNum == 0) continue;
            if (m.SubMenuType != 1) { w.WriteLine($"# SKIP {who}: '{label}' is type {m.SubMenuType}, not an icon grid"); continue; }
            for (int e = 0; e < m.SubMenuNum; e++)
            {
                uint param = m.SubMenuParam[e];
                uint icon = param;
                string via = "param";
                var custom = cmc.GetRowOrDefault(param);
                if (custom.HasValue && custom.Value.Icon != 0) { icon = custom.Value.Icon; via = $"CMC[{param}]"; }
                w.WriteLine($"{row.RowId}\t{who}\t{i}\t{e + 1}\t{param}\t{icon}\t{via}");
                if (icon != 0 && written.Add(icon)) DumpIcon(icon, outDir);
            }
        }
    }
    w.WriteLine($"# {written.Count} unique icons -> {Path.GetFullPath(outDir)}");
}

// Does the GAME name any entry of a type-1 menu? CharaMakeCustomize carries Hint
// (a Lobby row) and HintItem (an Item row, the aesthetician unlock), and a name from
// either of those would be the game's own word and must be preferred over anything
// the mod authors. recorded "the named ones are aesthetician unlocks not offered
// at creation" - this is what re-checks that per entry instead of taking it on trust,
// because it decides whether ~879 hairstyle descriptions have to be written at all.
void MenuNames(string menuName)
{
    var sheet = gd.GetExcelSheet<CharaMakeType>()!;
    var cmc = gd.GetExcelSheet<CharaMakeCustomize>()!;
    int named = 0, total = 0, purchasable = 0;
    var seen = new HashSet<uint>();

    foreach (var row in sheet)
        for (int i = 0; i < 28; i++)
        {
            var m = row.CharaMakeStruct[i];
            if ((m.Menu.ValueNullable?.Text.ExtractText() ?? "") != menuName || m.SubMenuNum == 0) continue;
            if (m.SubMenuType != 1) continue;
            for (int e = 0; e < m.SubMenuNum; e++)
            {
                uint param = m.SubMenuParam[e];
                var c = cmc.GetRowOrDefault(param);
                if (!c.HasValue || !seen.Add(param)) continue;
                total++;
                string hint = c.Value.Hint.ValueNullable?.Text.ExtractText() ?? "";
                string item = c.Value.HintItem.ValueNullable?.Name.ExtractText() ?? "";
                if (c.Value.IsPurchasable) purchasable++;
                if (hint.Length > 0 || item.Length > 0)
                {
                    named++;
                    w.WriteLine($"param={param} icon={c.Value.Icon} feature={c.Value.FeatureID} " +
                                $"purchasable={c.Value.IsPurchasable} hint='{hint}' item='{item}'");
                }
            }
        }
    w.WriteLine($"# {menuName}: {total} distinct CharaMakeCustomize params, {named} carry a Hint or HintItem, {purchasable} purchasable");
}

void DumpIcon(uint icon, string outDir)
{
    string grp = $"{icon / 1000 * 1000:D6}";
    string[] cands = { $"ui/icon/{grp}/{icon:D6}_hr1.tex", $"ui/icon/{grp}/{icon:D6}.tex" };
    foreach (var p in cands)
    {
        if (!gd.FileExists(p)) continue;
        var tex = gd.GetFile<Lumina.Data.Files.TexFile>(p)!;
        var bytes = tex.ImageData;   // B8G8R8A8
        using var fs = File.Create(Path.Combine(outDir, $"{icon}.raw"));
        using var bw = new BinaryWriter(fs);
        bw.Write(tex.Header.Width); bw.Write(tex.Header.Height); bw.Write(bytes);
        return;
    }
    w.WriteLine($"# NO TEX for icon {icon}");
}

// For every face model that exists, count its shape keys by prefix. The prefixes
// are the face features the type-0 menus drive; a count of N-1 next to a menu of N
// entries is what proves the mapping (entry 1 = the untouched base mesh).
void ShapeCheck()
{
    string[] codes = { "0101", "0201", "0301", "0401", "0501", "0601", "0701", "0801",
                       "0901", "1001", "1101", "1201", "1301", "1401", "1501", "1601",
                       "1701", "1801" };
    foreach (var c in codes)
        for (int f = 0; f <= 220; f++)
        {
            string p = $"chara/human/c{c}/obj/face/f{f:D4}/model/c{c}f{f:D4}_fac.mdl";
            if (!gd.FileExists(p)) continue;
            var names = ModelStrings(p);
            var shapes = names.Where(n => n.StartsWith("shp_")).ToList();
            var byPrefix = shapes.GroupBy(n => n.Length >= 7 ? n.Substring(4, 3) : n)
                                 .OrderBy(g => g.Key)
                                 .Select(g => $"{g.Key}={g.Count()}");
            w.WriteLine($"c{c} f{f:D4}: {string.Join(" ", byPrefix)}");
        }
}

string[] ModelStrings(string path)
{
    var d = gd.GetFile(path)!.Data;
    int off = 0x44 + BitConverter.ToUInt16(d, 12) * 136;
    int size = (int)BitConverter.ToUInt32(d, off + 4);
    return Encoding.UTF8.GetString(d, off + 8, size).Split('\0', StringSplitOptions.RemoveEmptyEntries);
}

void MdlRaw(string path)
{
    if (!gd.FileExists(path)) { w.WriteLine($"MISSING {path}"); return; }
    var f = gd.GetFile(path)!;
    var d = f.Data;
    w.WriteLine($"{path}: {d.Length} bytes, type={f.FileInfo.Type}");
    // ModelFileHeader: version u32, stackSize u32, runtimeSize u32, vertexDeclCount u16, matCount u16, ...
    w.WriteLine($"  version={BitConverter.ToUInt32(d, 0)} stack={BitConverter.ToUInt32(d, 4)} runtime={BitConverter.ToUInt32(d, 8)} " +
                $"vertDecl={BitConverter.ToUInt16(d, 12)} mat={BitConverter.ToUInt16(d, 14)}");
    int off = 0x44 + BitConverter.ToUInt16(d, 12) * 136;   // header 68 bytes + decls
    int strCount = BitConverter.ToUInt16(d, off);
    int strSize = (int)BitConverter.ToUInt32(d, off + 4);
    w.WriteLine($"  after decls at 0x{off:X}: stringCount={strCount} stringSize={strSize}");
    var names = Encoding.UTF8.GetString(d, off + 8, strSize).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    foreach (var n in names) w.WriteLine($"    {n}");
}

static string ReadStr(byte[] strings, uint off)
{
    int end = (int)off;
    while (end < strings.Length && strings[end] != 0) end++;
    return Encoding.UTF8.GetString(strings, (int)off, end - (int)off);
}

// ── Is "Light"/"Dark" a PALETTE BAND or a separate value? ──
// Lobby rows 2122/2123 are 'Dark'/'Light' and 2127 is 'None' - controls in the
// colour picker, not entries of any CharaMakeType menu. The Lip Color and Face
// Paint Color menus offer n=96 where Skin/Hair/Tattoo offer n=192, so the obvious
// candidate is that the picker shows one 96-entry HALF of a 192-entry palette and
// Light/Dark chooses which. That is testable offline: if the theory holds, the two
// halves must differ systematically in brightness, and they must differ in the
// blocks with a Light/Dark control while matching in the ones without.
// Prints mean luminance per 96-entry band per block. It does NOT decide what the
// buttons do - it only says whether the DATA is split the way the theory needs.
void PaletteBands()
{
    var d = gd.GetFile("chara/xls/charamake/human.cmp")!.Data;
    const int PaletteEntries = 256;

    // The blocks CharaMakePalette.cs pins: 0 shared (eye/tattoo), 11 lip, 13 face
    // paint. Same constants as the plugin so a disagreement here is a real one.
    var blocks = new (int Block, string Name)[] { (0, "eye/tattoo (shared)"), (11, "lip"), (13, "face paint") };

    foreach (var (block, name) in blocks)
    {
        Console.WriteLine($"block {block} - {name}");
        for (var band = 0; band < 2; band++)
        {
            double sum = 0; var n = 0; var distinct = new HashSet<int>();
            for (var i = band * 96; i < (band + 1) * 96; i++)
            {
                var at = ((block * PaletteEntries) + i) * 4;
                if (at + 3 >= d.Length) break;
                int r = d[at], g = d[at + 1], b = d[at + 2];
                // Rec. 601 luma: brightness as an eye weights it, not a raw mean.
                sum += (0.299 * r) + (0.587 * g) + (0.114 * b);
                distinct.Add((r << 16) | (g << 8) | b);
                n++;
            }
            if (n == 0) { Console.WriteLine($"    band {band}: outside the file"); continue; }
            Console.WriteLine($"    band {band} (entries {band * 96}-{(band * 96) + n - 1}): "
                              + $"mean luma {sum / n,6:F1}  distinct colours {distinct.Count}/{n}");
        }
    }
}
