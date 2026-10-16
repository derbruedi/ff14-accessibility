using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
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
