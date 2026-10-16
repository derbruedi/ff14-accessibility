using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public sealed class TranslationInstaller(string root, ReleaseClient releases, Action ensureClosed, Action<string> log)
{
    public const string WaitForPluginsSetting = "IsResumeGameAfterPluginLoad";

    public static bool WaitsForPlugins(JObject config) =>
        config[WaitForPluginsSetting]?.Type == JTokenType.Boolean && (bool)config[WaitForPluginsSetting]!;

    private string PenumbraRoot => Path.Combine(root, "pluginConfigs", "Penumbra");
    private string Backups => Path.Combine(root, "installer-backups");

    public async Task EnsurePenumbra(CancellationToken token)
    {
        ensureClosed();
        var cfg = new JsonChange(Path.Combine(root, "dalamudConfig.json"));
        var found = DalamudSettings.Find(root, cfg.Value, "Penumbra");
        if (found.Count > 1) throw new InvalidOperationException("Найдено несколько копий Penumbra. Оставьте одну через Dalamud и повторите установку.");
        if (found.SingleOrDefault() is { } existing && existing.Version != null)
        {
            PackageFiles.ValidatePlugin(existing.Folder, "Penumbra", existing.Version);
            if (existing.Catalog)
            {
                var manifest = new JsonChange(Path.Combine(existing.Folder, "Penumbra.json"));
                var raw = (string?)manifest.Value["WorkingPluginId"];
                if (!Guid.TryParse(raw, out var id) || id == Guid.Empty)
                {
                    var profiles = new[] { cfg.Value["DefaultProfile"]! }.Concat(cfg.Value["SavedProfiles"] == null
                        ? [] : DalamudSettings.Array(cfg.Value["SavedProfiles"], "SavedProfiles"));
                    var ids = profiles.SelectMany(p => DalamudSettings.Array(p["Plugins"], "Plugins"))
                        .Where(p => (string?)p["InternalName"] == "Penumbra")
                        .Select(p => Guid.TryParse((string?)p["WorkingPluginId"], out var candidate) ? candidate : Guid.Empty)
                        .Where(candidate => candidate != Guid.Empty).Distinct().ToArray();
                    if (ids.Length > 1)
                        throw new InvalidDataException("Профили Dalamud содержат разные записи Penumbra, а её описание потеряло идентификатор. Нужна проверка этих настроек.");
                    id = ids.SingleOrDefault();
                    if (id == Guid.Empty) id = Guid.NewGuid();
                }
                var enabled = DalamudSettings.EnableCatalog(cfg.Value, "Penumbra", id);
                cfg.Value.ReplaceContents(enabled);
                manifest.Value["WorkingPluginId"] = id.ToString();
                if (manifest.Value.ContainsKey("Disabled")) manifest.Value["Disabled"] = false;
                if (manifest.Value.ContainsKey("ScheduledForDeletion")) manifest.Value["ScheduledForDeletion"] = false;
                // Catalog plugins with an absent/disabled source repository are orphaned
                // in Dalamud and will not load even when every profile says enabled.
                var source = (string?)manifest.Value["InstalledFromUrl"];
                if (string.IsNullOrWhiteSpace(source) || source == "OFFICIAL")
                    manifest.Value["InstalledFromUrl"] = source = ReleaseClient.PenumbraUrl;
                if (cfg.Value["ThirdRepoList"] == null) cfg.Value["ThirdRepoList"] = new JArray();
                var repos = DalamudSettings.Array(cfg.Value["ThirdRepoList"], "ThirdRepoList");
                var repo = repos.OfType<JObject>().FirstOrDefault(r => (string?)r["Url"] == source);
                if (repo == null)
                {
                    if (source != ReleaseClient.PenumbraUrl)
                        throw new InvalidDataException("Каталог Penumbra не найден в Dalamud: " + source + ". Восстановите прежний каталог в настройках Dalamud.");
                    repo = new JObject { ["Url"] = source };
                    repos.Add(repo);
                }
                repo["IsEnabled"] = true;
                SettingsTransaction.Apply(Backups, [cfg, manifest], ensureClosed, log);
            }
            else InstallTransaction.Apply(root, "Penumbra", existing.Folder, null, cfg.Original!,
                PackageFiles.Snapshot(existing.Folder), log, ensureClosed);
            DalamudSettings.VerifyEnabled(root, "Penumbra", existing);
            log("Penumbra найдена; запись автозагрузки и включение в профилях Dalamud проверены. Папка: " + existing.Folder);
            return;
        }
        if (found.SingleOrDefault()?.Catalog == true)
            throw new InvalidDataException("Каталоговая Penumbra повреждена. Восстановите её через каталог Dalamud.");
        var release = await releases.Penumbra(token);
        var target = found.SingleOrDefault()?.Folder ?? Path.Combine(root, "devPlugins", "Penumbra");
        var before = PackageFiles.Snapshot(target);
        DalamudSettings.Enable(cfg.Value, "Penumbra", Path.Combine(target, "Penumbra.dll"));
        using var temp = new TemporaryFolder();
        var zip = Path.Combine(temp.Path, "Penumbra.zip");
        log("Скачиваю Penumbra " + release.DisplayVersion + " из каталога разработчика…");
        await releases.Download(release, zip, log, token);
        var payload = Path.Combine(temp.Path, "plugin");
        PackageFiles.Extract(zip, payload, token: token);
        if (PackageFiles.ValidatePlugin(payload, "Penumbra", release.Version) != release.ApiLevel)
            throw new InvalidDataException("Версия Dalamud в архиве Penumbra не совпала с каталогом.");
        token.ThrowIfCancellationRequested();
        InstallTransaction.Apply(root, "Penumbra", target, payload, cfg.Original!, before, log, ensureClosed);
        DalamudSettings.VerifyEnabled(root, "Penumbra", new InstalledPlugin(target, release.Version, false));
        log("Penumbra установлена; запись автозагрузки и включение в профилях Dalamud проверены.");
    }

    public void SetRussianLanguage(CancellationToken token = default) =>
        new UninstallService(root, ensureClosed, log).SetModLanguage(3, token);

    public async Task<string> Install(CancellationToken token, IEnumerable<string>? searchFolders = null)
    {
        ensureClosed();
        // XIV Rus replaces early-loaded interface tables. Enabling Penumbra alone
        // is insufficient: Dalamud must hold the game until plugins have loaded.
        // Save this with the translation so failed preparation cannot report success.
        var startup = new JsonChange(Path.Combine(root, "dalamudConfig.json"));
        startup.Value[WaitForPluginsSetting] = true;
        var release = await releases.Translation(token);
        log("Официальный XIV Rus: " + release.DisplayVersion + ". Источник: https://xivrus.ru/download");
        var configPath = Path.Combine(PenumbraRoot, "config", "penumbra.json");
        var collections = Path.Combine(PenumbraRoot, "collections");
        var activePath = Path.Combine(PenumbraRoot, "active_collections.json");
        var legacyPath = Path.Combine(root, "pluginConfigs", "Penumbra.json");
        if (!File.Exists(configPath) && !File.Exists(legacyPath))
        {
            var dalamud = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(root, "dalamudConfig.json")));
            var version = DalamudSettings.Find(root, dalamud, "Penumbra").Single().Version;
            if (version == null || version.Major != 1 || version.Minor != 7 || version.Build != 2)
                throw new InvalidDataException("Автоматическое создание настроек проверено для Penumbra 1.7.2. " +
                    "Для установленной версии " + version + " требуется совместимое обновление установщика или Penumbra.");
        }
        // Penumbra 1.7.2 MainConfig.Load does NOT create the file on first boot.
        // Prepare the supported format ourselves, in the same transaction as the mod.
        var config = new JsonChange(configPath, new JObject { ["Version"] = 100 });
        if ((int?)config.Value["Version"] != 100)
            throw new InvalidDataException("Формат настроек Penumbra изменился. Требуется обновление установщика.");
        // An older config can still be migrated by Penumbra on its next boot. Keep its
        // mod directory and enablement consistent so migration cannot undo this setup.
        var legacy = File.Exists(legacyPath) ? new JsonChange(legacyPath) : null;
        if (legacy != null && (int?)legacy.Value["Version"] != 15)
            throw new InvalidDataException("Найден старый формат настроек Penumbra. Обновите Penumbra через Dalamud: " + legacyPath);
        var modRoot = (string?)(legacy?.Value["ModDirectory"] ?? config.Value["ModDirectory"]);
        if (string.IsNullOrWhiteSpace(modRoot)) modRoot = Path.Combine(root, "PenumbraMods");
        if (!Path.IsPathFullyQualified(modRoot) || Path.GetPathRoot(modRoot) == modRoot)
            throw new InvalidDataException("В Penumbra указана неподходящая папка модов.");
        modRoot = Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);
        PackageFiles.RejectLinks(modRoot);
        config.Value["ModDirectory"] = modRoot;
        config.Value["EnableMods"] = true;
        PackageFiles.RejectLinks(collections);
        var changes = (Directory.Exists(collections) ? Directory.GetFiles(collections, "*.json") : [])
            .Order().Select(p => new JsonChange(p)).ToList();
        foreach (var collection in changes)
            if ((int?)collection.Value["Version"] != 2 || collection.Value["Settings"] is not JObject ||
                string.IsNullOrWhiteSpace((string?)collection.Value["Name"]) ||
                !Guid.TryParse((string?)collection.Value["Id"], out var id) || id == Guid.Empty)
                throw new InvalidDataException("Коллекция Penumbra повреждена или имеет неизвестный формат: " + collection.Path);
        if (changes.GroupBy(c => Guid.Parse((string)c.Value["Id"]!)).Any(g => g.Count() > 1))
            throw new InvalidDataException("Найдены коллекции Penumbra с одинаковыми идентификаторами. Настройки сохранены без изменений.");
        if (changes.Count == 0)
        {
            var id = Guid.NewGuid().ToString();
            changes.Add(new JsonChange(Path.Combine(collections, id + ".json"), new JObject
            {
                ["Version"] = 2, ["Id"] = id, ["Name"] = "Default",
                ["Settings"] = new JObject(), ["Inheritance"] = new JArray()
            }));
        }
        var fallback = (string?)changes.FirstOrDefault(c => (string?)c.Value["Name"] == "Default")?.Value["Id"]
            ?? (string)changes[0].Value["Id"]!;
        var active = new JsonChange(activePath, new JObject
        {
            ["Version"] = 2, ["Default"] = fallback, ["Interface"] = fallback,
            ["Current"] = fallback, ["Individuals"] = new JArray()
        });
        if ((int?)active.Value["Version"] != 2) throw new InvalidDataException("Неизвестный формат активных коллекций Penumbra.");
        // Both EXD and interface resources must have a real collection, not the empty collection.
        foreach (var key in new[] { "Default", "Interface" })
        {
            if (!Guid.TryParse((string?)active.Value[key], out var id)) throw new InvalidDataException("Неверная активная коллекция Penumbra.");
            if (id == Guid.Empty) active.Value[key] = fallback;
            else if (!changes.Any(c => Guid.Parse((string)c.Value["Id"]!) == id))
                throw new InvalidDataException("Активная коллекция Penumbra не найдена на диске.");
        }
        var matches = new List<string>();
        if (Directory.Exists(modRoot))
        foreach (var folder in Directory.GetDirectories(modRoot))
        {
            var path = Path.Combine(folder, "meta.json");
            if (File.Exists(path) && (string?)PackageFiles.ReadJson(File.ReadAllText(path))["Name"] == "XIV Rus") matches.Add(folder);
        }
        if (matches.Count > 1) throw new InvalidDataException("Найдено несколько установленных XIV Rus. Оставьте одну копию через Penumbra.");
        var target = matches.SingleOrDefault() ?? Path.Combine(modRoot, "XIV Rus");
        if (matches.Count == 0 && Directory.Exists(target))
            throw new InvalidDataException("Папка XIV Rus занята неизвестными файлами. Сохраните их в другом месте.");
        var reuse = false;
        if (matches.Count == 1)
        {
            try { reuse = ValidateMod(target) >= release.Version; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or Newtonsoft.Json.JsonException)
            { log("Установленный перевод неполон; подготовлю восстановление. " + ex.Message); }
        }
        var modKey = Path.GetFileName(target);
        foreach (var collection in changes)
        {
            var settings = (JObject)collection.Value["Settings"]!;
            if (settings[modKey] != null && settings[modKey]!.Type != JTokenType.Null && settings[modKey] is not JObject)
                throw new InvalidDataException("Неизвестный формат настроек XIV Rus в коллекции.");
            var entry = settings[modKey] as JObject ?? new JObject { ["Priority"] = 100 };
            entry["Enabled"] = true;
            settings[modKey] = entry;
        }
        changes.Add(config);
        changes.Add(active);
        changes.Add(startup);
        if (legacy != null)
        {
            legacy.Value["ModDirectory"] = modRoot;
            legacy.Value["EnableMods"] = true;
            changes.Add(legacy);
        }
        if (changes.Any(c => c.Original == null))
            log("Подготовлены недостающие настройки Penumbra. Повторно входить в игру для их создания не требуется.");
        var backupRoot = Path.Combine(Path.GetDirectoryName(modRoot)!, Path.GetFileName(modRoot) + "-backup");
        if (reuse)
        {
            token.ThrowIfCancellationRequested();
            SettingsTransaction.Apply(backupRoot, changes, ensureClosed, log);
            log("Найден полный установленный перевод: " + target + ". Повторное скачивание не требуется.");
        }
        else
        {
            var archive = await GetArchive(release, searchFolders, token);
            var original = PackageFiles.Snapshot(target);
            // Stage and backups stay outside the mod root and on the same drive as the target.
            var stage = Path.Combine(Path.GetDirectoryName(modRoot)!, ".xivrus-stage-" + Guid.NewGuid().ToString("N"));
            try
            {
                log("Распаковываю перевод. Потребуется несколько гигабайт свободного места…");
                PackageFiles.Extract(archive, stage, 16L * 1024 * 1024 * 1024, token);
                var payload = File.Exists(Path.Combine(stage, "meta.json")) ? stage :
                    Directory.GetDirectories(stage).SingleOrDefault(p => File.Exists(Path.Combine(p, "meta.json")))
                    ?? throw new InvalidDataException("В архиве не найдено описание XIV Rus.");
                if (ValidateMod(payload, log) != release.Version) throw new InvalidDataException("Версия XIV Rus в архиве не совпала с выпуском.");
                WriteInventory(payload, release.Sha256!);
                token.ThrowIfCancellationRequested();
                SettingsTransaction.Apply(backupRoot, changes, ensureClosed, log, payload, target, original);
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        log("Включено ожидание загрузки плагинов перед запуском игры. Это необходимо для перевода меню и настроек XIV Rus.");
        return "Русификация завершена: перевод игры XIV Rus включён в Penumbra, " +
            "игра будет ждать загрузки перевода. Запустите игру заново через XIVLauncher с английским языком клиента " +
            "и проверьте очередь входа, настройки игры и речь.";
    }

    public static Version ValidateMod(string folder, Action<string>? log = null)
    {
        PackageFiles.RejectLinks(folder);
        var meta = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(folder, "meta.json")));
        if ((string?)meta["Name"] != "XIV Rus" || (int?)meta["FileVersion"] is not (3 or 4))
            throw new InvalidDataException("Это не поддерживаемый пакет XIV Rus.");
        var required = new List<JObject>();
        var optional = new List<JObject>();
        if ((int?)meta["FileVersion"] == 3)
        {
            required.Add(PackageFiles.ReadJson(File.ReadAllText(Path.Combine(folder, "default_mod.json"))));
            optional.AddRange(Directory.GetFiles(folder, "group_*.json").Select(f => PackageFiles.ReadJson(File.ReadAllText(f))));
        }
        else
        {
            required.Add(meta["DefaultData"] as JObject ?? throw new InvalidDataException("В переводе нет основных данных."));
            if (meta["Groups"] is JArray groups) optional.AddRange(groups.OfType<JObject>());
        }
        static string?[] Files(IEnumerable<JObject> data) => data.SelectMany(d => d.Descendants().OfType<JProperty>()).Where(p => p.Name == "Files")
            .SelectMany(p => p.Value is JObject o ? o.Properties().Select(v => (string?)v.Value)
                : throw new InvalidDataException("Неизвестный список файлов XIV Rus.")).Distinct().ToArray();
        var files = Files(required);
        if (files.Length == 0) throw new InvalidDataException("В переводе нет файлов игры.");
        var prefix = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        foreach (var file in files) CheckFile(file, true);
        // Official XIV Rus 1.19.1 has a dangling optional VVDNotebookContents reference.
        // Keep the checksum-verified package unchanged; only mandatory resources must exist here.
        // For installations made by us, the inventory below also checks every actual optional file.
        foreach (var file in Files(optional)) CheckFile(file, false);
        var receipt = Path.Combine(folder, ".xivrus-installer-inventory.json");
        if (File.Exists(receipt))
        {
            var inventory = PackageFiles.ReadJson(File.ReadAllText(receipt));
            if (inventory["Files"] is not JObject entries) throw new InvalidDataException("Повреждён список установленных файлов перевода.");
            foreach (var entry in entries.Properties())
            {
                var path = CheckFile(entry.Name, true);
                if (new FileInfo(path).Length != (long)entry.Value) throw new InvalidDataException("Файл перевода повреждён: " + entry.Name);
            }
        }
        return PackageFiles.VersionOf((string?)meta["Version"] ?? "");

        string CheckFile(string? file, bool mustExist)
        {
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.Contains(':'))
                throw new InvalidDataException("Недопустимая ссылка на файл в переводе.");
            var path = Path.GetFullPath(Path.Combine(folder, file));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ссылка выходит за папку перевода: " + file);
            if (mustExist && (!File.Exists(path) || new FileInfo(path).Length == 0))
                throw new InvalidDataException("В переводе отсутствует файл: " + file);
            if (!mustExist && !File.Exists(path)) log?.Invoke("В официальном пакете нет файла необязательного компонента: " + file + ". Остальные компоненты будут установлены.");
            return path;
        }
    }

    private static void WriteInventory(string folder, string archiveHash)
    {
        const string name = ".xivrus-installer-inventory.json";
        var files = new JObject();
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file);
            // Penumbra migrates root JSON metadata from format 3 to 4 after its first discovery.
            // Only content files are stable across that migration.
            if (relative == name || Path.GetDirectoryName(relative) == "" && Path.GetExtension(relative) == ".json") continue;
            files[relative] = new FileInfo(file).Length;
        }
        File.WriteAllText(Path.Combine(folder, name), new JObject { ["ArchiveSHA256"] = archiveHash, ["Files"] = files }.ToString());
    }

    private async Task<string> GetArchive(PluginRelease release, IEnumerable<string>? folders, CancellationToken token)
    {
        var cache = Path.Combine(root, "installer-cache");
        var destination = Path.Combine(cache, "XIVRus-" + release.DisplayVersion + "-" + release.Sha256![..12] + ".pmp");
        var candidates = new List<string> { destination };
        foreach (var folder in (folders ?? SearchFolders()).Distinct(StringComparer.OrdinalIgnoreCase))
            if (Directory.Exists(folder)) candidates.AddRange(Directory.GetFiles(folder, "*.pmp"));
        foreach (var file in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(file)) continue;
            PackageFiles.RejectLinks(file);
            if (PackageFiles.Hash(file).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            { log("Найден актуальный архив перевода: " + file); return file; }
            log("Архив не совпал с текущим официальным выпуском и сохранён без изменений: " + file);
        }
        PackageFiles.RejectLinks(cache);
        Directory.CreateDirectory(cache);
        var partial = destination + ".part-" + Guid.NewGuid().ToString("N");
        try
        {
            log("Скачиваю XIV Rus " + release.DisplayVersion + " с официальной страницы проекта…");
            await releases.Download(release, partial, log, token, 2L * 1024 * 1024 * 1024);
            if (File.Exists(destination)) File.Move(destination, destination + ".old-" + Guid.NewGuid().ToString("N"));
            File.Move(partial, destination);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    private static IEnumerable<string> SearchFolders()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
        if (key?.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") is string downloads)
            yield return Environment.ExpandEnvironmentVariables(downloads);
    }
}

internal static class JsonExtensions
{
    public static void ReplaceContents(this JObject destination, JObject source)
    {
        destination.RemoveAll();
        foreach (var property in source.Properties()) destination.Add(property.Name, property.Value.DeepClone());
    }
}
