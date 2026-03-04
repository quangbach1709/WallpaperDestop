using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WallpaperDestop.Services;
using WallpaperDestop.ViewModels;

namespace WallpaperDestop;

/// <summary>
/// Interaction logic for App.xaml
/// 
/// Startup behavior:
/// - Normal launch: Shows the main window with photo gallery
/// - Silent auto-run (--autorun): Completely headless operation - fetches random wallpaper, 
///   sets lock screen, shows toast notification, then exits without any UI
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Override OnStartup để xử lý command line arguments và chế độ silent autorun
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Debug.WriteLine($"[App] OnStartup called with {e.Args.Length} arguments");
        foreach (var arg in e.Args)
        {
            Debug.WriteLine($"[App] Argument: {arg}");
        }

        // Kiểm tra tham số --autorun cho chế độ chạy ngầm
        bool isAutorunMode = e.Args.Any(arg => 
            string.Equals(arg, "--autorun", StringComparison.OrdinalIgnoreCase));

        if (isAutorunMode)
        {
            Debug.WriteLine($"[App] Detected --autorun mode - running silently");
            
            // Nhánh 1: Chạy ngầm hoàn toàn - TUYỆT ĐỐI KHÔNG khởi tạo MainWindow
            RunSilentAutorunAsync();
        }
        else
        {
            Debug.WriteLine($"[App] Normal mode - showing MainWindow");
            
            // Nhánh 2: Chế độ bình thường - khởi tạo và hiển thị MainWindow
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }

    /// <summary>
    /// Chế độ chạy ngầm hoàn toàn với Anti-Duplication (--autorun):
    /// 1. Lấy 5 ảnh landscape random từ Unsplash API (/photos/random?orientation=landscape&count=5)
    /// 2. Lọc ảnh chưa dùng dựa trên lịch sử UsedImageIds.json (tối đa 100 ID)
    /// 3. Chọn ảnh đầu tiên chưa dùng, fallback ảnh đầu tiên nếu tất cả đã dùng
    /// 4. Tải ảnh về và set làm lock screen
    /// 5. Lưu ID ảnh vào lịch sử để tránh trùng lặp
    /// 6. Hiển thị Windows Toast notification
    /// 7. Dọn dẹp file ảnh cũ
    /// 8. Tự động shutdown application để không tốn RAM
    /// 
    /// LƯU Ý: Xử lý async/await an toàn trong OnStartup synchronous method
    /// </summary>
    private async void RunSilentAutorunAsync()
    {
        var startTime = DateTime.Now;
        Debug.WriteLine($"[App] Silent autorun started at {startTime}");

        try
        {
            // Khởi tạo services
            var settingsService = new SettingsService();
            
            // Kiểm tra API key trước khi tiếp tục
            if (!settingsService.HasApiKey)
            {
                Debug.WriteLine($"[App] Silent autorun: No API key configured, exiting");
                NotificationService.ShowErrorNotification("Chưa cấu hình Unsplash API Key");
                return;
            }

            using var unsplashService = new UnsplashService(settingsService);
            var imageHistoryService = new ImageHistoryService();

            // Bước 1: Lấy random landscape wallpaper với anti-duplication
            Debug.WriteLine($"[App] Fetching random landscape wallpaper with anti-duplication...");
            var photo = await unsplashService.GetRandomPhotoWithAntiDuplicationAsync("desktop wallpaper", imageHistoryService);

            if (photo == null)
            {
                Debug.WriteLine($"[App] Silent autorun: No photo returned from API");
                NotificationService.ShowErrorNotification("Không thể lấy ảnh từ Unsplash");
                return;
            }

            Debug.WriteLine($"[App] Selected unique photo: {photo.Id} by {photo.User.Name} ({photo.Width}x{photo.Height})");

            // Bước 2: Download và set lock screen
            Debug.WriteLine($"[App] Downloading and setting lock screen...");
            var downloadSuccess = await LockScreenService.DownloadAndSetLockScreenAsync(unsplashService, photo);

            if (downloadSuccess)
            {
                Debug.WriteLine($"[App] Lock screen set successfully!");
                
                // Bước 3: Lưu ID ảnh vào lịch sử để tránh trùng lặp trong tương lai
                Debug.WriteLine($"[App] Adding image ID to history for anti-duplication...");
                await imageHistoryService.AddImageIdAsync(photo.Id);
                
                // Bước 4: Hiển thị success toast notification
                Debug.WriteLine($"[App] Showing success notification...");
                var downloadedImagePath = LockScreenService.GetWallpaperPath(photo.Id);
                NotificationService.ShowSuccessNotification(photo.User.Name, downloadedImagePath);
                
                // QUAN TRỌNG: Đợi 1.5 giây để đảm bảo toast notification được gửi lên Windows
                // trước khi process bị shutdown
                await Task.Delay(1500);
                
                var elapsedTime = DateTime.Now - startTime;
                Debug.WriteLine($"[App] Silent autorun completed successfully in {elapsedTime.TotalSeconds:F1}s");
            }
            else
            {
                Debug.WriteLine($"[App] Failed to set lock screen");
                NotificationService.ShowErrorNotification("Lỗi khi đặt hình nền khóa");
                await Task.Delay(1500); // Vẫn đợi để toast hiển thị
            }
        }
        catch (UnsplashRateLimitException ex)
        {
            Debug.WriteLine($"[App] Silent autorun: Rate limit exceeded - {ex.Message}");
            NotificationService.ShowRateLimitNotification();
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Silent autorun EXCEPTION: {ex}");
            NotificationService.ShowErrorNotification($"Lỗi: {ex.Message}");
            await Task.Delay(1500);
        }
        finally
        {
            // Bước 4: Tự động shutdown để không tốn RAM
            Debug.WriteLine($"[App] Silent autorun shutting down application...");
            Application.Current.Shutdown();
        }
    }
}