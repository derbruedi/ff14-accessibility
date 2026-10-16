using System.Text;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

// All original bytes are kept, including BOM and unrelated settings. No JSON type activation.
public sealed class JsonChange
{
    public string Path { get; }
    public byte[]? Original { get; }
    public JObject Value { get; }
    public JsonChange(string path, JObject? initial = null)
    {
        Path = System.IO.Path.GetFullPath(path);
        PackageFiles.RejectLinks(Path);
        Original = File.Exists(Path) ? File.ReadAllBytes(Path) : null;
        Value = Original == null ? initial ?? throw new FileNotFoundException("Не найдены настройки", Path)
            : PackageFiles.ReadJson(Encoding.UTF8.GetString(Original));
    }
    public byte[] Next => Encoding.UTF8.GetBytes(Value.ToString());
    public bool Changed => Original == null || !JToken.DeepEquals(PackageFiles.ReadJson(Encoding.UTF8.GetString(Original)), Value);
    public void Check()
    {
        PackageFiles.RejectLinks(Path);
        if (Original == null ? File.Exists(Path) : !File.Exists(Path) || !File.ReadAllBytes(Path).SequenceEqual(Original))
            throw new IOException("Настройки изменились во время установки: " + Path);
    }
}

public static class SettingsTransaction
{
    public static void Apply(string backupRoot, IReadOnlyList<JsonChange> changes, Action ensureClosed, Action<string> log,
        string? stagedMod = null, string? targetMod = null, IReadOnlyDictionary<string, string>? originalMod = null,
        Action<string>? checkpoint = null)
    {
        ensureClosed();
        foreach (var change in changes) change.Check();
        if (stagedMod != null)
        {
            PackageFiles.RejectLinks(stagedMod);
            PackageFiles.VerifySnapshot(targetMod!, originalMod!);
        }
        var changed = changes.Where(c => c.Changed).ToArray();
        if (changed.Length == 0 && stagedMod == null) return;
        var backup = System.IO.Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        PackageFiles.RejectLinks(backup);
        Directory.CreateDirectory(backup);
        for (var i = 0; i < changed.Length; i++)
            if (changed[i].Original is { } bytes) File.WriteAllBytes(System.IO.Path.Combine(backup, i + ".json"), bytes);
        File.WriteAllText(System.IO.Path.Combine(backup, "restore-map.json"), new JArray(changed.Select((c, i) => new JObject
        { ["OriginalPath"] = c.Path, ["BackupFile"] = c.Original == null ? null : i + ".json", ["Existed"] = c.Original != null })).ToString());
        var oldMod = System.IO.Path.Combine(backup, "previous-mod");
        File.WriteAllText(System.IO.Path.Combine(backup, "README.txt"),
            "Прежние настройки перечислены в restore-map.json. Восстанавливайте только при закрытой игре.\r\n" +
            "Папка перевода: " + targetMod + "\r\nПрежняя папка перевода: " + oldMod + "\r\n");
        var written = new List<JsonChange>();
        var saved = false;
        var placed = false;
        try
        {
            ensureClosed();
            foreach (var change in changes) change.Check();
            if (stagedMod != null)
            {
                PackageFiles.VerifySnapshot(targetMod!, originalMod!);
                if (Directory.Exists(targetMod)) { Directory.Move(targetMod, oldMod); saved = true; }
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetMod!)!);
                Directory.Move(stagedMod, targetMod!);
                placed = true;
            }
            checkpoint?.Invoke("files");
            foreach (var change in changed)
            {
                change.Check();
                Write(change.Path, change.Next);
                written.Add(change);
                if (!File.ReadAllBytes(change.Path).SequenceEqual(change.Next)) throw new IOException("Запись настроек не прошла проверку.");
                checkpoint?.Invoke(change.Path);
            }
            log("Резервная копия: " + backup);
        }
        catch (Exception failure)
        {
            try
            {
                foreach (var change in written.AsEnumerable().Reverse())
                {
                    if (!File.ReadAllBytes(change.Path).SequenceEqual(change.Next))
                        throw new IOException("Другая программа изменила настройки после записи: " + change.Path);
                    if (change.Original == null) File.Delete(change.Path);
                    else Write(change.Path, change.Original);
                }
                if (placed) Directory.Move(targetMod!, System.IO.Path.Combine(backup, "incomplete-mod"));
                if (saved) Directory.Move(oldMod, targetMod!);
                log("Прежний перевод и настройки восстановлены после ошибки.");
            }
            catch (Exception rollback)
            {
                throw new IOException($"Установка и восстановление не завершены. Копия: {backup}. Причина: {failure.Message}. Восстановление: {rollback.Message}", rollback);
            }
            throw;
        }
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temporary = path + ".installer-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
