using System.IO.Compression;
using System.Text;
using LiteDB;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public sealed class UninstallService(string root, Action ensureClosed, Action<string> log)
{
    private const string Accessibility = "FF14Accessibility";
    private readonly HashSet<string> modKeys = new(StringComparer.OrdinalIgnoreCase) { "XIV Rus" };
    private readonly HashSet<string> pluginFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> backupRoots = new(StringComparer.OrdinalIgnoreCase);
    private readonly RemovalPlan plan = new();
    private bool removeMod;
    private bool removeTranslation;
    private bool removeModTranslation;
    private int modLanguage = 2;
    private bool missingLanguageSettings;
    private JObject? cachedLanguageSettings;
    private long cachedLanguageId = long.MinValue;

    // Language changes share the rollback-safe file/database plan with removal.
    public void SetModLanguage(int language, CancellationToken token)
    {
        if (language is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(language));
        modLanguage = language;
        Remove(false, false, token, modTranslation: true);
    }

    public string Remove(bool mod, bool translation, CancellationToken token, Action<string>? checkpoint = null, bool modTranslation = false)
    {
        if (!mod && !translation && !modTranslation) throw new ArgumentException("Не выбран компонент для удаления.");
        removeMod = mod;
        removeTranslation = translation;
        removeModTranslation = modTranslation;
        token.ThrowIfCancellationRequested();
        ensureClosed();
        root = Path.GetFullPath(root);
        RemovalPlan.RejectParents(root);
        log(modTranslation ? "Проверяю настройки языка мода и их сохранённые копии…" :
            "Подготавливаю полное удаление. Проверяю файлы, настройки, кеш и резервные копии…");
        var configPath = Path.Combine(root, "dalamudConfig.json");
        var config = File.Exists(configPath) ? PackageFiles.ReadJson(Encoding.UTF8.GetString(plan.Read(configPath))) : new JObject();
        backupRoots.Add(Path.Combine(root, "installer-backups"));
        if (mod) DiscoverPlugin(config);
        if (translation) DiscoverTranslation();
        if (modTranslation && !mod) DiscoverModLanguage();
        if (mod)
        {
            foreach (var folder in pluginFolders) plan.Delete(folder);
            DeleteNamed(Path.Combine(root, "pluginConfigs"), Accessibility);
            plan.Edit(configPath, CleanDalamud);
            foreach (var file in Files(root).Where(p => Path.GetFileName(p).StartsWith("dalamudConfig.json.", StringComparison.OrdinalIgnoreCase)))
                plan.Edit(file, CleanDalamud);
            // Dalamud's own per-plugin ZIP backups are separate from installer backups.
            DeleteNamed(Path.Combine(root, "backups"), Accessibility);
        }
        if (!modTranslation || modLanguage == 2)
            foreach (var backups in backupRoots) CleanBackups(backups);
        CleanVfs();
        if (missingLanguageSettings)
        {
            var value = cachedLanguageSettings ?? new JObject { ["Version"] = 2 };
            value["Language"] = modLanguage;
            plan.Create(Path.Combine(root, "pluginConfigs", Accessibility + ".json"), Encoding.UTF8.GetBytes(value.ToString()));
        }
        if (translation) CleanPenumbraBackups();
        if ((mod || translation) && RemovalPlan.Same(root, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher")))
        {
            plan.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF14AccessibilityInstaller"));
            if (mod)
                foreach (var name in new[] { "FFXIV_Keybinds.txt", "FF14_Sprache.txt", "FF14_Auftritt.txt", "FFXIV_Spawn.txt", "FFXIV_CS_Dump.txt", "FFXIV_UI_Dump.txt" })
                    plan.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), name));
        }
        token.ThrowIfCancellationRequested();
        plan.Apply(ensureClosed, log, token, checkpoint);
        if (modTranslation && !mod && !translation)
            return modLanguage == 3 ? "Русский язык мода включён." :
                "Русификация мода удалена: язык сообщений и речи переключён на английский. Запустите игру заново через XIVLauncher.";
        log("Отсутствие удалённых файлов проверено. Резервная копия удалённых компонентов не оставлена.");
        return mod && translation ? "Мод и русификация полностью удалены." :
            mod ? "Мод полностью удалён: файлы, настройки, личные клавиши, маршруты и записи автозагрузки." :
            "Русификация игры полностью удалена: XIV Rus, записи в коллекциях, кеш и прежние копии перевода.";
    }

    private void DiscoverPlugin(JObject config)
    {
        var standard = Path.Combine(root, "devPlugins", Accessibility);
        ValidateDedicated(standard);
        pluginFolders.Add(standard);
        pluginFolders.Add(Path.Combine(root, "installedPlugins", Accessibility));
        var locations = config["DevPluginLoadLocations"] == null ? [] : DalamudSettings.Array(config["DevPluginLoadLocations"], "DevPluginLoadLocations");
        var paths = locations.Select(p => (string?)p["Path"]).Concat((config["DevPluginSettings"] as JObject)?.Properties()
            .Where(p => p.Name != "$type").Select(p => (string?)p.Name) ?? []);
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            if (!Path.IsPathFullyQualified(path!)) throw new InvalidDataException("Неполный путь плагина в настройках Dalamud: " + path);
            var folder = Path.GetExtension(path!).Equals(".dll", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(path!)! : path!;
            if (!Path.GetFileName(path!).Equals(Accessibility + ".dll", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(Path.Combine(folder, Accessibility + ".dll")) &&
                !Path.GetFileName(folder).Equals(Accessibility, StringComparison.OrdinalIgnoreCase)) continue;
            folder = Path.GetFullPath(folder);
            if (!RemovalPlan.Same(folder, standard)) ValidateDedicated(folder);
            pluginFolders.Add(folder);
            backupRoots.Add(Path.Combine(Path.GetDirectoryName(folder)!, ".FF14Accessibility-backups"));
        }
        // Recover deletion interrupted after settings were written or during erasure.
        foreach (var parent in new[] { Path.Combine(root, "devPlugins"), Path.Combine(root, "installedPlugins") })
            foreach (var folder in Directories(parent).Where(p => Path.GetFileName(p).StartsWith(Accessibility + ".removing-", StringComparison.OrdinalIgnoreCase)))
                pluginFolders.Add(folder);
    }

    private void ValidateDedicated(string folder)
    {
        if (!Directory.Exists(folder)) return;
        PackageFiles.RejectLinks(folder);
        var protectedPaths = new[] { root, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") };
        if (protectedPaths.Any(p => RemovalPlan.Same(p, folder)) || Directory.Exists(Path.Combine(folder, ".git")) ||
            Directory.GetFiles(folder, "*.csproj").Length > 0 || Directory.GetFiles(folder, "*.sln*").Length > 0)
            throw new IOException("Папка плагина содержит проект или личные файлы. Полное удаление остановлено: " + folder);
        var manifest = Path.Combine(folder, Accessibility + ".json");
        if (!Path.GetFileName(folder).Equals(Accessibility, StringComparison.OrdinalIgnoreCase) &&
            (!File.Exists(manifest) || (string?)PackageFiles.ReadJson(File.ReadAllText(manifest))["InternalName"] != Accessibility))
            throw new IOException("Не подтверждена отдельная папка мода. Файлы не изменены: " + folder);
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            if (!File.Exists(Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + ".dll"))) continue;
            var value = PackageFiles.ReadJson(File.ReadAllText(file));
            if (value["InternalName"] is JValue name && (string?)name != Accessibility)
                throw new IOException("В одной папке находятся разные плагины. Удаление остановлено: " + folder);
        }
    }

    private bool PluginPath(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) &&
        (Path.GetFileName(path).Equals(Accessibility + ".dll", StringComparison.OrdinalIgnoreCase) ||
         pluginFolders.Any(p => RemovalPlan.Same(path, p) || RemovalPlan.Under(path, p)));

    private bool CleanDalamud(JObject value)
    {
        var changed = false;
        if (value["DevPluginLoadLocations"] != null)
        {
            var entries = DalamudSettings.Array(value["DevPluginLoadLocations"], "DevPluginLoadLocations");
            foreach (var entry in entries.Where(p => PluginPath((string?)p["Path"])).ToArray()) { entry.Remove(); changed = true; }
        }
        if (value["DevPluginSettings"] != null && value["DevPluginSettings"] is not JObject)
            throw new InvalidDataException("Неизвестный формат DevPluginSettings. Удаление остановлено.");
        if (value["DevPluginSettings"] is JObject settings)
            foreach (var entry in settings.Properties().Where(p => PluginPath(p.Name)).ToArray()) { entry.Remove(); changed = true; }
        var profiles = new List<JToken>();
        if (value["DefaultProfile"] != null) profiles.Add(value["DefaultProfile"]!);
        if (value["SavedProfiles"] != null) profiles.AddRange(DalamudSettings.Array(value["SavedProfiles"], "SavedProfiles"));
        foreach (var profile in profiles)
            foreach (var entry in DalamudSettings.Array(profile["Plugins"], "Plugins")
                .Where(p => string.Equals((string?)p["InternalName"], Accessibility, StringComparison.OrdinalIgnoreCase)).ToArray())
            { entry.Remove(); changed = true; }
        return changed;
    }

    private void DiscoverTranslation()
    {
        var pen = Path.Combine(root, "pluginConfigs", "Penumbra");
        var modRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(root, "PenumbraMods") };
        foreach (var path in new[] { Path.Combine(pen, "config", "penumbra.json"), Path.Combine(root, "pluginConfigs", "Penumbra.json") })
        {
            if (!File.Exists(path)) continue;
            var value = PackageFiles.ReadJson(Encoding.UTF8.GetString(plan.Read(path)));
            var directory = (string?)value["ModDirectory"];
            if (string.IsNullOrWhiteSpace(directory)) continue;
            if (!Path.IsPathFullyQualified(directory) || RemovalPlan.Same(directory, Path.GetPathRoot(directory)!))
                throw new InvalidDataException("Неподходящая папка модов Penumbra: " + directory);
            modRoots.Add(Path.GetFullPath(directory));
        }
        // A translation may have been moved since installation. Old configuration
        // backups and Penumbra ZIPs retain those locations and old directory keys.
        foreach (var archive in Files(Path.Combine(root, "backups", "Penumbra")).Where(p => Path.GetExtension(p).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = new MemoryStream(plan.Read(archive));
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries.Where(e => e.FullName.Replace('\\', '/').EndsWith("/config/penumbra.json", StringComparison.OrdinalIgnoreCase)))
            {
                if (entry.Length > 4 * 1024 * 1024) throw new InvalidDataException("Слишком большой файл настроек в архиве Penumbra.");
                using var reader = new StreamReader(entry.Open());
                AddOldRoot((string?)PackageFiles.ReadJson(reader.ReadToEnd())["ModDirectory"]);
            }
        }
        var scanned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            foreach (var directory in modRoots)
                backupRoots.Add(Path.Combine(Path.GetDirectoryName(directory)!, Path.GetFileName(directory) + "-backup"));
            var pending = backupRoots.Where(p => !scanned.Contains(p)).ToArray();
            if (pending.Length == 0) break;
            foreach (var backups in pending)
            {
                scanned.Add(backups);
                PackageFiles.RejectLinks(backups);
                foreach (var folder in Directories(backups))
                {
                    var description = Path.Combine(folder, "README.txt");
                    if (File.Exists(description))
                        foreach (var line in Encoding.UTF8.GetString(plan.Read(description)).Split('\n'))
                            if (line.StartsWith("Папка перевода: ", StringComparison.Ordinal))
                            {
                                var target = line["Папка перевода: ".Length..].Trim();
                                if (!Path.IsPathFullyQualified(target)) continue;
                                modKeys.Add(Path.GetFileName(target));
                                AddOldRoot(Path.GetDirectoryName(target));
                            }
                    var mapPath = Path.Combine(folder, "restore-map.json");
                    if (!File.Exists(mapPath)) continue;
                    var map = JArray.Parse(Encoding.UTF8.GetString(plan.Read(mapPath)).TrimStart('\uFEFF'));
                    foreach (var item in map)
                    {
                        var original = (string?)item["OriginalPath"];
                        var name = (string?)item["BackupFile"];
                        if (original == null || name == null || Path.GetFileName(name) != name ||
                            !Path.GetFileName(original).Equals("penumbra.json", StringComparison.OrdinalIgnoreCase)) continue;
                        var file = Path.Combine(folder, name);
                        if (File.Exists(file)) AddOldRoot((string?)PackageFiles.ReadJson(Encoding.UTF8.GetString(plan.Read(file)))["ModDirectory"]);
                    }
                }
            }
        }
        foreach (var directory in modRoots)
        {
            RemovalPlan.RejectParents(directory);
            foreach (var folder in Directories(directory))
                if (IsTranslation(folder)) { modKeys.Add(Path.GetFileName(folder)); plan.Delete(folder); }
            backupRoots.Add(Path.Combine(Path.GetDirectoryName(directory)!, Path.GetFileName(directory) + "-backup"));
            foreach (var stage in Directories(Path.GetDirectoryName(directory)!).Where(p =>
                Path.GetFileName(p).StartsWith(".xivrus-stage-", StringComparison.OrdinalIgnoreCase))) plan.Delete(stage);
        }
        CleanPenumbraTree(pen);
        foreach (var file in Files(Path.Combine(root, "installer-cache")).Where(p => Path.GetFileName(p).StartsWith("XIVRus-", StringComparison.OrdinalIgnoreCase)))
            plan.Delete(file);
        void AddOldRoot(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            if (!Path.IsPathFullyQualified(directory) || RemovalPlan.Same(directory, Path.GetPathRoot(directory)!))
                throw new InvalidDataException("Неподходящая прежняя папка модов Penumbra: " + directory);
            modRoots.Add(Path.GetFullPath(directory));
        }
    }

    private void DiscoverModLanguage()
    {
        var path = Path.Combine(root, "pluginConfigs", Accessibility + ".json");
        // Auto follows Windows and can still speak Russian when no setting exists.
        missingLanguageSettings = !File.Exists(path);
        if (modLanguage == 3 && !missingLanguageSettings)
        {
            var before = plan.Read(path);
            var value = PackageFiles.ReadJson(Encoding.UTF8.GetString(before));
            if ((int?)value["Language"] != 3)
            {
                var backup = Path.Combine(root, "installer-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
                plan.Create(Path.Combine(backup, "0.json"), before);
                plan.Create(Path.Combine(backup, "restore-map.json"), Encoding.UTF8.GetBytes(new JArray(new JObject
                { ["OriginalPath"] = path, ["BackupFile"] = "0.json", ["Existed"] = true }).ToString()));
                log("Резервная копия настройки языка подготовлена.");
            }
        }
        foreach (var file in Files(Path.GetDirectoryName(path)!).Where(p => Path.GetFileName(p).Equals(Accessibility + ".json", StringComparison.OrdinalIgnoreCase) ||
            modLanguage == 2 && Path.GetFileName(p).StartsWith(Accessibility + ".json.", StringComparison.OrdinalIgnoreCase)))
            plan.Edit(file, ChangeLanguage);
    }

    private bool ChangeLanguage(JObject value)
    {
        if ((int?)value["Language"] == modLanguage) return false;
        value["Language"] = modLanguage; // Explicit English, because Auto on Russian Windows is still Russian.
        return true;
    }

    private bool IsTranslation(string folder)
    {
        if (Path.GetFileName(folder).Equals("XIV Rus", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(folder).StartsWith("XIV Rus.removing-", StringComparison.OrdinalIgnoreCase)) return true;
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return false;
        var meta = Path.Combine(folder, "meta.json");
        return File.Exists(meta) && string.Equals((string?)PackageFiles.ReadJson(File.ReadAllText(meta))["Name"], "XIV Rus", StringComparison.OrdinalIgnoreCase);
    }

    private void CleanPenumbraTree(string pen)
    {
        PackageFiles.RejectLinks(pen);
        foreach (var file in FilesRecursive(pen).Where(p => Path.GetExtension(p).Equals(".json", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "mod_data", StringComparison.OrdinalIgnoreCase) && modKeys.Contains(Path.GetFileNameWithoutExtension(file)))
                plan.Delete(file);
            else plan.EditToken(file, CleanTranslationJson);
        }
        var database = Path.Combine(pen, "mod_data.db");
        if (File.Exists(database)) plan.Replace(database, CleanLiteDb(plan.Read(database)));
        // Diagnostic logs can retain old translation paths; they are disposable.
        foreach (var file in Files(pen).Where(p => Path.GetExtension(p).Equals(".log", StringComparison.OrdinalIgnoreCase))) plan.Delete(file);
    }

    private bool CleanTranslationJson(JToken value)
    {
        var changed = false;
        var descendants = value is JContainer container ? container.Descendants().ToArray() : [];
        foreach (var property in descendants.OfType<JProperty>().Where(p => modKeys.Contains(p.Name)).ToArray())
        { property.Remove(); changed = true; }
        // Old sort order and new selected/locked filesystem nodes can use values.
        foreach (var item in (value is JContainer current ? current.Descendants().OfType<JValue>().ToArray() : []).Where(p => p.Type == JTokenType.String &&
                     modKeys.Contains((string)p!)).ToArray())
        {
            if (item.Parent is JArray) { item.Remove(); changed = true; }
            else if (item.Parent is JProperty property) { property.Remove(); changed = true; }
        }
        return changed;
    }

    private byte[] CleanLiteDb(byte[] original)
    {
        using var stream = new MemoryStream();
        stream.Write(original);
        stream.Position = 0;
        using (var database = new LiteDatabase(stream))
        {
            var names = database.GetCollectionNames().ToArray();
            foreach (var name in new[] { "LocalModData", "PresetData" })
            {
                if (!names.Contains(name)) continue;
                var collection = database.GetCollection<BsonDocument>(name);
                foreach (var document in collection.FindAll().Where(d =>
                    modKeys.Contains(d["_id"].IsString ? d["_id"].AsString : "") ||
                    modKeys.Contains(d["Mod"].IsString ? d["Mod"].AsString : "")).ToArray()) collection.Delete(document["_id"]);
            }
            // Rebuild removes deleted documents from free pages as well.
            database.Rebuild();
        }
        return stream.ToArray();
    }

    private void CleanBackups(string backups)
    {
        PackageFiles.RejectLinks(backups);
        foreach (var folder in Directories(backups))
        {
            var description = Path.Combine(folder, "README.txt");
            var ownModBackup = removeMod && File.Exists(description) && File.ReadAllText(description)
                .StartsWith("Установка " + Accessibility + " завершена.", StringComparison.Ordinal);
            if (ownModBackup) { plan.Delete(folder); continue; }
            if (removeMod) DeleteNamed(folder, Accessibility);
            if (removeTranslation)
                foreach (var child in Directories(folder).Where(p => IsTranslation(p) ||
                    Path.GetFileName(p) is "previous-mod" or "incomplete-mod")) plan.Delete(child);
            var restoreMap = Path.Combine(folder, "restore-map.json");
            if (File.Exists(restoreMap))
            {
                var map = JArray.Parse(Encoding.UTF8.GetString(plan.Read(restoreMap)).TrimStart('\uFEFF'));
                foreach (var entry in map.ToArray())
                {
                    var original = (string?)entry["OriginalPath"] ?? throw new InvalidDataException("Повреждена карта резервной копии: " + restoreMap);
                    var backupName = (string?)entry["BackupFile"];
                    if (backupName != null && (Path.GetFileName(backupName) != backupName ||
                        !int.TryParse(Path.GetFileNameWithoutExtension(backupName), out var index) || index < 0 ||
                        !Path.GetExtension(backupName).Equals(".json", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("Путь выходит за резервную копию.");
                    var backupFile = backupName == null ? null : Path.Combine(folder, backupName);
                    if (removeMod && (PluginPath(original) || Path.GetFileName(original).StartsWith(Accessibility + ".json", StringComparison.OrdinalIgnoreCase)))
                    { if (backupFile != null) plan.Delete(backupFile); entry.Remove(); }
                    else if (backupFile == null) continue;
                    else if (Path.GetFileName(original).Equals("dalamudConfig.json", StringComparison.OrdinalIgnoreCase) && removeMod)
                        plan.Edit(backupFile, CleanDalamud);
                    else if (removeModTranslation && Path.GetFileName(original).StartsWith(Accessibility + ".json", StringComparison.OrdinalIgnoreCase))
                        plan.Edit(backupFile, ChangeLanguage);
                    else if (removeTranslation && RemovalPlan.Under(original, Path.Combine(root, "pluginConfigs", "Penumbra")))
                        plan.EditToken(backupFile, CleanTranslationJson);
                }
                plan.Replace(restoreMap, Encoding.UTF8.GetBytes(map.ToString()));
                if (map.Count == 0) { plan.Delete(folder); continue; }
            }
            if (removeMod) plan.Edit(Path.Combine(folder, "dalamudConfig.json"), CleanDalamud);
            // These descriptions contain paths to removed versions and have no settings.
            if (removeTranslation && File.Exists(restoreMap)) plan.Delete(description);
        }
    }

    private void CleanVfs()
    {
        var path = Path.Combine(root, "dalamudVfs.db");
        if (!File.Exists(path)) return;
        // Work on a private copy; the real database changes with the same removal plan.
        using var temp = new TemporaryFolder();
        var copy = Path.Combine(temp.Path, "vfs.db");
        File.WriteAllBytes(copy, plan.Read(path));
        // A live WAL may contain the latest committed settings. Fold it into the copy.
        if (File.Exists(path + "-wal")) File.WriteAllBytes(copy + "-wal", plan.Read(path + "-wal"));
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            connection.Open();
            using var select = connection.CreateCommand();
            select.CommandText = "SELECT Id, Path, Data FROM DbFile";
            var records = new List<(long Id, string Path, byte[] Bytes)>();
            using (var reader = select.ExecuteReader())
                while (reader.Read()) records.Add((reader.GetInt64(0), reader.GetString(1), (byte[])reader[2]));
            var anyChanged = false;
            foreach (var record in records)
            {
                var originalPath = Environment.ExpandEnvironmentVariables(record.Path);
                var isConfig = removeMod && (RemovalPlan.Same(originalPath, Path.Combine(root, "pluginConfigs", Accessibility + ".json")) ||
                    RemovalPlan.Under(originalPath, Path.Combine(root, "pluginConfigs", Accessibility)));
                byte[]? next = null;
                var modConfig = RemovalPlan.Same(originalPath, Path.Combine(root, "pluginConfigs", Accessibility + ".json"));
                var relevant = removeMod && Path.GetFileName(originalPath).Equals("dalamudConfig.json", StringComparison.OrdinalIgnoreCase) ||
                    removeTranslation && RemovalPlan.Under(originalPath, Path.Combine(root, "pluginConfigs", "Penumbra")) ||
                    removeModTranslation && modConfig;
                var value = !isConfig && relevant && Path.GetExtension(originalPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? RemovalPlan.Parse(record.Bytes) : null;
                if (value != null)
                {
                    var changed = removeMod && Path.GetFileName(originalPath).Equals("dalamudConfig.json", StringComparison.OrdinalIgnoreCase) && CleanDalamud((JObject)value);
                    if (removeTranslation && RemovalPlan.Under(originalPath, Path.Combine(root, "pluginConfigs", "Penumbra"))) changed |= CleanTranslationJson(value);
                    if (removeModTranslation && modConfig)
                    {
                        if (record.Id > cachedLanguageId)
                        {
                            cachedLanguageSettings = (JObject)value.DeepClone();
                            cachedLanguageId = record.Id;
                        }
                        changed |= ChangeLanguage((JObject)value);
                    }
                    if (changed) next = Encoding.UTF8.GetBytes(value.ToString());
                }
                if (!isConfig && next == null) continue;
                anyChanged = true;
                using var command = connection.CreateCommand();
                command.CommandText = isConfig ? "DELETE FROM DbFile WHERE Id = $id" : "UPDATE DbFile SET Data = $data WHERE Id = $id";
                command.Parameters.AddWithValue("$id", record.Id);
                if (next != null) command.Parameters.AddWithValue("$data", next);
                command.ExecuteNonQuery();
            }
            if (!anyChanged) return;
            using var compact = connection.CreateCommand();
            compact.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA journal_mode=DELETE; VACUUM;";
            compact.ExecuteNonQuery();
        }
        plan.Replace(path, File.ReadAllBytes(copy));
        plan.Delete(path + "-wal");
        plan.Delete(path + "-shm");
    }

    private void CleanPenumbraBackups()
    {
        var backups = Path.Combine(root, "backups", "Penumbra");
        foreach (var path in Files(backups).Where(p => Path.GetExtension(p).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
        {
            using var temp = new TemporaryFolder();
            var source = Path.Combine(temp.Path, "backup.zip");
            File.WriteAllBytes(source, plan.Read(path));
            var extract = Path.Combine(temp.Path, "files");
            PackageFiles.Extract(source, extract);
            // Rewrite only verified Penumbra backup files, preserving other mods.
            foreach (var file in Directory.GetFiles(extract, "*.json", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(Path.GetDirectoryName(file)) == "mod_data" && modKeys.Contains(Path.GetFileNameWithoutExtension(file))) File.Delete(file);
                else
                {
                    var value = RemovalPlan.Parse(File.ReadAllBytes(file));
                    if (CleanTranslationJson(value)) File.WriteAllText(file, value.ToString());
                }
            }
            foreach (var file in Directory.GetFiles(extract, "mod_data.db", SearchOption.AllDirectories)) File.WriteAllBytes(file, CleanLiteDb(File.ReadAllBytes(file)));
            var rebuilt = Path.Combine(temp.Path, "clean.zip");
            ZipFile.CreateFromDirectory(extract, rebuilt);
            plan.Replace(path, File.ReadAllBytes(rebuilt));
        }
    }

    private void DeleteNamed(string parent, string name)
    {
        PackageFiles.RejectLinks(parent);
        foreach (var path in Entries(parent).Where(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(p).StartsWith(name + ".", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(p).StartsWith(name + "-", StringComparison.OrdinalIgnoreCase))) plan.Delete(path);
    }
    private static IEnumerable<string> Files(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    private static IEnumerable<string> Directories(string folder) => Directory.Exists(folder) ? Directory.GetDirectories(folder) : [];
    private static IEnumerable<string> Entries(string folder) => Directory.Exists(folder) ? Directory.GetFileSystemEntries(folder) : [];
    private static IEnumerable<string> FilesRecursive(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories) : [];
}
