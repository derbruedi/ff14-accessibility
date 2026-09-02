using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Checks GitHub for a newer release and installs it into the plugin's own
/// folder, from inside the running game.
///
/// <para>
/// WHY THIS EXISTS AT ALL. The plugin is installed by an external tool
/// (FF14AccessibilityInstaller.exe) rather than through Dalamud's plugin
/// installer, because that installer is an ImGui overlay no screen reader can
/// read. The price was that every update meant leaving the game and running the
/// installer again. This service pays that price back: update from the menu,
/// without closing anything.
/// </para>
///
/// <para>
/// WHY OVERWRITING OUR OWN DLL IS SAFE - measured and read, not assumed:
/// <list type="bullet">
/// <item>The loaded managed DLLs are NOT locked. Measured 2026-09-01 with the
/// game running and the plugin loaded: every managed file in the folder opened
/// for exclusive write. Only the two NATIVE ones did not - see
/// <see cref="NativeFiles"/>.</item>
/// <item>Dalamud watches ONLY the main DLL. <c>LocalDevPlugin.EnableReloading</c>
/// creates a <c>FileSystemWatcher</c> with <c>Filter = DllFile.Name</c> and
/// <c>NotifyFilter = LastWrite</c>; every other file in the folder passes
/// unnoticed. That is why <see cref="InstallAsync"/> writes the main DLL LAST:
/// the reload then finds a complete set.</item>
/// <item>The reload is debounced. <c>LocalDevPlugin.OnFileChanged</c> waits
/// 500 ms and skips its own run if the file changed again in between, so a
/// half-written folder cannot be picked up.</item>
/// </list>
/// This is the same route the developer build has taken hundreds of times; the
/// only difference is who writes the file.
/// </para>
///
/// <para>
/// Nothing here touches the screen reader, the config or any game state: the
/// caller does that after jumping back onto the framework thread. Every method
/// reports failure as a result, never as an exception - HTTP, ZIP and the file
/// system are all external, which is exactly where the error rules permit
/// try-catch.
/// </para>
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string RepoOwner = "derbruedi";
    private const string RepoName  = "ff14-accessibility";

    /// <summary>Release asset carrying the plugin, named
    /// "FF14Accessibility-v&lt;version&gt;.zip" - same asset the installer picks
    /// (InstallerService.UpdateAccessibilityPluginAsync).</summary>
    private const string AssetPrefix = "FF14Accessibility-v";

    /// <summary>
    /// The two files that CANNOT be replaced while the game runs: they are native
    /// libraries, loaded into the process by LoadLibrary, and Windows holds them
    /// open. Measured 2026-09-01 - every other file in the folder was writable,
    /// these two threw.
    ///
    /// <para>They are skipped rather than treated as an error, because they do not
    /// change: both have been byte-identical since 2025-08-16. If one of them ever
    /// DOES differ, <see cref="InstallAsync"/> says so and the player is sent to
    /// the installer - silently shipping an update that left the screen reader
    /// bridge behind would be the worst of both worlds.</para>
    /// </summary>
    private static readonly string[] NativeFiles =
    {
        "Tolk.dll",
        "nvdaControllerClient64.dll",
        "nvdaControllerClient32.dll",
    };

    /// <summary>Refuse a single entry past this. The biggest file we ship is a
    /// few megabytes; unpacking an unexpected giant blindly is how an archive
    /// from a compromised source turns into a full disk.</summary>
    private const int MaxEntryBytes = 32 * 1024 * 1024;

    /// <summary>Refuse the whole archive past this. Same reason, for the sum.</summary>
    private const long MaxTotalBytes = 128L * 1024 * 1024;

    private readonly IPluginLog _log;
    private readonly HttpClient _http;
    private readonly string _pluginDirectory;
    private readonly string _mainDllName;

    public UpdateService(IPluginLog log, string assemblyLocation)
    {
        _log = log;
        _pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
        _mainDllName = Path.GetFileName(assemblyLocation);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        // GitHub answers requests without a user agent, but not reliably and not
        // with a useful error when it declines.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14Accessibility (Dalamud plugin)");
    }

    /// <summary>True while a check or an install runs, so a second trigger is
    /// refused instead of writing the same folder from two threads.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// What the last check found, or null if none has run this session.
    ///
    /// <para>It lives here rather than in the menu because the menu is rebuilt
    /// from scratch every time it opens - a result held there would vanish
    /// between "check" and "install", which are two separate visits.</para>
    /// </summary>
    public UpdateCheckResult? LastCheck { get; private set; }

    /// <summary>
    /// The version that is RUNNING, read from the loaded assembly rather than
    /// from the manifest next to it. The manifest is a file on disk and can
    /// already describe an update that has not been loaded yet; the assembly
    /// cannot lie about itself.
    /// </summary>
    public string LocalVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Whether this installation can update itself: only a DevPlugin folder is
    /// ours to write.
    ///
    /// <para>A plugin pulled from the Dalamud repository lives in
    /// <c>installedPlugins</c>, where Dalamud owns the files, tracks versions and
    /// would overwrite whatever we put there on its next update. Writing into
    /// that folder would produce a plugin whose files and whose bookkeeping
    /// disagree.</para>
    /// </summary>
    public bool CanSelfUpdate =>
        _pluginDirectory.Contains("devPlugins", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asks GitHub for the newest release. Never throws.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken token = default)
    {
        if (IsRunning) return UpdateCheckResult.Failed("bereits laufend");
        IsRunning = true;

        try
        {
            var apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            _log.Info($"[Update] Frage neueste Fassung ab: {apiUrl}");

            var json = await _http.GetStringAsync(apiUrl, token).ConfigureAwait(false);
            var release = JsonNode.Parse(json);

            var tag = release?["tag_name"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(tag))
            {
                _log.Error("[Update] Antwort von GitHub enthaelt kein 'tag_name'.");
                return UpdateCheckResult.Failed("Antwort ohne Versionsangabe");
            }

            var remote = tag.TrimStart('v', 'V').Trim();
            var url = FindAssetUrl(release);
            if (url == null)
            {
                // Reached GitHub, got a release, found no plugin in it: the naming
                // upstream changed. A real error - it must not read as "no update".
                _log.Error($"[Update] Release {tag} enthaelt kein Asset '{AssetPrefix}*.zip'.");
                return UpdateCheckResult.Failed("Release ohne passende Datei");
            }

            var local = LocalVersion;
            var newer = IsNewer(remote, local);
            _log.Info($"[Update] Installiert {local}, angeboten {remote}, neuer={newer}.");
            LastCheck = UpdateCheckResult.Succeeded(local, remote, url, newer);
            return LastCheck;
        }
        catch (OperationCanceledException)
        {
            _log.Info("[Update] Abfrage abgebrochen.");
            return UpdateCheckResult.Failed("abgebrochen");
        }
        catch (Exception ex)
        {
            _log.Error($"[Update] Abfrage fehlgeschlagen: {ex.Message}");
            return UpdateCheckResult.Failed(ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Downloads the release and writes it into the plugin folder. Never throws.
    ///
    /// <para>
    /// NOTHING IS WRITTEN UNTIL EVERYTHING IS READ, and the main DLL is written
    /// LAST. The first rule keeps a network drop from leaving a half-updated
    /// folder; the second one decides WHEN Dalamud reloads, because the main DLL
    /// is the only file it watches. Reversed, the reload would start on a folder
    /// still being filled.
    /// </para>
    /// </summary>
    public async Task<UpdateInstallResult> InstallAsync(string downloadUrl,
                                                        CancellationToken token = default)
    {
        if (IsRunning) return UpdateInstallResult.Failed("bereits laufend");
        if (!CanSelfUpdate) return UpdateInstallResult.Failed("kein DevPlugin-Ordner");
        IsRunning = true;

        try
        {
            _log.Info($"[Update] Lade Archiv: {downloadUrl}");
            var archive = await _http.GetByteArrayAsync(downloadUrl, token).ConfigureAwait(false);
            _log.Info($"[Update] Archiv geladen: {archive.Length} Bytes.");

            var files = ExtractFiles(archive);
            if (files.Count == 0)
            {
                _log.Error("[Update] Archiv enthaelt keine Dateien - Aufbau der Quelle geaendert.");
                return UpdateInstallResult.Failed("leeres Archiv");
            }

            if (!files.ContainsKey(_mainDllName))
            {
                // Without our own DLL the archive is not a plugin release, and
                // writing the rest would leave the folder mismatched.
                _log.Error($"[Update] Archiv enthaelt '{_mainDllName}' nicht - wird nicht eingespielt.");
                return UpdateInstallResult.Failed("Archiv ohne Plugin-DLL");
            }

            // Native libraries cannot be replaced while the process holds them.
            // Only worth mentioning when they actually DIFFER - see NativeFiles.
            var blocked = files.Keys
                               .Where(IsNativeFile)
                               .Where(name => DiffersOnDisk(name, files[name]))
                               .ToList();
            if (blocked.Count > 0)
            {
                _log.Warning($"[Update] Neue Fassung aendert gesperrte Dateien: {string.Join(", ", blocked)}. " +
                             "Einspielen im laufenden Spiel nicht moeglich.");
                return UpdateInstallResult.NeedsInstaller(blocked);
            }

            // Everything but the main DLL first - none of it wakes the watcher.
            var written = 0;
            foreach (var (name, content) in files)
            {
                if (name == _mainDllName) continue;
                if (IsNativeFile(name)) continue;   // identical anyway, see above
                File.WriteAllBytes(Path.Combine(_pluginDirectory, name), content);
                written++;
            }

            // And now the one file Dalamud is watching. From here on this service
            // must not do anything else: the reload is 500 ms away and takes this
            // assembly with it.
            File.WriteAllBytes(Path.Combine(_pluginDirectory, _mainDllName), files[_mainDllName]);
            written++;

            _log.Info($"[Update] {written} Dateien geschrieben, Plugin-DLL zuletzt - " +
                      "Dalamud laedt in etwa einer halben Sekunde neu.");
            return UpdateInstallResult.Succeeded(written);
        }
        catch (OperationCanceledException)
        {
            _log.Info("[Update] Einspielen abgebrochen.");
            return UpdateInstallResult.Failed("abgebrochen");
        }
        catch (Exception ex)
        {
            _log.Error($"[Update] Einspielen fehlgeschlagen: {ex.Message}");
            return UpdateInstallResult.Failed(ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Picks the plugin archive out of the release's assets.</summary>
    private static string? FindAssetUrl(JsonNode? release)
    {
        var assets = release?["assets"]?.AsArray();
        if (assets == null) return null;

        foreach (var asset in assets)
        {
            var name = asset?["name"]?.GetValue<string>();
            if (name == null) continue;
            if (!name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            return asset!["browser_download_url"]?.GetValue<string>();
        }

        return null;
    }

    /// <summary>
    /// Reads the archive into memory - only its ROOT, one entry per file name.
    ///
    /// <para>
    /// ONLY ROOT ENTRIES, AND THAT IS NOT A SIMPLIFICATION. The release archive
    /// carries one file twice: "System.Speech.dll" at the root and again under
    /// "runtimes/win/lib/net9.0/" (verified against the v5.95 release,
    /// 2026-09-02). Taking both would mean the second one silently overwrites the
    /// first, decided by nothing but their order in the archive. The installer
    /// deploys the root as well (<c>DeployPluginFiles</c> only recurses when the
    /// root is empty), so this keeps both paths installing the same thing.
    /// </para>
    ///
    /// <para>
    /// IT IS ALSO THE PATH-TRAVERSAL GUARD, and the obvious form of that guard
    /// does NOT work: <c>ZipArchiveEntry.Name</c> is ALWAYS the bare file name in
    /// .NET, so comparing it against <c>Path.GetFileName</c> compares a string
    /// with itself and passes for every entry, "../../evil.dll" included. The
    /// directory part only ever appears in <c>FullName</c> - so that is what gets
    /// checked. An archive is foreign input.
    /// </para>
    /// </summary>
    private Dictionary<string, byte[]> ExtractFiles(byte[] archiveBytes)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long total = 0;

        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;   // directory entry

            // Anything below the root - including every form of "..".
            var full = entry.FullName.Replace('\\', '/');
            if (full.Contains('/'))
            {
                _log.Info($"[Update] Eintrag '{entry.FullName}' uebersprungen: nicht im Wurzelverzeichnis.");
                continue;
            }

            var name = entry.Name;
            if (name != Path.GetFileName(name) || Path.IsPathRooted(name))
            {
                _log.Warning($"[Update] Eintrag '{entry.FullName}' uebersprungen: unzulaessiger Dateiname.");
                continue;
            }

            if (entry.Length > MaxEntryBytes)
            {
                _log.Warning($"[Update] Eintrag '{name}' uebersprungen: {entry.Length} Bytes ueberschreiten die Grenze.");
                continue;
            }

            total += entry.Length;
            if (total > MaxTotalBytes)
            {
                _log.Error($"[Update] Archiv ueberschreitet {MaxTotalBytes} Bytes - abgebrochen.");
                return new Dictionary<string, byte[]>();
            }

            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            result[name] = buffer.ToArray();
        }

        return result;
    }

    private static bool IsNativeFile(string name) =>
        NativeFiles.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the archive's copy of a file differs from the one on
    /// disk. Compared by content, not by date: a rebuilt but unchanged file
    /// would otherwise send the player to the installer for nothing.</summary>
    private bool DiffersOnDisk(string name, byte[] content)
    {
        var path = Path.Combine(_pluginDirectory, name);
        if (!File.Exists(path)) return true;

        try
        {
            return !File.ReadAllBytes(path).AsSpan().SequenceEqual(content);
        }
        catch (Exception ex)
        {
            // Unreadable means we cannot claim it is identical.
            _log.Warning($"[Update] '{name}' nicht lesbar ({ex.Message}) - gilt als geaendert.");
            return true;
        }
    }

    /// <summary>
    /// Whether <paramref name="remote"/> is newer. Both sides are padded to four
    /// parts first: <see cref="Version"/> counts unset parts as -1, so "5.97"
    /// would otherwise rank BELOW "5.96.0.0" and the update would never offer
    /// itself. (Same trap the installer documents.)
    /// </summary>
    private static bool IsNewer(string remote, string local)
    {
        var r = ParseLoose(remote);
        var l = ParseLoose(local);
        if (r != null && l != null) return r > l;
        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Releases the HttpClient. A request still in flight is ended by the
    /// caller's cancellation token before this runs (Plugin.Dispose).</summary>
    public void Dispose() => _http.Dispose();

    private static Version? ParseLoose(string s)
    {
        s = s.TrimStart('v', 'V').Trim();
        var parts = s.Split('.');
        if (parts.Length is 0 or > 4) return null;
        while (parts.Length < 4)
        {
            s += ".0";
            parts = s.Split('.');
        }
        return Version.TryParse(s, out var version) ? version : null;
    }
}

/// <summary>What the version check found.</summary>
public sealed class UpdateCheckResult
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public string LocalVersion { get; private init; } = "";
    public string RemoteVersion { get; private init; } = "";
    public string? DownloadUrl { get; private init; }

    /// <summary>Whether the offered version is actually newer than the running
    /// one. False also covers "same" and "older" - both mean nothing to do.</summary>
    public bool IsNewer { get; private init; }

    public static UpdateCheckResult Failed(string error) =>
        new() { Ok = false, Error = error };

    public static UpdateCheckResult Succeeded(string local, string remote, string url, bool newer) =>
        new() { Ok = true, LocalVersion = local, RemoteVersion = remote,
                DownloadUrl = url, IsNewer = newer };
}

/// <summary>What installing the update did.</summary>
public sealed class UpdateInstallResult
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public int FilesWritten { get; private init; }

    /// <summary>Set when the update changes files that cannot be replaced while
    /// the game runs - see <c>UpdateService.NativeFiles</c>. Nothing was written
    /// in that case.</summary>
    public IReadOnlyList<string> BlockedFiles { get; private init; } = Array.Empty<string>();

    public static UpdateInstallResult Failed(string error) =>
        new() { Ok = false, Error = error };

    public static UpdateInstallResult NeedsInstaller(IReadOnlyList<string> blocked) =>
        new() { Ok = false, Error = "gesperrte Dateien", BlockedFiles = blocked };

    public static UpdateInstallResult Succeeded(int files) =>
        new() { Ok = true, FilesWritten = files };
}
