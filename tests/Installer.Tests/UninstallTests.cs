using System.IO.Compression;
using System.Text;
using FF14AccessibilityInstaller.Russian;
using LiteDB;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests
{
    private UninstallService Remover(Action? closed = null) => new(Root, closed ?? (() => { }), _ => { });

    [Fact]
    public void PreviousModLocationAndRenamedKeyFromBackupAreAlsoPurged()
    {
        Ready();
        var oldRoot = Path.Combine(sandbox, "previous-mods");
        var oldTarget = MakeMod("1.18.0", Path.Combine(oldRoot, "Old Russian"));
        var backup = Path.Combine(sandbox, "mods-backup", "old");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "README.txt"), "Папка перевода: " + oldTarget + "\r\n");
        var collection = Path.Combine(PenConfig, "collections", CollectionId + ".json");
        var settings = PackageFiles.ReadJson(File.ReadAllText(collection));
        settings["Settings"]!["Old Russian"] = new JObject { ["Enabled"] = true };
        File.WriteAllText(collection, settings.ToString());
        Remover().Remove(false, true, default);
        Assert.False(Directory.Exists(oldTarget));
        Assert.DoesNotContain("Old Russian", File.ReadAllText(collection));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../outside.json")]
    [InlineData("C:\\outside.json")]
    public void BackupMapCannotEscapeOwnedBackupFolder(string malicious)
    {
        var folder = Path.Combine(Root, "installer-backups", "bad");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "restore-map.json"), new JArray(new JObject
        {
            ["OriginalPath"] = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json"),
            ["BackupFile"] = malicious, ["Existed"] = true
        }).ToString());
        var before = PackageFiles.Snapshot(sandbox);
        Assert.Throws<InvalidDataException>(() => Remover().Remove(true, false, default));
        PackageFiles.VerifySnapshot(sandbox, before);
    }

    [Fact]
    public void PlainProfilesAndStaleOnlyRegistrationArePurged()
    {
        var config = new JObject
        {
            ["DevPluginLoadLocations"] = new JArray(new JObject { ["Path"] = Path.Combine(Target, "FF14Accessibility.dll"), ["IsEnabled"] = true }),
            ["DevPluginSettings"] = new JObject(),
            ["DefaultProfile"] = new JObject { ["Plugins"] = new JArray(new JObject { ["InternalName"] = "FF14Accessibility" }) },
            ["SavedProfiles"] = new JArray(new JObject { ["Plugins"] = new JArray(new JObject { ["InternalName"] = "FF14Accessibility" }) })
        };
        File.WriteAllText(ConfigPath, config.ToString());
        Remover().Remove(true, false, default);
        Assert.DoesNotContain("FF14Accessibility", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void ArrayFilesystemRecordsAndIncompleteNamedTranslationAreRemoved()
    {
        Ready();
        Directory.CreateDirectory(Path.Combine(Mods, "XIV Rus"));
        File.WriteAllText(Path.Combine(Mods, "XIV Rus", "broken.txt"), "missing metadata");
        var selection = Path.Combine(PenConfig, "selected_nodes.json");
        File.WriteAllText(selection, "[\"XIV Rus\",\"Other\"]");
        Remover().Remove(false, true, default);
        Assert.Equal("Other", JArray.Parse(File.ReadAllText(selection)).Single().ToString());
        Assert.False(Directory.Exists(Path.Combine(Mods, "XIV Rus")));
    }

    [Fact]
    public void UnrelatedInstallerBackupIsPreservedByteForByte()
    {
        var folder = Path.Combine(Root, "installer-backups", "other");
        Directory.CreateDirectory(folder);
        Plugin("Penumbra", "1.7.2.1", Path.Combine(folder, "Penumbra"));
        File.WriteAllText(Path.Combine(folder, "README.txt"), "Установка Penumbra завершена.");
        File.WriteAllText(Path.Combine(folder, "dalamudConfig.json"), Config().ToString());
        var before = PackageFiles.Snapshot(folder);
        Remover().Remove(true, false, default);
        PackageFiles.VerifySnapshot(folder, before);
    }

    [Fact]
    public void MixedPluginDirectoryStopsWithoutDeletingOtherPlugin()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        Plugin("OtherPlugin", "1.0", Target);
        var before = PackageFiles.Snapshot(Root);
        Assert.Throws<IOException>(() => Remover().Remove(true, false, default));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public void CorruptCollectionStopsBeforeRemovingTranslationFiles()
    {
        Ready();
        MakeMod("1.19.1", Path.Combine(Mods, "XIV Rus"));
        File.WriteAllText(Path.Combine(PenConfig, "collections", "broken.json"), "broken");
        var before = PackageFiles.Snapshot(sandbox);
        Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => Remover().Remove(false, true, default));
        PackageFiles.VerifySnapshot(sandbox, before);
    }

    [Fact]
    public void ConcurrentSettingsChangeStopsAndKeepsPlugin()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        File.WriteAllText(ConfigPath, DalamudSettings.Enable(Config(), "FF14Accessibility", Path.Combine(Target, "FF14Accessibility.dll")).ToString());
        var calls = 0;
        Assert.Throws<IOException>(() => Remover(() =>
        {
            if (++calls == 2) File.WriteAllText(ConfigPath, Config().ToString());
        }).Remove(true, false, default));
        Assert.True(File.Exists(Path.Combine(Target, "FF14Accessibility.dll")));
        Assert.Equal(Config().ToString(), File.ReadAllText(ConfigPath));
    }

    [Fact]
    public async Task FullModRemovalPurgesEveryVersionSettingsProfilesAndBackupsWithoutNetwork()
    {
        var handler = Network();
        using var service = Service(handler);
        await service.Install(default, false);
        Plugin("FF14Accessibility", "6.8.70.0", Path.Combine(Root, "installedPlugins", "FF14Accessibility", "6.8.70"));
        var configs = Path.Combine(Root, "pluginConfigs");
        Directory.CreateDirectory(Path.Combine(configs, "FF14Accessibility", "DungeonPaths"));
        File.WriteAllText(Path.Combine(configs, "FF14Accessibility", "DungeonPaths", "route.json"), "{}");
        File.WriteAllText(Path.Combine(configs, "FF14Accessibility.json"), "{\"Language\":3,\"keys\":true}");
        File.WriteAllText(Path.Combine(configs, "FF14Accessibility.json.bak-lang-ru"), "old keys");
        File.WriteAllText(Path.Combine(configs, "Other.json"), "other settings");
        File.WriteAllText(ConfigPath + ".bak-installer", File.ReadAllText(ConfigPath));
        var before = PackageFiles.ReadJson(File.ReadAllText(ConfigPath))["Unrelated"]!.ToString();
        var requests = handler.Requests.Count;
        Assert.Contains("полностью удалён", service.Uninstall(true, false, default));
        Assert.Equal(requests, handler.Requests.Count);
        Assert.False(Directory.Exists(Target));
        Assert.False(Directory.Exists(Path.Combine(Root, "installedPlugins", "FF14Accessibility")));
        Assert.Single(Directory.GetFiles(configs), p => Path.GetFileName(p) == "Other.json");
        Assert.False(Directory.Exists(Path.Combine(configs, "FF14Accessibility")));
        foreach (var configFile in new[] { ConfigPath, ConfigPath + ".bak-installer" })
        {
            var result = PackageFiles.ReadJson(File.ReadAllText(configFile));
            Assert.DoesNotContain("FF14Accessibility", result.ToString());
            Assert.Equal(before, result["Unrelated"]!.ToString());
            Assert.Single(DalamudSettings.Array(result["DefaultProfile"]!["Plugins"], "plugins"));
        }
        Assert.Contains("полностью удалён", service.Uninstall(true, false, default));
    }

    [Fact]
    public async Task TranslationRemovalCleansDuplicateRenamedModsCacheCollectionsAndKeepsOtherMods()
    {
        var handler = TranslationNetwork();
        Ready();
        using var service = Service(handler);
        await service.Russify(default, []);
        MakeMod("1.18.0", Path.Combine(Mods, "Renamed Russian"));
        var other = Path.Combine(Mods, "Other");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "meta.json"), "{\"Name\":\"Other mod\"}");
        var otherSnapshot = PackageFiles.Snapshot(other);
        var collection = Path.Combine(PenConfig, "collections", CollectionId + ".json");
        var json = PackageFiles.ReadJson(File.ReadAllText(collection));
        json["Settings"]!["Renamed Russian"] = new JObject { ["Enabled"] = true };
        File.WriteAllText(collection, json.ToString());
        var accBytes = File.ReadAllBytes(Path.Combine(Target, "FF14Accessibility.dll"));
        Assert.Contains("полностью удалена", service.Uninstall(false, true, default));
        Assert.Single(Directory.GetDirectories(Mods));
        PackageFiles.VerifySnapshot(other, otherSnapshot);
        var result = PackageFiles.ReadJson(File.ReadAllText(collection));
        Assert.Single(((JObject)result["Settings"]!).Properties());
        Assert.NotNull(result["Settings"]!["Other"]);
        Assert.False(Directory.Exists(Path.Combine(Root, "installer-cache")));
        Assert.Equal(3, (int)PackageFiles.ReadJson(File.ReadAllText(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json")))["Language"]!);
        Assert.Equal(accBytes, File.ReadAllBytes(Path.Combine(Target, "FF14Accessibility.dll")));
        Assert.Contains("полностью удалена", service.Uninstall(false, true, default));
    }

    [Fact]
    public async Task InstallBothThenRemoveBothThenInstallAgainWorks()
    {
        var handler = TranslationNetwork();
        using var service = Service(handler);
        await service.Russify(default, []);
        Assert.Contains("полностью удалены", service.Uninstall(true, true, default));
        Assert.False(Directory.Exists(Target));
        Assert.False(Directory.Exists(Path.Combine(Root, "PenumbraMods")));
        Assert.DoesNotContain("XIV Rus", string.Join("\n", Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText)));
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        Assert.True(File.Exists(Path.Combine(Target, "FF14Accessibility.dll")));
    }

    [Fact]
    public void MissingDalamudConfigStillAllowsCleaningBrokenInstallationOffline()
    {
        File.Delete(ConfigPath);
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "broken.dll"), "broken");
        Assert.Contains("полностью удалён", Remover().Remove(true, false, default));
        Assert.False(Directory.Exists(Target));
    }

    [Fact]
    public void CancellationBeforeMutationPreservesInstallation()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        var before = PackageFiles.Snapshot(Root);
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.Throws<OperationCanceledException>(() => Remover().Remove(true, true, token.Token));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public void RunningGameBlocksRemovalBeforeAnyWrites()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        var before = PackageFiles.Snapshot(Root);
        Assert.Throws<InvalidOperationException>(() => Remover(() => throw new InvalidOperationException("game running")).Remove(true, false, default));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Theory]
    [InlineData("moved")]
    [InlineData("written")]
    public void WriteFailureRestoresExactOriginalFilesAndSettings(string failure)
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        File.WriteAllText(ConfigPath, DalamudSettings.Enable(Config(), "FF14Accessibility", Path.Combine(Target, "FF14Accessibility.dll")).ToString());
        var before = PackageFiles.Snapshot(Root);
        Assert.Throws<IOException>(() => Remover().Remove(true, false, default, stage =>
        { if (stage == failure) throw new IOException("injected failure"); }));
        PackageFiles.VerifySnapshot(Root, before);
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(Target)!, "*.removing-*"));
    }

    [Fact]
    public void LockedFileDoesNotDeleteAnythingOrReportSuccess()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        var before = File.ReadAllBytes(ConfigPath);
        using var handle = new FileStream(Path.Combine(Target, "FF14Accessibility.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<IOException>(() => Remover().Remove(true, false, default));
        Assert.True(Directory.Exists(Target));
        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public void ExternalDedicatedCopyAndMissingDllRegistrationAreBothRemoved()
    {
        var external = Plugin("FF14Accessibility", "6.8.73.0", Path.Combine(sandbox, "external", "FF14Accessibility"));
        var config = DalamudSettings.Enable(Config(), "FF14Accessibility", Path.Combine(external, "FF14Accessibility.dll"));
        File.Delete(Path.Combine(external, "FF14Accessibility.dll"));
        File.WriteAllText(ConfigPath, config.ToString());
        Remover().Remove(true, false, default);
        Assert.False(Directory.Exists(external));
        Assert.DoesNotContain("FF14Accessibility", File.ReadAllText(ConfigPath));
    }

    [Fact]
    public void ProjectDirectoryCannotBeDeletedThroughDevRegistration()
    {
        var external = Plugin("FF14Accessibility", "6.8.73.0", Path.Combine(sandbox, "project"));
        File.WriteAllText(Path.Combine(external, "project.csproj"), "project");
        File.WriteAllText(ConfigPath, DalamudSettings.Enable(Config(), "FF14Accessibility", Path.Combine(external, "FF14Accessibility.dll")).ToString());
        var before = PackageFiles.Snapshot(sandbox);
        Assert.Throws<IOException>(() => Remover().Remove(true, false, default));
        PackageFiles.VerifySnapshot(sandbox, before);
    }

    [Fact]
    public void SqliteShadowCopiesArePurgedAndOtherPluginRowsPreserved()
    {
        Plugin("FF14Accessibility", "6.8.73.0", Target);
        var config = DalamudSettings.Enable(Config(), "FF14Accessibility", Path.Combine(Target, "FF14Accessibility.dll"));
        File.WriteAllText(ConfigPath, config.ToString());
        var path = Path.Combine(Root, "dalamudVfs.db");
        using (var database = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
        {
            database.Open();
            using var command = database.CreateCommand();
            command.CommandText = "CREATE TABLE DbFile (Id INTEGER PRIMARY KEY, ContainerId TEXT, Path TEXT, Data BLOB)";
            command.ExecuteNonQuery();
            foreach (var record in new[] { (ConfigPath, config.ToString()), (Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json"), "{\"keys\":true}"),
                (Path.Combine(Root, "pluginConfigs", "Other.json"), "{\"Other\":true}") })
            {
                command.CommandText = "INSERT INTO DbFile (Path,Data) VALUES ($p,$d)";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$p", record.Item1);
                command.Parameters.AddWithValue("$d", Encoding.UTF8.GetBytes(record.Item2));
                command.ExecuteNonQuery();
            }
        }
        Remover().Remove(true, false, default);
        using var read = new SqliteConnection("Data Source=" + path + ";Pooling=False");
        read.Open();
        using var select = read.CreateCommand();
        select.CommandText = "SELECT Data FROM DbFile";
        using var reader = select.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(Encoding.UTF8.GetString((byte[])reader[0]));
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, p => p.Contains("FF14Accessibility"));
        Assert.Contains("{\"Other\":true}", rows);
    }

    [Fact]
    public void LiteDbTranslationPresetsAndArchivedCopiesAreCleaned()
    {
        Ready();
        MakeMod("1.19.1", Path.Combine(Mods, "XIV Rus"));
        var dbPath = Path.Combine(PenConfig, "mod_data.db");
        using (var db = new LiteDatabase(dbPath))
        {
            db.GetCollection("LocalModData").Insert(new BsonDocument { ["_id"] = "XIV Rus", ["Name"] = "Russian" });
            db.GetCollection("LocalModData").Insert(new BsonDocument { ["_id"] = "Other", ["Name"] = "Keep" });
            db.GetCollection("PresetData").Insert(new BsonDocument { ["_id"] = Guid.NewGuid(), ["Mod"] = "XIV Rus" });
            db.GetCollection("PresetData").Insert(new BsonDocument { ["_id"] = Guid.NewGuid(), ["Mod"] = "Other" });
        }
        var backups = Path.Combine(Root, "backups", "Penumbra");
        Directory.CreateDirectory(backups);
        ZipFile.CreateFromDirectory(PenConfig, Path.Combine(backups, "old.zip"));
        Remover().Remove(false, true, default);
        using (var db = new LiteDatabase(dbPath))
        {
            Assert.Equal(1, db.GetCollection("LocalModData").Count());
            Assert.NotNull(db.GetCollection("LocalModData").FindById("Other"));
            Assert.Equal(1, db.GetCollection("PresetData").Count());
        }
        var extracted = Path.Combine(sandbox, "clean-backup");
        ZipFile.ExtractToDirectory(Path.Combine(backups, "old.zip"), extracted);
        using var archived = new LiteDatabase(Path.Combine(extracted, "mod_data.db"));
        Assert.Null(archived.GetCollection("LocalModData").FindById("XIV Rus"));
        Assert.NotNull(archived.GetCollection("LocalModData").FindById("Other"));
    }
}
