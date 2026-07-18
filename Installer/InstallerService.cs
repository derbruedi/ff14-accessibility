using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller;

/// <summary>
/// Barrier-free installer/updater for the FF14 Accessibility plugin (and the
/// vnavmesh pathfinding plugin it uses for auto-walk). Extracted from the
/// original console version, now driven from a WinForms GUI.
///
/// Why the DevPlugin route: Dalamud's own plugin installer is an ImGui overlay
/// that screen readers cannot read, so a blind user cannot use it. This tool
/// instead copies the plugin DLLs into Dalamud's devPlugins folder and enables
/// them directly in dalamudConfig.json, so Dalamud auto-loads them on the next
/// game start - no ImGui click required.
///
/// All status text goes through <see cref="LogMessage"/>, which the GUI writes
/// into a focusable, read-only, multi-line log textbox (screen-reader friendly).
/// Yes/No questions go through <see cref="AskYesNo"/>, which the GUI answers via
/// a standard MessageBox (also read aloud automatically by screen readers).
/// </summary>
public sealed class InstallerService
{
    private const string AccessibilityInternalName = "FF14Accessibility";
    private const string VnavmeshInternalName = "vnavmesh";

    private const string AccessibilityRepoOwner = "derbruedi";
    private const string AccessibilityRepoName = "ff14-accessibility";
    private const string XivLauncherRepoOwner = "goatcorp";
    private const string XivLauncherRepoName = "FFXIVQuickLauncher";
    private const string VnavmeshRepositoryJsonUrl = "https://puni.sh/api/repository/veyn";

    // Selbst-Update: kleines Manifest-Asset im Release, das die Installer-Version
    // trägt. Der EXE-Dateiname bleibt bewusst versionslos (stabiler Download-Link).
    private const string InstallerManifestAssetName = "installer.json";
    private const string InstallerExeAssetName = "FF14AccessibilityInstaller.exe";

    private static readonly string XivLauncherRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
    private static readonly string DevPluginsRoot = Path.Combine(XivLauncherRoot, "devPlugins");
    private static readonly string DalamudConfigPath = Path.Combine(XivLauncherRoot, "dalamudConfig.json");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>Raised for every status line. The GUI appends these to the log textbox.</summary>
    public event Action<string>? LogMessage;

    /// <summary>Ja/Nein-Rückfrage. Die GUI beantwortet das über eine MessageBox
    /// (synchron auf dem UI-Thread - RunAsync wird ohne ConfigureAwait(false)
    /// aufgerufen, daher laufen alle Fortsetzungen auf dem UI-Thread).</summary>
    public Func<string, bool>? AskYesNo { get; set; }

    /// <summary>Wird ausgelöst, wenn ein Selbst-Update läuft und sich dieser
    /// Prozess gleich beenden muss. Die GUI schließt daraufhin das Fenster.</summary>
    public event Action? RestartRequested;

    public InstallerService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14AccessibilityInstaller/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    private void Log(string message) => LogMessage?.Invoke(message);
    private void Info(string message) => Log(message);
    private void Warn(string message) => Log(Loc.Get("WarnPrefix") + message);
    private void Error(string message) => Log(Loc.Get("ErrorPrefix") + message);

    /// <summary>Führt den kompletten Installations-/Update-Ablauf aus. Ein einziger
    /// Codepfad für Erstinstallation und Update (siehe Architektur-Doc Abschnitt 4.1).
    /// Gibt true zurück, wenn ein Selbst-Update eingeleitet wurde und sich der
    /// Prozess gleich beendet - der Aufrufer zeigt dann keine Abschlussmeldung.</summary>
    public async Task<bool> RunAsync()
    {
        var ownVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? Loc.Get("UnknownVersion");
        Info(Loc.Get("InstallerHeader", ownVersion));
        Info(string.Empty);

        // Zuerst der Installer selbst: sonst würde erst installiert und danach
        // neu gestartet, und der Nutzer müsste den ganzen Ablauf zweimal hören.
        if (await TrySelfUpdateAsync(ownVersion))
            return true;
        Info(string.Empty);

        try
        {
            Info(Loc.Get("CheckingXivLauncher"));
            if (!Directory.Exists(XivLauncherRoot))
            {
                await HandleMissingXivLauncherAsync();
                return false; // Nutzer muss erst einloggen/Dalamud aktivieren/Spiel starten.
            }
            Info(Loc.Get("XivLauncherFound"));
            Info(string.Empty);

            var accResult = await UpdateAccessibilityPluginAsync(ownVersion);
            Info(string.Empty);
            var vnavResult = await UpdateVnavmeshAsync();
            Info(string.Empty);
            var patchResult = PatchDalamudConfig();

            Info(string.Empty);
            Info(Loc.Get("SummaryHeader"));
            Info(Loc.Get("SummaryAccessibility", accResult));
            Info(Loc.Get("SummaryVnavmesh", vnavResult));
            Info(patchResult);
        }
        catch (Exception ex)
        {
            Error(Loc.Get("UnexpectedError", ex.Message));
            Error(Loc.Get("NoPartialWrite"));
        }
        return false;
    }

    // ── XIVLauncher ────────────────────────────────────────────────────────

    private async Task HandleMissingXivLauncherAsync()
    {
        Warn(Loc.Get("XivLauncherNotInstalled1"));
        Warn(Loc.Get("XivLauncherNotInstalled2", XivLauncherRoot));
        Info(Loc.Get("XivLauncherNeeded"));
        Info(Loc.Get("DownloadingXivLauncherAuto"));

        (string tag, string url, string name)? asset;
        try
        {
            var release = await GetLatestReleaseAsync(XivLauncherRepoOwner, XivLauncherRepoName);
            asset = PickAsset(release, n => n.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpRequestException ex)
        {
            Error(Loc.Get("GitHubUnreachable", ex.Message));
            Error(Loc.Get("InstallXivLauncherManually1"));
            Error(Loc.Get("RunProgramAgain"));
            return;
        }
        catch (TaskCanceledException)
        {
            Error(Loc.Get("TimeoutFetchXivLauncher"));
            return;
        }

        if (asset == null)
        {
            Error(Loc.Get("NoXivLauncherSetupFound"));
            Error(Loc.Get("InstallXivLauncherManually2"));
            return;
        }

        var (version, url, assetName) = asset.Value;
        var setupPath = Path.Combine(Path.GetTempPath(), assetName);

        try
        {
            Info(Loc.Get("DownloadingXivLauncherVersion", version, assetName));
            await DownloadFileAsync(url, setupPath, "XIVLauncher-Setup");
        }
        catch (HttpRequestException ex)
        {
            Error(Loc.Get("DownloadFailedInstallManually", ex.Message));
            Error(Loc.Get("UrlAndRunAgain"));
            return;
        }
        catch (TaskCanceledException)
        {
            Error(Loc.Get("TimeoutDownloadRetry"));
            return;
        }
        catch (IOException ex)
        {
            Error(Loc.Get("XivLauncherSaveFailed", ex.Message));
            return;
        }

        Info(Loc.Get("InstallingXivLauncherSilent"));
        try
        {
            var psi = new ProcessStartInfo(setupPath)
            {
                Arguments = "--silent",
                UseShellExecute = true,
            };
            using var proc = Process.Start(psi);
            if (proc != null)
                await Task.Run(() => proc.WaitForExit(180_000)); // 3 Minuten Timeout, danach machen wir trotzdem weiter.
            Info(Loc.Get("XivLauncherInstallStarted"));
        }
        catch (Exception ex)
        {
            Warn(Loc.Get("AutoInstallNotConfirmed", ex.Message));
            Warn(Loc.Get("RunSetupManuallyHint"));
            Warn("  " + setupPath);
        }

        Info(string.Empty);
        Info(Loc.Get("LoginHint1"));
        Info(Loc.Get("LoginHint2"));
        Info(Loc.Get("LoginHint3"));
    }

    // ── Eigenes Plugin (FF14Accessibility) ────────────────────────────────

    private async Task<string> UpdateAccessibilityPluginAsync(string ownVersion)
    {
        Info(Loc.Get("CheckingAccessibilityVersion"));
        try
        {
            var release = await GetLatestReleaseAsync(AccessibilityRepoOwner, AccessibilityRepoName);

            var asset = PickAsset(release, n =>
                n.StartsWith("FF14Accessibility-v", StringComparison.OrdinalIgnoreCase) &&
                n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset == null)
            {
                Warn(Loc.Get("NoAccessibilityAssetFound"));
                return Loc.Get("ErrorNoReleaseAsset");
            }

            var (tag, url, _) = asset.Value;
            var remoteVersion = tag.TrimStart('v', 'V');

            var targetDir = Path.Combine(DevPluginsRoot, AccessibilityInternalName);
            var manifestPath = Path.Combine(targetDir, AccessibilityInternalName + ".json");
            var localVersion = ReadLocalManifestVersion(manifestPath);

            if (localVersion != null && !IsNewer(remoteVersion, localVersion))
            {
                Info(Loc.Get("AccessibilityUpToDate", localVersion));
                return Loc.Get("UpToDateShort", localVersion);
            }

            Info(Loc.Get("DownloadingAccessibility", remoteVersion));
            var zipPath = Path.Combine(Path.GetTempPath(), "FF14Accessibility_" + Guid.NewGuid() + ".zip");
            await DownloadFileAsync(url, zipPath, "FF14 Accessibility");

            var extractDir = Path.Combine(Path.GetTempPath(), "FF14AccExtract_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            DeployPluginFiles(extractDir, targetDir);

            var wasInstalled = localVersion != null;
            Info(wasInstalled
                ? Loc.Get("AccessibilityUpdated", remoteVersion)
                : Loc.Get("AccessibilityInstalled", remoteVersion));
            return wasInstalled
                ? Loc.Get("UpdatedToShort", remoteVersion)
                : Loc.Get("NewlyInstalledShort", remoteVersion);
        }
        catch (IOException ex)
        {
            Error(Loc.Get("CouldNotWritePluginFiles", ex.Message));
            Error(Loc.Get("CloseGameAndLauncher"));
            return Loc.Get("ErrorFilesLocked");
        }
        catch (HttpRequestException ex)
        {
            Error(Loc.Get("AccessibilityGitHubUnreachable", ex.Message));
            return Loc.Get("ErrorNoNetworkGitHub");
        }
        catch (TaskCanceledException)
        {
            Error(Loc.Get("AccessibilityDownloadTimeout"));
            return Loc.Get("ErrorTimeout");
        }
        catch (Exception ex)
        {
            Error(Loc.Get("AccessibilityUnexpectedError", ex.Message));
            return Loc.Get("ErrorGeneric");
        }
    }

    // ── vnavmesh (Auto-Lauf) ───────────────────────────────────────────────

    private async Task<string> UpdateVnavmeshAsync()
    {
        Info(Loc.Get("CheckingVnavmeshVersion"));

        JsonNode? entry;
        try
        {
            var json = await _http.GetStringAsync(VnavmeshRepositoryJsonUrl);
            var arr = JsonNode.Parse(json)?.AsArray();
            entry = null;
            if (arr != null)
            {
                foreach (var e in arr)
                {
                    if (string.Equals(e?["InternalName"]?.GetValue<string>(), VnavmeshInternalName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        entry = e;
                        break;
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Warn(Loc.Get("VnavmeshPunishUnreachable", ex.Message));
            return Loc.Get("ErrorNoNetworkPunish");
        }
        catch (TaskCanceledException)
        {
            Warn(Loc.Get("VnavmeshPunishTimeout"));
            return Loc.Get("ErrorTimeout");
        }

        if (entry == null)
        {
            Warn(Loc.Get("VnavmeshNotFound"));
            return Loc.Get("ErrorNotFound");
        }

        var remoteVersion = entry["AssemblyVersion"]?.GetValue<string>() ?? Loc.Get("UnknownVersion");
        var downloadUrl = entry["DownloadLinkInstall"]?.GetValue<string>();
        if (string.IsNullOrEmpty(downloadUrl))
        {
            Warn(Loc.Get("VnavmeshNoDownloadLink"));
            return Loc.Get("ErrorNoDownloadLink");
        }

        var targetDir = Path.Combine(DevPluginsRoot, VnavmeshInternalName);
        var manifestPath = Path.Combine(targetDir, VnavmeshInternalName + ".json");
        var localVersion = ReadLocalManifestVersion(manifestPath);

        if (localVersion != null && !IsNewer(remoteVersion, localVersion))
        {
            Info(Loc.Get("VnavmeshUpToDate", localVersion));
            return Loc.Get("UpToDateShort", localVersion);
        }

        if (localVersion == null)
        {
            Info(string.Empty);
            Info(Loc.Get("AutoWalkNeedsVnav1"));
            Info(Loc.Get("AutoWalkNeedsVnav2"));
            Info(Loc.Get("AutoWalkNeedsVnav3"));
            var yes = AskYesNo?.Invoke(Loc.Get("AskSetupVnavmesh")) ?? false;
            if (!yes)
            {
                Info(Loc.Get("VnavmeshSkipped"));
                return Loc.Get("SkippedShort");
            }
        }

        try
        {
            Info(Loc.Get("DownloadingVnavmesh", remoteVersion));
            var zipPath = Path.Combine(Path.GetTempPath(), "vnavmesh_" + Guid.NewGuid() + ".zip");
            await DownloadFileAsync(downloadUrl, zipPath, "vnavmesh");

            var extractDir = Path.Combine(Path.GetTempPath(), "vnavmeshExtract_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            DeployPluginFiles(extractDir, targetDir);

            var wasInstalled = localVersion != null;
            Info(wasInstalled
                ? Loc.Get("VnavmeshUpdated", remoteVersion)
                : Loc.Get("VnavmeshSetup", remoteVersion));
            return wasInstalled
                ? Loc.Get("UpdatedToShort", remoteVersion)
                : Loc.Get("NewlySetupShort", remoteVersion);
        }
        catch (IOException ex)
        {
            Error(Loc.Get("VnavmeshCouldNotWriteFiles", ex.Message));
            Error(Loc.Get("CloseGameAndLauncher"));
            return Loc.Get("ErrorFilesLocked");
        }
        catch (HttpRequestException ex)
        {
            Error(Loc.Get("VnavmeshDownloadFailed", ex.Message));
            return Loc.Get("ErrorNoNetwork");
        }
        catch (TaskCanceledException)
        {
            Error(Loc.Get("VnavmeshDownloadTimeout"));
            return Loc.Get("ErrorTimeout");
        }
        catch (Exception ex)
        {
            Error(Loc.Get("VnavmeshUnexpectedError", ex.Message));
            return Loc.Get("ErrorGeneric");
        }
    }

    // ── dalamudConfig.json ─────────────────────────────────────────────────

    /// <summary>
    /// Registers the plugin DLLs as DevPlugin load locations and seeds everything
    /// Dalamud needs to load them on the next boot without any UI interaction:
    /// DevMode=true, a DevPluginSettings entry per DLL (StartOnBoot + WorkingPluginId)
    /// and a matching enabled DefaultProfile entry (see <see cref="EnableDevPlugin"/>).
    /// Conservative on purpose: makes a backup and writes BOM-free (Dalamud's
    /// ReliableFileStorage reads raw bytes; a UTF-8 BOM makes it silently fall
    /// back to an old SQLite copy - documented project trap).
    /// </summary>
    private string PatchDalamudConfig()
    {
        if (!File.Exists(DalamudConfigPath))
        {
            Warn(Loc.Get("ConfigNotExist1"));
            Warn(Loc.Get("ConfigNotExist2"));
            Warn(Loc.Get("ConfigNotExist3"));
            Warn(Loc.Get("ConfigNotExist4"));
            return Loc.Get("ConfigMissingReturn");
        }

        byte[] bytes;
        string text;
        try
        {
            bytes = File.ReadAllBytes(DalamudConfigPath);
            text = StripBom(bytes);
        }
        catch (IOException ex)
        {
            Error(Loc.Get("ConfigReadFailed", ex.Message));
            Error(Loc.Get("CloseGameAndLauncher"));
            return Loc.Get("ConfigReadFailedReturn");
        }

        JObject config;
        try
        {
            config = JObject.Parse(text);
        }
        catch (Exception ex)
        {
            Error(Loc.Get("ConfigParseFailed", ex.Message));
            Error(Loc.Get("ConfigNotTouching"));
            return Loc.Get("ConfigInvalidReturn");
        }

        var loadLocations = config["DevPluginLoadLocations"]?["$values"] as JArray;
        if (loadLocations == null)
        {
            Warn(Loc.Get("ConfigUnexpectedStructure"));
            Warn(Loc.Get("ConfigSafetyNoChange1"));
            Warn(Loc.Get("ConfigSafetyNoChange2"));
            return Loc.Get("ConfigUnexpectedStructureReturn");
        }

        var accDll = Path.Combine(DevPluginsRoot, AccessibilityInternalName, AccessibilityInternalName + ".dll");
        var vnavDll = Path.Combine(DevPluginsRoot, VnavmeshInternalName, VnavmeshInternalName + ".dll");
        var hasVnav = File.Exists(vnavDll);

        try
        {
            var backup = DalamudConfigPath + ".bak-installer";
            File.Copy(DalamudConfigPath, backup, overwrite: true);

            // Without DevMode Dalamud never scans DevPluginLoadLocations at all
            // (PluginManager boot load is gated on configuration.DevMode).
            config["DevMode"] = true;

            var hasAcc = File.Exists(accDll);
            if (hasAcc) AddDevPluginLocation(loadLocations, accDll);
            if (hasVnav) AddDevPluginLocation(loadLocations, vnavDll);

            var enabled = true;
            if (hasAcc) enabled &= EnableDevPlugin(config, AccessibilityInternalName, accDll);
            if (hasVnav) enabled &= EnableDevPlugin(config, VnavmeshInternalName, vnavDll);

            WriteAllTextNoBom(DalamudConfigPath, config.ToString());
            Info(Loc.Get("ConfigUpdated", Path.GetFileName(backup)));

            if (!enabled)
            {
                Warn(Loc.Get("ProfileStructureUnexpected"));
                return Loc.Get("ProfileStructureUnexpectedReturn");
            }

            return Loc.Get("PluginsRegisteredEnabledReturn");
        }
        catch (IOException ex)
        {
            Error(Loc.Get("ConfigWriteFailed", ex.Message));
            Error(Loc.Get("CloseGameAndLauncher"));
            return Loc.Get("ConfigWriteFailedReturn");
        }
    }

    private static void AddDevPluginLocation(JArray loadLocations, string dllPath)
    {
        var exists = loadLocations.Any(e =>
            string.Equals((string?)e["Path"], dllPath, StringComparison.OrdinalIgnoreCase));
        if (exists) return;

        loadLocations.Add(new JObject
        {
            ["$type"] = "Dalamud.Configuration.DevPluginLocationSettings, Dalamud",
            ["Path"] = dllPath,
            ["IsEnabled"] = true,
            ["Nickname"] = null,
        });
    }

    /// <summary>
    /// Seeds everything a dev plugin needs to load on boot. Verified against
    /// decompiled Dalamud 15.0.2.2 (PluginManager.LoadPluginAsync, LocalDevPlugin
    /// ctor, Profile.WantsPlugin): a dev plugin loads at boot only when its
    /// DevPluginSettings entry (dictionary keyed by full DLL path) has
    /// StartOnBoot=true AND the DefaultProfile contains an entry with the SAME
    /// WorkingPluginId and IsEnabled=true. Dalamud only generates a new GUID when
    /// the DevPluginSettings entry is missing, so pre-seeding both sides with one
    /// GUID is stable. Returns false if the profile structure is missing.
    /// </summary>
    private static bool EnableDevPlugin(JObject config, string internalName, string dllPath)
    {
        if (config["DevPluginSettings"] is not JObject devSettings)
        {
            devSettings = new JObject
            {
                ["$type"] = "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[Dalamud.Configuration.Internal.DevPluginSettings, Dalamud]], System.Private.CoreLib",
            };
            config["DevPluginSettings"] = devSettings;
        }

        Guid workingId;
        if (devSettings[dllPath] is JObject entry)
        {
            entry["StartOnBoot"] = true;
            if (!Guid.TryParse((string?)entry["WorkingPluginId"], out workingId) || workingId == Guid.Empty)
            {
                workingId = Guid.NewGuid();
                entry["WorkingPluginId"] = workingId.ToString();
            }
        }
        else
        {
            workingId = Guid.NewGuid();
            devSettings[dllPath] = new JObject
            {
                ["$type"] = "Dalamud.Configuration.Internal.DevPluginSettings, Dalamud",
                ["StartOnBoot"] = true,
                ["NotifyForErrors"] = true,
                // Auto-reload on file change (user request 2026-07-16): a new
                // deploy is picked up without restarting the game.
                ["AutomaticReloading"] = true,
                ["WorkingPluginId"] = workingId.ToString(),
                ["DismissedValidationProblems"] = new JObject
                {
                    ["$type"] = "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib",
                    ["$values"] = new JArray(),
                },
            };
        }

        if (config["DefaultProfile"]?["Plugins"]?["$values"] is not JArray profilePlugins)
            return false;

        var existing = profilePlugins.FirstOrDefault(p =>
            string.Equals((string?)p["InternalName"], internalName, StringComparison.Ordinal));
        if (existing != null)
        {
            existing["IsEnabled"] = true;
            // DevPluginSettings is the authority for the GUID (EffectiveWorkingPluginId).
            existing["WorkingPluginId"] = workingId.ToString();
        }
        else
        {
            profilePlugins.Add(new JObject
            {
                ["$type"] = "Dalamud.Plugin.Internal.Profiles.ProfileModelV1+ProfileModelV1Plugin, Dalamud",
                ["InternalName"] = internalName,
                ["WorkingPluginId"] = workingId.ToString(),
                ["IsEnabled"] = true,
            });
        }
        return true;
    }

    // ── GitHub-API-Helfer ──────────────────────────────────────────────────

    private async Task<JsonNode?> GetLatestReleaseAsync(string owner, string repo)
    {
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = await _http.GetStringAsync(apiUrl);
        return JsonNode.Parse(json);
    }

    /// <summary>Wie <see cref="PickAsset"/>, liefert aber den ganzen Asset-Knoten
    /// (für Felder wie "size", die das Tupel nicht trägt).</summary>
    private static JsonNode? PickAssetNode(JsonNode? release, Func<string, bool> pick)
    {
        var assets = release?["assets"]?.AsArray();
        if (assets == null) return null;

        foreach (var a in assets)
        {
            var name = a?["name"]?.GetValue<string>();
            if (name != null && pick(name)) return a;
        }
        return null;
    }

    private static (string tag, string url, string name)? PickAsset(JsonNode? release, Func<string, bool> pick)
    {
        var tag = release?["tag_name"]?.GetValue<string>() ?? "";
        var assets = release?["assets"]?.AsArray();
        if (assets != null)
        {
            foreach (var a in assets)
            {
                var name = a?["name"]?.GetValue<string>();
                if (name != null && pick(name))
                    return (tag, a!["browser_download_url"]!.GetValue<string>(), name);
            }
        }
        return null;
    }

    // ── Selbst-Update des Installers ───────────────────────────────────────

    /// <summary>
    /// Prüft, ob im neuesten Release eine neuere Installer-Version liegt, fragt
    /// den Nutzer, lädt sie und startet Phase 2 (siehe <see cref="SelfUpdate"/>).
    /// Gibt true zurück, wenn der Neustart eingeleitet wurde.
    ///
    /// Versionsquelle ist das Release-Asset "installer.json", NICHT der
    /// Dateiname der EXE: die heißt bewusst unveränderlich
    /// FF14AccessibilityInstaller.exe, damit der Download-Link und die
    /// Anleitung in der README stabil bleiben. (Der frühere Hinweis-Code las
    /// die Version per Regex aus dem Asset-Namen und konnte deshalb nie
    /// anschlagen.)
    ///
    /// Jeder Fehler ist hier unkritisch: das Selbst-Update entfällt dann still
    /// und die normale Plugin-Installation läuft weiter.
    /// </summary>
    private async Task<bool> TrySelfUpdateAsync(string ownVersion)
    {
        Info(Loc.Get("CheckingInstallerVersion"));

        JsonNode? manifest;
        JsonNode? release;
        try
        {
            release = await GetLatestReleaseAsync(AccessibilityRepoOwner, AccessibilityRepoName);
            var manifestAsset = PickAsset(release, n =>
                n.Equals(InstallerManifestAssetName, StringComparison.OrdinalIgnoreCase));
            if (manifestAsset == null)
            {
                // Releases vor Installer 1.1 haben kein installer.json.
                Info(Loc.Get("NoInstallerManifest"));
                return false;
            }

            var json = await _http.GetStringAsync(manifestAsset.Value.url);
            manifest = JsonNode.Parse(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            Warn(Loc.Get("InstallerCheckFailed", ex.Message));
            return false;
        }

        var remoteVersion = manifest?["InstallerVersion"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(remoteVersion))
        {
            Warn(Loc.Get("InstallerManifestUnreadable"));
            return false;
        }

        if (!IsNewer(remoteVersion, ownVersion))
        {
            Info(Loc.Get("InstallerUpToDate", ownVersion));
            return false;
        }

        var exeAssetName = manifest?["AssetName"]?.GetValue<string>() ?? InstallerExeAssetName;
        var exeAsset = PickAssetNode(release, n => n.Equals(exeAssetName, StringComparison.OrdinalIgnoreCase));
        if (exeAsset == null)
        {
            Warn(Loc.Get("InstallerAssetMissing", exeAssetName));
            return false;
        }

        var targetPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(targetPath))
        {
            Warn(Loc.Get("InstallerOwnPathUnknown"));
            return false;
        }

        var sizeMb = (exeAsset["size"]?.GetValue<long>() ?? 0L) / (1024 * 1024);
        Info(Loc.Get("InstallerUpdateAvailable", remoteVersion, ownVersion));

        if (AskYesNo == null || !AskYesNo(Loc.Get("InstallerUpdateQuestion", remoteVersion, sizeMb)))
        {
            Info(Loc.Get("InstallerUpdateDeclined"));
            return false;
        }

        var downloadUrl = exeAsset["browser_download_url"]?.GetValue<string>();
        if (string.IsNullOrEmpty(downloadUrl))
        {
            Warn(Loc.Get("InstallerAssetMissing", exeAssetName));
            return false;
        }

        var newExePath = Path.Combine(Path.GetTempPath(), SelfUpdate.DownloadFilePrefix + Guid.NewGuid() + ".exe");
        try
        {
            Info(Loc.Get("DownloadingInstaller", remoteVersion));
            await DownloadFileAsync(downloadUrl, newExePath, Loc.Get("InstallerDownloadLabel"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            Warn(Loc.Get("InstallerDownloadFailed", ex.Message));
            TryDelete(newExePath);
            return false;
        }

        // Integritätsprüfung, damit niemals eine abgebrochene oder veränderte
        // Datei gestartet wird. Fehlt der Hash im Manifest, wird nur geloggt -
        // dann gilt dieselbe Vertrauensbasis wie beim manuellen Download.
        var expectedHash = manifest?["Sha256"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(expectedHash))
        {
            var actual = ComputeSha256(newExePath);
            if (!string.Equals(actual, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Error(Loc.Get("InstallerHashMismatch"));
                TryDelete(newExePath);
                return false;
            }
            Info(Loc.Get("InstallerHashOk"));
        }
        else
        {
            Info(Loc.Get("InstallerNoHash"));
        }

        try
        {
            Process.Start(new ProcessStartInfo(newExePath)
            {
                Arguments = $"{SelfUpdate.ApplyUpdateArg} \"{targetPath}\" {Environment.ProcessId}",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Warn(Loc.Get("InstallerStartFailed", ex.Message));
            TryDelete(newExePath);
            return false;
        }

        Info(Loc.Get("InstallerRestarting", remoteVersion));
        RestartRequested?.Invoke();
        return true;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* Temp-Datei bleibt liegen - unkritisch. */ }
        catch (UnauthorizedAccessException) { /* dito */ }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    // ── Download / Entpacken ───────────────────────────────────────────────

    private async Task DownloadFileAsync(string url, string destinationPath, string label)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var httpStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        var lastDecile = -1;
        int read;
        while ((read = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            readTotal += read;
            if (totalBytes > 0)
            {
                var percent = (int)(readTotal * 100 / totalBytes);
                var decile = percent / 10;
                if (decile != lastDecile || percent == 100)
                {
                    Info(Loc.Get("DownloadProgress", label, percent));
                    lastDecile = decile;
                }
            }
        }
    }

    /// <summary>Kopiert die entpackten Plugin-Dateien (DLL, JSON-Manifest, PDB,
    /// Tolk.dll, nvdaControllerClient64.dll, NAudio*.dll) in den devPlugins-Zielordner.
    /// Kopiert alle Dateien im ZIP-Root (Struktur live verifiziert), notfalls
    /// rekursiv, falls die ZIP einen Unterordner enthält.</summary>
    private static void DeployPluginFiles(string extractDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        var files = Directory.GetFiles(extractDir);
        if (files.Length == 0)
            files = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }
    }

    // ── Versions-/Sonstige Helfer ──────────────────────────────────────────

    private static string? ReadLocalManifestVersion(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            var node = JsonNode.Parse(json);
            return node?["AssemblyVersion"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewer(string remote, string local)
    {
        var r = ParseVersionLoose(remote);
        var l = ParseVersionLoose(local);
        if (r != null && l != null) return r > l;
        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parst eine Versionsangabe und füllt sie IMMER auf vier Stellen auf.
    /// Ohne das Auffüllen gilt "1.1.0" als KLEINER als "1.1.0.0" (nicht gesetzte
    /// Stellen zählen bei <see cref="Version"/> als -1) - eine dreistellige
    /// Angabe in installer.json würde das Selbst-Update also still nie auslösen.
    /// </summary>
    private static Version? ParseVersionLoose(string s)
    {
        s = s.TrimStart('v', 'V').Trim();
        var parts = s.Split('.');
        if (parts.Length > 4) return null;
        while (parts.Length < 4)
        {
            s += ".0";
            parts = s.Split('.');
        }
        return Version.TryParse(s, out var v) ? v : null;
    }

    private static string StripBom(byte[] bytes)
    {
        // UTF-8 BOM = EF BB BF.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        return new UTF8Encoding(false).GetString(bytes);
    }

    private static void WriteAllTextNoBom(string path, string content) =>
        File.WriteAllText(path, content, new UTF8Encoding(false));
}
