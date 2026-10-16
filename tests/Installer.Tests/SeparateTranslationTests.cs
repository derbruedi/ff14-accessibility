using System.Text;
using FF14AccessibilityInstaller.Russian;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests
{
    [Fact]
    public async Task GameTranslationInstallsWithoutAccessibilityOrItsReleaseServer()
    {
        var handler = TranslationNetwork();
        handler.Data.Remove(ReleaseClient.LatestUrl);
        handler.Data.Remove(ZipUrl);
        using var service = Service(handler);
        Assert.Contains("перевод игры", await service.RussifyGame(default, []));
        Assert.False(Directory.Exists(Target));
        Assert.False(File.Exists(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json")));
        Assert.DoesNotContain(ReleaseClient.LatestUrl, handler.Requests);
        Assert.DoesNotContain(ZipUrl, handler.Requests);
        Assert.DoesNotContain(VnavZip, handler.Requests);
        Assert.True(File.Exists(Path.Combine(Root, "PenumbraMods", "XIV Rus", "text.exd")));
        var before = PackageFiles.Snapshot(Root);
        await service.RussifyGame(default, []);
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public async Task GameTranslationPreservesAccessibilitySettingsEvenWhenTheyAreUnreadable()
    {
        Ready();
        var path = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json");
        File.WriteAllText(path, "unreadable accessibility settings");
        var before = File.ReadAllBytes(path);
        using var service = Service(TranslationNetwork());
        await service.RussifyGame(default, []);
        Assert.Equal(before, File.ReadAllBytes(path));
        service.Uninstall(false, true, default);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task ModTranslationInstallsWithoutPenumbraOrXivRusAndKeepsGameTranslation()
    {
        Ready();
        MakeMod("1.19.1", Path.Combine(Mods, "XIV Rus"));
        var penumbra = PackageFiles.Snapshot(PenConfig);
        var mods = PackageFiles.Snapshot(Mods);
        File.WriteAllText(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json"),
            "{\"Language\":2,\"KeyHelp\":\"Alt+H\",\"Volume\":0.3,\"TranslateItemsAndActions\":false}");
        var handler = Network(); // This server has no Penumbra or XIV Rus responses.
        using var service = Service(handler);
        Assert.Contains("Русификация мода установлена", await service.RussifyMod(default));
        var settings = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json")));
        Assert.Equal(3, (int)settings["Language"]!);
        Assert.Equal("Alt+H", (string?)settings["KeyHelp"]);
        Assert.Equal(0.3, (double)settings["Volume"]!);
        Assert.False((bool)settings["TranslateItemsAndActions"]!);
        Assert.DoesNotContain(ReleaseClient.PenumbraUrl, handler.Requests);
        Assert.DoesNotContain(ReleaseClient.TranslationUrl, handler.Requests);
        Assert.DoesNotContain(ReleaseClient.VnavUrl, handler.Requests);
        PackageFiles.VerifySnapshot(PenConfig, penumbra);
        PackageFiles.VerifySnapshot(Mods, mods);
        var before = PackageFiles.Snapshot(Root);
        await service.RussifyMod(default);
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(null)]
    public async Task RemovingModTranslationSetsExplicitEnglishAndPreservesOtherData(int? language)
    {
        Ready();
        MakeMod("1.19.1", Path.Combine(Mods, "XIV Rus"));
        var handler = Network();
        using var service = Service(handler);
        await service.Install(default, false);
        var configFolder = Path.Combine(Root, "pluginConfigs");
        Directory.CreateDirectory(Path.Combine(configFolder, "FF14Accessibility", "DungeonPaths"));
        File.WriteAllText(Path.Combine(configFolder, "FF14Accessibility", "DungeonPaths", "route.json"), "{}");
        var path = Path.Combine(configFolder, "FF14Accessibility.json");
        var settings = new JObject { ["KeyHelp"] = "Alt+H", ["Volume"] = 0.3 };
        if (language != null) settings["Language"] = language;
        File.WriteAllText(path, settings.ToString());
        File.WriteAllText(path + ".bak", settings.ToString());
        var mods = PackageFiles.Snapshot(Mods);
        var pen = PackageFiles.Snapshot(PenConfig);
        var plugin = PackageFiles.Snapshot(Target);
        var routes = PackageFiles.Snapshot(Path.Combine(configFolder, "FF14Accessibility"));
        var dalamud = File.ReadAllBytes(ConfigPath);
        var requests = handler.Requests.Count;
        Assert.Contains("язык сообщений и речи переключён на английский", service.RemoveModTranslation(default));
        Assert.Equal(requests, handler.Requests.Count);
        foreach (var file in new[] { path, path + ".bak" })
        {
            var result = PackageFiles.ReadJson(File.ReadAllText(file));
            Assert.Equal(2, (int)result["Language"]!);
            result.Remove("Language");
            settings.Remove("Language");
            Assert.True(JToken.DeepEquals(settings, result));
        }
        Assert.Equal(dalamud, File.ReadAllBytes(ConfigPath));
        PackageFiles.VerifySnapshot(Mods, mods);
        PackageFiles.VerifySnapshot(PenConfig, pen);
        PackageFiles.VerifySnapshot(Target, plugin);
        PackageFiles.VerifySnapshot(Path.Combine(configFolder, "FF14Accessibility"), routes);
        var before = PackageFiles.Snapshot(Root);
        service.RemoveModTranslation(default);
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public void RemovingModTranslationWithoutSettingsPreventsRussianWindowsAutoLanguage()
    {
        Remover().Remove(false, false, default, modTranslation: true);
        var result = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json")));
        Assert.Equal(2, (int)result["Language"]!);
        Assert.Equal(2, (int)result["Version"]!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageRemovalRollsBackCreatedOrExistingSettingsOnFailure(bool exists)
    {
        var path = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json");
        if (exists)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{\"Language\":3,\"keys\":true}");
        }
        var before = exists ? File.ReadAllBytes(path) : null;
        Assert.Throws<IOException>(() => Remover().Remove(false, false, default,
            stage => { if (stage == "written") throw new IOException("simulated write failure"); }, modTranslation: true));
        if (exists) Assert.Equal(before, File.ReadAllBytes(path));
        else Assert.False(File.Exists(path));
    }

    [Fact]
    public void LanguageCreationRefusesConcurrentSettingsWithoutOverwritingThem()
    {
        var path = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json");
        var calls = 0;
        Assert.Throws<IOException>(() => Remover(() =>
        {
            if (++calls == 2)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "{\"Language\":3,\"newKeys\":true}");
            }
        }).Remove(false, false, default, modTranslation: true));
        Assert.Contains("newKeys", File.ReadAllText(path));
    }

    [Fact]
    public void GameLanguageRemovalPreservesModLanguageInDalamudCache()
    {
        Ready();
        var path = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json");
        var settings = "{\"Language\":3,\"KeyHelp\":\"Alt+H\"}";
        var database = Path.Combine(Root, "dalamudVfs.db");
        using (var connection = new SqliteConnection("Data Source=" + database + ";Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE DbFile (Id INTEGER PRIMARY KEY, Path TEXT, Data BLOB); INSERT INTO DbFile (Path, Data) VALUES ($p, $d)";
            command.Parameters.AddWithValue("$p", path);
            command.Parameters.AddWithValue("$d", Encoding.UTF8.GetBytes(settings));
            command.ExecuteNonQuery();
        }
        Remover().Remove(false, true, default);
        Assert.Equal(3, CachedLanguage());
        Remover().Remove(false, false, default, modTranslation: true);
        Assert.Equal(2, CachedLanguage());
        var restored = PackageFiles.ReadJson(File.ReadAllText(path));
        Assert.Equal("Alt+H", (string?)restored["KeyHelp"]);
        using var releases = new ReleaseClient(Network());
        new TranslationInstaller(Root, releases, () => { }, _ => { }).SetRussianLanguage();
        Assert.Equal(3, CachedLanguage());
        Assert.Equal("Alt+H", (string?)PackageFiles.ReadJson(File.ReadAllText(path))["KeyHelp"]);

        int CachedLanguage()
        {
            using var connection = new SqliteConnection("Data Source=" + database + ";Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Data FROM DbFile";
            var value = PackageFiles.ReadJson(Encoding.UTF8.GetString((byte[])command.ExecuteScalar()!));
            Assert.Equal("Alt+H", (string?)value["KeyHelp"]);
            return (int)value["Language"]!;
        }
    }

    [Fact]
    public void ModLanguageRemovalHonorsCancellationAndRunningGameBeforeAnyWrites()
    {
        using var token = new CancellationTokenSource();
        token.Cancel();
        var before = PackageFiles.Snapshot(Root);
        Assert.Throws<OperationCanceledException>(() => Remover().Remove(false, false, token.Token, modTranslation: true));
        Assert.Throws<InvalidOperationException>(() => Remover(() => throw new InvalidOperationException("game running"))
            .Remove(false, false, default, modTranslation: true));
        PackageFiles.VerifySnapshot(Root, before);
    }
}

public sealed class JournalTextTests
{
    [Fact]
    public void BackupMessagesDoNotExposeTimestampedFolderNames() =>
        Assert.Equal("Резервная копия прежних файлов и настроек сохранена." + Environment.NewLine + Environment.NewLine,
            JournalText.Format("Резервная копия: C:\\Users\\Secret\\installer-backups\\20261006-110011-00000000000000000000000000000001"));

    [Fact]
    public void JournalHasParagraphsWithoutAddedTimeOrPersonalPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "dalamudConfig.json");
        Assert.Equal("Настройки: %APPDATA%\\XIVLauncher\\dalamudConfig.json" + Environment.NewLine + Environment.NewLine,
            JournalText.Format("Настройки: " + path));
        Assert.Equal("Проверка завершена." + Environment.NewLine + Environment.NewLine, JournalText.Format(" Проверка завершена. "));
    }

    [Theory]
    [InlineData("Ошибка: C:\\Users\\Private Name\\Downloads\\archive.pmp", "Ошибка: %USERPROFILE%\\Downloads\\archive.pmp")]
    [InlineData("Файл 'C:/Users/HiddenUser/Mods/XIV Rus/meta.json'", "Файл '%USERPROFILE%/Mods/XIV Rus/meta.json'")]
    [InlineData("https://github.com/pitpamyati-netizen/ff14-accessibility/releases/latest", "https://github.com/pitpamyati-netizen/ff14-accessibility/releases/latest")]
    [InlineData("Версия: 1.3.0. Ошибка: контрольная сумма не совпала.", "Версия: 1.3.0. Ошибка: контрольная сумма не совпала.")]
    public void PersonalPathsAreHiddenButUsefulMessagesAndUrlsRemain(string input, string expected) =>
        Assert.Equal(expected + Environment.NewLine + Environment.NewLine, JournalText.Format(input));
}
