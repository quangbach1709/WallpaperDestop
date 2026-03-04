using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WallpaperDestop.Helpers;
using WallpaperDestop.Models;
using WallpaperDestop.Services;

namespace WallpaperDestop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly UnsplashService _unsplashService;
    private readonly SettingsService _settingsService;
    private readonly CacheService _cacheService;

    public MainViewModel(UnsplashService unsplashService, SettingsService settingsService, CacheService cacheService)
    {
        _unsplashService = unsplashService;
        _settingsService = settingsService;
        _cacheService = cacheService;

        Photos = [];

        LoadPhotosCommand = new AsyncRelayCommand(LoadPhotosAsync, () => !IsBusy && _settingsService.HasApiKey);
        LoadMoreCommand = new AsyncRelayCommand(LoadMorePhotosAsync, () => !IsBusy && !IsLoadingMore && _settingsService.HasApiKey);
        SetLockScreenCommand = new AsyncRelayCommand(SetLockScreenAsync, _ => !IsBusy);
        RandomLockScreenCommand = new AsyncRelayCommand(RandomLockScreenAsync, () => !IsBusy && _settingsService.HasApiKey);
        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(() =>
        {
            IsSettingsOpen = false;
            RelayCommand.RaiseCanExecuteChanged();
        });

        // Auto-load photos on startup
        _ = Task.Run(AutoLoadPhotosAsync);
    }

    // ── Properties ──────────────────────────────────────────────

    private ObservableCollection<UnsplashPhoto> _photos = [];
    public ObservableCollection<UnsplashPhoto> Photos
    {
        get => _photos;
        set => SetProperty(ref _photos, value);
    }

    private UnsplashPhoto? _selectedPhoto;
    public UnsplashPhoto? SelectedPhoto
    {
        get => _selectedPhoto;
        set => SetProperty(ref _selectedPhoto, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isLoadingMore;
    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        set
        {
            SetProperty(ref _isLoadingMore, value);
            OnPropertyChanged(nameof(LoadMoreButtonText));
            RelayCommand.RaiseCanExecuteChanged();
        }
    }

    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string LoadMoreButtonText => IsLoadingMore ? "Đang tải..." : "Tải thêm ảnh";

    private string _statusMessage = "Ready. Configure your API key in Settings to get started.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    private SettingsViewModel? _settingsViewModel;
    public SettingsViewModel SettingsViewModel
    {
        get => _settingsViewModel ??= new SettingsViewModel(_settingsService);
        set => SetProperty(ref _settingsViewModel, value);
    }

    // ── Commands ────────────────────────────────────────────────

    public ICommand LoadPhotosCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand SetLockScreenCommand { get; }
    public ICommand RandomLockScreenCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }

    // ── Command Handlers ────────────────────────────────────────

    /// <summary>
    /// Auto-load photos when app starts: load from cache if valid, otherwise fetch from API
    /// </summary>
    private async Task AutoLoadPhotosAsync()
    {
        await Task.Delay(500); // Small delay to let UI initialize

        if (!_settingsService.HasApiKey)
        {
            StatusMessage = "Please configure your Unsplash API Key in Settings.";
            return;
        }

        try
        {
            // Try loading from cache first
            if (_cacheService.IsCacheValid())
            {
                Debug.WriteLine("[MainViewModel] AutoLoad: Cache is valid, loading from cache");
                var cacheData = await _cacheService.LoadCacheAsync();
                
                if (cacheData?.Photos?.Count > 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Photos.Clear();
                        foreach (var photo in cacheData.Photos)
                        {
                            Photos.Add(photo);
                        }
                        CurrentPage = cacheData.CurrentPage;
                    });

                    StatusMessage = $"Loaded {cacheData.Photos.Count} photos from cache.";
                    Debug.WriteLine($"[MainViewModel] AutoLoad: Loaded {cacheData.Photos.Count} photos from cache");
                    return;
                }
            }

            // Cache not valid or empty, fetch from API
            Debug.WriteLine("[MainViewModel] AutoLoad: Cache not valid, fetching from API");
            await LoadInitialPhotosAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] AutoLoad ERROR: {ex}");
            Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = $"Auto-load failed: {ex.Message}";
            });
        }
    }

    /// <summary>
    /// Load initial 5 photos (page 1) and cache them
    /// </summary>
    private async Task LoadInitialPhotosAsync()
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                IsBusy = true;
                StatusMessage = "Loading photos...";
            });

            var settings = _settingsService.Load();
            Debug.WriteLine($"[MainViewModel] LoadInitial: Loading 5 photos, query='{settings.SearchQuery}'");

            var photos = await _unsplashService.GetPhotosAsync(count: 5, page: 1, query: settings.SearchQuery);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Photos.Clear();
                foreach (var photo in photos)
                    Photos.Add(photo);
                
                CurrentPage = 1;
                StatusMessage = $"Loaded {photos.Count} landscape photos.";
            });

            // Cache the results
            await _cacheService.SaveCacheAsync(photos.ToList(), 1);
            Debug.WriteLine($"[MainViewModel] LoadInitial: Loaded and cached {photos.Count} photos");
        }
        catch (UnsplashRateLimitException ex)
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = ex.Message;
            });
            Debug.WriteLine($"[MainViewModel] LoadInitial: Rate limit exceeded");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API Key"))
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = "Please configure your Unsplash API Key in Settings.";
            });
            Debug.WriteLine($"[MainViewModel] LoadInitial: API Key not configured");
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                StatusMessage = $"Error loading photos: {ex.Message}";
            });
            Debug.WriteLine($"[MainViewModel] LoadInitial ERROR: {ex}");
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() => 
            {
                IsBusy = false;
            });
        }
    }

    /// <summary>
    /// Manual reload - clear cache and fetch fresh photos
    /// </summary>
    private async Task LoadPhotosAsync()
    {
        IsBusy = true;
        StatusMessage = "Fetching fresh photos from Unsplash...";

        try
        {
            var settings = _settingsService.Load();
            Debug.WriteLine($"[MainViewModel] LoadPhotos: Loading 5 photos, query='{settings.SearchQuery}'");

            var photos = await _unsplashService.GetPhotosAsync(count: 5, page: 1, query: settings.SearchQuery);

            Photos.Clear();
            foreach (var photo in photos)
                Photos.Add(photo);

            CurrentPage = 1;
            StatusMessage = $"Loaded {photos.Count} landscape photos.";
            
            // Update cache
            await _cacheService.SaveCacheAsync(photos.ToList(), 1);
            Debug.WriteLine($"[MainViewModel] LoadPhotos: Loaded and cached {photos.Count} photos");
        }
        catch (UnsplashRateLimitException ex)
        {
            StatusMessage = ex.Message;
            Debug.WriteLine($"[MainViewModel] LoadPhotos: Rate limit exceeded");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API Key"))
        {
            StatusMessage = "Please configure your Unsplash API Key in Settings.";
            Debug.WriteLine($"[MainViewModel] LoadPhotos: API Key not configured");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading photos: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] LoadPhotos ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load more photos (next page) and append to current collection
    /// </summary>
    private async Task LoadMorePhotosAsync()
    {
        IsLoadingMore = true;

        try
        {
            var settings = _settingsService.Load();
            var nextPage = CurrentPage + 1;
            
            Debug.WriteLine($"[MainViewModel] LoadMore: Loading page {nextPage}, query='{settings.SearchQuery}'");

            var newPhotos = await _unsplashService.GetPhotosAsync(count: 5, page: nextPage, query: settings.SearchQuery);

            if (newPhotos.Count > 0)
            {
                // Append new photos to existing collection
                foreach (var photo in newPhotos)
                {
                    Photos.Add(photo);
                }

                CurrentPage = nextPage;
                StatusMessage = $"Loaded {newPhotos.Count} more photos. Total: {Photos.Count} photos.";
                
                // Update cache with accumulated photos
                await _cacheService.UpdateCacheWithNewPhotosAsync(newPhotos.ToList(), CurrentPage);
                Debug.WriteLine($"[MainViewModel] LoadMore: Added {newPhotos.Count} photos, total now {Photos.Count}");
            }
            else
            {
                StatusMessage = "No more photos available.";
                Debug.WriteLine($"[MainViewModel] LoadMore: No more photos returned");
            }
        }
        catch (UnsplashRateLimitException ex)
        {
            StatusMessage = ex.Message;
            Debug.WriteLine($"[MainViewModel] LoadMore: Rate limit exceeded");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading more photos: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] LoadMore ERROR: {ex}");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private async Task SetLockScreenAsync(object? parameter)
    {
        var photo = parameter as UnsplashPhoto ?? SelectedPhoto;
        if (photo is null)
        {
            StatusMessage = "Please select a photo first.";
            Debug.WriteLine($"[MainViewModel] SetLockScreen: No photo selected");
            return;
        }

        IsBusy = true;
        StatusMessage = $"Setting lock screen: \"{photo.DisplayDescription}\"...";

        try
        {
            Debug.WriteLine($"[MainViewModel] ══════════════════════════════════════");
            Debug.WriteLine($"[MainViewModel] SetLockScreen START");
            Debug.WriteLine($"[MainViewModel] Photo ID: {photo.Id}");
            Debug.WriteLine($"[MainViewModel] Description: {photo.DisplayDescription}");
            Debug.WriteLine($"[MainViewModel] Photographer: {photo.User.Name}");
            Debug.WriteLine($"[MainViewModel] Full URL: {photo.Urls.Full}");

            var success = await LockScreenService.DownloadAndSetLockScreenAsync(_unsplashService, photo);

            if (success)
            {
                StatusMessage = $"Lock screen set! Photo by {photo.User.Name}.";
                Debug.WriteLine($"[MainViewModel] SetLockScreen SUCCESS");
            }
            else
            {
                StatusMessage = "Failed to set lock screen. Check Debug output for details.";
                Debug.WriteLine($"[MainViewModel] SetLockScreen returned false");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] SetLockScreen EXCEPTION:");
            Debug.WriteLine($"[MainViewModel] Type: {ex.GetType().FullName}");
            Debug.WriteLine($"[MainViewModel] Message: {ex.Message}");
            Debug.WriteLine($"[MainViewModel] StackTrace: {ex.StackTrace}");
            if (ex.InnerException is not null)
            {
                Debug.WriteLine($"[MainViewModel] Inner: {ex.InnerException.GetType().FullName}");
                Debug.WriteLine($"[MainViewModel] Inner Message: {ex.InnerException.Message}");
                Debug.WriteLine($"[MainViewModel] Inner StackTrace: {ex.InnerException.StackTrace}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RandomLockScreenAsync()
    {
        IsBusy = true;
        StatusMessage = "Fetching a random landscape wallpaper...";

        try
        {
            var settings = _settingsService.Load();
            Debug.WriteLine($"[MainViewModel] RandomLockScreen: query='{settings.SearchQuery}'");

            var photo = await _unsplashService.GetRandomPhotoAsync(settings.SearchQuery);

            if (photo is null)
            {
                StatusMessage = "Could not fetch a random photo.";
                Debug.WriteLine($"[MainViewModel] RandomLockScreen: API returned null");
                return;
            }

            Debug.WriteLine($"[MainViewModel] RandomLockScreen: Got photo {photo.Id} by {photo.User.Name}");

            StatusMessage = $"Setting random wallpaper: \"{photo.DisplayDescription}\"...";
            var success = await LockScreenService.DownloadAndSetLockScreenAsync(_unsplashService, photo);

            if (success)
            {
                StatusMessage = $"Random lock screen set! Photo by {photo.User.Name}.";
                Debug.WriteLine($"[MainViewModel] RandomLockScreen SUCCESS");
            }
            else
            {
                StatusMessage = "Failed to set lock screen. Check Debug output for details.";
                Debug.WriteLine($"[MainViewModel] RandomLockScreen returned false");
            }
        }
        catch (UnsplashRateLimitException ex)
        {
            StatusMessage = ex.Message;
            Debug.WriteLine($"[MainViewModel] RandomLockScreen: Rate limit exceeded");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API Key"))
        {
            StatusMessage = "Please configure your Unsplash API Key in Settings.";
            Debug.WriteLine($"[MainViewModel] API Key not configured");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Debug.WriteLine($"[MainViewModel] RandomLockScreen EXCEPTION:");
            Debug.WriteLine($"[MainViewModel] Type: {ex.GetType().FullName}");
            Debug.WriteLine($"[MainViewModel] Message: {ex.Message}");
            Debug.WriteLine($"[MainViewModel] StackTrace: {ex.StackTrace}");
            if (ex.InnerException is not null)
            {
                Debug.WriteLine($"[MainViewModel] Inner: {ex.InnerException.GetType().FullName}");
                Debug.WriteLine($"[MainViewModel] Inner Message: {ex.InnerException.Message}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Silent mode: fetch random photo, set lock screen, clean up, exit.
    /// </summary>
    public static async Task RunSilentAsync(SettingsService settingsService)
    {
        if (!settingsService.HasApiKey)
        {
            Debug.WriteLine("[MainViewModel] Silent mode: No API key configured, exiting.");
            return;
        }

        using var unsplashService = new UnsplashService(settingsService);

        try
        {
            var settings = settingsService.Load();
            Debug.WriteLine($"[MainViewModel] Silent mode: Fetching random photo, query='{settings.SearchQuery}'");

            var photo = await unsplashService.GetRandomPhotoAsync(settings.SearchQuery);

            if (photo is not null)
            {
                Debug.WriteLine($"[MainViewModel] Silent mode: Got photo {photo.Id}, setting lock screen...");
                var result = await LockScreenService.DownloadAndSetLockScreenAsync(unsplashService, photo);
                Debug.WriteLine($"[MainViewModel] Silent mode: Result = {result}");
            }
            else
            {
                Debug.WriteLine("[MainViewModel] Silent mode: No photo returned from API.");
            }
        }
        catch (UnsplashRateLimitException ex)
        {
            Debug.WriteLine($"[MainViewModel] Silent mode: Rate limit exceeded - {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Silent mode EXCEPTION: {ex}");
        }
    }
}