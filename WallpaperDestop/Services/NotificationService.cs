using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Toolkit.Uwp.Notifications;

namespace WallpaperDestop.Services
{
    /// <summary>
    /// Service to show Windows Toast notifications for wallpaper updates.
    /// Uses Microsoft.Toolkit.Uwp.Notifications for native Windows 10/11 toast support.
    /// </summary>
    public static class NotificationService
    {
        /// <summary>
        /// Shows a success notification when wallpaper is changed successfully.
        /// </summary>
        /// <param name="authorName">Name of the photo author from Unsplash</param>
        /// <param name="imagePath">Optional path to the downloaded image for thumbnail (can be null)</param>
        public static void ShowSuccessNotification(string authorName, string? imagePath = null)
        {
            try
            {
                Debug.WriteLine($"[NotificationService] Showing success notification for author: {authorName}");
                
                var toastBuilder = new ToastContentBuilder()
                    .AddToastActivationInfo("wallpaper_changed", ToastActivationType.Foreground)
                    .AddText("Đổi hình nền thành công!")
                    .AddText($"Ảnh mới từ tác giả {authorName} trên Unsplash");

                // Tùy chọn nâng cao: Thêm thumbnail nếu file ảnh tồn tại
                if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
                {
                    try
                    {
                        // Convert path to proper URI format for toast notification
                        var imageUri = new Uri(imagePath);
                        toastBuilder.AddHeroImage(imageUri);
                        Debug.WriteLine($"[NotificationService] Added hero image: {imageUri}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[NotificationService] Failed to add hero image: {ex.Message}");
                        // Continue without image if failed
                    }
                }

                // Configure toast appearance and behavior
                toastBuilder
                    .AddAttributionText("WallpaperDestop")
                    .SetToastScenario(ToastScenario.Default);

                // Show the toast notification
                toastBuilder.Show();
                
                Debug.WriteLine($"[NotificationService] Toast notification displayed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] ERROR showing notification: {ex.Message}");
                Debug.WriteLine($"[NotificationService] Exception details: {ex}");
            }
        }

        /// <summary>
        /// Shows an error notification when wallpaper change fails.
        /// </summary>
        /// <param name="errorMessage">The error message to display</param>
        public static void ShowErrorNotification(string errorMessage)
        {
            try
            {
                Debug.WriteLine($"[NotificationService] Showing error notification: {errorMessage}");
                
                new ToastContentBuilder()
                    .AddToastActivationInfo("wallpaper_error", ToastActivationType.Foreground)
                    .AddText("Lỗi đổi hình nền")
                    .AddText(errorMessage)
                    .AddAttributionText("WallpaperDestop")
                    .SetToastScenario(ToastScenario.Default)
                    .Show();
                    
                Debug.WriteLine($"[NotificationService] Error toast notification displayed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] ERROR showing error notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a rate limit notification when API quota is exceeded.
        /// </summary>
        public static void ShowRateLimitNotification()
        {
            try
            {
                Debug.WriteLine($"[NotificationService] Showing rate limit notification");
                
                new ToastContentBuilder()
                    .AddToastActivationInfo("rate_limit", ToastActivationType.Foreground)
                    .AddText("Đã hết lượt tải ảnh")
                    .AddText("Vui lòng thử lại sau 1 giờ. Unsplash giới hạn 50 requests/giờ.")
                    .AddAttributionText("WallpaperDestop")
                    .SetToastScenario(ToastScenario.Default)
                    .Show();
                    
                Debug.WriteLine($"[NotificationService] Rate limit toast notification displayed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] ERROR showing rate limit notification: {ex.Message}");
            }
        }
    }
}