using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json.Linq;

namespace Installer.Tests;

public sealed partial class InstallerTests : IDisposable
{
    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "InstallerTests-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(sandbox, "XIVLauncher");
    private string ConfigPath => Path.Combine(Root, "dalamudConfig.json");
    private string Target => Path.Combine(Root, "devPlugins", "FF14Accessibility");
    private const string ZipUrl = "https://github.com/pitpamyati-netizen/ff14-accessibility/releases/download/v6.08.73/FF14Accessibility-6.08.73-RU-no-source.zip";
    private const string VnavZip = "https://puni.sh/api/plugins/download/48/vnavmesh/versions/1.2.3.14/install/latest.zip";

    public InstallerTests() { Directory.CreateDirectory(Root); File.WriteAllText(ConfigPath, Config().ToString()); }
    public void Dispose() => Directory.Delete(sandbox, true);

    private static JObject Config() => JObject.Parse("""
        {"DevMode":false,"Unrelated":{"date":"2026-01-01T00:00:00Z","number":123456789123456789},
         "DevPluginLoadLocations":{"$type":"list","$values":[]},"DevPluginSettings":{},
         "DefaultProfile":{"Plugins":{"$values":[{"InternalName":"Other","IsEnabled":false,"WorkingPluginId":"00000000-0000-0000-0000-000000000001"}]}},
         "SavedProfiles":{"$values":[{"n":"character","Plugins":{"$values":[]}}]}}
        """);

    private string Plugin(string name, string version, string? folder = null)
    {
        folder ??= Path.Combine(sandbox, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var assembly = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = PackageFiles.VersionOf(version) }, typeof(object).Assembly);
        assembly.DefineDynamicModule(name).DefineType("Fixture", TypeAttributes.Public).CreateType();
        assembly.Save(Path.Combine(folder, name + ".dll"));
        File.WriteAllText(Path.Combine(folder, name + ".json"), new JObject
        { ["InternalName"] = name, ["AssemblyVersion"] = version, ["DalamudApiLevel"] = 15 }.ToString());
        if (name == "FF14Accessibility")
        {
            foreach (var f in new[] { "FF14Accessibility.deps.json", "Tolk.dll", "nvdaControllerClient64.dll", "System.Speech.dll",
                "NAudio.dll", "NAudio.Core.dll", "NAudio.WinMM.dll", "NAudio.Wasapi.dll", "LICENSE", "THIRD-PARTY-NOTICES.md" })
                File.WriteAllText(Path.Combine(folder, f), "test");
            var audio = Path.Combine(folder, "assets", "partymonitor");
            Directory.CreateDirectory(audio);
            for (var i = 0; i < 122; i++) File.WriteAllText(Path.Combine(audio, i + ".mp3"), "test");
        }
        return folder;
    }

    private byte[] Zip(string name, string version, bool wrap)
    {
        var payload = Plugin(name, version);
        var content = payload;
        if (wrap)
        {
            content = Path.Combine(sandbox, Guid.NewGuid().ToString("N"));
            PackageFiles.CopyTree(payload, Path.Combine(content, "plugin"));
            File.WriteAllText(Path.Combine(content, "install.ps1"), "must never execute");
        }
        var zip = Path.Combine(sandbox, Guid.NewGuid() + ".zip");
        ZipFile.CreateFromDirectory(content, zip);
        return File.ReadAllBytes(zip);
    }

    private Handler Network(byte[]? zip = null)
    {
        zip ??= Zip("FF14Accessibility", "6.8.73.0", true);
        var handler = new Handler();
        handler.Data[ReleaseClient.LatestUrl] = Encoding.UTF8.GetBytes(new JObject
        {
            ["tag_name"] = "v6.08.73", ["draft"] = false, ["prerelease"] = false,
            ["assets"] = new JArray(new JObject { ["name"] = "FF14Accessibility-6.08.73-RU-no-source.zip",
                ["browser_download_url"] = ZipUrl, ["digest"] = "sha256:" + Convert.ToHexString(SHA256.HashData(zip)) })
        }.ToString());
        handler.Data[ZipUrl] = zip;
        handler.Data[ReleaseClient.VnavUrl] = Encoding.UTF8.GetBytes(new JArray(new JObject
        { ["InternalName"] = "vnavmesh", ["AssemblyVersion"] = "1.2.3.14", ["DalamudApiLevel"] = 15, ["DownloadLinkInstall"] = VnavZip }).ToString());
        handler.Data[VnavZip] = Zip("vnavmesh", "1.2.3.14", false);
        return handler;
    }

    private InstallerService Service(Handler handler, bool accept = false) => new(Root, new ReleaseClient(handler), () => { })
    { Ask = _ => Task.FromResult(accept) };

    [Theory]
    [InlineData("v6.08.73", "6.8.73.0")]
    [InlineData("6.08.9", "6.8.9.0")]
    [InlineData("1.2", "1.2.0.0")]
    public void VersionsAreNumeric(string input, string expected) => Assert.Equal(new Version(expected), PackageFiles.VersionOf(input));

    [Theory]
    [InlineData("v6.8.73-beta")]
    [InlineData("newest")]
    [InlineData("6.8.73.0.1")]
    public void UnknownVersionsFail(string input) => Assert.Throws<InvalidDataException>(() => PackageFiles.VersionOf(input));

    [Theory]
    [InlineData("../escape.dll")]
    [InlineData("/root.dll")]
    [InlineData("plugin/CON.dll")]
    [InlineData("plugin/file.dll:evil")]
    [InlineData("plugin/file. ")]
    public void UnsafeArchiveCannotEscape(string entry)
    {
        var path = Path.Combine(sandbox, "unsafe.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry(entry);
        Assert.Throws<InvalidDataException>(() => PackageFiles.Extract(path, Path.Combine(sandbox, "extract")));
        Assert.False(Directory.Exists(Path.Combine(sandbox, "extract")));
    }

    [Fact]
    public void DuplicateArchiveNamesRejected()
    {
        var path = Path.Combine(sandbox, "duplicate.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) { zip.CreateEntry("plugin/A.dll"); zip.CreateEntry("plugin/a.dll"); }
        Assert.Throws<InvalidDataException>(() => PackageFiles.Extract(path, Path.Combine(sandbox, "extract")));
    }

    [Fact]
    public void ProfileIdentityAndUnrelatedSettingsSurviveRepeatEnable()
    {
        var original = Config();
        var path = Path.Combine(Target, "FF14Accessibility.dll");
        var changed = DalamudSettings.Enable(original, "FF14Accessibility", path);
        var twice = DalamudSettings.Enable(changed, "FF14Accessibility", path);
        Assert.True(JToken.DeepEquals(changed, twice));
        Assert.True(JToken.DeepEquals(original["Unrelated"], changed["Unrelated"]));
        var id = (string?)changed["DevPluginSettings"]?[path]?["WorkingPluginId"];
        Assert.True(Guid.TryParse(id, out _));
        foreach (var profile in new[] { changed["DefaultProfile"]!, changed["SavedProfiles"]!["$values"]![0]! })
        {
            var entry = DalamudSettings.Array(profile["Plugins"], "test").Single(p => (string?)p["InternalName"] == "FF14Accessibility");
            Assert.Equal(id, (string?)entry["WorkingPluginId"]);
            Assert.True((bool)entry["IsEnabled"]!);
        }
        Assert.False((bool)changed["DefaultProfile"]!["Plugins"]!["$values"]![0]!["IsEnabled"]!);
    }

    [Fact]
    public async Task FirstInstallRegistersPluginAndDecliningVnavDoesNotDownloadIt()
    {
        var handler = Network();
        using var service = Service(handler);
        var result = await service.Install(default);
        Assert.Equal(new Version(6, 8, 73, 0), PackageFiles.DllVersion(Path.Combine(Target, "FF14Accessibility.dll")));
        Assert.DoesNotContain(ReleaseClient.VnavUrl, handler.Requests);
        Assert.Contains("пропущена", result);
        Assert.False(File.Exists(Path.Combine(Target, "install.ps1")));
        Assert.True((bool)PackageFiles.ReadJson(File.ReadAllText(ConfigPath))["DevMode"]!);
    }

    [Fact]
    public async Task AcceptedVnavIsDownloadedValidatedAndEnabled()
    {
        var handler = Network();
        using var service = Service(handler, true);
        Assert.Contains("vnavmesh 1.2.3.14 установлена", await service.Install(default));
        var dll = Path.Combine(Root, "devPlugins", "vnavmesh", "vnavmesh.dll");
        Assert.Equal(new Version(1, 2, 3, 14), PackageFiles.DllVersion(dll));
        Assert.True((bool)PackageFiles.ReadJson(File.ReadAllText(ConfigPath))["DevPluginSettings"]![dll]!["StartOnBoot"]!);
    }

    [Theory]
    [InlineData("6.8.73.0")]
    [InlineData("6.8.99.0")]
    public async Task SameOrNewerDllIsNeverDownloadedOrDowngraded(string version)
    {
        Plugin("FF14Accessibility", version, Target);
        var before = PackageFiles.Snapshot(Target);
        var handler = Network();
        using var service = Service(handler);
        await service.Install(default);
        Assert.DoesNotContain(ZipUrl, handler.Requests);
        PackageFiles.VerifySnapshot(Target, before);
    }

    [Fact]
    public async Task OlderDllIsUpdatedAndEntireOldFolderBackedUp()
    {
        Plugin("FF14Accessibility", "6.8.9.0", Target);
        File.WriteAllText(Path.Combine(Target, "user-file.txt"), "keep");
        var before = PackageFiles.Snapshot(Target);
        using var service = Service(Network());
        await service.Install(default);
        var backup = Directory.GetDirectories(Path.Combine(Root, "installer-backups")).Single();
        PackageFiles.VerifySnapshot(Path.Combine(backup, "FF14Accessibility"), before);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(Target, "user-file.txt")));
        Assert.Equal(new Version(6, 8, 73, 0), PackageFiles.DllVersion(Path.Combine(Target, "FF14Accessibility.dll")));
    }

    [Fact]
    public async Task CatalogVnavIsDetectedWithoutCreatingDevCopy()
    {
        Plugin("vnavmesh", "1.2.3.14", Path.Combine(Root, "installedPlugins", "vnavmesh", "1.2.3.14"));
        var handler = Network();
        using var service = Service(handler);
        service.Ask = _ => throw new Exception("Must not ask");
        Assert.Contains("уже установлена", await service.Install(default));
        Assert.DoesNotContain(ReleaseClient.VnavUrl, handler.Requests);
        Assert.False(Directory.Exists(Path.Combine(Root, "devPlugins", "vnavmesh")));
    }

    [Fact]
    public async Task ReadOnlyCheckDoesNotWriteOrDownloadPlugin()
    {
        var before = PackageFiles.Snapshot(Root);
        var handler = Network();
        using var service = Service(handler);
        await service.Check(default);
        PackageFiles.VerifySnapshot(Root, before);
        Assert.Equal(new[] { ReleaseClient.LatestUrl }, handler.Requests);
    }

    [Fact]
    public async Task BadHashLeavesInstallationAndSettingsUntouched()
    {
        var handler = Network();
        handler.Data[ZipUrl] = Encoding.UTF8.GetBytes("corrupt");
        var before = PackageFiles.Snapshot(Root);
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Install(default));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public async Task WrongPackageVersionLeavesInstallationUntouched()
    {
        var handler = Network(Zip("FF14Accessibility", "6.8.72.0", true));
        var before = PackageFiles.Snapshot(Root);
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Install(default));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Theory]
    [InlineData("files")]
    [InlineData("config")]
    public void FailureRestoresFilesAndExactConfigBytes(string at)
    {
        Plugin("FF14Accessibility", "6.8.72.0", Target);
        var payload = Plugin("FF14Accessibility", "6.8.73.0");
        var bytes = File.ReadAllBytes(ConfigPath);
        var snapshot = PackageFiles.Snapshot(Target);
        Assert.Throws<IOException>(() => InstallTransaction.Apply(Root, "FF14Accessibility", Target, payload, bytes, snapshot,
            _ => { }, () => { }, point => { if (point == at) throw new IOException("injected failure"); }));
        Assert.Equal(bytes, File.ReadAllBytes(ConfigPath));
        PackageFiles.VerifySnapshot(Target, snapshot);
    }

    [Fact]
    public void ConcurrentConfigEditIsNotOverwritten()
    {
        var bytes = File.ReadAllBytes(ConfigPath);
        File.AppendAllText(ConfigPath, " ");
        var changed = File.ReadAllBytes(ConfigPath);
        Assert.Throws<IOException>(() => InstallTransaction.Apply(Root, "FF14Accessibility", Target, null, bytes,
            PackageFiles.Snapshot(Target), _ => { }, () => { }));
        Assert.Equal(changed, File.ReadAllBytes(ConfigPath));
        Assert.False(Directory.Exists(Target));
    }

    [Fact]
    public async Task MissingConfigAndRunningGameRefuseBeforeAnyWrite()
    {
        var handler = Network();
        using (var service = new InstallerService(Root, new ReleaseClient(handler), () => throw new InvalidOperationException("game")))
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Install(default));
        Assert.Empty(handler.Requests);
        File.Delete(ConfigPath);
        using var missing = Service(Network());
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.Install(default));
        Assert.False(Directory.Exists(Target));
    }

    [Fact]
    public async Task VnavNetworkFailureReportsPartialResultHonestly()
    {
        var handler = Network();
        handler.Data.Remove(VnavZip);
        using var service = Service(handler, true);
        var result = await service.Install(default);
        Assert.Contains("Мод установлен. vnavmesh не удалось", result);
        Assert.True(File.Exists(Path.Combine(Target, "FF14Accessibility.dll")));
        Assert.False(Directory.Exists(Path.Combine(Root, "devPlugins", "vnavmesh")));
    }

    [Fact]
    public async Task CanceledDownloadDoesNotInstall()
    {
        var before = PackageFiles.Snapshot(Root);
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var service = Service(Network());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.Install(source.Token));
        PackageFiles.VerifySnapshot(Root, before);
    }

    [Fact]
    public void CustomRegisteredPathIsFoundAndDisabledEntryReenabled()
    {
        var custom = Plugin("vnavmesh", "1.2.3.14");
        var config = Config();
        var dll = Path.Combine(custom, "vnavmesh.dll");
        DalamudSettings.Array(config["DevPluginLoadLocations"], "test").Add(new JObject { ["Path"] = dll, ["IsEnabled"] = false });
        Assert.Equal(custom, DalamudSettings.Find(Root, config, "vnavmesh").Single().Folder);
        var enabled = DalamudSettings.Enable(config, "vnavmesh", dll);
        Assert.True((bool)DalamudSettings.Array(enabled["DevPluginLoadLocations"], "test")[0]["IsEnabled"]!);
    }

    [Fact]
    public async Task EmptyVnavFolderCanBeRepairedAfterConsent()
    {
        Directory.CreateDirectory(Path.Combine(Root, "devPlugins", "vnavmesh"));
        using var service = Service(Network(), true);
        Assert.Contains("vnavmesh 1.2.3.14 установлена", await service.Install(default));
    }

    [Fact]
    public async Task MalformedProfileIsRejectedBeforeDownload()
    {
        var config = Config();
        config["SavedProfiles"]!["$values"]![0]!["Plugins"] = "invalid";
        File.WriteAllText(ConfigPath, config.ToString());
        var snapshot = PackageFiles.Snapshot(Root);
        var handler = Network();
        using var service = Service(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.Install(default));
        Assert.DoesNotContain(ZipUrl, handler.Requests);
        PackageFiles.VerifySnapshot(Root, snapshot);
    }

    [Fact]
    public async Task MissingDigestUsesPublishedChecksumFile()
    {
        var handler = Network();
        var release = PackageFiles.ReadJson(Encoding.UTF8.GetString(handler.Data[ReleaseClient.LatestUrl]));
        ((JObject)release["assets"]![0]!).Remove("digest");
        var sums = ZipUrl[..ZipUrl.LastIndexOf('/')] + "/SHA256SUMS.txt";
        ((JArray)release["assets"]!).Add(new JObject { ["name"] = "SHA256SUMS.txt", ["browser_download_url"] = sums });
        handler.Data[ReleaseClient.LatestUrl] = Encoding.UTF8.GetBytes(release.ToString());
        handler.Data[sums] = Encoding.UTF8.GetBytes(Convert.ToHexString(SHA256.HashData(handler.Data[ZipUrl])) + "  FF14Accessibility-6.08.73-RU-no-source.zip\r\n");
        using var service = Service(handler);
        await service.Install(default);
        Assert.Contains(sums, handler.Requests);
        Assert.True(File.Exists(Path.Combine(Target, "FF14Accessibility.dll")));
    }

    [Fact]
    public async Task IncompatibleVnavIsNotDownloaded()
    {
        var handler = Network();
        var entry = JArray.Parse(Encoding.UTF8.GetString(handler.Data[ReleaseClient.VnavUrl]));
        entry[0]["DalamudApiLevel"] = 14;
        handler.Data[ReleaseClient.VnavUrl] = Encoding.UTF8.GetBytes(entry.ToString());
        using var service = Service(handler, true);
        Assert.Contains("vnavmesh не удалось", await service.Install(default));
        Assert.DoesNotContain(VnavZip, handler.Requests);
    }

    [Fact]
    public void PlainArraysAreSupportedAndDateStringsRemainStrings()
    {
        var config = PackageFiles.ReadJson(Config().ToString());
        config["DevPluginLoadLocations"] = new JArray();
        config["DefaultProfile"]!["Plugins"] = new JArray();
        config["SavedProfiles"] = new JArray();
        var updated = DalamudSettings.Enable(config, "vnavmesh", Path.Combine(Root, "vnavmesh.dll"));
        Assert.Equal(JTokenType.String, updated["Unrelated"]!["date"]!.Type);
        Assert.IsType<JArray>(updated["DevPluginLoadLocations"]);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Data { get; } = [];
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            return Task.FromResult(new HttpResponseMessage(Data.ContainsKey(url) ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            { RequestMessage = request, Content = new ByteArrayContent(Data.GetValueOrDefault(url) ?? []) });
        }
    }
}
