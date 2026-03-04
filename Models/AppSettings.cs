namespace WallpaperDestop.Models;

/// <summary>
/// Application settings persisted as a local JSON file.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Unsplash API access key (client ID).
    /// </summary>
    public string UnsplashApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether the app should start with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; }

    /// <summary>
    /// Number of photos to fetch for the gallery (5-30).
    /// </summary>
    public int PhotoCount { get; set; } = 10;

    /// <summary>
    /// Optional search query for themed wallpapers (e.g. "nature", "city").
    /// Empty means fetch curated/editorial photos.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;
}
