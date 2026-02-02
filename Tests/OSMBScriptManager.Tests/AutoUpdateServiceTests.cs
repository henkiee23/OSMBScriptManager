using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using OSMBScriptManager.Services;
using Xunit;

namespace OSMBScriptManager.Tests;

internal class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }
}

public class AutoUpdateServiceTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_PicksSetupWinX64Asset()
    {
                var json = "{\n  \"tag_name\": \"v1.2.3\",\n  \"html_url\": \"https://github.com/owner/repo/releases/tag/v1.2.3\",\n  \"assets\": [\n    { \"name\": \"SomeApp-Setup-win-x64.exe\", \"browser_download_url\": \"https://example.org/download/setup.exe\" },\n    { \"name\": \"Other.zip\", \"browser_download_url\": \"https://example.org/download/other.zip\" }\n  ]\n}";

        var handler = new FakeHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return resp;
        });

        var client = new HttpClient(handler);
        var svc = new AutoUpdateService(client);

        var info = await svc.GetLatestReleaseAsync("owner", "repo");

        Assert.NotNull(info);
        Assert.Equal("v1.2.3", info.TagName);
        Assert.Equal("https://example.org/download/setup.exe", info.AssetUrl);
        Assert.Equal("SomeApp-Setup-win-x64.exe", info.AssetName);
    }

    [Fact]
    public async Task DownloadAssetAsync_WritesFileAndReportsProgress()
    {
        var data = new byte[10000];
        new Random(42).NextBytes(data);

        var handler = new FakeHandler(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data)
            };
            resp.Content.Headers.ContentLength = data.Length;
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return resp;
        });

        var client = new HttpClient(handler);
        var svc = new AutoUpdateService(client);

        var dest = Path.Combine(Path.GetTempPath(), "download_test_" + Path.GetRandomFileName() + ".bin");
        try
        {
            double lastProgress = 0;
            var progress = new Progress<double>(p => lastProgress = p);

            var result = await svc.DownloadAssetAsync("https://example.org/download/setup.exe", dest, progress);

            Assert.Equal(dest, result);
            Assert.True(File.Exists(dest));
            var written = await File.ReadAllBytesAsync(dest);
            Assert.Equal(data.Length, written.Length);
            Assert.True(lastProgress > 0);
        }
        finally
        {
            try { File.Delete(dest); } catch { }
        }
    }
}
