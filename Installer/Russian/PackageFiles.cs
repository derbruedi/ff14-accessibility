using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public static class PackageFiles
{
    public static Version VersionOf(string text)
    {
        if (!Regex.IsMatch(text, @"^[vV]?\d+\.\d+(\.\d+){0,2}$") ||
            !Version.TryParse(text.TrimStart('v', 'V'), out var v))
            throw new InvalidDataException($"Непонятный номер версии: {text}");
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
    }

    public static JObject ReadJson(string text)
    {
        using var reader = new JsonTextReader(new StringReader(text.TrimStart('\uFEFF')))
        { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static void VerifyHash(string path, string expected)
    {
        if (!Regex.IsMatch(expected, "^[a-fA-F0-9]{64}$") ||
            !Hash(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Контрольная сумма скачанного файла не совпала. Установка отменена.");
    }

    // Reject ambiguous Windows names as well as traversal, links and oversized archives.
    public static void Extract(string zipPath, string destination, long limit = 512L * 1024 * 1024,
        CancellationToken token = default)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > 100000) throw new InvalidDataException("В архиве слишком много файлов.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            var parts = name.TrimEnd('/').Split('/');
            if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') ||
                p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM\d|LPT\d)(\.|$)", RegexOptions.IgnoreCase)) ||
                !names.Add(name.TrimEnd('/')) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException($"Недопустимое имя в архиве: {name}");
            total = checked(total + entry.Length);
            if (entry.Length > limit || total > limit)
                throw new InvalidDataException("Распакованный архив превышает допустимый размер.");
            var path = Path.GetFullPath(Path.Combine(destination, name));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Файл выходит за пределы папки распаковки.");
        }
        RejectLinks(destination);
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!);
        if (drive.AvailableFreeSpace < total + 256L * 1024 * 1024)
            throw new IOException($"Недостаточно места для распаковки: нужно ещё {total / 1024 / 1024 + 256} МБ.");
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var path = Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: false);
        }
    }

    public static Version DllVersion(string path) => AssemblyName.GetAssemblyName(path).Version
        ?? throw new InvalidDataException($"В DLL отсутствует номер версии: {path}");

    public static int ValidatePlugin(string folder, string name, Version version)
    {
        var manifest = ReadJson(File.ReadAllText(Path.Combine(folder, name + ".json")));
        if ((string?)manifest["InternalName"] != name ||
            VersionOf((string?)manifest["AssemblyVersion"] ?? "") != version ||
            DllVersion(Path.Combine(folder, name + ".dll")) != version)
            throw new InvalidDataException($"DLL, описание и номер выпуска {name} не совпадают.");
        var api = (int?)manifest["DalamudApiLevel"] ?? 0;
        if (api <= 0) throw new InvalidDataException("Не указан совместимый выпуск Dalamud.");
        if (name == "FF14Accessibility")
        {
            foreach (var file in new[] { "FF14Accessibility.deps.json", "Tolk.dll", "nvdaControllerClient64.dll",
                "System.Speech.dll", "NAudio.dll", "NAudio.Core.dll", "NAudio.WinMM.dll", "NAudio.Wasapi.dll",
                "LICENSE", "THIRD-PARTY-NOTICES.md" })
                if (!File.Exists(Path.Combine(folder, file))) throw new InvalidDataException($"В архиве нет {file}.");
            if (!Directory.Exists(Path.Combine(folder, "assets", "partymonitor")) ||
                Directory.GetFiles(Path.Combine(folder, "assets", "partymonitor"), "*.mp3").Length != 122)
                throw new InvalidDataException("В архиве отсутствует полный набор звуков.");
        }
        return api;
    }

    public static void RejectLinks(string path)
    {
        for (var p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((Directory.Exists(p) || File.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Папка или файл является ссылкой: {p}. Установка остановлена.");
        if (!Directory.Exists(path)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Обнаружена ссылка: {entry}");
            if (Directory.Exists(entry)) RejectLinks(entry);
        }
    }

    public static Dictionary<string, string> Snapshot(string folder)
    {
        RejectLinks(folder);
        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(
                f => Path.GetRelativePath(folder, f), Hash, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void VerifySnapshot(string folder, IReadOnlyDictionary<string, string> expected, bool exact = true)
    {
        var actual = Snapshot(folder);
        if ((exact && actual.Count != expected.Count) || expected.Any(p => !actual.TryGetValue(p.Key, out var hash) || hash != p.Value))
            throw new IOException($"Проверка файлов не пройдена: {folder}");
    }

    public static void CopyTree(string source, string destination)
    {
        RejectLinks(source);
        RejectLinks(destination);
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), true);
    }
}
