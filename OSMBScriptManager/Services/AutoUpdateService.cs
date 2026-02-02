using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace OSMBScriptManager.Services;

public class ReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string AssetUrl { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
}

public class AutoUpdateService
{
    private readonly HttpClient _http;

    public AutoUpdateService() : this(new HttpClient()) { }

    // Allow injecting a custom HttpClient for testing
    public AutoUpdateService(HttpClient client)
    {
        _http = client ?? throw new ArgumentNullException(nameof(client));
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent", "OSMBScriptManager-Updater");
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        using var s = await resp.Content.ReadAsStreamAsync();
        var doc = await JsonDocument.ParseAsync(s);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tag_name", out var tag)) return null;
        var info = new ReleaseInfo { TagName = tag.GetString() ?? string.Empty };
        if (root.TryGetProperty("html_url", out var html)) info.HtmlUrl = html.GetString() ?? string.Empty;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var nameProp)) continue;
                var name = nameProp.GetString() ?? string.Empty;
                // prefer windows installer x64 produced by the CI
                if (name.Contains("Setup") && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("win-x64"))
                {
                    if (a.TryGetProperty("browser_download_url", out var dl))
                    {
                        info.AssetUrl = dl.GetString() ?? string.Empty;
                        info.AssetName = name;
                        return info;
                    }
                }
            }

            // fallback: take first exe
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var nameProp)) continue;
                var name = nameProp.GetString() ?? string.Empty;
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (a.TryGetProperty("browser_download_url", out var dl))
                    {
                        info.AssetUrl = dl.GetString() ?? string.Empty;
                        info.AssetName = name;
                        return info;
                    }
                }
            }
        }

        return info.AssetUrl != string.Empty ? info : null;
    }

    public async Task<string> DownloadAssetAsync(string url, string destinationPath, IProgress<double>? progress = null)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;
        using var src = await resp.Content.ReadAsStreamAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? Path.GetTempPath());
        using var dst = File.OpenWrite(destinationPath);
        var buffer = new byte[81920];
        long read = 0;
        while (true)
        {
            var n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (n == 0) break;
            await dst.WriteAsync(buffer.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }

        return destinationPath;
    }
}
