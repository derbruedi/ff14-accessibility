using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests
{
    [Theory]
    [InlineData("1.7.1.0")]
    [InlineData("1.8.0.0")]
    public async Task UnknownPenumbraFirstSetupDoesNotWriteGuessedSettings(string version)
    {
        var handler = TranslationNetwork();
        Plugin("Penumbra", version, Path.Combine(Root, "devPlugins", "Penumbra"));
        using var service = Service(handler);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        Assert.Contains("Автоматическое создание настроек", error.Message);
        Assert.False(Directory.Exists(PenConfig));
        Assert.DoesNotContain(PmpUrl, handler.Requests);
    }

    [Theory]
    [InlineData("case")]
    [InlineData("slashes")]
    [InlineData("directory")]
    public async Task ExistingDisabledDevPenumbraGetsExactStartupKeyAndMatchingProfiles(string spelling)
    {
        var handler = TranslationNetwork();
        var folder = Plugin("Penumbra", "1.7.2.1", Path.Combine(Root, "devPlugins", "Penumbra"));
        var dll = Path.Combine(folder, "Penumbra.dll");
        var alias = spelling == "slashes" ? dll.Replace('\\', '/') : dll.ToLowerInvariant();
        var config = Config();
        config["DevPluginSettings"]![alias] = new JObject
        { ["WorkingPluginId"] = CollectionId, ["StartOnBoot"] = false, ["NotifyForErrors"] = false };
        DalamudSettings.Array(config["DevPluginLoadLocations"], "test").Add(new JObject
        { ["Path"] = spelling == "directory" ? folder.ToLowerInvariant() : alias, ["IsEnabled"] = false });
        DalamudSettings.Array(config["DefaultProfile"]!["Plugins"], "test").Add(new JObject
        { ["InternalName"] = "Penumbra", ["WorkingPluginId"] = Guid.NewGuid().ToString(), ["IsEnabled"] = false });
        File.WriteAllText(ConfigPath, config.ToString());
        using var service = Service(handler);
        await service.Russify(default, []);
        var updated = PackageFiles.ReadJson(File.ReadAllText(ConfigPath));
        // The consumer is case-sensitive, unlike Windows file existence checks.
        var entries = (JObject)updated["DevPluginSettings"]!;
        Assert.NotNull(entries.Property(dll, StringComparison.Ordinal));
        Assert.Null(entries.Property(alias, StringComparison.Ordinal));
        Assert.Equal(CollectionId, (string?)entries[dll]!["WorkingPluginId"]);
        Assert.True((bool)entries[dll]!["StartOnBoot"]!);
        Assert.False((bool)entries[dll]!["NotifyForErrors"]!);
        foreach (var profile in new[] { updated["DefaultProfile"]!, updated["SavedProfiles"]!["$values"]![0]! })
        {
            var entry = DalamudSettings.Array(profile["Plugins"], "test").Single(p => (string?)p["InternalName"] == "Penumbra");
            Assert.Equal(CollectionId, (string?)entry["WorkingPluginId"]);
            Assert.True((bool)entry["IsEnabled"]!);
        }
        Assert.DoesNotContain(PenZipUrl, handler.Requests);
        var snapshot = PackageFiles.Snapshot(Root);
        await service.Russify(default, []);
        PackageFiles.VerifySnapshot(Root, snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogPenumbraRecoversIdentityAndEnablesSourceRepository(bool existingRepository)
    {
        var handler = TranslationNetwork();
        var folder = Plugin("Penumbra", "1.7.2.1", Path.Combine(Root, "installedPlugins", "Penumbra", "1.7.2.1"));
        var manifestPath = Path.Combine(folder, "Penumbra.json");
        var manifest = PackageFiles.ReadJson(File.ReadAllText(manifestPath));
        manifest["Disabled"] = true;
        manifest["ScheduledForDeletion"] = true;
        manifest["InstalledFromUrl"] = ReleaseClient.PenumbraUrl;
        File.WriteAllText(manifestPath, manifest.ToString());
        var config = Config();
        DalamudSettings.Array(config["DefaultProfile"]!["Plugins"], "test").Add(new JObject
        { ["InternalName"] = "Penumbra", ["WorkingPluginId"] = CollectionId, ["IsEnabled"] = false });
        config["ThirdRepoList"] = new JObject { ["$values"] = existingRepository
            ? new JArray(new JObject { ["Url"] = ReleaseClient.PenumbraUrl, ["IsEnabled"] = false }) : new JArray() };
        File.WriteAllText(ConfigPath, config.ToString());
        using var service = Service(handler);
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        var after = PackageFiles.ReadJson(File.ReadAllText(manifestPath));
        Assert.Equal(CollectionId, (string?)after["WorkingPluginId"]);
        Assert.False((bool)after["Disabled"]!);
        Assert.False((bool)after["ScheduledForDeletion"]!);
        var repos = DalamudSettings.Array(PackageFiles.ReadJson(File.ReadAllText(ConfigPath))["ThirdRepoList"], "test");
        Assert.Single(repos);
        Assert.True((bool)repos[0]["IsEnabled"]!);
        Assert.False(Directory.Exists(Path.Combine(Root, "devPlugins", "Penumbra")));
    }

    [Theory]
    [InlineData("config")]
    [InlineData("active")]
    [InlineData("collections")]
    public async Task MissingFirstBootFilesArePreparedWithoutDiscardingOtherSettings(string missing)
    {
        var handler = TranslationNetwork();
        Ready();
        var activePath = Path.Combine(PenConfig, "active_collections.json");
        var active = PackageFiles.ReadJson(File.ReadAllText(activePath));
        active["Individuals"] = new JArray(new JObject { ["PlayerName"] = "Fixture", ["Collection"] = CollectionId });
        File.WriteAllText(activePath, active.ToString());
        if (missing == "config") File.Delete(Path.Combine(PenConfig, "config", "penumbra.json"));
        else if (missing == "active") File.Delete(activePath);
        else
        {
            File.Delete(Path.Combine(PenConfig, "collections", CollectionId + ".json"));
            File.Delete(activePath); // A fresh empty setup has no dangling assignments.
        }
        using var service = Service(handler);
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        if (missing == "config")
            Assert.True(JToken.DeepEquals(active["Individuals"], PackageFiles.ReadJson(File.ReadAllText(activePath))["Individuals"]));
        var collections = Directory.GetFiles(Path.Combine(PenConfig, "collections"), "*.json");
        Assert.Single(collections);
        Assert.True((bool)PackageFiles.ReadJson(File.ReadAllText(collections[0]))["Settings"]!["XIV Rus"]!["Enabled"]!);
    }

    [Fact]
    public async Task LegacyMigrationCannotUndoEnabledTranslationOrLoseThePreviousModDirectory()
    {
        var handler = TranslationNetwork();
        Directory.CreateDirectory(Path.Combine(Root, "pluginConfigs"));
        var legacyPath = Path.Combine(Root, "pluginConfigs", "Penumbra.json");
        File.WriteAllText(legacyPath, new JObject
        { ["Version"] = 15, ["ModDirectory"] = Mods, ["EnableMods"] = false, ["UserSetting"] = "keep" }.ToString());
        using var service = Service(handler);
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        var legacy = PackageFiles.ReadJson(File.ReadAllText(legacyPath));
        var modern = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(PenConfig, "config", "penumbra.json")));
        Assert.Equal("keep", (string?)legacy["UserSetting"]);
        Assert.True((bool)legacy["EnableMods"]!);
        Assert.Equal((string?)legacy["ModDirectory"], (string?)modern["ModDirectory"]);
        Assert.True(File.Exists(Path.Combine(Mods, "XIV Rus", "text.exd")));
    }

    [Fact]
    public async Task MissingReferencedCollectionReportsDamageInsteadOfRequestingAnotherLogin()
    {
        var handler = TranslationNetwork();
        Ready();
        File.Delete(Path.Combine(PenConfig, "collections", CollectionId + ".json"));
        var before = PackageFiles.Snapshot(PenConfig);
        using var service = Service(handler);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        Assert.Contains("коллекция", error.Message);
        Assert.DoesNotContain(PmpUrl, handler.Requests);
        PackageFiles.VerifySnapshot(PenConfig, before);
    }

    [Fact]
    public async Task SafeModeCannotBeReportedAsSuccessfulRussification()
    {
        var handler = TranslationNetwork();
        var config = Config();
        config["PluginSafeMode"] = true;
        File.WriteAllText(ConfigPath, config.ToString());
        using var service = Service(handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Russify(default, []));
        Assert.Contains("без плагинов", error.Message);
        Assert.DoesNotContain(PmpUrl, handler.Requests);
        Assert.True((bool)PackageFiles.ReadJson(File.ReadAllText(ConfigPath))["PluginSafeMode"]!);
    }

    [Fact]
    public async Task InstalledDalamudApiMismatchCannotBeReportedAsEnabled()
    {
        var handler = TranslationNetwork();
        Plugin("Dalamud", "16.0.0.0", Path.Combine(Root, "addon", "Hooks", "dev"));
        using var service = Service(handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Russify(default, []));
        Assert.Contains("а установлена 16", error.Message);
        Assert.DoesNotContain(PmpUrl, handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogIdentityCanBeRecoveredFromSavedProfileOrCreatedForFirstInstallation(bool savedProfile)
    {
        var handler = TranslationNetwork();
        var folder = Plugin("Penumbra", "1.7.2.1", Path.Combine(Root, "installedPlugins", "Penumbra", "1.7.2.1"));
        if (savedProfile)
        {
            var config = Config();
            DalamudSettings.Array(config["SavedProfiles"]!["$values"]![0]!["Plugins"], "test").Add(new JObject
            { ["InternalName"] = "Penumbra", ["WorkingPluginId"] = CollectionId, ["IsEnabled"] = false });
            File.WriteAllText(ConfigPath, config.ToString());
        }
        using var service = Service(handler);
        await service.Russify(default, []);
        var manifest = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(folder, "Penumbra.json")));
        var id = (Guid)manifest["WorkingPluginId"]!;
        Assert.NotEqual(Guid.Empty, id);
        if (savedProfile) Assert.Equal(Guid.Parse(CollectionId), id);
        DalamudSettings.VerifyEnabled(Root, "Penumbra", new InstalledPlugin(folder, new Version(1, 7, 2, 1), true));
    }

    [Fact]
    public void FailureWhileCreatingFirstSettingsRemovesNewFilesAndRestoresModAbsence()
    {
        var config = Path.Combine(PenConfig, "config", "penumbra.json");
        var active = Path.Combine(PenConfig, "active_collections.json");
        var changes = new[] { new JsonChange(config, new JObject { ["Version"] = 100 }),
            new JsonChange(active, new JObject { ["Version"] = 2 }) };
        var staged = MakeMod("1.19.1");
        var target = Path.Combine(Mods, "XIV Rus");
        Assert.Throws<IOException>(() => SettingsTransaction.Apply(Mods + "-backup", changes, () => { }, _ => { },
            staged, target, PackageFiles.Snapshot(target), point => { if (point == active) throw new IOException("injected"); }));
        Assert.False(File.Exists(config));
        Assert.False(File.Exists(active));
        Assert.False(Directory.Exists(target));
    }
}
