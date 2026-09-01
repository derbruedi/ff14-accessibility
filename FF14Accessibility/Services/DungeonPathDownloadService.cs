using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Fetches the dungeon path files that <see cref="DungeonRouteService"/> reads.
///
/// <para>
/// WHY THIS EXISTS. The route category was released in v5.94 and did not work
/// for a single player: it only appears where a path file covers the current
/// zone, the files are deliberately not shipped, and nothing ever told anyone
/// that a folder had to be filled. For a blind player that is the worst possible
/// shape of failure - the category is simply absent, which is indistinguishable
/// from a feature that is broken. The fix is not a sentence in a readme nobody
/// gets read aloud, it is the plugin filling its own folder.
/// </para>
///
/// <para>
/// WHAT THIS DOES AND DOES NOT CHANGE ABOUT THE LICENCE QUESTION. The mod still
/// ships nothing: the files are downloaded ON THE PLAYER'S MACHINE, from the
/// upstream repository they were recorded in, at the moment the player's own
/// plugin asks for them. That is the same relationship a package manager has to
/// what it installs. It is NOT a licence - the upstream repository names none,
/// which means all rights reserved - and this was a deliberate decision by the
/// mod author (2026-08-31), not an oversight.
/// </para>
///
/// <para>
/// ONE REQUEST, NOT THREE HUNDRED. The source has 309 path files. Asking for
/// them one by one would be 309 requests against a rate limit, would take
/// minutes, and would leave a half filled folder whenever it failed in the
/// middle. The repository's own zip snapshot is a single 750 KB download that
/// carries all of them, so that is what is fetched; everything outside the path
/// folder is discarded unread.
/// </para>
/// </summary>
public sealed class DungeonPathDownloadService : IDisposable
{
    /// <summary>
    /// The zip snapshot of the upstream repository's default branch.
    ///
    /// The ACTIVE fork, not the original: github.com/ffxivcode/AutoDuty is
    /// archived and its path folder stops where its archiving did, while
    /// erdelf/AutoDuty is the fork the project itself points at and is where new
    /// duties keep arriving (309 files vs. the 254 an older copy holds).
    /// </summary>
    private const string SourceUrl =
        "https://codeload.github.com/erdelf/AutoDuty/zip/refs/heads/master";

    /// <summary>
    /// The folder inside the archive that holds the path files. Matched as a
    /// SUBSTRING of the entry path, never as a prefix: a GitHub zip wraps
    /// everything in a "&lt;repo&gt;-&lt;branch&gt;/" directory, so a prefix match would
    /// break the day the default branch is renamed - silently, and into "no
    /// files found" rather than into an error.
    /// </summary>
    private const string PathsFolder = "/AutoDuty/Paths/";

    /// <summary>Refuse an entry larger than this. A path file is a few kilobytes;
    /// anything near this is not one, and unpacking it blindly is how an archive
    /// from a compromised source turns into a full disk.</summary>
    private const int MaxEntryBytes = 2 * 1024 * 1024;

    /// <summary>Refuse the whole archive past this. Same reason, for the sum.</summary>
    private const long MaxTotalBytes = 64L * 1024 * 1024;

    private readonly IPluginLog _log;
    private readonly HttpClient _http;

    public DungeonPathDownloadService(IPluginLog log)
    {
        _log = log;
        _http = new HttpClient
        {
            // Long enough for a slow line to finish 750 KB, short enough that a
            // dead host does not leave the task hanging for the session.
            Timeout = TimeSpan.FromSeconds(90),
        };
        // GitHub answers requests without a user agent, but not reliably and not
        // with a useful error when it declines. Naming ourselves also means the
        // traffic is attributable rather than anonymous.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14Accessibility (Dalamud plugin)");
    }

    /// <summary>True while a fetch is running, so a second trigger is refused
    /// instead of writing the same folder from two threads.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Downloads the path files into <paramref name="targetFolder"/> and reports
    /// how many were written.
    ///
    /// <para>
    /// NOTHING IS WRITTEN UNTIL EVERYTHING IS READ. The archive is unpacked into
    /// memory first and only then hits the disk. A network drop halfway through
    /// would otherwise leave a folder holding half the dungeons, and the
    /// category would then be present in some zones and absent in others with no
    /// way for the player to tell why.
    /// </para>
    ///
    /// <para>
    /// Never throws. HTTP, the archive format and the file system are all
    /// external, this is exactly the try-catch the error rules allow, and the
    /// caller gets the reason in the result rather than in an exception.
    /// </para>
    /// </summary>
    public async Task<DungeonPathFetchResult> FetchAsync(string targetFolder,
                                                         CancellationToken token = default)
    {
        if (IsRunning) return DungeonPathFetchResult.Failed("bereits laufend");
        IsRunning = true;

        try
        {
            _log.Info($"[Dungeon] Wegdateien werden geladen: {SourceUrl}");
            var archive = await _http.GetByteArrayAsync(SourceUrl, token).ConfigureAwait(false);
            _log.Info($"[Dungeon] Archiv geladen: {archive.Length} Bytes.");

            var files = ExtractPathFiles(archive);
            if (files.Count == 0)
            {
                // Reached the source, got an archive, found nothing in it: the
                // layout upstream changed. That is a real error and must not read
                // as "downloaded, 0 files" - it needs somebody to look.
                _log.Error($"[Dungeon] Archiv enthaelt keine Datei unter '{PathsFolder}' - " +
                           "die Quelle hat ihren Aufbau geaendert.");
                return DungeonPathFetchResult.Failed("keine Wegdateien im Archiv");
            }

            Directory.CreateDirectory(targetFolder);
            foreach (var (name, content) in files)
                File.WriteAllBytes(Path.Combine(targetFolder, name), content);

            _log.Info($"[Dungeon] {files.Count} Wegdateien geschrieben nach '{targetFolder}'.");
            return DungeonPathFetchResult.Succeeded(files.Count);
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a fault. Logged as information so it does not sit in
            // the log looking like a failure to chase.
            _log.Info("[Dungeon] Laden der Wegdateien abgebrochen.");
            return DungeonPathFetchResult.Failed("abgebrochen");
        }
        catch (Exception ex)
        {
            _log.Error($"[Dungeon] Wegdateien konnten nicht geladen werden: {ex.Message}");
            return DungeonPathFetchResult.Failed(ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Picks the path files out of the archive.
    ///
    /// <para>
    /// THE ENTRY'S FOLDER IS NEVER USED AS A DESTINATION, only its bare file
    /// name, and a name that still carries a separator or a drive is dropped. An
    /// archive is foreign input; an entry named "..\..\something.json" is how
    /// unpacking one writes outside the folder it was pointed at.
    /// </para>
    /// </summary>
    private List<(string Name, byte[] Content)> ExtractPathFiles(byte[] archiveBytes)
    {
        var result = new List<(string, byte[])>();
        long total = 0;

        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            var full = entry.FullName.Replace('\\', '/');
            if (full.IndexOf(PathsFolder, StringComparison.Ordinal) < 0) continue;
            if (!full.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            var name = Path.GetFileName(entry.Name);
            if (string.IsNullOrEmpty(name) || name != entry.Name)
            {
                _log.Warning($"[Dungeon] Eintrag '{entry.FullName}' uebersprungen: unzulaessiger Dateiname.");
                continue;
            }

            if (entry.Length > MaxEntryBytes)
            {
                _log.Warning($"[Dungeon] Eintrag '{name}' uebersprungen: {entry.Length} Bytes ueberschreiten die Grenze.");
                continue;
            }

            total += entry.Length;
            if (total > MaxTotalBytes)
            {
                _log.Error("[Dungeon] Archiv ueberschreitet die Gesamtgrenze - Abbruch, nichts wird geschrieben.");
                return new List<(string, byte[])>();
            }

            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            result.Add((name, buffer.ToArray()));
        }

        return result;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// What a fetch did. A record rather than a bare int because "0 files" and
/// "failed" have to stay tellable apart - they sound identical to a player and
/// mean entirely different things.
/// </summary>
/// <param name="Ok">Whether the folder now holds the files.</param>
/// <param name="Files">How many were written. Only meaningful when <paramref name="Ok"/>.</param>
/// <param name="Error">Why it failed, for the log. Never spoken - the player gets
/// the sentence from AccessibilityStrings, not an exception message in English.</param>
public readonly record struct DungeonPathFetchResult(bool Ok, int Files, string? Error)
{
    public static DungeonPathFetchResult Succeeded(int files) => new(true, files, null);
    public static DungeonPathFetchResult Failed(string error) => new(false, 0, error);
}
