namespace FF14AccessibilityInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var args = Environment.GetCommandLineArgs();

        ApplicationConfiguration.Initialize();

        // Phase 2 des Selbst-Updates: aus %TEMP% gestartet, ersetzt die
        // Original-EXE und startet sie neu (siehe SelfUpdate). Läuft ohne GUI
        // und ohne Sprachdialog durch - die Sprache ist bereits gespeichert.
        var applyIndex = Array.IndexOf(args, SelfUpdate.ApplyUpdateArg);
        if (applyIndex >= 0 && applyIndex + 2 < args.Length)
        {
            Loc.Current = Loc.LoadSavedLanguage() ?? DefaultLanguage();
            if (SelfUpdate.ApplyUpdate(args[applyIndex + 1], args[applyIndex + 2]))
                return;   // Original-EXE läuft jetzt - dieser Temp-Prozess ist fertig.

            // Ersetzen fehlgeschlagen: Nutzer wurde informiert, wir zeigen die
            // GUI aus dem temporären Ordner, damit er trotzdem installieren kann.
            Application.Run(new MainForm(justUpdated: true));
            return;
        }

        // Reste früherer Updates aufräumen (je ~160 MB in %TEMP%). Nicht im
        // --apply-update-Zweig, denn dort läuft eine dieser Dateien gerade selbst.
        SelfUpdate.CleanupLeftovers();

        // Frisch aktualisiert gestartet: Sprachdialog überspringen (sonst müsste
        // der Nutzer ihn wegen des Updates ein zweites Mal beantworten) und die
        // Installation direkt weiterlaufen lassen.
        if (Array.IndexOf(args, SelfUpdate.UpdatedArg) >= 0)
        {
            Loc.Current = Loc.LoadSavedLanguage() ?? DefaultLanguage();
            Application.Run(new MainForm(justUpdated: true));
            return;
        }

        var saved = Loc.LoadSavedLanguage();
        var preselect = saved ?? DefaultLanguage();

        using var languageDialog = new LanguageDialog(preselect);
        languageDialog.ShowDialog();
        Loc.Current = languageDialog.SelectedLanguage;
        Loc.SaveLanguage(Loc.Current);

        Application.Run(new MainForm());
    }

    private static string DefaultLanguage()
        => Loc.SystemLanguageIsGerman() ? Loc.German : Loc.English;
}
