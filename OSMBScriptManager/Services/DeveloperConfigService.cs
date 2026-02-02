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

    public async Task<List<ScriptDeveloper>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new List<ScriptDeveloper>();

        var txt = await File.ReadAllTextAsync(_filePath);
        try
        {
            return JsonSerializer.Deserialize<List<ScriptDeveloper>>(txt) ?? new List<ScriptDeveloper>();
        }
        catch
        {
            return new List<ScriptDeveloper>();
        }
    }

    public async Task SaveAsync(List<ScriptDeveloper> developers)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(developers, opts);
        await File.WriteAllTextAsync(_filePath, txt);
    }
}
