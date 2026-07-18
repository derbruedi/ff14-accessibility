using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FF14AccessibilityInstaller;

/// <summary>
/// Minimal static localization helper (DE/EN). No resx needed - just two
/// dictionaries and a Get(key) lookup. Keeps the current UI language in
/// <see cref="Current"/> and persists the user's choice to
/// %APPDATA%\FF14AccessibilityInstaller\installer-settings.json so it can be
/// pre-selected (not auto-applied) the next time the language dialog shows.
/// </summary>
public static class Loc
{
    public const string German = "de";
    public const string English = "en";

    /// <summary>Currently active UI language ("de" or "en"). Defaults to German.</summary>
    public static string Current { get; set; } = German;

    public static string Get(string key)
    {
        if (Texts.TryGetValue(Current, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        if (Texts[German].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    public static string Get(string key, params object?[] args) => string.Format(Get(key), args);

    // ── Sprache erkennen/merken ─────────────────────────────────────────────

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FF14AccessibilityInstaller");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "installer-settings.json");

    /// <summary>Reads the previously saved language, or null if none was saved yet.</summary>
    public static string? LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("language", out var langProp))
            {
                var lang = langProp.GetString();
                if (lang == German || lang == English) return lang;
            }
        }
        catch
        {
            // Beschädigte/fehlende Settings-Datei ist kein Fehlerfall - einfach neu fragen.
        }
        return null;
    }

    public static void SaveLanguage(string language)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(new { language });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Nicht kritisch - beim naechsten Start wird einfach wieder gefragt.
        }
    }

    /// <summary>
    /// Detects whether the system UI language is German, via a raw Win32 call
    /// (GetUserDefaultLocaleName) rather than CultureInfo. The project sets
    /// InvariantGlobalization=true (see csproj), which makes .NET's own
    /// CultureInfo.CurrentUICulture always report the invariant culture - so
    /// we bypass that and ask Windows directly.
    /// </summary>
    public static bool SystemLanguageIsGerman()
    {
        try
        {
            var sb = new StringBuilder(85);
            if (GetUserDefaultLocaleName(sb, sb.Capacity) > 0)
                return sb.ToString().StartsWith("de", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Falls der Aufruf scheitert, bleibt es bei Englisch als Fallback.
        }
        return false;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetUserDefaultLocaleName(StringBuilder lpLocaleName, int cchLocaleName);

    // ── Texte ────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        [German] = new Dictionary<string, string>
        {
            ["WarnPrefix"] = "Achtung: ",
            ["ErrorPrefix"] = "Fehler: ",
            ["UnknownVersion"] = "unbekannt",

            ["InstallerHeader"] = "FF14 Accessibility – Installer und Updater (Version {0}).",
            ["CheckingXivLauncher"] = "Prüfe XIVLauncher ...",
            ["XivLauncherFound"] = "XIVLauncher gefunden.",
            ["SummaryHeader"] = "=== Zusammenfassung ===",
            ["SummaryAccessibility"] = "FF14 Accessibility: {0}",
            ["SummaryVnavmesh"] = "vnavmesh (Auto-Lauf): {0}",
            ["UnexpectedError"] = "Unerwarteter Fehler: {0}",
            ["NoPartialWrite"] = "Es wurde nichts Unvollständiges geschrieben, das dein System beschädigt.",

            ["XivLauncherNotInstalled1"] = "XIVLauncher ist nicht installiert (Ordner nicht gefunden:",
            ["XivLauncherNotInstalled2"] = "  {0}).",
            ["XivLauncherNeeded"] = "XIVLauncher wird gebraucht, weil es Dalamud lädt – die Grundlage für das Plugin.",
            ["DownloadingXivLauncherAuto"] = "Lade die neueste XIVLauncher-Version herunter und installiere sie automatisch ...",
            ["GitHubUnreachable"] = "GitHub nicht erreichbar ({0}). Bitte Internetverbindung prüfen.",
            ["InstallXivLauncherManually1"] = "Installiere XIVLauncher alternativ manuell von https://goatcorp.github.io/ und",
            ["RunProgramAgain"] = "führe dieses Programm danach erneut aus.",
            ["TimeoutFetchXivLauncher"] = "Zeitüberschreitung beim Abruf der XIVLauncher-Version. Bitte Internetverbindung prüfen.",
            ["NoXivLauncherSetupFound"] = "Kein XIVLauncher-Setup im neuesten Release gefunden.",
            ["InstallXivLauncherManually2"] = "Bitte installiere XIVLauncher manuell von https://goatcorp.github.io/ .",
            ["DownloadingXivLauncherVersion"] = "Lade XIVLauncher {0} herunter ({1}) ...",
            ["DownloadFailedInstallManually"] = "Download fehlgeschlagen ({0}). Bitte installiere XIVLauncher manuell von",
            ["UrlAndRunAgain"] = "https://goatcorp.github.io/ und führe dieses Programm danach erneut aus.",
            ["TimeoutDownloadRetry"] = "Zeitüberschreitung beim Download. Bitte Internetverbindung prüfen und erneut versuchen.",
            ["XivLauncherSaveFailed"] = "XIVLauncher-Setup konnte nicht gespeichert werden: {0}",
            ["InstallingXivLauncherSilent"] = "Installiere XIVLauncher automatisch im Hintergrund (--silent) ...",
            ["XivLauncherInstallStarted"] = "XIVLauncher-Installation wurde gestartet und sollte inzwischen abgeschlossen sein.",
            ["AutoInstallNotConfirmed"] = "Die automatische Installation konnte nicht bestätigt werden ({0}).",
            ["RunSetupManuallyHint"] = "Falls XIVLauncher nicht gestartet ist, führe die Datei manuell aus:",
            ["LoginHint1"] = "Bitte melde dich jetzt im XIVLauncher an, aktiviere in den Einstellungen",
            ["LoginHint2"] = "Dalamud und starte das Spiel EINMAL. Führe diesen Installer danach erneut",
            ["LoginHint3"] = "aus, um das Barrierefreiheits-Plugin einzurichten.",

            // Selbst-Update des Installers
            ["CheckingInstallerVersion"] = "Prüfe, ob es eine neuere Installer-Version gibt ...",
            ["NoInstallerManifest"] = "Keine Installer-Versionsangabe im Release gefunden - überspringe die Prüfung.",
            ["InstallerCheckFailed"] = "Installer-Version konnte nicht geprüft werden ({0}). Der Installer arbeitet normal weiter.",
            ["InstallerManifestUnreadable"] = "Installer-Versionsangabe war nicht lesbar - überspringe die Prüfung.",
            ["InstallerUpToDate"] = "Der Installer ist aktuell (Version {0}).",
            ["InstallerAssetMissing"] = "Die neue Installer-Datei ({0}) fehlt im Release - überspringe das Update.",
            ["InstallerOwnPathUnknown"] = "Eigener Programmpfad nicht ermittelbar - überspringe das Installer-Update.",
            ["InstallerUpdateAvailable"] = "Neue Installer-Version verfügbar: {0} (installiert ist {1}).",
            ["InstallerUpdateQuestion"] =
                "Es gibt eine neuere Version des Installers ({0}).\n\n" +
                "Soll sie jetzt heruntergeladen und gestartet werden? Der Download ist etwa {1} Megabyte groß.\n\n" +
                "Der Installer schließt sich dabei kurz und öffnet sich automatisch neu. " +
                "Danach läuft die Installation von selbst weiter.\n\n" +
                "Ja = jetzt aktualisieren, Nein = mit der aktuellen Version weiterarbeiten.",
            ["InstallerUpdateDeclined"] = "Installer-Update übersprungen. Es geht mit der vorhandenen Version weiter.",
            ["DownloadingInstaller"] = "Lade Installer-Version {0} herunter ...",
            ["InstallerDownloadLabel"] = "Installer",
            ["InstallerDownloadFailed"] = "Installer-Update konnte nicht geladen werden ({0}). Es geht mit der vorhandenen Version weiter.",
            ["InstallerHashOk"] = "Prüfsumme der neuen Installer-Datei stimmt.",
            ["InstallerNoHash"] = "Keine Prüfsumme im Release hinterlegt - Download wird ungeprüft übernommen.",
            ["InstallerHashMismatch"] = "Die Prüfsumme der geladenen Installer-Datei stimmt NICHT. Update abgebrochen, es geht mit der vorhandenen Version weiter.",
            ["InstallerStartFailed"] = "Der neue Installer konnte nicht gestartet werden ({0}). Es geht mit der vorhandenen Version weiter.",
            ["InstallerRestarting"] = "Installer wird auf Version {0} aktualisiert. Das Fenster schließt sich jetzt und öffnet sich gleich automatisch neu ...",
            ["InstallerUpdatedTo"] = "Installer wurde auf Version {0} aktualisiert. Die Installation läuft automatisch weiter.",
            ["InstallerUpdatedMessage"] =
                "Der Installer wurde auf Version {0} aktualisiert und neu gestartet.\n\n" +
                "Die Installation läuft nach dem Bestätigen automatisch weiter - du musst nichts weiter tun.",
            ["SelfUpdateNoOwnPath"] =
                "Der eigene Programmpfad war nicht ermittelbar. Die alte Installer-Datei bleibt bestehen; " +
                "dieses Fenster arbeitet aus dem temporären Ordner weiter.",
            ["SelfUpdateReplaceFailed"] =
                "Die vorhandene Installer-Datei konnte nicht ersetzt werden:\n{0}\n\nGrund: {1}\n\n" +
                "Die Installation kann trotzdem weiterlaufen. Wenn du die neue Version dauerhaft behalten willst, " +
                "lade FF14AccessibilityInstaller.exe einmal von Hand aus dem neuesten Release herunter.",
            ["SelfUpdateRestartFailed"] =
                "Die Installer-Datei wurde aktualisiert, konnte aber nicht automatisch gestartet werden:\n{0}\n\nGrund: {1}\n\n" +
                "Bitte starte sie von Hand.",

            ["CheckingAccessibilityVersion"] = "Prüfe neueste Version von FF14 Accessibility ...",
            ["NoAccessibilityAssetFound"] = "Kein passendes Release-Paket für FF14 Accessibility gefunden.",
            ["ErrorNoReleaseAsset"] = "Fehler (kein Release-Asset gefunden)",
            ["AccessibilityUpToDate"] = "FF14 Accessibility ist aktuell (Version {0}).",
            ["UpToDateShort"] = "aktuell (Version {0})",
            ["DownloadingAccessibility"] = "Lade FF14 Accessibility {0} herunter ...",
            ["AccessibilityUpdated"] = "FF14 Accessibility aktualisiert auf Version {0}.",
            ["AccessibilityInstalled"] = "FF14 Accessibility installiert (Version {0}).",
            ["UpdatedToShort"] = "aktualisiert auf {0}",
            ["NewlyInstalledShort"] = "neu installiert, Version {0}",
            ["CouldNotWritePluginFiles"] = "Konnte Plugin-Dateien nicht schreiben: {0}",
            ["CloseGameAndLauncher"] = "Bitte schließe FINAL FANTASY XIV und den XIVLauncher vollständig und versuche es erneut.",
            ["ErrorFilesLocked"] = "Fehler (Dateien gesperrt – bitte Spiel/Launcher schließen)",
            ["AccessibilityGitHubUnreachable"] = "FF14 Accessibility: GitHub nicht erreichbar ({0}).",
            ["ErrorNoNetworkGitHub"] = "Fehler (kein Netzwerk/GitHub nicht erreichbar)",
            ["AccessibilityDownloadTimeout"] = "FF14 Accessibility: Zeitüberschreitung beim Download.",
            ["ErrorTimeout"] = "Fehler (Zeitüberschreitung)",
            ["AccessibilityUnexpectedError"] = "FF14 Accessibility: Unerwarteter Fehler ({0}).",
            ["ErrorGeneric"] = "Fehler",

            ["CheckingVnavmeshVersion"] = "Prüfe neueste Version von vnavmesh (Auto-Lauf) ...",
            ["VnavmeshPunishUnreachable"] = "vnavmesh: puni.sh nicht erreichbar ({0}). Auto-Lauf bleibt unverändert.",
            ["ErrorNoNetworkPunish"] = "Fehler (kein Netzwerk/puni.sh nicht erreichbar)",
            ["VnavmeshPunishTimeout"] = "vnavmesh: Zeitüberschreitung bei puni.sh.",
            ["VnavmeshNotFound"] = "vnavmesh nicht im puni.sh-Repository gefunden.",
            ["ErrorNotFound"] = "Fehler (nicht gefunden)",
            ["VnavmeshNoDownloadLink"] = "vnavmesh: kein Download-Link im Repository gefunden.",
            ["ErrorNoDownloadLink"] = "Fehler (kein Download-Link)",
            ["VnavmeshUpToDate"] = "vnavmesh ist aktuell (Version {0}).",
            ["AutoWalkNeedsVnav1"] = "Das Auto-Lauf-Feature (automatisch zu Zielen laufen) braucht das separate",
            ["AutoWalkNeedsVnav2"] = "Plugin vnavmesh. Es stammt von einem anderen Autor (veyn) und wird vom",
            ["AutoWalkNeedsVnav3"] = "Original geladen, nicht von uns weitergegeben.",
            ["AskSetupVnavmesh"] = "Soll vnavmesh jetzt für den Auto-Lauf eingerichtet werden?",
            ["VnavmeshSkipped"] = "vnavmesh übersprungen. Alles außer dem Auto-Lauf funktioniert trotzdem.",
            ["SkippedShort"] = "übersprungen",
            ["DownloadingVnavmesh"] = "Lade vnavmesh {0} herunter ...",
            ["VnavmeshUpdated"] = "vnavmesh aktualisiert auf Version {0}.",
            ["VnavmeshSetup"] = "vnavmesh eingerichtet (Version {0}).",
            ["NewlySetupShort"] = "neu eingerichtet, Version {0}",
            ["VnavmeshCouldNotWriteFiles"] = "vnavmesh: Konnte Dateien nicht schreiben: {0}",
            ["VnavmeshDownloadFailed"] = "vnavmesh: Download fehlgeschlagen ({0}).",
            ["ErrorNoNetwork"] = "Fehler (kein Netzwerk)",
            ["VnavmeshDownloadTimeout"] = "vnavmesh: Zeitüberschreitung beim Download.",
            ["VnavmeshUnexpectedError"] = "vnavmesh: Unerwarteter Fehler ({0}).",

            ["ConfigNotExist1"] = "dalamudConfig.json existiert noch nicht – Dalamud legt sie erst beim",
            ["ConfigNotExist2"] = "ersten Spielstart an. Starte das Spiel EINMAL über XIVLauncher (mit",
            ["ConfigNotExist3"] = "aktiviertem Dalamud) und führe diesen Installer danach erneut aus, damit",
            ["ConfigNotExist4"] = "die Plugins aktiviert werden können.",
            ["ConfigMissingReturn"] = "dalamudConfig.json fehlt noch – bitte Spiel einmal starten und Installer erneut ausführen.",
            ["ConfigReadFailed"] = "dalamudConfig.json konnte nicht gelesen werden: {0}",
            ["ConfigReadFailedReturn"] = "Fehler beim Lesen von dalamudConfig.json (Datei gesperrt?).",
            ["ConfigParseFailed"] = "dalamudConfig.json ließ sich nicht lesen ({0}).",
            ["ConfigNotTouching"] = "Ich fasse sie nicht an. Bitte melde dich – hier ist Handarbeit sicherer.",
            ["ConfigInvalidReturn"] = "Fehler: dalamudConfig.json ungültig, nicht verändert.",
            ["ConfigUnexpectedStructure"] = "Unerwarteter Aufbau der Konfiguration (DevPluginLoadLocations fehlt).",
            ["ConfigSafetyNoChange1"] = "Zur Sicherheit wird nichts geändert. Bitte starte das Spiel einmal und",
            ["ConfigSafetyNoChange2"] = "versuche es erneut.",
            ["ConfigUnexpectedStructureReturn"] = "Fehler: unerwarteter Aufbau von dalamudConfig.json, nicht verändert.",
            ["ConfigUpdated"] = "Konfiguration aktualisiert (Sicherung: {0}).",
            ["ProfileStructureUnexpected"] = "Unerwarteter Aufbau der Konfiguration (DefaultProfile fehlt) – Plugins konnten nicht aktiviert werden. Bitte melde dich.",
            ["ProfileStructureUnexpectedReturn"] = "Fehler: Plugins eingetragen, aber Aktivierung nicht möglich (DefaultProfile fehlt).",
            ["PluginsRegisteredEnabledReturn"] = "Plugins eingetragen und aktiviert.",
            ["ConfigWriteFailed"] = "dalamudConfig.json konnte nicht geschrieben werden: {0}",
            ["ConfigWriteFailedReturn"] = "Fehler beim Schreiben von dalamudConfig.json (Datei gesperrt?).",

            ["DownloadProgress"] = "{0}: {1} % ...",

            ["WindowTitle"] = "FF14 Accessibility – Installer",
            ["MainTitleWithVersion"] = "FF14 Accessibility – Installer und Updater (Version {0})",
            ["MainTitleAccessibleName"] = "FF14 Accessibility Installer, Version {0}",
            ["LogBoxAccessibleName"] = "Statusmeldungen",
            ["LogBoxAccessibleDescription"] = "Fortschritts- und Ergebnismeldungen des Installers, mit Pfeiltasten durchgehbar.",
            ["InstallButtonText"] = "Installieren / Aktualisieren",
            ["InstallButtonAccessibleName"] = "Installieren oder Aktualisieren",
            ["ExitButtonText"] = "Beenden",
            ["ExitButtonAccessibleName"] = "Beenden",
            ["OperationCompleted"] = "Vorgang abgeschlossen. Details siehe Log-Bereich im Fenster.",

            ["LanguageDialogTitle"] = "Sprache wählen / Choose language",
            ["LanguageGermanButton"] = "Deutsch",
            ["LanguageEnglishButton"] = "English",
        },
        [English] = new Dictionary<string, string>
        {
            ["WarnPrefix"] = "Warning: ",
            ["ErrorPrefix"] = "Error: ",
            ["UnknownVersion"] = "unknown",

            ["InstallerHeader"] = "FF14 Accessibility – Installer and Updater (version {0}).",
            ["CheckingXivLauncher"] = "Checking for XIVLauncher ...",
            ["XivLauncherFound"] = "XIVLauncher found.",
            ["SummaryHeader"] = "=== Summary ===",
            ["SummaryAccessibility"] = "FF14 Accessibility: {0}",
            ["SummaryVnavmesh"] = "vnavmesh (auto-walk): {0}",
            ["UnexpectedError"] = "Unexpected error: {0}",
            ["NoPartialWrite"] = "Nothing incomplete was written that could damage your system.",

            ["XivLauncherNotInstalled1"] = "XIVLauncher is not installed (folder not found:",
            ["XivLauncherNotInstalled2"] = "  {0}).",
            ["XivLauncherNeeded"] = "XIVLauncher is required because it loads Dalamud, the foundation the plugin runs on.",
            ["DownloadingXivLauncherAuto"] = "Downloading the latest XIVLauncher version and installing it automatically ...",
            ["GitHubUnreachable"] = "GitHub is unreachable ({0}). Please check your internet connection.",
            ["InstallXivLauncherManually1"] = "Alternatively, install XIVLauncher manually from https://goatcorp.github.io/ and",
            ["RunProgramAgain"] = "run this program again afterwards.",
            ["TimeoutFetchXivLauncher"] = "Timed out while checking the XIVLauncher version. Please check your internet connection.",
            ["NoXivLauncherSetupFound"] = "No XIVLauncher setup found in the latest release.",
            ["InstallXivLauncherManually2"] = "Please install XIVLauncher manually from https://goatcorp.github.io/ .",
            ["DownloadingXivLauncherVersion"] = "Downloading XIVLauncher {0} ({1}) ...",
            ["DownloadFailedInstallManually"] = "Download failed ({0}). Please install XIVLauncher manually from",
            ["UrlAndRunAgain"] = "https://goatcorp.github.io/ and run this program again afterwards.",
            ["TimeoutDownloadRetry"] = "Timed out during download. Please check your internet connection and try again.",
            ["XivLauncherSaveFailed"] = "Could not save the XIVLauncher setup file: {0}",
            ["InstallingXivLauncherSilent"] = "Installing XIVLauncher automatically in the background (--silent) ...",
            ["XivLauncherInstallStarted"] = "The XIVLauncher installation was started and should be finished by now.",
            ["AutoInstallNotConfirmed"] = "The automatic installation could not be confirmed ({0}).",
            ["RunSetupManuallyHint"] = "If XIVLauncher did not start, run the file manually:",
            ["LoginHint1"] = "Please log in to XIVLauncher now, enable Dalamud in the settings,",
            ["LoginHint2"] = "and start the game ONCE. Then run this installer again",
            ["LoginHint3"] = "to set up the accessibility plugin.",

            // Installer self-update
            ["CheckingInstallerVersion"] = "Checking whether a newer installer version exists ...",
            ["NoInstallerManifest"] = "No installer version info found in the release - skipping the check.",
            ["InstallerCheckFailed"] = "Could not check the installer version ({0}). Carrying on as usual.",
            ["InstallerManifestUnreadable"] = "The installer version info could not be read - skipping the check.",
            ["InstallerUpToDate"] = "The installer is up to date (version {0}).",
            ["InstallerAssetMissing"] = "The new installer file ({0}) is missing from the release - skipping the update.",
            ["InstallerOwnPathUnknown"] = "Could not determine this program's own path - skipping the installer update.",
            ["InstallerUpdateAvailable"] = "A newer installer version is available: {0} (you have {1}).",
            ["InstallerUpdateQuestion"] =
                "A newer version of the installer is available ({0}).\n\n" +
                "Download and start it now? The download is about {1} megabytes.\n\n" +
                "The installer will close briefly and reopen automatically. " +
                "The installation then continues on its own.\n\n" +
                "Yes = update now, No = keep using the current version.",
            ["InstallerUpdateDeclined"] = "Installer update skipped. Continuing with the current version.",
            ["DownloadingInstaller"] = "Downloading installer version {0} ...",
            ["InstallerDownloadLabel"] = "Installer",
            ["InstallerDownloadFailed"] = "Could not download the installer update ({0}). Continuing with the current version.",
            ["InstallerHashOk"] = "Checksum of the new installer file matches.",
            ["InstallerNoHash"] = "No checksum published in the release - the download is accepted unverified.",
            ["InstallerHashMismatch"] = "The checksum of the downloaded installer does NOT match. Update aborted, continuing with the current version.",
            ["InstallerStartFailed"] = "The new installer could not be started ({0}). Continuing with the current version.",
            ["InstallerRestarting"] = "Updating the installer to version {0}. This window closes now and reopens automatically in a moment ...",
            ["InstallerUpdatedTo"] = "The installer was updated to version {0}. The installation continues automatically.",
            ["InstallerUpdatedMessage"] =
                "The installer was updated to version {0} and restarted.\n\n" +
                "After you confirm, the installation continues automatically - there is nothing else you need to do.",
            ["SelfUpdateNoOwnPath"] =
                "Could not determine this program's own path. The old installer file stays as it is; " +
                "this window continues from the temporary folder.",
            ["SelfUpdateReplaceFailed"] =
                "Could not replace the existing installer file:\n{0}\n\nReason: {1}\n\n" +
                "The installation can still continue. If you want to keep the new version permanently, " +
                "download FF14AccessibilityInstaller.exe manually from the latest release once.",
            ["SelfUpdateRestartFailed"] =
                "The installer file was updated but could not be started automatically:\n{0}\n\nReason: {1}\n\n" +
                "Please start it manually.",

            ["CheckingAccessibilityVersion"] = "Checking for the latest version of FF14 Accessibility ...",
            ["NoAccessibilityAssetFound"] = "No matching release package found for FF14 Accessibility.",
            ["ErrorNoReleaseAsset"] = "Error (no release asset found)",
            ["AccessibilityUpToDate"] = "FF14 Accessibility is up to date (version {0}).",
            ["UpToDateShort"] = "up to date (version {0})",
            ["DownloadingAccessibility"] = "Downloading FF14 Accessibility {0} ...",
            ["AccessibilityUpdated"] = "FF14 Accessibility updated to version {0}.",
            ["AccessibilityInstalled"] = "FF14 Accessibility installed (version {0}).",
            ["UpdatedToShort"] = "updated to {0}",
            ["NewlyInstalledShort"] = "newly installed, version {0}",
            ["CouldNotWritePluginFiles"] = "Could not write plugin files: {0}",
            ["CloseGameAndLauncher"] = "Please close FINAL FANTASY XIV and XIVLauncher completely, then try again.",
            ["ErrorFilesLocked"] = "Error (files locked – please close the game/launcher)",
            ["AccessibilityGitHubUnreachable"] = "FF14 Accessibility: GitHub is unreachable ({0}).",
            ["ErrorNoNetworkGitHub"] = "Error (no network / GitHub unreachable)",
            ["AccessibilityDownloadTimeout"] = "FF14 Accessibility: Timed out during download.",
            ["ErrorTimeout"] = "Error (timed out)",
            ["AccessibilityUnexpectedError"] = "FF14 Accessibility: Unexpected error ({0}).",
            ["ErrorGeneric"] = "Error",

            ["CheckingVnavmeshVersion"] = "Checking for the latest version of vnavmesh (auto-walk) ...",
            ["VnavmeshPunishUnreachable"] = "vnavmesh: puni.sh is unreachable ({0}). Auto-walk remains unchanged.",
            ["ErrorNoNetworkPunish"] = "Error (no network / puni.sh unreachable)",
            ["VnavmeshPunishTimeout"] = "vnavmesh: Timed out contacting puni.sh.",
            ["VnavmeshNotFound"] = "vnavmesh not found in the puni.sh repository.",
            ["ErrorNotFound"] = "Error (not found)",
            ["VnavmeshNoDownloadLink"] = "vnavmesh: no download link found in the repository.",
            ["ErrorNoDownloadLink"] = "Error (no download link)",
            ["VnavmeshUpToDate"] = "vnavmesh is up to date (version {0}).",
            ["AutoWalkNeedsVnav1"] = "The auto-walk feature (walking to targets automatically) needs the separate",
            ["AutoWalkNeedsVnav2"] = "vnavmesh plugin. It comes from a different author (veyn) and is loaded from",
            ["AutoWalkNeedsVnav3"] = "the original source, not redistributed by us.",
            ["AskSetupVnavmesh"] = "Set up vnavmesh now for auto-walk?",
            ["VnavmeshSkipped"] = "vnavmesh skipped. Everything except auto-walk still works.",
            ["SkippedShort"] = "skipped",
            ["DownloadingVnavmesh"] = "Downloading vnavmesh {0} ...",
            ["VnavmeshUpdated"] = "vnavmesh updated to version {0}.",
            ["VnavmeshSetup"] = "vnavmesh set up (version {0}).",
            ["NewlySetupShort"] = "newly set up, version {0}",
            ["VnavmeshCouldNotWriteFiles"] = "vnavmesh: Could not write files: {0}",
            ["VnavmeshDownloadFailed"] = "vnavmesh: Download failed ({0}).",
            ["ErrorNoNetwork"] = "Error (no network)",
            ["VnavmeshDownloadTimeout"] = "vnavmesh: Timed out during download.",
            ["VnavmeshUnexpectedError"] = "vnavmesh: Unexpected error ({0}).",

            ["ConfigNotExist1"] = "dalamudConfig.json does not exist yet – Dalamud only creates it on the",
            ["ConfigNotExist2"] = "first game start. Start the game ONCE via XIVLauncher (with",
            ["ConfigNotExist3"] = "Dalamud enabled) and run this installer again afterwards so",
            ["ConfigNotExist4"] = "the plugins can be enabled.",
            ["ConfigMissingReturn"] = "dalamudConfig.json is still missing – please start the game once and run the installer again.",
            ["ConfigReadFailed"] = "dalamudConfig.json could not be read: {0}",
            ["ConfigReadFailedReturn"] = "Error reading dalamudConfig.json (file locked?).",
            ["ConfigParseFailed"] = "dalamudConfig.json could not be parsed ({0}).",
            ["ConfigNotTouching"] = "I will not touch it. Please get in touch – manual editing is safer here.",
            ["ConfigInvalidReturn"] = "Error: dalamudConfig.json is invalid, not modified.",
            ["ConfigUnexpectedStructure"] = "Unexpected configuration structure (DevPluginLoadLocations missing).",
            ["ConfigSafetyNoChange1"] = "For safety, nothing will be changed. Please start the game once and",
            ["ConfigSafetyNoChange2"] = "try again.",
            ["ConfigUnexpectedStructureReturn"] = "Error: unexpected dalamudConfig.json structure, not modified.",
            ["ConfigUpdated"] = "Configuration updated (backup: {0}).",
            ["ProfileStructureUnexpected"] = "Unexpected configuration structure (DefaultProfile missing) – plugins could not be enabled. Please get in touch.",
            ["ProfileStructureUnexpectedReturn"] = "Error: plugins registered, but enabling was not possible (DefaultProfile missing).",
            ["PluginsRegisteredEnabledReturn"] = "Plugins registered and enabled.",
            ["ConfigWriteFailed"] = "dalamudConfig.json could not be written: {0}",
            ["ConfigWriteFailedReturn"] = "Error writing dalamudConfig.json (file locked?).",

            ["DownloadProgress"] = "{0}: {1} % ...",

            ["WindowTitle"] = "FF14 Accessibility – Installer",
            ["MainTitleWithVersion"] = "FF14 Accessibility – Installer and Updater (version {0})",
            ["MainTitleAccessibleName"] = "FF14 Accessibility Installer, version {0}",
            ["LogBoxAccessibleName"] = "Status messages",
            ["LogBoxAccessibleDescription"] = "Progress and result messages from the installer, navigable with arrow keys.",
            ["InstallButtonText"] = "Install / Update",
            ["InstallButtonAccessibleName"] = "Install or update",
            ["ExitButtonText"] = "Exit",
            ["ExitButtonAccessibleName"] = "Exit",
            ["OperationCompleted"] = "Operation completed. See the log area in the window for details.",

            ["LanguageDialogTitle"] = "Sprache wählen / Choose language",
            ["LanguageGermanButton"] = "Deutsch",
            ["LanguageEnglishButton"] = "English",
        },
    };
}
