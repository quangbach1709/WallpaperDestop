using System.Diagnostics;
using System.IO;
using Windows.Storage;
using Windows.System.UserProfile;

namespace WallpaperDestop.Services;

/// <summary>
/// Service to manage the Windows Lock Screen wallpaper.
/// 
/// === GHI CHÚ QUAN TRỌNG VỀ API ===
/// 
/// Có 2 API khác nhau trong WinRT để đổi lock screen:
/// 
/// 1. LockScreen.SetImageFileAsync(IStorageFile)
///    - Đây là API CHÍNH XÁC và ĐÁNG TIN CẬY nhất cho Lock Screen.
///    - Namespace: Windows.System.UserProfile.LockScreen
///    - Hoạt động tốt với cả packaged và unpackaged app.
/// 
/// 2. UserProfilePersonalizationSettings.TrySetLockScreenImageAsync(StorageFile)
///    - API này TỒN TẠI nhưng KHÔNG ĐÁNG TIN CẬY cho Lock Screen.
///    - Trên nhiều máy, nó trả về true nhưng KHÔNG thực sự thay đổi Lock Screen.
///    - API này hoạt động tốt hơn cho Desktop Wallpaper (TrySetWallpaperImageAsync).
///    - => KHÔNG SỬ DỤNG API NÀY cho Lock Screen.
/// 
/// === FLOW ĐÚNG ===
/// 1. Đảm bảo file ảnh tồn tại tại đường dẫn tuyệt đối
/// 2. Convert sang StorageFile qua StorageFile.GetFileFromPathAsync()
/// 3. Gọi LockScreen.SetImageFileAsync(storageFile)
/// </summary>
public sealed class LockScreenService
{
    /// <summary>
    /// Local folder where downloaded wallpapers are stored temporarily.
    /// Sử dụng LocalApplicationData vì:
    /// - WinRT StorageFile API có full quyền truy cập vào thư mục này
    /// - Không cần quyền admin
    /// - Tách biệt theo user
    /// </summary>
    private static readonly string WallpaperCacheFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperDestop",
            "Cache");

    /// <summary>
    /// Gets the path to the wallpaper cache folder.
    /// </summary>
    public static string CacheFolder => WallpaperCacheFolder;

    /// <summary>
    /// Generate a file path for a new wallpaper download.
    /// Luôn trả về đường dẫn tuyệt đối (absolute path).
    /// </summary>
    /// <param name="photoId">Unsplash photo ID used as filename.</param>
    public static string GetCachePath(string photoId)
    {
        Directory.CreateDirectory(WallpaperCacheFolder);
        var path = Path.Combine(WallpaperCacheFolder, $"{photoId}.jpg");

        // Đảm bảo luôn là absolute path
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Delete all previously cached wallpaper images to free disk space.
    /// Call this BEFORE downloading a new wallpaper.
    /// </summary>
    /// <param name="exceptFile">Optional file path to exclude from deletion.</param>
    public static void CleanupOldWallpapers(string? exceptFile = null)
    {
        if (!Directory.Exists(WallpaperCacheFolder))
            return;

        var files = Directory.GetFiles(WallpaperCacheFolder, "*.jpg");
        foreach (var file in files)
        {
            if (exceptFile is not null &&
                string.Equals(
                    Path.GetFullPath(file),
                    Path.GetFullPath(exceptFile),
                    StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(file);
                Debug.WriteLine($"[LockScreenService] Deleted old cache: {file}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LockScreenService] Cannot delete {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Set the Windows Lock Screen image from a local file path.
    /// 
    /// LUỒNG XỬ LÝ:
    /// 1. Validate file tồn tại + là absolute path
    /// 2. Convert sang StorageFile (WinRT)
    /// 3. Gọi LockScreen.SetImageFileAsync() — API chính xác cho Lock Screen
    /// 4. Log chi tiết mọi bước để debug
    /// </summary>
    /// <param name="imagePath">Đường dẫn tuyệt đối đến file .jpg</param>
    /// <returns>True nếu set lock screen thành công.</returns>
    public static async Task<bool> SetLockScreenAsync(string imagePath)
    {
        // ═══ BƯỚC 1: Validate input ═══
        Debug.WriteLine($"[LockScreenService] ══════════════════════════════════════");
        Debug.WriteLine($"[LockScreenService] SetLockScreenAsync called");
        Debug.WriteLine($"[LockScreenService] Input path: {imagePath}");

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            var msg = "Image path is null or empty.";
            Debug.WriteLine($"[LockScreenService] ERROR: {msg}");
            throw new ArgumentException(msg, nameof(imagePath));
        }

        // Đảm bảo đường dẫn là absolute
        imagePath = Path.GetFullPath(imagePath);
        Debug.WriteLine($"[LockScreenService] Absolute path: {imagePath}");

        if (!File.Exists(imagePath))
        {
            var msg = $"File not found at: {imagePath}";
            Debug.WriteLine($"[LockScreenService] ERROR: {msg}");
            throw new FileNotFoundException(msg, imagePath);
        }

        // Log file info
        var fileInfo = new FileInfo(imagePath);
        Debug.WriteLine($"[LockScreenService] File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0:N1} KB)");
        Debug.WriteLine($"[LockScreenService] File extension: {fileInfo.Extension}");
        Debug.WriteLine($"[LockScreenService] File last write: {fileInfo.LastWriteTime}");

        // ═══ BƯỚC 2: Convert sang StorageFile ═══
        StorageFile storageFile;
        try
        {
            Debug.WriteLine($"[LockScreenService] Converting to StorageFile...");
            storageFile = await StorageFile.GetFileFromPathAsync(imagePath);
            Debug.WriteLine($"[LockScreenService] StorageFile created: {storageFile.Path}");
            Debug.WriteLine($"[LockScreenService] StorageFile.ContentType: {storageFile.ContentType}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[LockScreenService] ACCESS DENIED converting to StorageFile!");
            Debug.WriteLine($"[LockScreenService] Exception: {ex.Message}");
            Debug.WriteLine($"[LockScreenService] StackTrace: {ex.StackTrace}");
            throw new InvalidOperationException(
                $"Access denied when reading image file. " +
                $"Ensure the file is in an accessible location (LocalAppData). " +
                $"Path: {imagePath}", ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LockScreenService] FAILED to create StorageFile!");
            Debug.WriteLine($"[LockScreenService] Exception type: {ex.GetType().FullName}");
            Debug.WriteLine($"[LockScreenService] Exception: {ex.Message}");
            Debug.WriteLine($"[LockScreenService] StackTrace: {ex.StackTrace}");
            throw new InvalidOperationException(
                $"Failed to open image file as StorageFile: {ex.Message}", ex);
        }

        // ═══ BƯỚC 3: Set Lock Screen bằng LockScreen.SetImageFileAsync ═══
        try
        {
            Debug.WriteLine($"[LockScreenService] Calling LockScreen.SetImageFileAsync()...");
            await LockScreen.SetImageFileAsync(storageFile);
            Debug.WriteLine($"[LockScreenService] SUCCESS! Lock screen image has been set.");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[LockScreenService] ACCESS DENIED setting lock screen!");
            Debug.WriteLine($"[LockScreenService] This may mean:");
            Debug.WriteLine($"[LockScreenService]   - Group Policy blocks lock screen changes");
            Debug.WriteLine($"[LockScreenService]   - The app needs to run as administrator");
            Debug.WriteLine($"[LockScreenService]   - Windows Spotlight is enforced");
            Debug.WriteLine($"[LockScreenService] Exception: {ex.Message}");
            Debug.WriteLine($"[LockScreenService] StackTrace: {ex.StackTrace}");
            throw new InvalidOperationException(
                "Access denied when setting lock screen. " +
                "Check if Group Policy or Windows Spotlight is blocking changes. " +
                $"Details: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LockScreenService] FAILED to set lock screen!");
            Debug.WriteLine($"[LockScreenService] Exception type: {ex.GetType().FullName}");
            Debug.WriteLine($"[LockScreenService] Exception: {ex.Message}");
            Debug.WriteLine($"[LockScreenService] StackTrace: {ex.StackTrace}");
            throw new InvalidOperationException(
                $"Failed to set lock screen image: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Full workflow: clean old cache -> download new image -> set lock screen.
    /// Mỗi bước được log chi tiết.
    /// </summary>
    public static async Task<bool> DownloadAndSetLockScreenAsync(
        UnsplashService unsplashService,
        Models.UnsplashPhoto photo)
    {
        Debug.WriteLine($"[LockScreenService] ══════════════════════════════════════");
        Debug.WriteLine($"[LockScreenService] DownloadAndSetLockScreenAsync START");
        Debug.WriteLine($"[LockScreenService] Photo ID: {photo.Id}");
        Debug.WriteLine($"[LockScreenService] Photo: {photo.DisplayDescription}");
        Debug.WriteLine($"[LockScreenService] By: {photo.User.Name}");

        // Step 1: Generate cache path (absolute)
        var cachePath = GetCachePath(photo.Id);
        Debug.WriteLine($"[LockScreenService] Cache path: {cachePath}");

        // Step 2: Clean up old cached wallpapers BEFORE downloading
        Debug.WriteLine($"[LockScreenService] Cleaning up old wallpapers...");
        CleanupOldWallpapers();

        // Step 3: Download the new image
        Debug.WriteLine($"[LockScreenService] Downloading image...");
        await unsplashService.DownloadPhotoAsync(photo, cachePath);

        // Step 4: Verify download succeeded
        if (!File.Exists(cachePath))
        {
            Debug.WriteLine($"[LockScreenService] ERROR: Downloaded file not found at {cachePath}");
            throw new FileNotFoundException(
                "Image download completed but file was not found on disk.", cachePath);
        }

        var fileSize = new FileInfo(cachePath).Length;
        Debug.WriteLine($"[LockScreenService] Download complete. File size: {fileSize:N0} bytes");

        if (fileSize == 0)
        {
            Debug.WriteLine($"[LockScreenService] ERROR: Downloaded file is 0 bytes!");
            throw new InvalidOperationException("Downloaded image file is empty (0 bytes).");
        }

        // Step 5: Set as lock screen
        Debug.WriteLine($"[LockScreenService] Setting lock screen...");
        var result = await SetLockScreenAsync(cachePath);

        Debug.WriteLine($"[LockScreenService] DownloadAndSetLockScreenAsync COMPLETE. Result: {result}");
        return result;
    }

    /// <summary>
    /// Get the local file path where a wallpaper would be cached.
    /// Alias for GetCachePath for compatibility with notification service.
    /// </summary>
    /// <param name="photoId">Unsplash photo ID</param>
    /// <returns>Full path to the cached wallpaper file</returns>
    public static string GetWallpaperPath(string photoId)
    {
        return GetCachePath(photoId);
    }
}
