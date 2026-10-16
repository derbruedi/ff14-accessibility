using System.Net;
using FF14AccessibilityInstaller.Russian;

// Optional read-only GitHub CLI exports avoid anonymous API limits in repeated
// integration runs. Package downloads and all installer checks remain unchanged.
internal sealed class ExportedMetadataHandler(string folder) : DelegatingHandler(new HttpClientHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var name = request.RequestUri?.ToString() switch
        {
            ReleaseClient.LatestUrl => "accessibility-release.json",
            ReleaseClient.TranslationUrl => "xivrus-release.json",
            _ => null
        };
        if (name == null) return base.SendAsync(request, token);
        token.ThrowIfCancellationRequested();
        Console.WriteLine("Release metadata from GitHub API export: " + name);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { RequestMessage = request, Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(folder, name))) });
    }
}
