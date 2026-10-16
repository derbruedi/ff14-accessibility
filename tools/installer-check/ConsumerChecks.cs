using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using FF14AccessibilityInstaller.Russian;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Read isolated generated fixtures with the installed consumers' real types.
// This does not instantiate plugins or call game services and is not a game test.
internal static class ConsumerChecks
{
    public static void Verify(string root, string dalamudFolder)
    {
        var penumbraFolder = Path.Combine(root, "devPlugins", "Penumbra");
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            foreach (var folder in new[] { dalamudFolder, penumbraFolder })
            {
                var path = Path.Combine(folder, name.Name + ".dll");
                if (File.Exists(path)) return context.LoadFromAssemblyPath(path);
            }
            return null;
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            var dalamud = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dalamudFolder, "Dalamud.dll"));
            var config = PackageFiles.ReadJson(File.ReadAllText(Path.Combine(root, "dalamudConfig.json")));
            var configType = dalamud.GetType("Dalamud.Configuration.Internal.DalamudConfiguration", true)!;
            var startupJson = new JObject { [TranslationInstaller.WaitForPluginsSetting] = config[TranslationInstaller.WaitForPluginsSetting]?.DeepClone() };
            var startup = JsonConvert.DeserializeObject(startupJson.ToString(), configType)!;
            if (configType.GetProperty(TranslationInstaller.WaitForPluginsSetting)!.GetValue(startup) is not true)
                throw new Exception("Actual Dalamud configuration does not wait for plugins before loading the game.");
            Console.WriteLine("PASS: actual Dalamud configuration enables waiting for plugins before the game loads.");
            var settingsType = dalamud.GetType("Dalamud.Configuration.Internal.DevPluginSettings", true)!;
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), settingsType);
            var serializer = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var settings = (IDictionary)JsonConvert.DeserializeObject(config["DevPluginSettings"]!.ToString(), dictionaryType, serializer)!;
            var profileType = dalamud.GetType("Dalamud.Plugin.Internal.Profiles.ProfileModelV1", true)!;
            var profile = JsonConvert.DeserializeObject(config["DefaultProfile"]!.ToString(), profileType, serializer)!;
            var plugins = (IEnumerable)profileType.GetProperty("Plugins")!.GetValue(profile)!;
            foreach (var name in new[] { "FF14Accessibility", "Penumbra", "vnavmesh" })
            {
                var dll = new FileInfo(Path.Combine(root, "devPlugins", name, name + ".dll")).FullName;
                var entry = settings[dll] ?? throw new Exception("Dalamud did not find exact path: " + dll);
                if (!(bool)settingsType.GetProperty("StartOnBoot")!.GetValue(entry)!) throw new Exception("StartOnBoot is false.");
                var id = (Guid)settingsType.GetProperty("WorkingPluginId")!.GetValue(entry)!;
                var record = plugins.Cast<object>().Single(p => (string?)p.GetType().GetProperty("InternalName")!.GetValue(p) == name);
                if (id == Guid.Empty || (Guid)record.GetType().GetProperty("WorkingPluginId")!.GetValue(record)! != id ||
                    !(bool)record.GetType().GetProperty("IsEnabled")!.GetValue(record)!) throw new Exception("Actual Dalamud profile identity disagrees.");
            }
            Console.WriteLine("PASS: actual Dalamud " + dalamud.GetName().Version + " deserialized startup keys and enabled profile identities.");

            var penumbra = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(penumbraFolder, "Penumbra.dll"));
            var reader = penumbra.GetType("Penumbra.Collections.ModCollectionSave", true)!
                .GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static)!;
            foreach (var file in Directory.GetFiles(Path.Combine(root, "pluginConfigs", "Penumbra", "collections"), "*.json"))
            {
                object?[] args = [file, null, null, null, null, null];
                if (!(bool)reader.Invoke(null, args)! || (Guid)args[1]! == Guid.Empty || (int)args[3]! != 2)
                    throw new Exception("Actual Penumbra reader rejected the collection.");
                var mods = (IDictionary)args[4]!;
                var mod = mods["XIV Rus"] ?? throw new Exception("Actual Penumbra reader lost XIV Rus.");
                var enabled = mod.GetType().GetField("Enabled")?.GetValue(mod)
                    ?? mod.GetType().GetProperty("Enabled")?.GetValue(mod);
                if (enabled is not true) throw new Exception("Actual Penumbra reader did not enable XIV Rus.");
            }
            Console.WriteLine("PASS: actual Penumbra " + penumbra.GetName().Version + " read collections with XIV Rus enabled.");
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
    }
}
