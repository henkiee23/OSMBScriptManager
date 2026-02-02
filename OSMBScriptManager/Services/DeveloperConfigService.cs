using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OSMBScriptManager.Models;

namespace OSMBScriptManager.Services;

public class DeveloperConfigService
{
    private readonly string _filePath;

    public DeveloperConfigService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), "developers.json");
    }

    public async Task<List<PluginDeveloper>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new List<PluginDeveloper>();

        var txt = await File.ReadAllTextAsync(_filePath);
        try
        {
            return JsonSerializer.Deserialize<List<PluginDeveloper>>(txt) ?? new List<PluginDeveloper>();
        }
        catch
        {
            return new List<PluginDeveloper>();
        }
    }

    public async Task SaveAsync(List<PluginDeveloper> developers)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(developers, opts);
        await File.WriteAllTextAsync(_filePath, txt);
    }
}
