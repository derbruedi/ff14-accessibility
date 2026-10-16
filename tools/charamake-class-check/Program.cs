using System.Reflection;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("CharaMakeClassCheck <game/sqpack> <report.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.English });
var data = new GameDataReader(game);
var type = typeof(CharaMakeReader).Assembly.GetType("FF14Accessibility.Services.CharaMakeClassText")!;
var read = type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
var resolveName = type.GetMethod("ResolveName", BindingFlags.Static | BindingFlags.NonPublic,
    [typeof(IDataManager), typeof(string)])!;
var readById = type.GetMethod("ReadById", BindingFlags.Static | BindingFlags.NonPublic)!;
var focusCases = new List<object>();
var cases = new List<object>();
// Explicit expectations separate from the implementation's class-to-Lobby map.
uint[] classes = [1, 2, 3, 4, 5, 6, 7, 26];
uint[] lobbyIds = [178, 180, 182, 184, 186, 188, 190, 192];
// Full main-hand values observed in dalamud.log, 2026-09-30 23:13 (6.08.82).
// Do not derive these inputs from the same table that the resolver uses.
ulong[] liveWeapons = [0x0001002B00C9, 0x00010009012D, 0x0007001F0191, 0x000C000301F5,
    0x000800010259, 0x000C00010321, 0x0009000F0385, 0x0001000106AC];
var weapons = game.Excel.GetSheet<CharaMakeClassEquip>().ToArray();
if (weapons.Length != 8 || weapons.Select(x => x.Weapon).Distinct().Count() != 8)
    throw new Exception("Starting weapons changed; check the class mapping.");
foreach (var language in Enum.GetValues<ClientLanguage>())
foreach (var mode in new[] { LanguageMode.Russian, LanguageMode.English })
{
    data.Language = language;
    Loc.Mode = mode;
    for (var i = 0; i < classes.Length; i++)
    {
        var equipment = weapons.Single(x => x.Class.RowId == classes[i]);
        // The highlighted text is independent of the old equipped weapon.
        string[] russianLabels = ["Гладиатор", "Борец", "Мародёр", "Копейщик", "Лучник", "Элементалист", "Оккультист", "Арканист"];
        var labels = mode == LanguageMode.Russian
            ? new[] { data.GetExcelSheet<ClassJob>().GetRow(classes[i]).Name.ExtractText(), russianLabels[i] }
            : new[] { data.GetExcelSheet<ClassJob>().GetRow(classes[i]).Name.ExtractText() };
        foreach (var label in labels)
        {
            var focusedId = (uint)resolveName.Invoke(null, [data, label])!;
            var focused = readById.Invoke(null, [data, focusedId]) ?? throw new Exception($"Unknown focused class: {label}");
            var description = (string)focused.GetType().GetProperty("Description")!.GetValue(focused)!;
            if (focusedId != classes[i] || description.Length < 40) throw new Exception($"Wrong focused class: {label}");
            var fromWeapon = read.Invoke(null, [data, liveWeapons[i]])!;
            if ((string)fromWeapon.GetType().GetProperty("Description")!.GetValue(fromWeapon)! != description)
                throw new Exception($"Focused class description differs from its own source: {label}");
            focusCases.Add(new { ClientLanguage = language.ToString(), PluginLanguage = mode.ToString(), Label = label, ClassId = focusedId });
        }
        foreach (var (model, kind) in new[] { (liveWeapons[i], "LogStartingWeapon"), (equipment.Weapon, "ShowcaseWeapon") })
        {
            var result = read.Invoke(null, [data, model]) ?? throw new Exception($"Class {classes[i]} was not resolved for {kind} {model:X12}.");
            var resultType = result.GetType();
            var id = (uint)resultType.GetProperty("ClassId")!.GetValue(result)!;
            var name = (string)resultType.GetProperty("Name")!.GetValue(result)!;
            var description = (string)resultType.GetProperty("Description")!.GetValue(result)!;
            var source = data.GetExcelSheet<Lobby>().GetRow(lobbyIds[i]);
            if (id != classes[i] || description.Length < 40)
                throw new Exception($"Wrong description for {language}/{mode}/{classes[i]}.");
            if (mode == LanguageMode.Russian)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, "[А-Яа-яЁё]"))
                    throw new Exception($"Russian class name missing: {name}");
                if (!System.Text.RegularExpressions.Regex.IsMatch(description, "[А-Яа-яЁё]")
                    || System.Text.RegularExpressions.Regex.IsMatch(description, "[A-Za-z]")
                    || !description.Contains(i == 7 ? "Соответствующие профессии:" : "Соответствующая профессия:"))
                    throw new Exception($"Russian class description missing: {language}/{classes[i]}.");
                using var stream = typeof(AccessibilityStrings).Assembly.GetManifestResourceStream(
                    "FF14Accessibility.Resources.RussianCharaMakeClasses.json");
                using var catalog = JsonDocument.Parse(stream!);
                var entry = catalog.RootElement.GetProperty(id.ToString());
                if (entry.GetProperty("Source").GetString() != game.Excel.GetSheet<Lobby>(Lumina.Data.Language.English).GetRow(lobbyIds[i]).Unknown1.ExtractText().Trim()
                    || entry.GetProperty("Russian").GetString() != description)
                    throw new Exception($"Stale or wrong Russian class description: {id}");
            }
            else if (description != source.Unknown1.ExtractText().Trim()
                     || (!string.Equals(name, equipment.Class.Value.Name.ExtractText(), StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(name, data.GetExcelSheet<ClassJob>().GetRow(id).Name.ExtractText(), StringComparison.OrdinalIgnoreCase))
                    )
                throw new Exception($"Wrong game-language name: {name}");
            cases.Add(new { ClientLanguage = language.ToString(), PluginLanguage = mode.ToString(), ClassId = id,
                LobbyRow = lobbyIds[i], WeaponKind = kind, Weapon = $"{model:X12}", Name = name, Description = description });
        }
    }
}
File.WriteAllText(args[1], JsonSerializer.Serialize(new { CheckedAt = DateTimeOffset.Now, Cases = cases, FocusCases = focusCases,
    InGameVerified = false, RussianDescriptionsVerified = true }, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Passed {cases.Count} class/description and {focusCases.Count} highlighted-label checks using installed game tables. Live selection and speech still require FFXIV.");
