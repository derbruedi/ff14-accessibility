namespace FF14AccessibilityInstaller;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var saved = Loc.LoadSavedLanguage();
        var preselect = saved ?? (Loc.SystemLanguageIsGerman() ? Loc.German : Loc.English);

        using var languageDialog = new LanguageDialog(preselect);
        languageDialog.ShowDialog();
        Loc.Current = languageDialog.SelectedLanguage;
        Loc.SaveLanguage(Loc.Current);

        Application.Run(new MainForm());
    }
}
