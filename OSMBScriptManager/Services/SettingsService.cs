using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OSMBScriptManager.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public class Settings
{
    public string TargetDirectory { get; set; } = string.Empty;
    public bool FollowSystemTheme { get; set; } = true;
    public ThemePreference PreferredTheme { get; set; } = ThemePreference.System;
}

public class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            _filePath = filePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "OSMBScriptManager");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "settings.json");
        }
    }

    public async Task<Settings> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new Settings();

        var txt = await File.ReadAllTextAsync(_filePath);
        try
        {
            return JsonSerializer.Deserialize<Settings>(txt) ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public async Task SaveAsync(Settings settings)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        var txt = JsonSerializer.Serialize(settings, opts);
        await File.WriteAllTextAsync(_filePath, txt);
    }
}
