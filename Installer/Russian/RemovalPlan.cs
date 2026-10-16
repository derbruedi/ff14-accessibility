using System.Text;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

// Discover and validate everything before changing the installation. Renames and
// settings writes can be rolled back; final erasure is deliberately irreversible.
public sealed class RemovalPlan
{
    private readonly Dictionary<string, byte[]?> originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> replacements = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> deleted = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> prune = new(StringComparer.OrdinalIgnoreCase);

    public void Delete(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        PackageFiles.RejectLinks(path);
        if (Path.GetPathRoot(path) == path) throw new IOException("Удаление корня диска запрещено.");
        deleted.Add(path);
        prune.Add(Path.GetDirectoryName(path)!);
    }

    public void Edit(string path, Func<JObject, bool> edit)
    {
        if (!File.Exists(path)) return;
        var bytes = Read(path);
        var value = PackageFiles.ReadJson(Encoding.UTF8.GetString(bytes));
        if (edit(value)) Replace(path, Encoding.UTF8.GetBytes(value.ToString()));
    }

    public void EditToken(string path, Func<JToken, bool> edit)
    {
        if (!File.Exists(path)) return;
        var value = Parse(Read(path));
        if (edit(value)) Replace(path, Encoding.UTF8.GetBytes(value.ToString()));
    }

    public static JToken Parse(byte[] bytes)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')))
        { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        return JToken.Load(reader, new Newtonsoft.Json.Linq.JsonLoadSettings
        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }

    public byte[] Read(string path)
    {
        path = Path.GetFullPath(path);
        PackageFiles.RejectLinks(path);
        if (!originals.ContainsKey(path)) originals[path] = File.ReadAllBytes(path);
        return replacements.GetValueOrDefault(path) ?? originals[path]!;
    }

    public void Create(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path);
        PackageFiles.RejectLinks(path);
        if (File.Exists(path) || Directory.Exists(path) || originals.ContainsKey(path))
            throw new IOException("Файл уже существует: " + path);
        originals[path] = null;
        replacements[path] = bytes;
    }

    public void Replace(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path);
        Read(path);
        replacements[path] = bytes;
    }

    private bool Covered(string path) => deleted.Any(d => Same(path, d) || Under(path, d));
    public static bool Same(string a, string b) => Path.GetFullPath(a).TrimEnd('\\', '/')
        .Equals(Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    public static bool Under(string path, string folder) => Path.GetFullPath(path)
        .StartsWith(Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static void RejectParents(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Путь удаления является ссылкой: " + current);
    }

    public void Apply(Action ensureClosed, Action<string> log, CancellationToken token, Action<string>? checkpoint = null)
    {
        var paths = deleted.Where(p => !deleted.Any(d => !Same(p, d) && Under(p, d))).Order().ToArray();
        var writes = replacements.Where(p => !Covered(p.Key) && (originals[p.Key] == null || !p.Value.SequenceEqual(originals[p.Key]!))).ToArray();
        token.ThrowIfCancellationRequested();
        ensureClosed();
        foreach (var path in paths) PackageFiles.RejectLinks(path);
        foreach (var pair in originals)
            if (pair.Value == null ? File.Exists(pair.Key) || Directory.Exists(pair.Key) :
                !File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                throw new IOException("Файлы изменились во время подготовки удаления: " + pair.Key);
        // Check sharing/permissions before any mutation, including files in old backups.
        foreach (var path in paths.SelectMany(p => Directory.Exists(p)
                     ? Directory.GetFiles(p, "*", SearchOption.AllDirectories) : [p]).Concat(writes.Select(p => p.Key)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            PackageFiles.RejectLinks(path);
            if (!File.Exists(path)) continue;
            using var handle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        token.ThrowIfCancellationRequested();
        var moved = new List<(string From, string To, bool Directory)>();
        var written = new List<string>();
        try
        {
            // No cancellation after the first rename: complete or restore this operation.
            ensureClosed();
            foreach (var path in paths)
            {
                var destination = path + ".removing-" + Guid.NewGuid().ToString("N");
                var directory = Directory.Exists(path);
                if (directory) Directory.Move(path, destination); else File.Move(path, destination);
                moved.Add((path, destination, directory));
            }
            checkpoint?.Invoke("moved");
            foreach (var pair in writes)
            {
                PackageFiles.RejectLinks(pair.Key);
                if (originals[pair.Key] == null ? File.Exists(pair.Key) || Directory.Exists(pair.Key) :
                    !File.ReadAllBytes(pair.Key).SequenceEqual(originals[pair.Key]!))
                    throw new IOException("Настройки изменились перед записью: " + pair.Key);
                Write(pair.Key, pair.Value, create: originals[pair.Key] == null);
                written.Add(pair.Key);
                if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) throw new IOException("Не подтверждена запись: " + pair.Key);
            }
            checkpoint?.Invoke("written");
        }
        catch (Exception failure)
        {
            try
            {
                foreach (var path in written.AsEnumerable().Reverse())
                {
                    if (!File.ReadAllBytes(path).SequenceEqual(replacements[path]))
                        throw new IOException("Настройки изменены другой программой: " + path);
                    if (originals[path] == null) File.Delete(path);
                    else Write(path, originals[path]!);
                }
                foreach (var item in moved.AsEnumerable().Reverse())
                    if (item.Directory) Directory.Move(item.To, item.From); else File.Move(item.To, item.From);
            }
            catch (Exception rollback)
            { throw new IOException("Удаление остановлено, восстановление не завершено. Остатки: " +
                string.Join(", ", moved.Select(p => p.To)) + ". " + rollback.Message, failure); }
            throw;
        }
        // Settings now agree with absent plugins. Never claim success if final erasure fails.
        foreach (var item in moved)
        {
            try
            {
                if (item.Directory) Directory.Delete(item.To, true); else File.Delete(item.To);
                log("Удалено: " + item.From);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new IOException("Удаление не завершено. Закройте программы, использующие файлы, и повторите. Осталась папка или файл: " + item.To, ex); }
        }
        // Only immediate parents of verified owned items, and only when empty.
        foreach (var folder in prune.OrderByDescending(p => p.Length))
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        if (paths.Any(p => File.Exists(p) || Directory.Exists(p)) || moved.Any(p => File.Exists(p.To) || Directory.Exists(p.To)))
            throw new IOException("После удаления обнаружены остатки. Удаление не завершено.");
    }

    private static void Write(string path, byte[] bytes, bool create = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".removal-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (create) File.Move(temp, path);
            else File.Replace(temp, path, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
