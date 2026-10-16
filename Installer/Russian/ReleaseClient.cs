using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FF14AccessibilityInstaller.Russian;

public sealed record PluginRelease(Version Version, string DisplayVersion, string FileName, string Url, string? Sha256, int? ApiLevel = null);

public sealed class ReleaseClient : IDisposable
{
    public const string Repository = "pitpamyati-netizen/ff14-accessibility";
    public const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    public const string VnavUrl = "https://puni.sh/api/repository/veyn";
    public const string PenumbraUrl = "https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json";
    public const string TranslationUrl = "https://api.github.com/repos/xivrus/xiv_ru_weblate/releases/latest";
    private readonly HttpClient http;

    public ReleaseClient(HttpMessageHandler? handler = null)
    {
        http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromMinutes(10);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FF14Accessibility-RU-Installer/1.3.0");
    }

    public async Task<PluginRelease> Latest(CancellationToken token)
    {
        var json = PackageFiles.ReadJson(await GetText(LatestUrl, token));
        if ((bool?)json["draft"] == true || (bool?)json["prerelease"] == true)
            throw new InvalidDataException("GitHub вернул предварительный выпуск вместо стабильного.");
        var tag = (string?)json["tag_name"] ?? "";
        var version = PackageFiles.VersionOf(tag);
        var display = tag.TrimStart('v', 'V');
        var name = $"FF14Accessibility-{display}-RU-no-source.zip";
        var assets = json["assets"] as JArray ?? throw new InvalidDataException("В релизе нет файлов.");
        var asset = assets.SingleOrDefault(a => (string?)a["name"] == name)
            ?? throw new InvalidDataException("В последнем релизе пока нет установочного архива русской версии.");
        var url = (string?)asset["browser_download_url"] ?? "";
        ValidateUrl(url, github: true);
        var digest = (string?)asset["digest"];
        string? sha = digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest[7..] : null;
        if (sha == null)
        {
            var sumsUrl = (string?)assets.SingleOrDefault(a => (string?)a["name"] == "SHA256SUMS.txt")?["browser_download_url"]
                ?? throw new InvalidDataException("В релизе нет контрольной суммы. Установка отменена.");
            ValidateUrl(sumsUrl, github: true);
            var sums = await GetText(sumsUrl, token);
            sha = sums.Split('\n').Select(l => l.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length == 2 && p[1].TrimStart('*') == name).Select(p => p[0]).SingleOrDefault()
                ?? throw new InvalidDataException("Не найдена контрольная сумма архива.");
        }
        return new PluginRelease(version, display, name, url, sha);
    }

    public async Task<PluginRelease> Vnav(CancellationToken token)
    {
        var array = JArray.Parse(await GetText(VnavUrl, token));
        var item = array.SingleOrDefault(p => (string?)p["InternalName"] == "vnavmesh")
            ?? throw new InvalidDataException("vnavmesh отсутствует в официальном каталоге.");
        var display = (string?)item["AssemblyVersion"] ?? "";
        var url = (string?)item["DownloadLinkInstall"] ?? "";
        ValidateUrl(url, github: false);
        return new PluginRelease(PackageFiles.VersionOf(display), display, "vnavmesh.zip", url, null,
            (int?)item["DalamudApiLevel"] ?? throw new InvalidDataException("Не указана совместимость vnavmesh."));
    }

    public static void ValidateUrl(string url, bool github)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort ||
            (github ? uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal)
                    : uri.Host != "puni.sh" || !uri.AbsolutePath.StartsWith("/api/plugins/download/", StringComparison.Ordinal)))
            throw new InvalidDataException("Адрес скачивания не принадлежит ожидаемому источнику.");
    }

    public async Task<PluginRelease> Penumbra(CancellationToken token)
    {
        var item = JArray.Parse(await GetText(PenumbraUrl, token)).Single(p => (string?)p["InternalName"] == "Penumbra");
        var version = (string?)item["AssemblyVersion"] ?? "";
        var url = (string?)item["DownloadLinkInstall"] ?? "";
        ValidateRepositoryUrl(url, "xivdev/Penumbra");
        return new(PackageFiles.VersionOf(version), version, "Penumbra.zip", url, null,
            (int?)item["DalamudApiLevel"] ?? throw new InvalidDataException("Не указана совместимость Penumbra."));
    }

    public async Task<PluginRelease> Translation(CancellationToken token)
    {
        var release = PackageFiles.ReadJson(await GetText(TranslationUrl, token));
        if ((bool?)release["draft"] == true || (bool?)release["prerelease"] == true)
            throw new InvalidDataException("XIV Rus вернул предварительный выпуск.");
        var asset = (release["assets"] as JArray)?.SingleOrDefault(a => (string?)a["name"] == "release.pmp")
            ?? throw new InvalidDataException("В официальном выпуске XIV Rus не найден release.pmp.");
        var url = (string?)asset["browser_download_url"] ?? "";
        ValidateRepositoryUrl(url, "xivrus/xiv_ru_weblate");
        var digest = (string?)asset["digest"];
        if (digest == null || !System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("Сервер не предоставил контрольную сумму XIV Rus. Повторите позже.");
        var version = ((string?)release["tag_name"] ?? "").TrimStart('v', 'V');
        return new(PackageFiles.VersionOf(version), version, "release.pmp", url, digest[7..]);
    }

    private static void ValidateRepositoryUrl(string url, string repository)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "github.com" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/" + repository + "/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("Адрес загрузки не принадлежит официальному источнику.");
    }

    private async Task<string> GetText(string url, CancellationToken token)
    {
        using var output = new MemoryStream();
        await Transfer(url, output, 4 * 1024 * 1024, null, token);
        return Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
    }

    public async Task Download(PluginRelease release, string destination, Action<string> log, CancellationToken token,
        long limit = 256L * 1024 * 1024)
    {
        using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await Transfer(release.Url, output, limit, log, token);
        if (release.Sha256 != null) { PackageFiles.VerifyHash(destination, release.Sha256); log("Контрольная сумма архива совпала."); }
    }

    private async Task Transfer(string url, Stream output, long limit, Action<string>? log, CancellationToken token)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Сервер ограничил запросы. Повторите проверку позже.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Сервер загрузки ответил ошибкой {(int)response.StatusCode}. Повторите позже.");
        if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new IOException("Скачивание требует защищённого соединения HTTPS.");
        var length = response.Content.Headers.ContentLength;
        if (length > limit) throw new InvalidDataException("Скачиваемый файл слишком велик.");
        using var input = await response.Content.ReadAsStreamAsync(token);
        var buffer = new byte[81920];
        long count = 0;
        var last = -1;
        // With ResponseHeadersRead the HttpClient timeout ends at the headers.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        int read;
        while ((read = await input.ReadAsync(buffer, timeout.Token)) != 0)
        {
            count += read;
            if (count > limit) throw new InvalidDataException("Скачиваемый файл слишком велик.");
            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            var step = length > 0 ? (int)(count * 10 / length.Value) : (int)(count / (5 * 1024 * 1024));
            if (step != last) { last = step; log?.Invoke(length > 0 ? $"Скачивание: {Math.Min(100, step * 10)}%." : $"Скачано {count / 1024 / 1024} МБ."); }
        }
        if (length != null && count != length) throw new IOException("Скачивание оборвалось до конца файла.");
    }

    public void Dispose() => http.Dispose();
}
