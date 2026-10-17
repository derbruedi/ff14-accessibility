using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;

namespace FF14Accessibility.Services;

/// <summary>
/// Downloads <see cref="HuntSpawnCatalog.SourceUrl"/> into the plugin config
/// folder. The CSV is GPL-3 and stays out of the repository and the release
/// zip. The player's plugin fetches it the same way it fetches dungeon routes.
/// </summary>
public sealed class HuntSpawnDownloadService : IDisposable
{
    /// <summary>The measured file is about 894 KB. Anything past this is not
    /// that list.</summary>
    private const int MaxBytes = 4 * 1024 * 1024;

    private readonly IPluginLog _log;
    private readonly HttpClient _http;

    public HuntSpawnDownloadService(IPluginLog log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14Accessibility (Dalamud plugin)");
    }

    public bool IsRunning { get; private set; }

    /// <summary>Writes the file only after the whole body has been read and
    /// parsed into at least one spawn. Never throws.</summary>
    public async Task<HuntSpawnFetchResult> FetchAsync(string targetFile, CancellationToken token = default)
    {
        if (IsRunning) return HuntSpawnFetchResult.Failed("bereits laufend");
        IsRunning = true;

        try
        {
            _log.Info($"[Jagd] Standortliste wird geladen: {HuntSpawnCatalog.SourceUrl}");
            using var response = await _http.GetAsync(
                HuntSpawnCatalog.SourceUrl, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(), token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaxBytes)
                {
                    _log.Error("[Jagd] Standortliste ueberschreitet die Groessengrenze. Nichts wird geschrieben.");
                    return HuntSpawnFetchResult.Failed("zu gross");
                }

                buffer.Write(chunk, 0, read);
            }

            var text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            var parsed = HuntSpawnCatalog.Parse(text);
            if (parsed.PointCount == 0)
            {
                _log.Error("[Jagd] Standortliste enthaelt keine verwendbare Zeile. Nichts wird geschrieben.");
                return HuntSpawnFetchResult.Failed("keine Standorte");
            }

            var folder = Path.GetDirectoryName(targetFile);
            if (string.IsNullOrEmpty(folder))
                return HuntSpawnFetchResult.Failed("kein Ordner");

            Directory.CreateDirectory(folder);
            var temp = targetFile + ".tmp";
            await File.WriteAllBytesAsync(temp, buffer.ToArray(), token).ConfigureAwait(false);
            File.Move(temp, targetFile, overwrite: true);
            _log.Info($"[Jagd] {parsed.PointCount} Standorte geschrieben nach '{targetFile}'.");
            return HuntSpawnFetchResult.Succeeded(parsed.PointCount);
        }
        catch (OperationCanceledException)
        {
            _log.Info("[Jagd] Laden der Standortliste abgebrochen.");
            return HuntSpawnFetchResult.Failed("abgebrochen");
        }
        catch (Exception ex)
        {
            _log.Error($"[Jagd] Standortliste konnte nicht geladen werden: {ex.Message}");
            return HuntSpawnFetchResult.Failed(ex.Message);
        }
        finally
        {
            IsRunning = false;
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Outcome of a spawn-list fetch. <see cref="Error"/> is for the log.
/// The player hears HuntSpawnsFailed, not this text.</summary>
public readonly record struct HuntSpawnFetchResult(bool Ok, int Points, string? Error)
{
    public static HuntSpawnFetchResult Succeeded(int points) => new(true, points, null);
    public static HuntSpawnFetchResult Failed(string error) => new(false, 0, error);
}
