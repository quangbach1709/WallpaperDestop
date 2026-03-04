using System.Windows;
using WallpaperDestop.Services;
using WallpaperDestop.ViewModels;

namespace WallpaperDestop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settingsService = new SettingsService();
        var unsplashService = new UnsplashService(settingsService);
        var cacheService = new CacheService();

        DataContext = new MainViewModel(unsplashService, settingsService, cacheService);
    }
}
