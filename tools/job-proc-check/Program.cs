using System.Text.Json;
using Dalamud.Game;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("JobProcCheck <game/sqpack> <report.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.English });
var data = new GameDataReader(game);
var effects = new List<object>();
var original = Loc.Mode;
try
{
    Loc.Mode = LanguageMode.Russian;
    foreach (var entry in JobProcCatalog.All)
    {
        if (!game.Excel.GetSheet<Status>().TryGetRow(entry.StatusId, out var row)
            || !entry.MatchesSource(row.Name.ExtractText()))
            throw new Exception($"Status ID/name mismatch: {entry.StatusId} {entry.EnglishName}");
        var names = new Dictionary<string, string>();
        foreach (var language in Enum.GetValues<ClientLanguage>())
        {
            data.Language = language;
            var localized = data.GetExcelSheet<Status>().GetRow(entry.StatusId);
            var name = RussianGameText.Name(data, localized, x => x.Name);
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "[А-Яа-яЁё]"))
                throw new Exception($"Russian name missing: {entry.StatusId} {language}: {name}");
            names[language.ToString()] = name;
        }
        if (names.Values.Distinct().Count() != 1) throw new Exception($"Client-dependent translation: {entry.StatusId}");
        effects.Add(new { entry.StatusId, entry.JobId, entry.EnglishName, RussianNames = names,
            Description = row.Description.ExtractText() });
    }

    // These ActionProcStatus icons reflect ordinary combo potency or a shield,
    // not an extra proc action. All remaining referenced PvE effects must be
    // covered and belong to the action's actual job (or starting class).
    uint[] exclusions = [2588, 2589, 3672, 3772, 3686];
    var references = new List<object>();
    foreach (var action in game.Excel.GetSheet<Lumina.Excel.Sheets.Action>()
        .Where(x => x.IsPlayerAction && !x.IsPvP && x.ActionProcStatus.RowId != 0))
    {
        var status = action.ActionProcStatus.Value.Status.RowId;
        if (exclusions.Contains(status)) continue;
        if (JobProcCatalog.Find(status, action.ClassJob.RowId) == null)
            throw new Exception($"Uncovered proc action: {action.RowId} {action.Name.ExtractText()}, job={action.ClassJob.RowId}, status={status}");
        references.Add(new { ActionId = action.RowId, Action = action.Name.ExtractText(),
            Job = action.ClassJob.RowId, StatusId = status });
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { CheckedAt = DateTimeOffset.Now,
        Effects = effects, ActionProcReferences = references, ExcludedComboOrShieldStatuses = exclusions,
        InGameVerified = false }, new JsonSerializerOptions { WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    Console.WriteLine($"PASS: {effects.Count} status IDs/names, {references.Count} PvE proc-action references, Russian names across four client languages. Not a live game test.");
}
finally { Loc.Mode = original; }
