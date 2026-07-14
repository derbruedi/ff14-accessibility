using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

    public InstallerService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14AccessibilityInstaller/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    private void Log(string message) => LogMessage?.Invoke(message);
    private void Info(string message) => Log(message);
    private void Warn(string message) => Log("Achtung: " + message);
    private void Error(string message) => Log("Fehler: " + message);

    /// <summary>Führt den kompletten Installations-/Update-Ablauf aus. Ein einziger
    /// Codepfad für Erstinstallation und Update (siehe Architektur-Doc Abschnitt 4.1).</summary>
    public async Task RunAsync()
    {
        var ownVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unbekannt";
        Info("FF14 Accessibility – Installer und Updater (Version " + ownVersion + ").");
        Info(string.Empty);

        try
        {
            Info("Prüfe XIVLauncher ...");
            if (!Directory.Exists(XivLauncherRoot))
            {
                await HandleMissingXivLauncherAsync();
                return; // Nutzer muss erst einloggen/Dalamud aktivieren/Spiel starten.
            }
            Info("XIVLauncher gefunden.");
            Info(string.Empty);

            var accResult = await UpdateAccessibilityPluginAsync(ownVersion);
            Info(string.Empty);
            var vnavResult = await UpdateVnavmeshAsync();
            Info(string.Empty);
            var patchResult = PatchDalamudConfig();

            Info(string.Empty);
            Info("=== Zusammenfassung ===");
            Info("FF14 Accessibility: " + accResult);
            Info("vnavmesh (Auto-Lauf): " + vnavResult);
            Info(patchResult);
        }
        catch (Exception ex)
        {
            Error("Unerwarteter Fehler: " + ex.Message);
            Error("Es wurde nichts Unvollständiges geschrieben, das dein System beschädigt.");
        }
    }

    // ── XIVLauncher ────────────────────────────────────────────────────────

    private async Task HandleMissingXivLauncherAsync()
    {
        Warn("XIVLauncher ist nicht installiert (Ordner nicht gefunden:");
        Warn("  " + XivLauncherRoot + ").");
        Info("XIVLauncher wird gebraucht, weil es Dalamud lädt – die Grundlage für das Plugin.");
        Info("Lade die neueste XIVLauncher-Version herunter und installiere sie automatisch ...");

        (string tag, string url, string name)? asset;
        try
        {
            var release = await GetLatestReleaseAsync(XivLauncherRepoOwner, XivLauncherRepoName);
            asset = PickAsset(release, n => n.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpRequestException ex)
        {
            Error("GitHub nicht erreichbar (" + ex.Message + "). Bitte Internetverbindung prüfen.");
            Error("Installiere XIVLauncher alternativ manuell von https://goatcorp.github.io/ und");
            Error("führe dieses Programm danach erneut aus.");
            return;
        }
        catch (TaskCanceledException)
        {
            Error("Zeitüberschreitung beim Abruf der XIVLauncher-Version. Bitte Internetverbindung prüfen.");
            return;
        }

        if (asset == null)
        {
            Error("Kein XIVLauncher-Setup im neuesten Release gefunden.");
            Error("Bitte installiere XIVLauncher manuell von https://goatcorp.github.io/ .");
            return;
        }

        var (version, url, assetName) = asset.Value;
        var setupPath = Path.Combine(Path.GetTempPath(), assetName);

        try
        {
            Info($"Lade XIVLauncher {version} herunter ({assetName}) ...");
            await DownloadFileAsync(url, setupPath, "XIVLauncher-Setup");
        }
        catch (HttpRequestException ex)
        {
            Error("Download fehlgeschlagen (" + ex.Message + "). Bitte installiere XIVLauncher manuell von");
            Error("https://goatcorp.github.io/ und führe dieses Programm danach erneut aus.");
            return;
        }
        catch (TaskCanceledException)
        {
            Error("Zeitüberschreitung beim Download. Bitte Internetverbindung prüfen und erneut versuchen.");
            return;
        }
        catch (IOException ex)
        {
            Error("XIVLauncher-Setup konnte nicht gespeichert werden: " + ex.Message);
            return;
        }

        Info("Installiere XIVLauncher automatisch im Hintergrund (--silent) ...");
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
            Info("XIVLauncher-Installation wurde gestartet und sollte inzwischen abgeschlossen sein.");
        }
        catch (Exception ex)
        {
            Warn("Die automatische Installation konnte nicht bestätigt werden (" + ex.Message + ").");
            Warn("Falls XIVLauncher nicht gestartet ist, führe die Datei manuell aus:");
            Warn("  " + setupPath);
        }

        Info(string.Empty);
        Info("Bitte melde dich jetzt im XIVLauncher an, aktiviere in den Einstellungen");
        Info("Dalamud und starte das Spiel EINMAL. Führe diesen Installer danach erneut");
        Info("aus, um das Barrierefreiheits-Plugin einzurichten.");
    }

    // ── Eigenes Plugin (FF14Accessibility) ────────────────────────────────

    private async Task<string> UpdateAccessibilityPluginAsync(string ownVersion)
    {
        Info("Prüfe neueste Version von FF14 Accessibility ...");
        try
        {
            var release = await GetLatestReleaseAsync(AccessibilityRepoOwner, AccessibilityRepoName);

            var hint = CheckInstallerUpdateHint(ownVersion, release);
            if (hint != null) Info(hint);

            var asset = PickAsset(release, n =>
                n.StartsWith("FF14Accessibility-v", StringComparison.OrdinalIgnoreCase) &&
                n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset == null)
            {
                Warn("Kein passendes Release-Paket für FF14 Accessibility gefunden.");
                return "Fehler (kein Release-Asset gefunden)";
            }

            var (tag, url, _) = asset.Value;
            var remoteVersion = tag.TrimStart('v', 'V');

            var targetDir = Path.Combine(DevPluginsRoot, AccessibilityInternalName);
            var manifestPath = Path.Combine(targetDir, AccessibilityInternalName + ".json");
            var localVersion = ReadLocalManifestVersion(manifestPath);

            if (localVersion != null && !IsNewer(remoteVersion, localVersion))
            {
                Info($"FF14 Accessibility ist aktuell (Version {localVersion}).");
                return $"aktuell (Version {localVersion})";
            }

            Info($"Lade FF14 Accessibility {remoteVersion} herunter ...");
            var zipPath = Path.Combine(Path.GetTempPath(), "FF14Accessibility_" + Guid.NewGuid() + ".zip");
            await DownloadFileAsync(url, zipPath, "FF14 Accessibility");

            var extractDir = Path.Combine(Path.GetTempPath(), "FF14AccExtract_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            DeployPluginFiles(extractDir, targetDir);

            var wasInstalled = localVersion != null;
            Info(wasInstalled
                ? $"FF14 Accessibility aktualisiert auf Version {remoteVersion}."
                : $"FF14 Accessibility installiert (Version {remoteVersion}).");
            return (wasInstalled ? "aktualisiert auf " : "neu installiert, Version ") + remoteVersion;
        }
        catch (IOException ex)
        {
            Error("Konnte Plugin-Dateien nicht schreiben: " + ex.Message);
            Error("Bitte schließe FINAL FANTASY XIV und den XIVLauncher vollständig und versuche es erneut.");
            return "Fehler (Dateien gesperrt – bitte Spiel/Launcher schließen)";
        }
        catch (HttpRequestException ex)
        {
            Error("FF14 Accessibility: GitHub nicht erreichbar (" + ex.Message + ").");
            return "Fehler (kein Netzwerk/GitHub nicht erreichbar)";
        }
        catch (TaskCanceledException)
        {
            Error("FF14 Accessibility: Zeitüberschreitung beim Download.");
            return "Fehler (Zeitüberschreitung)";
        }
        catch (Exception ex)
        {
            Error("FF14 Accessibility: Unerwarteter Fehler (" + ex.Message + ").");
            return "Fehler";
        }
    }

    // ── vnavmesh (Auto-Lauf) ───────────────────────────────────────────────

    private async Task<string> UpdateVnavmeshAsync()
    {
        Info("Prüfe neueste Version von vnavmesh (Auto-Lauf) ...");

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
            Warn("vnavmesh: puni.sh nicht erreichbar (" + ex.Message + "). Auto-Lauf bleibt unverändert.");
            return "Fehler (kein Netzwerk/puni.sh nicht erreichbar)";
        }
        catch (TaskCanceledException)
        {
            Warn("vnavmesh: Zeitüberschreitung bei puni.sh.");
            return "Fehler (Zeitüberschreitung)";
        }

        if (entry == null)
        {
            Warn("vnavmesh nicht im puni.sh-Repository gefunden.");
            return "Fehler (nicht gefunden)";
        }

        var remoteVersion = entry["AssemblyVersion"]?.GetValue<string>() ?? "unbekannt";
        var downloadUrl = entry["DownloadLinkInstall"]?.GetValue<string>();
        if (string.IsNullOrEmpty(downloadUrl))
        {
            Warn("vnavmesh: kein Download-Link im Repository gefunden.");
            return "Fehler (kein Download-Link)";
        }

        var targetDir = Path.Combine(DevPluginsRoot, VnavmeshInternalName);
        var manifestPath = Path.Combine(targetDir, VnavmeshInternalName + ".json");
        var localVersion = ReadLocalManifestVersion(manifestPath);

        if (localVersion != null && !IsNewer(remoteVersion, localVersion))
        {
            Info($"vnavmesh ist aktuell (Version {localVersion}).");
            return $"aktuell (Version {localVersion})";
        }

        if (localVersion == null)
        {
            Info(string.Empty);
            Info("Das Auto-Lauf-Feature (automatisch zu Zielen laufen) braucht das separate");
            Info("Plugin vnavmesh. Es stammt von einem anderen Autor (veyn) und wird vom");
            Info("Original geladen, nicht von uns weitergegeben.");
            var yes = AskYesNo?.Invoke("Soll vnavmesh jetzt für den Auto-Lauf eingerichtet werden?") ?? false;
            if (!yes)
            {
                Info("vnavmesh übersprungen. Alles außer dem Auto-Lauf funktioniert trotzdem.");
                return "übersprungen";
            }
        }

        try
        {
            Info($"Lade vnavmesh {remoteVersion} herunter ...");
            var zipPath = Path.Combine(Path.GetTempPath(), "vnavmesh_" + Guid.NewGuid() + ".zip");
            await DownloadFileAsync(downloadUrl, zipPath, "vnavmesh");

            var extractDir = Path.Combine(Path.GetTempPath(), "vnavmeshExtract_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(zipPath, extractDir);
            DeployPluginFiles(extractDir, targetDir);

            var wasInstalled = localVersion != null;
            Info(wasInstalled
                ? $"vnavmesh aktualisiert auf Version {remoteVersion}."
                : $"vnavmesh eingerichtet (Version {remoteVersion}).");
            return (wasInstalled ? "aktualisiert auf " : "neu eingerichtet, Version ") + remoteVersion;
        }
        catch (IOException ex)
        {
            Error("vnavmesh: Konnte Dateien nicht schreiben: " + ex.Message);
            Error("Bitte schließe FINAL FANTASY XIV und den XIVLauncher vollständig und versuche es erneut.");
            return "Fehler (Dateien gesperrt – bitte Spiel/Launcher schließen)";
        }
        catch (HttpRequestException ex)
        {
            Error("vnavmesh: Download fehlgeschlagen (" + ex.Message + ").");
            return "Fehler (kein Netzwerk)";
        }
        catch (TaskCanceledException)
        {
            Error("vnavmesh: Zeitüberschreitung beim Download.");
            return "Fehler (Zeitüberschreitung)";
        }
        catch (Exception ex)
        {
            Error("vnavmesh: Unerwarteter Fehler (" + ex.Message + ").");
            return "Fehler";
        }
    }

    // ── dalamudConfig.json ─────────────────────────────────────────────────

    /// <summary>
    /// Registers the plugin DLLs as DevPlugin load locations and flips their
    /// profile entries to enabled. Conservative on purpose: makes a backup and
    /// writes BOM-free (Dalamud's ReliableFileStorage reads raw bytes; a UTF-8 BOM
    /// makes it silently fall back to an old SQLite copy - documented project trap).
    /// </summary>
    private string PatchDalamudConfig()
    {
        if (!File.Exists(DalamudConfigPath))
        {
            Warn("dalamudConfig.json existiert noch nicht – Dalamud legt sie erst beim");
            Warn("ersten Spielstart an. Starte das Spiel EINMAL über XIVLauncher (mit");
            Warn("aktiviertem Dalamud) und führe diesen Installer danach erneut aus, damit");
            Warn("die Plugins aktiviert werden können.");
            return "dalamudConfig.json fehlt noch – bitte Spiel einmal starten und Installer erneut ausführen.";
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
            Error("dalamudConfig.json konnte nicht gelesen werden: " + ex.Message);
            Error("Bitte schließe FINAL FANTASY XIV und den XIVLauncher vollständig und versuche es erneut.");
            return "Fehler beim Lesen von dalamudConfig.json (Datei gesperrt?).";
        }

        JObject config;
        try
        {
            config = JObject.Parse(text);
        }
        catch (Exception ex)
        {
            Error("dalamudConfig.json ließ sich nicht lesen (" + ex.Message + ").");
            Error("Ich fasse sie nicht an. Bitte melde dich – hier ist Handarbeit sicherer.");
            return "Fehler: dalamudConfig.json ungültig, nicht verändert.";
        }

        var loadLocations = config["DevPluginLoadLocations"]?["$values"] as JArray;
        if (loadLocations == null)
        {
            Warn("Unerwarteter Aufbau der Konfiguration (DevPluginLoadLocations fehlt).");
            Warn("Zur Sicherheit wird nichts geändert. Bitte starte das Spiel einmal und");
            Warn("versuche es erneut.");
            return "Fehler: unerwarteter Aufbau von dalamudConfig.json, nicht verändert.";
        }

        var accDll = Path.Combine(DevPluginsRoot, AccessibilityInternalName, AccessibilityInternalName + ".dll");
        var vnavDll = Path.Combine(DevPluginsRoot, VnavmeshInternalName, VnavmeshInternalName + ".dll");
        var hasVnav = File.Exists(vnavDll);

        try
        {
            var backup = DalamudConfigPath + ".bak-installer";
            File.Copy(DalamudConfigPath, backup, overwrite: true);

            if (File.Exists(accDll)) AddDevPluginLocation(loadLocations, accDll);
            if (hasVnav) AddDevPluginLocation(loadLocations, vnavDll);

            var enabled = EnableProfilePlugins(config,
                hasVnav
                    ? new[] { AccessibilityInternalName, VnavmeshInternalName }
                    : new[] { AccessibilityInternalName });

            WriteAllTextNoBom(DalamudConfigPath, config.ToString());
            Info("Konfiguration aktualisiert (Sicherung: " + Path.GetFileName(backup) + ").");

            if (!enabled)
            {
                Info(string.Empty);
                Info("Hinweis: Das Plugin ist als Ladeort eingetragen, aber noch nicht als");
                Info("aktiviert markiert (das legt Dalamud erst beim ersten Start an). Starte");
                Info("das Spiel EINMAL, beende es, und führe diesen Installer noch einmal aus –");
                Info("dann wird es scharf geschaltet.");
                return "Plugins eingetragen, aber noch nicht aktiviert – Spiel einmal starten und Installer erneut ausführen.";
            }

            return "Plugins eingetragen und aktiviert.";
        }
        catch (IOException ex)
        {
            Error("dalamudConfig.json konnte nicht geschrieben werden: " + ex.Message);
            Error("Bitte schließe FINAL FANTASY XIV und den XIVLauncher vollständig und versuche es erneut.");
            return "Fehler beim Schreiben von dalamudConfig.json (Datei gesperrt?).";
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

    /// <summary>Sets IsEnabled=true on the DefaultProfile entries for the given
    /// plugins. Returns true if at least one matching entry existed.</summary>
    private static bool EnableProfilePlugins(JObject config, string[] internalNames)
    {
        var plugins = config["DefaultProfile"]?["Plugins"]?["$values"] as JArray;
        if (plugins == null) return false;

        var any = false;
        foreach (var p in plugins)
        {
            var name = (string?)p["InternalName"];
            if (name != null && internalNames.Contains(name))
            {
                p["IsEnabled"] = true;
                any = true;
            }
        }
        return any;
    }

    // ── GitHub-API-Helfer ──────────────────────────────────────────────────

    private async Task<JsonNode?> GetLatestReleaseAsync(string owner, string repo)
    {
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var json = await _http.GetStringAsync(apiUrl);
        return JsonNode.Parse(json);
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

    /// <summary>Sucht im selben Release ein Asset mit "Installer" im Namen und
    /// vergleicht dessen Versionsnummer mit der eigenen. Kein Selbst-Update -
    /// nur ein Hinweistext (siehe Architektur-Doc Abschnitt 4.3).</summary>
    private static string? CheckInstallerUpdateHint(string ownVersion, JsonNode? release)
    {
        var assets = release?["assets"]?.AsArray();
        if (assets == null) return null;

        foreach (var a in assets)
        {
            var name = a?["name"]?.GetValue<string>();
            if (name == null || !name.Contains("Installer", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = Regex.Match(name, @"([0-9]+(?:\.[0-9]+){1,3})");
            if (!m.Success) continue;

            var remote = ParseVersionLoose(m.Groups[1].Value);
            var own = ParseVersionLoose(ownVersion);
            if (remote != null && own != null && remote > own)
            {
                var url = a!["browser_download_url"]?.GetValue<string>() ?? "";
                return $"Hinweis: Eine neuere Installer-Version ({m.Groups[1].Value}) ist verfügbar: {url}";
            }
        }
        return null;
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
                    Info($"{label}: {percent} % ...");
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

    private static Version? ParseVersionLoose(string s)
    {
        s = s.TrimStart('v', 'V');
        var parts = s.Split('.');
        if (parts.Length < 2) s += ".0";
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
