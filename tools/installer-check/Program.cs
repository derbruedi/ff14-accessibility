using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json.Linq;

if (args.Length == 2 && args[0] == "--latest-only")
{
    using var releases = new ReleaseClient();
    var release = await releases.Latest(default);
    if (release.DisplayVersion != args[1]) throw new Exception("Unexpected latest version: " + release.DisplayVersion);
    Console.WriteLine("PASS: real ReleaseClient.Latest selects " + release.DisplayVersion + " and " + release.Url);
    return;
}

if (args.Length is < 1 or > 3) throw new ArgumentException("Specify a NEW isolated output directory, optionally --translation or an archive search folder, then an exported release metadata folder.");
var root = Path.GetFullPath(args[0]);
var live = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
if (root.StartsWith(live, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("The output must be a new isolated directory outside XIVLauncher.");
if (args.Length == 2 && args[1] == "--verify-existing")
{
    if (!File.Exists(Path.Combine(root, "verification-log.txt")))
        throw new InvalidOperationException("Expected a completed isolated verification directory.");
    var existingConfig = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(root, "dalamudConfig.json")));
    foreach (var name in new[] { "FF14Accessibility", "Penumbra", "vnavmesh" })
        DalamudSettings.VerifyEnabled(root, name, DalamudSettings.Find(root, existingConfig, name).Single());
    ConsumerChecks.Verify(root, Path.Combine(live, "addon", "Hooks", "dev"));
    Console.WriteLine("PASS: final source verifies the isolated installation without writes.");
    return;
}
if (Directory.Exists(root)) throw new InvalidOperationException("The output directory already exists.");
var liveConfig = File.ReadAllBytes(Path.Combine(live, "dalamudConfig.json"));
var liveAcc = PackageFiles.Snapshot(Path.Combine(live, "devPlugins", "FF14Accessibility"));
var liveVnav = PackageFiles.Snapshot(Path.Combine(live, "devPlugins", "vnavmesh"));
var livePen = PackageFiles.Snapshot(Path.Combine(live, "devPlugins", "Penumbra"));
var livePenConfig = PackageFiles.Snapshot(Path.Combine(live, "pluginConfigs", "Penumbra"));
Directory.CreateDirectory(root);
// Exercise the real current schema after removing all personal paths and profiles.
var config = PackageFiles.ReadJson(System.Text.Encoding.UTF8.GetString(liveConfig));
var fixture = new JObject
{
    [TranslationInstaller.WaitForPluginsSetting] = false,
    ["DevMode"] = false,
    ["DevPluginLoadLocations"] = new JObject { ["$type"] = config["DevPluginLoadLocations"]?["$type"]?.DeepClone(), ["$values"] = new JArray() },
    ["DevPluginSettings"] = new JObject { ["$type"] = config["DevPluginSettings"]?["$type"]?.DeepClone() },
    ["DefaultProfile"] = new JObject { ["$type"] = config["DefaultProfile"]?["$type"]?.DeepClone(),
        ["Plugins"] = new JObject { ["$type"] = config["DefaultProfile"]?["Plugins"]?["$type"]?.DeepClone(), ["$values"] = new JArray() } },
    ["SavedProfiles"] = new JObject { ["$type"] = config["SavedProfiles"]?["$type"]?.DeepClone(), ["$values"] = new JArray() }
};
File.WriteAllText(Path.Combine(root, "dalamudConfig.json"), fixture.ToString());
var log = new List<string>();
using var service = new InstallerService(root,
    client: args.Length == 3 ? new ReleaseClient(new ExportedMetadataHandler(args[2])) : null, checkClosed: () => { })
{
    Log = message => { Console.WriteLine(message); log.Add(message); },
    Ask = question => { log.Add(question + " [isolated test: yes]"); return Task.FromResult(true); }
};
Console.WriteLine(await service.Install(default));
var afterFirst = PackageFiles.Snapshot(root);
Console.WriteLine(await service.Install(default));
PackageFiles.VerifySnapshot(root, afterFirst);
await service.Check(default);
if (args.Length >= 2)
{
    var folders = args[1] == "--translation" ? Array.Empty<string>() : new[] { Path.GetFullPath(args[1]) };
    Console.WriteLine(await service.RussifyMod(default));
    var languageBeforeGame = File.ReadAllBytes(Path.Combine(root, "pluginConfigs", "FF14Accessibility.json"));
    var first = await service.RussifyGame(default, folders);
    if (!File.ReadAllBytes(Path.Combine(root, "pluginConfigs", "FF14Accessibility.json")).SequenceEqual(languageBeforeGame))
        throw new Exception("Game translation changed the mod language or settings.");
    Console.WriteLine(first);
    if (!first.Contains("Русификация завершена")) throw new Exception("Fresh installation did not complete.");
    // No invented first-game files: all of these must come from the installer itself.
    var pen = Path.Combine(root, "pluginConfigs", "Penumbra");
    ConsumerChecks.Verify(root, Path.Combine(live, "addon", "Hooks", "dev"));
    var complete = PackageFiles.Snapshot(root);
    Console.WriteLine(await service.RussifyGame(default, folders));
    Console.WriteLine(await service.RussifyMod(default));
    PackageFiles.VerifySnapshot(root, complete);
    // Reproduce Penumbra's real first-boot state: collections exist but MainConfig.Load
    // has never saved config/penumbra.json. Installation must recover on this invocation.
    File.Move(Path.Combine(pen, "config", "penumbra.json"), Path.Combine(root, "verified-main-config.json"));
    var activeBefore = File.ReadAllBytes(Path.Combine(pen, "active_collections.json"));
    if (!(await service.RussifyGame(default, folders)).Contains("Русификация завершена")) throw new Exception("Recovery failed.");
    if (!File.ReadAllBytes(Path.Combine(pen, "active_collections.json")).SequenceEqual(activeBefore))
        throw new Exception("Recovery changed collection assignments.");
    ConsumerChecks.Verify(root, Path.Combine(live, "addon", "Hooks", "dev"));
    Console.WriteLine("PASS: real Penumbra and XIV Rus; fresh setup, repeat, missing main config recovery; actual Dalamud and Penumbra readers.");
}
if (!File.ReadAllBytes(Path.Combine(live, "dalamudConfig.json")).SequenceEqual(liveConfig)) throw new Exception("Live config changed.");
PackageFiles.VerifySnapshot(Path.Combine(live, "devPlugins", "FF14Accessibility"), liveAcc);
PackageFiles.VerifySnapshot(Path.Combine(live, "devPlugins", "vnavmesh"), liveVnav);
PackageFiles.VerifySnapshot(Path.Combine(live, "devPlugins", "Penumbra"), livePen);
PackageFiles.VerifySnapshot(Path.Combine(live, "pluginConfigs", "Penumbra"), livePenConfig);
File.WriteAllLines(Path.Combine(root, "verification-log.txt"), log);
Console.WriteLine("PASS: live downloads, first install including vnavmesh, repeat no-op, read-only check; real installation unchanged.");
