using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public static class InstallTransaction
{
    public static void EnsureGameClosed()
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "ffxiv_dx11", "ffxiv", "ffxivboot", "ffxivboot64", "ffxivlauncher", "ffxivlauncher64", "XIVLauncher", "XIVLauncher.Core" };
        foreach (var process in Process.GetProcesses())
        {
            using (process)
                if (blocked.Contains(process.ProcessName))
                    throw new InvalidOperationException("Закройте FFXIV и XIVLauncher, затем повторите установку.");
        }
    }

    public static string? Apply(string root, string name, string target, string? payload,
        byte[] originalConfig, IReadOnlyDictionary<string, string> originalFiles, Action<string> log,
        Action? ensureClosed = null, Action<string>? checkpoint = null)
    {
        ensureClosed ??= EnsureGameClosed;
        ensureClosed();
        target = Path.GetFullPath(target);
        var cfgPath = Path.Combine(root, "dalamudConfig.json");
        PackageFiles.RejectLinks(cfgPath);
        PackageFiles.RejectLinks(target);
        var config = PackageFiles.ReadJson(Encoding.UTF8.GetString(originalConfig));
        var updated = DalamudSettings.Enable(config, name, Path.Combine(target, name + ".dll"));
        var configChanged = !JToken.DeepEquals(config, updated);
        CheckOriginal();
        if (payload == null && !configChanged) return null;

        var backupRoot = target.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, "installer-backups")
            : Path.Combine(Path.GetDirectoryName(target)!, ".FF14Accessibility-backups");
        var backup = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        PackageFiles.RejectLinks(backup);
        Directory.CreateDirectory(backup);
        File.WriteAllBytes(Path.Combine(backup, "dalamudConfig.json"), originalConfig);
        var old = Path.Combine(backup, name);
        // Stage next to the target so the final rename always stays on the same volume.
        var stage = Path.Combine(Path.GetDirectoryName(target)!, ".installer-" + Guid.NewGuid().ToString("N"));
        var saved = false;
        var placed = false;
        var configWritten = false;
        var configTemp = cfgPath + ".installer-" + Guid.NewGuid().ToString("N");
        var nextBytes = new UTF8Encoding(false).GetBytes(updated.ToString());
        try
        {
            if (payload != null)
            {
                if (Directory.Exists(target)) PackageFiles.CopyTree(target, stage);
                PackageFiles.CopyTree(payload, stage);
                PackageFiles.VerifySnapshot(stage, PackageFiles.Snapshot(payload), exact: false);
            }
            if (configChanged) File.WriteAllBytes(configTemp, nextBytes);
            ensureClosed();
            CheckOriginal();
            log("Резервная копия: " + backup);
            if (payload != null)
            {
                if (Directory.Exists(target)) { Directory.Move(target, old); saved = true; }
                Directory.Move(stage, target);
                placed = true;
                PackageFiles.VerifySnapshot(target, PackageFiles.Snapshot(payload), exact: false);
            }
            checkpoint?.Invoke("files");
            if (configChanged)
            {
                if (!File.ReadAllBytes(cfgPath).SequenceEqual(originalConfig))
                    throw new IOException("Настройки изменились во время установки. Повторите после закрытия игры.");
                File.Replace(configTemp, cfgPath, null);
                configWritten = true;
                if (!File.ReadAllBytes(cfgPath).SequenceEqual(nextBytes)) throw new IOException("Настройки не прошли проверку после записи.");
            }
            checkpoint?.Invoke("config");
            if (saved) PackageFiles.VerifySnapshot(old, originalFiles);
            File.WriteAllText(Path.Combine(backup, "README.txt"),
                $"Установка {name} завершена. Прежние файлы: {old}\r\nРабочая папка: {target}\r\n" +
                "dalamudConfig.json содержит прежние настройки. Не восстанавливайте их при запущенной игре.\r\n");
            return backup;
        }
        catch (Exception failure)
        {
            try
            {
                if (configWritten)
                {
                    if (!File.ReadAllBytes(cfgPath).SequenceEqual(nextBytes))
                        throw new IOException("Настройки изменены другой программой; автоматическое восстановление настроек остановлено.");
                    File.WriteAllBytes(configTemp, originalConfig);
                    File.Replace(configTemp, cfgPath, null);
                }
                if (placed) Directory.Move(target, Path.Combine(backup, name + "-неудачная-установка"));
                if (saved) Directory.Move(old, target);
                PackageFiles.VerifySnapshot(target, originalFiles);
                log("Ошибка установки. Прежние файлы восстановлены.");
            }
            catch (Exception rollback)
            {
                throw new IOException($"Установка и восстановление не завершены. Резервная копия: {backup}. " +
                    $"Причина установки: {failure.Message}. Причина восстановления: {rollback.Message}", rollback);
            }
            throw;
        }

        void CheckOriginal()
        {
            if (!File.ReadAllBytes(cfgPath).SequenceEqual(originalConfig))
                throw new IOException("Настройки Dalamud изменились. Повторите установку.");
            PackageFiles.VerifySnapshot(target, originalFiles);
        }
    }
}
