using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests
{
    private const string PmpUrl = "https://github.com/xivrus/xiv_ru_weblate/releases/download/v1.19.1/release.pmp";
    private const string PenZipUrl = "https://github.com/xivdev/Penumbra/releases/download/1.7.2.1/Penumbra.zip";
    private string PenConfig => Path.Combine(Root, "pluginConfigs", "Penumbra");
    private string Mods => Path.Combine(sandbox, "mods");
    private const string CollectionId = "00000000-0000-0000-0000-000000000003";

    private string MakeMod(string version, string? folder = null, bool modern = false)
    {
        folder ??= Path.Combine(sandbox, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "text.exd"), "Russian game text");
        var data = new JObject { ["Files"] = new JObject { ["exd/text_en.exd"] = "text.exd" } };
        var meta = new JObject { ["Name"] = "XIV Rus", ["Version"] = version, ["FileVersion"] = modern ? 4 : 3 };
        if (modern) meta["DefaultData"] = data;
        else File.WriteAllText(Path.Combine(folder, "default_mod.json"), data.ToString());
        File.WriteAllText(Path.Combine(folder, "meta.json"), meta.ToString());
        return folder;
    }

    private Handler TranslationNetwork()
    {
        var handler = Network();
        var archive = Path.Combine(sandbox, "translation.pmp");
        ZipFile.CreateFromDirectory(MakeMod("1.19.1"), archive);
        handler.Data[PmpUrl] = File.ReadAllBytes(archive);
        handler.Data[ReleaseClient.TranslationUrl] = Encoding.UTF8.GetBytes(new JObject
        {
            ["tag_name"] = "v1.19.1", ["assets"] = new JArray(new JObject
            { ["name"] = "release.pmp", ["browser_download_url"] = PmpUrl,
                ["digest"] = "sha256:" + Convert.ToHexString(SHA256.HashData(handler.Data[PmpUrl])) })
        }.ToString());
        handler.Data[ReleaseClient.PenumbraUrl] = Encoding.UTF8.GetBytes(new JArray(new JObject
        { ["InternalName"] = "Penumbra", ["AssemblyVersion"] = "1.7.2.1", ["DalamudApiLevel"] = 15, ["DownloadLinkInstall"] = PenZipUrl }).ToString());
        handler.Data[PenZipUrl] = Zip("Penumbra", "1.7.2.1", false);
        return handler;
    }

    private void Ready(bool emptyRoot = false)
    {
        Directory.CreateDirectory(Path.Combine(PenConfig, "config"));
        Directory.CreateDirectory(Path.Combine(PenConfig, "collections"));
        File.WriteAllText(Path.Combine(PenConfig, "config", "penumbra.json"), new JObject
        { ["Version"] = 100, ["ModDirectory"] = emptyRoot ? "" : Mods, ["EnableMods"] = false, ["Unrelated"] = "keep" }.ToString());
        File.WriteAllText(Path.Combine(PenConfig, "collections", CollectionId + ".json"), new JObject
        {
            ["Version"] = 2, ["Id"] = CollectionId, ["Name"] = "Default", ["Inheritance"] = new JArray(),
            ["Settings"] = new JObject { ["Other"] = new JObject { ["Enabled"] = false } }
        }.ToString());
        File.WriteAllText(Path.Combine(PenConfig, "active_collections.json"), new JObject
        { ["Version"] = 2, ["Default"] = CollectionId, ["Interface"] = Guid.Empty.ToString(), ["Individuals"] = new JArray() }.ToString());
    }

    [Fact]
    public async Task FirstRussificationCompletesWithoutGameAndRepeatDoesNotChangeFiles()
    {
        var handler = TranslationNetwork();
        using var service = Service(handler);
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        Assert.True(File.Exists(Path.Combine(Root, "devPlugins", "Penumbra", "Penumbra.dll")));
        Assert.Equal(3, (int)PackageFiles.ReadJson(File.ReadAllText(Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json")))["Language"]!);
        var installedMods = Path.Combine(Root, "PenumbraMods");
        Assert.Equal(new Version(1, 19, 1, 0), TranslationInstaller.ValidateMod(Path.Combine(installedMods, "XIV Rus")));
        Assert.Single(handler.Requests, r => r == PmpUrl);
        var files = PackageFiles.Snapshot(Root);
        var mods = PackageFiles.Snapshot(installedMods);
        await service.Russify(default, []);
        PackageFiles.VerifySnapshot(Root, files);
        PackageFiles.VerifySnapshot(installedMods, mods);
        Assert.Single(handler.Requests, r => r == PenZipUrl);
    }

    [Fact]
    public async Task PenumbraFirstBootWithoutMainConfigDoesNotRequireAnotherGameLaunch()
    {
        var handler = TranslationNetwork();
        Ready();
        // MainConfig.Load returns without saving if this file does not exist.
        // Collections are created independently by Penumbra's first boot.
        File.Delete(Path.Combine(PenConfig, "config", "penumbra.json"));
        using var service = Service(handler);
        Assert.Contains("Русификация завершена", await service.Russify(default, []));
        var active = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(PenConfig, "active_collections.json")));
        Assert.Equal(CollectionId, (string?)active["Default"]);
        Assert.Single(Directory.GetFiles(Path.Combine(PenConfig, "collections"), "*.json"));
        Assert.True(File.Exists(Path.Combine(Root, "PenumbraMods", "XIV Rus", "text.exd")));
    }

    [Fact]
    public async Task FoundArchiveIsUsedAndUserSettingsPreserved()
    {
        var handler = TranslationNetwork();
        Ready();
        var path = Path.Combine(Root, "pluginConfigs", "FF14Accessibility.json");
        File.WriteAllText(path, "{\"Language\":2,\"KeyHelp\":\"Alt+H\",\"Volume\":0.3}");
        using var service = Service(handler);
        await service.Russify(default, [sandbox]);
        Assert.DoesNotContain(PmpUrl, handler.Requests);
        var language = PackageFiles.ReadJson(File.ReadAllText(path));
        Assert.Equal("Alt+H", (string?)language["KeyHelp"]);
        Assert.Equal(0.3, (double)language["Volume"]!);
        var config = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(PenConfig, "config", "penumbra.json")));
        Assert.Equal("keep", (string?)config["Unrelated"]);
        Assert.True((bool)config["EnableMods"]!);
        var collection = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(PenConfig, "collections", CollectionId + ".json")));
        Assert.False((bool)collection["Settings"]!["Other"]!["Enabled"]!);
        Assert.True((bool)collection["Settings"]!["XIV Rus"]!["Enabled"]!);
        Assert.Equal(CollectionId, (string?)PackageFiles.ReadJson(File.ReadAllText(Path.Combine(PenConfig, "active_collections.json")))["Interface"]);
    }

    [Theory]
    [InlineData("1.19.1")]
    [InlineData("1.20.0")]
    public async Task InstalledModernTranslationIsFoundByMetadataAndNeverDowngraded(string version)
    {
        var handler = TranslationNetwork();
        Ready();
        var folder = MakeMod(version, Path.Combine(Mods, "My Russian text"), modern: true);
        var before = PackageFiles.Snapshot(folder);
        var cpath = Path.Combine(PenConfig, "collections", CollectionId + ".json");
        var c = PackageFiles.ReadJson(File.ReadAllText(cpath));
        c["Settings"]!["My Russian text"] = new JObject
        { ["Enabled"] = false, ["Priority"] = 321, ["Settings"] = new JObject { ["Font"] = 2 } };
        File.WriteAllText(cpath, c.ToString());
        using var service = Service(handler);
        await service.Russify(default, []);
        Assert.DoesNotContain(PmpUrl, handler.Requests);
        PackageFiles.VerifySnapshot(folder, before);
        var entry = PackageFiles.ReadJson(File.ReadAllText(cpath))["Settings"]!["My Russian text"]!;
        Assert.True((bool)entry["Enabled"]!);
        Assert.Equal(321, (int)entry["Priority"]!);
        Assert.Equal(2, (int)entry["Settings"]!["Font"]!);
        Assert.False(Directory.Exists(Path.Combine(Mods, "XIV Rus")));
    }

    [Fact]
    public async Task CatalogPenumbraIsEnabledWithoutDevCopyAndIdentityIsPreserved()
    {
        var handler = TranslationNetwork();
        var folder = Plugin("Penumbra", "1.7.2.1", Path.Combine(Root, "installedPlugins", "Penumbra", "1.7.2.1"));
        var path = Path.Combine(folder, "Penumbra.json");
        var manifest = PackageFiles.ReadJson(File.ReadAllText(path));
        manifest["WorkingPluginId"] = CollectionId;
        manifest["Disabled"] = true;
        File.WriteAllText(path, manifest.ToString());
        using var service = Service(handler);
        await service.Russify(default, []);
        Assert.DoesNotContain(PenZipUrl, handler.Requests);
        Assert.False(Directory.Exists(Path.Combine(Root, "devPlugins", "Penumbra")));
        var config = PackageFiles.ReadJson(File.ReadAllText(ConfigPath));
        var entry = DalamudSettings.Array(config["DefaultProfile"]!["Plugins"], "test").Single(p => (string?)p["InternalName"] == "Penumbra");
        Assert.Equal(CollectionId, (string?)entry["WorkingPluginId"]);
        Assert.True((bool)entry["IsEnabled"]!);
        Assert.False((bool)PackageFiles.ReadJson(File.ReadAllText(path))["Disabled"]!);
    }

    [Fact]
    public async Task MissingModDirectoryIsChosenAutomaticallyAfterGameCreatedSettings()
    {
        var handler = TranslationNetwork();
        Ready(emptyRoot: true);
        using var service = Service(handler);
        await service.Russify(default, []);
        Assert.True(File.Exists(Path.Combine(Root, "PenumbraMods", "XIV Rus", "text.exd")));
    }

    [Fact]
    public async Task CorruptDownloadCannotReplaceOldTranslationOrCollections()
    {
        var handler = TranslationNetwork();
        Ready();
        MakeMod("1.18.0", Path.Combine(Mods, "XIV Rus"));
        var before = PackageFiles.Snapshot(Mods);
        var settings = PackageFiles.Snapshot(PenConfig);
        handler.Data[PmpUrl] = Encoding.UTF8.GetBytes("bad bytes");
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        PackageFiles.VerifySnapshot(Mods, before);
        PackageFiles.VerifySnapshot(PenConfig, settings);
        Assert.Empty(Directory.GetFiles(Path.Combine(Root, "installer-cache")));
    }

    [Fact]
    public async Task MalformedCollectionAndMultipleTranslationsRefuseBeforeDownload()
    {
        var handler = TranslationNetwork();
        Ready();
        var path = Path.Combine(PenConfig, "collections", CollectionId + ".json");
        var bytes = File.ReadAllBytes(path);
        File.WriteAllText(path, "{\"Version\":2,\"Settings\":[]}");
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        Assert.DoesNotContain(PmpUrl, handler.Requests);
        File.WriteAllBytes(path, bytes);
        MakeMod("1.19.1", Path.Combine(Mods, "one"));
        MakeMod("1.19.1", Path.Combine(Mods, "two"));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Russify(default, []));
        Assert.DoesNotContain(PmpUrl, handler.Requests);
    }

    [Fact]
    public async Task UpdateBacksUpEntireOldModAndDoesNotMergeObsoleteFiles()
    {
        var handler = TranslationNetwork();
        Ready();
        var folder = MakeMod("1.18.0", Path.Combine(Mods, "XIV Rus"));
        File.WriteAllText(Path.Combine(folder, "obsolete.txt"), "keep in backup");
        var before = PackageFiles.Snapshot(folder);
        using var service = Service(handler);
        await service.Russify(default, []);
        Assert.False(File.Exists(Path.Combine(folder, "obsolete.txt")));
        var backup = Directory.GetDirectories(Mods + "-backup").Single();
        PackageFiles.VerifySnapshot(Path.Combine(backup, "previous-mod"), before);
    }

    [Fact]
    public async Task OptionalDanglingReferenceIsAcceptedButLostInstalledFileIsRepairedAfterMetadataMigration()
    {
        var handler = TranslationNetwork();
        var source = MakeMod("1.19.1");
        File.WriteAllText(Path.Combine(source, "optional.tex"), "font");
        var group = new JObject { ["Options"] = new JArray(new JObject { ["Files"] = new JObject
        { ["font"] = "optional.tex", ["outdated-entry"] = "missing-optional.exd" } }) };
        File.WriteAllText(Path.Combine(source, "group_001_font.json"), group.ToString());
        var archive = Path.Combine(sandbox, "optional.pmp");
        ZipFile.CreateFromDirectory(source, archive);
        handler.Data[PmpUrl] = File.ReadAllBytes(archive);
        var release = PackageFiles.ReadJson(Encoding.UTF8.GetString(handler.Data[ReleaseClient.TranslationUrl]));
        release["assets"]![0]!["digest"] = "sha256:" + Convert.ToHexString(SHA256.HashData(handler.Data[PmpUrl]));
        handler.Data[ReleaseClient.TranslationUrl] = Encoding.UTF8.GetBytes(release.ToString());
        Ready();
        using var service = Service(handler);
        await service.Russify(default, []);
        var target = Path.Combine(Mods, "XIV Rus");
        var meta = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(target, "meta.json")));
        meta["FileVersion"] = 4;
        meta["DefaultData"] = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(target, "default_mod.json")));
        meta["Groups"] = new JArray(group);
        File.WriteAllText(Path.Combine(target, "meta.json"), meta.ToString());
        File.Move(Path.Combine(target, "default_mod.json"), Path.Combine(target, "default_mod.json.bak"));
        File.Move(Path.Combine(target, "group_001_font.json"), Path.Combine(target, "group_001_font.json.bak"));
        var before = PackageFiles.Snapshot(target);
        await service.Russify(default, []);
        PackageFiles.VerifySnapshot(target, before);
        File.Delete(Path.Combine(target, "optional.tex"));
        await service.Russify(default, []);
        Assert.Equal("font", File.ReadAllText(Path.Combine(target, "optional.tex")));
        Assert.Single(handler.Requests, r => r == PmpUrl);
    }

    [Theory]
    [InlineData("files")]
    [InlineData("config")]
    public void TranslationWriteFailureRestoresModAndExactSettings(string point)
    {
        Ready();
        var target = MakeMod("1.18.0", Path.Combine(Mods, "XIV Rus"));
        var stage = MakeMod("1.19.1");
        var before = PackageFiles.Snapshot(target);
        var path = Path.Combine(PenConfig, "config", "penumbra.json");
        var bytes = File.ReadAllBytes(path);
        var change = new JsonChange(path);
        change.Value["EnableMods"] = true;
        Assert.Throws<IOException>(() => SettingsTransaction.Apply(Mods + "-backup", [change], () => { }, _ => { },
            stage, target, before, at => { if (at == "files" && point == "files" || at == path && point == "config") throw new IOException("injected"); }));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        PackageFiles.VerifySnapshot(target, before);
    }

    [Fact]
    public void SettingsChangedWhileDownloadingArePreserved()
    {
        Ready();
        var path = Path.Combine(PenConfig, "config", "penumbra.json");
        var change = new JsonChange(path);
        change.Value["EnableMods"] = true;
        File.AppendAllText(path, " ");
        var bytes = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() => SettingsTransaction.Apply(Mods + "-backup", [change], () => { }, _ => { }));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task RussificationRefusesRunningGameBeforeNetworkOrWrites()
    {
        var handler = TranslationNetwork();
        var before = PackageFiles.Snapshot(Root);
        using var service = new InstallerService(Root, new ReleaseClient(handler), () => throw new InvalidOperationException("running"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Russify(default, []));
        PackageFiles.VerifySnapshot(Root, before);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UnknownDownloadHostAndMissingChecksumRefuse()
    {
        var handler = TranslationNetwork();
        var release = PackageFiles.ReadJson(Encoding.UTF8.GetString(handler.Data[ReleaseClient.TranslationUrl]));
        release["assets"]![0]!["browser_download_url"] = "https://example.com/release.pmp";
        handler.Data[ReleaseClient.TranslationUrl] = Encoding.UTF8.GetBytes(release.ToString());
        using var client = new ReleaseClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.Translation(default));
        release["assets"]![0]!["browser_download_url"] = PmpUrl;
        ((JObject)release["assets"]![0]!).Remove("digest");
        handler.Data[ReleaseClient.TranslationUrl] = Encoding.UTF8.GetBytes(release.ToString());
        await Assert.ThrowsAsync<InvalidDataException>(() => client.Translation(default));
    }
}
