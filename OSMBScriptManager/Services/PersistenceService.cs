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
    private readonly string _fallbackFilePath;

    public PersistenceService(string? filePath = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "OSMBScriptManager");
        _fallbackFilePath = Path.Combine(appFolder, "plugin_state.json");

        _filePath = filePath ?? _fallbackFilePath;
    }

    public async Task<Dictionary<string, string>> LoadStateAsync()
    {
        // Try primary path first, then fallback path if primary doesn't exist or is unreadable.
        if (File.Exists(_filePath))
        {
            try
            {
                var txt = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(txt) ?? new Dictionary<string, string>();
            }
            catch
            {
                // ignore and try fallback
            }
        }

        if (!string.Equals(_fallbackFilePath, _filePath, StringComparison.OrdinalIgnoreCase) && File.Exists(_fallbackFilePath))
        {
            try
            {
                var txt = await File.ReadAllTextAsync(_fallbackFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(txt) ?? new Dictionary<string, string>();
            }
            catch
            {
                // ignore and return empty
            }
        }

        return new Dictionary<string, string>();
    }

    public async Task SaveStateAsync(Dictionary<string, string> state)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(state, opts);

        // Try writing to the primary path first. If that fails due to permissions or IO errors,
        // attempt to write to the fallback path under LocalAppData.
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(_filePath, txt);
            return;
        }
        catch (UnauthorizedAccessException)
        {
            // fall through to fallback
        }
        catch (IOException)
        {
            // fall through to fallback
        }

        try
        {
            var dir = Path.GetDirectoryName(_fallbackFilePath) ?? Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(_fallbackFilePath, txt);

            // Create a small marker file to indicate fallback was used so the UI can notify the user.
            try
            {
                var marker = Path.Combine(Path.GetDirectoryName(_fallbackFilePath) ?? dir ?? string.Empty, "persistence_fallback.txt");
                await File.WriteAllTextAsync(marker, $"Fallback used: {_fallbackFilePath}");
            }
            catch { }
        }
        catch
        {
            // Swallow exceptions to avoid crashing the app; caller can continue without persistence.
        }
    }
}
