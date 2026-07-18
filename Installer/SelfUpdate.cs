using System.Diagnostics;

namespace FF14AccessibilityInstaller;

/// <summary>
/// Selbst-Update des Installers, Phase 2.
///
/// Windows erlaubt es einer laufenden EXE nicht, sich selbst zu überschreiben.
/// Der Ablauf ist deshalb zweistufig:
///
///   Phase 1 (<see cref="InstallerService.TrySelfUpdateAsync"/>): die laufende
///   Instanz lädt die neue EXE nach %TEMP%, startet sie mit
///   <see cref="ApplyUpdateArg"/>, Zielpfad und eigener Prozess-ID und beendet
///   sich anschließend.
///
///   Phase 2 (hier): die neue Instanz wartet, bis die alte wirklich beendet ist,
///   kopiert sich selbst über die Original-EXE und startet diese mit
///   <see cref="UpdatedArg"/>. Damit liegt die aktualisierte Datei wieder dort,
///   wo der Nutzer sie abgelegt hat (z. B. im Downloads-Ordner) - sonst bliebe
///   dort die alte Version liegen und der große Download käme beim nächsten
///   Start erneut.
///
/// Phase 2 zeigt bewusst keine GUI: sie läuft nur wenige Sekunden. Schlägt das
/// Ersetzen fehl, meldet sie das per MessageBox und arbeitet aus dem
/// temporären Ordner weiter, damit der Nutzer trotzdem installieren kann.
/// </summary>
internal static class SelfUpdate
{
    /// <summary>Phase-2-Schalter: "--apply-update &lt;Zielpfad&gt; &lt;PID der alten Instanz&gt;".</summary>
    public const string ApplyUpdateArg = "--apply-update";

    /// <summary>Markiert eine Instanz, die gerade frisch aktualisiert gestartet wurde.</summary>
    public const string UpdatedArg = "--updated";

    // Die alte Instanz beendet sich unmittelbar nach dem Start von Phase 2.
    // 30 Sekunden sind großzügig; danach machen wir trotzdem weiter, weil ein
    // haengengebliebener Alt-Prozess den Nutzer sonst komplett blockieren würde.
    private const int WaitForOldProcessMs = 30_000;

    // Auch nach Prozessende hält Windows die Datei kurz gesperrt (Virenscanner,
    // verzögerte Handle-Freigabe), daher mehrere Versuche.
    private const int CopyAttempts = 20;
    private const int CopyRetryDelayMs = 500;

    /// <summary>
    /// Führt Phase 2 aus. Gibt true zurück, wenn die aktualisierte Original-EXE
    /// gestartet wurde und sich dieser Prozess beenden soll; false, wenn das
    /// Ersetzen fehlschlug und diese (temporäre) Instanz die GUI zeigen soll.
    /// </summary>
    public static bool ApplyUpdate(string targetPath, string pidArgument)
    {
        WaitForOldInstance(pidArgument);

        var ownPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(ownPath))
        {
            // Ohne eigenen Pfad kann nicht kopiert werden - aus %TEMP% weiterarbeiten.
            ShowWarning(Loc.Get("SelfUpdateNoOwnPath"));
            return false;
        }

        var copyError = TryCopyOverTarget(ownPath, targetPath);
        if (copyError != null)
        {
            ShowWarning(Loc.Get("SelfUpdateReplaceFailed", targetPath, copyError));
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(targetPath)
            {
                Arguments = UpdatedArg,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty,
            });
            return true;
        }
        catch (Exception ex)
        {
            // Ersetzt ist die Datei bereits - der Nutzer muss sie nur neu starten.
            ShowWarning(Loc.Get("SelfUpdateRestartFailed", targetPath, ex.Message));
            return false;
        }
    }

    private static void WaitForOldInstance(string pidArgument)
    {
        if (!int.TryParse(pidArgument, out var pid)) return;

        try
        {
            using var old = Process.GetProcessById(pid);
            old.WaitForExit(WaitForOldProcessMs);
        }
        catch (ArgumentException)
        {
            // Prozess existiert nicht mehr - genau das, worauf wir warten wollten.
        }
        catch (InvalidOperationException)
        {
            // Prozess ist zwischen Abfrage und Warten beendet worden.
        }
    }

    /// <summary>Kopiert die eigene EXE über den Zielpfad. Gibt null bei Erfolg
    /// zurück, sonst die letzte Fehlermeldung.</summary>
    private static string? TryCopyOverTarget(string ownPath, string targetPath)
    {
        string? lastError = null;

        for (var attempt = 0; attempt < CopyAttempts; attempt++)
        {
            try
            {
                File.Copy(ownPath, targetPath, overwrite: true);
                return null;
            }
            catch (IOException ex)
            {
                lastError = ex.Message;          // Datei noch gesperrt - erneut versuchen.
            }
            catch (UnauthorizedAccessException ex)
            {
                // Schreibschutz/fehlende Rechte ändern sich durch Warten nicht.
                return ex.Message;
            }
            Thread.Sleep(CopyRetryDelayMs);
        }

        return lastError ?? "unbekannt";
    }

    private static void ShowWarning(string message)
        => MessageBox.Show(message, Loc.Get("WindowTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>
    /// Löscht heruntergeladene Update-Dateien früherer Läufe aus %TEMP%. Jede ist
    /// rund 160 MB gross (self-contained EXE), die sammeln sich sonst an. Die
    /// gerade laufende Datei wird ausgespart; noch gesperrte Reste verschwinden
    /// beim nächsten Start.
    /// </summary>
    public static void CleanupLeftovers()
    {
        try
        {
            var ownPath = Environment.ProcessPath;
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), DownloadFilePrefix + "*.exe"))
            {
                if (string.Equals(file, ownPath, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); }
                catch (IOException) { /* noch gesperrt - naechstes Mal. */ }
                catch (UnauthorizedAccessException) { /* dito */ }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Aufraeumen ist Kosmetik - niemals den Start blockieren.
        }
    }

    /// <summary>Präfix der Update-Downloads in %TEMP% (siehe InstallerService).</summary>
    public const string DownloadFilePrefix = "FF14AccInstaller_";
}
