namespace FF14AccessibilityInstaller.Russian;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\FF14Accessibility-RU-Installer", out var first);
        if (!first)
        {
            MessageBox.Show("Установщик уже открыт. Переключитесь на его окно.", "FF14 Accessibility", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { Application.Run(new MainForm()); }
        finally { mutex.ReleaseMutex(); }
    }
}
