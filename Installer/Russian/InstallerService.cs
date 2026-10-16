using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public sealed class InstallerService : IDisposable
{
    private readonly string root;
    private readonly ReleaseClient releases;
    private readonly Action ensureClosed;
    public Action<string> Log { get; set; } = _ => { };
    public Func<string, Task<bool>> Ask { get; set; } = _ => Task.FromResult(false);

    public InstallerService(string? launcherRoot = null, ReleaseClient? client = null, Action? checkClosed = null)
    {
        root = Path.GetFullPath(launcherRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher"));
        releases = client ?? new ReleaseClient();
        ensureClosed = checkClosed ?? InstallTransaction.EnsureGameClosed;
    }

    private (byte[] Bytes, JObject Config) ReadSettings()
    {
        var path = Path.Combine(root, "dalamudConfig.json");
        Log("Папка настроек XIVLauncher: " + root);
        if (!File.Exists(path))
            throw new InvalidOperationException("Не найдены настройки XIVLauncher. Установите XIVLauncher, включите Dalamud, " +
                "один раз войдите в игру и закройте игру и лаунчер. Затем повторите установку. Папка: " + root);
        var bytes = File.ReadAllBytes(path);
        var json = PackageFiles.ReadJson(System.Text.Encoding.UTF8.GetString(bytes));
        return (bytes, json);
    }

    public async Task Check(CancellationToken token)
    {
        Log("Проверяю последний стабильный выпуск на GitHub…");
        var release = await releases.Latest(token);
        Log("Последняя русская версия: " + release.DisplayVersion + ".");
        var (_, config) = ReadSettings();
        var installed = DalamudSettings.Find(root, config, "FF14Accessibility");
        Describe(installed, release);
        ReportVnav(config);
        var penumbra = DalamudSettings.Find(root, config, "Penumbra");
        if (penumbra.Count == 1)
        {
            Log(TranslationInstaller.WaitsForPlugins(config)
                ? "Ожидание загрузки плагинов перед игрой включено."
                : "Ожидание загрузки плагинов перед игрой выключено. Из-за этого часть интерфейса XIV Rus может остаться английской. " +
                  "Для исправления нажмите «Установить русификацию игры», затем запустите игру заново.");
            try
            {
                DalamudSettings.VerifyEnabled(root, "Penumbra", penumbra[0]);
                Log("Penumbra: автозагрузка и профили включены. Фактическую загрузку проверяют после запуска игры.");
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
            { Log("Penumbra требует исправления: " + ex.Message + " Нажмите «Установить русификацию игры»."); }
        }
        Log("Проверка завершена. Для установки или обновления нажмите «Установить / обновить мод».");
    }

    private void Describe(IReadOnlyList<InstalledPlugin> installed, PluginRelease release)
    {
        if (installed.Count == 0) { Log("Мод пока не установлен."); return; }
        foreach (var p in installed)
            Log($"Установлено: {p.Version?.ToString() ?? "версия не читается"}. Папка: {p.Folder}");
        if (installed.Count > 1) Log("Найдено несколько копий мода. Перед обновлением нужно устранить дублирование.");
        else if (installed[0].Version is not { } current) Log("Установка повреждена; её можно восстановить.");
        else if (current < release.Version) Log("На GitHub есть более новая версия.");
        else if (current == release.Version) Log("Установлена последняя версия.");
        else Log("Установленная версия новее опубликованной. Более старая версия установлена не будет.");
    }

    private void ReportVnav(JObject config)
    {
        var found = DalamudSettings.Find(root, config, "vnavmesh");
        if (found.Count == 0) Log("vnavmesh не установлена. После установки мода будет предложено добавить её для автоматического движения.");
        else foreach (var p in found) Log($"vnavmesh найдена: {p.Version?.ToString() ?? "версия не читается"}. Папка: {p.Folder}");
    }

    // Called on a worker thread: copying, hashing and extraction never freeze the accessible UI.
    public async Task<string> Install(CancellationToken token, bool offerVnav = true)
    {
        ensureClosed();
        var (bytes, config) = ReadSettings();
        var release = await releases.Latest(token);
        var installed = DalamudSettings.Find(root, config, "FF14Accessibility");
        Describe(installed, release);
        if (installed.Count > 1)
            throw new InvalidOperationException("Найдено несколько копий FF14Accessibility. Отключите лишнюю копию в настройках Dalamud и повторите установку. Пути приведены в журнале.");
        if (installed.FirstOrDefault()?.Catalog == true)
            throw new InvalidOperationException("Мод установлен через каталог Dalamud. Чтобы не смешать две системы обновления, " +
                "сначала удалите его через каталог Dalamud, сохранив настройки, затем повторите установку русской версии.");
        var target = installed.FirstOrDefault()?.Folder ?? Path.Combine(root, "devPlugins", "FF14Accessibility");
        var snapshot = PackageFiles.Snapshot(target);
        // Validate configuration before downloading or changing any plugin files.
        DalamudSettings.Enable(config, "FF14Accessibility", Path.Combine(target, "FF14Accessibility.dll"));
        var current = installed.FirstOrDefault()?.Version;
        var needsFiles = current == null || current < release.Version;
        if (current == release.Version)
        {
            try { PackageFiles.ValidatePlugin(target, "FF14Accessibility", release.Version); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or Newtonsoft.Json.JsonException or FormatException)
            { Log("Некоторые файлы мода отсутствуют или повреждены; восстанавливаю эту же версию."); needsFiles = true; }
        }
        using var temporary = new TemporaryFolder();
        string? payload = null;
        if (needsFiles)
        {
            Log("Скачиваю русскую версию " + release.DisplayVersion + "…");
            var zip = Path.Combine(temporary.Path, "accessibility.zip");
            await releases.Download(release, zip, Log, token);
            var extract = Path.Combine(temporary.Path, "accessibility");
            PackageFiles.Extract(zip, extract);
            payload = Path.Combine(extract, "plugin");
            PackageFiles.ValidatePlugin(payload, "FF14Accessibility", release.Version);
        }
        token.ThrowIfCancellationRequested();
        InstallTransaction.Apply(root, "FF14Accessibility", target, payload, bytes, snapshot, Log, ensureClosed);
        DalamudSettings.VerifyEnabled(root, "FF14Accessibility", new InstalledPlugin(target, release.Version, false));
        Log(needsFiles ? "Русская версия " + release.DisplayVersion + " установлена и включена в Dalamud." : "Файлы мода актуальны; включение в Dalamud проверено.");
        if (!offerVnav) return "Мод доступности установлен и включён.";

        // This is a separate transaction: declining/failing the optional dependency must not undo the main plugin.
        try
        {
            token.ThrowIfCancellationRequested();
            var vnavResult = await InstallVnav(target, temporary.Path, token);
            Log(vnavResult);
            return "Мод готов. " + vnavResult + " Запустите игру через XIVLauncher.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { return "Мод установлен. Установка vnavmesh отменена."; }
        catch (Exception ex)
        {
            Log("Мод установлен, но проверка или установка vnavmesh не завершена: " + ex.Message);
            return "Мод установлен. vnavmesh не удалось проверить или установить. Подробности в журнале; повторите установку, когда причина устранена.";
        }
    }

    private async Task<string> InstallVnav(string accessibilityFolder, string temp, CancellationToken token)
    {
        var (bytes, config) = ReadSettings();
        var found = DalamudSettings.Find(root, config, "vnavmesh");
        if (found.Count > 0 && (found.Count > 1 || found[0].Version != null || found[0].Catalog))
        {
            ReportVnav(config);
            return found.Any(p => p.Version == null)
                ? "Обнаружена неполная установка vnavmesh. Исправьте её в прежнем месте; новая копия не создавалась."
                : "vnavmesh уже установлена; её файлы и настройки сохранены.";
        }
        Log("vnavmesh не установлена или её DLL повреждена. Она нужна для автоматического движения по маршруту.");
        if (!await Ask("Установить vnavmesh для автоматического движения? Установщик скачает её из каталога разработчика и включит в Dalamud."))
            return "Установка vnavmesh пропущена по вашему выбору.";
        token.ThrowIfCancellationRequested();
        var release = await releases.Vnav(token);
        var accManifest = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(accessibilityFolder, "FF14Accessibility.json")));
        if (release.ApiLevel != (int?)accManifest["DalamudApiLevel"])
            throw new InvalidOperationException("Доступная vnavmesh рассчитана на другой выпуск Dalamud. Дождитесь совместимого обновления.");
        var target = found.FirstOrDefault()?.Folder ?? Path.Combine(root, "devPlugins", "vnavmesh");
        var snapshot = PackageFiles.Snapshot(target);
        DalamudSettings.Enable(config, "vnavmesh", Path.Combine(target, "vnavmesh.dll"));
        Log("Скачиваю vnavmesh " + release.DisplayVersion + " из каталога разработчика…");
        var zip = Path.Combine(temp, "vnavmesh.zip");
        await releases.Download(release, zip, Log, token);
        var extract = Path.Combine(temp, "vnavmesh");
        PackageFiles.Extract(zip, extract);
        var api = PackageFiles.ValidatePlugin(extract, "vnavmesh", release.Version);
        if (api != release.ApiLevel) throw new InvalidDataException("Совместимость в архиве vnavmesh не совпала с каталогом.");
        token.ThrowIfCancellationRequested();
        InstallTransaction.Apply(root, "vnavmesh", target, extract, bytes, snapshot, Log, ensureClosed);
        return "vnavmesh " + release.DisplayVersion + " установлена и включена.";
    }

    public void Dispose() => releases.Dispose();

    public string Uninstall(bool mod, bool translation, CancellationToken token) =>
        new UninstallService(root, ensureClosed, Log).Remove(mod, translation, token);

    public string RemoveModTranslation(CancellationToken token) =>
        new UninstallService(root, ensureClosed, Log).Remove(false, false, token, modTranslation: true);

    public async Task<string> RussifyMod(CancellationToken token)
    {
        await Install(token, offerVnav: false);
        token.ThrowIfCancellationRequested();
        new TranslationInstaller(root, releases, ensureClosed, Log).SetRussianLanguage(token);
        return "Русификация мода установлена. Русский язык сообщений и речи включён. Запустите игру заново через XIVLauncher.";
    }

    public async Task<string> RussifyGame(CancellationToken token, IEnumerable<string>? searchFolders = null)
    {
        ensureClosed();
        ReadSettings();
        var translation = new TranslationInstaller(root, releases, ensureClosed, Log);
        await translation.EnsurePenumbra(token);
        token.ThrowIfCancellationRequested();
        var result = await translation.Install(token, searchFolders);
        var (_, config) = ReadSettings();
        DalamudSettings.VerifyEnabled(root, "Penumbra", DalamudSettings.Find(root, config, "Penumbra").Single());
        return result;
    }

    public async Task<string> Russify(CancellationToken token, IEnumerable<string>? searchFolders = null)
    {
        ensureClosed();
        ReadSettings();
        var translation = new TranslationInstaller(root, releases, ensureClosed, Log);
        await translation.EnsurePenumbra(token);
        await Install(token, offerVnav: false);
        var (_, config) = ReadSettings();
        var penumbra = DalamudSettings.Find(root, config, "Penumbra").Single();
        var accessibility = DalamudSettings.Find(root, config, "FF14Accessibility").Single();
        var penApi = PackageFiles.ValidatePlugin(penumbra.Folder, "Penumbra", penumbra.Version!);
        var accApi = (int?)PackageFiles.ReadJson(File.ReadAllText(Path.Combine(accessibility.Folder, "FF14Accessibility.json")))["DalamudApiLevel"];
        if (penApi != accApi)
            throw new InvalidOperationException("Penumbra и мод доступности рассчитаны на разные версии Dalamud. Обновите Penumbra и повторите русификацию.");
        token.ThrowIfCancellationRequested();
        translation.SetRussianLanguage();
        Log("Мод доступности установлен, русский язык включён. Проверяю русский текст игры…");
        var result = await translation.Install(token, searchFolders);
        DalamudSettings.VerifyEnabled(root, "Penumbra", penumbra);
        DalamudSettings.VerifyEnabled(root, "FF14Accessibility", accessibility);
        return result;
    }
}

internal sealed class TemporaryFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FF14Accessibility-RU-" + Guid.NewGuid().ToString("N"));
    public TemporaryFolder() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        // Only our unique temporary directory, never a user-selected folder.
        try { Directory.Delete(Path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
