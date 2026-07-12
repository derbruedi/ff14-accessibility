using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller;

/// <summary>
/// Barrier-free installer/updater for the FF14 Accessibility plugin (and the
/// vnavmesh pathfinding plugin it uses for auto-walk).
///
/// Why a console app and the DevPlugin route: Dalamud's own plugin installer is
/// an ImGui overlay that screen readers cannot read, so a blind user cannot use
/// it. This tool instead copies the plugin DLLs into Dalamud's devPlugins folder
/// and enables them directly in dalamudConfig.json, so Dalamud auto-loads them on
/// the next game start - no ImGui click required. Console output is read aloud by
/// NVDA automatically.
///
/// Flow:
///  - Update (main use): overwrite the DLLs in devPlugins. Next game start runs
///    the new version. No config change needed.
///  - First install: copy DLLs + register the DevPluginLoadLocation. After the
///    user starts the game once (Dalamud creates the profile entries, disabled by
///    design), a second run flips IsEnabled to true.
/// </summary>
internal static class Program
{
    // Third-party downloads. XIVLauncher setup from the official goatcorp repo;
    // vnavmesh from its official distribution (puni.sh) - we never redistribute
    // it ourselves (it has no license that would permit bundling).
    private const string XivLauncherSetupUrl =
        "https://github.com/goatcorp/FFXIVQuickLauncher/releases/latest/download/Setup.exe";
    // NOTE: pinned vnavmesh version - bump when a newer build is required.
    private const string VnavmeshZipUrl =
        "https://puni.sh/api/plugins/download/48/vnavmesh/versions/1.2.3.8/install/latest.zip";

    private const string AccessibilityInternalName = "FF14Accessibility";
    private const string VnavmeshInternalName = "vnavmesh";

    private static readonly string XivLauncherRoot =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
    private static readonly string DevPluginsRoot = Path.Combine(XivLauncherRoot, "devPlugins");
    private static readonly string DalamudConfigPath = Path.Combine(XivLauncherRoot, "dalamudConfig.json");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Info("FF14 Accessibility - Installer und Updater.");
        Info("Dieses Programm richtet das Barrierefreiheits-Plugin ein und hält es aktuell.");
        Info(string.Empty);

        try
        {
            // 1. XIVLauncher present?
            if (!Directory.Exists(XivLauncherRoot))
            {
                if (!HandleMissingXivLauncher())
                    return Finish(1);
                // After installing XIVLauncher the user still has to log in, enable
                // Dalamud and start the game once. We cannot do that for them.
                return Finish(0);
            }
            Info("XIVLauncher gefunden.");

            // 2. Copy the bundled plugin files into devPlugins (install OR update).
            var copied = CopyPluginFiles();
            if (!copied) return Finish(1);

            // 3. Offer vnavmesh (auto-walk) if it is not there yet.
            EnsureVnavmesh();

            // 4. Register / enable the plugins in dalamudConfig.json.
            PatchDalamudConfig();

            Info(string.Empty);
            Info("Fertig. Starte FINAL FANTASY XIV über XIVLauncher (neu), damit die");
            Info("Änderungen geladen werden. Beim Login sollte das Plugin sich mit einer");
            Info("Versionsansage melden.");
            return Finish(0);
        }
        catch (Exception ex)
        {
            Error("Unerwarteter Fehler: " + ex.Message);
            Error("Es wurde nichts Unvollständiges geschrieben, das dein System beschädigt.");
            return Finish(1);
        }
    }

    // ── XIVLauncher ────────────────────────────────────────────────────────

    private static bool HandleMissingXivLauncher()
    {
        Warn("XIVLauncher ist nicht installiert (Ordner nicht gefunden:");
        Warn("  " + XivLauncherRoot + ").");
        Info("XIVLauncher wird gebraucht, weil es Dalamud lädt - die Grundlage für das Plugin.");
        if (!AskYesNo("Soll ich das offizielle XIVLauncher-Setup jetzt herunterladen und starten?"))
        {
            Info("Abgebrochen. Installiere XIVLauncher von https://goatcorp.github.io/ und");
            Info("führe mich danach erneut aus.");
            return false;
        }

        var setupPath = Path.Combine(Path.GetTempPath(), "XIVLauncherSetup.exe");
        Info("Lade XIVLauncher-Setup herunter ...");
        if (!TryDownload(XivLauncherSetupUrl, setupPath))
        {
            Error("Download fehlgeschlagen. Bitte installiere XIVLauncher manuell von");
            Error("https://goatcorp.github.io/ und führe mich danach erneut aus.");
            return false;
        }

        Info("Starte das Setup. Folge dem XIVLauncher-Assistenten, melde dich an und");
        Info("aktiviere in den Einstellungen Dalamud. Starte das Spiel danach EINMAL.");
        Info("Führe dieses Programm anschließend erneut aus, um das Plugin einzurichten.");
        Process.Start(new ProcessStartInfo(setupPath) { UseShellExecute = true });
        return true;
    }

    // ── Plugin files ───────────────────────────────────────────────────────

    /// <summary>
    /// Copies the bundled plugin files (shipped in a "plugin" folder next to this
    /// EXE) into devPlugins\FF14Accessibility. Overwrites existing files, which is
    /// exactly what an update needs.
    /// </summary>
    private static bool CopyPluginFiles()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "plugin");
        if (!Directory.Exists(source))
        {
            Error("Die mitgelieferten Plugin-Dateien fehlen (erwartet neben dieser EXE:");
            Error("  " + source + ").");
            Error("Bitte lade das komplette Installer-Paket herunter, nicht nur die EXE.");
            return false;
        }

        var mainDll = Path.Combine(source, AccessibilityInternalName + ".dll");
        if (!File.Exists(mainDll))
        {
            Error("Im plugin-Ordner fehlt " + AccessibilityInternalName + ".dll - Paket unvollständig.");
            return false;
        }

        var target = Path.Combine(DevPluginsRoot, AccessibilityInternalName);
        Directory.CreateDirectory(target);

        var count = 0;
        foreach (var file in Directory.GetFiles(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
            count++;
        }
        Info($"Plugin-Dateien kopiert ({count} Dateien) nach:");
        Info("  " + target);
        return true;
    }

    // ── vnavmesh (auto-walk) ───────────────────────────────────────────────

    private static void EnsureVnavmesh()
    {
        var target = Path.Combine(DevPluginsRoot, VnavmeshInternalName);
        if (File.Exists(Path.Combine(target, "vnavmesh.dll")))
        {
            Info("vnavmesh ist bereits vorhanden (für den Auto-Lauf).");
            return;
        }

        Info(string.Empty);
        Info("Das Auto-Lauf-Feature (automatisch zu Zielen laufen) braucht das separate");
        Info("Plugin vnavmesh. Es stammt von einem anderen Autor und wird vom Original");
        Info("geladen, nicht von uns weitergegeben.");
        if (!AskYesNo("Soll ich vnavmesh jetzt herunterladen und einrichten?"))
        {
            Info("Übersprungen. Ohne vnavmesh funktioniert alles außer dem Auto-Lauf.");
            return;
        }

        var zipPath = Path.Combine(Path.GetTempPath(), "vnavmesh.zip");
        Info("Lade vnavmesh herunter ...");
        if (!TryDownload(VnavmeshZipUrl, zipPath))
        {
            Warn("vnavmesh-Download fehlgeschlagen - der Auto-Lauf bleibt vorerst aus.");
            Warn("Das übrige Plugin funktioniert trotzdem. Du kannst es später erneut versuchen.");
            return;
        }

        try
        {
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
            Info("vnavmesh eingerichtet nach:");
            Info("  " + target);
        }
        catch (Exception ex)
        {
            Warn("vnavmesh konnte nicht entpackt werden: " + ex.Message);
            Warn("Der Auto-Lauf bleibt vorerst aus; der Rest funktioniert.");
        }
    }

    // ── dalamudConfig.json ─────────────────────────────────────────────────

    /// <summary>
    /// Registers the plugin DLLs as DevPlugin load locations and flips their
    /// profile entries to enabled. Conservative on purpose: makes a backup and
    /// writes BOM-free (Dalamud's ReliableFileStorage reads raw bytes; a UTF-8 BOM
    /// makes it silently fall back to an old SQLite copy - documented project trap).
    /// </summary>
    private static void PatchDalamudConfig()
    {
        Info(string.Empty);
        if (!File.Exists(DalamudConfigPath))
        {
            Warn("dalamudConfig.json existiert noch nicht - Dalamud legt sie erst beim");
            Warn("ersten Spielstart an. Starte das Spiel EINMAL über XIVLauncher (mit");
            Warn("aktiviertem Dalamud) und führe mich danach erneut aus, damit ich das");
            Warn("Plugin aktivieren kann.");
            return;
        }

        // Read raw bytes and strip a UTF-8 BOM if present, so parsing is clean.
        var bytes = File.ReadAllBytes(DalamudConfigPath);
        var text = StripBom(bytes);

        JObject config;
        try
        {
            config = JObject.Parse(text);
        }
        catch (Exception ex)
        {
            Error("dalamudConfig.json ließ sich nicht lesen (" + ex.Message + ").");
            Error("Ich fasse sie nicht an. Bitte melde dich - hier ist Handarbeit sicherer.");
            return;
        }

        var loadLocations = config["DevPluginLoadLocations"]?["$values"] as JArray;
        if (loadLocations == null)
        {
            Warn("Unerwarteter Aufbau der Konfiguration (DevPluginLoadLocations fehlt).");
            Warn("Zur Sicherheit ändere ich nichts. Bitte starte das Spiel einmal und");
            Warn("versuche es erneut.");
            return;
        }

        // Backup before the first write.
        var backup = DalamudConfigPath + ".bak-installer";
        File.Copy(DalamudConfigPath, backup, overwrite: true);

        var accDll = Path.Combine(DevPluginsRoot, AccessibilityInternalName, AccessibilityInternalName + ".dll");
        var vnavDll = Path.Combine(DevPluginsRoot, VnavmeshInternalName, VnavmeshInternalName + ".dll");

        AddDevPluginLocation(loadLocations, accDll);
        if (File.Exists(vnavDll)) AddDevPluginLocation(loadLocations, vnavDll);

        // Enable existing profile entries (Dalamud adds new DevPlugins disabled).
        var enabled = EnableProfilePlugins(config,
            File.Exists(vnavDll)
                ? new[] { AccessibilityInternalName, VnavmeshInternalName }
                : new[] { AccessibilityInternalName });

        WriteAllTextNoBom(DalamudConfigPath, config.ToString());
        Info("Konfiguration aktualisiert (Sicherung: " + Path.GetFileName(backup) + ").");

        if (!enabled)
        {
            Info(string.Empty);
            Info("Hinweis: Das Plugin ist als Ladeort eingetragen, aber noch nicht als");
            Info("aktiviert markiert (das legt Dalamud erst beim ersten Start an). Starte");
            Info("das Spiel EINMAL, beende es, und führe mich noch einmal aus - dann");
            Info("schalte ich es scharf.");
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

    // ── Helpers ────────────────────────────────────────────────────────────

    private static bool TryDownload(string url, string destination)
    {
        try
        {
            using var response = Http.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            using var fs = File.Create(destination);
            response.Content.CopyToAsync(fs).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Error("Download-Fehler: " + ex.Message);
            return false;
        }
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

    private static bool AskYesNo(string question)
    {
        Console.WriteLine();
        Console.WriteLine(question + " Tippe j für ja oder n für nein und drücke Enter.");
        while (true)
        {
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (answer is "j" or "ja" or "y" or "yes") return true;
            if (answer is "n" or "nein" or "no") return false;
            Console.WriteLine("Bitte j oder n eingeben.");
        }
    }

    private static int Finish(int code)
    {
        Console.WriteLine();
        Console.WriteLine("Drücke Enter, um dieses Fenster zu schließen.");
        Console.ReadLine();
        return code;
    }

    private static void Info(string message) => Console.WriteLine(message);
    private static void Warn(string message) => Console.WriteLine("Achtung: " + message);
    private static void Error(string message) => Console.WriteLine("Fehler: " + message);
}
