using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WallpaperDestop.Services;

/// <summary>
/// Manages the "Start with Windows" functionality by adding/removing
/// the application from the Windows Registry Run key.
/// 
/// Registry path: HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
/// When auto-started, the app is launched with the "--autorun" argument
/// so it runs completely headless (no UI), fetches a wallpaper, sets lock screen,
/// shows toast notification, then exits automatically.
/// </summary>
public static class StartupService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WallpaperDestop";
    public const string AutorunArgument = "--autorun";

    /// <summary>
    /// Gets the full path to the current executable.
    /// </summary>
    private static string ExePath =>
        Process.GetCurrentProcess().MainModule?.FileName
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WallpaperDestop.exe");

    /// <summary>
    /// Returns true if the app was launched with the --autorun argument.
    /// </summary>
    public static bool IsAutorunMode =>
        Environment.GetCommandLineArgs().Any(
            arg => arg.Equals(AutorunArgument, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Legacy support: Returns true if launched with --silent (old parameter)
    /// </summary>
    public static bool IsSilentMode =>
        Environment.GetCommandLineArgs().Any(
            arg => arg.Equals("--silent", StringComparison.OrdinalIgnoreCase)) || IsAutorunMode;

    /// <summary>
    /// Check if the app is currently registered to start with Windows.
    /// </summary>
    public static bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
                return key?.GetValue(AppName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Register the app to start with Windows (runs with --autorun flag for headless operation).
    /// BẮT BUỘC: Đường dẫn Registry phải có tham số --autorun để chế độ chạy ngầm hoạt động.
    /// </summary>
    public static void Register()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            // BẮT BUỘC nối thêm tham số --autorun theo yêu cầu
            var registryValue = $"\"{ExePath}\" {AutorunArgument}";
            key?.SetValue(AppName, registryValue);
            
            Debug.WriteLine($"[StartupService] Registered autorun: {registryValue}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to register startup entry: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Remove the app from Windows startup.
    /// </summary>
    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key?.GetValue(AppName) is not null)
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to unregister startup entry: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Set the startup registration to match the desired state.
    /// </summary>
    public static void SetStartup(bool enabled)
    {
        if (enabled)
            Register();
        else
            Unregister();
    }
}
