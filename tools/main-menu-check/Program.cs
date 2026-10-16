using System.Text.Json;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using FF14Accessibility;
using FF14Accessibility.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

if (args.Length != 2) throw new ArgumentException("MainMenuCheck <game/sqpack> <report.json>");
using var game = new GameData(args[0], new LuminaOptions { DefaultExcelLanguage = Language.English });
var data = new GameDataReader(game);
var cases = new Dictionary<string, string>();
void Equal(string key, string expected, string actual)
{
    if (actual != expected) throw new Exception($"{key}: expected '{expected}', got '{actual}'");
    cases.Add(key, actual);
}
string[] english = ["Character", "Duty", "Logs", "Travel", "Party", "Social", "System"];
string[] russian = ["Персонаж", "Задания и миссии", "Журналы", "Путешествия", "Группа", "Общение", "Система"];
var categories = game.Excel.GetSheet<MainCommandCategory>();
foreach (var language in Enum.GetValues<ClientLanguage>())
{
    data.Language = language;
    for (uint id = 1; id <= 7; id++)
    {
        Equal($"category {language}/{id}", english[id - 1], categories.GetRow(id).Name.ExtractText());
        Loc.Mode = LanguageMode.Russian;
        Equal($"Russian {language}/{id}", russian[id - 1], MainCommandMenu.ReadCategoryName(data, id));
        foreach (var mode in new[] { LanguageMode.English, LanguageMode.German })
        {
            Loc.Mode = mode;
            Equal($"native {mode}/{language}/{id}", data.GetExcelSheet<MainCommandCategory>().GetRow(id).Name.ExtractText(),
                MainCommandMenu.ReadCategoryName(data, id));
        }
    }
    Loc.Mode = LanguageMode.Russian;
    Equal($"empty {language}", "", MainCommandMenu.ReadCategoryName(data, 0));
    Equal($"missing {language}", "", MainCommandMenu.ReadCategoryName(data, uint.MaxValue));
}
// Reproduce the category/command id collision from the player's log using real data.
var commands = game.Excel.GetSheet<MainCommand>();
Equal("command 7 is Gathering Log", "Gathering Log", commands.GetRow(7).Name.ExtractText());
Equal("Gathering Log belongs to Logs", "3", commands.GetRow(7).MainCommandCategory.RowId.ToString());
Equal("Crafting Log belongs to Logs", "3", commands.GetRow(9).MainCommandCategory.RowId.ToString());
Equal("Journal belongs to Duty", "2", commands.GetRow(4).MainCommandCategory.RowId.ToString());
Equal("param 2 in player log opens Duty", "Duty", categories.GetRow(2).Name.ExtractText());
Equal("param 4 in player log opens Travel", "Travel", categories.GetRow(4).Name.ExtractText());
Equal("param 7 in player log opens System", "System", categories.GetRow(7).Name.ExtractText());
var report = new { CheckedAt = DateTimeOffset.Now, PluginVersion = typeof(MainCommandMenu).Assembly.GetName().Version?.ToString(),
    Assertions = cases.Count, Cases = cases, InGameVerified = false };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"Passed {cases.Count} assertions using installed game data. No live UI verification.");

public sealed class GameDataReader(GameData game) : IDataManager
{
    public ClientLanguage Language { get; set; } = ClientLanguage.English;
    public GameData GameData => game;
    public ExcelModule Excel => game.Excel;
    public bool HasModifiedGameDataFiles => false;
    private static Lumina.Data.Language ConvertLanguage(ClientLanguage language) => language switch
    {
        ClientLanguage.German => Lumina.Data.Language.German,
        ClientLanguage.French => Lumina.Data.Language.French,
        ClientLanguage.Japanese => Lumina.Data.Language.Japanese,
        _ => Lumina.Data.Language.English,
    };
    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelRow<T>
        => game.Excel.GetSheet<T>(ConvertLanguage(language ?? Language), name);
    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelSubrow<T>
        => game.Excel.GetSubrowSheet<T>(ConvertLanguage(language ?? Language), name);
    public FileResource? GetFile(string path) => game.GetFile(path);
    public T? GetFile<T>(string path) where T : FileResource => game.GetFile<T>(path);
    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken = default) where T : FileResource
        => Task.FromResult(game.GetFile<T>(path) ?? throw new FileNotFoundException(path));
    public bool FileExists(string path) => game.FileExists(path);
}
