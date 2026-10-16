using System.Text.RegularExpressions;

namespace FF14AccessibilityInstaller.Russian;

public static class JournalText
{
    // The window and its saved journal use the same readable, neutral text.
    public static string Format(string message)
    {
        if (message.StartsWith("Резервная копия: ", StringComparison.Ordinal))
            message = "Резервная копия прежних файлов и настроек сохранена.";
        var folders = new[]
        {
            (Environment.SpecialFolder.LocalApplicationData, "%LOCALAPPDATA%"),
            (Environment.SpecialFolder.ApplicationData, "%APPDATA%"),
            (Environment.SpecialFolder.DesktopDirectory, "%DESKTOP%"),
            (Environment.SpecialFolder.MyDocuments, "%DOCUMENTS%"),
            (Environment.SpecialFolder.UserProfile, "%USERPROFILE%")
        }.Select(f => (Path: Environment.GetFolderPath(f.Item1), Label: f.Item2))
            .Where(f => !string.IsNullOrEmpty(f.Path)).OrderByDescending(f => f.Path.Length);
        foreach (var folder in folders)
            message = Regex.Replace(message, Regex.Escape(folder.Path) + @"(?=[\\/\s\""'.,;:]|$)",
                _ => folder.Label, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        message = Regex.Replace(message, @"\b[A-Z]:[\\/]Users[\\/][^\\/\r\n\""']+(?=[\\/])",
            "%USERPROFILE%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return message.Trim() + Environment.NewLine + Environment.NewLine;
    }
}
