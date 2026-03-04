using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperDestop.Models;

namespace WallpaperDestop.Services;

/// <summary>
/// Service to interact with the Unsplash API for HIGH-QUALITY LANDSCAPE WALLPAPERS.
/// 
/// KEY OPTIMIZATIONS:
/// - BẮT BUỘC sử dụng /search/photos với query "desktop wallpapers" 
/// - BẮT BUỘC orientation=landscape cho tất cả requests
/// - Ưu tiên urls.raw với tham số tối ưu (w=1920&q=80&fit=crop)
/// - Fallback urls.full, TUYỆT ĐỐI KHÔNG dùng regular/small
/// - Preview UI sử dụng urls.raw với w=800&q=75&fm=webp
/// - Anti-duplication: Lấy 5 ảnh/lần, lọc theo lịch sử UsedImageIds.json
/// </summary>
public sealed class UnsplashService : IDisposable
{
    private const string BaseUrl = "https://api.unsplash.com";

    /// <summary>
    /// HttpClient cho Unsplash API calls (có BaseAddress).
    /// </summary>
    private readonly HttpClient _apiClient;

    /// <summary>
    /// HttpClient riêng cho download ảnh (KHÔNG có BaseAddress).
    /// 
    /// LÝ DO: URL ảnh từ Unsplash là absolute URL (https://images.unsplash.com/...).
    /// Nếu dùng HttpClient có BaseAddress để gọi URL tuyệt đối, hành vi
    /// phụ thuộc vào cách .NET resolve URI — có thể gây lỗi hoặc request sai URL.
    /// Tách riêng client đảm bảo download luôn đúng.
    /// </summary>
    private readonly HttpClient _downloadClient;

    private readonly SettingsService _settingsService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UnsplashService(SettingsService settingsService)
    {
        _settingsService = settingsService;

        _apiClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _downloadClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120) // Ảnh lớn cần timeout dài hơn
        };
    }

    /// <summary>
    /// Ensures the Authorization header is set with the current API key.
    /// </summary>
    private void EnsureAuthHeader()
    {
        var apiKey = _settingsService.Load().UnsplashApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Unsplash API Key is not configured. Please set it in Settings.");

        _apiClient.DefaultRequestHeaders.Clear();
        _apiClient.DefaultRequestHeaders.Add("Authorization", $"Client-ID {apiKey}");
        _apiClient.DefaultRequestHeaders.Add("Accept-Version", "v1");
    }

    /// <summary>
    /// Fetch high-quality landscape wallpaper photos with pagination support.
    /// ALWAYS uses /search/photos endpoint with desktop wallpaper queries to ensure landscape orientation.
    /// </summary>
    /// <param name="count">Number of photos per page (1-30)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="query">Search query (optional, defaults to wallpaper-specific terms)</param>
    /// <returns>List of UnsplashPhoto</returns>
    /// <exception cref="UnsplashRateLimitException">When rate limit is exceeded (HTTP 403/429)</exception>
    public async Task<List<UnsplashPhoto>> GetPhotosAsync(int count = 5, int page = 1, string? query = null)
    {
        EnsureAuthHeader();
        count = Math.Clamp(count, 1, 30);
        page = Math.Max(1, page);

        // BẮT BUỘC: Luôn sử dụng search endpoint với query wallpaper để đảm bảo ảnh phù hợp làm hình nền
        var searchQuery = !string.IsNullOrWhiteSpace(query) 
            ? query 
            : "desktop wallpapers"; // Default query cho wallpaper chất lượng cao

        // URL chuẩn theo yêu cầu: /search/photos với orientation=landscape BẮT BUỘC
        var url = $"/search/photos?query={Uri.EscapeDataString(searchQuery)}" +
                  $"&orientation=landscape" +
                  $"&per_page={count}" +
                  $"&page={page}" +
                  $"&order_by=relevant";

        Debug.WriteLine($"[UnsplashService] API URL: {BaseUrl}{url}");

        try
        {
            var response = await _apiClient.GetAsync(url);
            
            // Handle rate limit errors
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new UnsplashRateLimitException(response.StatusCode);
            }
            
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<UnsplashSearchResult>(json, JsonOptions);
            
            var photos = searchResult?.Results ?? [];
            Debug.WriteLine($"[UnsplashService] Fetched {photos.Count} landscape wallpapers");
            
            return photos;
        }
        catch (UnsplashRateLimitException)
        {
            // Re-throw rate limit exceptions
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("429"))
        {
            // Catch HttpRequestExceptions that might contain rate limit errors
            throw new UnsplashRateLimitException(HttpStatusCode.Forbidden, "Đã hết lượt tải ảnh miễn phí. Vui lòng thử lại sau", ex);
        }
    }

    /// <summary>
    /// Fetch a single random landscape wallpaper from Unsplash.
    /// Uses /photos/random endpoint with desktop wallpaper query for optimal results.
    /// </summary>
    /// <exception cref="UnsplashRateLimitException">When rate limit is exceeded (HTTP 403/429)</exception>
    public async Task<UnsplashPhoto?> GetRandomPhotoAsync(string? query = null)
    {
        EnsureAuthHeader();

        // BẮT BUỘC: orientation=landscape và query wallpaper cụ thể
        var searchQuery = !string.IsNullOrWhiteSpace(query) 
            ? query 
            : "desktop wallpaper"; // Default query cho random wallpaper

        // URL chuẩn theo yêu cầu với orientation=landscape BẮT BUỘC
        var url = $"/photos/random?orientation=landscape&query={Uri.EscapeDataString(searchQuery)}";

        Debug.WriteLine($"[UnsplashService] Random API URL: {BaseUrl}{url}");

        try
        {
            var response = await _apiClient.GetAsync(url);
            
            // Handle rate limit errors
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new UnsplashRateLimitException(response.StatusCode);
            }
            
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var photo = JsonSerializer.Deserialize<UnsplashPhoto>(json, JsonOptions);
            
            if (photo != null)
            {
                Debug.WriteLine($"[UnsplashService] Random wallpaper: {photo.Id} ({photo.Width}x{photo.Height})");
            }
            
            return photo;
        }
        catch (UnsplashRateLimitException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("429"))
        {
            throw new UnsplashRateLimitException(HttpStatusCode.Forbidden, "Đã hết lượt tải ảnh miễn phí. Vui lòng thử lại sau", ex);
        }
    }

    /// <summary>
    /// Fetch multiple random landscape wallpapers with anti-duplication filtering.
    /// Tối ưu API: Lấy 5 ảnh trong 1 lần gọi, lọc ảnh chưa dùng, tiết kiệm rate limit.
    /// </summary>
    /// <param name="query">Search query (optional, defaults to "desktop wallpaper")</param>
    /// <param name="imageHistoryService">Service to check for used images</param>
    /// <returns>First unused photo from the batch, or first photo as fallback</returns>
    /// <exception cref="UnsplashRateLimitException">When rate limit is exceeded (HTTP 403/429)</exception>
    public async Task<UnsplashPhoto?> GetRandomPhotoWithAntiDuplicationAsync(string? query = null, ImageHistoryService? imageHistoryService = null)
    {
        EnsureAuthHeader();

        // BẮT BUỘC: orientation=landscape và query wallpaper cụ thể
        var searchQuery = !string.IsNullOrWhiteSpace(query) 
            ? query 
            : "desktop wallpaper"; // Default query cho random wallpaper

        // Tối ưu API: Lấy 5 ảnh trong 1 lần gọi để tiết kiệm rate limit
        var url = $"/photos/random?orientation=landscape&query={Uri.EscapeDataString(searchQuery)}&count=5";

        Debug.WriteLine($"[UnsplashService] Anti-duplication Random API URL: {BaseUrl}{url}");

        try
        {
            var response = await _apiClient.GetAsync(url);
            
            // Handle rate limit errors
            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new UnsplashRateLimitException(response.StatusCode);
            }
            
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            
            // API trả về mảng khi count > 1
            var photos = JsonSerializer.Deserialize<List<UnsplashPhoto>>(json, JsonOptions) ?? new List<UnsplashPhoto>();
            
            Debug.WriteLine($"[UnsplashService] Received {photos.Count} random landscape wallpapers");
            
            if (!photos.Any())
            {
                Debug.WriteLine($"[UnsplashService] No photos returned from random API");
                return null;
            }

            // Log thông tin các ảnh nhận được
            foreach (var photo in photos)
            {
                Debug.WriteLine($"[UnsplashService] Photo {photo.Id}: {photo.Width}x{photo.Height} by {photo.User.Name}");
            }

            // Nếu không có ImageHistoryService, trả về ảnh đầu tiên
            if (imageHistoryService == null)
            {
                Debug.WriteLine($"[UnsplashService] No history service provided, returning first photo: {photos.First().Id}");
                return photos.First();
            }

            // Tìm ảnh ĐẦU TIÊN chưa được sử dụng
            var unusedPhoto = await imageHistoryService.FindFirstUnusedImageAsync(photos, photo => photo.Id);
            
            if (unusedPhoto != null)
            {
                Debug.WriteLine($"[UnsplashService] Selected unused photo: {unusedPhoto.Id} by {unusedPhoto.User.Name}");
                return unusedPhoto;
            }

            // Xử lý ngoại lệ: Cả 5 ảnh đều đã dùng - lấy ảnh đầu tiên làm fallback
            Debug.WriteLine($"[UnsplashService] WARNING: All {photos.Count} photos have been used before!");
            Debug.WriteLine($"[UnsplashService] Using first photo as fallback: {photos.First().Id}");
            return photos.First();
        }
        catch (UnsplashRateLimitException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("403") || ex.Message.Contains("429"))
        {
            throw new UnsplashRateLimitException(HttpStatusCode.Forbidden, "Đã hết lượt tải ảnh miễn phí. Vui lòng thử lại sau", ex);
        }
    }

    /// <summary>
    /// Download a high-resolution wallpaper to a local file path.
    /// 
    /// QUAN TRỌNG:
    /// - Ưu tiên urls.raw với tham số tối ưu cho desktop wallpaper
    /// - Fallback sang urls.full để đảm bảo chất lượng cao
    /// - KHÔNG sử dụng urls.regular hay urls.small (chất lượng thấp)
    /// - Dùng _downloadClient (không có BaseAddress) cho URL tuyệt đối
    /// - Flush + Close FileStream trước khi return để đảm bảo WinRT API hoạt động
    /// - Trigger download tracking endpoint (Unsplash API guideline)
    /// </summary>
    public async Task DownloadPhotoAsync(UnsplashPhoto photo, string destinationPath)
    {
        EnsureAuthHeader();

        Debug.WriteLine($"[UnsplashService] DownloadPhotoAsync START");
        Debug.WriteLine($"[UnsplashService] Photo ID: {photo.Id}");
        Debug.WriteLine($"[UnsplashService] Photo Size: {photo.Width}x{photo.Height}");
        Debug.WriteLine($"[UnsplashService] Destination: {destinationPath}");

        // Trigger download tracking (Unsplash API requirement)
        if (!string.IsNullOrWhiteSpace(photo.Links.DownloadLocation))
        {
            try
            {
                Debug.WriteLine($"[UnsplashService] Triggering download tracking...");
                await _apiClient.GetAsync(photo.Links.DownloadLocation);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UnsplashService] Download tracking failed (non-critical): {ex.Message}");
            }
        }

        // BẮT BUỘC: Sử dụng urls.raw hoặc urls.full, KHÔNG dùng regular/small
        string imageUrl;
        if (!string.IsNullOrWhiteSpace(photo.Urls.Raw))
        {
            // Tùy chọn nâng cao: Raw URL với tham số tối ưu cho desktop wallpaper
            // w=1920: width 1920px (phù hợp màn hình Full HD)
            // q=80: quality 80% (cân bằng giữa chất lượng và file size)  
            // fit=crop: crop để đảm bảo tỷ lệ chính xác
            // fm=jpg: format JPG cho wallpaper
            imageUrl = $"{photo.Urls.Raw}&w=1920&q=80&fit=crop&fm=jpg";
            Debug.WriteLine($"[UnsplashService] Using Raw URL with desktop optimization");
        }
        else if (!string.IsNullOrWhiteSpace(photo.Urls.Full))
        {
            // Fallback: Full resolution (chất lượng gốc)
            imageUrl = photo.Urls.Full;
            Debug.WriteLine($"[UnsplashService] Using Full URL as fallback");
        }
        else
        {
            throw new InvalidOperationException(
                $"Photo {photo.Id} không có URL chất lượng cao (Raw/Full). " +
                "Không thể tải ảnh wallpaper chất lượng tốt.");
        }

        Debug.WriteLine($"[UnsplashService] High-Resolution Download URL: {imageUrl}");

        // Download using the dedicated download client (no BaseAddress)
        using var imageResponse = await _downloadClient.GetAsync(imageUrl);
        imageResponse.EnsureSuccessStatusCode();

        Debug.WriteLine($"[UnsplashService] HTTP {(int)imageResponse.StatusCode} - Content-Length: {imageResponse.Content.Headers.ContentLength}");

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to disk — CRITICAL: flush and close before returning
        // WinRT StorageFile.GetFileFromPathAsync sẽ đọc file này ngay sau đó,
        // nên file PHẢI được đóng hoàn toàn, không còn lock.
        {
            await using var fileStream = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920); // 80KB buffer for better I/O performance

            await imageResponse.Content.CopyToAsync(fileStream);

            // Explicitly flush to ensure all bytes are written to disk
            await fileStream.FlushAsync();
        }
        // FileStream is disposed here — file is fully closed and unlocked

        // Verify file was written correctly
        var writtenFileInfo = new FileInfo(destinationPath);
        Debug.WriteLine($"[UnsplashService] High-resolution wallpaper saved: {writtenFileInfo.Length:N0} bytes");

        if (writtenFileInfo.Length == 0)
        {
            throw new InvalidOperationException(
                $"Downloaded wallpaper file is 0 bytes. URL may be invalid: {imageUrl}");
        }

        Debug.WriteLine($"[UnsplashService] DownloadPhotoAsync COMPLETE");
    }

    public void Dispose()
    {
        _apiClient.Dispose();
        _downloadClient.Dispose();
    }
}