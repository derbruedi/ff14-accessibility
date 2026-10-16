using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.Evaluator;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Text.ReadOnly;

if (args.Length != 2) throw new ArgumentException("MenuReadingCheck <game/sqpack> <report.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Lumina.Data.Language.English });
var data = new GameDataReader(game);
var log = DispatchProxy.Create<IPluginLog, SilentLog>();
var service = new GameDescriptionService(data, new TextOnlyEvaluator(), log);
var resolve = typeof(GameDescriptionService).GetMethod("FromActionMenuLabel", BindingFlags.Instance | BindingFlags.NonPublic)!;
string[] labels = ["Aspect Mastery III\nУр. 35", "Maim and Mend II\nУр. 40", "Firestarter\nУр. 42", "Enhanced Swiftcast\nУр. 94", "Enhanced Addle\nУр. 98"];
var results = new List<object>();
// The recorded rows belong to THM/BLM. In the plugin these IDs come directly
// from AgentActionMenu.TraitList; this offline check supplies that job's sheet rows.
var menuTraits = game.Excel.GetSheet<Lumina.Excel.Sheets.Trait>()
    .Where(row => row.ClassJob.RowId is 0 or 7 or 25).Select(row => row.RowId).ToHashSet();
foreach (var language in new[] { ClientLanguage.English, ClientLanguage.German })
{
    data.Language = language;
    foreach (var label in labels)
    {
        Loc.Mode = LanguageMode.Russian;
        var result = resolve.Invoke(service, [label, "", menuTraits])!;
        var name = (string)result.GetType().GetProperty("Name")!.GetValue(result)!;
        var description = (string)result.GetType().GetProperty("Description")!.GetValue(result)!;
        if (!Regex.IsMatch(name, "[А-Яа-яЁё]") || !Regex.IsMatch(description, "[А-Яа-яЁё]"))
            throw new Exception($"Not translated from recorded row: {language}: {label} => {name}: {description}");
        // Re-resolving a Russian row must use the same typed record.
        var level = Regex.Match(label, @"\d+$").Value;
        var russian = resolve.Invoke(service, [name + " Ур. " + level, "", menuTraits])!;
        if (!result.Equals(russian)) throw new Exception($"Russian row did not resolve identically: {name}");
        results.Add(new { ClientLanguage = language.ToString(), Label = label, Name = name, Description = description });
    }
}
Loc.Mode = LanguageMode.Russian;
if (!service.GeneralAction(30).Contains("питомца")) throw new Exception("Missing general-action description.");
if (!service.MainCommand(1).Contains("оружие")) throw new Exception("Missing main-command description.");
data.Language = ClientLanguage.English; Loc.Mode = LanguageMode.English;
if (service.GeneralAction(30) != "Change the order of hotbar-assigned actions.") throw new Exception("English fallback changed.");
var translationAssertions = TranslationToggleChecks.Run(game, data, service, log);
var nativeItems = NativeItemIdChecks.Run(data, service, log);
var recipeChecks = RecipeChecks.Run(data, service);
var report = new { CheckedAt = DateTimeOffset.Now, Cases = results, AdditionalAssertions = 3,
    TranslationToggleAssertions = translationAssertions,
    NativeItems = nativeItems,
    RecipeChecks = recipeChecks,
    InGameVerified = false, DynamicNumbersEvaluated = false };
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Passed {results.Count * 2 + 3} menu assertions and {translationAssertions} translation-toggle assertions using real game sheets. Dynamic formulas and native focus require the game.");
Console.WriteLine($"Recipe checks: {recipeChecks}");
Console.WriteLine($"Native item checks: {nativeItems}");

public class SilentLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
}

// Deliberately does not pretend to run the game's condition/parameter evaluator.
// This check proves source selection and translation; numeric formulas stay live-only.
public sealed class TextOnlyEvaluator : ISeStringEvaluator
{
    public ReadOnlySeString Evaluate(ReadOnlySeString value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => value;
    public ReadOnlySeString Evaluate(ReadOnlySeStringSpan value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => new(value.Data.ToArray());
    public ReadOnlySeString EvaluateMacroString(string value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateMacroString(ReadOnlySpan<byte> value, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromAddon(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromLobby(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public ReadOnlySeString EvaluateFromLogMessage(uint id, Span<SeStringParameter> parameters = default, ClientLanguage? language = null) => throw new NotSupportedException();
    public string EvaluateActStr(ActionKind kind, uint id, ClientLanguage? language = null) => throw new NotSupportedException();
    public string EvaluateObjStr(ObjectKind kind, uint id, ClientLanguage? language = null) => throw new NotSupportedException();
}
