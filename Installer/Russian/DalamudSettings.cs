using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public sealed record InstalledPlugin(string Folder, Version? Version, bool Catalog);

public static class DalamudSettings
{
    public static JArray Array(JToken? token, string label) => token switch
    {
        JArray a => a,
        JObject o when o["$values"] is JArray a => a,
        _ => throw new InvalidDataException($"Неизвестный формат настроек {label}. Файлы не изменены.")
    };

    public static IReadOnlyList<InstalledPlugin> Find(string root, JObject config, string name)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var standard = Path.Combine(root, "devPlugins", name);
        if (Directory.Exists(standard)) paths.Add(Path.GetFullPath(standard));
        if (config["DevPluginLoadLocations"] != null)
        foreach (var item in Array(config["DevPluginLoadLocations"], "DevPluginLoadLocations"))
        {
            var path = (string?)item["Path"];
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (Path.GetFileName(path).Equals(name + ".dll", StringComparison.OrdinalIgnoreCase))
                paths.Add(Path.GetDirectoryName(Path.GetFullPath(path))!);
            else if (Directory.Exists(path) && File.Exists(Path.Combine(path, name + ".dll")))
                paths.Add(Path.GetFullPath(path));
        }
        var found = paths.Where(p => Directory.Exists(p)).Select(p => Inspect(p, name, false)).ToList();
        var catalog = Path.Combine(root, "installedPlugins", name);
        if (Directory.Exists(catalog))
        {
            PackageFiles.RejectLinks(catalog);
            var versions = Directory.GetDirectories(catalog).Append(catalog)
                .Where(p => File.Exists(Path.Combine(p, name + ".dll")))
                .Select(p => Inspect(p, name, true)).OrderByDescending(p => p.Version).ToList();
            // Dalamud keeps older catalog versions; these are one installation.
            if (versions.Count > 0) found.Add(versions[0]);
        }
        return found;
    }

    private static InstalledPlugin Inspect(string folder, string name, bool catalog)
    {
        var dll = Path.Combine(folder, name + ".dll");
        Version? version = null;
        if (File.Exists(dll))
        {
            try { version = PackageFiles.DllVersion(dll); }
            catch (BadImageFormatException) { }
        }
        return new InstalledPlugin(folder, version, catalog);
    }

    public static JObject Enable(JObject original, string name, string dll)
    {
        dll = Path.GetFullPath(dll);
        var config = (JObject)original.DeepClone();
        var locations = Array(config["DevPluginLoadLocations"], "DevPluginLoadLocations");
        if (config["DevPluginSettings"] == null) config["DevPluginSettings"] = new JObject();
        if (config["DevPluginSettings"] is not JObject settings)
            throw new InvalidDataException("Неизвестный формат DevPluginSettings.");
        var aliases = settings.Properties().Where(p => SamePath(p.Name, dll)).ToArray();
        var property = aliases.FirstOrDefault(p => p.Name == dll) ?? aliases.FirstOrDefault();
        var entry = property?.Value as JObject ?? new JObject
        {
            ["$type"] = "Dalamud.Configuration.Internal.DevPluginSettings, Dalamud",
            ["NotifyForErrors"] = true, ["AutomaticReloading"] = false
        };
        if (property != null && property.Value is not JObject) throw new InvalidDataException("Повреждены настройки плагина.");
        if (!Guid.TryParse((string?)entry["WorkingPluginId"], out var id) || id == Guid.Empty) id = Guid.NewGuid();
        entry["WorkingPluginId"] = id.ToString();
        entry["StartOnBoot"] = true;
        // Dalamud looks up dllFile.FullName in a case-sensitive dictionary. A key
        // with different casing/slashes is ignored and a new disabled identity appears.
        foreach (var alias in aliases) alias.Remove();
        settings[dll] = entry;
        config["DevMode"] = true;
        var matches = locations.OfType<JObject>().Where(p =>
            SamePath((string?)p["Path"], dll) || SamePath((string?)p["Path"], Path.GetDirectoryName(dll)!)).ToList();
        if (matches.Count == 0)
        {
            var location = new JObject { ["$type"] = "Dalamud.Configuration.DevPluginLocationSettings, Dalamud", ["Path"] = dll };
            locations.Add(location);
            matches.Add(location);
        }
        foreach (var match in matches) { match["Path"] = dll; match["IsEnabled"] = true; }
        EnableProfiles(config, name, id);
        return config;
    }

    private static bool SamePath(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || !Path.IsPathFullyQualified(left)) return false;
        return Path.GetFullPath(left).TrimEnd('\\', '/').Equals(right.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    public static void VerifyEnabled(string root, string name, InstalledPlugin plugin)
    {
        var config = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(root, "dalamudConfig.json")));
        if ((bool?)config["PluginSafeMode"] == true || File.Exists(Path.Combine(root, ".dalamud_safemode")))
            throw new InvalidOperationException("Dalamud настроен на запуск без плагинов. Выключите безопасный режим в XIVLauncher и повторите установку.");
        var dalamudDll = Path.Combine(root, "addon", "Hooks", "dev", "Dalamud.dll");
        if (File.Exists(dalamudDll))
        {
            var installedApi = PackageFiles.DllVersion(dalamudDll).Major;
            var pluginApi = (int?)PackageFiles.ReadJson(File.ReadAllText(Path.Combine(plugin.Folder, name + ".json")))["DalamudApiLevel"];
            if (pluginApi != installedApi)
                throw new InvalidOperationException(name + ": версия плагина рассчитана на Dalamud " + pluginApi +
                    ", а установлена " + installedApi + ". Обновите несовместимый компонент и повторите установку.");
        }
        Guid id;
        if (plugin.Catalog)
        {
            var manifest = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(plugin.Folder, name + ".json")));
            if (!Guid.TryParse((string?)manifest["WorkingPluginId"], out id) || id == Guid.Empty ||
                (bool?)manifest["Disabled"] == true || (bool?)manifest["ScheduledForDeletion"] == true)
                throw new InvalidDataException(name + ": установленная копия не включена или отмечена для удаления.");
            var source = (string?)manifest["InstalledFromUrl"];
            if (string.IsNullOrWhiteSpace(source) || config["ThirdRepoList"] == null ||
                !Array(config["ThirdRepoList"], "ThirdRepoList").Any(p => (string?)p["Url"] == source && (bool?)p["IsEnabled"] == true))
                throw new InvalidDataException(name + ": каталог установленного плагина не включён в Dalamud.");
        }
        else
        {
            var dll = Path.GetFullPath(Path.Combine(plugin.Folder, name + ".dll"));
            var settings = config["DevPluginSettings"]?[dll];
            if ((bool?)config["DevMode"] != true || (bool?)settings?["StartOnBoot"] != true ||
                !Guid.TryParse((string?)settings?["WorkingPluginId"], out id) || id == Guid.Empty ||
                !Array(config["DevPluginLoadLocations"], "DevPluginLoadLocations").Any(p =>
                    (string?)p["Path"] == dll && (bool?)p["IsEnabled"] == true))
                throw new InvalidDataException(name + ": автозагрузка в Dalamud не прошла проверку.");
        }
        var profiles = new[] { config["DefaultProfile"]! }.Concat(
            config["SavedProfiles"] == null ? [] : Array(config["SavedProfiles"], "SavedProfiles"));
        foreach (var profile in profiles)
        {
            var entries = Array(profile["Plugins"], "Plugins").Where(p => (string?)p["InternalName"] == name).ToArray();
            if (entries.Length == 0 || entries.Any(p => (bool?)p["IsEnabled"] != true || (Guid?)p["WorkingPluginId"] != id))
                throw new InvalidDataException(name + ": включение в профилях Dalamud не прошло проверку.");
        }
    }

    public static JObject EnableCatalog(JObject original, string name, Guid id)
    {
        if (id == Guid.Empty) throw new InvalidDataException("Не найден идентификатор установленного плагина.");
        var config = (JObject)original.DeepClone();
        EnableProfiles(config, name, id);
        return config;
    }

    private static void EnableProfiles(JObject config, string name, Guid id)
    {
        var profiles = new List<JToken> { config["DefaultProfile"] ?? throw new InvalidDataException("Нет основного профиля Dalamud.") };
        if (config["SavedProfiles"] != null) profiles.AddRange(Array(config["SavedProfiles"], "SavedProfiles"));
        foreach (var profile in profiles)
        {
            var plugins = Array(profile["Plugins"], "Plugins");
            var records = plugins.OfType<JObject>().Where(p => (string?)p["InternalName"] == name).ToList();
            if (records.Count == 0)
            {
                var record = new JObject
                {
                    ["$type"] = "Dalamud.Plugin.Internal.Profiles.ProfileModelV1+ProfileModelV1Plugin, Dalamud",
                    ["InternalName"] = name
                };
                plugins.Add(record);
                records.Add(record);
            }
            foreach (var record in records) { record["WorkingPluginId"] = id.ToString(); record["IsEnabled"] = true; }
        }
    }
}
