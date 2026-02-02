using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OSMBScriptManager.Models;

namespace OSMBScriptManager.Services;

public class PersistenceService
{
    private readonly string _filePath;

    public PersistenceService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), "plugin_state.json");
    }

    public async Task<Dictionary<string, string>> LoadStateAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();

        var txt = await File.ReadAllTextAsync(_filePath);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(txt) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public async Task SaveStateAsync(Dictionary<string, string> state)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(state, opts);
        await File.WriteAllTextAsync(_filePath, txt);
    }
}
