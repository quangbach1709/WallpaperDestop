namespace WallpaperDestop.Models;

/// <summary>
/// Represents a photo returned from the Unsplash API.
/// Mapped from the JSON response.
/// </summary>
public sealed class UnsplashPhoto
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AltDescription { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Color { get; set; } = "#000000";
    public UnsplashUrls Urls { get; set; } = new();
    public UnsplashUser User { get; set; } = new();
    public UnsplashLinks Links { get; set; } = new();

    /// <summary>
    /// Display-friendly description, falls back to alt_description.
    /// </summary>
    public string DisplayDescription =>
        !string.IsNullOrWhiteSpace(Description)
            ? Description
            : !string.IsNullOrWhiteSpace(AltDescription)
                ? AltDescription
                : "Untitled";
}

public sealed class UnsplashUrls
{
    public string Raw { get; set; } = string.Empty;
    public string Full { get; set; } = string.Empty;
    public string Regular { get; set; } = string.Empty;
    public string Small { get; set; } = string.Empty;
    public string Thumb { get; set; } = string.Empty;

    /// <summary>
    /// High-quality preview URL for UI display.
    /// Uses Raw with optimized parameters or Full as fallback.
    /// BẮT BUỘC KHÔNG sử dụng Regular/Small (chất lượng thấp).
    /// </summary>
    public string HighQualityPreview
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Raw))
            {
                // Tối ưu cho preview UI: width 800px, quality 75%, format webp (nhẹ hơn)
                return $"{Raw}&w=800&q=75&fm=webp&fit=crop";
            }
            else if (!string.IsNullOrWhiteSpace(Full))
            {
                // Fallback sang Full nếu không có Raw
                return Full;
            }
            else
            {
                // Last resort: Regular (không lý tưởng nhưng tránh lỗi)
                return Regular;
            }
        }
    }
}

public sealed class UnsplashUser
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public UnsplashProfileImage? ProfileImage { get; set; }
}

public sealed class UnsplashProfileImage
{
    public string Small { get; set; } = string.Empty;
    public string Medium { get; set; } = string.Empty;
    public string Large { get; set; } = string.Empty;
}

public sealed class UnsplashLinks
{
    public string Html { get; set; } = string.Empty;
    public string Download { get; set; } = string.Empty;
    public string DownloadLocation { get; set; } = string.Empty;
}

/// <summary>
/// Wrapper for the /photos endpoint (list response is a JSON array).
/// For /search/photos the response is wrapped in an object.
/// </summary>
public sealed class UnsplashSearchResult
{
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public List<UnsplashPhoto> Results { get; set; } = [];
}
