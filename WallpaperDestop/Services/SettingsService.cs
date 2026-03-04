using System.IO;
using System.Text.Json;
using WallpaperDestop.Models;

namespace WallpaperDestop.Services;

/// <summary>
/// Manages reading and writing application settings to a local JSON file.
/// Settings are stored in %LocalAppData%\WallpaperDestop\settings.json
/// </summary>
public sealed class SettingsService
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallpaperDestop");

    private static readonly string SettingsFilePath = Path.Combine(AppFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private AppSettings? _cachedSettings;

    /// <summary>
    /// Load settings from disk. Returns default settings if file does not exist.
    /// </summary>
    public AppSettings Load()
    {
        if (_cachedSettings is not null)
            return _cachedSettings;

        if (!File.Exists(SettingsFilePath))
        {
            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            _cachedSettings = new AppSettings();
        }

        return _cachedSettings;
    }

    /// <summary>
    /// Save settings to disk.
    /// </summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppFolder);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
        _cachedSettings = settings;
    }

    /// <summary>
    /// Convenience: update settings via an action and persist immediately.
    /// </summary>
    public void Update(Action<AppSettings> mutator)
    {
        var settings = Load();
        mutator(settings);
        Save(settings);
    }

    /// <summary>
    /// Returns true if a valid API key is configured.
    /// </summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(Load().UnsplashApiKey);
}
