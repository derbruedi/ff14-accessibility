using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(null, true)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RussificationWaitsForPluginsForFreshAndExistingTranslations(bool? previous, bool reuse)
    {
        var handler = TranslationNetwork();
        Ready();
        if (reuse) MakeMod("1.19.1", Path.Combine(Mods, "XIV Rus"), modern: true);
        var config = Config();
        if (previous.HasValue) config[TranslationInstaller.WaitForPluginsSetting] = previous.Value;
        File.WriteAllText(ConfigPath, config.ToString());
        config = PackageFiles.ReadJson(File.ReadAllText(ConfigPath));
        using var service = Service(handler);

        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        var after = PackageFiles.ReadJson(File.ReadAllText(ConfigPath));
        Assert.True(TranslationInstaller.WaitsForPlugins(after));
        Assert.True(JToken.DeepEquals(config["Unrelated"], after["Unrelated"]));
        if (reuse) Assert.DoesNotContain(PmpUrl, handler.Requests);
        if (previous != true)
        {
            var map = Directory.GetFiles(Mods + "-backup", "restore-map.json", SearchOption.AllDirectories)
                .Select(path => (Path: path, Entries: JArray.Parse(File.ReadAllText(path))))
                .Single(x => x.Entries.Any(e => (string?)e["OriginalPath"] == ConfigPath));
            var entry = map.Entries.Single(e => (string?)e["OriginalPath"] == ConfigPath);
            var backup = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(Path.GetDirectoryName(map.Path)!, (string)entry["BackupFile"]!)));
            Assert.Equal(previous, (bool?)backup[TranslationInstaller.WaitForPluginsSetting]);
        }

        var snapshot = PackageFiles.Snapshot(Root);
        var modSnapshot = PackageFiles.Snapshot(Mods);
        await service.Russify(default, []);
        PackageFiles.VerifySnapshot(Root, snapshot);
        PackageFiles.VerifySnapshot(Mods, modSnapshot);
    }

    [Fact]
    public async Task FailedTranslationDoesNotEnableWaitingOrClaimSuccess()
    {
        var handler = TranslationNetwork();
        Ready();
        var config = Config();
        config[TranslationInstaller.WaitForPluginsSetting] = false;
        File.WriteAllText(ConfigPath, config.ToString());
        handler.Data[PmpUrl] = System.Text.Encoding.UTF8.GetBytes("invalid archive");
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        Assert.False(TranslationInstaller.WaitsForPlugins(PackageFiles.ReadJson(File.ReadAllText(ConfigPath))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckReportsStartupModeWithoutChangingFiles(bool waiting)
    {
        var handler = TranslationNetwork();
        Ready();
        Plugin("Penumbra", "1.7.2.1", Path.Combine(Root, "devPlugins", "Penumbra"));
        var config = Config();
        config[TranslationInstaller.WaitForPluginsSetting] = waiting;
        File.WriteAllText(ConfigPath, config.ToString());
        var snapshot = PackageFiles.Snapshot(Root);
        var messages = new List<string>();
        using var service = Service(handler);
        service.Log = messages.Add;
        await service.Check(default);
        Assert.Contains(messages, m => m.Contains(waiting
            ? "Ожидание загрузки плагинов перед игрой включено."
            : "Ожидание загрузки плагинов перед игрой выключено."));
        PackageFiles.VerifySnapshot(Root, snapshot);
    }

    [Fact]
    public async Task AccessibilityOnlyInstallPreservesStartupPreference()
    {
        var handler = Network();
        using var service = Service(handler);
        await service.Install(default, offerVnav: false);
        Assert.Null(PackageFiles.ReadJson(File.ReadAllText(ConfigPath))[TranslationInstaller.WaitForPluginsSetting]);
    }
}
